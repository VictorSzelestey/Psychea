using PsyPath.Services;
using PsyPath.Views;

namespace PsyPath;

public class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        CrashLog.Write("Lifecycle", "CreateWindow");
        return new Window(new LoadingPage());
    }

    // вернулись в приложение: отправить то, что накопилось без сети, и обновить напоминание
    protected override void OnResume()
    {
        base.OnResume();
        CrashLog.Write("Lifecycle", "Resume");
        Updater.InBackground = false;
        // APK мог докачаться, пока приложение было свёрнуто
        _ = Updater.Resume();
        _ = Sync.Flush();
        Reminders.Update();
        Music.Wake();
    }

    protected override void OnSleep()
    {
        CrashLog.Write("Lifecycle", "Sleep");
        Updater.InBackground = true;
        Music.Sleep();
        base.OnSleep();
    }
}
