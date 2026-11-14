using PsyPath.Models;
using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

public class LessonPage : BasePage
{
    // null — «работа над ошибками»: задания из разных уроков, урок не отмечается пройденным
    readonly Lesson? lesson;
    readonly string title;
    readonly Random rnd = new();

    // ключ задания для списка ошибок
    readonly Dictionary<TaskItem, string> keys = new();
    readonly List<TaskItem> queue = new();
    readonly HashSet<TaskItem> retried = new();
    readonly HashSet<TaskItem> answeredFirst = new();
    readonly HashSet<TaskItem> solved = new();
    int total;
    int firstTryCorrect;
    int mistakes;
    int xpGained;
    double ratingStart;
    int levelStart;

    readonly Bar progress = new(Pal.Green);
    readonly VerticalStackLayout body = new() { Spacing = 12, Padding = new Thickness(18, 12, 18, 24) };
    readonly ScrollView scroll = new();
    readonly Btn actionBtn = new("ПРОВЕРИТЬ", Pal.Violet, Pal.Paper);
    readonly Border feedback;
    readonly Label fbTitle = Ui.Text("", 18, Pal.Ink, true);
    readonly Label fbText = Ui.Text("", 13, Pal.Ink);
    readonly Label fbRef;
    RefEntry? fbEntry;

    TaskItem? cur;
    Func<bool>? evaluate;
    // вызывается после проверки с её итогом: подсветить верное и неверное
    Action<bool>? onChecked;
    bool isChecked;
    bool switching;
    // введённый ответ засчитан с опечаткой
    bool typo;

    // теория перед заданиями: номер показанной карточки, -1 — уже идут задания
    readonly List<TheoryCard> theory;
    int theoryAt = -1;

    // подсказки: ограниченное число раз за весь урок (не за задание)
    int hintsLeft = 2;
    readonly Label hintIcon = Ui.Text("?", 18, Pal.TealInk, true, TextAlignment.Center);
    readonly Label hintCount = Ui.Text("2", 10, Pal.Ink2, true, TextAlignment.Center);

    public LessonPage(Lesson lesson) : this(lesson, lesson.Title, Practice.ForLesson(lesson, AppState.PickTasks(lesson, 8)))
    {
    }

    // работа над ошибками
    public LessonPage(List<PracticeTask> tasks) : this(null, "Работа над ошибками", tasks)
    {
    }

    LessonPage(Lesson? lesson, string title, List<PracticeTask> tasks) : base(true)
    {
        this.lesson = lesson;
        this.title = title;
        theory = lesson?.Theory ?? new();

        // «▸ В СПРАВОЧНИК: ТЕРМИН» — статья по теме задания, если она есть
        fbRef = (Label)Form.Link("", Pal.TealInk, async () =>
        {
            if (fbEntry != null) await Push(new RefEntryPage(fbEntry));
        });
        fbRef.Padding = new Thickness(0);

        var fbBox = new VerticalStackLayout { Spacing = 4 };
        fbBox.Children.Add(fbTitle);
        fbBox.Children.Add(fbText);
        fbBox.Children.Add(fbRef);
        feedback = Ui.Card(fbBox, Pal.Paper, 12);
        feedback.IsVisible = false;

        ratingStart = AppState.P.Rating;
        levelStart = AppState.Level;

        foreach (var t in tasks)
        {
            keys[t.Task] = t.Key;
            queue.Add(t.Task);
        }
        total = queue.Count;

        BuildLayout();
        actionBtn.Clicked += OnAction;
        if (theory.Count > 0)
            _ = ShowTheory(0);
        else
            _ = ShowNext();
    }

