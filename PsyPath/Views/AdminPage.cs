using System.Text.Json;
using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

// главный экран админки: статистика и переходы к пользователям и контенту
public class AdminPage : AdminBase
{
    public AdminPage()
    {
        Show("АДМИН", Message("ЗАГРУЗКА…"));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var r = await Api.AdminStats();
        if (!r.Ok)
        {
            Show("АДМИН", Message(r.Error, Pal.RedInk));
            return;
        }
        Show("АДМИН", Build(r.Body), animate: true);
    }

    View Build(JsonElement stats)
    {
        var stack = List();
        var o = stats.GetProperty("overview");
        var avg = o.GetProperty("avgRating");

        var grid = new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 8,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) }
        };
        void Cell(string text, int row, int col)
        {
            var lcd = Ui.Lcd(text, 13);
            Grid.SetRow(lcd, row);
            Grid.SetColumn(lcd, col);
            grid.Children.Add(lcd);
        }
        Cell($"ЮЗЕРОВ {o.GetProperty("usersTotal").GetInt64()}", 0, 0);
        Cell($"АКТИВНЫ 7Д {o.GetProperty("active7d").GetInt64()}", 0, 1);
        Cell($"СР.РЕЙТИНГ {(avg.ValueKind == JsonValueKind.Number ? avg.GetDecimal().ToString("0") : "—")}", 1, 0);
        Cell($"УРОКОВ {o.GetProperty("lessonsFinished").GetInt64()}", 1, 1);
        Cell($"ОПРОСНИКОВ {o.GetProperty("questionnairesTaken").GetInt64()}", 2, 0);
        stack.Children.Add(grid);

        void Link(string text, Color fill, Func<Page> page)
        {
            var b = new Btn(text, fill);
            b.Clicked += async (s, e) => await Push(page());
            stack.Children.Add(b);
        }
        Link("ПОЛЬЗОВАТЕЛИ ▶", Pal.Paper2, () => new AdminUsersPage());
        Link("УРОКИ ✎", Pal.Amber, () => new AdminContentPage("lessons"));
        Link("ОПРОСНИКИ ✎", Pal.Amber, () => new AdminContentPage("questionnaires"));

        var lessons = new VerticalStackLayout { Spacing = 8 };
        lessons.Children.Add(Ui.Text("УРОКИ: ПРОХОЖДЕНИЯ И ТОЧНОСТЬ", 12, Pal.RedInk, true));
        var titles = AppState.Lessons.Sections.SelectMany(s => s.Lessons).DistinctBy(l => l.Id).ToDictionary(l => l.Id, l => l.Title);
        var rows = stats.GetProperty("lessons").EnumerateArray().Select(l =>
        {
            string id = l.GetProperty("lessonId").GetString() ?? "";
            double acc = (double)l.GetProperty("avgAccuracy").GetDecimal();
            return (titles.GetValueOrDefault(id, id), acc / 100, $"{acc:0}% ×{l.GetProperty("attempts").GetInt64()}", Pal.Teal);
        }).ToList();
        if (rows.Count == 0) lessons.Children.Add(Ui.Text("Пока никто не проходил уроки.", 12, Pal.Ink2));
        else lessons.Children.Add(Ui.Bars(rows));
        stack.Children.Add(Ui.Card(lessons, Pal.Paper, 12));

        var quiz = new VerticalStackLayout { Spacing = 4 };
        quiz.Children.Add(Ui.Text("ОПРОСНИКИ: РЕЗУЛЬТАТЫ", 12, Pal.RedInk, true));
        var qTitles = AppState.Questionnaires.DistinctBy(q => q.Id).ToDictionary(q => q.Id, q => q.Title);
        var groups = stats.GetProperty("questionnaires").EnumerateArray()
            .GroupBy(q => q.GetProperty("questionnaireId").GetString() ?? "")
            .ToList();
        if (groups.Count == 0) quiz.Children.Add(Ui.Text("Пока никто не проходил опросники.", 12, Pal.Ink2));
        foreach (var g in groups)
        {
            quiz.Children.Add(Ui.Text(qTitles.GetValueOrDefault(g.Key, g.Key).ToUpperInvariant(), 13, Pal.Ink, true));
            foreach (var q in g)
                quiz.Children.Add(Ui.Text($"▸ {q.GetProperty("headline").GetString()} — {q.GetProperty("times").GetInt64()}", 12));
        }
        stack.Children.Add(Ui.Card(quiz, Pal.Paper, 12));
        return stack;
    }
}
