namespace PsyPath.Services;

// рубежи серии: на них один раз показывается заставка с огоньком и даются кристаллы
public static class Streak
{
    // после 100 — каждые 50 дней
    static readonly int[] Milestones = { 5, 10, 20, 30, 50, 100 };

    public static bool IsMilestone(int days) =>
        Milestones.Contains(days) || (days > 100 && days % 50 == 0);

    public static int Next(int days)
    {
        foreach (int m in Milestones)
            if (m > days) return m;
        return (days / 50 + 1) * 50;
    }

    // кристаллов за рубеж — столько, сколько дней в серии
    public static int Reward(int days) => days;

    // сегодня серия встала на рубеж и заставку ещё не показывали. Отмечает показ и начисляет кристаллы;
    // вызывать после AppState.RegisterActivity, возвращает число дней или 0
    public static int TakeCelebration()
    {
        var p = AppState.P;
        if (!IsMilestone(p.Streak) || p.StreakCelebrated == p.LastActive) return 0;
        p.StreakCelebrated = p.LastActive;
        Crystals.Earn(Reward(p.Streak));
        AppState.Save();
        return p.Streak;
    }
}
