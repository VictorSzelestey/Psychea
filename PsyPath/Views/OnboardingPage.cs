using PsyPath.Models;
using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

public class OnboardingPage : BasePage
{
    static readonly string[] RoleIds = { "student", "psychologist", "curious" };
    static readonly string[] RoleTitles = { "СТУДЕНТ", "ПСИХОЛОГ", "ПРОСТО ИНТЕРЕСНО" };

    // экраны с точками: привет, роль, три экрана обучения, итог (экран цели своих точек не показывает)
    const int Steps = 6;
    const int TourFirst = 2;
    const int TourSteps = 3;

    // 0..Steps-1 — какая точка внизу горит
    int dot;
    bool busy;
    // tourOnly — только обучение, открыто из настроек; в конце возврат назад
    readonly bool tourOnly;
    // текущий экран; при повороте собирается заново под новую ориентацию
    Func<View> screen;

    public OnboardingPage(bool tourOnly = false) : base(true)
    {
        this.tourOnly = tourOnly;
        screen = tourOnly ? LessonTourView : IntroView;
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

    View Dots()
    {
        int count = tourOnly ? TourSteps : Steps;
        int at = tourOnly ? dot - TourFirst : dot;
        return Ui.Text(string.Join(" ", Enumerable.Range(0, count).Select(i => i == at ? "●" : "○")), 16, Pal.Ink, true, TextAlignment.Center);
    }

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

        bool tour = dot >= TourFirst && dot < TourFirst + TourSteps;
        root.Children.Add(Header(tour ? "КАК ЭТО РАБОТАЕТ" : "ПСИХЕЯ // СТАРТ"));

        var mid = new VerticalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
        mid.Children.Add(Bubble(text));
        mid.Children.Add(mascot);
        // в горизонтальном режиме и на экранах обучения по высоте может не влезть
        View center = IsLandscape || tour ? new ScrollView { Content = mid } : mid;
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
                await Show(LessonTourView);
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

    // обучение: как устроен урок
    View LessonTourView()
    {
        dot = TourFirst;
        var demo = new VerticalStackLayout { Spacing = 10, Margin = new Thickness(20, 0) };
        demo.Children.Add(Ui.Text("ТЕОРИЯ  //  1 ИЗ 3", 11, Pal.TealInk, true));
        demo.Children.Add(LessonPage.TheoryCardView(new TheoryCard
        {
            Title = "Психика и сознание",
            Text = "Сознание — только часть психики. Многое идёт без осознания: например, водитель ведёт машину «на автомате»."
        }));
        demo.Children.Add(Ui.Text("Потом задания: выбери ответ, верно или неверно, соедини пары, расставь по порядку.", 13, Pal.Ink2));
        return TextSlide(
            "Ничего не знаешь о психологии? Не страшно! Каждый урок начинается с короткой теории простыми словами, а потом — задания по ней.",
            demo,
            async () => await Show(HelpTourView));
    }

    // обучение: помощники внутри урока
    View HelpTourView()
    {
        dot = TourFirst + 1;
        var demo = new VerticalStackLayout { Spacing = 10, Margin = new Thickness(20, 0) };
        demo.Children.Add(TourRow(new Image { Source = "tab_book.png", WidthRequest = 26, HeightRequest = 26 },
            "Книжка в шапке урока — конспект теории. Открывай, если забыл."));
        demo.Children.Add(TourRow(Ui.Text("?", 22, Pal.TealInk, true, TextAlignment.Center),
            "Подсказка к заданию. На урок их две — береги."));
        demo.Children.Add(TourRow(new PixelIcon(Icons.Target, Pal.Ink, 26, Pal.Paper),
            "Ошибся — задание вернётся в конце урока, а потом в «Работе над ошибками»."));
        demo.Children.Add(Ui.Mood("study", IsLandscape ? 70 : 120));
        return TextSlide(
            "Застрял на вопросе? Помощники всегда под рукой.",
            demo,
            async () => await Show(AppTourView));
    }

    // обучение: что ещё есть в приложении
    View AppTourView()
    {
        dot = TourFirst + 2;
        var demo = new VerticalStackLayout { Spacing = 10, Margin = new Thickness(20, 0) };
        demo.Children.Add(TourRow(new PixelIcon(Icons.Brain, Pal.Violet, 26, Pal.Amber),
            "XP — опыт за верные ответы. С ним растут уровень и рейтинг."));
        demo.Children.Add(TourRow(new PixelIcon(Icons.Flame, Pal.Orange, 26, Pal.Yellow),
            "Серия — сколько дней подряд ты занимаешься."));
        demo.Children.Add(TourRow(new Image { Source = "tab_quiz.png", WidthRequest = 26, HeightRequest = 26 },
            "Опросники — узнай свой темперамент и черты личности."));
        demo.Children.Add(TourRow(new Image { Source = "tab_book.png", WidthRequest = 26, HeightRequest = 26 },
            "Справочник — все термины, а карточки помогают их запомнить."));
        return TextSlide(
            "Внизу экрана — вкладки: Главная с уроками, Опросники, Справочник и Профиль.",
            demo,
            async () =>
            {
                if (tourOnly) await Pop();
                else await Show(OutroView);
            });
    }

    // строка обучения: значок слева, пояснение справа
    static View TourRow(View icon, string text)
    {
        var g = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions = { new ColumnDefinition(new GridLength(36)), new ColumnDefinition(GridLength.Star) }
        };
        icon.HorizontalOptions = LayoutOptions.Center;
        icon.VerticalOptions = LayoutOptions.Center;
        g.Children.Add(icon);
        var label = Ui.Text(text, 14);
        label.VerticalTextAlignment = TextAlignment.Center;
        Grid.SetColumn(label, 1);
        g.Children.Add(label);
        return Ui.Box(g, Pal.Paper, 12);
    }

    View OutroView()
    {
        dot = Steps - 1;
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