    void BuildLayout()
    {
        var close = Ui.Text("✕", 24, Pal.Ink, true, TextAlignment.Center);
        // зона нажатия 48×48 dp
        close.WidthRequest = 48;
        close.HeightRequest = 48;
        Ui.OnTap(close, async () =>
        {
            bool leave = await Dlg.Confirm(this, "ВЫЙТИ?", "Прогресс этого урока будет потерян.", "ВЫЙТИ", "ОСТАТЬСЯ");
            if (leave) await Pop();
        });

        var header = new Grid
        {
            ColumnSpacing = 12,
            Padding = new Thickness(16, 10, 16, 6),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        close.VerticalTextAlignment = TextAlignment.Center;
        header.Children.Add(close);
        progress.VerticalOptions = LayoutOptions.Center;
        Grid.SetColumn(progress, 1);
        header.Children.Add(progress);

        // книжка — конспект теории урока, можно открыть посреди заданий
        if (theory.Count > 0)
        {
            var book = new Grid { WidthRequest = 44, HeightRequest = 48, VerticalOptions = LayoutOptions.Center };
            book.Children.Add(new Image
            {
                Source = "tab_book.png",
                WidthRequest = 24,
                HeightRequest = 24,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            });
            SemanticProperties.SetDescription(book, "Теория урока");
            Ui.OnTap(book, async () =>
            {
                if (theoryAt >= 0 || lesson == null) return;
                await Push(new TheoryPage(lesson));
            });
            Grid.SetColumn(book, 2);
            header.Children.Add(book);
        }

        // «?» в углу — подсказка по текущему заданию, ограничена на весь урок
        var hintBox = new VerticalStackLayout
        {
            Spacing = 0,
            WidthRequest = 44,
            HeightRequest = 48,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        hintIcon.VerticalTextAlignment = TextAlignment.Center;
        hintBox.Children.Add(hintIcon);
        hintBox.Children.Add(hintCount);
        SemanticProperties.SetDescription(hintBox, "Подсказка");
        Ui.OnTap(hintBox, async () =>
        {
            if (cur == null || isChecked || theoryAt >= 0) return;
            if (hintsLeft <= 0)
            {
                await Dlg.Alert(this, "ПОДСКАЗКИ", "На этот урок подсказки закончились.");
                return;
            }
            hintsLeft--;
            hintCount.Text = hintsLeft.ToString();
            if (hintsLeft == 0) hintIcon.TextColor = Pal.Ink2;
            string hint = string.IsNullOrWhiteSpace(cur.Explain)
                ? "Подумай ещё раз — ответ напрямую связан с темой вопроса."
                : cur.Explain;
            await Dlg.Alert(this, "ПОДСКАЗКА", hint);
        });
        Grid.SetColumn(hintBox, 3);
        header.Children.Add(hintBox);

        var topBox = new VerticalStackLayout { Spacing = 0 };
        topBox.Children.Add(header);

        var bottom = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(16, 8, 16, 16), BackgroundColor = Pal.Paper };
        bottom.Children.Add(feedback);
        bottom.Children.Add(actionBtn);

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };
        root.Children.Add(topBox);
        scroll.Content = body;
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        Grid.SetRow(bottom, 2);
        root.Children.Add(bottom);

        SetBody(root);
    }

    async Task ShowNext()
    {
        if (queue.Count == 0)
        {
            await Finish();
            return;
        }

        // первый экран показывает анимация входа страницы, остальные сменяются сдвигом
        bool animate = body.Children.Count > 0;
        actionBtn.Enabled = false;
        if (animate)
            await Task.WhenAll(body.Fade(0, 110), body.Move(-24, 0, 110, Easing.CubicIn));

        cur = queue[0];
        queue.RemoveAt(0);
        isChecked = false;
        typo = false;
        evaluate = null;
        onChecked = null;

        feedback.IsVisible = false;
        actionBtn.Text = "ПРОВЕРИТЬ";
        actionBtn.SetColors(Pal.Violet, Pal.Paper);
        actionBtn.Enabled = false;

        body.Children.Clear();
        Render(cur);

        if (animate)
        {
            body.TranslationX = 24;
            await Task.WhenAll(body.Fade(1, 180), body.Move(0, 0, 180, Easing.CubicOut));
        }
    }

    // карточка теории: заголовок и текст; её же показывает конспект TheoryPage
    public static View TheoryCardView(TheoryCard card)
    {
        var box = new VerticalStackLayout { Spacing = 8 };
        box.Children.Add(Ui.Text(card.Title, 19, Pal.Ink, true));
        box.Children.Add(Ui.Text(card.Text, 16));
        return Ui.Box(box, Pal.Paper, 16);
    }

