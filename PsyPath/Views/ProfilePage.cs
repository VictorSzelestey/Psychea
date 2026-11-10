using System.Text.Json;
using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

public class ProfilePage : DataPage
{
    readonly Label syncInfo = Ui.Text("", 11, Pal.Ink2, true);
    Elephant? elephant;
    bool active;

    // "student" → "СТУДЕНТ" и т.п.; ответ из онбординга показывается тут
    static string RoleTitle(string role) => role switch
    {
        "student" => "СТУДЕНТ",
        "psychologist" => "ПСИХОЛОГ",
        "curious" => "ПРОСТО ИНТЕРЕСНО",
        _ => role
    };

    // подписка только пока вкладка на экране: статическое событие иначе держало бы старые экраны
    protected override void OnAppearing()
    {
        base.OnAppearing();
        active = true;
        Sync.Changed += ShowSync;
        ShowSync();
        elephant?.Start();
        _ = RefreshAccount();
    }

    async Task RefreshAccount()
    {
        var user = Api.User;
        if (user == null) return;
        var result = await Api.Me();
        if (!result.Ok || Api.User?.Id != user.Id) return;
        if (result.Body.ValueKind != JsonValueKind.Object || !result.Body.TryGetProperty("user", out var account)) return;
        try
        {
            var updated = account.Deserialize<ApiUser>(AppState.JsonOptions);
            if (updated == null || updated.Id != user.Id || updated == Api.User) return;
            Api.UpdateUser(account);
            if (active) Refresh();
        }
        catch (JsonException ex) { CrashLog.Write("Refresh account", ex); }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        active = false;
        Sync.Changed -= ShowSync;
        elephant?.Stop();
    }

    // «ВСЁ ОТПРАВЛЕНО» или «ЖДЁТ СЕТИ: 3» — результаты, сделанные без интернета
    void ShowSync()
    {
        if (Api.User == null)
        {
            syncInfo.Text = "";
            return;
        }
        int n = Sync.Pending;
        syncInfo.Text = n == 0 ? "▣ ВСЁ СОХРАНЕНО НА СЕРВЕРЕ" : $"▒ ЖДЁТ СЕТИ: {n}. ОТПРАВИТСЯ САМО, КОГДА ПОЯВИТСЯ ИНТЕРНЕТ";
        syncInfo.TextColor = n == 0 ? Pal.Ink2 : Pal.RedInk;
    }

