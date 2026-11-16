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

    // выбранная в магазине тема видна сразу: игрок возвращается с острова на карту
    public static void UseTheme(Theme theme)
    {
        AppState.P.Theme = theme.Id;
        AppState.Save();
        Islands.Enter(null);
    }

    // «Космос» продавался за 100 кристаллов, с 1.7.0 его открывает Остров ночи. Купившим — возврат один раз
    // и сообщение на главной; тема вернётся вместе с островом. true — прогресс изменился
    public const int CosmoRefund = 100;

    public static bool RefundRemoved()
    {
        var p = AppState.P;
        if (!p.Owned.Contains("cosmo") || p.Refunds.Contains("cosmo")) return false;
        p.Owned.RemoveAll(id => id == "cosmo");
        p.Crystals += CosmoRefund;
        p.Refunds.Add("cosmo");
        p.Notices.Add("refund_cosmo");
        if (p.Theme == "cosmo" && !Islands.ThemeOpen(Themes.Find("cosmo"))) p.Theme = "";
        return true;
    }

    // всё, что продаётся за кристаллы и что можно подарить промокодом: темы, аватарки, рамки, наряды
    public static IReadOnlyList<(string Id, string Name, int Price)> ShopItems =>
        Themes.All.Where(t => t.Price > 0).Select(t => (t.Id, "Тема «" + t.Name + "»", t.Price))
            .Concat(Avatars.All.Where(a => a.Price > 0).Select(a => (a.Id, "Аватарка «" + a.Name + "»", a.Price)))
            .Concat(Cosmetics.Frames.Select(f => (f.Id, "Рамка «" + f.Name + "»", f.Price)))
            .Concat(Cosmetics.Outfits.Select(o => (o.Id, "Наряд «" + o.Name + "»", o.Price)))
            .ToList();

    // награда промокода в прогресс; строка — что получил игрок. Уже купленный предмет не дублируется:
    // вместо него — кристаллы по его цене. Предмет, которого эта версия не знает, всё равно записывается в покупки —
    // он появится после обновления
    public static string GrantPromo(string reward, int amount, string item)
    {
        var p = AppState.P;
        string result;
        if (reward == "crystals")
        {
            Earn(amount);
            result = $"+{amount} {Word(amount)}";
        }
        else if (ShopItems.FirstOrDefault(x => x.Id == item) is { Id: not null } thing)
        {
            if (p.Owned.Contains(item))
            {
                Earn(thing.Price);
                result = $"{thing.Name} уже есть, поэтому +{thing.Price} {Word(thing.Price)}";
            }
            else
            {
                p.Owned.Add(item);
                result = thing.Name + " — уже в магазине";
            }
        }
        else
        {
            if (!p.Owned.Contains(item)) p.Owned.Add(item);
            result = "Подарок появится после обновления приложения";
        }
        AppState.Save();
        return result;
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
