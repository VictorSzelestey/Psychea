using System.Globalization;
using PsyPath.Models;

namespace PsyPath.Bot;

public record AchDef(string Id, string Title, string Desc);

public enum LessonState
{
    Locked,
    Open,
    Current,
    Done,
    Mastered
}

public record PathNode(Section Section, Lesson Lesson, int Index, LessonState State);

// правила игры — те же, что в AppState приложения: прогресс общий, и считать его надо одинаково
public static class Game
{
    static readonly Random Rnd = new();
    static TimeZoneInfo zone = TimeZoneInfo.Local;

    // у телефона своё местное время, у бота — часовой пояс из настроек
    public static void SetTimeZone(string id)
    {
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            Console.WriteLine($"часовой пояс {id} не найден, беру системный");
        }
    }

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
    public static DateTime Today => Now.Date;
    static string TodayKey => Today.ToString("yyyy-MM-dd");

    public static readonly AchDef[] AchDefs =
    {
        new("first", "Первая кассета", "Пройди первый урок"),
        new("streak3", "Три дня в эфире", "Серия 3 дня"),
        new("streak7", "Неделя в эфире", "Серия 7 дней"),
        new("perfect", "Без помех", "Пройди урок без ошибок"),
        new("ten", "Десять кассет", "Пройди 10 уроков"),
        new("lv3", "Студент", "Достигни 3 уровня"),
        new("lv5", "Эксперт", "Достигни 5 уровня"),
        new("quiz", "Первый опросник", "Пройди любой опросник"),
    };

    public static readonly (int Min, string Name)[] Goals =
    {
        (5, "лёгкая"),
        (10, "обычная"),
        (15, "серьёзная"),
        (20, "интенсив")
    };

    // 1 -> 100, 2 -> 300 ... 5 -> 900
    public static int QuestionRating(int d) => d * 200 - 100;

    public static int LevelOf(double rating)
    {
        if (rating >= 800) return 5;
        if (rating >= 600) return 4;
        if (rating >= 400) return 3;
        if (rating >= 200) return 2;
        return 1;
    }

    public static LevelInfo LevelInfoOf(Content c, int level)
    {
        return c.Rating.Levels.FirstOrDefault(x => x.Level == level)
               ?? new LevelInfo { Level = level, Name = "Уровень " + level, RatingFrom = (level - 1) * 200, RatingTo = level * 200 - 1 };
    }

    public static double UpdateRating(double r, int difficulty, bool correct, double k = 32)
    {
        double qr = QuestionRating(difficulty);
        double expected = 1.0 / (1.0 + Math.Pow(10, (qr - r) / 400.0));
        double next = r + k * ((correct ? 1.0 : 0.0) - expected);
        return Math.Clamp(next, 0, 1000);
    }

    public static int UnlockLevel(Content c, int sectionId)
    {
        var u = c.Rating.SectionUnlock.FirstOrDefault(x => x.Section == sectionId);
        return u?.UnlockLevel ?? 1;
    }

    public static bool IsDone(ProgressData p, Lesson l) => p.Done.Contains(l.Id);
    public static bool IsMastered(ProgressData p, Lesson l) => !IsDone(p, l) && l.RecommendedLevel <= LevelOf(p.Rating) - 2;
    public static bool IsPassed(ProgressData p, Lesson l) => IsDone(p, l) || IsMastered(p, l);

    // как вкладка «Вопросы»: раздел открывается уровнем, урок — пройденным предыдущим
    public static List<PathNode> Path(ProgressData p, Content c)
    {
        int level = LevelOf(p.Rating);
        var nodes = new List<PathNode>();
        bool currentFound = false;
        foreach (var sec in c.Lessons.Sections)
        {
            bool secUnlocked = level >= UnlockLevel(c, sec.Id);
            for (int i = 0; i < sec.Lessons.Count; i++)
            {
                var l = sec.Lessons[i];
                bool prevPassed = i == 0 || IsPassed(p, sec.Lessons[i - 1]);
                LessonState st;
                if (!secUnlocked) st = LessonState.Locked;
                else if (IsDone(p, l)) st = LessonState.Done;
                else if (IsMastered(p, l)) st = LessonState.Mastered;
                else if (prevPassed)
                {
                    st = currentFound ? LessonState.Open : LessonState.Current;
                    currentFound = true;
                }
                else st = LessonState.Locked;
                nodes.Add(new PathNode(sec, l, i, st));
            }
        }
        return nodes;
    }

    // «введи ответ» и «кейс с ветвлением» есть только в приложении; в боте такие задания пропускаются
    static readonly HashSet<string> AppOnly = new() { "input", "branch" };

    public static List<TaskItem> PickTasks(ProgressData p, Lesson lesson, int size = 8)
    {
        int level = LevelOf(p.Rating);
        var chosen = lesson.Tasks
            .Where(t => !AppOnly.Contains(t.Type))
            .OrderBy(t => Math.Abs(t.Difficulty - level) + Rnd.NextDouble() * 0.9)
            .Take(size)
            .ToList();
        return chosen.OrderBy(t => t.Difficulty).ThenBy(_ => Rnd.Next()).ToList();
    }

    public static List<T> Shuffle<T>(IEnumerable<T> items) => items.OrderBy(_ => Rnd.Next()).ToList();

    public static int DailyGoalXp(ProgressData p) => p.DailyGoalMin switch
    {
        5 => 20,
        10 => 40,
        15 => 60,
        20 => 80,
        _ => 40
    };

    public static int TodayXp(ProgressData p) => p.XpDate == TodayKey ? p.TodayXp : 0;

    public static void AddXp(ProgressData p, int n)
    {
        if (p.XpDate != TodayKey)
        {
            p.XpDate = TodayKey;
            p.TodayXp = 0;
        }
        p.TodayXp += n;
        p.TotalXp += n;
    }

    public static int DisplayStreak(ProgressData p)
    {
        if (!DateTime.TryParseExact(p.LastActive, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last))
            return 0;
        int diff = (Today - last.Date).Days;
        return diff <= 1 ? p.Streak : 0;
    }

    public static void RegisterActivity(ProgressData p)
    {
        var today = Today;
        if (DateTime.TryParseExact(p.LastActive, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last))
        {
            int diff = (today - last.Date).Days;
            if (diff == 0) return;
            p.Streak = diff == 1 ? p.Streak + 1 : 1;
        }
        else
        {
            p.Streak = 1;
        }
        p.LastActive = TodayKey;
    }

    public static List<string> CheckAchievements(ProgressData p)
    {
        var earned = new List<string>();
        int level = LevelOf(p.Rating);

        void Give(string id, bool cond)
        {
            if (!cond || p.Achievements.Contains(id)) return;
            p.Achievements.Add(id);
            earned.Add(AchDefs.First(x => x.Id == id).Title);
        }

        Give("first", p.Done.Count >= 1);
        Give("streak3", p.Streak >= 3);
        Give("streak7", p.Streak >= 7);
        Give("perfect", p.PerfectLessons >= 1);
        Give("ten", p.Done.Count >= 10);
        Give("lv3", level >= 3);
        Give("lv5", level >= 5);
        Give("quiz", p.History.Count >= 1);
        return earned;
    }

    public static QOutcome Score(Questionnaire q, List<int> answers)
    {
        var values = q.Scales.ToDictionary(x => x.Key, _ => 0);
        var maxes = q.Scales.ToDictionary(x => x.Key, _ => 0);

        if (q.IsYesNo)
        {
            foreach (var sc in q.Scales)
            {
                maxes[sc.Key] = sc.Yes.Count + sc.No.Count;
                foreach (int n in sc.Yes)
                    if (n >= 1 && n <= answers.Count && answers[n - 1] == 1) values[sc.Key]++;
                foreach (int n in sc.No)
                    if (n >= 1 && n <= answers.Count && answers[n - 1] == 0) values[sc.Key]++;
            }
        }
        else
        {
            for (int i = 0; i < q.Questions.Count && i < answers.Count; i++)
            {
                var opts = q.Questions[i].Options;
                foreach (var g in opts.GroupBy(o => o.Scale))
                    if (maxes.ContainsKey(g.Key)) maxes[g.Key] += g.Max(o => o.Points);
                int a = answers[i];
                if (a >= 0 && a < opts.Count && values.ContainsKey(opts[a].Scale))
                    values[opts[a].Scale] += opts[a].Points;
            }
        }

        var res = new QOutcome();
        foreach (var sc in q.Scales)
        {
            int v = values[sc.Key];
            var range = sc.Ranges.OrderBy(r => r.Max).FirstOrDefault(r => v <= r.Max) ?? sc.Ranges.LastOrDefault();
            res.Scores.Add(new QScore { Scale = sc, Value = v, Max = maxes[sc.Key], Range = range });
        }

        if (q.Mode == "dominant" && res.Scores.Count > 0)
        {
            var ordered = res.Scores.OrderByDescending(x => x.Value).ToList();
            res.Main = ordered[0];
            if (ordered.Count > 1 && ordered[1].Value > 0 && ordered[1].Value >= 0.8 * ordered[0].Value)
                res.Second = ordered[1];
            res.Headline = res.Main.Scale.Name;
            if (res.Second != null) res.Headline += " + " + res.Second.Scale.Name;
        }
        else
        {
            res.Headline = string.Join("; ", res.Scores.Select(x => x.Scale.Name + ": " + (x.Range?.Title ?? x.Value.ToString())));
        }
        return res;
    }

    public static string BlendText(Questionnaire q, QOutcome r)
    {
        if (r.Main == null || r.Second == null) return "";
        var pair = new[] { r.Main.Scale, r.Second.Scale }
            .OrderBy(x => q.Scales.IndexOf(x))
            .Select(x => x.Key)
            .ToArray();
        string template = q.BlendTemplate
            .Replace("{main}", r.Main.Scale.Name)
            .Replace("{secondary}", r.Second.Scale.Name);
        string extra = q.BlendTexts.TryGetValue(pair[0] + "+" + pair[1], out var t) ? t : "";
        return (template + " " + extra).Trim();
    }

    // запись в историю профиля; на сервер её отправляет вызывающий
    public static void SaveResult(ProgressData p, Questionnaire q, QOutcome r)
    {
        var item = new QHistoryItem
        {
            Id = q.Id,
            Title = q.Title,
            Date = Now.ToString("dd.MM.yyyy HH:mm"),
            Headline = r.Headline,
            Lines = r.Scores.Select(x => $"{x.Scale.Name}: {x.Value}/{x.Max}").ToList()
        };
        p.History.Insert(0, item);
        if (p.History.Count > 50) p.History.RemoveRange(50, p.History.Count - 50);
        AddXp(p, 15);
        RegisterActivity(p);
        CheckAchievements(p);
    }
}
