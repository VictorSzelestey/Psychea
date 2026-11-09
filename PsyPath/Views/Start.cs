using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

// куда попадает вошедший пользователь: в приложение или на приветственные слайды
public static class Start
{
    public static Page Home()
    {
        // админу слайды с объяснением приложения не нужны
        if (Api.User?.IsAdmin == true && !AppState.P.Onboarded)
        {
            AppState.P.Onboarded = true;
            AppState.P.Rating = 100;
            AppState.Save();
        }
        return AppState.P.Onboarded ? new AppShell() : new NavigationPage(new OnboardingPage());
    }

    // автообновление: не чаще раза в сутки, когда приложение уже открыто на вкладках
    public static async Task OfferUpdate()
    {
        if (!Settings.AutoUpdate) return;
        if (DateTime.UtcNow - Preferences.Default.Get("update_checked", DateTime.MinValue) < TimeSpan.FromDays(1)) return;

        // запуск не должен ждать сеть
        await Task.Delay(2500);
        var check = await Updater.Check();
        if (!check.Ok) return;
        if (Application.Current?.Windows[0].Page is not Shell shell) return;
        Preferences.Default.Set("update_checked", DateTime.UtcNow);
        if (check.Update == null) return;

        bool yes = await Dlg.Confirm(shell, "ОБНОВЛЕНИЕ", Updater.Prompt(check.Update), "ОБНОВИТЬ", "ПОЗЖЕ");
        if (yes) await shell.Navigation.PushAsync(new SettingsPage(check.Update), false);
    }
}
