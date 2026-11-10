// Only phone storage/logging is stubbed; ApiUser's permission properties are real.
public static class Preferences
{
    public static Storage Default { get; } = new();
    public sealed class Storage
    {
        public T Get<T>(string key, T fallback) => fallback;
        public void Set<T>(string key, T value) { }
        public void Remove(string key) { }
    }
}
public static class SecureStorage
{
    public static Secrets Default { get; } = new();
    public sealed class Secrets
    {
        public Task<string?> GetAsync(string key) => Task.FromResult<string?>(null);
        public Task SetAsync(string key, string value) => Task.CompletedTask;
        public void Remove(string key) { }
    }
}
namespace PsyPath.Models { public class ProgressData { } }
namespace PsyPath.Services
{
    public static class AppState { public static void AccountChanged() { } }
    public static class CrashLog
    {
        public static void Write(string area, string message) { }
        public static void Write(string area, Exception ex, string message) { }
    }
}
