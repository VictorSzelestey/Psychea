using System.Globalization;
using System.Text;
using PsyPath.Models;

namespace PsyPath.Services;

// задание урока вместе с ключом, по которому оно хранится в «работе над ошибками»
public record PracticeTask(string Key, TaskItem Task);

// работа над ошибками, карточки справочника, проверка введённого ответа, поиск статьи для задания
public static class Practice
{
    // в прогрессе хранится не больше, чтобы он не разрастался (сервер принимает до 200 КБ)
    const int MaxMistakes = 150;

    // промежутки прежней системы по номеру коробки: с них продолжаются уже начатые карточки
    static readonly int[] LegacyIntervals = { 0, 1, 3, 7, 14, 30 };

    static readonly Random Rnd = new();

    static string Today => DateTime.Today.ToString("yyyy-MM-dd");

    // ключ не зависит от порядка заданий в уроке: урок + хеш типа и вопроса.
    // string.GetHashCode меняется от запуска к запуску, поэтому свой FNV-1a
    public static string TaskKey(string lessonId, TaskItem t)
    {
        uint h = 2166136261;
        foreach (char c in t.Type + "|" + t.Q)
        {
            h ^= c;
            h *= 16777619;
        }
        return lessonId + ":" + h.ToString("x8");
    }

    public static List<PracticeTask> ForLesson(Lesson lesson, List<TaskItem> tasks) =>
        tasks.Select(t => new PracticeTask(TaskKey(lesson.Id, t), t)).ToList();

    // первая попытка в уроке или на повторе: ошибка — в список, верно — из списка
    public static void Record(string key, bool ok)
    {
        var m = AppState.P.Mistakes;
        if (ok)
        {
            m.Remove(key);
            return;
        }
        m[key] = Today;
        if (m.Count > MaxMistakes)
            foreach (var old in m.OrderBy(x => x.Value).Take(m.Count - MaxMistakes).Select(x => x.Key).ToList())
                m.Remove(old);
    }

    // ошибки, задания которых ещё есть в уроках (админ мог их удалить или поменять)
    public static List<PracticeTask> MistakeTasks()
    {
        var all = new Dictionary<string, PracticeTask>();
        foreach (var l in AppState.Lessons.Sections.SelectMany(s => s.Lessons))
            foreach (var t in l.Tasks)
                all.TryAdd(TaskKey(l.Id, t), new PracticeTask(TaskKey(l.Id, t), t));

        return AppState.P.Mistakes
            .OrderBy(x => x.Value)
            .Where(x => all.ContainsKey(x.Key))
            .Select(x => all[x.Key])
            .ToList();
    }

    public static int MistakeCount => MistakeTasks().Count;

    // до size заданий: сначала самые давние ошибки, порядок внутри перемешан
    public static List<PracticeTask> PickReview(int size = 8) =>
        MistakeTasks().Take(size).OrderBy(_ => Rnd.Next()).ToList();

    // статья справочника для кнопки после ответа: явная ссылка или термин, который встречается в задании
    public static RefEntry? FindRef(TaskItem t)
    {
        if (!string.IsNullOrWhiteSpace(t.Ref))
            return AppState.Reference.FirstOrDefault(e => string.Equals(e.Term, t.Ref.Trim(), StringComparison.OrdinalIgnoreCase));

        string text = " " + Normalize(t.Q + " " + t.Explain + " " + t.Story) + " ";
        return AppState.Reference.FirstOrDefault(e =>
        {
            var words = Normalize(e.Term).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // «темперамент» найдётся и в «темперамента»: сравниваем начало слова без окончания
            return words.Length > 0 && words.All(w => text.Contains(" " + (w.Length > 5 ? w[..^2] : w)));
        });
    }

