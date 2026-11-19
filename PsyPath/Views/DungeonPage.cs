using System.Diagnostics;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Dispatching;
using PsyPath.Models;
using PsyPath.Services;
using PsyPath.UI;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;

namespace PsyPath.Views;

public sealed class DungeonPage : ContentPage
{
    static readonly Color Ink = Color.FromArgb("#FFF4D6");
    static readonly Color Gold = Color.FromArgb("#F2C45F");
    static readonly Color DarkInk = Color.FromArgb("#2B1B0D");
    static readonly Color Surface = Color.FromArgb("#231E18");
    static readonly Color Stone = Color.FromArgb("#392E24");
    static readonly Color Outline = Color.FromArgb("#AA8A56");
    static readonly Color CursedOutline = Color.FromArgb("#D698FF");
    static readonly Color Ocher = Color.FromArgb("#CC943C");
    static readonly Color PressedOcher = Color.FromArgb("#B7802E");
    static readonly Color CorrectOutline = Color.FromArgb("#62F5C0");

    readonly DungeonDefinition dungeon;
    readonly Stopwatch stopwatch = new();
    readonly Dictionary<string, Task<DungeonClip?>> clips = new();
    readonly Dictionary<int, AnswerTile> answerTiles = new();
    readonly IDispatcherTimer ticker;
    readonly Image backdrop = new() { Aspect = Aspect.AspectFill, InputTransparent = true };
    readonly Image warrior = new()
    {
        Source = "dungeon_warrior.png", Aspect = Aspect.AspectFit, HeightRequest = 206, WidthRequest = 177,
        HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, IsVisible = false,
        InputTransparent = true, Margin = new Thickness(0, 0, 0, 52)
    };
    readonly SKCanvasView scene = Overlay(), edges = Overlay(), hearts = Overlay(), timer = Overlay(), bricks = Overlay();
    readonly Grid answers = new() { ColumnSpacing = 8, RowSpacing = 8 };
    readonly Grid body = new();
    readonly Grid play = new() { VerticalOptions = LayoutOptions.Center, IsVisible = false };
    readonly ContentView panel = new() { VerticalOptions = LayoutOptions.Center };
    readonly VerticalStackLayout hud = new() { Spacing = 0, HorizontalOptions = LayoutOptions.Center };
    readonly Label prompt = Text("", 19 * 1.15, Ink, true);
    DungeonRun run;
    DungeonPhase? shown;
    DungeonTurn? drawnTurn;
    int drawnRevision, drawnRoom = -1, drawnHalfHearts = -1, clockPosition = -1, frameIndex = -1;
    bool drawnOverheal, showingFlashes, leaving, enteringQuestion, showHints;
    double now, clockFraction, clockDrawnAt, clockShakePhase, resolutionAt, enteredAt;
    DungeonClip? sceneClip;
    DungeonBricks? fragments;
    DungeonReward? prize;
    CancellationTokenSource? artwork;
    Task<DungeonClip?> queued = Task.FromResult<DungeonClip?>(null);
    Window? gameWindow;
    bool UsesDungeonMusic => dungeon.Id == DungeonDefinition.Jungle.Id;
    double AnswerWidth => Math.Clamp((play.WidthRequest - answers.ColumnSpacing) / 2 - 20, 82, 132);

    public DungeonPage(DungeonDefinition dungeon)
    {
        this.dungeon = dungeon;
        run = new DungeonRun(dungeon, Random.Shared);
        BackgroundColor = Surface;
        Title = dungeon.Title;
        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);
        NavigationPage.SetHasNavigationBar(this, false);
        ticker = Dispatcher.CreateTimer();
        ticker.Interval = TimeSpan.FromMilliseconds(33);
        ticker.Tick += (_, _) => Tick();

