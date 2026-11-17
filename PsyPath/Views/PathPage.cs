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

// главный экран: карта из четырёх островов. На острове — его тема и музыка, путь из разделов и уроков.
// Переход между картой и островом закрывают облака: под ними тема меняется и вкладки собираются заново
public class PathPage : DataPage
{
    // кружок урока на дороге острова, квадрат под него с украшением и ширина знака раздела
    const double LessonSize = 56;
    const double StopBox = LessonSize * 1.7;
    const double SignWidth = 190;

    // облака закрыли экран перед сменой острова: новая главная собирается под ними и раскрывает их
    static bool cloudsClosed;

    // кнопка текущего раздела; пульсирует, только пока вкладка на экране
    VisualElement? pulsing;
    // кружок текущего урока на дороге острова: к нему прокручивается экран, когда приплыли на остров
    View? currentStop;
    ScrollView? scroll;
    Clouds? clouds;
    IslandMap? map;
    bool traveling;

    protected override bool AnimateEnter => !cloudsClosed;

    protected override void Refresh()
    {
        // острова открываются по пройденным урокам; задание дня, выполненное не на занятии, приносит кристаллы здесь
        bool changed = Islands.Update();
        changed |= Daily.Collect().Count > 0;
        if (changed) AppState.Save();
        base.Refresh();
        StartPulse();
        _ = AfterShow();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        pulsing?.AbortAnimation("pulse");
    }

    // «Назад» на острове возвращает на карту, а не закрывает приложение
    protected override bool OnBackButtonPressed()
    {
        if (Islands.Active == null || NoteOpen) return base.OnBackButtonPressed();
        _ = Travel(null);
        return true;
    }

    async Task AfterShow()
    {
        if (cloudsClosed && clouds != null)
        {
            // экран успевает нарисоваться под облаками; флаг снимается после паузы,
            // иначе въезд вкладки (PlayEnter сразу после Refresh) проявил бы её вместе с облаками с нуля
            await Task.Delay(140);
            // на острове экран под облаками доезжает до текущего урока
            if (currentStop != null && scroll != null) await scroll.ScrollToAsync(currentStop, ScrollToPosition.Center, false);
            cloudsClosed = false;
            await clouds.Open();
        }
        if (map != null) await map.Reveal();
        await ShowNotices();
    }

    // сообщения, которые главная показывает один раз (ProgressData.Notices)
    async Task ShowNotices()
    {
        var p = AppState.P;
        if (!p.Notices.Contains("refund_cosmo")) return;
        int n = Crystals.CosmoRefund;
        await Note("love", "КРИСТАЛЛЫ ВЕРНУЛИСЬ",
            "Тема «Космос» больше не продаётся: теперь она открывается вместе с Островом ночи. " +
            $"Мы вернули {n} {Crystals.Word(n)}, потраченных на неё.");
        p.Notices.Remove("refund_cosmo");
        AppState.Save();
    }

