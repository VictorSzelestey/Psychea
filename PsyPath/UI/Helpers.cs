using PsyPath.Services;

namespace PsyPath.UI;

// в .NET 10 методы анимации переименовали в *Async, поэтому обёртки.
// Все анимации идут через них, поэтому здесь же учитывается системное «Отключить анимацию»
public static class Anim
{
    // Android: «Для разработчиков» / «Спец. возможности» → «Отключить анимацию» (масштаб длительности 0)
    // или «Анимации: выкл» в настройках приложения. Тогда элементы сразу встают в конечное состояние
    static readonly bool SystemReduced = DetectReduced();

    // копия Settings.ReduceMotion: настройка читается при каждой анимации, Preferences для этого медленные
    public static bool Still { get; set; } = Settings.ReduceMotion;

    public static bool Reduced => SystemReduced || Still;

    static bool DetectReduced()
    {
#if ANDROID
        try
        {
            var resolver = Android.App.Application.Context.ContentResolver;
            return Android.Provider.Settings.Global.GetFloat(resolver, Android.Provider.Settings.Global.AnimatorDurationScale, 1f) == 0f;
        }
        catch
        {
            return false;
        }
#else
        return false;
#endif
    }

    public static Task Move(this VisualElement v, double x, double y, uint ms = 250, Easing? easing = null)
    {
        if (Reduced)
        {
            v.TranslationX = x;
            v.TranslationY = y;
            return Task.CompletedTask;
        }
#if NET10_0_OR_GREATER
        return v.TranslateToAsync(x, y, ms, easing);
#else
        return v.TranslateTo(x, y, ms, easing);
#endif
    }

    public static Task Fade(this VisualElement v, double opacity, uint ms = 250)
    {
        if (Reduced)
        {
            v.Opacity = opacity;
            return Task.CompletedTask;
        }
#if NET10_0_OR_GREATER
        return v.FadeToAsync(opacity, ms);
#else
        return v.FadeTo(opacity, ms);
#endif
    }

    public static Task Zoom(this VisualElement v, double scale, uint ms = 250, Easing? easing = null)
    {
        if (Reduced)
        {
            v.Scale = scale;
            return Task.CompletedTask;
        }
#if NET10_0_OR_GREATER
        return v.ScaleToAsync(scale, ms, easing);
#else
        return v.ScaleTo(scale, ms, easing);
#endif
    }

    // короткое «проседание» под пальцем
    public static async Task Press(this VisualElement v)
    {
        try
        {
            await v.Zoom(0.96, 70, Easing.CubicOut);
            await v.Zoom(1, 140, Easing.CubicOut);
        }
        catch (ArgumentException) { v.Scale = 1; }
    }

    // верный ответ: лёгкий «подскок»
    public static async Task Bounce(this VisualElement v)
    {
        try
        {
            await v.Zoom(1.05, 110, Easing.CubicOut);
            await v.Zoom(1, 180, Easing.CubicIn);
        }
        catch (ArgumentException) { v.Scale = 1; }
    }

    // неверный ответ: короткое покачивание из стороны в сторону
    public static async Task Shake(this VisualElement v)
    {
        try
        {
            foreach (double x in new[] { -10.0, 9, -6, 4, 0 })
                await v.Move(x, v.TranslationY, 45);
        }
        catch (ArgumentException) { v.TranslationX = 0; }
    }

    // появление элемента списка: снизу вверх с задержкой delay мс
    public static async Task Appear(this VisualElement v, int delay)
    {
        v.Opacity = 0;
        v.TranslationY = 18;
        try
        {
            await Task.Delay(delay);
            // сдвиг по X не трогаем: на «пути уроков» кружки стоят зигзагом
            await Task.WhenAll(v.Fade(1, 260), v.Move(v.TranslationX, 0, 260, Easing.CubicOut));
        }
        catch (ArgumentException)
        {
            // элемент ещё не в окне — показываем без анимации
            v.Opacity = 1;
            v.TranslationY = 0;
        }
    }

    // элементы списка появляются по очереди; дальние — сразу, чтобы не ждать
    public static void Stagger(Layout list, int max = 10)
    {
        int i = 0;
        foreach (var child in list.Children.OfType<VisualElement>().Take(max))
            _ = child.Appear(20 + i++ * 40);
    }

    // число «набегает» от 0 до to, когда подпись появляется на экране
    public static void CountUp(Label label, int to, Func<int, string> format, uint ms = 800)
    {
        if (Reduced)
        {
            label.Text = format(to);
            return;
        }
        label.Text = format(0);
        void Start() => new Animation(v => label.Text = format((int)Math.Round(v)), 0, to, Easing.CubicOut)
            .Commit(label, "count", 16, ms, finished: (v, c) => label.Text = format(to));
        if (label.IsLoaded) Start();
        else label.Loaded += (s, e) => Start();
    }

    // покачивание вверх-вниз: несколько раз при появлении и стоп —
    // бесконечная декоративная анимация отвлекает
    public static void Idle(VisualElement v, double amplitude = 6, uint period = 2400, int cycles = 3)
    {
        if (Reduced) return;
        void Start()
        {
            int left = cycles;
            var a = new Animation();
            a.Add(0, 0.5, new Animation(y => v.TranslationY = y, 0, -amplitude, Easing.SinInOut));
            a.Add(0.5, 1, new Animation(y => v.TranslationY = y, -amplitude, 0, Easing.SinInOut));
            a.Commit(v, "idle", 16, period, null, null, () => v.IsLoaded && --left > 0);
        }
        if (v.IsLoaded) Start();
        else v.Loaded += (s, e) => Start();
    }

    public static Task Spin(this VisualElement v, double rotation, uint ms = 250)
    {
        if (Reduced)
        {
            v.Rotation = rotation;
            return Task.CompletedTask;
        }
#if NET10_0_OR_GREATER
        return v.RotateToAsync(rotation, ms);
#else
        return v.RotateTo(rotation, ms);
#endif
    }
}

public static class Dlg
{
    public static Task Alert(Page page, string title, string message, string ok = "ОК")
    {
#if NET10_0_OR_GREATER
        return page.DisplayAlertAsync(title, message, ok);
#else
        return page.DisplayAlert(title, message, ok);
#endif
    }

    public static Task<bool> Confirm(Page page, string title, string message, string yes, string no)
    {
#if NET10_0_OR_GREATER
        return page.DisplayAlertAsync(title, message, yes, no);
#else
        return page.DisplayAlert(title, message, yes, no);
#endif
    }
}
