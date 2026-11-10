using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using PsyPath.Services;

void Check(bool ok, string message)
{
    if (!ok) throw new Exception(message);
    Console.WriteLine("PASS " + message);
}

var project = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "AppProject.xml"));
AppInfo.Current.Version = Version.Parse(project.Descendants("ApplicationDisplayVersion").Single().Value);
var notes = await ReleaseNotes.Load();
string json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "changelog.json"));
using var metadata = JsonDocument.Parse(json);
Check(metadata.RootElement.GetProperty("version").GetString() == AppInfo.Current.Version.ToString(), "bundled release matches application version");
Check(notes.Count == 3 && notes[0].Version == AppInfo.Current.Version, "offline history starts with installed release");
Check(notes[0].Date == new DateOnly(2026, 11, 11), "release date is preserved");
Check(notes[0].Changes.Count == 4 && notes[0].Changes.Any(x => x.Contains("синхронизации")), "new release includes crash fixes");
Check(ReleaseNotes.ParseHistory(json, new Version(1, 4)).Single().Version == new Version(1, 4, 0), "future releases are hidden and short versions normalize");
Check(ReleaseNotes.ParseHistory("null", new Version(1, 5, 1)).Count == 0, "invalid root does not crash history");
var cleaned = ReleaseNotes.ParseHistory("""
{"version":"1.5.1","date":"not-a-date","changelog":["  change  ",null,1," "],
 "history":[null,{"version":"oops"},{"version":"1.5.1"},{"version":"1.4"}]}
""", new Version(1, 5, 1));
Check(cleaned.Count == 2 && cleaned[0].Changes.SequenceEqual(new[] { "change" }) && cleaned[0].Date == null,
    "bad entries, duplicates and blank notes are ignored");
Check(ReleaseNotes.Format(Array.Empty<string>()).Contains("не опубликовано"), "missing description has readable fallback");

// HTTP is fully replaced: exercise production Updater.Check without GitHub access.
var transport = new ReleaseTransport { Json = json };
var client = (HttpClient)typeof(Updater).GetField("Http", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client, transport);
AppInfo.Current.Version = new Version(1, 5, 0);
var update = await Updater.Check();
Check(update.Ok && update.Update?.Version == new Version(1, 5, 1), "previous version detects new release");
Check(update.Update!.Changelog.SequenceEqual(notes[0].Changes), "update offer uses same changes as offline history");
string prompt = Updater.Prompt(update.Update);
Check(prompt.Contains("Что нового:") && notes[0].Changes.All(x => prompt.Contains("• " + x)), "update prompt shows every change before installation");
AppInfo.Current.Version = new Version(1, 5, 1);
Check((await Updater.Check()) is { Ok: true, Update: null }, "installed release is not offered again");
AppInfo.Current.Version = new Version(1, 4, 0);
transport.Json = """{"version":"1.5.0","apk":"https://example.invalid/app.apk"}""";
var legacy = await Updater.Check();
Check(legacy.Ok && legacy.Update != null && Updater.Prompt(legacy.Update).Contains("не опубликовано"), "older manifests without changelog remain supported");
Console.WriteLine("Update checks passed.");

sealed class ReleaseTransport : HttpMessageHandler
{
    public string Json = "{}";
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json) });
}

public static class AppInfo
{
    public static AppVersion Current { get; } = new();
    public sealed class AppVersion { public Version Version { get; set; } = new(1, 5, 1); }
}
public static class FileSystem
{
    public static string CacheDirectory => Path.GetTempPath();
    public static Task<Stream> OpenAppPackageFileAsync(string name) => Task.FromResult<Stream>(File.OpenRead(Path.Combine(AppContext.BaseDirectory, name)));
}
public record ReadOnlyFile(string Path, string ContentType);
public record OpenFileRequest(string Title, ReadOnlyFile File);
public static class Launcher
{
    public static TestLauncher Default { get; } = new();
    public sealed class TestLauncher { public Task OpenAsync(OpenFileRequest request) => throw new NotSupportedException("Installer is not used in these checks"); }
}
