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

    static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(10);
    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        // срок задаётся для каждого запроса отдельно
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

    // текст окна «Доступна новая версия»
    public static string Prompt(AppUpdate update)
    {
        string news = "Что нового:\n" + ReleaseNotes.Format(update.Changelog) + "\n\n";
        return $"Доступна версия {update.Version}.\n\n{news}Скачать и установить?";
    }

    public static async Task<UpdateCheck> Check()
    {
        // APK ставится только на Android; на iPhone новая сборка приходит переустановкой
        if (DeviceInfo.Platform != DevicePlatform.Android)
            return new UpdateCheck(false, null, "На iPhone обновление — переустановкой новой сборки");

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
                return new UpdateCheck(true, new AppUpdate(latest, url, ReleaseNotes.ReadChanges(root)));
            return new UpdateCheck(false, null, "В новой версии нет файла APK");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return new UpdateCheck(false, null, "Нет связи с GitHub");
        }
    }

    // идущая или скачанная, но ещё не установленная загрузка переживает сворачивание и перезапуск приложения
    const string DownloadIdKey = "update_download_id";
    const string DownloadVersionKey = "update_download_version";
    // установщик после возвращения в приложение открывается один раз; дальше — кнопкой «Обновить» в настройках
    const string OfferedKey = "update_download_offered";

    // приложение свёрнуто: Android не даст открыть установщик из фона, его откроет Resume
    public static bool InBackground { get; set; }

    // APK ещё качается (не скачан и не сорвался)
    public static bool Downloading
    {
        get
        {
#if ANDROID
            long id = Preferences.Default.Get(DownloadIdKey, -1L);
            return id >= 0 && Droid.ApkDownload.Query(id).State is Droid.ApkDownload.State.Running or Droid.ApkDownload.State.Waiting;
#else
            return false;
#endif
        }
    }

    // скачивает APK системным загрузчиком и открывает установщик; возвращает текст ошибки или null.
    // Приложение можно свернуть: загрузка идёт дальше, прогресс — в шторке
    public static async Task<string?> DownloadAndInstall(AppUpdate update, Action<int> progress)
    {
#if ANDROID
        long id = Preferences.Default.Get(DownloadIdKey, -1L);
        // та же версия уже качается — следим за ней, а не начинаем заново
        if (id < 0 || Preferences.Default.Get(DownloadVersionKey, "") != update.Version.ToString())
        {
            if (id >= 0) Droid.ApkDownload.Cancel(id);
            try { id = Droid.ApkDownload.Start(update.ApkUrl, $"Психея {update.Version}"); }
            catch (Exception) { return "Не удалось начать скачивание"; }
            Preferences.Default.Set(DownloadIdKey, id);
            Preferences.Default.Set(DownloadVersionKey, update.Version.ToString());
            Preferences.Default.Remove(OfferedKey);
        }

        while (true)
        {
            var (state, percent) = Droid.ApkDownload.Query(id);
            if (state == Droid.ApkDownload.State.Failed)
            {
                Forget();
                return "Не удалось скачать обновление";
            }
            if (state == Droid.ApkDownload.State.Done)
                return InBackground ? null : await OpenInstaller();
            progress(percent);
            await Task.Delay(500);
        }
#else
        await Task.CompletedTask;
        return "Обновление из APK ставится только на Android";
#endif
    }

    // вернулись в приложение, а APK докачался, пока оно было свёрнуто или закрыто
    public static async Task Resume()
    {
#if ANDROID
        long id = Preferences.Default.Get(DownloadIdKey, -1L);
        if (id < 0) return;
        // уже стоит эта версия или новее — скачанный файл не нужен
        if (!Version.TryParse(Preferences.Default.Get(DownloadVersionKey, ""), out var v) || Normalize(v) <= Normalize(AppInfo.Current.Version))
        {
            Droid.ApkDownload.Cancel(id);
            Forget();
            return;
        }
        var (state, _) = Droid.ApkDownload.Query(id);
        if (state == Droid.ApkDownload.State.Done && !Preferences.Default.Get(OfferedKey, false))
        {
            Preferences.Default.Set(OfferedKey, true);
            await OpenInstaller();
        }
        else if (state == Droid.ApkDownload.State.Failed) Forget();
#else
        await Task.CompletedTask;
#endif
    }

    static void Forget()
    {
        Preferences.Default.Remove(DownloadIdKey);
        Preferences.Default.Remove(DownloadVersionKey);
        Preferences.Default.Remove(OfferedKey);
    }

#if ANDROID
    // установщику файл отдаётся из кэша: так MAUI открывает его через свой FileProvider.
    // Отметка о загрузке остаётся: если установку отменили, Resume предложит её снова
    static async Task<string?> OpenInstaller()
    {
        try
        {
            string path = Path.Combine(FileSystem.CacheDirectory, "update.apk");
            File.Copy(Droid.ApkDownload.FilePath, path, true);
            await Launcher.Default.OpenAsync(new OpenFileRequest("Обновление Психеи", new ReadOnlyFile(path, ApkType)));
            return null;
        }
        catch (Exception)
        {
            return "Не удалось открыть установщик";
        }
    }
#endif
}
