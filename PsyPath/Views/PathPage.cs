using Microsoft.Maui.Controls.Shapes;
using PsyPath.Models;
using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

public enum LessonState
{
    Locked,
    Open,
    Current,
    Done,
    Mastered
}

// главный экран: путь из разделов, внутри раздела — его уроки
public class PathPage : DataPage
{
    // зигзаг: отступ кружка слева; справа строка держит остаток до MaxOffset, чтобы карточка не вылезла за экран
    static readonly double[] Offsets = { 14, 54, 0, 58, 10, 44 };
    const double MaxOffset = 58;
    const double SectionSize = 78;
    const double LessonSize = 58;

    // кнопка текущего раздела; пульсирует, только пока вкладка на экране
    VisualElement? pulsing;
    // раскрытые разделы переживают пересборку экрана после урока
    readonly HashSet<int> opened = new();

    protected override void Refresh()
    {
        base.Refresh();
        StartPulse();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        pulsing?.AbortAnimation("pulse");
    }

    void StartPulse()
    {
        if (pulsing == null || Anim.Reduced) return;
        var target = pulsing;
        target.AbortAnimation("pulse");
        var pulse = new Animation();
        pulse.Add(0, 0.5, new Animation(v => target.Scale = v, 1, 1.05, Easing.SinInOut));
        pulse.Add(0.5, 1, new Animation(v => target.Scale = v, 1.05, 1, Easing.SinInOut));
        pulse.Commit(target, "pulse", 16, 1400, null, null, () => true);
    }

