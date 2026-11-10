// Only platform/UI boundaries are replaced. Tests compile the production page,
// scheduler, persistence and HTTP/sync logic unchanged; no server is contacted.
global using PsyPath.TestPlatform;

namespace PsyPath.TestPlatform
{
    public sealed class MemoryPreferences
    {
        readonly Dictionary<string, object> values = new();
        public T Get<T>(string key, T fallback) => values.TryGetValue(key, out var v) ? (T)v : fallback;
        public void Set<T>(string key, T value) where T : notnull => values[key] = value;
        public void Remove(string key) => values.Remove(key);
        public void Clear() => values.Clear();
    }
    public static class Preferences { public static MemoryPreferences Default { get; } = new(); }
    public sealed class MemorySecrets
    {
        public Task<string?> GetAsync(string key) => Task.FromResult<string?>(null);
        public Task SetAsync(string key, string value) => Task.CompletedTask;
        public bool Remove(string key) => true;
    }
    public static class SecureStorage { public static MemorySecrets Default { get; } = new(); }
    public static class MainThread { public static void BeginInvokeOnMainThread(Action a) => a(); }
    public enum NetworkAccess { None, Internet }
    public sealed class ConnectivityChangedEventArgs : EventArgs { public NetworkAccess NetworkAccess { get; init; } }
    public sealed class ConnectivityState { public event EventHandler<ConnectivityChangedEventArgs>? ConnectivityChanged; public void Online() => ConnectivityChanged?.Invoke(this, new() { NetworkAccess = NetworkAccess.Internet }); }
    public static class Connectivity { public static ConnectivityState Current { get; } = new(); }
    public static class FileSystem
    {
        public static string AppDataDirectory => Path.GetTempPath();
        public static Task<Stream> OpenAppPackageFileAsync(string name) => throw new NotSupportedException();
    }
    public class Color { }
    public enum TextAlignment { Start, Center, End }
    public record Thickness(double Left, double Top, double Right, double Bottom);
    public class View { public double Scale { get; set; } }
    public class VerticalStackLayout : View
    {
        public List<View> Children { get; } = new();
        public double Spacing { get; set; }
        public double MinimumHeightRequest { get; set; }
        public Thickness? Padding { get; set; }
        public Color? BackgroundColor { get; set; }
    }
    public class Grid : View
    {
        public List<View> Children { get; } = new();
        public double ColumnSpacing { get; set; }
        public double RowSpacing { get; set; }
        public List<ColumnDefinition> ColumnDefinitions { get; } = new();
        public List<RowDefinition> RowDefinitions { get; } = new();
        public static void SetRow(View v, int row) { }
        public static void SetColumn(View v, int col) { }
    }
    public class GridLength { public static GridLength Star { get; } = new(); public static GridLength Auto { get; } = new(); }
    public record ColumnDefinition(GridLength Width);
    public record RowDefinition(GridLength Height);
    public class Easing { public static Easing CubicIn { get; } = new(); public static Easing CubicOut { get; } = new(); }
}
namespace PsyPath.UI
{
    public static class Pal
    {
        public static readonly Color Violet = new(), Paper = new(), Paper2 = new(), Ink = new(), Ink2 = new(),
            Teal = new(), TealInk = new(), RedInk = new(), Orange = new(), GreenInk = new(), Amber = new();
    }
    public sealed class TextView(string text) : View { public string Text { get; } = text; }
    public static class Ui
    {
        public static View Text(string t, double size = 14, Color? color = null, bool bold = false, TextAlignment align = TextAlignment.Start) => new TextView(t);
        public static View Box(View v, Color? c = null, double pad = 14) => v;
        public static View Mascot(double h, bool idle = false) => new View();
    }
    public sealed class Btn(string text, Color fill, Color? color = null, double size = 16) : View
    {
        public string Text { get; } = text;
        public event EventHandler? Clicked;
        // Simulates delayed completion of the real button's tap animation.
        public void Click() => Clicked?.Invoke(this, EventArgs.Empty);
    }
    public sealed class Bar(Color color) : View { public double Value { get; set; } }
    public sealed class Heading(string text, double size) : View { }
    public static class Anim
    {
        public static TaskCompletionSource? PendingZoom;
        public static Task Zoom(this View v, double scale, uint ms, Easing easing) => PendingZoom?.Task ?? Task.CompletedTask;
    }
}
namespace PsyPath.Views
{
    public abstract class AdminBase
    {
        public View? Body { get; private set; }
        public View? Bottom { get; private set; }
        protected static VerticalStackLayout List() => new();
        protected void Show(string title, View body, View? bottom = null) { Body = body; Bottom = bottom; }
        protected Task Pop() => Task.CompletedTask;
    }
}
namespace PsyPath.Services
{
    public static class CrashLog
    {
        public static void Write(string area, string message) { }
        public static void Write(string area, Exception ex, string message) { }
    }
    public static class Reminders { public static void Update() { } }
    public static class Sound { public static void Play(string name) { } }
}