    async Task ShowTheory(int i)
    {
        bool animate = body.Children.Count > 0;
        actionBtn.Enabled = false;
        if (animate)
            await Task.WhenAll(body.Fade(0, 110), body.Move(-24, 0, 110, Easing.CubicIn));

        theoryAt = i;
        bool last = i == theory.Count - 1;
        feedback.IsVisible = false;
        actionBtn.Text = last ? "К ЗАДАНИЯМ ▶" : "ДАЛЕЕ ▶";
        actionBtn.SetColors(Pal.Violet, Pal.Paper);

        body.Children.Clear();
        body.Children.Add(Ui.Text($"ТЕОРИЯ  //  {i + 1} ИЗ {theory.Count}", 11, Pal.TealInk, true));
        if (i == 0)
        {
            body.Children.Add(Ui.Mood("study", 110));
            body.Children.Add(Ui.Text("Сначала немного теории — с ней задания решаются легче.", 13, Pal.Ink2));
        }
        body.Children.Add(TheoryCardView(theory[i]));
        if (!last)
            body.Children.Add(Form.Link("ПРОПУСТИТЬ ТЕОРИЮ ▸", Pal.Ink2, async () => await StartTasks(), TextAlignment.Center));
        _ = scroll.ScrollToAsync(0, 0, false);
        actionBtn.Enabled = true;

        if (animate)
        {
            body.TranslationX = 24;
            await Task.WhenAll(body.Fade(1, 180), body.Move(0, 0, 180, Easing.CubicOut));
        }
    }

    async Task StartTasks()
    {
        if (theoryAt < 0 || switching) return;
        switching = true;
        theoryAt = -1;
        await ShowNext();
        switching = false;
    }

    async void OnAction(object? sender, EventArgs e)
    {
        if (switching) return;
        if (theoryAt >= 0)
        {
            if (theoryAt == theory.Count - 1)
            {
                await StartTasks();
                return;
            }
            switching = true;
            await ShowTheory(theoryAt + 1);
            switching = false;
            return;
        }
        if (cur == null) return;
        if (!isChecked)
        {
            DoCheck(null);
            return;
        }
        switching = true;
        await ShowNext();
        switching = false;
    }

    void DoCheck(bool? forced)
    {
        if (cur == null || isChecked) return;
        bool ok = forced ?? (evaluate != null && evaluate());
        isChecked = true;

        bool first = answeredFirst.Add(cur);
        if (first)
        {
            if (keys.TryGetValue(cur, out var key)) Practice.Record(key, ok);
            AppState.P.Rating = AppState.UpdateRating(AppState.P.Rating, cur.Difficulty, ok);
            AppState.P.Answered++;
            if (ok)
            {
                firstTryCorrect++;
                AppState.P.Correct++;
            }
        }

        if (ok)
        {
            solved.Add(cur);
            xpGained += 10;
            progress.Value = total == 0 ? 1 : (double)solved.Count / total;
        }
        else
        {
            mistakes++;
            // ошибку повторяем один раз в конце урока; на повторе она просто остаётся в списке до следующего раза
            if (lesson != null && retried.Add(cur)) queue.Add(cur);
        }

        onChecked?.Invoke(ok);
        Sound.Play(ok ? "ok" : "bad");
        ShowFeedback(ok);
        AppState.Save();
    }

    void ShowFeedback(bool ok)
    {
        feedback.BackgroundColor = ok ? Pal.GreenSoft : Pal.RedSoft;
        fbTitle.Text = ok ? "ВЕРНО!" : "НЕ СОВСЕМ";
        fbTitle.TextColor = ok ? Pal.GreenInk : Pal.RedInk;

        var sb = new System.Text.StringBuilder();
        if (!ok) sb.AppendLine((cur!.Type == "branch" ? "Лучший путь: " : "Правильно: ") + CorrectAnswerText(cur!));
        if (ok && typo) sb.AppendLine("Засчитано с опечаткой. Правильно: " + CorrectAnswerText(cur!));
        sb.Append(cur!.Explain);
        fbText.Text = sb.ToString().Trim();

        fbEntry = Practice.FindRef(cur);
        fbRef.IsVisible = fbEntry != null;
        if (fbEntry != null) fbRef.Text = "▸ В СПРАВОЧНИК: " + fbEntry.Term.ToUpperInvariant();

        feedback.IsVisible = true;
        feedback.Opacity = 0;
        feedback.TranslationY = 40;
        _ = ShowFeedbackAnimated(ok);

        actionBtn.Text = "ПРОДОЛЖИТЬ";
        actionBtn.SetColors(ok ? Pal.GreenInk : Pal.RedInk, Pal.Paper);
        actionBtn.Enabled = true;
    }

    // плашка выезжает снизу; верный ответ «подпрыгивает», неверный — покачивается
    async Task ShowFeedbackAnimated(bool ok)
    {
        await Task.WhenAll(feedback.Fade(1, 200), feedback.Move(0, 0, 220, Easing.CubicOut));
        await (ok ? feedback.Bounce() : feedback.Shake());
    }

