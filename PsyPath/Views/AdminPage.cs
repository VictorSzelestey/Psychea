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
        if (Api.User?.CanEditContent != true)
        {
            Show("КОНТЕНТ", Message("Нет доступа к управлению контентом.", Pal.RedInk));
            return;
        }
        if (Api.User.IsManager)
        {
            Show("КОНТЕНТ", ContentLinks(), animate: true);
            return;
        }
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

        var users = new Btn("ПОЛЬЗОВАТЕЛИ ▶", Pal.Paper2);
        users.Clicked += async (_, _) => await Push(new AdminUsersPage());
        stack.Children.Add(users);
        var promo = new Btn("ПРОМОКОДЫ ▶", Pal.Paper2);
        promo.Clicked += async (_, _) => await Push(new AdminPromoPage());
        stack.Children.Add(promo);
        stack.Children.Add(CrystalsCard());
        stack.Children.Add(IslandsCard());
        stack.Children.Add(ContentLinks());

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

    // кристаллы себе: проверить магазин без прохождения уроков. Только у админа
    View CrystalsCard()
    {
        var box = new VerticalStackLayout { Spacing = 8 };
        box.Children.Add(Ui.Text("КРИСТАЛЛЫ АДМИНУ", 12, Pal.RedInk, true));
        var balance = Ui.Lcd(Crystals.Balance.ToString(), 16, Icons.Crystal, Pal.Teal, Pal.Cyan);
        balance.HorizontalOptions = LayoutOptions.Center;
        balance.Padding = new Thickness(18, 8);
        box.Children.Add(balance);

        var row = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
        };
        int col = 0;
        foreach (int n in new[] { 100, 1000 })
        {
            var give = new Btn($"+{n}", Pal.Violet, Pal.OnAccent, 15);
            give.Clicked += (_, _) =>
            {
                if (Api.User?.IsAdmin != true) return;
                Crystals.Earn(n);
                AppState.Save();
                Sound.Play("ok");
                ((Label)((HorizontalStackLayout)balance.Content!).Children[1]).Text = Crystals.Balance.ToString();
            };
            Grid.SetColumn(give, col++);
            row.Children.Add(give);
        }
        box.Children.Add(row);
        box.Children.Add(Ui.Text("Начисляются на этот аккаунт и уходят на сервер вместе с прогрессом.", 11, Pal.Ink2));
        return Ui.Card(box, Pal.Paper, 12);
    }

    // острова себе без прохождения уроков: проверить остров, его тему и падение замка. Астрал не открывается
    View IslandsCard()
    {
        var box = new VerticalStackLayout { Spacing = 8 };
        box.Children.Add(Ui.Text("ОСТРОВА АДМИНУ", 12, Pal.RedInk, true));
        foreach (var island in Islands.All.Where(i => i.Number > 1 && !i.Soon))
        {
            bool open = Islands.IsOpen(island);
            var btn = new Btn(open ? $"✓ {island.Name} открыт" : $"ОТКРЫТЬ «{island.Name}»", open ? Pal.Paper2 : Pal.Violet, open ? Pal.Ink2 : Pal.OnAccent, 14);
            btn.Enabled = !open;
            var target = island;
            btn.Clicked += (_, _) =>
            {
                if (Api.User?.IsAdmin != true || Islands.IsOpen(target)) return;
                Islands.OpenByAdmin(target);
                Sound.Play("win");
                btn.Text = $"✓ {target.Name} открыт";
                btn.SetColors(Pal.Paper2, Pal.Ink2);
                btn.Enabled = false;
            };
            box.Children.Add(btn);
        }
        box.Children.Add(Ui.Text("Замок упадёт на карте при следующем показе главной. Тема острова откроется в магазине.", 11, Pal.Ink2));
        return Ui.Card(box, Pal.Paper, 12);
    }

    View ContentLinks()
    {
        var stack = List();
        stack.Children.Add(Ui.Text("Добавляй и редактируй материалы, которые увидят пользователи приложения.", 14));
        foreach (var (name, title) in new[] { ("reference", "СПРАВОЧНИК"), ("lessons", "УРОКИ"),
            ("questionnaires", "ОПРОСНИКИ"), ("rating", "РЕЙТИНГ И УРОВНИ") })
        {
            var edit = new Btn(title + " ✎", Pal.Amber) { MinimumHeightRequest = 48 };
            edit.Clicked += async (_, _) => await Push(new AdminContentPage(name));
            stack.Children.Add(edit);
        }
        return stack;
    }
}