    protected override ScrollView Build()
    {
        pulsing?.AbortAnimation("pulse");
        pulsing = null;

        int level = AppState.Level;
        var lv = AppState.LevelInfoOf(level);

        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) }
        };
        root.Children.Add(Hud(level, lv.Name));

        var path = new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 12, 18, 32) };

        var states = new Dictionary<string, LessonState>();
        Lesson? current = null;
        foreach (var sec in AppState.Lessons.Sections)
        {
            bool secUnlocked = level >= AppState.UnlockLevel(sec.Id);
            for (int i = 0; i < sec.Lessons.Count; i++)
            {
                var l = sec.Lessons[i];
                bool prevPassed = i == 0 || AppState.IsPassed(sec.Lessons[i - 1]);
                LessonState st;
                if (!secUnlocked) st = LessonState.Locked;
                else if (AppState.IsDone(l)) st = LessonState.Done;
                else if (AppState.IsMastered(l)) st = LessonState.Mastered;
                else if (prevPassed)
                {
                    if (current == null)
                    {
                        st = LessonState.Current;
                        current = l;
                    }
                    else st = LessonState.Open;
                }
                else st = LessonState.Locked;
                states[l.Id] = st;
            }
        }

        path.Children.Add(HeroCard(current));
        var review = ReviewCard();
        if (review != null) path.Children.Add(review);

        int place = 0;
        foreach (var sec in AppState.Lessons.Sections)
        {
            if (place > 0) path.Children.Add(new DashLink(SectionCenter(place - 1), SectionCenter(place)));
            AddSection(path, sec, place, states, level);
            place++;
        }

        var known = AppState.Lessons.Sections.Select(s => s.Id).ToHashSet();
        foreach (var u in AppState.Rating.SectionUnlock.Where(u => !known.Contains(u.Section)))
        {
            if (place > 0) path.Children.Add(new DashLink(SectionCenter(place - 1), SectionCenter(place)));
            int unlock = u.UnlockLevel;
            var chevron = Chevron(true);
            path.Children.Add(Node(place, SectionSize, $"Раздел {u.Section}", u.Title, LessonState.Locked, u.Section.ToString(), chevron, async () =>
                await Dlg.Alert(this, "СКОРО", $"Раздел появится позже и откроется на {unlock} уровне.")));
            place++;
        }

        var scroll = new ScrollView { Content = path };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        SetBody(root);
        return scroll;
    }

    static double OffsetOf(int place) => Offsets[place % Offsets.Length];

    static double SectionCenter(int place) => OffsetOf(place) + SectionSize / 2;

    static double LessonCenter(int place) => OffsetOf(place) + LessonSize / 2;

    void AddSection(VerticalStackLayout path, Section sec, int place, Dictionary<string, LessonState> states, int level)
    {
        var lessonStates = sec.Lessons.Select(l => states[l.Id]).ToList();
        LessonState state;
        if (level < AppState.UnlockLevel(sec.Id)) state = LessonState.Locked;
        else if (lessonStates.Contains(LessonState.Current)) state = LessonState.Current;
        else if (lessonStates.Count > 0 && lessonStates.All(s => s is LessonState.Done or LessonState.Mastered)) state = LessonState.Done;
        else state = LessonState.Open;

        var lessons = new VerticalStackLayout { Spacing = 14, IsVisible = opened.Contains(sec.Id) };
        for (int i = 0; i < sec.Lessons.Count; i++)
        {
            if (i > 0) lessons.Children.Add(new DashLink(LessonCenter(i - 1), LessonCenter(i)));
            lessons.Children.Add(MakeLesson(sec.Lessons[i], i, states[sec.Lessons[i].Id]));
        }

        var chevron = Chevron(state == LessonState.Locked);
        chevron.Rotation = lessons.IsVisible ? 90 : 0;

        int id = sec.Id;
        int unlock = AppState.UnlockLevel(id);
        var node = Node(place, SectionSize, $"Раздел {sec.Id}", sec.Title, state,
            state == LessonState.Done ? "✓" : sec.Id.ToString(), chevron, async () =>
            {
                if (state == LessonState.Locked)
                {
                    await Dlg.Alert(this, "ЗАКРЫТО", $"Раздел откроется на {unlock} уровне.");
                    return;
                }
                bool show = !lessons.IsVisible;
                lessons.IsVisible = show;
                if (show) opened.Add(id);
                else opened.Remove(id);
                await chevron.Spin(show ? 90 : 0, 160);
            }, burst: state == LessonState.Current, pulse: state == LessonState.Current);

        path.Children.Add(node);
        path.Children.Add(lessons);
    }

    View MakeLesson(Lesson lesson, int idx, LessonState state)
    {
        string glyph = state is LessonState.Done or LessonState.Mastered ? "✓" : (idx + 1).ToString();
        return Node(idx, LessonSize, $"Урок {idx + 1}", lesson.Title, state, glyph, Chevron(state == LessonState.Locked), async () =>
        {
            if (state == LessonState.Locked)
            {
                await Dlg.Alert(this, "ЗАКРЫТО", "Сначала пройди предыдущие уроки или повысь уровень.");
                return;
            }
            await Push(new LessonPage(lesson));
        });
    }

    static Label Chevron(bool locked)
    {
        var chevron = Ui.Text("›", 26, locked ? Pal.Ink2 : Pal.Violet, true);
        chevron.VerticalTextAlignment = TextAlignment.Center;
        return chevron;
    }

    // кружок на пути и карточка, заходящая под него
    View Node(int place, double size, string caption, string title, LessonState state, string glyph, Label chevron, Action tap,
        bool burst = false, bool pulse = false)
    {
        bool isLocked = state == LessonState.Locked;
        Color fill = state switch
        {
            LessonState.Current => Pal.Violet,
            LessonState.Done => Pal.Amber,
            LessonState.Mastered => Pal.TealInk,
            LessonState.Open => Pal.Paper2,
            _ => Pal.Gray
        };
        Color text = state is LessonState.Current or LessonState.Mastered ? Pal.Paper : Pal.Ink;

        View mark;
        if (isLocked)
        {
            mark = new PixelIcon(Icons.Lock, Pal.Paper, size * 0.36, Pal.Gray) { HorizontalOptions = LayoutOptions.Center };
        }
        else
        {
            var label = Ui.Text(glyph, size * 0.36, text, true, TextAlignment.Center);
            // галочки нет в основном шрифте
            if (glyph == "✓") label.FontFamily = "MonoBold";
            label.VerticalTextAlignment = TextAlignment.Center;
            label.HorizontalOptions = LayoutOptions.Center;
            label.VerticalOptions = LayoutOptions.Center;
            mark = label;
        }

        var circle = new Border
        {
            Content = mark,
            BackgroundColor = fill,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(size / 2) },
            WidthRequest = size,
            HeightRequest = size,
            VerticalOptions = LayoutOptions.Center,
            Shadow = Ui.SoftShadow(Pal.Violet)
        };
        if (pulse) pulsing = circle;

        var info = new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center };
        info.Children.Add(Ui.Text(caption, 12, Pal.Ink2));
        info.Children.Add(Ui.Text(title, size > LessonSize ? 16 : 14, isLocked ? Pal.Ink2 : Pal.Ink, true));

        var inner = new Grid
        {
            ColumnSpacing = 6,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        inner.Children.Add(info);
        Grid.SetColumn(chevron, 1);
        inner.Children.Add(chevron);

        var card = Ui.Card(inner, Pal.Paper, 12, 22);
        card.Padding = new Thickness(28, 12, 14, 12);
        card.Margin = new Thickness(-18, 6, 0, 6);
        card.VerticalOptions = LayoutOptions.Center;
        card.Shadow = Ui.SoftShadow();
        Grid.SetColumn(card, 1);

        double left = OffsetOf(place);
        var node = new Grid
        {
            Margin = new Thickness(left, 0, MaxOffset - left, 0),
            ColumnDefinitions = { new ColumnDefinition(new GridLength(size)), new ColumnDefinition(GridLength.Star) }
        };
        node.Children.Add(card);
        node.Children.Add(circle);
        if (burst) node.Children.Add(new Burst());
        Ui.OnTap(node, tap);
        return node;
    }

    View Hud(int level, string levelName)
    {
        int goal = AppState.DailyGoalXp;
        int today = AppState.TodayXp;

        var g = new Grid
        {
            ColumnSpacing = 8,
            Padding = new Thickness(18, 12, 18, 0),
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(52)),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(48))
            }
        };

        var avatar = new Border
        {
            Content = Ui.Mascot(62),
            BackgroundColor = Pal.Paper,
            Stroke = new SolidColorBrush(Pal.Line),
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(26) },
            WidthRequest = 52,
            HeightRequest = 52
        };
        g.Children.Add(avatar);

        var streak = Ui.Lcd(AppState.DisplayStreak.ToString(), 16, Icons.Flame, Pal.Orange, Pal.Yellow);
        streak.Padding = new Thickness(16, 10);
        streak.VerticalOptions = LayoutOptions.Center;
        Grid.SetColumn(streak, 1);
        g.Children.Add(streak);

        var xp = Ui.Lcd(AppState.P.TotalXp.ToString(), 16, Icons.Brain, Pal.Violet, Pal.Amber);
        xp.Padding = new Thickness(16, 10);
        xp.VerticalOptions = LayoutOptions.Center;
        Grid.SetColumn(xp, 2);
        g.Children.Add(xp);

        var gear = IconButton(Icons.Gear, "Настройки", async () => await Push(new SettingsPage()));
        Grid.SetColumn(gear, 4);
        g.Children.Add(gear);

        var box = new VerticalStackLayout { Spacing = 4 };
        box.Children.Add(g);

        var goalRow = new VerticalStackLayout { Spacing = 3, Padding = new Thickness(18, 6, 18, 6) };
        var captions = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        captions.Children.Add(Ui.Text($"Уровень {level} · {levelName}", 12, Pal.Ink2, true));
        var goalText = Ui.Text($"Цель дня {Math.Min(today, goal)}/{goal} XP", 12, Pal.Ink2, true);
        Grid.SetColumn(goalText, 1);
        captions.Children.Add(goalText);
        goalRow.Children.Add(captions);
        goalRow.Children.Add(new Bar(Pal.Orange) { Value = goal == 0 ? 0 : (double)today / goal });
        box.Children.Add(goalRow);
        return box;
    }

    // круглая кнопка-иконка: зона нажатия 48×48 dp, name — для экранного диктора
    static View IconButton(string[] icon, string name, Action action)
    {
        var button = new Border
        {
            Content = new PixelIcon(icon, Pal.Violet, 24, Pal.Paper) { HorizontalOptions = LayoutOptions.Center },
            BackgroundColor = Pal.Paper,
            Stroke = new SolidColorBrush(Pal.Line),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24) },
            WidthRequest = 48,
            HeightRequest = 48
        };
        SemanticProperties.SetDescription(button, name);
        Ui.OnTap(button, action);
        return button;
    }

    // манул занимает правый нижний угол, текст и кнопка держатся левее него
    View HeroCard(Lesson? current)
    {
        const double CatHeight = 136;

        var text = new VerticalStackLayout { Spacing = 8, Margin = new Thickness(0, 0, 118, 0) };
        text.Children.Add(Ui.Text("Изучай психологию вместе с манулом!", 21, Pal.Ink, true));
        text.Children.Add(Ui.Text(current != null ? "Маленькие шаги — большие перемены ♥" : "Ты прошёл всё, что сейчас доступно. Новые разделы скоро появятся!", 13, Pal.Ink2));
        if (current != null)
        {
            var lesson = current;
            var go = new Btn("▶  Начать урок", Pal.Violet, Pal.Paper, 15);
            go.Clicked += async (s, e) => await Push(new LessonPage(lesson));
            text.Children.Add(go);
        }

        var g = new Grid();
        var cat = Ui.Mood("love", CatHeight);
        cat.WidthRequest = CatHeight * 900 / 811;
        cat.HorizontalOptions = LayoutOptions.End;
        cat.VerticalOptions = LayoutOptions.End;
        cat.Margin = new Thickness(0, 0, -22, -14);
        g.Children.Add(cat);
        g.Children.Add(text);

        var card = Ui.Card(g, Pal.Paper, 16, 30);
        card.Shadow = Ui.SoftShadow();
        return card;
    }

    // «работа над ошибками» — только когда есть что повторять
    View? ReviewCard()
    {
        int n = Practice.MistakeCount;
        if (n == 0) return null;

        var g = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        var icon = new PixelIcon(Icons.Target, Pal.Ink, 26, Pal.Paper) { VerticalOptions = LayoutOptions.Center };
        g.Children.Add(icon);
        var text = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        text.Children.Add(Ui.Text("Работа над ошибками", 15, Pal.Ink, true));
        text.Children.Add(Ui.Text($"{n} {Plural(n, "задание", "задания", "заданий")} с ошибкой ждут повтора", 12, Pal.Ink));
        Grid.SetColumn(text, 1);
        g.Children.Add(text);
        var go = Ui.Text("›", 26, Pal.Violet, true);
        go.VerticalTextAlignment = TextAlignment.Center;
        Grid.SetColumn(go, 2);
        g.Children.Add(go);

        var card = Ui.Box(g, Pal.Amber, 12);
        Ui.OnTap(card, async () =>
        {
            var tasks = Practice.PickReview();
            if (tasks.Count > 0) await Push(new LessonPage(tasks));
        });
        return card;
    }

    static string Plural(int n, string one, string few, string many)
    {
        int d = n % 100;
        if (d is >= 11 and <= 14) return many;
        return (n % 10) switch
        {
            1 => one,
            >= 2 and <= 4 => few,
            _ => many
        };
    }
}
