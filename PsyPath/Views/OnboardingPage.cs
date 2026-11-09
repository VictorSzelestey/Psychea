using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

public class OnboardingPage : BasePage
{
    static readonly string[] Slides =
    {
        "Привет! Я Манул, твой проводник по психологии. Сразу скажу: я не ставлю диагнозы. Это учебный тренажёр.",
        "Запомни: темперамент врождённый, а характер формируется в жизни. Это разные вещи.",
        "Проходи опросники, отвечай на вопросы без лимита попыток и держи серию дней. Всё просто!"
    };

    int slide;
    bool busy;
    // текущий экран; при повороте собирается заново под новую ориентацию
    Func<View> screen;

    public OnboardingPage() : base(true)
    {
        screen = SlideView;
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
        box.Children.Add(Ui.Stripes(4));
        var t = new Glitch(title, 22);
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

    View SlideView()
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
        mid.Children.Add(Bubble(Slides[slide]));
        mid.Children.Add(Ui.Mascot(IsLandscape ? 110 : 260, idle: true));
        // в горизонтальном режиме по высоте может не влезть
        View center = IsLandscape ? new ScrollView { Content = mid } : mid;
        Grid.SetRow(center, 1);
        root.Children.Add(center);

        var bottom = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(20, 8, 20, 24) };
        string dots = string.Join(" ", Enumerable.Range(0, Slides.Length).Select(i => i == slide ? "●" : "○"));
        bottom.Children.Add(Ui.Text(dots, 16, Pal.Ink, true, TextAlignment.Center));
        var next = new Btn("ДАЛЕЕ ▶", Pal.Violet, Pal.Paper);
        next.Clicked += async (s, e) =>
        {
            if (busy) return;
            busy = true;
            if (slide < Slides.Length - 1)
            {
                slide++;
                await Show(SlideView);
            }
            else
            {
                await Show(GoalView);
            }
            busy = false;
        };
        bottom.Children.Add(next);
        Grid.SetRow(bottom, 2);
        root.Children.Add(bottom);

        return root;
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
