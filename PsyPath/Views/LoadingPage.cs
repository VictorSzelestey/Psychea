using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

public class LoadingPage : BasePage
{
    readonly Intro intro = new();

    public LoadingPage() : base(true)
    {
        SetBody(intro);
    }

    // заставка продолжает системную, въезд справа здесь не нужен
    protected override bool AnimateEnter => false;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // анимация идёт, пока грузится контент; переход — когда готово и то и другое
        var played = intro.Play();
        try
        {
            await AppState.LoadAsync();
            _ = Sound.Init();
            Sync.Start();
            Reminders.Update();
        }
        catch (Exception ex)
        {
            CrashLog.Write("Loading", ex);
            var err = new VerticalStackLayout { Padding = 24, VerticalOptions = LayoutOptions.Center, Spacing = 12 };
            err.Children.Add(Ui.Mood("worried", 140));
            err.Children.Add(Ui.Text("ОШИБКА ЗАГРУЗКИ", 18, Pal.RedInk, true));
            err.Children.Add(Ui.Text(ex.Message, 12));
            err.Children.Add(Ui.Text("Проверь, что JSON-файлы лежат в Resources/Raw.", 12, Pal.Ink2));
            SetBody(err);
            return;
        }

        if (Api.LoggedIn)
        {
            // токен продлевается при каждом запуске, но мог уже истечь;
            // без связи ждём не дольше 3 с и работаем с тем, что в телефоне
            var check = Api.RefreshSession();
            await Task.WhenAny(check, Task.Delay(3000));
            if (check.IsCompleted && check.Result.Status == 401)
            {
                Api.Logout();
                AppState.ResetAll();
            }
            else
            {
                // этот телефон — главный источник прогресса, отправляем то, что могло не уйти
                _ = AppState.PushNow();
            }
        }

        await played;
        // без затухания: заставка видна, пока строится следующий экран, иначе мелькает пустое окно
        await SwitchRoot(Api.LoggedIn ? Start.Home() : new NavigationPage(new LoginPage()), fade: false);
        _ = Start.OfferUpdate();
    }
}
