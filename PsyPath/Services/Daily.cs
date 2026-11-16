using PsyPath.Models;

namespace PsyPath.Services;

public record DailyTask(string Id, string Title, int Goal, int Reward, Func<ProgressData, int> Progress);

// задания дня: три из пяти, набор зависит от даты. Кристаллы начисляет Collect: после урока, карточек и опросника,
// и главная при каждом показе: если 50 XP набрались не уроком, карточками или опросником
// (например, прогресс пришёл с сервера), кристаллы не ждут следующего занятия
public static class Daily
{
    static readonly DailyTask[] Pool =
    {
        // награды с 1.7.0 на 50% больше прежних 4, 5, 3, 3, 4; половинки округлены вверх
        new("lessons2", "Пройди 2 урока", 2, 6, p => p.DailyLessons),
        new("perfect", "Пройди урок без ошибок", 1, 8, p => p.DailyPerfect),
        new("cards10", "Повтори 10 карточек", 10, 5, p => p.DailyCards),
        new("quiz", "Пройди опросник", 1, 5, p => p.DailyQuizzes),
        new("xp50", "Набери 50 XP", 50, 6, _ => AppState.TodayXp)
    };

    // шаг 2 по кругу из пяти: три разных задания, каждый день другой набор
    public static IReadOnlyList<DailyTask> Today()
    {
        int start = DateOnly.FromDateTime(DateTime.Today).DayNumber % Pool.Length;
        return new[] { Pool[start], Pool[(start + 2) % Pool.Length], Pool[(start + 4) % Pool.Length] };
    }

    public static bool IsDone(DailyTask t) => AppState.P.DailyDone.Contains(t.Id) && AppState.P.DailyDate == Key;

    public static int ProgressOf(DailyTask t)
    {
        NewDay();
        return Math.Min(t.Progress(AppState.P), t.Goal);
    }

    static string Key => DateTime.Today.ToString("yyyy-MM-dd");

    static void NewDay()
    {
        var p = AppState.P;
        if (p.DailyDate == Key) return;
        p.DailyDate = Key;
        p.DailyLessons = p.DailyPerfect = p.DailyCards = p.DailyQuizzes = 0;
        p.DailyDone.Clear();
    }

    public static void Count(int lessons = 0, int perfect = 0, int cards = 0, int quizzes = 0)
    {
        NewDay();
        var p = AppState.P;
        p.DailyLessons += lessons;
        p.DailyPerfect += perfect;
        p.DailyCards += cards;
        p.DailyQuizzes += quizzes;
    }

    // начисляет кристаллы за только что выполненные задания; возвращает строки для экрана итогов
    public static List<string> Collect()
    {
        NewDay();
        var p = AppState.P;
        var done = new List<string>();
        foreach (var t in Today())
        {
            if (p.DailyDone.Contains(t.Id) || t.Progress(p) < t.Goal) continue;
            p.DailyDone.Add(t.Id);
            Crystals.Earn(t.Reward);
            done.Add($"{t.Title}: +{t.Reward} {Crystals.Word(t.Reward)}");
        }
        return done;
    }
}
