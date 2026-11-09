using System.Globalization;
using System.Text.Json;
using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

// карточка одного пользователя: прогресс, все его опросники с баллами по шкалам, пройденные уроки
public class AdminUserPage : AdminBase
{
    readonly long id;

    public AdminUserPage(long id)
    {
        this.id = id;
        Show("ПОЛЬЗОВАТЕЛЬ", Message("ЗАГРУЗКА…"));
        _ = Load();
    }

    async Task Load()
    {
        var r = await Api.AdminUser(id);
        if (!r.Ok)
        {
            Show("ПОЛЬЗОВАТЕЛЬ", Message(r.Error, Pal.RedInk));
            return;
        }

        var u = r.Body.GetProperty("user");
        var stack = List();

        var head = new VerticalStackLayout { Spacing = 2 };
        head.Children.Add(Ui.Text(u.GetProperty("displayName").GetString()!.ToUpperInvariant(), 18, Pal.Ink, true));
        head.Children.Add(Ui.Text("@" + u.GetProperty("login").GetString() +
            (u.GetProperty("role").GetString() == "admin" ? "  //  АДМИН" : ""), 12, Pal.Ink2, true));
        head.Children.Add(Ui.Text($"Регистрация: {Date(u.GetProperty("createdAt"))}, последний вход: {Date(u.GetProperty("lastLoginAt"))}", 11, Pal.Ink2));
        stack.Children.Add(Ui.Box(head, Pal.Paper, 12));

        var grid = new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 8,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) }
        };
        void Cell(string text, int row, int col, string[]? icon = null)
        {
            var lcd = Ui.Lcd(text, 13, icon);
            Grid.SetRow(lcd, row);
            Grid.SetColumn(lcd, col);
            grid.Children.Add(lcd);
        }
        Cell($"РЕЙТИНГ {u.GetProperty("rating").GetDouble():0}", 0, 0);
        Cell($"{u.GetProperty("totalXp").GetInt32()} XP", 0, 1, Icons.Bolt);
        Cell($"{u.GetProperty("streak").GetInt32()} ДН.", 1, 0, Icons.Flame);
        Cell($"ТОЧНОСТЬ {u.GetProperty("accuracy").GetInt32()}%", 1, 1);
        stack.Children.Add(grid);

        stack.Children.Add(Ui.Text("ОПРОСНИКИ", 12, Pal.RedInk, true));
        var quizzes = r.Body.GetProperty("questionnaires").EnumerateArray().ToList();
        if (quizzes.Count == 0) stack.Children.Add(Ui.Text("Опросники ещё не проходил.", 12, Pal.Ink2));
        foreach (var q in quizzes) stack.Children.Add(QuizCard(q));

        stack.Children.Add(Ui.Text("УРОКИ", 12, Pal.RedInk, true));
        var lessons = r.Body.GetProperty("lessons").EnumerateArray().ToList();
        if (lessons.Count == 0) stack.Children.Add(Ui.Text("Уроки ещё не проходил.", 12, Pal.Ink2));
        var titles = AppState.Lessons.Sections.SelectMany(s => s.Lessons).DistinctBy(l => l.Id).ToDictionary(l => l.Id, l => l.Title);
        foreach (var l in lessons)
        {
            string lid = l.GetProperty("lessonId").GetString() ?? "";
            var box = new VerticalStackLayout { Spacing = 2 };
            box.Children.Add(Ui.Text("▸ " + titles.GetValueOrDefault(lid, lid), 13, Pal.Ink, true));
            box.Children.Add(Ui.Text(
                $"{Date(l.GetProperty("finishedAt"))}  //  точность {l.GetProperty("accuracy").GetInt32()}%, ошибок {l.GetProperty("mistakes").GetInt32()}, +{l.GetProperty("xp").GetInt32()} XP",
                11, Pal.Ink2));
            stack.Children.Add(Ui.Card(box, Pal.Paper, 10));
        }

        Show("ПОЛЬЗОВАТЕЛЬ", stack, animate: true);
    }

    // результат опросника: заголовок и полоски по шкалам с их названиями из текущего контента
    static View QuizCard(JsonElement q)
    {
        string qid = q.GetProperty("questionnaireId").GetString() ?? "";
        var quiz = AppState.Questionnaires.FirstOrDefault(x => x.Id == qid);

        var box = new VerticalStackLayout { Spacing = 6 };
        box.Children.Add(Ui.Text((quiz?.Title ?? qid).ToUpperInvariant() + "  //  " + Date(q.GetProperty("takenAt")), 10, Pal.Ink2, true));
        box.Children.Add(Ui.Text(q.GetProperty("headline").GetString() ?? "", 14, Pal.Ink, true));

        var rows = new List<(string, double, string, Color)>();
        int i = 0;
        foreach (var s in q.GetProperty("scores").EnumerateArray())
        {
            string key = s.TryGetProperty("scale", out var k) ? k.GetString() ?? "" : "";
            int value = s.TryGetProperty("value", out var v) ? v.GetInt32() : 0;
            int max = s.TryGetProperty("max", out var m) ? m.GetInt32() : 0;
            var scale = quiz?.Scales.FirstOrDefault(x => x.Key == key);
            rows.Add(((scale?.Name ?? key), max == 0 ? 0 : (double)value / max, $"{value}/{max}", Pal.Cycle(i++)));
        }
        if (rows.Count > 0) box.Children.Add(Ui.Bars(rows));
        return Ui.Card(box, Pal.Paper, 12);
    }

    static string Date(JsonElement e) =>
        e.ValueKind == JsonValueKind.String
            ? DateTime.Parse(e.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToLocalTime().ToString("dd.MM.yyyy HH:mm")
            : "—";
}
