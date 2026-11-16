using PsyPath.UI;

namespace PsyPath.Services;

// кристаллы и магазин. Баланс и покупки лежат в ProgressData и уходят на сервер вместе с прогрессом
public static class Crystals
{
    // сколько дают: новый урок, урок без ошибок сверху, опросник (один раз в день на опросник).
    // За рубеж серии — столько, сколько дней (Streak.Reward)
    public const int ForLesson = 2;
    public const int ForPerfect = 1;
    public const int ForQuestionnaire = 1;

    public static int Balance => AppState.P.Crystals;

    public static void Earn(int n)
    {
        if (n > 0) AppState.P.Crystals += n;
    }

    // игроку, который занимался до магазина, — кристалл за каждые 10 XP. Один раз, в том числе после входа в аккаунт
    public static void GiveStartBonus()
    {
        var p = AppState.P;
        if (p.CrystalsStarted) return;
        p.CrystalsStarted = true;
        p.Crystals += p.TotalXp / 10;
    }

    public static bool Owns(string id, int price) => price == 0 || AppState.P.Owned.Contains(id);

    // false — не хватает кристаллов; уже купленное повторно не списывается
    public static bool Buy(string id, int price)
    {
        var p = AppState.P;
        if (Owns(id, price)) return true;
        if (p.Crystals < price) return false;
        p.Crystals -= price;
        p.Owned.Add(id);
        AppState.Save();
        return true;
    }

    public static void UseTheme(Theme theme)
    {
        AppState.P.Theme = theme.Id;
        AppState.Save();
        Pal.Apply(theme);
        // у «Космоса» своя музыка, у остальных тем — обычная
        _ = Music.UseTheme();
    }

    // 1 кристалл, 2 кристалла, 5 кристаллов
    public static string Word(int n)
    {
        int d = Math.Abs(n) % 100;
        if (d is >= 11 and <= 14) return "кристаллов";
        return (d % 10) switch
        {
            1 => "кристалл",
            2 or 3 or 4 => "кристалла",
            _ => "кристаллов"
        };
    }
}
