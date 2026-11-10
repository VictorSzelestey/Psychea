using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PsyPath.Models;

namespace PsyPath.Services;

public record ApiUser(long Id, string Login, string DisplayName, string Role)
{
    public bool IsAdmin => Role == "admin";
}

// ответ сервера: Status 0 — нет связи; Error — текст, который можно показать пользователю
public record ApiResult(bool Ok, int Status, string Error = "", JsonElement Body = default);

public static class Api
{
    // сервер в облаке НГТУ (публикация TCP 6354 → 5080); когда заработает домен,
    // лучше перейти на https://psypath.cloud.nstu.ru — там шифрование.
    // Для API на своём ПК адрес меняется на экране входа: эмулятор — http://10.0.2.2:5080
    const string DefaultUrl = "http://217.71.129.139:6354";

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    static string? token;

    public static ApiUser? User { get; private set; }
    public static bool LoggedIn => token != null && User != null;

    public static string BaseUrl => DefaultUrl;

    public static async Task RestoreSession()
    {
        try { token = await SecureStorage.Default.GetAsync("token"); }
        catch { token = null; }
        var json = Preferences.Default.Get("user", "");
        try { User = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<ApiUser>(json, Web); }
        catch { User = null; }
        if (User == null) token = null;
    }

    public static void Logout()
    {
        token = null;
        User = null;
        SecureStorage.Default.Remove("token");
        Preferences.Default.Remove("user");
    }

    // роль или имя могли поменяться на сервере
    public static void UpdateUser(JsonElement user)
    {
        User = user.Deserialize<ApiUser>(Web);
        Preferences.Default.Set("user", JsonSerializer.Serialize(User, Web));
    }

    public static async Task<ApiResult> Login(string login, string password)
    {
        var r = await Send(HttpMethod.Post, "/api/auth/login", new { login, password });
        if (r.Ok) await SetSession(r.Body);
        return r;
    }

    public static async Task<ApiResult> Register(string login, string password, string displayName)
    {
        var r = await Send(HttpMethod.Post, "/api/auth/register", new { login, password, displayName });
        if (r.Ok) await SetSession(r.Body);
        return r;
    }

    static async Task SetSession(JsonElement body)
    {
        token = body.GetProperty("token").GetString();
        UpdateUser(body.GetProperty("user"));
        await SecureStorage.Default.SetAsync("token", token!);
    }

    // новый токен вместо старого: пока приложение запускают, сессия не истекает
    public static async Task<ApiResult> RefreshSession()
    {
        var r = await Send(HttpMethod.Post, "/api/auth/refresh");
        if (r.Ok) await SetSession(r.Body);
        return r;
    }

    public static Task<ApiResult> Me() => Send(HttpMethod.Get, "/api/me");
    public static Task<ApiResult> Top() => Send(HttpMethod.Get, "/api/me/top");
    public static Task<ApiResult> SetTopHidden(bool hidden) => Send(HttpMethod.Put, "/api/me/top", new { hidden });
    public static Task<ApiResult> DeleteAccount(string password) => Send(HttpMethod.Post, "/api/me/delete", new { password });

    // ProgressData уходит как есть, сервер сам берёт из него числа для статистики
    public static Task<ApiResult> PushProgress(ProgressData p) =>
        SendRaw(HttpMethod.Put, "/api/me/progress", JsonSerializer.Serialize(p));

    // результаты уроков и опросников уходят через очередь Sync: тело собрано заранее
    public static Task<ApiResult> PostJson(string path, string json) => SendRaw(HttpMethod.Post, path, json);

    public static Task<ApiResult> ContentVersions() => Send(HttpMethod.Get, "/api/content/versions");
    public static Task<ApiResult> Content(string name) => Send(HttpMethod.Get, $"/api/content/{name}");

    public static Task<ApiResult> AdminStats() => Send(HttpMethod.Get, "/api/admin/stats");
    public static Task<ApiResult> AdminUsers() => Send(HttpMethod.Get, "/api/admin/users");
    public static Task<ApiResult> AdminUser(long id) => Send(HttpMethod.Get, $"/api/admin/users/{id}");
    public static Task<ApiResult> ContentHistory(string name) => Send(HttpMethod.Get, $"/api/admin/content/{name}/history");
    public static Task<ApiResult> Rollback(string name, int version) =>
        Send(HttpMethod.Post, $"/api/admin/content/{name}/rollback/{version}");

    // version — версия, которую админ открыл для правки
    public static Task<ApiResult> SaveContent(string name, int version, string dataJson) =>
        SendRaw(HttpMethod.Put, $"/api/admin/content/{name}", $"{{\"version\":{version},\"data\":{dataJson}}}");

    static Task<ApiResult> Send(HttpMethod method, string path, object? body = null) =>
        SendRaw(method, path, body == null ? null : JsonSerializer.Serialize(body, Web));

    static async Task<ApiResult> SendRaw(HttpMethod method, string path, string? json)
    {
        try
        {
            using var req = new HttpRequestMessage(method, BaseUrl + path);
            if (json != null) req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            if (token != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var resp = await Http.SendAsync(req);
            var text = await resp.Content.ReadAsStringAsync();
            JsonElement body = default;
            if (text.Length > 0)
            {
                using var doc = JsonDocument.Parse(text);
                body = doc.RootElement.Clone();
            }

            int status = (int)resp.StatusCode;
            if (!resp.IsSuccessStatusCode) CrashLog.Write("API", $"{method} {path}: HTTP {status}");
            if (resp.IsSuccessStatusCode) return new ApiResult(true, status, Body: body);

            string error = resp.StatusCode switch
            {
                HttpStatusCode.TooManyRequests => "Слишком много попыток, подожди минуту",
                _ when body.ValueKind == JsonValueKind.Object && body.TryGetProperty("error", out var e) => e.GetString() ?? "",
                HttpStatusCode.Unauthorized => "Сессия истекла, войди заново",
                HttpStatusCode.Forbidden => "Нет доступа",
                _ => $"Ошибка сервера ({status})"
            };
            return new ApiResult(false, status, error);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            CrashLog.Write("API", ex, $"{method} {path}");
            return new ApiResult(false, 0, "Нет связи с сервером");
        }
    }
}
