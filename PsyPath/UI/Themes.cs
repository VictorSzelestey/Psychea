namespace PsyPath.UI;

// тема из магазина: перекрашивает фон, карточки, текст и главную кнопку.
// Контраст каждой проверен: текст Ink на Paper, Bg и Accent — не ниже 6.7, светлая надпись на Primary — не ниже 5.2 (WCAG AA)
public record Theme(string Id, string Name, int Price,
    Color Bg, Color Hill, Color HillFar, Color Paper, Color Paper2, Color Line,
    Color Ink, Color Ink2, Color Primary, Color Accent, Color Link, Color Gray);

public static class Themes
{
    static Color C(string hex) => Color.FromArgb(hex);

    public static readonly Theme[] All =
    {
        new("lilac", "Сирень", 0,
            C("#E9DFF4"), C("#E0D4EF"), C("#D9CBEB"), C("#FBF5F3"), C("#F1E9F7"), C("#DCCFEB"),
            C("#3D2C63"), C("#5F4E80"), C("#6E4BA6"), C("#CDB8EC"), C("#553A8B"), C("#AE9FC3")),
        new("mint", "Мята", 150,
            C("#DDF1EA"), C("#D0EADF"), C("#C4E2D5"), C("#F7FBF8"), C("#E6F4EE"), C("#C5E1D5"),
            C("#1F4A3D"), C("#3B6458"), C("#2B7560"), C("#B5E3D2"), C("#1E6450"), C("#9DBFB2")),
        new("peach", "Персик", 150,
            C("#FBE6DA"), C("#F6DACB"), C("#F0CDBB"), C("#FFF8F3"), C("#FCEDE4"), C("#F0D3C3"),
            C("#5A2E1E"), C("#7A4632"), C("#B04A24"), C("#F7C9AE"), C("#94391A"), C("#D0A894")),
        new("sea", "Море", 150,
            C("#DCE9F6"), C("#CFE0F2"), C("#C2D6EC"), C("#F6FAFE"), C("#E6F0FA"), C("#C6D9EE"),
            C("#1D3557"), C("#3A5679"), C("#2A67A6"), C("#BFD8F2"), C("#1F5794"), C("#9FB6CF")),
        new("rose", "Роза", 150,
            C("#F7E1EA"), C("#F1D3E0"), C("#EAC5D6"), C("#FFF7FA"), C("#FBEAF1"), C("#EDCADB"),
            C("#5B1F3D"), C("#7A3F5C"), C("#8E3B7A"), C("#F2C2D7"), C("#7E2E68"), C("#CFA2B8")),
    };

    // неизвестная или пустая тема — сиреневая
    public static Theme Find(string? id) => All.FirstOrDefault(t => t.Id == id) ?? All[0];
}
