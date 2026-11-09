using System.Text;
using PsyPath.Models;
using Telegram.Bot.Types.ReplyMarkups;
using static PsyPath.Bot.Html;

namespace PsyPath.Bot;

public record Screen(string Text, InlineKeyboardMarkup? Keyboard);

// один урок в чате: то же, что LessonPage приложения, но экран — одно сообщение, которое бот правит
public class LessonRun
{
    public int Id { get; }
    public Section Section { get; }
    public Lesson Lesson { get; }
    public int MessageId { get; set; }
    public TaskItem? Cur { get; private set; }
    public bool IsChecked { get; private set; }
    public bool AskingQuit { get; set; }

    readonly List<TaskItem> queue;
    readonly HashSet<TaskItem> retried = new();
    readonly HashSet<TaskItem> answeredFirst = new();
    readonly HashSet<TaskItem> solved = new();
    readonly int total;
    readonly double ratingStart;
    readonly int levelStart;
    int firstTryCorrect;
    int mistakes;
    int xpGained;
    bool lastOk;

    // состояние текущего задания
    List<int> optionOrder = new();
    int selected = -1;
    List<int> bank = new();
    readonly List<int> chosen = new();
    List<int> right = new();
    bool[] matched = Array.Empty<bool>();
    int wrongTries;

    public LessonRun(int id, Section section, Lesson lesson, ProgressData p)
    {
        Id = id;
        Section = section;
        Lesson = lesson;
        queue = Game.PickTasks(p, lesson);
        total = queue.Count;
        ratingStart = p.Rating;
        levelStart = Game.LevelOf(p.Rating);
    }

    string Cb(string verb, int arg = 0) => $"l:{Id}:{verb}:{arg}";

    // false — задания кончились, пора подводить итог
    public bool Next()
    {
        AskingQuit = false;
        if (queue.Count == 0)
        {
            Cur = null;
            return false;
        }
        Cur = queue[0];
        queue.RemoveAt(0);
        IsChecked = false;
        selected = -1;
        chosen.Clear();
        wrongTries = 0;

        int options = Cur.Type == "truefalse" ? 2 : Cur.Options.Count;
        optionOrder = Game.Shuffle(Enumerable.Range(0, options));
        bank = Game.Shuffle(Enumerable.Range(0, Cur.Items.Count));
        right = Game.Shuffle(Enumerable.Range(0, Cur.Pairs.Count));
        matched = new bool[Cur.Pairs.Count];
        return true;
    }

    int Correct(TaskItem t) => t.Type == "truefalse" ? (t.AnswerBool ? 0 : 1) : t.AnswerIndex;
    static string TfText(int i) => i == 0 ? "ВЕРНО" : "НЕВЕРНО";

    // выбор варианта: choice, fill, case, truefalse
    public bool Choose(ProgressData p, int idx)
    {
        if (Cur == null || IsChecked || Cur.Type is "order" or "match" || !optionOrder.Contains(idx)) return false;
        selected = idx;
        Check(p, idx == Correct(Cur));
        return true;
    }

    public bool PickOrder(ProgressData p, int idx)
    {
        if (Cur == null || IsChecked || Cur.Type != "order" || chosen.Contains(idx) || idx < 0 || idx >= Cur.Items.Count) return false;
        chosen.Add(idx);
        if (chosen.Count == Cur.Items.Count)
            Check(p, chosen.Select((x, i) => x == i).All(x => x));
        return true;
    }

    public bool ResetOrder()
    {
        if (Cur == null || IsChecked || Cur.Type != "order" || chosen.Count == 0) return false;
        chosen.Clear();
        return true;
    }

    int CurrentLeft => Array.IndexOf(matched, false);

    // пара для текущего левого элемента; null — нажатие не к месту, иначе текст подсказки
    public string? PickMatch(ProgressData p, int idx)
    {
        if (Cur == null || IsChecked || Cur.Type != "match" || idx < 0 || idx >= matched.Length || matched[idx]) return null;
        int left = CurrentLeft;
        if (idx != left)
        {
            wrongTries++;
            return "Не та пара, попробуй другую";
        }
        matched[idx] = true;
        if (matched.All(x => x)) Check(p, wrongTries == 0);
        return "";
    }

