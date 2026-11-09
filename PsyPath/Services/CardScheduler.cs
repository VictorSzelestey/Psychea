namespace PsyPath.Services;

public enum CardRating
{
    Again,
    Hard,
    Good,
    Easy
}

public readonly record struct CardPlan(int Days, double Ease);

// расчёт повторов по образцу Anki: пользователь сам оценивает, насколько легко вспомнил.
// Ease — лёгкость карточки: чем она выше, тем быстрее растёт промежуток между повторами
public static class CardScheduler
{
    public const double StartEase = 2.5;
    const double MinEase = 1.3;
    const double HardFactor = 1.2;
    const double EasyBonus = 1.3;
    const double HardPenalty = 0.15;
    const double AgainPenalty = 0.2;
    const double EasyGain = 0.15;
    const int MaxDays = 365;

    // lastDays — прошлый промежуток в днях, 0 у новой или забытой карточки.
    // Days = 0 — повтор сегодня же
    public static CardPlan Next(int lastDays, double ease, CardRating rating)
    {
        int hard = Math.Max(1, Scale(lastDays, HardFactor));
        // кнопка правее всегда отодвигает карточку дальше
        int good = Math.Max(hard + 1, lastDays == 0 ? 1 : Scale(lastDays, ease));
        int easy = Math.Max(good + 1, lastDays == 0 ? 4 : Scale(lastDays, ease * EasyBonus));

        return rating switch
        {
            CardRating.Again => new CardPlan(0, Math.Max(MinEase, ease - AgainPenalty)),
            CardRating.Hard => new CardPlan(Math.Min(hard, MaxDays), Math.Max(MinEase, ease - HardPenalty)),
            CardRating.Good => new CardPlan(Math.Min(good, MaxDays), ease),
            _ => new CardPlan(Math.Min(easy, MaxDays), ease + EasyGain)
        };
    }

    static int Scale(int days, double factor) => (int)Math.Round(days * factor, MidpointRounding.AwayFromZero);
}