    // на остров или на карту (null): облака закрывают экран, тема меняется, вкладки собираются заново
    async Task Travel(Island? to)
    {
        if (traveling) return;
        traveling = true;
        Sound.Play("flip");
        if (clouds != null) await clouds.Close();
        Islands.Enter(to);
        cloudsClosed = true;
        await SwitchRoot(new AppShell(), fade: false);
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
        map = null;
        currentStop = null;

        int level = AppState.Level;
        var lv = AppState.LevelInfoOf(level);

        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) }
        };
        root.Children.Add(Hud(level, lv.Name));

        var path = new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 12, 18, 32) };

        // состояния уроков открытых островов по порядку островов; первый непройденный — текущий
        var states = new Dictionary<string, LessonState>();
        Lesson? current = null;
        foreach (var sec in Islands.All.Where(Islands.IsOpen).SelectMany(Islands.SectionsOf))
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

        var island = Islands.Active;
        if (island == null)
        {
            map = new IslandMap();
            map.Opened += i => _ = Travel(i);
            path.Children.Add(map);
            path.Children.Add(HeroCard(current));
            path.Children.Add(DailyCard());
            var review = ReviewCard();
            if (review != null) path.Children.Add(review);
        }
        else
        {
            path.Children.Add(IslandHeader(island));
            var fresh = Islands.NotShown();
            if (fresh.Count > 0) path.Children.Add(NewIslandCard(fresh));
            path.Children.Add(Road(island, states, level));
        }

        scroll = new ScrollView { Content = path };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        // облака поверх всего экрана, вместе с верхней панелью
        clouds = new Clouds(cloudsClosed);
        Grid.SetRowSpan(clouds, 2);
        root.Children.Add(clouds);

        SetBody(root);
        return scroll;
    }

    // шапка острова: картинка, название, сколько пройдено и дорога назад к карте
    View IslandHeader(Island island)
    {
        var lessons = Islands.LessonsOf(island);
        int passed = lessons.Count(AppState.IsPassed);

        var g = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }
        };
        var picture = new Image { Source = $"island_{island.Image}.png", WidthRequest = 104, HeightRequest = 104, Aspect = Aspect.AspectFit, VerticalOptions = LayoutOptions.Center };
        Anim.Idle(picture, 3);
        g.Children.Add(picture);

        var text = new VerticalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
        text.Children.Add(Ui.Text($"ОСТРОВ {island.Number} · ТЕМА «{Islands.ThemeOf(island).Name.ToUpperInvariant()}»", 11, Pal.Ink2, true));
        text.Children.Add(Ui.Text(island.Name, 20, Pal.Ink, true));
        text.Children.Add(new Bar(Pal.Violet) { Value = lessons.Count == 0 ? 0 : (double)passed / lessons.Count });
        text.Children.Add(Ui.Text($"Пройдено {passed} из {lessons.Count}", 12, Pal.Ink2));
        Grid.SetColumn(text, 1);
        g.Children.Add(text);

        var box = new VerticalStackLayout { Spacing = 10 };
        box.Children.Add(g);
        var back = new Btn("◀  К КАРТЕ ОСТРОВОВ", Pal.Paper2, null, 14);
        back.Clicked += async (s, e) => await Travel(null);
        box.Children.Add(back);

        return new Lifted(Ui.Card(box, Pal.Paper, 14, 26), 26);
    }

    // остров пройден, а следующий ещё не показан на карте — зовём туда
    View NewIslandCard(List<Island> fresh)
    {
        var box = new VerticalStackLayout { Spacing = 8 };
        box.Children.Add(Ui.Text("ОТКРЫТ НОВЫЙ ОСТРОВ!", 12, Pal.RedInk, true));
        foreach (var i in fresh)
            box.Children.Add(Ui.Text($"{i.Name} и тема «{Islands.ThemeOf(i).Name}» для всего приложения", 14, Pal.Ink, true));
        var go = new Btn("К КАРТЕ ▶", Pal.Violet, Pal.OnAccent, 14);
        go.Clicked += async (s, e) => await Travel(null);
        box.Children.Add(go);
        return Ui.Box(box, Pal.Amber, 14);
    }

    // дорога острова снизу вверх: знак раздела, за ним кружки его уроков.
    // Нажатие на кружок открывает подсказку с названием урока и кнопкой; у текущего урока она открыта сразу
    View Road(Island island, Dictionary<string, LessonState> states, int level)
    {
        var road = new IslandRoad(island.Image);
        (View Stop, Func<View> Card)? open = null;
        int place = 0;
        foreach (var sec in Islands.SectionsOf(island))
        {
            road.Add(SectionSign(sec, level), SignWidth);
            for (int i = 0; i < sec.Lessons.Count; i++)
            {
                var lesson = sec.Lessons[i];
                var state = states[lesson.Id];
                int index = i;
                var stop = LessonStop(state, index, island.Decor, place + i + 1);
                View Card() => LessonCard(sec, lesson, index, state);
                Ui.OnTap(stop, () =>
                {
                    Sound.Play("tap");
                    if (road.IsOpen(stop)) road.Hide();
                    else road.Show(stop, Card());
                });
                SemanticProperties.SetDescription(stop, $"Урок {index + 1}: {lesson.Title}" + (state == LessonState.Locked ? ", закрыт" : ""));
                AutomationProperties.SetIsInAccessibleTree(stop, true);
                road.Add(stop, StopBox);
                if (state == LessonState.Current) open = (stop, Card);
            }
            place += 2;
        }
        currentStop = open?.Stop;
        if (open is { } o) road.Show(o.Stop, o.Card());
        return road;
    }

    // знак раздела на дороге: номер и название, у закрытого — уровень, на котором откроется
    View SectionSign(Section sec, int level)
    {
        int number = AppState.SectionNumber(sec.Id);
        int unlock = AppState.UnlockLevel(sec.Id);
        bool locked = level < unlock;
        int passed = sec.Lessons.Count(AppState.IsPassed);
        string head = locked ? $"РАЗДЕЛ {number} · С {unlock} УРОВНЯ" :
            passed == sec.Lessons.Count && passed > 0 ? $"РАЗДЕЛ {number} · ПРОЙДЕН" : $"РАЗДЕЛ {number} · {passed}/{sec.Lessons.Count}";

        var box = new VerticalStackLayout { Spacing = 1 };
        box.Children.Add(Ui.Text(head, 11, Pal.Ink2, true, TextAlignment.Center));
        var title = Ui.Text(sec.Title, 13, locked ? Pal.Ink2 : Pal.Ink, true, TextAlignment.Center);
        title.MaxLines = 2;
        title.LineBreakMode = LineBreakMode.TailTruncation;
        box.Children.Add(title);

        var sign = Ui.Card(box, Pal.Paper.WithAlpha(0.94f), 8, 16);
        sign.Padding = new Thickness(12, 6);
        sign.Stroke = new SolidColorBrush(locked ? Pal.Gray : Pal.Violet);
        sign.StrokeThickness = 2;
        var host = new Grid { WidthRequest = SignWidth, HeightRequest = SignWidth };
        host.Children.Add(new Lifted(sign, 16) { VerticalOptions = LayoutOptions.Center, InputTransparent = true, CascadeInputTransparent = false });
        SemanticProperties.SetDescription(host, $"Раздел {number}: {sec.Title}");
        if (locked) Ui.OnTap(sign, async () => await Dlg.Alert(this, "ЗАКРЫТО", $"Раздел откроется на {unlock} уровне."));
        else sign.InputTransparent = true;
        // квадрат выше знака не должен перехватывать нажатия на дорогу и соседние кружки
        host.InputTransparent = true;
        host.CascadeInputTransparent = false;
        return host;
    }

    // кружок урока с украшением острова; текущий пульсирует, рядом искры
    View LessonStop(LessonState state, int index, string decor, int variant)
    {
        Color fill = state switch
        {
            LessonState.Current => Pal.Violet,
            LessonState.Done => Pal.Amber,
            LessonState.Mastered => Pal.TealInk,
            LessonState.Open => Pal.Paper2,
            _ => Pal.Gray
        };
        Color text = state is LessonState.Current or LessonState.Mastered ? Pal.OnAccent : Pal.Ink;
        string glyph = state is LessonState.Done or LessonState.Mastered ? "✓" : (index + 1).ToString();

        View mark;
        if (state == LessonState.Locked)
            mark = new PixelIcon(Icons.Lock, Pal.OnAccent, LessonSize * 0.36, Pal.Gray) { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        else
        {
            var label = Ui.Text(glyph, LessonSize * 0.36, text, true, TextAlignment.Center);
            // галочки нет в основном шрифте
            if (glyph == "✓") label.FontFamily = "MonoBold";
            label.VerticalTextAlignment = TextAlignment.Center;
            label.HorizontalOptions = LayoutOptions.Center;
            label.VerticalOptions = LayoutOptions.Center;
            mark = label;
        }

        // белая кайма отделяет кружок от пёстрой картинки дороги
        var circle = new Border
        {
            Content = mark,
            BackgroundColor = fill,
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 3,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(LessonSize / 2) },
            WidthRequest = LessonSize,
            HeightRequest = LessonSize,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        if (state == LessonState.Current) pulsing = circle;

        var stop = new Grid { WidthRequest = StopBox, HeightRequest = StopBox };
        stop.Children.Add(new Decor(decor, variant, LessonSize) { Margin = 0 });
        // поля тени симметричные: кружок остаётся в центре украшения
        stop.Children.Add(new Lifted(circle, -1, Pal.Violet, new Thickness(7)) { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, InputTransparent = true });
        if (state == LessonState.Current)
            stop.Children.Add(new Burst { Margin = new Thickness((StopBox - LessonSize) / 2 - 14, (StopBox - LessonSize) / 2 - 8, 0, 0) });
        return stop;
    }

    // подсказка урока: раздел и номер, название и кнопка; у закрытого — почему закрыт
    View LessonCard(Section sec, Lesson lesson, int index, LessonState state)
    {
        var box = new VerticalStackLayout { Spacing = 6 };
        box.Children.Add(Ui.Text($"РАЗДЕЛ {AppState.SectionNumber(sec.Id)} · УРОК {index + 1}", 11, Pal.Ink2, true));
        box.Children.Add(Ui.Text(lesson.Title, 15, Pal.Ink, true));
        if (state == LessonState.Locked)
            box.Children.Add(Ui.Text("Сначала пройди предыдущие уроки или повысь уровень.", 13, Pal.Ink2));
        else
        {
            bool again = state is LessonState.Done or LessonState.Mastered;
            var go = again ? new Btn("ПОВТОРИТЬ", Pal.Paper2, null, 14) : new Btn("▶  НАЧАТЬ", Pal.Violet, Pal.OnAccent, 14);
            go.Clicked += async (s, e) => await Push(new LessonPage(lesson));
            box.Children.Add(go);
        }
        var card = Ui.Card(box, Pal.Paper, 14, 20);
        card.Stroke = new SolidColorBrush(Pal.Line);
        card.StrokeThickness = 1;
        // нажатие по самой подсказке не уходит в картинку под ней и не закрывает подсказку
        Ui.OnTap(card, () => { });
        return new Lifted(card, 20);
    }

    View Hud(int level, string levelName)
    {
        int goal = AppState.DailyGoalXp;
        int today = AppState.TodayXp;

        // три счётчика и две кнопки помещаются в 360 dp: отступы и промежутки уже, чем на других экранах
        var g = new Grid
        {
            ColumnSpacing = 6,
            Padding = new Thickness(12, 12, 12, 0),
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(52)),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(48))
            }
        };

        // выбранная в профиле аватарка, пока её нет — манул; нажатие открывает выбор
        Border avatar;
        if (Avatars.Exists(AppState.P.Avatar))
        {
            avatar = Avatars.View(AppState.P.Avatar, Api.User?.DisplayName ?? "", 52, AppState.P.Frame);
            if (Cosmetics.FindFrame(AppState.P.Frame) == null) avatar.StrokeThickness = 2;
        }
        else
            avatar = new Border
            {
                Content = Ui.Mascot(62),
                BackgroundColor = Pal.Paper,
                Stroke = new SolidColorBrush(Pal.Line),
                StrokeThickness = 2,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(26) },
                WidthRequest = 52,
                HeightRequest = 52
            };
        // здесь аватарка — кнопка, поэтому диктор её видит
        AutomationProperties.SetIsInAccessibleTree(avatar, true);
        SemanticProperties.SetDescription(avatar, "Аватарка, нажми, чтобы сменить");
        Ui.OnTap(avatar, async () => await Push(new AvatarPage()));
        g.Children.Add(avatar);

        // огонёк меняет цвет на рубежах серии: 10, 30, 50, 100 дней
        var (flame, flameAccent) = StreakBurst.FlameColors(AppState.DisplayStreak);
        var streak = Ui.Lcd(AppState.DisplayStreak.ToString(), 15, Icons.Flame, flame, flameAccent);
        streak.Padding = new Thickness(8, 10);
        streak.VerticalOptions = LayoutOptions.Center;
        Grid.SetColumn(streak, 1);
        g.Children.Add(streak);

        var xp = Ui.Lcd(Short(AppState.P.TotalXp), 15, Icons.Brain, Pal.Violet, Pal.Amber);
        xp.Padding = new Thickness(8, 10);
        xp.VerticalOptions = LayoutOptions.Center;
        Grid.SetColumn(xp, 2);
        g.Children.Add(xp);

        // кристаллы: нажатие открывает магазин
        var crystals = Ui.Lcd(Short(AppState.P.Crystals), 15, Icons.Crystal, Pal.Teal, Pal.Cyan);
        crystals.Padding = new Thickness(8, 10);
        crystals.VerticalOptions = LayoutOptions.Center;
        AutomationProperties.SetIsInAccessibleTree(crystals, true);
        SemanticProperties.SetDescription(crystals, $"Кристаллы: {AppState.P.Crystals}. Нажми, чтобы открыть магазин");
        Ui.OnTap(crystals, async () => await Push(new ShopPage()));
        Grid.SetColumn(crystals, 3);
        g.Children.Add(crystals);

        var gear = IconButton(Icons.Gear, "Настройки", async () => await Push(new SettingsPage()));
        Grid.SetColumn(gear, 5);
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

    // задания дня: прогресс и награда; выполненные — с галочкой и зачёркнутой наградой
    static View DailyCard()
    {
        var box = new VerticalStackLayout { Spacing = 8 };
        box.Children.Add(Ui.Text("ЗАДАНИЯ ДНЯ", 12, Pal.RedInk, true));
        foreach (var t in Daily.Today())
        {
            bool done = Daily.IsDone(t);
            int have = Daily.ProgressOf(t);
            var row = new Grid
            {
                ColumnSpacing = 8,
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
            };
            var left = new VerticalStackLayout { Spacing = 3 };
            left.Children.Add(Ui.Text((done ? "✓ " : "") + t.Title, 13, done ? Pal.GreenInk : Pal.Ink, true));
            if (!done) left.Children.Add(new Bar(Pal.Teal) { Value = (double)have / t.Goal });
            row.Children.Add(left);
            // сначала прогресс цели, потом награда и кристалл: «1/2  +6 ◆»
            var prize = new HorizontalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
            prize.Children.Add(Ui.Text(done ? $"ПОЛУЧЕНО  +{t.Reward}" : $"{have}/{t.Goal}  +{t.Reward}", 12, done ? Pal.Ink2 : Pal.Ink, true));
            prize.Children.Add(new PixelIcon(Icons.Crystal, Pal.Teal, 14, Pal.Cyan));
            Grid.SetColumn(prize, 1);
            row.Children.Add(prize);
            SemanticProperties.SetDescription(row, done ? $"{t.Title}: выполнено" : $"{t.Title}: {have} из {t.Goal}, награда {t.Reward} {Crystals.Word(t.Reward)}");
            box.Children.Add(row);
        }
        var card = Ui.Card(box, Pal.Paper, 14);
        // манул в карточке «Начать урок» свисает ниже неё на 14 — без доп. отступа задания дня к нему прилипали
        card.Margin = new Thickness(0, 12, 0, 0);
        return card;
    }

    // 12345 → 12K: пять цифр в верхней панели уже не помещаются рядом с тремя счётчиками,
    // а при крупном тексте сокращаются уже четыре: 1840 → 1.8K
    static string Short(int n)
    {
        int limit = Ui.Scale > 1 ? 1000 : 10000;
        if (n < limit) return n.ToString();
        return n < 10000 ? (n / 1000.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "K" : $"{n / 1000}K";
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

    // манул занимает правый нижний угол, текст и кнопка держатся левее него.
    // Кнопка везёт на остров текущего урока: уроки проходят в теме своего острова
    View HeroCard(Lesson? current)
    {
        const double CatHeight = 136;

        var text = new VerticalStackLayout { Spacing = 8, Margin = new Thickness(0, 0, 128, 8) };
        text.Children.Add(Ui.Text("Изучай психологию вместе с манулом!", 21, Pal.Ink, true));
        text.Children.Add(Ui.Text(current != null ? "Маленькие шаги — большие перемены ♥" : "Ты прошёл всё, что сейчас доступно. Новые острова скоро появятся!", 13, Pal.Ink2));
        var island = current == null ? null : Islands.All.FirstOrDefault(i => Islands.SectionsOf(i).Any(s => s.Lessons.Contains(current)));
        if (island != null)
        {
            var go = new Btn("▶  Плыть к уроку", Pal.Violet, Pal.OnAccent, 15);
            go.Clicked += async (s, e) => await Travel(island);
            text.Children.Add(go);
            text.Children.Add(Ui.Text(island.Name, 12, Pal.Ink2, true));
        }

        var g = new Grid();
        // в наряде из магазина — манул в нём, иначе влюблённый манул
        var cat = AppState.P.Outfit.Length > 0 ? Ui.Mascot(CatHeight) : Ui.Mood("love", CatHeight);
        cat.WidthRequest = CatHeight * 900 / 811;
        cat.HorizontalOptions = LayoutOptions.End;
        cat.VerticalOptions = LayoutOptions.End;
        g.Children.Add(cat);
        g.Children.Add(text);

        // манул прижат к углу, но целиком внутри карточки: отрицательные отступы карточка обрезала по краю
        var card = Ui.Card(g, Pal.Paper, 16, 30);
        card.Padding = new Thickness(16, 16, 6, 6);
        return new Lifted(card, 30);
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
