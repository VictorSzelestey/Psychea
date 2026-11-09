using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

// удаление аккаунта: что пропадёт, подтверждение паролем, отдельная красная кнопка
public class DeleteAccountPage : AdminBase
{
    readonly Entry password;
    readonly Label status = Ui.Text("", 12, Pal.RedInk, true);
    bool busy;

    public DeleteAccountPage()
    {
        password = new Entry
        {
            IsPassword = true,
            FontFamily = "Body",
            FontSize = 15 * Ui.Scale,
            TextColor = Pal.Ink,
            BackgroundColor = Colors.Transparent
        };

        var s = List();
        s.Children.Add(Ui.Text($"Аккаунт @{Api.User?.Login} будет удалён навсегда:", 14, Pal.Ink, true));
        s.Children.Add(Ui.Bullets("ПРОПАДУТ", new[]
        {
            "логин и профиль",
            "уровень, рейтинг, XP и серия",
            "пройденные уроки и результаты опросников"
        }, Pal.RedSoft));
        s.Children.Add(Form.Hint("Восстановить аккаунт будет нельзя. Чтобы продолжить, введи пароль."));

        var field = new VerticalStackLayout { Spacing = 3 };
        field.Children.Add(Ui.Text("ПАРОЛЬ", 11, Pal.Ink2, true));
        field.Children.Add(Ui.Card(password, Pal.Paper2, 4));
        s.Children.Add(field);
        s.Children.Add(status);

        var bottom = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(16, 8, 16, 12), BackgroundColor = Pal.Paper };
        var delete = new Btn("УДАЛИТЬ НАВСЕГДА", Pal.RedInk, Pal.Paper);
        delete.Clicked += async (o, e) => await Delete();
        bottom.Children.Add(delete);
        bottom.Children.Add(Form.Link("ОТМЕНА", Pal.Ink2, async () => await Pop(), TextAlignment.Center));

        Show("УДАЛЕНИЕ", s, bottom);
    }

    async Task Delete()
    {
        if (busy) return;
        if (string.IsNullOrEmpty(password.Text))
        {
            status.Text = "Введи пароль";
            return;
        }
        if (!await Dlg.Confirm(this, "ТОЧНО?", "Аккаунт и весь прогресс будут удалены без возможности восстановления.", "УДАЛИТЬ", "ОТМЕНА"))
            return;

        busy = true;
        status.TextColor = Pal.Ink2;
        status.Text = "Удаляю…";
        var r = await Api.DeleteAccount(password.Text);
        busy = false;
        if (!r.Ok)
        {
            status.TextColor = Pal.RedInk;
            status.Text = r.Error;
            return;
        }

        Api.Logout();
        AppState.ResetAll();
        await Dlg.Alert(this, "ГОТОВО", "Аккаунт удалён.");
        await SwitchRoot(new NavigationPage(new LoginPage()));
    }
}
