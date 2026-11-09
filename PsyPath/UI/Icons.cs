namespace PsyPath.UI;

// пиксельные иконки вместо эмодзи: одинаково выглядят на любом телефоне
// и красятся цветами палитры. Рисунок 8×8: '#' — основной цвет, '+' — акцент, '.' — пусто
public static class Icons
{
    public static readonly string[] Flame =
    {
        "...#....",
        "...##...",
        "..###.#.",
        "..#####.",
        ".##++##.",
        ".#++++#.",
        ".#++++#.",
        "..####.."
    };

    public static readonly string[] Bolt =
    {
        "....###.",
        "...###..",
        "..###...",
        ".######.",
        "...###..",
        "..###...",
        ".##.....",
        ".#......"
    };

    public static readonly string[] Lock =
    {
        "..####..",
        ".#....#.",
        ".#....#.",
        "########",
        "###++###",
        "###++###",
        "########",
        "########"
    };

    public static readonly string[] Star =
    {
        "...##...",
        "...##...",
        "########",
        ".######.",
        "..####..",
        ".##..##.",
        ".#....#.",
        "........"
    };

    public static readonly string[] StarEmpty =
    {
        "...##...",
        "...##...",
        "###..###",
        ".#....#.",
        "..#..#..",
        ".#.##.#.",
        ".##..##.",
        "........"
    };

    // список с галочкой — опросники
    public static readonly string[] Tape =
    {
        "..####..",
        ".#....#.",
        ".#.##.#.",
        ".#.##.#.",
        ".#.##.#.",
        ".#....#.",
        "..####..",
        "....#..."
    };

    // шевроны — уровень
    public static readonly string[] Rank =
    {
        "#......#",
        "##....##",
        ".##..##.",
        "..####..",
        "#......#",
        "##....##",
        ".##..##.",
        "..####.."
    };

    // мишень — точность
    public static readonly string[] Target =
    {
        "..####..",
        ".#....#.",
        "#..##..#",
        "#.#++#.#",
        "#.#++#.#",
        "#..##..#",
        ".#....#.",
        "..####.."
    };

    // человечек — пользователи
    public static readonly string[] User =
    {
        "...##...",
        "..####..",
        "..####..",
        "...##...",
        ".######.",
        "########",
        "##.##.##",
        "##.##.##"
    };

    // мозг — познавательные процессы, очки опыта
    public static readonly string[] Brain =
    {
        "..####..",
        ".#+##+#.",
        "##+##+##",
        "#+####+#",
        "##+##+##",
        ".#+##+#.",
        "..####..",
        "...##..."
    };

    // батарейка на исходе — состояния
    public static readonly string[] Battery =
    {
        "........",
        "#######.",
        "#+....#.",
        "#+....##",
        "#+....##",
        "#+....#.",
        "#######.",
        "........"
    };

    // маска — свойства личности
    public static readonly string[] Mask =
    {
        "########",
        "#..##..#",
        "########",
        "########",
        "#.####.#",
        "##....##",
        ".######.",
        "..####.."
    };

    // сердце — эмоции и чувства
    public static readonly string[] Heart =
    {
        ".##..##.",
        "########",
        "#+######",
        "########",
        ".######.",
        "..####..",
        "...##...",
        "........"
    };

    // кубок — топ игроков
    public static readonly string[] Cup =
    {
        "########",
        "#.####.#",
        "#.####.#",
        ".######.",
        "..####..",
        "...##...",
        "..####..",
        ".######."
    };

    // шестерёнка — настройки
    public static readonly string[] Gear =
    {
        "...##...",
        ".#.##.#.",
        "..####..",
        "###..###",
        "###..###",
        "..####..",
        ".#.##.#.",
        "...##..."
    };

    // начала слов: одна основа покрывает все падежи
    static readonly (string[] Stems, string[] Icon)[] Terms =
    {
        (new[] { "памят", "внимани", "мышлени", "восприяти", "воображени", "ощущени", "реч", "интеллект", "мозг" }, Brain),
        (new[] { "усталост", "утомлени", "стресс", "апати", "сонлив", "бодрост" }, Battery),
        (new[] { "темперамент", "характер", "личност", "персона", "способност", "тень", "самость" }, Mask),
        (new[] { "вдохновени", "эмоци", "чувств", "страх", "радост", "грусть", "гнев", "тревог", "ревност", "ностальги", "настроени", "любов" }, Heart)
    };

    // иконка термина из одного-двух слов; у длинных формулировок её нет
    public static string[]? ForTerm(string text)
    {
        string t = text.Trim().ToLowerInvariant();
        if (t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 2) return null;
        foreach (var (stems, icon) in Terms)
            if (stems.Any(t.StartsWith)) return icon;
        return null;
    }

    // иконки показываются, только если они есть у всех вариантов: одна-единственная подсказала бы ответ
    public static bool ForAll(IEnumerable<string> texts) => texts.All(t => ForTerm(t) != null);
}

public class PixelIcon : GraphicsView
{
    public PixelIcon(string[] pixels, Color color, double size = 16, Color? accent = null)
    {
        Drawable = new PixelDrawable(pixels, color, accent ?? Pal.Amber);
        WidthRequest = size;
        HeightRequest = size;
        BackgroundColor = Colors.Transparent;
        InputTransparent = true;
        VerticalOptions = LayoutOptions.Center;
    }
}

class PixelDrawable(string[] pixels, Color color, Color accent) : IDrawable
{
    public void Draw(ICanvas canvas, RectF rect)
    {
        int h = pixels.Length, w = pixels[0].Length;
        float cell = Math.Min(rect.Width / w, rect.Height / h);
        float ox = (rect.Width - cell * w) / 2, oy = (rect.Height - cell * h) / 2;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char c = pixels[y][x];
                if (c == '.') continue;
                canvas.FillColor = c == '+' ? accent : color;
                // +0.5 — без щелей между соседними пикселями при дробном масштабе
                canvas.FillRectangle(ox + x * cell, oy + y * cell, cell + 0.5f, cell + 0.5f);
            }
    }
}
