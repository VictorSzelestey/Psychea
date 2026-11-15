namespace PsyPath.UI;

public static class Pal
{
    // цвета, которые меняет тема из магазина (Themes.cs). Остальные — смысловые и одинаковы во всех темах:
    // красный — «неверно», зелёный — «верно», оранжевый — огонёк серии

    // фон экрана и холмы на нём
    public static Color Bg { get; private set; } = Color.FromArgb("#E9DFF4");
    public static Color Hill { get; private set; } = Color.FromArgb("#E0D4EF");
    public static Color HillFar { get; private set; } = Color.FromArgb("#D9CBEB");

    // светлые карточки и граница между карточками одного цвета
    public static Color Paper { get; private set; } = Color.FromArgb("#FBF5F3");
    public static Color Paper2 { get; private set; } = Color.FromArgb("#F1E9F7");
    public static Color Line { get; private set; } = Color.FromArgb("#DCCFEB");
    public static Color Ink { get; private set; } = Color.FromArgb("#3D2C63");
    public static Color Ink2 { get; private set; } = Color.FromArgb("#5F4E80");

    // главная кнопка, текущий урок, полоски прогресса
    public static Color Violet { get; private set; } = Color.FromArgb("#6E4BA6");
    // светлый акцент: выбранный вариант, пройденный урок. На нём — только тёмный текст
    public static Color Amber { get; private set; } = Color.FromArgb("#CDB8EC");

    public static readonly Color Red = Color.FromArgb("#E0517E");
    public static readonly Color Orange = Color.FromArgb("#F08A24");
    public static readonly Color Yellow = Color.FromArgb("#F6C453");
    public static readonly Color Teal = Color.FromArgb("#43A6A8");
    public static readonly Color Green = Color.FromArgb("#3E9B6E");

    public static Color Gray { get; private set; } = Color.FromArgb("#AE9FC3");
    public static readonly Color Cyan = Color.FromArgb("#49C6DA");

    public static readonly Color GreenSoft = Color.FromArgb("#CFEFD9");
    public static readonly Color RedSoft = Color.FromArgb("#FAD3DF");

    // тёмные варианты для текста на карточках и для кнопок со светлой надписью: контраст не ниже 4.5 (WCAG AA).
    // TealInk — цвет ссылок, имя осталось от прежней палитры
    public static readonly Color RedInk = Color.FromArgb("#A32A54");
    public static Color TealInk { get; private set; } = Color.FromArgb("#553A8B");
    public static readonly Color GreenInk = Color.FromArgb("#276638");

    public static Color Cycle(int i)
    {
        Color[] cycle = { Violet, Orange, Teal, Red, Cyan, Green };
        return cycle[i % cycle.Length];
    }

    // тема применяется до постройки экранов: уже открытые экраны перестраивает тот, кто её сменил
    public static void Apply(Theme t)
    {
        Bg = t.Bg;
        Hill = t.Hill;
        HillFar = t.HillFar;
        Paper = t.Paper;
        Paper2 = t.Paper2;
        Line = t.Line;
        Ink = t.Ink;
        Ink2 = t.Ink2;
        Violet = t.Primary;
        Amber = t.Accent;
        TealInk = t.Link;
        Gray = t.Gray;
#if ANDROID
        // фон окна под страницами (MainActivity): без него при смене экрана мелькает старый цвет
        MainThread.BeginInvokeOnMainThread(() =>
            Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window?.SetBackgroundDrawable(
                new Android.Graphics.Drawables.ColorDrawable(Microsoft.Maui.Platform.ColorExtensions.ToPlatform(t.Bg))));
#endif
    }

    // «соедини пары»: мягкая заливка двух плашек пары и насыщенный цвет линии и обводки.
    // Без красного и зелёного (это «неверно» и «верно») и без сиреневого (это выбранная плашка);
    // тёмный текст на любой заливке — контраст выше 7
    static readonly (Color Fill, Color Line)[] pairs =
    {
        (Color.FromArgb("#D9E4F7"), Color.FromArgb("#3F6FC4")),
        (Color.FromArgb("#FCE3CC"), Color.FromArgb("#D9761C")),
        (Color.FromArgb("#D3EEEE"), Color.FromArgb("#2F8C8E")),
        (Color.FromArgb("#F8D9E6"), Color.FromArgb("#C2457A")),
        (Color.FromArgb("#F5E9C2"), Color.FromArgb("#B08A1E")),
        (Color.FromArgb("#EEDFD5"), Color.FromArgb("#8C6448"))
    };

    public static (Color Fill, Color Line) Pair(int i) => pairs[i % pairs.Length];
}
