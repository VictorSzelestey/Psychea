using System.Text.Json;

namespace PsyPath.Services;

public record AppUpdate(Version Version, string ApkUrl, IReadOnlyList<string> Changelog);

// Ok = false — проверить не удалось; Update = null — установлена последняя версия
public record UpdateCheck(bool Ok, AppUpdate? Update, string Error = "");

// обновление приложения из ветки main на GitHub: Android сам спросит подтверждение и сверит подпись APK
public static class Updater
{
    // version.json и APK меняются только при релизе в main
    const string LatestUrl = "https://raw.githubusercontent.com/VictorSzelestey/Psychea/main/releases/version.json";
    const string ApkType = "application/vnd.android.package-archive";
    const int BufferSize = 81920;

    static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(10);
    static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(5);
    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        // сроки задаются для каждого запроса отдельно: скачивание идёт намного дольше проверки
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        // без User-Agent GitHub отклоняет запрос
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PsyPath-App");
        return client;
    }

    // 1.2 и 1.2.0 — одна и та же версия, а для Version первая меньше второй
    static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (!Version.TryParse((tag ?? "").TrimStart('v', 'V'), out var parsed)) return false;
        version = Normalize(parsed);
        return true;
    }

    // changelog в version.json необязателен: без него окно обновления покажет только номер версии
    static List<string> ReadChangelog(JsonElement root)
    {
        if (!root.TryGetProperty("changelog", out var items) || items.ValueKind != JsonValueKind.Array) return new();
        return items.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString() ?? "")
            .Where(x => x.Length > 0)
            .ToList();
    }

    // текст окна «Доступна новая версия»
    public static string Prompt(AppUpdate update)
    {
        string news = update.Changelog.Count == 0
            ? ""
            : "Что нового:\n" + string.Join("\n", update.Changelog.Select(x => "• " + x)) + "\n\n";
        return $"Доступна версия {update.Version}.\n\n{news}Скачать и установить?";
    }

    public static async Task<UpdateCheck> Check()
    {
        try
        {
            using var timeout = new CancellationTokenSource(CheckTimeout);
            using var req = new HttpRequestMessage(HttpMethod.Get, LatestUrl);
            using var resp = await Http.SendAsync(req, timeout.Token);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new UpdateCheck(false, null, "Обновления пока не опубликованы");
            if (!resp.IsSuccessStatusCode)
                return new UpdateCheck(false, null, "GitHub сейчас не отвечает, попробуй позже");

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(timeout.Token));
            var root = doc.RootElement;
            if (!TryParseTag(root.GetProperty("version").GetString(), out var latest))
                return new UpdateCheck(false, null, "Не удалось разобрать номер версии");

            if (latest <= Normalize(AppInfo.Current.Version))
                return new UpdateCheck(true, null);

            string url = root.GetProperty("apk").GetString() ?? "";
            if (url.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)
                && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return new UpdateCheck(true, new AppUpdate(latest, url, ReadChangelog(root)));
            return new UpdateCheck(false, null, "В новой версии нет файла APK");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return new UpdateCheck(false, null, "Нет связи с GitHub");
        }
    }

    // скачивает APK и открывает системный установщик; возвращает текст ошибки или null
    public static async Task<string?> DownloadAndInstall(AppUpdate update, Action<int> progress)
    {
        string path = Path.Combine(FileSystem.CacheDirectory, "update.apk");
        try
        {
            using var timeout = new CancellationTokenSource(DownloadTimeout);
            using var resp = await Http.GetAsync(update.ApkUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!resp.IsSuccessStatusCode) return "Не удалось скачать обновление";

            long total = resp.Content.Headers.ContentLength ?? 0;
            await using (var source = await resp.Content.ReadAsStreamAsync(timeout.Token))
            await using (var file = File.Create(path))
            {
                var buffer = new byte[BufferSize];
                long done = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, timeout.Token)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                    done += read;
                    if (total > 0) progress((int)(done * 100 / total));
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            return "Не удалось скачать обновление";
        }

        try
        {
            await Launcher.Default.OpenAsync(new OpenFileRequest("Обновление Психеи", new ReadOnlyFile(path, ApkType)));
            return null;
        }
        catch (Exception)
        {
            return "Не удалось открыть установщик";
        }
    }
}
