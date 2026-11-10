using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

public class OnboardingPage : BasePage
{
    static readonly string[] RoleIds = { "student", "psychologist", "curious" };
    static readonly string[] RoleTitles = { "СТУДЕНТ", "ПСИХОЛОГ", "ПРОСТО ИНТЕРЕСНО" };

    // 0..2 — точки внизу трёх вступительных экранов (экран цели своих точек не показывает)
    int dot;
    bool busy;
    // текущий экран; при повороте собирается заново под новую ориентацию
    Func<View> screen;

    public OnboardingPage() : base(true)
    {
        screen = IntroView;
        SetBody(screen());
    }

    async Task Show(Func<View> next)
    {
        screen = next;
        await SwapBody(next());
    }

    protected override void OnOrientationChanged() => SetBody(screen());

    View Header(string title)
    {
        var box = new VerticalStackLayout { Spacing = 0 };
        var t = new Heading(title, 22);
        t.Margin = new Thickness(18, 14, 18, 0);
        box.Children.Add(t);
        return box;
    }

    View Bubble(string text)
    {
        var box = new VerticalStackLayout { Spacing = 0, Margin = new Thickness(20, 0) };
        box.Children.Add(Ui.Box(Ui.Text(text, 15), Pal.Paper, 14));
        var arrow = Ui.Text("▼", 18, Pal.Ink, true, TextAlignment.Center);
        box.Children.Add(arrow);
        return box;
    }

    View Dots() => Ui.Text(string.Join(" ", Enumerable.Range(0, 3).Select(i => i == dot ? "●" : "○")), 16, Pal.Ink, true, TextAlignment.Center);

    // общий каркас для текстовых слайдов с котом и кнопкой «ДАЛЕЕ»
    View TextSlide(string text, View mascot, Func<Task> onNext)
    {
        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };

        root.Children.Add(Header("ПСИХЕЯ // СТАРТ"));

        var mid = new VerticalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
        mid.Children.Add(Bubble(text));
        mid.Children.Add(mascot);
        // в горизонтальном режиме по высоте может не влезть
        View center = IsLandscape ? new ScrollView { Content = mid } : mid;
        Grid.SetRow(center, 1);
        root.Children.Add(center);

        var bottom = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(20, 8, 20, 24) };
        bottom.Children.Add(Dots());
        var next = new Btn("ДАЛЕЕ ▶", Pal.Violet, Pal.Paper);
        next.Clicked += async (s, e) =>
        {
            if (busy) return;
            busy = true;
            await onNext();
            busy = false;
        };
        bottom.Children.Add(next);
        Grid.SetRow(bottom, 2);
        root.Children.Add(bottom);

        return root;
    }

    View IntroView()
    {
        dot = 0;
        return TextSlide(
            "Привет! Я Манул, твой проводник по психологии. Сразу скажу: я не ставлю диагнозы. Это учебный тренажёр.",
            Ui.Mascot(IsLandscape ? 110 : 260, idle: true),
            async () => await Show(RoleView));
    }

    // новый экран вместо старого слайда «темперамент/характер»: кто пользователь, ответ уходит в профиль
    View RoleView()
    {
        dot = 1;
        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) }
        };
        root.Children.Add(Header("ПСИХЕЯ // СТАРТ"));

        var stack = new VerticalStackLayout { Spacing = 14, Padding = new Thickness(20, 16, 20, 24) };
        stack.Children.Add(Ui.Mascot(IsLandscape ? 80 : 150, idle: true));
        stack.Children.Add(Bubble("Расскажи, кто ты?"));

        for (int i = 0; i < RoleIds.Length; i++)
        {
            string id = RoleIds[i];
            var row = Ui.Box(Ui.Text(RoleTitles[i], 16, Pal.Ink, true, TextAlignment.Center), Pal.Paper2, 14);
            Ui.OnTap(row, async () =>
            {
                if (busy) return;
                busy = true;
                AppState.P.Role = id;
                AppState.Save();
                await Show(OutroView);
                busy = false;
            });
            stack.Children.Add(row);
        }
        stack.Children.Add(Dots());

        var scroll = new ScrollView { Content = stack };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        return root;
    }

    View OutroView()
    {
        dot = 2;
        return TextSlide(
            "Проходи опросники, отвечай на вопросы без лимита попыток и держи серию дней. Всё просто!",
            Ui.Mood("study", IsLandscape ? 110 : 260),
            async () => await Show(GoalView));
    }

    View GoalView()
    {
        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) }
        };
        root.Children.Add(Header("ЦЕЛЬ НА ДЕНЬ"));

        var stack = new VerticalStackLayout { Spacing = 14, Padding = new Thickness(20, 16, 20, 24) };
        stack.Children.Add(Ui.Mascot(IsLandscape ? 80 : 150, idle: true));
        stack.Children.Add(Bubble("Сколько минут в день будем заниматься?"));

        var goals = new (int min, string name)[]
        {
            (5, "ЛЁГКАЯ"),
            (10, "ОБЫЧНАЯ"),
            (15, "СЕРЬЁЗНАЯ"),
            (20, "ИНТЕНСИВ")
        };
        foreach (var g in goals)
        {
            var row = Ui.Box(Ui.Text($"{g.min} МИН  //  {g.name}", 16, Pal.Ink, true, TextAlignment.Center), Pal.Paper2, 14);
            var goal = g;
            Ui.OnTap(row, async () =>
            {
                if (busy) return;
                busy = true;
                AppState.P.DailyGoalMin = goal.min;
                AppState.Save();
                await Finish();
            });
            stack.Children.Add(row);
        }

        var scroll = new ScrollView { Content = stack };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        return root;
    }

    async Task Finish()
    {
        AppState.P.Onboarded = true;
        AppState.P.Rating = 100;
        AppState.Save();
        await SwitchRoot(new AppShell());
    }
}