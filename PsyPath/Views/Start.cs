using PsyPath.Services;

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
}
