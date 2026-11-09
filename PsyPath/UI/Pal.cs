namespace PsyPath.UI;

public static class Pal
{
    public static readonly Color Paper = Color.FromArgb("#F2E9D8");
    public static readonly Color Paper2 = Color.FromArgb("#E7DAC0");
    public static readonly Color Ink = Color.FromArgb("#241A14");
    public static readonly Color Ink2 = Color.FromArgb("#6B5A4C");

    public static readonly Color Red = Color.FromArgb("#E2382F");
    public static readonly Color Orange = Color.FromArgb("#F08A24");
    public static readonly Color Amber = Color.FromArgb("#F4B63F");
    public static readonly Color Teal = Color.FromArgb("#2A9D8F");
    public static readonly Color Green = Color.FromArgb("#3E9B55");

    public static readonly Color Gray = Color.FromArgb("#B8AD98");
    public static readonly Color Cyan = Color.FromArgb("#35C5D8");
    public static readonly Color Phosphor = Color.FromArgb("#FFC24A");

    public static readonly Color GreenSoft = Color.FromArgb("#CFEBD6");
    public static readonly Color RedSoft = Color.FromArgb("#F7CFCB");

    // тёмные варианты для текста на бумаге и для кнопок со светлой надписью:
    // яркие Red/Teal/Green дают контраст 2.8–3.6, эти — не ниже 4.5 (WCAG AA)
    public static readonly Color RedInk = Color.FromArgb("#A8261F");
    public static readonly Color TealInk = Color.FromArgb("#1B6B62");
    public static readonly Color GreenInk = Color.FromArgb("#276638");

    static readonly Color[] cycle = { Amber, Red, Teal, Orange, Cyan, Green };

    public static Color Cycle(int i) => cycle[i % cycle.Length];
}
