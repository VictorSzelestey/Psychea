using Android.App;
using Android.Runtime;
using PsyPath.Services;

namespace PsyPath;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override void OnCreate()
    {
        AndroidCrashLog.Initialize(this);
        base.OnCreate();
        // Чтение системных traces не задерживает старт интерфейса.
        _ = Task.Run(() => AndroidCrashLog.PreviousExit(this));
    }

    public override void OnTrimMemory(Android.Content.TrimMemory level)
    {
        CrashLog.Write("Android memory", $"trim={level}");
        base.OnTrimMemory(level);
    }

    public override void OnLowMemory()
    {
        CrashLog.Write("Android memory", "LOW MEMORY");
        base.OnLowMemory();
    }
}
