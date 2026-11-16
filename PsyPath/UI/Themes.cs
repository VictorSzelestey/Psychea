namespace PsyPath.UI;

// тема из магазина: перекрашивает фон, карточки, текст и главную кнопку.
// Контраст каждой проверен: текст Ink на Paper, Bg и Accent — не ниже 6.7, надпись OnAccent на Primary — не ниже 5.2 (WCAG AA).
// Хвостовые цвета задают только тёмные темы; у светлых они общие (см. Pal.Apply).
// Music — своя фоновая музыка темы из Resources/Raw, Space — звёзды и планета вместо облаков на фоне.
// Island — номер острова (Islands.All), который открывает тему: она не продаётся, а открывается вместе с островом.
// Scene — пейзаж острова на фоне экранов (BackdropDrawable): crystal, jungle, night, astral; null — горы и облака
public record Theme(string Id, string Name, int Price,
    Color Bg, Color Hill, Color HillFar, Color Paper, Color Paper2, Color Line,
    Color Ink, Color Ink2, Color Primary, Color Accent, Color Link, Color Gray,
    bool Dark = false, Color? OnAccent = null, Color? GreenSoft = null, Color? RedSoft = null,
    Color? RedInk = null, Color? GreenInk = null, string? Music = null, bool Space = false, int Island = 0, string? Scene = null);

public static class Themes
{
    static Color C(string hex) => Color.FromArgb(hex);

    public static readonly Theme[] All =
    {
        new("lilac", "Сирень", 0,
            C("#E9DFF4"), C("#E0D4EF"), C("#D9CBEB"), C("#FBF5F3"), C("#F1E9F7"), C("#DCCFEB"),
            C("#3D2C63"), C("#5F4E80"), C("#6E4BA6"), C("#CDB8EC"), C("#553A8B"), C("#AE9FC3"), Island: 1, Scene: "crystal"),
        // пастельный лайм острова «Джунгли психики». Контраст: Ink на Paper 12.0, на Accent 8.7, OnAccent на Primary 5.1
        new("jungle", "Джунгли", 0,
            C("#E3F1D0"), C("#D7EBBF"), C("#CCE4B0"), C("#FAFDF4"), C("#EDF6E1"), C("#CFE3B8"),
            C("#253A1F"), C("#45603A"), C("#3F7A2A"), C("#C2E39A"), C("#2F6A22"), C("#A3BC93"),
            Music: "music_jungle.mp3", Island: 2, Scene: "jungle"),
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
        // тёмная: светлый текст, а кнопки — светлые заливки с тёмной надписью
        new("night", "Ночь", 0,
            C("#1B1630"), C("#221C3B"), C("#282145"), C("#2A2342"), C("#332B4F"), C("#44395F"),
            C("#EDE6FA"), C("#BFB3D9"), C("#B79CF0"), C("#4B3B78"), C("#C7B2F7"), C("#8A7DA6"),
            Dark: true, OnAccent: C("#1B1530"), GreenSoft: C("#1F3D2E"), RedSoft: C("#4A2333"),
            RedInk: C("#F59DBA"), GreenInk: C("#8FD9AE")),
        // тёмно-синий космос острова ночи: звёзды на фоне и своя музыка в меню. Контраст: Ink на Paper 12.9, на Accent 6.9, OnAccent на Primary 8.7.
        // До 1.7.0 продавался за 100 кристаллов — купившим их вернули (Refunds)
        new("cosmo", "Космос", 0,
            C("#0E1430"), C("#2B3570"), C("#1A2150"), C("#1C2347"), C("#252D57"), C("#38427A"),
            C("#E8ECFF"), C("#AEB8E6"), C("#8FB4FF"), C("#3A4A94"), C("#9CC2FF"), C("#7480B8"),
            Dark: true, OnAccent: C("#0E1430"), GreenSoft: C("#173B37"), RedSoft: C("#47213A"),
            RedInk: C("#FF9FC4"), GreenInk: C("#86E0C0"), Music: "music_cosmo.mp3", Space: true, Island: 3, Scene: "night"),
        // бело-золотой «Астрал»: остров пока закрыт, тема и музыка появятся вместе с ним. Контраст: Ink на Paper 11.3, на Accent 8.2, OnAccent на Primary 4.9
        new("astral", "Астрал", 0,
            C("#FBF1D6"), C("#F5E7C0"), C("#EEDDB0"), C("#FFFDF6"), C("#FBF4E2"), C("#EADCB4"),
            C("#47371A"), C("#6B5733"), C("#8C6A1C"), C("#EBD9A4"), C("#7A5A12"), C("#CDBB91"), Island: 4, Scene: "astral"),
    };

    // неизвестная или пустая тема — сиреневая
    public static Theme Find(string? id) => All.FirstOrDefault(t => t.Id == id) ?? All[0];
}