    // то же, что DoCheck приложения: рейтинг — по первой попытке, ошибка повторяется один раз в конце
    void Check(ProgressData p, bool ok)
    {
        var t = Cur!;
        IsChecked = true;
        lastOk = ok;

        if (answeredFirst.Add(t))
        {
            p.Rating = Game.UpdateRating(p.Rating, t.Difficulty, ok);
            p.Answered++;
            if (ok)
            {
                firstTryCorrect++;
                p.Correct++;
            }
        }

        if (ok)
        {
            solved.Add(t);
            xpGained += 10;
        }
        else
        {
            mistakes++;
            if (retried.Add(t)) queue.Add(t);
        }
    }

    static string Instruction(string type) => type switch
    {
        "truefalse" => "ВЕРНО ИЛИ НЕТ?",
        "fill" => "ЗАПОЛНИ ПРОПУСК",
        "case" => "МИНИ-КЕЙС",
        "match" => "СОЕДИНИ ПАРЫ",
        "order" => "РАССТАВЬ ПО ПОРЯДКУ",
        _ => "ВЫБЕРИ ВЕРНЫЙ ОТВЕТ"
    };

    static string CorrectAnswerText(TaskItem t) => t.Type switch
    {
        "truefalse" => t.AnswerBool ? "Верно" : "Неверно",
        "order" => string.Join(" → ", t.Items),
        "match" => string.Join("; ", t.Pairs.Select(x => x[0] + " — " + x[1])),
        _ => t.AnswerIndex >= 0 && t.AnswerIndex < t.Options.Count ? t.Options[t.AnswerIndex] : ""
    };