        hearts.WidthRequest = 177;
        hearts.HeightRequest = 32;
        hearts.HorizontalOptions = LayoutOptions.Center;
        AutomationProperties.SetIsInAccessibleTree(hearts, true);
        hearts.PaintSurface += (_, e) =>
        {
            e.Surface.Canvas.Clear(SKColors.Transparent);
            DungeonEffects.DrawHearts(e.Surface.Canvas, e.Info, run.HalfHearts, run.Overheal);
        };
        timer.WidthRequest = 232;
        timer.HeightRequest = 90;
        timer.HorizontalOptions = LayoutOptions.Center;
        timer.PaintSurface += (_, e) =>
        {
            e.Surface.Canvas.Clear(SKColors.Transparent);
            DungeonEffects.DrawClock(e.Surface.Canvas, e.Info, clockFraction, now, clockShakePhase, Anim.Reduced);
        };
        scene.PaintSurface += (_, e) =>
        {
            e.Surface.Canvas.Clear(SKColors.Transparent);
            sceneClip?.Draw(e.Surface.Canvas, frameIndex, e.Info);
        };
        edges.PaintSurface += (_, e) =>
        {
            e.Surface.Canvas.Clear(SKColors.Transparent);
            if (showingFlashes)
                DungeonEffects.DrawFlashes(e.Surface.Canvas, e.Info, run.Flashes, now, Anim.Reduced);
        };
        bricks.PaintSurface += (_, e) =>
        {
            e.Surface.Canvas.Clear(SKColors.Transparent);
            fragments?.Draw(e.Surface.Canvas, now);
        };

