namespace PsyPath.UI;

public static class Pal
{
    // сиреневый фон экрана и холмы на нём
    public static readonly Color Bg = Color.FromArgb("#E9DFF4");
    public static readonly Color Hill = Color.FromArgb("#E0D4EF");
    public static readonly Color HillFar = Color.FromArgb("#D9CBEB");

    // кремовые карточки и граница между карточками одного цвета
    public static readonly Color Paper = Color.FromArgb("#FBF5F3");
    public static readonly Color Paper2 = Color.FromArgb("#F1E9F7");
    public static readonly Color Line = Color.FromArgb("#DCCFEB");
    public static readonly Color Ink = Color.FromArgb("#3D2C63");
    public static readonly Color Ink2 = Color.FromArgb("#5F4E80");

    // главная кнопка, текущий урок, полоски прогресса
    public static readonly Color Violet = Color.FromArgb("#6E4BA6");
    // светлый акцент: выбранный вариант, пройденный урок. На нём — только тёмный текст
    public static readonly Color Amber = Color.FromArgb("#CDB8EC");

    public static readonly Color Red = Color.FromArgb("#E0517E");
    public static readonly Color Orange = Color.FromArgb("#F08A24");
    public static readonly Color Yellow = Color.FromArgb("#F6C453");
    public static readonly Color Teal = Color.FromArgb("#43A6A8");
    public static readonly Color Green = Color.FromArgb("#3E9B6E");

    public static readonly Color Gray = Color.FromArgb("#AE9FC3");
    public static readonly Color Cyan = Color.FromArgb("#49C6DA");

    public static readonly Color GreenSoft = Color.FromArgb("#CFEFD9");
    public static readonly Color RedSoft = Color.FromArgb("#FAD3DF");

    // тёмные варианты для текста на карточках и для кнопок со светлой надписью: контраст не ниже 4.5 (WCAG AA).
    // TealInk — цвет ссылок, имя осталось от прежней палитры
    public static readonly Color RedInk = Color.FromArgb("#A32A54");
    public static readonly Color TealInk = Color.FromArgb("#553A8B");
    public static readonly Color GreenInk = Color.FromArgb("#276638");

    static readonly Color[] cycle = { Violet, Orange, Teal, Red, Cyan, Green };

    public static Color Cycle(int i) => cycle[i % cycle.Length];

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