    public Screen Render()
    {
        var t = Cur!;
        var sb = new StringBuilder();
        sb.Append(B($"УРОК {Lesson.Id} · {Lesson.Title}")).Append('\n');
        sb.Append($"{Bar(total == 0 ? 1 : (double)solved.Count / total, 8)} {solved.Count}/{total}\n");
        sb.Append(E($"{Instruction(t.Type)} · {Meter(t.Difficulty)}")).Append("\n\n");
        if (t.Type == "case" && !string.IsNullOrWhiteSpace(t.Story))
            sb.Append(I(t.Story)).Append("\n\n");
        sb.Append(B(t.Q));

        var rows = new List<InlineKeyboardButton[]>();
        switch (t.Type)
        {
            case "order":
                RenderOrder(t, sb, rows);
                break;
            case "match":
                RenderMatch(t, sb, rows);
                break;
            case "truefalse":
                RenderTrueFalse(t, sb, rows);
                break;
            default:
                RenderChoice(t, sb, rows);
                break;
        }

        if (IsChecked)
        {
            sb.Append("\n\n").Append(B(lastOk ? "✓ ВЕРНО!" : "✗ НЕ СОВСЕМ"));
            if (!lastOk) sb.Append('\n').Append(E("Правильно: " + CorrectAnswerText(t)));
            if (!string.IsNullOrWhiteSpace(t.Explain)) sb.Append("\n\n").Append(E(t.Explain));
            rows.Clear();
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("ПРОДОЛЖИТЬ ▶", Cb("n")) });
        }
        else if (AskingQuit)
        {
            sb.Append("\n\n").Append(B("ВЫЙТИ?")).Append(' ').Append(E("Прогресс этого урока будет потерян."));
            rows.Clear();
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("ВЫЙТИ", Cb("y")),
                InlineKeyboardButton.WithCallbackData("ОСТАТЬСЯ", Cb("s"))
            });
        }
        else
        {
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("✕ выйти из урока", Cb("x")) });
        }
        return new Screen(sb.ToString(), new InlineKeyboardMarkup(rows));
    }

    void RenderChoice(TaskItem t, StringBuilder sb, List<InlineKeyboardButton[]> rows)
    {
        sb.Append("\n\n");
        int correct = Correct(t);
        for (int n = 0; n < optionOrder.Count; n++)
        {
            int idx = optionOrder[n];
            string mark = !IsChecked ? $"{n + 1}." : idx == correct ? "✓" : idx == selected ? "✗" : "·";
            sb.Append(E($"{mark} {t.Options[idx]}")).Append('\n');
        }
        // номера в ряд: варианты длинные и целиком видны только в тексте
        var buttons = optionOrder.Select((idx, n) => InlineKeyboardButton.WithCallbackData((n + 1).ToString(), Cb("c", idx)));
        foreach (var chunk in buttons.Chunk(4)) rows.Add(chunk);
    }

    void RenderTrueFalse(TaskItem t, StringBuilder sb, List<InlineKeyboardButton[]> rows)
    {
        if (IsChecked)
            sb.Append("\n\n").Append(E($"Твой ответ: {TfText(selected)} {(lastOk ? "✓" : "✗")}"));
        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData(TfText(0), Cb("c", 0)),
            InlineKeyboardButton.WithCallbackData(TfText(1), Cb("c", 1))
        });
    }

    void RenderOrder(TaskItem t, StringBuilder sb, List<InlineKeyboardButton[]> rows)
    {
        sb.Append("\n\n").Append(B("ТВОЙ ПОРЯДОК:")).Append('\n');
        if (chosen.Count == 0) sb.Append(I("(нажимай варианты по порядку)")).Append('\n');
        for (int i = 0; i < chosen.Count; i++)
        {
            string mark = IsChecked ? (chosen[i] == i ? " ✓" : " ✗") : "";
            sb.Append(E($"{i + 1}. {t.Items[chosen[i]]}{mark}")).Append('\n');
        }

        var left = bank.Where(x => !chosen.Contains(x)).ToList();
        if (left.Count > 0)
        {
            sb.Append('\n').Append(B("ВАРИАНТЫ:")).Append('\n');
            foreach (int idx in left) sb.Append(E("▸ " + t.Items[idx])).Append('\n');
        }
        foreach (int idx in left)
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData(Short(t.Items[idx]), Cb("o", idx)) });
        if (chosen.Count > 0)
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("↺ заново", Cb("r")) });
    }

    void RenderMatch(TaskItem t, StringBuilder sb, List<InlineKeyboardButton[]> rows)
    {
        sb.Append("\n\n");
        int current = CurrentLeft;
        for (int i = 0; i < t.Pairs.Count; i++)
        {
            if (matched[i]) sb.Append(E($"✓ {t.Pairs[i][0]} — {t.Pairs[i][1]}"));
            else if (i == current) sb.Append(B($"▶ {t.Pairs[i][0]} — ?"));
            else sb.Append(E($"· {t.Pairs[i][0]}"));
            sb.Append('\n');
        }
        if (IsChecked || current < 0)
        {
            if (wrongTries > 0) sb.Append('\n').Append(E($"Неверных пар: {wrongTries}"));
            return;
        }
        sb.Append('\n').Append(I($"Что подходит к «{t.Pairs[current][0]}»?"));
        foreach (int idx in right.Where(x => !matched[x]))
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData(Short(t.Pairs[idx][1]), Cb("m", idx)) });
    }

    // итог урока; прогресс уже изменён, отправить его на сервер — дело вызывающего
    public (string Text, int Accuracy, int Mistakes, int Xp) Finish(ProgressData p, Content c)
    {
        xpGained += 10 + (mistakes == 0 ? 10 : 0);
        Game.AddXp(p, xpGained);
        Game.RegisterActivity(p);
        if (!p.Done.Contains(Lesson.Id)) p.Done.Add(Lesson.Id);
        if (mistakes == 0) p.PerfectLessons++;
        var newAch = Game.CheckAchievements(p);

        int levelNow = Game.LevelOf(p.Rating);
        int accuracy = total == 0 ? 100 : (int)Math.Round(firstTryCorrect * 100.0 / total);
        int ratingDelta = (int)Math.Round(p.Rating - ratingStart);

        var sb = new StringBuilder();
        sb.Append(B("▶▶ УРОК ПРОЙДЕН")).Append('\n').Append(E(Lesson.Title)).Append("\n\n");
        sb.Append(E($"XP +{xpGained}   ТОЧНОСТЬ {accuracy}%   РЕЙТИНГ {(ratingDelta >= 0 ? "+" : "")}{ratingDelta}"));
        if (mistakes == 0) sb.Append('\n').Append(E("Без единой ошибки: +10 XP сверху"));

        if (levelNow > levelStart)
        {
            var lv = Game.LevelInfoOf(c, levelNow);
            sb.Append("\n\n").Append(B($"НОВЫЙ УРОВЕНЬ! LV {levelNow} {lv.Name.ToUpperInvariant()}"));
            foreach (var u in c.Rating.SectionUnlock.Where(u => u.UnlockLevel > levelStart && u.UnlockLevel <= levelNow))
                sb.Append('\n').Append(E($"▸ Открыт раздел {u.Section}: {u.Title}"));
        }
        if (newAch.Count > 0)
        {
            sb.Append("\n\n").Append(B("НОВЫЕ ДОСТИЖЕНИЯ ★"));
            foreach (var a in newAch) sb.Append('\n').Append(E("★ " + a));
        }
        return (sb.ToString(), accuracy, mistakes, xpGained);
    }
}