    protected override ScrollView Build()
    {
        var p = AppState.P;
        int level = AppState.Level;
        var lv = AppState.LevelInfoOf(level);

        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) }
        };

        var top = new VerticalStackLayout { Spacing = 0 };
        var title = new Heading("ПРОФИЛЬ", 24);
        title.Margin = new Thickness(18, 10, 18, 6);
        top.Children.Add(title);
        root.Children.Add(top);

        var stack = new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 6, 18, 30) };

        var user = Api.User;
        if (user != null)
        {
            var account = new VerticalStackLayout { Spacing = 2 };
            account.Children.Add(Ui.Text(user.DisplayName.ToUpperInvariant(), 16, Pal.Ink, true));
            account.Children.Add(Ui.Text("@" + user.Login + (user.RoleCaption.Length > 0 ? "  //  " + user.RoleCaption : ""), 11, Pal.Ink2, true));
            (syncInfo.Parent as Layout)?.Children.Remove(syncInfo);
            account.Children.Add(syncInfo);
            ShowSync();
            stack.Children.Add(Ui.Card(account, Pal.Paper2, 12));

            if (user.CanEditContent)
            {
                var admin = new Btn(user.IsAdmin ? "АДМИН-ПАНЕЛЬ ▶" : "УПРАВЛЕНИЕ КОНТЕНТОМ ▶", Pal.TealInk, Pal.Paper);
                admin.Clicked += async (s, e) => await Push(new AdminPage());
                stack.Children.Add(admin);
            }
        }

        var lvBox = new VerticalStackLayout { Spacing = 6 };
        lvBox.Children.Add(Ui.Mascot(120));
        var lvRow = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center };
        lvRow.Children.Add(new PixelIcon(Icons.Rank, Pal.Ink, 20, Pal.Amber));
        lvRow.Children.Add(Ui.Text($"LV {level}  {lv.Name.ToUpperInvariant()}", 20, Pal.Ink, true, TextAlignment.Center));
        lvBox.Children.Add(lvRow);
        if (!string.IsNullOrEmpty(p.Role))
            lvBox.Children.Add(Ui.Text(RoleTitle(p.Role), 12, Pal.Ink2, true, TextAlignment.Center));
        lvBox.Children.Add(Ui.Text($"РЕЙТИНГ {(int)p.Rating}", 13, Pal.Ink2, true, TextAlignment.Center));
        double span = Math.Max(1, lv.RatingTo - lv.RatingFrom + 1);
        lvBox.Children.Add(new Bar(Pal.Violet) { Value = (p.Rating - lv.RatingFrom) / span });
        lvBox.Children.Add(Ui.Text(lv.About, 12, Pal.Ink, false, TextAlignment.Center));
        stack.Children.Add(Ui.Box(lvBox, Pal.Paper, 14));

        var stats = new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 8,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) }
        };
        void AddStat(string text, string[] icon, int row, int col)
        {
            var lcd = Ui.Lcd(text, 14, icon);
            Grid.SetRow(lcd, row);
            Grid.SetColumn(lcd, col);
            stats.Children.Add(lcd);
        }
        AddStat($"{p.TotalXp} XP", Icons.Bolt, 0, 0);
        AddStat($"{AppState.DisplayStreak} ДН.", Icons.Flame, 0, 1);
        int acc = p.Answered == 0 ? 0 : (int)Math.Round(p.Correct * 100.0 / p.Answered);
        AddStat($"{acc}%", Icons.Target, 1, 0);
        AddStat($"{p.History.Count} ОПР.", Icons.Tape, 1, 1);
        stack.Children.Add(stats);

        stack.Children.Add(Ui.Text($"ЦЕЛЬ ДНЯ: {p.DailyGoalMin} МИН (≈{AppState.DailyGoalXp} XP)", 12, Pal.Ink2, true));

        if (user != null)
        {
            var topBtn = new Btn("ТОП ИГРОКОВ ▶", Pal.Violet, Pal.Paper, 15);
            topBtn.Clicked += async (s, e) => await Push(new TopPage());
            stack.Children.Add(topBtn);
        }

        var settings = new Btn("НАСТРОЙКИ ▶", Pal.Paper2, null, 15);
        settings.Clicked += async (s, e) => await Push(new SettingsPage());
        stack.Children.Add(settings);

        var hist = new VerticalStackLayout { Spacing = 10 };
        hist.Children.Add(Ui.Text("ИСТОРИЯ ОПРОСНИКОВ", 12, Pal.RedInk, true));

        elephant?.Stop();
        elephant = new Elephant(120);
        elephant.Start();
        hist.Children.Add(elephant);
        var temperament = AppState.LastTemperament();
        if (temperament is var (temperTitle, temperAbout))
        {
            hist.Children.Add(Ui.Text("ТВОЙ ТЕМПЕРАМЕНТ", 11, Pal.Ink2, true, TextAlignment.Center));
            hist.Children.Add(Ui.Text(temperTitle, 20, Pal.Ink, true, TextAlignment.Center));
            if (temperAbout.Length > 0)
                hist.Children.Add(Ui.Text(temperAbout, 13, Pal.Ink, false, TextAlignment.Center));
        }
        else
            hist.Children.Add(Ui.Text("Темперамент пока неизвестен. Пройди опросник «Темперамент» или EPI.", 12, Pal.Ink2, false, TextAlignment.Center));

        if (p.History.Count == 0)
            hist.Children.Add(Ui.Text("Пока пусто. Пройди любой опросник на вкладке «Опросники».", 12, Pal.Ink2));
        foreach (var h in p.History.Take(15))
        {
            var item = new VerticalStackLayout { Spacing = 2 };
            item.Children.Add(Ui.Text($"{h.Title.ToUpperInvariant()}  //  {h.Date}", 10, Pal.Ink2, true));
            item.Children.Add(Ui.Text(h.Headline, 14, Pal.Ink, true));
            foreach (var line in h.Lines)
                item.Children.Add(Ui.Text("▸ " + line, 11));
            hist.Children.Add(Ui.Card(item, Pal.Paper2, 10));
        }
        stack.Children.Add(Ui.Card(hist, Pal.Paper, 12));

        var ach = new VerticalStackLayout { Spacing = 4 };
        ach.Children.Add(Ui.Text("ДОСТИЖЕНИЯ", 12, Pal.RedInk, true));
        foreach (var def in AppState.AchDefs)
        {
            bool got = p.Achievements.Contains(def.Id);
            var row = new Grid
            {
                ColumnSpacing = 8,
                ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }
            };
            // полученное — заполненная звезда и жирный текст, не полученное — контур; не только цвет
            row.Children.Add(new PixelIcon(got ? Icons.Star : Icons.StarEmpty, got ? Pal.Orange : Pal.Ink2, 14));
            var text = Ui.Text(def.Title + " — " + def.Desc, 12, got ? Pal.Ink : Pal.Ink2, got);
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            ach.Children.Add(row);
        }
        stack.Children.Add(Ui.Card(ach, Pal.Paper, 12));

        stack.Children.Add(Ui.Text("Психея — учебный тренажёр. Это не клиническая диагностика. Результаты опросников носят учебный характер.", 11, Pal.Ink2));

        stack.Children.Add(Form.Link("ВЫЙТИ ИЗ АККАУНТА", Pal.Ink2, async () =>
        {
            // без сети запрос висел бы до 10 с — ждём отправку не дольше 3 с
            await Task.WhenAny(AppState.PushNow(), Task.Delay(3000));
            int pending = Sync.Pending;
            string msg = pending == 0
                ? "Прогресс сохранён в аккаунте. Выйти?"
                : $"Нет связи с сервером: {pending} {(pending == 1 ? "запись не отправлена" : "записи не отправлены")} и пропадут после выхода. Всё равно выйти?";
            bool yes = await Dlg.Confirm(this, "ВЫХОД", msg, "ВЫЙТИ", "ОТМЕНА");
            if (!yes) return;
            Api.Logout();
            AppState.ResetAll();
            await SwitchRoot(new NavigationPage(new LoginPage()));
        }, TextAlignment.Center));

        stack.Children.Add(Ui.VersionLabel());

        var scroll = new ScrollView { Content = stack };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        SetBody(root);
        return scroll;
    }
}
