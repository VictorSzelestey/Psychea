using System.Text;
using PsyPath.Models;
using Telegram.Bot.Types.ReplyMarkups;
using static PsyPath.Bot.Html;

namespace PsyPath.Bot;

// прохождение опросника: вопрос за вопросом в одном сообщении, как QuestionnairePage
public class QuizRun(int id, Questionnaire quiz)
{
    public int Id { get; } = id;
    public Questionnaire Quiz { get; } = quiz;
    public int MessageId { get; set; }
    public List<int> Answers { get; } = new();
    // порядок вариантов перемешан один раз на вопрос, чтобы «назад» показывал тот же экран
    readonly Dictionary<int, List<int>> orders = new();

    public int Index => Answers.Count;
    public bool Done => Index >= Quiz.Questions.Count;

    string Cb(string verb, int arg = 0) => $"q:{Id}:{verb}:{arg}";

    List<int> Order(int question)
    {
        if (!orders.TryGetValue(question, out var o))
            orders[question] = o = Game.Shuffle(Enumerable.Range(0, Quiz.Questions[question].Options.Count));
        return o;
    }

    public bool Answer(int value)
    {
        if (Done) return false;
        bool valid = Quiz.IsYesNo ? value is 0 or 1 : Order(Index).Contains(value);
        if (!valid) return false;
        Answers.Add(value);
        return true;
    }

    public bool Back()
    {
        if (Answers.Count == 0) return false;
        Answers.RemoveAt(Answers.Count - 1);
        return true;
    }

    public Screen Render()
    {
        var q = Quiz.Questions[Index];
        int count = Quiz.Questions.Count;
        var sb = new StringBuilder();
        sb.Append(B(Quiz.Title.ToUpperInvariant())).Append('\n');
        sb.Append($"{Bar((double)Index / count, 8)} ВОПРОС {Index + 1:00}/{count:00}\n\n");
        sb.Append(B(q.Q));

        var rows = new List<InlineKeyboardButton[]>();
        if (Quiz.IsYesNo)
        {
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("ДА", Cb("a", 1)),
                InlineKeyboardButton.WithCallbackData("НЕТ", Cb("a", 0))
            });
        }
        else
        {
            sb.Append("\n\n");
            var order = Order(Index);
            for (int n = 0; n < order.Count; n++)
                sb.Append(E($"{n + 1}. {q.Options[order[n]].Text}")).Append('\n');
            var buttons = order.Select((idx, n) => InlineKeyboardButton.WithCallbackData((n + 1).ToString(), Cb("a", idx)));
            foreach (var chunk in buttons.Chunk(4)) rows.Add(chunk);
        }

        var nav = new List<InlineKeyboardButton>();
        if (Index > 0) nav.Add(InlineKeyboardButton.WithCallbackData("◀ назад", Cb("b")));
        nav.Add(InlineKeyboardButton.WithCallbackData("✕ выйти", Cb("x")));
        rows.Add(nav.ToArray());
        return new Screen(sb.ToString(), new InlineKeyboardMarkup(rows));
    }

    // текст результата — как экран «РЕЗУЛЬТАТ» приложения
    public static string ResultText(Questionnaire quiz, QOutcome res)
    {
        var sb = new StringBuilder();
        sb.Append(B("РЕЗУЛЬТАТ · " + quiz.Title.ToUpperInvariant()));

        if (res.Main != null)
        {
            var main = res.Main.Scale;
            sb.Append("\n\n").Append(B(main.Name.ToUpperInvariant()));
            if (!string.IsNullOrWhiteSpace(main.Tagline)) sb.Append('\n').Append(I(main.Tagline));

            double sum = Math.Max(1, res.Scores.Sum(x => x.Value));
            sb.Append("\n\n");
            foreach (var s in res.Scores)
                sb.Append(E($"{Bar(s.Value / sum)} {Math.Round(s.Value * 100 / sum)}% {s.Scale.Name}")).Append('\n');

            string blend = Game.BlendText(quiz, res);
            if (!string.IsNullOrEmpty(blend)) sb.Append('\n').Append(E(blend)).Append('\n');
            if (!string.IsNullOrWhiteSpace(main.Description)) sb.Append('\n').Append(E(main.Description));
            Bullets(sb, "СИЛЬНЫЕ СТОРОНЫ", main.Strengths);
            Bullets(sb, "НА ЧТО ОБРАТИТЬ ВНИМАНИЕ", main.WatchOut);
            Bullets(sb, "КАК ТЕБЕ УЧИТЬСЯ", main.StudyTips);
            if (!string.IsNullOrWhiteSpace(main.PsychFit)) Bullets(sb, "ЧТО ПОДОЙДЁТ В ПСИХОЛОГИИ", new[] { main.PsychFit });
        }
        else
        {
            foreach (var s in res.Scores)
            {
                sb.Append("\n\n").Append(B(s.Scale.Name.ToUpperInvariant())).Append('\n');
                sb.Append(E($"{Bar(s.Max == 0 ? 0 : (double)s.Value / s.Max)} {s.Value} из {s.Max}"));
                if (s.Range != null)
                {
                    sb.Append('\n').Append(B(s.Range.Title));
                    if (!string.IsNullOrWhiteSpace(s.Range.Text)) sb.Append('\n').Append(E(s.Range.Text));
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(quiz.Disclaimer)) sb.Append("\n\n").Append(I(quiz.Disclaimer));
        sb.Append("\n\n").Append(E("+15 XP · результат сохранён в профиле"));
        return sb.ToString();
    }

    static void Bullets(StringBuilder sb, string title, IEnumerable<string> lines)
    {
        var list = lines.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (list.Count == 0) return;
        sb.Append("\n\n").Append(B(title));
        foreach (var l in list) sb.Append('\n').Append(E("▸ " + l));
    }
}
