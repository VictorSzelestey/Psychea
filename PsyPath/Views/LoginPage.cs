using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

public class LoginPage : BasePage
{
    bool register;
    bool busy;
    // пароль виден: переключается глазиком справа в поле
    bool showPassword;
    // манул рассказал, какие данные хранятся: один раз за показ экрана, при первом переходе к регистрации
    bool privacyShown;
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
        password = MakeEntry(register ? "не короче 6 символов" : "", password.Text, !showPassword);
        name = MakeEntry("как к тебе обращаться", name.Text);
        status = Ui.Text(status.Text, 12, status.TextColor, true);

        var stack = new VerticalStackLayout { Spacing = 12, Padding = new Thickness(20, 16, 20, 28) };
        stack.Children.Add(new Heading(register ? "РЕГИСТРАЦИЯ" : "ВХОД", 26));
        stack.Children.Add(Ui.Mood("study", IsLandscape ? 80 : 130));
        // подпись над полем всегда видна, подсказка внутри поля — только пример
        stack.Children.Add(Labeled("ЛОГИН", login, register ? "3–32 символа: латиница, цифры, _ . -" : null));
        stack.Children.Add(Labeled("ПАРОЛЬ", PasswordRow(), null));
        if (register)
            stack.Children.Add(Labeled("ИМЯ (НЕОБЯЗАТЕЛЬНО)", name, null));

        stack.Children.Add(status);

        var go = new Btn(register ? "СОЗДАТЬ АККАУНТ ▶" : "ВОЙТИ ▶", Pal.Violet, Pal.OnAccent);
        go.Clicked += async (s, e) => await Submit();
        stack.Children.Add(go);

        stack.Children.Add(Form.Link(register ? "УЖЕ ЕСТЬ АККАУНТ? ВОЙТИ" : "НЕТ АККАУНТА? РЕГИСТРАЦИЯ", Pal.TealInk, async () =>
        {
            if (busy) return;
            register = !register;
            status.Text = "";
            await SwapBody(Build());
            if (register && !privacyShown)
            {
                privacyShown = true;
                await Note("love", "МЫ НЕ СЛЕДИМ ЗА ТОБОЙ",
                    "Психея не собирает личные данные и телеметрию. На сервере хранятся только логин, имя и результаты занятий — " +
                    "чтобы прогресс не потерялся при смене телефона. Журнал ошибок остаётся в телефоне, пока ты сам не решишь им поделиться.");
            }
        }, TextAlignment.Center));

        stack.Children.Add(Ui.VersionLabel());

        var root = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };
        var scroll = new ScrollView { Content = stack };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        return root;
    }

    static View Labeled(string label, View field, string? hint)
    {
        var box = new VerticalStackLayout { Spacing = 3 };
        box.Children.Add(Ui.Text(label, 11, Pal.Ink2, true));
        box.Children.Add(Ui.Card(field, Pal.Paper2, 4));
        if (hint != null) box.Children.Add(Form.Hint(hint));
        return box;
    }

    // поле пароля и пиксельный глазик справа: открытый — пароль виден, закрытый — скрыт
    View PasswordRow()
    {
        var row = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        row.Children.Add(password);

        // иконка мелкая, поэтому зона нажатия шире неё — не меньше 44 точек
        var eye = new ContentView
        {
            Padding = new Thickness(12, 0),
            MinimumWidthRequest = 44,
            MinimumHeightRequest = 44,
            VerticalOptions = LayoutOptions.Center
        };
        void Draw()
        {
            eye.Content = new PixelIcon(showPassword ? Icons.Eye : Icons.EyeClosed, Pal.Ink2, 20 * Ui.Scale, Pal.Violet);
            SemanticProperties.SetDescription(eye, showPassword ? "Скрыть пароль" : "Показать пароль");
        }
        Draw();
        Ui.OnTap(eye, () =>
        {
            showPassword = !showPassword;
            password.IsPassword = !showPassword;
            Draw();
        });
        Grid.SetColumn(eye, 1);
        row.Children.Add(eye);
        return row;
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

        // пока тянется прогресс и строится главный экран, вместо чёрной паузы — манул
        await SwapBody(Ui.Waiting("ЗАГРУЖАЮ ТВОЙ ПРОГРЕСС…"));
        await AppState.AdoptServerProgress();
        // короткая заставка «успех»: лого и текст проявляются по очереди, без прыжков —
        // только fade + лёгкий сдвиг/масштаб, строго и минималистично
        await PlayWelcome(register);
        // fade: false — экран ожидания и так уже на месте, а новую страницу
        // проявит её собственный PlayEnter; второй fade поверх первого дал бы лишнее мигание
        await SwitchRoot(Start.Home(), fade: false);
    }

    async Task PlayWelcome(bool justRegistered)
    {
        string who = Api.User?.DisplayName ?? "";

        var logo = Ui.Mood("love", 120);
        logo.Opacity = 0;
        logo.Scale = 0.9;

        var title = Ui.Text(justRegistered ? "ДОБРО ПОЖАЛОВАТЬ" : "С ВОЗВРАЩЕНИЕМ", 22, Pal.Ink, true, TextAlignment.Center);
        title.Opacity = 0;
        title.TranslationY = 14;

        var sub = Ui.Text(who.Length > 0 ? who : "ПСИХЕЯ", 15, Pal.Ink2, false, TextAlignment.Center);
        sub.Opacity = 0;
        sub.TranslationY = 14;

        var stack = new VerticalStackLayout
        {
            Spacing = 16,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };
        stack.Children.Add(logo);
        stack.Children.Add(title);
        stack.Children.Add(sub);

        var overlay = new Grid { BackgroundColor = Pal.Bg };
        overlay.Children.Add(stack);
        Root.Children.Add(overlay);

        await Task.WhenAll(logo.Fade(1, 420), logo.Zoom(1, 420, Easing.CubicOut));
        await Task.WhenAll(title.Fade(1, 260), title.Move(0, 0, 260, Easing.CubicOut));
        await Task.Delay(70);
        await Task.WhenAll(sub.Fade(1, 240), sub.Move(0, 0, 240, Easing.CubicOut));
        await Task.Delay(550);
        // overlay намеренно не гасим и не убираем: сразу следом SwitchRoot(..., fade: false)
        // целиком меняет страницу окна, так что отдельный fade-out здесь дал бы только лишнее мигание
    }

    void Show(string text, Color color)
    {
        status.Text = text;
        status.TextColor = color;
    }
}
