using System.Text;

namespace PsyPath.Bot;

// разметка сообщений Telegram (parse_mode HTML)
public static class Html
{
    public const int MaxMessage = 3900;

    public static string E(string? s) => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    public static string B(string? s) => "<b>" + E(s) + "</b>";
    public static string I(string? s) => "<i>" + E(s) + "</i>";

    // «кассетная» шкала вместо картинки: ▰▰▰▱▱
    public static string Bar(double fraction, int width = 10)
    {
        int n = (int)Math.Round(Math.Clamp(fraction, 0, 1) * width);
        return new string('▰', n) + new string('▱', width - n);
    }

    public static string Meter(int difficulty)
    {
        int d = Math.Clamp(difficulty, 1, 5);
        return "D" + d + " " + new string('▮', d) + new string('▯', 5 - d);
    }

    // надпись на кнопке: Telegram всё равно обрежет длинную, но лучше явно и с многоточием
    public static string Short(string s, int max = 48) => s.Length <= max ? s : s[..(max - 1)].TrimEnd() + "…";

    // длинный текст — несколько сообщений; режем только по границе абзаца, чтобы не порвать теги
    public static List<string> Split(string text)
    {
        var parts = new List<string>();
        var cur = new StringBuilder();
        foreach (var block in text.Split("\n\n"))
        {
            if (cur.Length > 0 && cur.Length + block.Length + 2 > MaxMessage)
            {
                parts.Add(cur.ToString());
                cur.Clear();
            }
            if (cur.Length > 0) cur.Append("\n\n");
            cur.Append(block);
        }
        if (cur.Length > 0) parts.Add(cur.ToString());
        return parts;
    }
}
