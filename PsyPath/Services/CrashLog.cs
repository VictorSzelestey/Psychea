using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace PsyPath.Services;

// Все записи закрываются и сбрасываются сразу: авария не должна потерять буфер.
// Ошибка диска никогда не должна вызвать ещё одно падение приложения.
public static class CrashLog
{
    const long MaxFileBytes = 1024 * 1024;
    const int MaxFiles = 10;
    static readonly object Gate = new();
    static readonly string Session = Guid.NewGuid().ToString("N")[..8];
    static readonly Regex Credentials = new(
        "(?i)(\\b(?:password|pwd|token|secret|api_key|authorization)\\b[\\\"']?\\s*[:=]\\s*)(?:\\\"[^\\\"]*\\\"|'[^']*'|[^\\s;,}]+)",
        RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
    static readonly Regex Bearer = new(@"(?i)\bBearer\s+[^\s,;]+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
    static readonly Regex Tokens = new(@"ghp_[A-Za-z0-9]+|github_pat_[A-Za-z0-9_]+|\b\d{8,}:AA[A-Za-z0-9_-]+|\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
    static string? currentFile;
    static bool initialized;

    public static string DirectoryPath { get; private set; } = "";

    public static void Initialize(string directory, string details)
    {
        lock (Gate)
        {
            if (initialized) return;
            DirectoryPath = directory;
            try
            {
                Directory.CreateDirectory(directory);
                NewFile();
                initialized = true;
                AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                    Write("FATAL .NET", e.ExceptionObject as Exception,
                        $"terminating={e.IsTerminating}");
                TaskScheduler.UnobservedTaskException += (_, e) => Write("UnobservedTask", e.Exception);
                Write("Session", details);
            }
            catch { /* Можно повторить инициализацию во внутренней папке. */ }
        }
    }

    static void NewFile()
    {
        currentFile = Path.Combine(DirectoryPath, $"psypath-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}-{Session}.txt");
        using (File.Create(currentFile)) { }
        foreach (var old in Directory.GetFiles(DirectoryPath, "psypath-*.txt")
                     .OrderByDescending(Path.GetFileName).Skip(MaxFiles))
            File.Delete(old);
    }

    public static void Write(string source, string message) => Write(source, null, message);

    public static void Write(string source, Exception? error, string message = "")
    {
        try
        {
            lock (Gate)
            {
                if (!initialized || currentFile == null) return;
                if (new FileInfo(currentFile).Length >= MaxFileBytes) NewFile();
                string text = $"{DateTimeOffset.UtcNow:O} [{Session}] [thread {Environment.CurrentManagedThreadId}] " +
                    $"[{source}] managedMB={GC.GetTotalMemory(false) / 1024 / 1024} {message}" +
                    (error == null ? "" : "\n" + error) + "\n";
                // Не пишем запросы/ответы API, пароли, токены и ответы на опросники.
                text = Tokens.Replace(Credentials.Replace(Bearer.Replace(text, "Bearer [redacted]"), "$1[redacted]"), "[redacted]");
                if (text.Length > 64 * 1024) text = text[..(64 * 1024)] + "\n[truncated]\n";
                using var stream = new FileStream(currentFile, FileMode.Append, FileAccess.Write, FileShare.Read);
                stream.Write(Encoding.UTF8.GetBytes(text));
                stream.Flush(flushToDisk: true);
            }
        }
        catch { /* В том числе нехватка памяти/места и ошибки фильтрации. */ }
    }

    // Копия в cache: системное окно отправки читает готовый файл, пока журнал продолжает писаться.
    public static string? Export(string cacheDirectory)
    {
        try
        {
            lock (Gate)
            {
                if (!initialized) return null;
                Directory.CreateDirectory(cacheDirectory);
                string path = Path.Combine(cacheDirectory, "Psychea-diagnostics.txt");
                using var output = new StreamWriter(path, false, new UTF8Encoding(false));
                foreach (var file in Directory.GetFiles(DirectoryPath, "psypath-*.txt").OrderBy(Path.GetFileName))
                {
                    output.WriteLine("=== " + Path.GetFileName(file) + " ===");
                    using var input = new StreamReader(file);
                    output.WriteLine(input.ReadToEnd());
                }
                return path;
            }
        }
        catch (Exception ex)
        {
            Write("Export", ex);
            return null;
        }
    }
}

// Пойманные самим MAUI ошибки тоже попадают в файл, даже без необработанного исключения.
public sealed class CrashLogProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName);
    public void Dispose() { }

    sealed class FileLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level)) return;
            try { CrashLog.Write(category, exception, $"{level}: {formatter(state, exception)}"); }
            catch { }
        }
    }
}
