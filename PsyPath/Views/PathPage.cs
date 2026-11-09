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

public class PathPage : DataPage
{
    // зигзаг: отступ кружка слева; справа строка держит остаток до MaxOffset, чтобы карточка не вылезла за экран
    static readonly double[] Offsets = { 0, 40, 12, 46, 6, 34 };
    const double MaxOffset = 46;
    const double NodeSize = 72;

    // кнопка текущего урока; пульсирует, только пока вкладка на экране
    VisualElement? pulsing;

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
        pulse.Add(0, 0.5, new Animation(v => target.Scale = v, 1, 1.08, Easing.SinInOut));
        pulse.Add(0.5, 1, new Animation(v => target.Scale = v, 1.08, 1, Easing.SinInOut));
        pulse.Commit(target, "pulse", 16, 1200, null, null, () => true);
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

        var top = new VerticalStackLayout { Spacing = 0 };
        top.Children.Add(Ui.Stripes(4));
        top.Children.Add(Hud(level, lv.Name));
        root.Children.Add(top);

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

        foreach (var sec in AppState.Lessons.Sections)
        {
            var banner = new VerticalStackLayout { Spacing = 2 };
            banner.Children.Add(Ui.Text($"Раздел {sec.Id}", 12, Pal.Ink, false));
            banner.Children.Add(Ui.Text(sec.Title, 17, Pal.Ink, true));
            path.Children.Add(Ui.Card(banner, Pal.Amber, 12));

            for (int i = 0; i < sec.Lessons.Count; i++)
            {
                var l = sec.Lessons[i];
                if (i > 0) path.Children.Add(new DashLink(CenterOf(i - 1), CenterOf(i)));
                path.Children.Add(MakeNode(sec, l, i, states[l.Id]));
            }
        }

        var known = AppState.Lessons.Sections.Select(s => s.Id).ToHashSet();
        foreach (var u in AppState.Rating.SectionUnlock.Where(u => !known.Contains(u.Section)))
        {
            var locked = new VerticalStackLayout { Spacing = 2 };
            locked.Children.Add(Ui.Text($"▒ РАЗДЕЛ {u.Section:00}  //  LV {u.UnlockLevel}", 10, Pal.Ink2, true));
            locked.Children.Add(Ui.Text(u.Title.ToUpperInvariant(), 13, Pal.Ink2, true));
            locked.Children.Add(Ui.Text("СКОРО", 10, Pal.Ink2, true));
            var card = Ui.Card(locked, Pal.Paper2, 10);
            card.Opacity = 0.75;
            path.Children.Add(card);
        }

