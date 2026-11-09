using Microsoft.Maui.Controls.Shapes;
using PsyPath.Services;

namespace PsyPath.UI;

public class Btn : ContentView
{
    readonly Border top;
    readonly Label label;
    bool enabled = true;

    public event EventHandler? Clicked;

    public Btn(string text, Color fill, Color? textColor = null, double size = 16)
    {
        label = Ui.Text(text, size, textColor ?? Pal.Ink, true, TextAlignment.Center);
        label.VerticalTextAlignment = TextAlignment.Center;

        top = Ui.Card(label, fill, 13);
        top.Margin = new Thickness(0, 0, 5, 5);

        var shadow = new Border
        {
            BackgroundColor = Pal.Ink,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) },
            Margin = new Thickness(5, 5, 0, 0)
        };

        var g = new Grid();
        g.Children.Add(shadow);
        g.Children.Add(top);
        Content = g;

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) =>
        {
            if (!enabled) return;
            Sound.Play("tap");
            await top.Move(4, 4, 50);
            await top.Move(0, 0, 60);
            Clicked?.Invoke(this, EventArgs.Empty);
        };
        g.GestureRecognizers.Add(tap);
    }

    public string Text
    {
        get => label.Text;
        set => label.Text = value;
    }

    public bool Enabled
    {
        get => enabled;
        set
        {
            enabled = value;
            top.Opacity = value ? 1 : 0.45;
        }
    }

    public void SetColors(Color fill, Color? textColor = null)
    {
        top.BackgroundColor = fill;
        if (textColor != null) label.TextColor = textColor;
    }
}

public class Bar : Border
{
    // доля задаётся звёздными столбцами: ширина считается за один проход раскладки,
    // без SizeChanged и второго прохода, из-за которого полоса прыгала
    readonly ColumnDefinition filled = new(new GridLength(0, GridUnitType.Star));
    readonly ColumnDefinition rest = new(new GridLength(1, GridUnitType.Star));
    double val;
    double shown;

    public Bar(Color? color = null)
    {
        var fill = new BoxView
        {
            Color = color ?? Pal.Amber,
            CornerRadius = new CornerRadius(4)
        };
        var g = new Grid { ColumnSpacing = 0, ColumnDefinitions = { filled, rest } };
        g.Children.Add(fill);

        BackgroundColor = Pal.Paper2;
        Stroke = new SolidColorBrush(Pal.Ink);
        StrokeThickness = 2;
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(7) };
        HeightRequest = 18;
        Padding = new Thickness(0);
        Content = g;
        // при появлении на экране полоса плавно заполняется от нуля
        Loaded += (s, e) => Animate(600);
    }

    public double Value
    {
        get => val;
        set
        {
            val = Math.Clamp(value, 0, 1);
            if (IsLoaded) Animate(350);
        }
    }

    void Animate(uint ms)
    {
        this.AbortAnimation("fill");
        if (Anim.Reduced)
        {
            Draw(val);
            return;
        }
        new Animation(Draw, shown, val, Easing.CubicOut).Commit(this, "fill", 16, ms);
    }

    void Draw(double f)
    {
        shown = f;
        filled.Width = new GridLength(f, GridUnitType.Star);
        rest.Width = new GridLength(1 - f, GridUnitType.Star);
    }
}

public class Glitch : Grid
{
    public Glitch(string text, double size = 28, Color? color = null)
    {
        var red = Ui.Text(text, size, Pal.Red, true);
        red.TranslationX = -1.5;
        red.Opacity = 0.85;

        var cyan = Ui.Text(text, size, Pal.Cyan, true);
        cyan.TranslationX = 1.5;
        cyan.Opacity = 0.85;

        var main = Ui.Text(text, size, color ?? Pal.Ink, true);

        Children.Add(red);
        Children.Add(cyan);
        Children.Add(main);
    }
}

class ScanDrawable : IDrawable
{
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.StrokeColor = new Color(0f, 0f, 0f, 0.06f);
        canvas.StrokeSize = 1;
        for (float y = 0; y < dirtyRect.Height; y += 3)
            canvas.DrawLine(0, y, dirtyRect.Width, y);
    }
}

public class Scanlines : Grid
{
    public Scanlines()
    {
        InputTransparent = true;
        CascadeInputTransparent = true;
        Children.Add(new GraphicsView { Drawable = new ScanDrawable(), InputTransparent = true });
    }
}

public abstract class BasePage : ContentPage
{
    // шире этого контент не растягивается (горизонтальный режим, планшеты)
    const double MaxContentWidth = 640;

    protected readonly Grid Root = new();
    readonly bool isTab;
    View? body;
    bool? landscape;
    bool navigating;

