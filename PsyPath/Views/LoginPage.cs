using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

public class LoginPage : BasePage
{
    bool register;
    bool busy;
    Entry login = new();
    Entry password = new();
    Entry name = new();
    Label status = Ui.Text("", 12, Pal.RedInk, true);

    public LoginPage() : base(true)
    {
        SetBody(Build());
    }

    protected override void OnOrientationChanged() => SetBody(Build());

    static Entry MakeEntry(string placeholder, string? text, bool secret = false) => new()
    {
        Text = text,
        Placeholder = placeholder,
        PlaceholderColor = Pal.Ink2,
        TextColor = Pal.Ink,
        FontFamily = "Body",
        FontSize = 15 * Ui.Scale,
        IsPassword = secret,
        IsSpellCheckEnabled = false,
        IsTextPredictionEnabled = false,
        BackgroundColor = Colors.Transparent
    };

    // поля создаются заново при каждой сборке: одно поле нельзя вставить в два экрана сразу
    View Build()
    {
        login = MakeEntry("например, ivan_92", login.Text);
        password = MakeEntry(register ? "не короче 6 символов" : "", password.Text, true);
        name = MakeEntry("как к тебе обращаться", name.Text);
        status = Ui.Text(status.Text, 12, status.TextColor, true);

        var stack = new VerticalStackLayout { Spacing = 12, Padding = new Thickness(20, 16, 20, 28) };
        stack.Children.Add(new Glitch(register ? "РЕГИСТРАЦИЯ" : "ВХОД", 26));
        stack.Children.Add(Ui.Mascot(IsLandscape ? 80 : 130, idle: true));
        // подпись над полем всегда видна, подсказка внутри поля — только пример
        stack.Children.Add(Labeled("ЛОГИН", login, register ? "3–32 символа: латиница, цифры, _ . -" : null));
        stack.Children.Add(Labeled("ПАРОЛЬ", password, null));
        if (register)
            stack.Children.Add(Labeled("ИМЯ (НЕОБЯЗАТЕЛЬНО)", name, null));

        stack.Children.Add(status);

        var go = new Btn(register ? "СОЗДАТЬ АККАУНТ ▶" : "ВОЙТИ ▶", Pal.Violet, Pal.Paper);
        go.Clicked += async (s, e) => await Submit();
        stack.Children.Add(go);

        stack.Children.Add(Form.Link(register ? "УЖЕ ЕСТЬ АККАУНТ? ВОЙТИ" : "НЕТ АККАУНТА? РЕГИСТРАЦИЯ", Pal.TealInk, async () =>
        {
            if (busy) return;
            register = !register;
            status.Text = "";
            await SwapBody(Build());
        }, TextAlignment.Center));

        // адрес сервера пользователям не показываем; разработчик меняет его пятью нажатиями на версию
        var version = Ui.VersionLabel();
        int taps = 0;
        Ui.OnTap(version, async () =>
        {
            if (++taps < 5) return;
            taps = 0;
            await EditServer();
        });
        stack.Children.Add(version);

        var root = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };
        root.Children.Add(Ui.Stripes(4));
        var scroll = new ScrollView { Content = stack };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        return root;
    }

    static View Labeled(string label, Entry entry, string? hint)
    {
        var box = new VerticalStackLayout { Spacing = 3 };
        box.Children.Add(Ui.Text(label, 11, Pal.Ink2, true));
        box.Children.Add(Ui.Card(entry, Pal.Paper2, 4));
        if (hint != null) box.Children.Add(Form.Hint(hint));
        return box;
    }

    async Task Submit()
    {
        if (busy) return;
        string l = (login.Text ?? "").Trim();
        string p = password.Text ?? "";
        if (l.Length == 0 || p.Length == 0)
        {
            Show("Введи логин и пароль", Pal.RedInk);
            return;
        }

        busy = true;
        Show(register ? "Создаю аккаунт…" : "Вхожу…", Pal.Ink2);
        var r = register ? await Api.Register(l, p, name.Text ?? "") : await Api.Login(l, p);
        if (!r.Ok)
        {
            Show(r.Error, Pal.RedInk);
            busy = false;
            return;
        }

        await AppState.AdoptServerProgress();
        await SwitchRoot(Start.Home());
    }

    void Show(string text, Color color)
    {
        status.Text = text;
        status.TextColor = color;
    }

    async Task EditServer()
    {
        string? url = await DisplayPromptAsync("СЕРВЕР",
            "Адрес API. Эмулятор: http://10.0.2.2:5080, телефон: http://<IP компьютера>:5080",
            "OK", "ОТМЕНА", initialValue: Api.BaseUrl, keyboard: Keyboard.Url);
        if (string.IsNullOrWhiteSpace(url)) return;
        if (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https"))
            Api.BaseUrl = url;
        else
            Show("Адрес должен начинаться с http:// или https://", Pal.RedInk);
    }
}
