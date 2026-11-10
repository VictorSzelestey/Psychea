using Android.App;
using Android.Runtime;
using PsyPath.Services;

namespace PsyPath;

static class AndroidCrashLog
{
    public static void Initialize(Android.App.Application app)
    {
        string details = $"version={app.PackageManager?.GetPackageInfo(app.PackageName!, 0)?.VersionName}; " +
            $"Android={Android.OS.Build.VERSION.Release}; SDK={Android.OS.Build.VERSION.SdkInt}; " +
            $"device={Android.OS.Build.Manufacturer} {Android.OS.Build.Model}; pid={Environment.ProcessId}";
        string? storage = null;
        try { storage = app.GetExternalFilesDir(null)?.AbsolutePath; }
        catch { }
        string internalPath = Path.Combine(app.FilesDir!.AbsolutePath, "Psychea", "logs");
        CrashLog.Initialize(storage == null ? internalPath : Path.Combine(storage, "Psychea", "logs"), details);
        CrashLog.Initialize(internalPath, details); // запасной путь при недоступной внешней памяти

        AndroidEnvironment.UnhandledExceptionRaiser += (_, e) => CrashLog.Write("FATAL Android", e.Exception);
        // Не ставим Handled и не подавляем штатное завершение процесса.
    }

    public static void PreviousExit(Android.App.Application app)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(30)) return;
        try
        {
            using var manager = (ActivityManager?)app.GetSystemService(Android.Content.Context.ActivityService);
            var exits = manager?.GetHistoricalProcessExitReasons(app.PackageName, 0, 3);
            if (exits == null) return;
            foreach (var exit in exits)
            {
                CrashLog.Write("Previous Android exit",
                    $"time={DateTimeOffset.FromUnixTimeMilliseconds(exit.Timestamp):O}; " +
                    $"reason={exit.Reason}; status={exit.Status}; description={exit.Description}; " +
                    $"pssKB={exit.Pss}; rssKB={exit.Rss}");
                // Android сохраняет ANR/native trace не для каждого завершения.
                using var trace = exit.TraceInputStream;
                if (trace == null) continue;
                using var reader = new StreamReader(trace);
                var buffer = new char[32 * 1024];
                int count = reader.ReadBlock(buffer, 0, buffer.Length);
                CrashLog.Write("Previous Android trace", new string(buffer, 0, count));
            }
        }
        catch (Exception ex) { CrashLog.Write("PreviousExit", ex); }
    }
}