    static string CorrectAnswerText(TaskItem t)
    {
        switch (t.Type)
        {
            case "truefalse":
                return t.AnswerBool ? "Верно" : "Неверно";
            case "order":
                return string.Join(" → ", t.Items);
            case "match":
                return string.Join("; ", t.Pairs.Select(p => p[0] + " — " + p[1]));
            case "input":
                return t.Options.FirstOrDefault() ?? "";
            case "branch":
                return Practice.BestPath(t);
            default:
                return t.AnswerIndex >= 0 && t.AnswerIndex < t.Options.Count ? t.Options[t.AnswerIndex] : "";
        }
    }

    void Render(TaskItem t)
    {
        switch (t.Type)
        {
            case "truefalse":
                RenderChoice(t, new List<string> { "ВЕРНО", "НЕВЕРНО" }, t.AnswerBool ? 0 : 1, null);
                break;
            case "match":
                RenderMatch(t);
                break;
            case "order":
                RenderOrder(t);
                break;
            case "case":
                RenderChoice(t, t.Options, t.AnswerIndex, t.Story);
                break;
            case "input":
                RenderInput(t);
                break;
            case "branch":
                RenderBranch(t);
                break;
            default:
                RenderChoice(t, t.Options, t.AnswerIndex, null);
                break;
        }
    }

    void AddHeader(TaskItem t)
    {
        string instruction = t.Type switch
        {
            "truefalse" => "ВЕРНО ИЛИ НЕТ?",
            "fill" => "ЗАПОЛНИ ПРОПУСК",
            "case" => "МИНИ-КЕЙС",
            "match" => "СОЕДИНИ ПАРЫ",
            "order" => "РАССТАВЬ ПО ПОРЯДКУ",
            "input" => t.Q.Contains("___") ? "ВПИШИ ПРОПУЩЕННОЕ" : "ВВЕДИ ОТВЕТ",
            "branch" => "КЕЙС: ЧТО ДЕЛАТЬ?",
            _ => "ВЫБЕРИ ВЕРНЫЙ ОТВЕТ"
        };
        int d = Math.Clamp(t.Difficulty, 1, 5);
        string meter = string.Concat(Enumerable.Repeat("▮", d)) + string.Concat(Enumerable.Repeat("▯", 5 - d));

        var row = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        row.Children.Add(Ui.Text(instruction, 12, Pal.TealInk, true));
        var meterLabel = Ui.Text("D" + d + " " + meter, 11, Pal.Ink2, true, TextAlignment.End);
        Grid.SetColumn(meterLabel, 1);
        row.Children.Add(meterLabel);
        body.Children.Add(row);
    }

    void RenderChoice(TaskItem t, List<string> opts, int correct, string? story)
    {
        AddHeader(t);
        if (!string.IsNullOrWhiteSpace(story))
            body.Children.Add(Ui.Card(Ui.Text(story, 14), Pal.Paper2, 12));
        body.Children.Add(Ui.Text(t.Q, 18, Pal.Ink, true));

        var order = Enumerable.Range(0, opts.Count).OrderBy(_ => rnd.Next()).ToList();
        var rows = new Dictionary<int, Border>();
        int selected = -1;
        bool icons = Icons.ForAll(opts);

        foreach (int idx in order)
        {
            int myIdx = idx;
            var row = Ui.Card(Ui.Term(opts[idx], 15, icons), Pal.Paper, 13);
            rows[idx] = row;

            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) =>
            {
                if (isChecked) return;
                _ = row.Press();
                selected = myIdx;
                foreach (var kv in rows)
                    kv.Value.BackgroundColor = kv.Key == selected ? Pal.Amber : Pal.Paper;
                actionBtn.Enabled = true;
            };
            row.GestureRecognizers.Add(tap);
            body.Children.Add(row);
        }

