using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PsyPath.Models;

namespace PsyPath.Bot;

public record ApiUser(long Id, string Login, string DisplayName, string Role)
{
    public bool IsAdmin => Role == "admin";
}

// ответ сервера: Status 0 — нет связи; Error — текст, который можно показать пользователю
public record ApiResult(bool Ok, int Status, string Error = "", JsonElement Body = default);

// тот же API, что у приложения; токен у каждого чата свой, поэтому передаётся в каждый вызов
public class PsyApi(string baseUrl)
{
    readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
    static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public string BaseUrl { get; } = baseUrl.TrimEnd('/');

    public Task<ApiResult> Login(string login, string password) =>
        Send(null, HttpMethod.Post, "/api/auth/login", new { login, password });

    public Task<ApiResult> Register(string login, string password, string displayName) =>
        Send(null, HttpMethod.Post, "/api/auth/register", new { login, password, displayName });

    public Task<ApiResult> Me(string token) => Send(token, HttpMethod.Get, "/api/me");

    public Task<ApiResult> DeleteAccount(string token, string password) =>
        Send(token, HttpMethod.Post, "/api/me/delete", new { password });

    // как в приложении: ProgressData целиком и с теми же именами полей
    public Task<ApiResult> PushProgress(string token, ProgressData p) =>
        SendRaw(token, HttpMethod.Put, "/api/me/progress", JsonSerializer.Serialize(p));

    public Task<ApiResult> PostLesson(string token, string lessonId, int accuracy, int mistakes, int xp) =>
        Send(token, HttpMethod.Post, "/api/me/lessons", new { lessonId, accuracy, mistakes, xp });

    public Task<ApiResult> PostQuestionnaire(string token, string questionnaireId, string headline, object scores) =>
        Send(token, HttpMethod.Post, "/api/me/questionnaires", new { questionnaireId, headline, scores });

    public Task<ApiResult> ContentVersions() => Send(null, HttpMethod.Get, "/api/content/versions");
    public Task<ApiResult> Content(string name) => Send(null, HttpMethod.Get, $"/api/content/{name}");

    public static ApiUser? ReadUser(JsonElement user) => user.Deserialize<ApiUser>(Web);

    Task<ApiResult> Send(string? token, HttpMethod method, string path, object? body = null) =>
        SendRaw(token, method, path, body == null ? null : JsonSerializer.Serialize(body, Web));

    async Task<ApiResult> SendRaw(string? token, HttpMethod method, string path, string? json)
    {
        try
        {
            using var req = new HttpRequestMessage(method, BaseUrl + path);
            if (json != null) req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            if (token != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var resp = await http.SendAsync(req);
            var text = await resp.Content.ReadAsStringAsync();
            JsonElement body = default;
            if (text.Length > 0)
            {
                using var doc = JsonDocument.Parse(text);
                body = doc.RootElement.Clone();
            }

            int status = (int)resp.StatusCode;
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
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new ApiResult(false, 0, "Нет связи с сервером Психеи, попробуй позже");
        }
    }
}