    // нижний регистр, ё → е, только буквы и цифры через один пробел
    public static string Normalize(string s)
    {
        var sb = new StringBuilder();
        bool space = false;
        foreach (char ch in s.ToLower(CultureInfo.GetCultureInfo("ru-RU")).Replace('ё', 'е'))
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (space && sb.Length > 0) sb.Append(' ');
                sb.Append(ch);
                space = false;
            }
            else space = true;
        }
        return sb.ToString();
    }

    // ответ засчитан, если совпал с одним из допустимых; мелкая опечатка прощается (typo = true)
    public static bool CheckInput(string input, IEnumerable<string> accepted, out bool typo)
    {
        typo = false;
        string a = Normalize(input);
        if (a.Length == 0) return false;
        foreach (var raw in accepted)
        {
            string b = Normalize(raw);
            if (b.Length == 0) continue;
            if (a == b) return true;
            int allowed = b.Length <= 4 ? 0 : b.Length <= 8 ? 1 : 2;
            if (allowed > 0 && Math.Abs(a.Length - b.Length) <= allowed && Distance(a, b) <= allowed)
            {
                typo = true;
                return true;
            }
        }
        return false;
    }

    // расстояние Левенштейна
    static int Distance(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) prev[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (int j = 1; j <= b.Length; j++)
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length];
    }

    // путь кейса с ветвлением по хорошим вариантам: «Выслушать → Уточнить»
    public static string BestPath(TaskItem t)
    {
        var steps = t.Steps ?? new List<CaseStep>();
        var parts = new List<string>();
        int i = 0;
        for (int guard = 0; guard <= steps.Count && i >= 0 && i < steps.Count; guard++)
        {
            var good = steps[i].Choices.FirstOrDefault(c => c.Good);
            if (good == null) break;
            parts.Add(good.Text);
            i = good.Next - 1;
        }
        return string.Join(" → ", parts);
    }

    // карточки к повтору сегодня: просроченные по дате, потом новые термины; всего до size
    public static List<RefEntry> PickCards(int size = 10)
    {
        var cards = AppState.P.Cards;
        string today = Today;
        var due = AppState.Reference
            .Where(e => cards.TryGetValue(e.Term, out var c) && string.CompareOrdinal(c.Due, today) <= 0)
            // среди одинаково просроченных первыми идут самые трудные
            .OrderBy(e => cards[e.Term].Due, StringComparer.Ordinal)
            .ThenBy(e => cards[e.Term].Ease)
            .ToList();
        var fresh = AppState.Reference.Where(e => !cards.ContainsKey(e.Term));
        return due.Concat(fresh).Take(size).ToList();
    }

    public static int DueCards => PickCards(int.MaxValue).Count;

    // ближайшая дата повтора, когда сегодня повторять нечего
    public static string? NextCardDate()
    {
        var next = AppState.P.Cards.Values.Select(c => c.Due).Where(d => d.Length > 0).OrderBy(d => d).FirstOrDefault();
        return next != null && DateTime.TryParseExact(next, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("dd.MM")
            : null;
    }

    static int LastDays(CardState c) =>
        c.Interval > 0 ? c.Interval : LegacyIntervals[Math.Clamp(c.Box, 0, LegacyIntervals.Length - 1)];

    // что будет с карточкой при этой оценке: подпись на кнопке и сам расчёт используют одно и то же
    public static CardPlan PlanFor(RefEntry e, CardRating rating)
    {
        var c = AppState.P.Cards.GetValueOrDefault(e.Term) ?? new CardState();
        return CardScheduler.Next(LastDays(c), c.Ease, rating);
    }

    public static void Answer(RefEntry e, CardRating rating)
    {
        var plan = PlanFor(e, rating);
        var cards = AppState.P.Cards;
        if (!cards.TryGetValue(e.Term, out var c)) cards[e.Term] = c = new CardState();
        c.Box = 0;
        c.Interval = plan.Days;
        c.Ease = plan.Ease;
        c.Due = DateTime.Today.AddDays(plan.Days).ToString("yyyy-MM-dd");
    }
}