        evaluate = () => selected == correct;
        onChecked = ok =>
        {
            foreach (var kv in rows)
            {
                if (kv.Key == correct)
                {
                    kv.Value.BackgroundColor = Pal.GreenSoft;
                    _ = kv.Value.Bounce();
                }
                else if (kv.Key == selected)
                {
                    kv.Value.BackgroundColor = Pal.RedSoft;
                    _ = kv.Value.Shake();
                }
            }
        };
    }

    // «соедини пары»: свободно подбираешь пары, ничего не подсвечивается красным,
    // проверка — только по общей кнопке «ПРОВЕРИТЬ», как у остальных заданий
    void RenderMatch(TaskItem t)
    {
        AddHeader(t);
        body.Children.Add(Ui.Text(t.Q, 18, Pal.Ink, true));

        int n = t.Pairs.Count;
        var left = new List<(string text, int id)>();
        var right = new List<(string text, int id)>();
        for (int i = 0; i < n; i++)
        {
            left.Add((t.Pairs[i][0], i));
            right.Add((t.Pairs[i][1], i));
        }
        right = right.OrderBy(_ => rnd.Next()).ToList();

        // средняя колонка — место для соединительных линий
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(28)),
                new ColumnDefinition(GridLength.Star)
            }
        };
        var lCol = new VerticalStackLayout { Spacing = 8 };
        var rCol = new VerticalStackLayout { Spacing = 8 };
        Grid.SetColumn(rCol, 2);
        var lines = new PairLines();
        Grid.SetColumnSpan(lines, 3);

        var lRows = new Border[n];
        var rRows = new Border[n];
        var chosen = new int?[n]; // chosen[leftId] = выбранный rightId, пока не нажали «Проверить»
        int selL = -1, selR = -1;

        // выбранная плашка — сиреневая с тёмной обводкой; пара — две плашки одного цвета и линия между ними.
        // Цвет пары — по левой плашке, о верности ответа он ничего не говорит
        static void Paint(Border row, bool selected, int pair)
        {
            if (selected)
            {
                row.BackgroundColor = Pal.Amber;
                row.Stroke = new SolidColorBrush(Pal.Violet);
                row.StrokeThickness = 2;
            }
            else if (pair >= 0)
            {
                var (fill, line) = Pal.Pair(pair);
                row.BackgroundColor = fill;
                row.Stroke = new SolidColorBrush(line);
                row.StrokeThickness = 2;
            }
            else
            {
                row.BackgroundColor = Pal.Paper;
                row.Stroke = new SolidColorBrush(Pal.Line);
                row.StrokeThickness = 1;
            }
        }

        void DrawLines()
        {
            var links = new List<(PointF, PointF, Color)>();
            for (int i = 0; i < n; i++)
            {
                if (chosen[i] is not int r) continue;
                var a = lRows[i].Bounds;
                var b = rRows[r].Bounds;
                if (a.Width <= 0 || b.Width <= 0) continue;
                links.Add((
                    new PointF((float)(lCol.X + a.Right), (float)(lCol.Y + a.Center.Y)),
                    new PointF((float)(rCol.X + b.Left), (float)(rCol.Y + b.Center.Y)),
                    Pal.Pair(i).Line));
            }
            lines.Set(links);
        }

        void Refresh()
        {
            for (int i = 0; i < n; i++)
                Paint(lRows[i], selL == i, chosen[i].HasValue ? i : -1);
            for (int i = 0; i < n; i++)
                Paint(rRows[i], selR == i, Array.IndexOf(chosen, (int?)i));
            DrawLines();
            if (!isChecked) actionBtn.Enabled = chosen.All(x => x.HasValue);
        }

        void TryPair()
        {
            if (selL < 0 || selR < 0) return;
            // одна пара справа привязывается только к одной слева
            for (int i = 0; i < n; i++)
                if (chosen[i] == selR) chosen[i] = null;
            chosen[selL] = selR;
            selL = -1;
            selR = -1;
            Refresh();
        }

        bool icons = Icons.ForAll(left.Select(x => x.text));
        foreach (var item in left)
        {
            int id = item.id;
            var row = Ui.Card(Ui.Term(item.text, 13, icons), Pal.Paper, 10);
            lRows[id] = row;
            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) =>
            {
                if (isChecked) return;
                _ = row.Press();
                selL = selL == id ? -1 : id;
                Refresh();
                TryPair();
            };
            row.GestureRecognizers.Add(tap);
            lCol.Children.Add(row);
        }

        foreach (var item in right)
        {
            int id = item.id;
            var row = Ui.Card(Ui.Text(item.text, 13), Pal.Paper, 10);
            rRows[id] = row;
            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) =>
            {
                if (isChecked) return;
                _ = row.Press();
                selR = selR == id ? -1 : id;
                Refresh();
                TryPair();
            };
            row.GestureRecognizers.Add(tap);
            rCol.Children.Add(row);
        }

        // текст в плашках переносится, высота меняется — линии перерисовываются по новой раскладке
        lCol.SizeChanged += (s, e) => DrawLines();
        rCol.SizeChanged += (s, e) => DrawLines();
        grid.Children.Add(lines);
        grid.Children.Add(lCol);
        grid.Children.Add(rCol);
        body.Children.Add(grid);
        body.Children.Add(Ui.Text("Нажми слева, потом справа. Соединишь все — жми «Проверить».", 11, Pal.Ink2));

        evaluate = () =>
        {
            for (int i = 0; i < n; i++)
                if (chosen[i] != i) return false;
            return true;
        };
    }

    void RenderOrder(TaskItem t)
    {
        AddHeader(t);
        body.Children.Add(Ui.Text(t.Q, 18, Pal.Ink, true));

        var chosen = new List<string>();
        var bankItems = t.Items.OrderBy(_ => rnd.Next()).ToList();
        var answerBox = new VerticalStackLayout { Spacing = 8 };
        var bankBox = new VerticalStackLayout { Spacing = 8 };

        body.Children.Add(Ui.Text("ТВОЙ ПОРЯДОК:", 11, Pal.Ink2, true));
        body.Children.Add(answerBox);
        body.Children.Add(Ui.Text("ВАРИАНТЫ:", 11, Pal.Ink2, true));
        body.Children.Add(bankBox);

        void Redraw()
        {
            answerBox.Children.Clear();
            bankBox.Children.Clear();

            for (int i = 0; i < chosen.Count; i++)
            {
                string item = chosen[i];
                var row = Ui.Card(Ui.Text($"{i + 1}. {item}", 14), Pal.Amber, 11);
                var tap = new TapGestureRecognizer();
                tap.Tapped += (s, e) =>
                {
                    if (isChecked) return;
                    chosen.Remove(item);
                    Redraw();
                };
                row.GestureRecognizers.Add(tap);
                answerBox.Children.Add(row);
            }
            if (chosen.Count == 0)
                answerBox.Children.Add(Ui.Text("(нажимай на варианты ниже)", 12, Pal.Ink2));

            foreach (string item in bankItems.Where(x => !chosen.Contains(x)).ToList())
            {
                string it = item;
                var row = Ui.Card(Ui.Text(it, 14), Pal.Paper, 11);
                var tap = new TapGestureRecognizer();
                tap.Tapped += (s, e) =>
                {
                    if (isChecked) return;
                    chosen.Add(it);
                    Redraw();
                };
                row.GestureRecognizers.Add(tap);
                bankBox.Children.Add(row);
            }

            if (!isChecked) actionBtn.Enabled = chosen.Count == t.Items.Count;
        }

        Redraw();
        evaluate = () => chosen.SequenceEqual(t.Items);
    }

    // «введи ответ»: ученик печатает термин или пропущенное слово; варианты в Options — допустимые ответы
    void RenderInput(TaskItem t)
    {
        AddHeader(t);
        body.Children.Add(Ui.Text(t.Q, 18, Pal.Ink, true));

        var entry = new Entry
        {
            Placeholder = "ОТВЕТ...",
            PlaceholderColor = Pal.Ink2,
            FontFamily = "Body",
            FontSize = 16 * Ui.Scale,
            TextColor = Pal.Ink,
            BackgroundColor = Colors.Transparent,
            // без автозамены: подсказки клавиатуры выдавали бы ответ
            Keyboard = Keyboard.Plain,
            ReturnType = ReturnType.Done
        };
        var box = Ui.Card(entry, Pal.Paper2, 6);
        body.Children.Add(box);
        body.Children.Add(Ui.Text("Регистр и «ё» не важны, мелкую опечатку засчитаем.", 11, Pal.Ink2));

        entry.TextChanged += (s, e) =>
        {
            if (!isChecked) actionBtn.Enabled = !string.IsNullOrWhiteSpace(e.NewTextValue);
        };
        entry.Completed += (s, e) =>
        {
            if (!isChecked && !string.IsNullOrWhiteSpace(entry.Text)) DoCheck(null);
        };
        entry.Loaded += (s, e) => entry.Focus();

        evaluate = () => Practice.CheckInput(entry.Text ?? "", t.Options, out typo);
        onChecked = ok =>
        {
            entry.IsEnabled = false;
            entry.Unfocus();
            box.BackgroundColor = ok ? Pal.GreenSoft : Pal.RedSoft;
            _ = ok ? box.Bounce() : box.Shake();
        };
    }

    // кейс с ветвлением: шаг за шагом, следующий шаг зависит от выбора; верно — если все выборы хорошие
    void RenderBranch(TaskItem t)
    {
        AddHeader(t);
        if (!string.IsNullOrWhiteSpace(t.Story))
            body.Children.Add(Ui.Card(Ui.Text(t.Story, 14), Pal.Paper2, 12));
        body.Children.Add(Ui.Text(t.Q, 18, Pal.Ink, true));

        var steps = t.Steps ?? new List<CaseStep>();
        bool allGood = true;
        int shown = 0;

        void ShowStep(int i)
        {
            var step = steps[i];
            shown++;
            var box = new VerticalStackLayout { Spacing = 8 };
            if (!string.IsNullOrWhiteSpace(step.Text))
                box.Children.Add(Ui.Text($"ШАГ {shown}. {step.Text}", 15, Pal.Ink, true));

            var rows = new List<Border>();
            bool picked = false;
            foreach (var c in step.Choices.OrderBy(_ => rnd.Next()))
            {
                var choice = c;
                var row = Ui.Card(Ui.Text(choice.Text, 14), Pal.Paper, 12);
                rows.Add(row);
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (s, e) =>
                {
                    if (picked || isChecked) return;
                    picked = true;
                    _ = row.Press();
                    if (!choice.Good) allGood = false;
                    row.BackgroundColor = choice.Good ? Pal.GreenSoft : Pal.RedSoft;
                    foreach (var r in rows.Where(r => r != row)) r.Opacity = 0.45;
                    if (!string.IsNullOrWhiteSpace(choice.Result))
                        box.Children.Add(Ui.Card(Ui.Text("→ " + choice.Result, 13), choice.Good ? Pal.GreenSoft : Pal.RedSoft, 10));

                    int next = choice.Next - 1;
                    // защита от кругового кейса: шагов не больше, чем вдвое против их числа
                    if (next >= 0 && next < steps.Count && shown < steps.Count * 2)
                    {
                        Sound.Play("tap");
                        await Task.Delay(350);
                        ShowStep(next);
                    }
                    else DoCheck(allGood);
                };
                row.GestureRecognizers.Add(tap);
                box.Children.Add(row);
            }
            body.Children.Add(box);
            if (shown > 1)
            {
                _ = box.Appear(0);
                Dispatcher.Dispatch(async () => await scroll.ScrollToAsync(box, ScrollToPosition.Start, !Anim.Reduced));
            }
        }

        // кейс без шагов редактор сохранить не даст; на всякий случай не оставляем урок без кнопки
        if (steps.Count > 0) ShowStep(0);
        else Dispatcher.Dispatch(() => DoCheck(true));
        evaluate = () => allGood;
    }

    async Task Finish()
    {
        progress.Value = 1;
        xpGained += 10 + (mistakes == 0 ? 10 : 0);

        var p = AppState.P;
        AppState.AddXp(xpGained);
        AppState.RegisterActivity();
        if (lesson != null)
        {
            if (!p.Done.Contains(lesson.Id)) p.Done.Add(lesson.Id);
            if (mistakes == 0) p.PerfectLessons++;
        }
        var newAch = AppState.CheckAchievements();
        AppState.Save();

        int levelNow = AppState.Level;
        int accuracy = total == 0 ? 100 : (int)Math.Round(firstTryCorrect * 100.0 / total);
        int ratingDelta = (int)Math.Round(p.Rating - ratingStart);

        // повтор ошибок в статистику уроков не идёт
        if (lesson != null) Sync.Lesson(lesson.Id, accuracy, mistakes, xpGained);
        Sound.Play("win");

        var stack = new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 10, 18, 30) };
        // Карточка на экране; для «Поделиться» отдельно рисуется PNG по макету.
        var card = new VerticalStackLayout { Spacing = 14, Padding = new Thickness(0, 0, 0, 6), BackgroundColor = Pal.Paper };
        card.Children.Add(new Heading(lesson != null ? "УРОК ПРОЙДЕН" : "ПОВТОР ПРОЙДЕН", 26));
        card.Children.Add(Ui.Text("▶ " + title.ToUpperInvariant(), 12, Pal.Ink2, true));
        card.Children.Add(mistakes == 0 ? Ui.Mood("love", 170) : Ui.Mascot(170, idle: true));

        var stats = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };
        // числа в итогах «набегают» от нуля
        View Stat(string label, int value, Func<int, string> format, Color fill, int col)
        {
            var box = new VerticalStackLayout { Spacing = 2 };
            box.Children.Add(Ui.Text(label, 10, Pal.Ink, true, TextAlignment.Center));
            var number = Ui.Text("", 20, Pal.Ink, true, TextAlignment.Center);
            Anim.CountUp(number, value, format);
            box.Children.Add(number);
            var card = Ui.Box(box, fill, 10);
            Grid.SetColumn(card, col);
            return card;
        }
        stats.Children.Add(Stat("XP", xpGained, v => "+" + v, Pal.Amber, 0));
        stats.Children.Add(Stat("ТОЧНОСТЬ", accuracy, v => v + "%", Pal.Paper2, 1));
        stats.Children.Add(Stat("РЕЙТИНГ", ratingDelta, v => (ratingDelta >= 0 ? "+" : "") + v, Pal.Paper2, 2));
        card.Children.Add(stats);
        card.Children.Add(Ui.Text("ПСИХЕЯ // ТРЕНАЖЁР ПСИХОЛОГА", 10, Pal.Ink2, true, TextAlignment.Center));
        stack.Children.Add(card);

        if (lesson == null)
        {
            int left = Practice.MistakeCount;
            stack.Children.Add(Ui.Card(Ui.Text(left == 0
                ? "Все ошибки исправлены. Новые появятся, если ошибёшься в уроке."
                : $"Осталось заданий с ошибками: {left}. Верный ответ с первой попытки убирает задание из списка.", 13), Pal.Paper2, 12));
        }

        if (levelNow > levelStart)
        {
            var lv = AppState.LevelInfoOf(levelNow);
            var box = new VerticalStackLayout { Spacing = 4 };
            box.Children.Add(Ui.Text("НОВЫЙ УРОВЕНЬ!", 12, Pal.RedInk, true, TextAlignment.Center));
            box.Children.Add(Ui.Text($"LV {levelNow}  {lv.Name.ToUpperInvariant()}", 20, Pal.Ink, true, TextAlignment.Center));
            var opened = AppState.Rating.SectionUnlock
                .Where(u => u.UnlockLevel > levelStart && u.UnlockLevel <= levelNow)
                .Select(u => "Открыт раздел " + AppState.SectionNumber(u.Section) + ": " + u.Title)
                .ToList();
            foreach (var o in opened)
                box.Children.Add(Ui.Text("▸ " + o, 12, Pal.Ink, false, TextAlignment.Center));
            stack.Children.Add(Ui.Box(box, Pal.Amber, 12));
        }

        if (newAch.Count > 0)
            stack.Children.Add(Ui.Bullets("НОВЫЕ ДОСТИЖЕНИЯ ★", newAch));

        var done = new Btn("ПРОДОЛЖИТЬ ▶", Pal.GreenInk, Pal.Paper);
        done.Clicked += async (s, e) => await Pop();
        stack.Children.Add(done);

        stack.Children.Add(Form.Link("ПОДЕЛИТЬСЯ РЕЗУЛЬТАТОМ", Pal.TealInk, async () =>
        {
            if (!await ShareCard.ShareLesson(new LessonShareResult(xpGained, accuracy, ratingDelta, lesson == null), "Психея: " + title))
                await Dlg.Alert(this, "НЕ ВЫШЛО", "Не удалось сделать картинку. Попробуй ещё раз.");
        }, TextAlignment.Center));

        await SwapBody(new ScrollView { Content = stack });
        Confetti();
    }

    void Confetti()
    {
        var layer = new AbsoluteLayout { InputTransparent = true };
        Root.Children.Add(layer);

        double w = Root.Width > 0 ? Root.Width : 360;
        double h = Root.Height > 0 ? Root.Height : 700;
        var cols = new[] { Pal.Amber, Pal.Orange, Pal.Red, Pal.Teal, Pal.Cyan };

        for (int i = 0; i < 40; i++)
        {
            double bw = 8 + rnd.Next(6);
            double bh = 8 + rnd.Next(10);
            var piece = new BoxView
            {
                Color = cols[rnd.Next(cols.Length)],
                WidthRequest = bw,
                HeightRequest = bh,
                Rotation = rnd.Next(360)
            };
            AbsoluteLayout.SetLayoutBounds(piece, new Rect(rnd.NextDouble() * w, -20, bw, bh));
            AbsoluteLayout.SetLayoutFlags(piece, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.None);
            layer.Children.Add(piece);

            uint duration = (uint)(1400 + rnd.Next(1800));
            _ = piece.Move(rnd.Next(-40, 40), h + 60, duration, Easing.CubicIn);
            _ = piece.Spin(rnd.Next(180, 720), duration);
        }
    }
}
