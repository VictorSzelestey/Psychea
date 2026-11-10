using System.Net;
using System.Reflection;
using System.Text.Json;
using PsyPath.Models;
using PsyPath.Services;
using PsyPath.UI;
using PsyPath.Views;

int failures = 0;
void Check(bool ok, string name)
{
    Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
    if (!ok) failures++;
}
async Task Run(string name, Func<Task> test)
{
    try { await test(); }
    catch (Exception ex) { failures++; Console.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}"); }
}
IEnumerable<Btn> Buttons(View? view) => view switch
{
    Btn b => new[] { b },
    VerticalStackLayout s => s.Children.SelectMany(Buttons),
    Grid g => g.Children.SelectMany(Buttons),
    _ => Array.Empty<Btn>()
};
Btn Button(FlashcardsPage p, string prefix) => Buttons(p.Bottom).Single(b => b.Text.StartsWith(prefix));
List<RefEntry> Queue(FlashcardsPage p) => (List<RefEntry>)typeof(FlashcardsPage).GetField("queue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(p)!;
void PrepareCards(int count)
{
    Api.Logout();
    AppState.ResetAll();
    var entries = AppState.Reference;
    entries.Clear();
    for (int i = 0; i < count; i++) entries.Add(new RefEntry { Term = "term" + i });
}
await Run("last card repeated tap", () =>
{
    PrepareCards(1);
    var p = new FlashcardsPage();
    Button(p, "ПОКАЗАТЬ").Click();
    var good = Button(p, "ХОРОШО");
    good.Click(); good.Click();
    Check(Queue(p).Count == 0 && AppState.P.TotalXp == 2, "last card is completed once without crash or duplicate XP");
    return Task.CompletedTask;
});
await Run("stale rating", () =>
{
    PrepareCards(2);
    var p = new FlashcardsPage();
    Button(p, "ПОКАЗАТЬ").Click();
    var oldGood = Button(p, "ХОРОШО");
    var oldAgain = Button(p, "СНОВА");
    oldGood.Click(); oldAgain.Click();
    Check(Queue(p).Count == 1 && Queue(p)[0].Term == "term1" && AppState.P.Cards.Count == 1,
        "stale rating cannot skip next card or overwrite previous rating");
    return Task.CompletedTask;
});
await Run("single card Again", () =>
{
    PrepareCards(1);
    var p = new FlashcardsPage();
    Button(p, "ПОКАЗАТЬ").Click();
    var oldGood = Button(p, "ХОРОШО");
    Button(p, "СНОВА").Click(); oldGood.Click();
    Check(Queue(p).Count == 1 && AppState.P.Cards["term0"].Interval == 0,
        "old buttons cannot answer a requeued instance of the same card");
    Button(p, "ПОКАЗАТЬ").Click(); Button(p, "ХОРОШО").Click();
    Check(Queue(p).Count == 0 && AppState.P.TotalXp == 0, "Again card can be completed without first-try XP");
    return Task.CompletedTask;
});
await Run("late flip completion", async () =>
{
    PrepareCards(2);
    var p = new FlashcardsPage();
    var oldShow = Button(p, "ПОКАЗАТЬ");
    Button(p, "ПОКАЗАТЬ").Click();
    Anim.PendingZoom = new(TaskCreationOptions.RunContinuationsAsynchronously);
    oldShow.Click();
    Button(p, "ХОРОШО").Click();
    var before = p.Bottom;
    var pending = Anim.PendingZoom;
    Anim.PendingZoom = null;
    pending.SetResult();
    await Task.Delay(50);
    Check(ReferenceEquals(before, p.Bottom) && Buttons(p.Bottom).Any(b => b.Text == "ПОКАЗАТЬ"),
        "late flip cannot reveal the next card");
});
// Replace only the transport of Api's actual HttpClient. Reflection stays in tests;
// requests never leave the process and all production response/queue code executes.
var transport = new TestTransport();
var client = (HttpClient)typeof(Api).GetField("Http", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client, transport);
await Run("Android socket failure", async () =>
{
    transport.Failure = new WebException("Socket closed");
    var r = await Api.Me();
    Check(!r.Ok && r.Status == 0, "WebException becomes offline result instead of escaping");
});
await Run("progress retry", async () =>
{
    Preferences.Default.Clear();
    transport.Failure = null;
    await Api.Login("local-test", "local-test");
    Sync.MarkDirty();
    transport.Failure = new WebException("Socket closed");
    await Sync.Flush();
    Check(Sync.Pending == 1, "failed progress upload remains pending");
    transport.Failure = null;
    await Sync.Flush();
    Check(Sync.Pending == 0, "pending progress uploads after connection recovers");
});
await Run("outbox retry", async () =>
{
    Preferences.Default.Set("outbox", "[{\"Path\":\"/api/me/lessons\",\"Json\":\"{}\"}]");
    transport.Failure = new WebException("Socket closed");
    await Sync.Flush();
    Check(Sync.Pending == 1, "failed lesson upload stays in outbox");
    transport.Failure = null;
    await Sync.Flush();
    Check(Sync.Pending == 0, "queued lesson uploads after connection recovers");
});
foreach (var failure in new Exception[] { new IOException("read interrupted"), new OperationCanceledException(), new HttpRequestException("offline") })
    await Run(failure.GetType().Name, async () =>
    {
        transport.Failure = failure;
        var r = await Api.Me();
        Check(!r.Ok && r.Status == 0, failure.GetType().Name + " is treated as offline");
    });
Api.Logout();
Console.WriteLine($"Crash recovery checks: {failures} failure(s)");
return failures == 0 ? 0 : 1;

sealed class TestTransport : HttpMessageHandler
{
    public Exception? Failure;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Failure != null) return Task.FromException<HttpResponseMessage>(Failure);
        string json = request.RequestUri!.AbsolutePath == "/api/auth/login"
            ? JsonSerializer.Serialize(new { token = "local-test", user = new { id = 1, login = "local-test", displayName = "test", role = "user" } })
            : "{}";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}