    protected BasePage(bool hideTabs)
    {
        isTab = !hideTabs;
        BackgroundColor = Pal.Paper;
        Shell.SetNavBarIsVisible(this, false);
        NavigationPage.SetHasNavigationBar(this, false);
        if (hideTabs) Shell.SetTabBarIsVisible(this, false);
        SizeChanged += (s, e) => OnResize();
    }

    protected bool IsLandscape => Width > Height;

    protected void SetBody(View body)
    {
        this.body = body;
        ApplyWidth();
        Root.Children.Clear();
        Root.Children.Add(body);
        Root.Children.Add(new Scanlines());
        Content = Root;
    }

    // смена содержимого внутри экрана: старое уходит влево, новое приходит справа
    protected async Task SwapBody(View next)
    {
        // страница ещё не на экране: анимировать нечего, её покажет PlayEnter
        bool shown = Width > 0;
        if (body != null && shown)
            await Task.WhenAll(body.Fade(0, 120), body.Move(-28, 0, 120, Easing.CubicIn));
        SetBody(next);
        if (!shown) return;
        next.Opacity = 0;
        next.TranslationX = 28;
        await Task.WhenAll(next.Fade(1, 220), next.Move(0, 0, 220, Easing.CubicOut));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        navigating = false;
        Refresh();
        _ = PlayEnter();
    }

    // вызывается перед каждым показом экрана, до анимации входа
    protected virtual void Refresh() { }

    // false — экран показывается сразу, без въезда (заставка при запуске)
    protected virtual bool AnimateEnter => true;

    // вкладки проявляются, открытые поверх экраны въезжают справа
    async Task PlayEnter()
    {
        if (!AnimateEnter || Content is not View v) return;
        v.CancelAnimations();
        v.Opacity = 0;
        if (isTab)
        {
            // вкладка проявляется и чуть всплывает
            v.TranslationY = 10;
            await Task.WhenAll(v.Fade(1, 200), v.Move(0, 0, 240, Easing.CubicOut));
            return;
        }
        v.TranslationX = 48;
        await Task.WhenAll(v.Fade(1, 240), v.Move(0, 0, 280, Easing.CubicOut));
    }

    // переходы без системной анимации: свою играет PlayEnter нового экрана
    protected async Task Push(Page page)
    {
        if (navigating) return;
        navigating = true;
        await Navigation.PushAsync(page, false);
    }

    protected async Task Pop()
    {
        if (navigating) return;
        navigating = true;
        if (Content is View v)
            await Task.WhenAll(v.Fade(0, 180), v.Move(48, 0, 180, Easing.CubicIn));
        await Navigation.PopAsync(false);
    }

    // замена корневой страницы окна: загрузка → вкладки, сброс → онбординг
    protected async Task SwitchRoot(Page next)
    {
        if (Content is View v) await v.Fade(0, 200);
        Application.Current!.Windows[0].Page = next;
    }

    protected override bool OnBackButtonPressed()
    {
        if (isTab || Navigation.NavigationStack.Count <= 1) return base.OnBackButtonPressed();
        _ = Pop();
        return true;
    }

    void OnResize()
    {
        ApplyWidth();
        bool now = IsLandscape;
        // первый замер в портрете — не смена ориентации, в горизонтали — да: экран собран под портрет
        bool changed = landscape.HasValue ? landscape != now : now;
        landscape = now;
        if (changed) OnOrientationChanged();
    }

    protected virtual void OnOrientationChanged() { }

    void ApplyWidth()
    {
        if (body == null || Width <= 0) return;
        bool wide = Width > MaxContentWidth;
        body.WidthRequest = wide ? MaxContentWidth : -1;
        body.HorizontalOptions = wide ? LayoutOptions.Center : LayoutOptions.Fill;
    }
}

// вкладка, которая перестраивается только когда поменялись данные:
// раньше каждое переключение собирало экран заново, и он дёргался
public abstract class DataPage : BasePage
{
    string builtFor = "";
    ScrollView? scroll;

    protected DataPage() : base(false)
    {
    }

    // собирает экран и возвращает его ScrollView, чтобы сохранить прокрутку
    protected abstract ScrollView Build();

    protected override void Refresh()
    {
        string key = AppState.Version + "|" + DateTime.Today.ToString("yyyy-MM-dd");
        if (key == builtFor) return;
        builtFor = key;
        bool first = scroll == null;
        double y = scroll?.ScrollY ?? 0;
        scroll = Build();
        if (y > 0) RestoreScroll(scroll, y);
        // при первом показе карточки появляются по очереди; при обновлении данных — без этого
        if (first && scroll.Content is Layout list) Anim.Stagger(list);
    }

    static void RestoreScroll(ScrollView s, double y)
    {
        void OnSize(object? sender, EventArgs e)
        {
            s.SizeChanged -= OnSize;
            s.Dispatcher.Dispatch(async () => await s.ScrollToAsync(0, y, false));
        }
        s.SizeChanged += OnSize;
    }
}
