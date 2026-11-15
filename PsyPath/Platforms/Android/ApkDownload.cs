using Android.App;
using Android.Content;

namespace PsyPath.Droid;

// скачивание APK системным загрузчиком Android: файл докачивается, даже если приложение свернули или закрыли,
// прогресс и «Загрузка завершена» видны в шторке, нажатие на уведомление открывает установщик
public static class ApkDownload
{
    const string FileName = "Psychea-update.apk";

    public enum State { Waiting, Running, Done, Failed }

    static DownloadManager Manager =>
        (DownloadManager)Android.App.Application.Context.GetSystemService(Context.DownloadService)!;

    // папка приложения в Download: разрешения на запись не нужны, файл удаляется вместе с приложением
    public static string FilePath =>
        Path.Combine(Android.App.Application.Context.GetExternalFilesDir(Android.OS.Environment.DirectoryDownloads)!.AbsolutePath, FileName);

    public static long Start(string url, string title)
    {
        // старый файл с тем же именем загрузчик не перезапишет, а сохранит рядом как «-1.apk»
        if (File.Exists(FilePath)) File.Delete(FilePath);
        var request = new DownloadManager.Request(Android.Net.Uri.Parse(url));
        request.SetTitle(title);
        request.SetDescription("Новая версия Психеи");
        request.SetMimeType("application/vnd.android.package-archive");
        request.SetNotificationVisibility(DownloadVisibility.VisibleNotifyCompleted);
        request.SetDestinationInExternalFilesDir(Android.App.Application.Context, Android.OS.Environment.DirectoryDownloads, FileName);
        return Manager.Enqueue(request);
    }

    public static (State State, int Percent) Query(long id)
    {
        var query = new DownloadManager.Query();
        query.SetFilterById(id);
        using var c = Manager.InvokeQuery(query);
        // загрузку отменили из шторки — записи больше нет
        if (c == null || !c.MoveToFirst()) return (State.Failed, 0);
        var status = (DownloadStatus)c.GetInt(c.GetColumnIndex(DownloadManager.ColumnStatus));
        long total = c.GetLong(c.GetColumnIndex(DownloadManager.ColumnTotalSizeBytes));
        long done = c.GetLong(c.GetColumnIndex(DownloadManager.ColumnBytesDownloadedSoFar));
        int percent = total > 0 ? (int)(done * 100 / total) : 0;
        return status switch
        {
            DownloadStatus.Successful => (State.Done, 100),
            DownloadStatus.Failed => (State.Failed, percent),
            DownloadStatus.Running => (State.Running, percent),
            // Pending и Paused: ждёт сети или очереди
            _ => (State.Waiting, percent)
        };
    }

    public static void Cancel(long id)
    {
        try { Manager.Remove(id); }
        catch (Exception) { }
    }
}
