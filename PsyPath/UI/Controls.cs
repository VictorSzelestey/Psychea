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

        top = Ui.Card(label, fill, 13, 24);
        top.StrokeThickness = 0;
        top.Shadow = Ui.SoftShadow();

        // нижний отступ — место под тень
        var g = new Grid { Padding = new Thickness(0, 0, 0, 4) };
        g.Children.Add(top);
        Content = g;

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) =>
        {
            if (!enabled) return;
            Sound.Play("tap");
            await top.Zoom(0.97, 50, Easing.CubicOut);
            await top.Zoom(1, 60, Easing.CubicOut);
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
            Color = color ?? Pal.Violet,
            CornerRadius = new CornerRadius(4)
        };
        var g = new Grid { ColumnSpacing = 0, ColumnDefinitions = { filled, rest } };
        g.Children.Add(fill);

        BackgroundColor = Pal.Paper2;
        Stroke = new SolidColorBrush(Pal.Line);
        StrokeThickness = 1;
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(7) };
        HeightRequest = 14;
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

// заголовок экрана
public class Heading : Grid
{
    public Heading(string text, double size = 28, Color? color = null)
    {
        Children.Add(Ui.Text(text, size, color ?? Pal.Ink, true));
    }
}

// холмы и облака за содержимым экрана
class BackdropDrawable : IDrawable
{
    public void Draw(ICanvas canvas, RectF rect)
    {
        float w = rect.Width, h = rect.Height;

        canvas.FillColor = Pal.Hill;
        canvas.FillCircle(w * 0.84f, 70, 54);
        canvas.FillCircle(w * 0.62f, 90, 36);
        canvas.FillCircle(w, 100, 46);
        canvas.FillCircle(-6, h * 0.46f, 44);
        canvas.FillCircle(w * 0.14f, h * 0.48f, 30);
        canvas.FillRoundedRectangle(-20, h * 0.48f, w * 0.34f, 34, 17);

        Mountains(canvas, w, h, h * 0.74f);
        Hill(canvas, w, h, h * 0.86f, Pal.Hill);
    }

    static void Mountains(ICanvas canvas, float w, float h, float top)
    {
        var path = new PathF();
        path.MoveTo(0, h);
        path.LineTo(0, top + 50);
        path.LineTo(w * 0.17f, top - 14);
        path.LineTo(w * 0.33f, top + 44);
        path.LineTo(w * 0.53f, top - 46);
        path.LineTo(w * 0.72f, top + 40);
        path.LineTo(w * 0.87f, top - 8);
        path.LineTo(w, top + 30);
        path.LineTo(w, h);
        path.Close();
        canvas.FillColor = Pal.HillFar;
        canvas.FillPath(path);
    }

    static void Hill(ICanvas canvas, float w, float h, float top, Color color)
    {
        var path = new PathF();
        path.MoveTo(0, h);
        path.LineTo(0, top + 40);
        path.CurveTo(w * 0.25f, top - 50, w * 0.45f, top + 60, w * 0.7f, top + 10);
        path.CurveTo(w * 0.85f, top - 20, w * 0.95f, top + 20, w, top);
        path.LineTo(w, h);
        path.Close();
        canvas.FillColor = color;
        canvas.FillPath(path);
    }
}

public class Backdrop : GraphicsView
{
    public Backdrop()
    {
        Drawable = new BackdropDrawable();
        BackgroundColor = Colors.Transparent;
        InputTransparent = true;
    }
}

// штрихи-«лучи» у текущего раздела на пути
class BurstDrawable : IDrawable
{
    public void Draw(ICanvas canvas, RectF rect)
    {
        canvas.StrokeColor = Pal.Violet;
        canvas.StrokeSize = 3.5f;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(4, 25, 12, 26);
        canvas.DrawLine(8, 12, 15, 17);
        canvas.DrawLine(20, 3, 24, 11);
    }
}

public class Burst : GraphicsView
{
    public Burst()
    {
        Drawable = new BurstDrawable();
        BackgroundColor = Colors.Transparent;
        WidthRequest = 30;
        HeightRequest = 30;
        Margin = new Thickness(-16, -10, 0, 0);
        HorizontalOptions = LayoutOptions.Start;
        VerticalOptions = LayoutOptions.Start;
        InputTransparent = true;
    }
}

// пунктир между соседними кружками пути: x0 и x1 — центры кружков
class DashDrawable(float x0, float x1) : IDrawable
{
    public void Draw(ICanvas canvas, RectF rect)
    {
        canvas.StrokeColor = Pal.Gray;
        canvas.StrokeSize = 3;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeDashPattern = new[] { 2f, 3f };
        var path = new PathF();
        path.MoveTo(x0, 0);
        path.CurveTo(x0, rect.Height * 0.6f, x1, rect.Height * 0.4f, x1, rect.Height);
        canvas.DrawPath(path);
    }
}

public class DashLink : GraphicsView
{
    public DashLink(double x0, double x1)
    {
        Drawable = new DashDrawable((float)x0, (float)x1);
        BackgroundColor = Colors.Transparent;
        HeightRequest = 26;
        Margin = new Thickness(0, -12);
        InputTransparent = true;
    }
}

// линии «соедини пары»: от правого края левой плашки к левому краю правой, цветом пары.
// Слой лежит под плашками: от точек на концах видны полукруглые «разъёмы»
class PairLinesDrawable : IDrawable
{
    public List<(PointF From, PointF To, Color Color)> Links { get; } = new();

    public void Draw(ICanvas canvas, RectF rect)
    {
        canvas.StrokeSize = 3;
        canvas.StrokeLineCap = LineCap.Round;
        foreach (var (a, b, color) in Links)
        {
            canvas.StrokeColor = color;
            canvas.FillColor = color;
            float bend = (b.X - a.X) / 2;
            var path = new PathF();
            path.MoveTo(a);
            path.CurveTo(a.X + bend, a.Y, b.X - bend, b.Y, b.X, b.Y);
            canvas.DrawPath(path);
            canvas.FillCircle(a, 4);
            canvas.FillCircle(b, 4);
        }
    }
}

public class PairLines : GraphicsView
{
    readonly PairLinesDrawable drawable = new();

    public PairLines()
    {
        Drawable = drawable;
        BackgroundColor = Colors.Transparent;
        InputTransparent = true;
    }

    public void Set(IEnumerable<(PointF From, PointF To, Color Color)> links)
    {
        drawable.Links.Clear();
        drawable.Links.AddRange(links);
        Invalidate();
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
        BackgroundColor = Pal.Bg;
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
        Root.Children.Add(new Backdrop());
        Root.Children.Add(body);
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
        CrashLog.Write("Page", GetType().Name);
        Music.Enter(MusicZone);
        navigating = false;
        Refresh();
        _ = PlayEnter();
    }

    // вызывается перед каждым показом экрана, до анимации входа
    protected virtual void Refresh() { }

    // опросники и карточки переопределяют: там музыку можно выключить в настройках
    protected virtual Music.Zone MusicZone => Music.Zone.Menu;

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
    // fade: false — старый экран остаётся видимым, пока строится новый (без чёрной паузы)
    protected async Task SwitchRoot(Page next, bool fade = true)
    {
        if (fade && Content is View v) await v.Fade(0, 200);
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
