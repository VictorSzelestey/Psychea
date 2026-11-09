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
    static readonly double[] Offsets = { 0, 50, 80, 50, 0, -50, -80, -50 };

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

        var titleRow = new Grid
        {
            Padding = new Thickness(18, 10, 18, 6),
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        titleRow.Children.Add(new Glitch("ВОПРОСЫ", 24));
        var lvLabel = Ui.Text(lv.Name.ToUpperInvariant(), 12, Pal.Ink2, true, TextAlignment.End);
        lvLabel.VerticalTextAlignment = TextAlignment.Center;
        Grid.SetColumn(lvLabel, 1);
        titleRow.Children.Add(lvLabel);
        top.Children.Add(titleRow);

        top.Children.Add(Hud(level));
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

        path.Children.Add(MascotCard(current));
        var review = ReviewCard();
        if (review != null) path.Children.Add(review);

        var bannerColors = new[] { Pal.Amber, Pal.Orange, Pal.Teal };
        int bi = 0;
        foreach (var sec in AppState.Lessons.Sections)
        {
            var banner = new VerticalStackLayout { Spacing = 2 };
            banner.Children.Add(Ui.Text($"ЮНИТ {sec.Id:00}", 11, Pal.Ink, true));
            banner.Children.Add(Ui.Text(sec.Title.ToUpperInvariant(), 17, Pal.Ink, true));
            path.Children.Add(Ui.Box(banner, bannerColors[bi % bannerColors.Length], 12));
            bi++;

            for (int i = 0; i < sec.Lessons.Count; i++)
            {
                var l = sec.Lessons[i];
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
            card.Opacity = 0.6;
            path.Children.Add(card);
        }

        var scroll = new ScrollView { Content = path };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        SetBody(root);
        return scroll;
    }

    View Hud(int level)
    {
        int goal = AppState.DailyGoalXp;
        int today = AppState.TodayXp;

        var g = new Grid
        {
            ColumnSpacing = 6,
            Padding = new Thickness(18, 4, 18, 0),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };

        var cells = new (string Text, string[] Icon)[]
        {
            (AppState.DisplayStreak.ToString(), Icons.Flame),
            (AppState.P.TotalXp.ToString(), Icons.Bolt),
            ($"LV {level}", Icons.Rank)
        };
        for (int i = 0; i < cells.Length; i++)
        {
            var cell = Ui.Lcd(cells[i].Text, 14, cells[i].Icon);
            Grid.SetColumn(cell, i);
            g.Children.Add(cell);
        }

        var box = new VerticalStackLayout { Spacing = 4 };
        box.Children.Add(g);

        var goalRow = new VerticalStackLayout { Spacing = 3, Padding = new Thickness(18, 4, 18, 8) };
        goalRow.Children.Add(Ui.Text($"ЦЕЛЬ ДНЯ  {Math.Min(today, goal)}/{goal} XP", 10, Pal.Ink2, true));
        goalRow.Children.Add(new Bar(Pal.Orange) { Value = goal == 0 ? 0 : (double)today / goal });
        box.Children.Add(goalRow);
        return box;
    }

    View MascotCard(Lesson? current)
    {
        string msg;
        int streak = AppState.DisplayStreak;
        if (current != null)
            msg = streak >= 3
                ? $"Серия {streak} дн.! Следующий урок: «{current.Title}». Продолжим?"
                : $"Следующий урок: «{current.Title}». Продолжим?";
        else
            msg = "Ты прошёл всё, что сейчас доступно. Новые разделы скоро появятся!";

        var g = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions = { new ColumnDefinition(new GridLength(84)), new ColumnDefinition(GridLength.Star) }
        };
        var cat = Ui.Mood("love", 100);
        g.Children.Add(cat);
        var text = Ui.Text(msg, 13);
        text.VerticalTextAlignment = TextAlignment.Center;
        Grid.SetColumn(text, 1);
        g.Children.Add(text);
        return Ui.Box(g, Pal.Paper, 10);
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
        Color fill = st switch
        {
            LessonState.Current => Pal.Red,
            LessonState.Done => Pal.Amber,
            LessonState.Mastered => Pal.TealInk,
            LessonState.Open => Pal.Paper2,
            _ => Pal.Gray
        };
        string glyph = st switch
        {
            LessonState.Current => "▶",
            LessonState.Done => "✓",
            LessonState.Mastered => "✓",
            LessonState.Open => (idx + 1).ToString(),
            _ => ""
        };
        Color txt = st == LessonState.Current || st == LessonState.Mastered ? Pal.Paper : Pal.Ink;

        View mark;
        if (st == LessonState.Locked)
        {
            mark = new PixelIcon(Icons.Lock, Pal.Ink, 28, Pal.Gray) { HorizontalOptions = LayoutOptions.Center };
        }
        else
        {
            var label = Ui.Text(glyph, 26, txt, true, TextAlignment.Center);
            label.VerticalTextAlignment = TextAlignment.Center;
            label.HorizontalOptions = LayoutOptions.Center;
            label.VerticalOptions = LayoutOptions.Center;
            mark = label;
        }

        var top = new Border
        {
            Content = mark,
            BackgroundColor = fill,
            Stroke = new SolidColorBrush(Pal.Ink),
            StrokeThickness = 3,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(36) },
            WidthRequest = 72,
            HeightRequest = 72,
            Margin = new Thickness(0, 0, 5, 5),
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start
        };
        var shadow = new Border
        {
            BackgroundColor = Pal.Ink,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(36) },
            WidthRequest = 72,
            HeightRequest = 72,
            Margin = new Thickness(5, 5, 0, 0),
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start
        };
        var circle = new Grid { WidthRequest = 80, HeightRequest = 80, HorizontalOptions = LayoutOptions.Center };
        circle.Children.Add(shadow);
        circle.Children.Add(top);

        // раньше бесконечная анимация запускалась при каждой пересборке и не останавливалась
        if (st == LessonState.Current) pulsing = top;

        var title = Ui.Text(les.Title, 11, st == LessonState.Locked ? Pal.Ink2 : Pal.Ink, false, TextAlignment.Center);
        title.WidthRequest = 160;

        var node = new VerticalStackLayout
        {
            Spacing = 4,
            HorizontalOptions = LayoutOptions.Center,
            TranslationX = Offsets[idx % Offsets.Length]
        };
        node.Children.Add(circle);
        node.Children.Add(title);

        var section = sec;
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
