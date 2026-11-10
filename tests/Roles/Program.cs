using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using PsyPath.Api;
using ClientUser = PsyPath.Services.ApiUser;

int checks = 0;
void Check(bool ok, string message)
{
    if (!ok) throw new Exception(message);
    checks++;
    Console.WriteLine("PASS " + message);
}
var manager = new ClientUser(2, "editor", "Editor", "manager");
Check(manager.CanEditContent && manager.IsManager && !manager.IsAdmin, "client manager edits content without admin privileges");
Check(new ClientUser(1, "admin", "Admin", "admin").CanEditContent, "client administrator retains content access");
Check(!new ClientUser(3, "user", "User", "user").CanEditContent, "client regular user has no editing access");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = Array.Empty<string>(), ContentRootPath = AppContext.BaseDirectory });
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddSingleton(NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=roles_test;Username=test;Timeout=1"));
builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestIdentity>("Test", _ => { });
builder.Services.AddRoleAuthorization();
builder.Services.RemoveAll<IUserRoles>();
var roles = new TestRoles();
builder.Services.AddSingleton<IUserRoles>(roles);
await using var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
// Probes run after the real authorization middleware. They avoid a database
// for read-only data handlers; role changes always execute the real endpoint.
app.Use(async (context, next) =>
{
    if (context.Request.Headers.ContainsKey("X-Probe") && context.GetEndpoint() != null)
        context.Response.StatusCode = 204;
    else await next(context);
});
app.MapAdmin();
await app.StartAsync();
using var http = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
async Task<HttpStatusCode> Request(HttpMethod method, string path, string? id, string? json = null, bool probe = false, string claim = "user")
{
    using var req = new HttpRequestMessage(method, path);
    if (id != null) { req.Headers.Add("X-Test-User", id); req.Headers.Add("X-Token-Role", claim); }
    if (probe) req.Headers.Add("X-Probe", "1");
    if (json != null) req.Content = new StringContent(json, Encoding.UTF8, "application/json");
    using var response = await http.SendAsync(req);
    return response.StatusCode;
}
foreach (var (method, path) in new[] { (HttpMethod.Get, "/api/admin/users"), (HttpMethod.Get, "/api/admin/users/3"),
    (HttpMethod.Get, "/api/admin/stats"), (HttpMethod.Put, "/api/admin/users/3/role") })
{
    string? body = method == HttpMethod.Put ? """{"role":"manager"}""" : null;
    Check(await Request(method, path, "2", body, probe: true, claim: "admin") == HttpStatusCode.Forbidden,
        "manager denied " + method + " " + path + " even with old admin claim");
    Check(await Request(method, path, "1", body, probe: true) == HttpStatusCode.NoContent,
        "current administrator allowed " + method + " " + path);
}
foreach (var (method, path) in new[] { (HttpMethod.Put, "/api/admin/content/reference"),
    (HttpMethod.Get, "/api/admin/content/reference/history"), (HttpMethod.Post, "/api/admin/content/reference/rollback/1") })
{
    string? body = method == HttpMethod.Put ? """{"version":1,"data":{"entries":[]}}""" : null;
    Check(await Request(method, path, "2", body, probe: true) == HttpStatusCode.NoContent, "manager allowed " + method + " " + path);
    Check(await Request(method, path, "1", body, probe: true) == HttpStatusCode.NoContent, "administrator retains " + method + " " + path);
    Check(await Request(method, path, "3", body, probe: true, claim: "admin") == HttpStatusCode.Forbidden, "regular user denied " + method + " " + path);
    Check(await Request(method, path, null, body, probe: true) == HttpStatusCode.Unauthorized, "anonymous denied " + method + " " + path);
}
Check(await Request(HttpMethod.Put, "/api/admin/content/reference", "2", """{"version":1,"data":{}}""") == HttpStatusCode.BadRequest,
    "manager reaches real content validation, not just permission probe");
Check(await Request(HttpMethod.Put, "/api/admin/users/3/role", "2", """{"role":"manager"}""") == HttpStatusCode.Forbidden,
    "manager cannot promote another user");
Check(roles.Accounts[3].Role == "user", "denied role change leaves account unchanged");
Check(await Request(HttpMethod.Put, "/api/admin/users/3/role", "1", """{"role":"manager"}""") == HttpStatusCode.OK,
    "administrator promotes user to manager");
Check(await Request(HttpMethod.Get, "/api/admin/content/reference/history", "3", probe: true) == HttpStatusCode.NoContent,
    "promotion works with previously issued regular-user token");
Check(await Request(HttpMethod.Put, "/api/admin/users/3/role", "1", """{"role":"user"}""") == HttpStatusCode.OK,
    "administrator revokes manager role");
Check(await Request(HttpMethod.Get, "/api/admin/content/reference/history", "3", probe: true, claim: "manager") == HttpStatusCode.Forbidden,
    "revoked manager token loses access immediately");
Check(await Request(HttpMethod.Put, "/api/admin/users/3/role", "1", """{"role":"admin"}""") == HttpStatusCode.BadRequest,
    "manager endpoint cannot grant administrator role");
Check(await Request(HttpMethod.Put, "/api/admin/users/1/role", "1", """{"role":"user"}""") == HttpStatusCode.Conflict,
    "administrator cannot accidentally demote self through manager endpoint");
Check(await Request(HttpMethod.Put, "/api/admin/users/999/role", "1", """{"role":"manager"}""") == HttpStatusCode.NotFound,
    "missing account returns 404");
roles.Accounts[2] = new AccountAccess("manager", true);
Check(await Request(HttpMethod.Get, "/api/admin/content/reference/history", "2", probe: true) == HttpStatusCode.Forbidden,
    "blocked manager has no content access");
roles.Accounts.Remove(2);
Check(await Request(HttpMethod.Get, "/api/admin/content/reference/history", "2", probe: true) == HttpStatusCode.Forbidden,
    "deleted account cannot use old token");
Check(await Request(HttpMethod.Get, "/api/admin/users", "not-an-id", probe: true) == HttpStatusCode.Forbidden,
    "malformed subject cannot access administration");
await app.StopAsync();
Console.WriteLine($"Role checks passed: {checks}");

sealed class TestRoles : IUserRoles
{
    public Dictionary<long, AccountAccess> Accounts { get; } = new()
    { [1] = new("admin", false), [2] = new("manager", false), [3] = new("user", false) };
    public Task<AccountAccess?> Find(long id, CancellationToken cancel) => Task.FromResult(Accounts.GetValueOrDefault(id));
    public Task<ManagerChange> SetManager(long id, bool manager, CancellationToken cancel)
    {
        if (!Accounts.TryGetValue(id, out var current)) return Task.FromResult(ManagerChange.NotFound);
        if (current.Role == "admin") return Task.FromResult(ManagerChange.Administrator);
        Accounts[id] = current with { Role = manager ? "manager" : "user" };
        return Task.FromResult(ManagerChange.Updated);
    }
}
sealed class TestIdentity(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var id)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity(new[] { new Claim("sub", id.ToString()),
            new Claim(ClaimTypes.Role, Request.Headers["X-Token-Role"].ToString()) }, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
