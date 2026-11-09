using PsyPath.Services;
using PsyPath.Views;

namespace PsyPath;

public class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new LoadingPage());
    }

    // вернулись в приложение: отправить то, что накопилось без сети, и обновить напоминание
    protected override void OnResume()
    {
        base.OnResume();
        _ = Sync.Flush();
        Reminders.Update();
    }
}