        var scroll = new ScrollView { Content = path };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        SetBody(root);
        return scroll;
    }

    static double OffsetOf(int idx) => Offsets[idx % Offsets.Length];

    static double CenterOf(int idx) => OffsetOf(idx) + NodeSize / 2;

    View Hud(int level, string levelName)
    {
        int goal = AppState.DailyGoalXp;
        int today = AppState.TodayXp;

        var g = new Grid
        {
            ColumnSpacing = 6,
            Padding = new Thickness(18, 10, 18, 0),
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(48)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(48)),
                new ColumnDefinition(new GridLength(48))
            }
        };

        var avatar = new Border
        {
            Content = Ui.Mascot(58),
            BackgroundColor = Pal.Paper,
            Stroke = new SolidColorBrush(Pal.Line),
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24) },
            WidthRequest = 48,
            HeightRequest = 48
        };
        g.Children.Add(avatar);

        var streak = Ui.Lcd(AppState.DisplayStreak.ToString(), 14, Icons.Flame, Pal.Orange, Pal.Yellow);
        Grid.SetColumn(streak, 1);
        g.Children.Add(streak);

        var xp = Ui.Lcd(AppState.P.TotalXp.ToString(), 14, Icons.Brain, Pal.Violet, Pal.Amber);
        Grid.SetColumn(xp, 2);
        g.Children.Add(xp);

        var top = IconButton(Icons.Cup, "Топ игроков", async () => await Push(new TopPage()));
        Grid.SetColumn(top, 3);
        g.Children.Add(top);

        var gear = IconButton(Icons.Gear, "Настройки", async () => await Push(new SettingsPage()));
        Grid.SetColumn(gear, 4);
        g.Children.Add(gear);

        var box = new VerticalStackLayout { Spacing = 4 };
        box.Children.Add(g);

        var goalRow = new VerticalStackLayout { Spacing = 3, Padding = new Thickness(18, 6, 18, 8) };
        var captions = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        captions.Children.Add(Ui.Text($"LV {level}  {levelName.ToUpperInvariant()}", 11, Pal.Ink2, true));
        var goalText = Ui.Text($"ЦЕЛЬ ДНЯ  {Math.Min(today, goal)}/{goal} XP", 11, Pal.Ink2, true);
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
            Content = new PixelIcon(icon, Pal.Violet, 22, Pal.Paper) { HorizontalOptions = LayoutOptions.Center },
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

    View HeroCard(Lesson? current)
    {
        string msg;
        int streak = AppState.DisplayStreak;
        if (current != null)
            msg = streak >= 3
                ? $"Серия {streak} дн.! Следующий урок: «{current.Title}»."
                : $"Следующий урок: «{current.Title}».";
        else
            msg = "Ты прошёл всё, что сейчас доступно. Новые разделы скоро появятся!";

        var text = new VerticalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
        text.Children.Add(new Glitch("Изучай психологию вместе с манулом!", 19, mono: false));
        text.Children.Add(Ui.Text(msg, 12, Pal.Ink2));
        if (current != null)
        {
            var lesson = current;
            var go = new Btn("НАЧАТЬ УРОК ▶", Pal.Violet, Pal.Paper, 14);
            go.Clicked += async (s, e) => await Push(new LessonPage(lesson));
            text.Children.Add(go);
        }

        var g = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        g.Children.Add(text);
        var cat = Ui.Mascot(112);
        cat.VerticalOptions = LayoutOptions.End;
        Grid.SetColumn(cat, 1);
        g.Children.Add(cat);
        return Ui.Box(g, Pal.Paper, 14);
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
        text.Children.Add(Ui.Text("РАБОТА НАД ОШИБКАМИ", 14, Pal.Ink, true));
        text.Children.Add(Ui.Text($"{n} {Plural(n, "задание", "задания", "заданий")} с ошибкой ждут повтора", 11, Pal.Ink));
        Grid.SetColumn(text, 1);
        g.Children.Add(text);
        var go = Ui.Text("▶", 22, Pal.Ink, true);
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

    View MakeNode(Section sec, Lesson les, int idx, LessonState st)
    {
        bool isLocked = st == LessonState.Locked;
        Color fill = st switch
        {
            LessonState.Current => Pal.Violet,
            LessonState.Done => Pal.Amber,
            LessonState.Mastered => Pal.TealInk,
            LessonState.Open => Pal.Paper2,
            _ => Pal.Gray
        };
        string glyph = st is LessonState.Done or LessonState.Mastered ? "✓" : (idx + 1).ToString();
        Color txt = st is LessonState.Current or LessonState.Mastered ? Pal.Paper : Pal.Ink;

        View mark;
        if (isLocked)
        {
            mark = new PixelIcon(Icons.Lock, Pal.Paper, 26, Pal.Gray) { HorizontalOptions = LayoutOptions.Center };
        }
        else
        {
            var label = Ui.Text(glyph, 26, txt, true, TextAlignment.Center);
            // галочки нет в основном шрифте
            label.FontFamily = "MonoBold";
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
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(NodeSize / 2) },
            WidthRequest = NodeSize,
            HeightRequest = NodeSize,
            VerticalOptions = LayoutOptions.Center,
            Shadow = Ui.SoftShadow(Pal.Violet)
        };

        // раньше бесконечная анимация запускалась при каждой пересборке и не останавливалась
        if (st == LessonState.Current) pulsing = circle;

        var info = new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center };
        info.Children.Add(Ui.Text($"Урок {idx + 1}", 11, Pal.Ink2));
        info.Children.Add(Ui.Text(les.Title, 14, isLocked ? Pal.Ink2 : Pal.Ink, true));

        var inner = new Grid
        {
            ColumnSpacing = 6,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        inner.Children.Add(info);
        var chevron = Ui.Text("›", 26, isLocked ? Pal.Ink2 : Pal.Violet, true);
        chevron.VerticalTextAlignment = TextAlignment.Center;
        Grid.SetColumn(chevron, 1);
        inner.Children.Add(chevron);

        // карточка заходит под кружок, как на макете
        var card = Ui.Card(inner, Pal.Paper, 12);
        card.Padding = new Thickness(26, 10, 12, 10);
        card.Margin = new Thickness(-16, 6, 0, 6);
        card.VerticalOptions = LayoutOptions.Center;
        Grid.SetColumn(card, 1);

        double left = OffsetOf(idx);
        var node = new Grid
        {
            Margin = new Thickness(left, 0, MaxOffset - left, 0),
            ColumnDefinitions = { new ColumnDefinition(new GridLength(NodeSize)), new ColumnDefinition(GridLength.Star) }
        };
        node.Children.Add(card);
        node.Children.Add(circle);

        var lesson = les;
        var state = st;
        Ui.OnTap(node, async () =>
        {
            if (state == LessonState.Locked)
            {
                await Dlg.Alert(this, "ЗАКРЫТО", "Сначала пройди предыдущие уроки или повысь уровень.");
                return;
            }
            await Push(new LessonPage(lesson));
        });
        return node;
    }
}