        Content = MakeLayout();
        SizeChanged += (_, _) =>
        {
            if (Width <= 32) return;
            body.WidthRequest = Math.Min(Width - 32, 560);
            play.WidthRequest = Math.Min(body.WidthRequest, 340);
            foreach (var tile in answerTiles.Values) tile.WidthRequest = AnswerWidth;
        };
        Render();
    }

    View MakeLayout()
    {
        var layout = new Grid();
        layout.Children.Add(backdrop);
        layout.Children.Add(warrior);
        layout.Children.Add(scene);
        layout.Children.Add(new BoxView
        {
            InputTransparent = true,
            Background = new LinearGradientBrush(new GradientStopCollection
            {
                new(Color.FromArgb("#F1161710"), 0),
                new(Color.FromArgb("#7D161710"), 0.25f),
                new(Colors.Transparent, 0.51f),
                new(Color.FromArgb("#D9181510"), 0.76f),
                new(Color.FromArgb("#FF181510"), 1)
            }, new Point(0, 0), new Point(0, 1))
        });

        var front = new Grid
        {
            Padding = new Thickness(16, 8, 16, 12), RowSpacing = 4,
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) }
        };
        var back = ActionButton("Назад", false);
        back.HorizontalOptions = LayoutOptions.Start;
        back.VerticalOptions = LayoutOptions.Start;
        back.Clicked += async (_, _) => await Leave();

        var heading = new Grid { MinimumHeightRequest = 48, IsClippedToBounds = true };
        heading.Children.Add(back);
        hud.Margin = new Thickness(0, 46, 0, 0);
        hud.Children.Add(hearts);
        hud.Children.Add(timer);
        hud.Children.Add(prompt);
        heading.Children.Add(hud);
        front.Children.Add(heading);

        var scroll = new ScrollView
        {
            Content = body, MaximumWidthRequest = 560, HorizontalOptions = LayoutOptions.Center,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never
        };
        scroll.SizeChanged += (_, _) =>
        {
            if (scroll.Height > 0) body.MinimumHeightRequest = scroll.Height;
        };
        Grid.SetRow(scroll, 1);
        front.Children.Add(scroll);
        layout.Children.Add(front);
        layout.Children.Add(edges);

        answers.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        answers.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        play.MaximumWidthRequest = 340;
        play.HorizontalOptions = LayoutOptions.Center;
        play.Children.Add(answers);
        play.Children.Add(bricks);
        // Keep gameplay in a single native scroll container through all room changes.
        body.Children.Add(play);
        body.Children.Add(panel);
        return layout;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        CrashLog.Write("Page", nameof(DungeonPage));
        if (UsesDungeonMusic) Music.Enter(Music.Zone.JungleDungeon);
        artwork = new CancellationTokenSource();
        if (Window is { } window)
        {
            gameWindow = window;
            window.Stopped += OnStopped;
            window.Resumed += OnResumed;
        }
        PrimeRoom();
        Resume();
    }

    protected override void OnDisappearing()
    {
        Pause();
        if (UsesDungeonMusic) Music.Enter(Music.Zone.Menu);
        if (gameWindow is { } window)
        {
            window.Stopped -= OnStopped;
            window.Resumed -= OnResumed;
            gameWindow = null;
        }
        artwork?.Cancel();
        artwork?.Dispose();
        artwork = null;
        SetScene(null, -1);
        foreach (var name in clips.Keys.ToArray()) Release(name);
        base.OnDisappearing();
    }

    void OnStopped(object? sender, EventArgs e) => Pause();
    void OnResumed(object? sender, EventArgs e) => Resume();

    void Pause()
    {
        ticker.Stop();
        stopwatch.Stop();
    }

    void Resume()
    {
        if (leaving || run.Phase == DungeonPhase.Briefing ||
            (run.Phase is DungeonPhase.Victory or DungeonPhase.Defeat && run.Flashes.Count == 0)) return;
        stopwatch.Start();
        ticker.Start();
    }

    void Start()
    {
        if (run.Phase is not (DungeonPhase.Briefing or DungeonPhase.Victory or DungeonPhase.Defeat)) return;
        if (run.Phase != DungeonPhase.Briefing)
            run = new DungeonRun(dungeon, Random.Shared);
        prize = null;
        drawnTurn = null;
        showHints = Settings.DungeonAnswerHints;
        stopwatch.Reset();
        now = clockDrawnAt = clockShakePhase = 0;
        run.Start(now);
        Resume();
        Render();
    }

    void Tick()
    {
        now = stopwatch.Elapsed.TotalSeconds;
        run.Advance(now);
        Render();
    }

    void Render()
    {
        if (prize == null && run.CompletionTime > TimeSpan.Zero)
            prize = DungeonRewards.Collect(dungeon, run.CompletionTime);

        if (drawnRoom != run.RoomIndex)
        {
            SetScene(null, -1);
            if (drawnRoom >= 0) Release($"transition_{drawnRoom + 1}");
            drawnRoom = run.RoomIndex;
            backdrop.Source = $"dungeon_room_{drawnRoom + 1}.png";
            PrimeRoom();
        }

        if (shown != run.Phase)
        {
            shown = run.Phase;
            switch (run.Phase)
            {
                case DungeonPhase.Briefing:
                    ShowBriefing();
                    break;
                case DungeonPhase.Question:
                    panel.IsVisible = false;
                    play.IsVisible = true;
                    fragments = null;
                    bricks.InvalidateSurface();
                    break;
                case DungeonPhase.Resolution:
                    ShowResolution();
                    break;
                case DungeonPhase.Passage:
                    ShowBody(Panel(Text($"Комната пройдена\n{dungeon.Rooms[run.RoomIndex + 1].Title}", 23, Ink, true),
                        new Thickness(24, 16)));
                    break;
                case DungeonPhase.Victory:
                case DungeonPhase.Defeat:
                    ShowResult();
                    break;
            }
        }

        hud.IsVisible = run.Phase != DungeonPhase.Briefing;
        timer.IsVisible = run.Phase is DungeonPhase.Question or DungeonPhase.Resolution;
        prompt.IsVisible = timer.IsVisible;
        if (drawnHalfHearts != run.HalfHearts || drawnOverheal != run.Overheal)
        {
            drawnHalfHearts = run.HalfHearts;
            drawnOverheal = run.Overheal;
            SemanticProperties.SetDescription(hearts, $"Жизни: {drawnHalfHearts / 2d:0.#}" + (drawnOverheal ? ", избыток лечения" : ""));
            hearts.InvalidateSurface();
        }
        if (timer.IsVisible)
        {
            clockFraction = run.TimeFraction(now);
            double urgency = 1 - clockFraction;
            clockShakePhase += (now - clockDrawnAt) * (2.7 + 16 * urgency * urgency);
            int pixel = (int)(clockFraction * DungeonEffects.ClockLength);
            if (clockPosition != pixel || !Anim.Reduced)
            {
                clockPosition = pixel;
                timer.InvalidateSurface();
            }
            UpdateCards();
        }
        clockDrawnAt = now;

        bool flash = run.Flashes.Count != 0;
        if (flash || showingFlashes)
        {
            showingFlashes = flash;
            edges.InvalidateSurface();
        }
        UpdateScene();
        if (!flash && run.Phase is (DungeonPhase.Victory or DungeonPhase.Defeat)) Pause();
    }

    void UpdateCards()
    {
        var turn = run.Turn!;
        if (run.Phase == DungeonPhase.Question)
        {
            if (drawnTurn != turn || drawnRevision != turn.Revision)
                ShowCards(turn);
            if (enteringQuestion)
            {
                double slide = Math.Clamp((now - enteredAt) / 0.20, 0, 1);
                prompt.TranslationX = 52 * Math.Pow(1 - slide, 3);
                prompt.Opacity = 0.25 + slide * 0.75;
                enteringQuestion = slide < 1;
            }
            if (!Anim.Reduced)
            {
                int i = 0;
                foreach (var tile in answerTiles.Values)
                {
                    double angle = now * 2.7 + i++ * 1.8;
                    tile.TranslationX = Math.Sin(angle) * 1.3;
                    tile.TranslationY = Math.Cos(angle) * 1.3;
                }
            }
            return;
        }

        double fade = Anim.Reduced ? 0.42 : 1 - Math.Clamp((now - resolutionAt) / 0.46, 0, 1);
        foreach (var (id, tile) in answerTiles)
            tile.Opacity = run.Selected == id ? 0 : fade;
        if (!Anim.Reduced)
        {
            double slide = Math.Clamp((run.ResolutionFraction(now) - 0.62) / 0.38, 0, 1);
            prompt.TranslationX = -Math.Max(80, prompt.Width) * slide * slide;
            prompt.Opacity = 1 - slide;
        }
        if (fragments != null)
        {
            if (!fragments.IsAlive(now)) fragments = null;
            bricks.InvalidateSurface();
        }
    }

    void ShowCards(DungeonTurn turn)
    {
        bool newTurn = drawnTurn != turn;
        if (newTurn)
        {
            answers.Children.Clear();
            answerTiles.Clear();
            enteringQuestion = drawnTurn != null && !Anim.Reduced;
            enteredAt = now;
            prompt.Text = turn.Prompt;
            prompt.TranslationX = enteringQuestion ? 52 : 0;
            prompt.Opacity = enteringQuestion ? 0.25 : 1;
        }
        drawnTurn = turn;
        drawnRevision = turn.Revision;

        int rows = (turn.Answers.Count + 1) / 2;
        while (answers.RowDefinitions.Count < rows)
            answers.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        while (answers.RowDefinitions.Count > rows)
            answers.RowDefinitions.RemoveAt(answers.RowDefinitions.Count - 1);

        for (int i = 0; i < turn.Answers.Count; i++)
        {
            var choice = turn.Answers[i];
            if (!answerTiles.TryGetValue(choice.Id, out var tile))
            {
                int id = choice.Id;
                tile = new AnswerTile { WidthRequest = AnswerWidth };
                tile.Button.Clicked += (_, _) =>
                {
                    if (leaving || run.Turn != turn) return;
                    now = stopwatch.Elapsed.TotalSeconds;
                    run.Answer(id, now);
                    Render();
                };
                answerTiles.Add(id, tile);
                answers.Children.Add(tile);
            }
            tile.SetChoice(choice, i, turn.Id, showHints && turn.IsCorrect(choice.Id));
            int columns = i == turn.Answers.Count - 1 && i % 2 == 0 ? 2 : 1;
            Grid.SetRow(tile, i / 2);
            Grid.SetColumn(tile, i % 2);
            Grid.SetColumnSpan(tile, columns);
        }
    }

    void ShowResolution()
    {
        resolutionAt = now;
        var result = run.Outcome;
        Sound.Play(result == DungeonOutcome.Correct ? "ok" : "bad");
        if (!Anim.Reduced && run.Selected is int id && answerTiles.TryGetValue(id, out var selection))
            fragments = new DungeonBricks(new Rect(selection.X + selection.TranslationX, selection.Y + selection.TranslationY,
                selection.Width, selection.Height), now, result == DungeonOutcome.Correct);
        foreach (var tile in answerTiles.Values)
        {
            tile.InputTransparent = true;
            tile.TranslationX = tile.TranslationY = 0;
        }
    }

    void UpdateScene()
    {
        DungeonClip? clip = null;
        int frame = -1;
        if (!Anim.Reduced)
        {
            if (run.Phase == DungeonPhase.Passage)
            {
                clip = Available($"transition_{run.RoomIndex + 1}");
                frame = clip?.FrameAt(run.PassageFraction(now) * DungeonRun.PassageSeconds) ?? -1;
            }
            else if (run.Phase is DungeonPhase.Question or DungeonPhase.Resolution && run.Attack is { } attack)
            {
                clip = Available(AttackArt(attack.Attack));
                frame = clip?.FrameAt(now - attack.At) ?? -1;
            }
        }
        SetScene(frame >= 0 ? clip : null, frame);
        warrior.IsVisible = run.RoomIndex == dungeon.Rooms.Count - 1 && frame < 0 &&
            run.Phase is DungeonPhase.Question or DungeonPhase.Resolution;
    }

    void SetScene(DungeonClip? clip, int frame)
    {
        if (clip == sceneClip && frame == frameIndex) return;
        sceneClip = clip;
        frameIndex = frame;
        scene.InvalidateSurface();
    }

    void PrimeRoom()
    {
        if (Anim.Reduced || artwork is null) return;
        if (run.RoomIndex + 1 < dungeon.Rooms.Count)
            Queue($"transition_{run.RoomIndex + 1}", artwork.Token);
        if (run.RoomIndex + 2 >= dungeon.Rooms.Count)
            foreach (var attack in Enum.GetValues<DungeonAttack>())
                Queue(AttackArt(attack), artwork.Token);
    }

    void Queue(string name, CancellationToken token)
    {
        if (clips.ContainsKey(name)) return;
        var previous = queued;
        queued = Task.Run(async () =>
        {
            await previous.ConfigureAwait(false);
            if (token.IsCancellationRequested) return null;
            try
            {
                using var source = await FileSystem.OpenAppPackageFileAsync($"dungeon/{name}.zip").ConfigureAwait(false);
                return await DungeonClip.Decode(source, name, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return null; }
            catch (Exception ex)
            {
                CrashLog.Write("Dungeon", $"{name}: {ex.Message}");
                return null;
            }
        });
        clips.Add(name, queued);
    }

    DungeonClip? Available(string key) =>
        clips.TryGetValue(key, out var task) && task.IsCompletedSuccessfully ? task.Result : null;

    void Release(string name)
    {
        if (clips.Remove(name, out var task)) _ = DisposeClip(task);
    }

    static async Task DisposeClip(Task<DungeonClip?> task) => (await task.ConfigureAwait(false))?.Dispose();

    static string AttackArt(DungeonAttack attack) => attack switch
    {
        DungeonAttack.Cleaver => "cleaver",
        DungeonAttack.CipherSpell => "cipher",
        DungeonAttack.Dart => "dart",
        _ => "shaman"
    };

    void ShowBriefing()
    {
        var copy = new VerticalStackLayout { Spacing = 12 };
        copy.Children.Add(Text(dungeon.Title, 31, Gold, true));
        copy.Children.Add(Text("Высокая сложность", 17, Ink, true));
        copy.Children.Add(Text("Это проверка быстрой памяти и ассоциаций и лёгкий контрольный опросник.", 16, Ink));
        copy.Children.Add(Text("У тебя 4 жизни. Дай по 4 верных ответа в каждой из трёх комнат. Времени на ответ будет всё меньше.", 16, Ink));
        var start = ActionButton("Войти в храм", true);
        start.Clicked += (_, _) => Start();
        copy.Children.Add(start);
        ShowBody(Panel(copy, new Thickness(20)));
    }

    void ShowResult()
    {
        var copy = new VerticalStackLayout { Spacing = 10 };
        copy.Children.Add(Text(prize.HasValue ? "Храм пройден" : "Попытка окончена", 28, Gold, true));
        if (prize is { } reward)
        {
            copy.Children.Add(Text($"Время прохождения  {FormatTime(run.CompletionTime)}", 18, Ink, true));
            copy.Children.Add(Text($"{(reward.IsRecord ? "Новый рекорд" : "Лучшее время")}  {FormatTime(reward.BestTime)}", 16, Ink));
            copy.Children.Add(Text($"+{DungeonDefinition.ExperienceReward} XP  ·  +{DungeonDefinition.RatingReward} рейтинга", 16, Gold, true));
            if (reward.FirstClear)
            {
                var bonus = new VerticalStackLayout { Spacing = 5 };
                bonus.Children.Add(Text("Сундук первого прохождения", 16, Gold, true));
                bonus.Children.Add(Text($"+{DungeonDefinition.FirstClearCrystals} кристаллов", 15, Ink, true));
                if (reward.Avatar is { } avatar)
                {
                    bonus.Children.Add(Avatars.View(avatar.Id, avatar.Name, 42));
                    bonus.Children.Add(Text(avatar.Name, 14, Ink));
                }
                copy.Children.Add(Panel(bonus, new Thickness(10)));
            }
        }
        else
        {
            copy.Children.Add(Text("Жизни закончились. Новая попытка начнётся у входа в храм.", 17, Ink));
        }

        var retry = ActionButton("Ещё попытка", true);
        retry.Clicked += (_, _) => Start();
        copy.Children.Add(retry);
        var back = ActionButton("К острову", false);
        back.Clicked += async (_, _) => await Leave();
        copy.Children.Add(back);
        ShowBody(Panel(copy, new Thickness(20)));
    }

    void ShowBody(View child)
    {
        play.IsVisible = false;
        panel.Content = child;
        panel.IsVisible = true;
    }

    static string FormatTime(TimeSpan time) => $"{(int)time.TotalMinutes:00}:{time.Seconds:00}.{time.Milliseconds / 100}";

    async Task Leave()
    {
        if (leaving) return;
        leaving = true;
        Pause();
        try { await Navigation.PopAsync(false); }
        finally
        {
            leaving = false;
            if (gameWindow != null) Resume();
        }
    }

    protected override bool OnBackButtonPressed()
    {
        _ = Leave();
        return true;
    }

    static SKCanvasView Overlay()
    {
        var layer = new SKCanvasView { IgnorePixelScaling = true, InputTransparent = true };
        AutomationProperties.SetIsInAccessibleTree(layer, false);
        return layer;
    }

    static Label Text(string text, double size, Color color, bool bold = false) =>
        Ui.Text(text, size, color, bold, TextAlignment.Center);

    static Border Panel(View content, Thickness padding) => new()
    {
        Content = content, Padding = padding, BackgroundColor = Surface.WithAlpha(0.96f),
        Stroke = new SolidColorBrush(Outline), StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(18) }
    };

    static Button ActionButton(string title, bool primary) => new()
    {
        Text = title, FontFamily = "BodyBold", FontSize = 16, MinimumHeightRequest = 48,
        BackgroundColor = primary ? Gold : Stone, TextColor = primary ? DarkInk : Ink,
        BorderColor = Outline, BorderWidth = 1, CornerRadius = 12,
        Padding = new Thickness(12, 7)
    };

    sealed class AnswerTile : Grid
    {
        static readonly double[] Angles = [-24, 18, 30, -13, -19, 10];
        readonly Border plate = new() { BackgroundColor = Ocher, InputTransparent = true };
        public Button Button { get; } = new()
        {
            FontFamily = "BodyBold", FontSize = 16, MinimumHeightRequest = 52,
            BackgroundColor = Colors.Transparent, TextColor = DarkInk, Padding = new Thickness(6, 4)
        };

        public AnswerTile()
        {
            HorizontalOptions = LayoutOptions.Center;
            Margin = new Thickness(10, 23);
            Children.Add(plate);
            Children.Add(Button);
            Button.Pressed += (_, _) => plate.BackgroundColor = PressedOcher;
            Button.Released += (_, _) => plate.BackgroundColor = Ocher;
        }

        public void SetChoice(DungeonAnswer choice, int position, int turn, bool correct)
        {
            Button.Text = choice.Text;
            SemanticProperties.SetHint(Button, correct ? "Правильный ответ" : null);
            int pose = (position + turn) % Angles.Length;
            plate.Rotation = Angles[pose];
            // Mirror the stone across its horizontal axis, keeping the button and its text upright.
            plate.StrokeShape = new RoundRectangle
            {
                CornerRadius = pose % 2 == 0 ? new CornerRadius(18, 6, 6, 18) : new CornerRadius(6, 18, 18, 6)
            };
            plate.Stroke = new SolidColorBrush(correct ? CorrectOutline : choice.Cursed ? CursedOutline : DarkInk);
            plate.StrokeThickness = correct ? 4 : choice.Cursed ? 3 : 1;
        }
    }
}
