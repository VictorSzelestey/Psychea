using System.Text.Json;
using PsyPath.Models;
using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

// задание урока: тип, сложность, вопрос, ответ в виде, удобном для этого типа
public class AdminTaskPage : AdminFormPage
{
    readonly Lesson lesson;
    readonly TaskItem task;

    public AdminTaskPage(Lesson lesson, TaskItem task)
    {
        this.lesson = lesson;
        this.task = task;
    }

    bool IsOptions => task.Type is "choice" or "fill" or "case";
    int Answer => task.Answer.ValueKind == JsonValueKind.Number ? task.Answer.GetInt32() : -1;
    void SetAnswer(int i) => task.Answer = JsonSerializer.SerializeToElement(i);

    protected override void Build()
    {
        int n = lesson.Tasks.IndexOf(task) + 1;
        var s = List();

        s.Children.Add(Form.Choice("ТИП ЗАДАНИЯ", AdminLessonPage.Types, task.Type, v =>
        {
            task.Type = v;
            Prepare();
            Rebuild();
        }));

        var diffs = Enumerable.Range(1, 5).Select(d =>
        {
            string name = AppState.Rating.DifficultyScale.FirstOrDefault(x => x.D == d)?.Name ?? "";
            return (d.ToString(), $"D{d} {name}".Trim().ToUpperInvariant());
        }).ToList();
        s.Children.Add(Form.Choice("СЛОЖНОСТЬ", diffs, task.Difficulty.ToString(), v => task.Difficulty = int.Parse(v)));

        if (task.Type is "case" or "branch")
            s.Children.Add(Form.Field("СИТУАЦИЯ", task.Story, v => task.Story = v, multiline: true, hint: "Короткая история, по которой задаётся вопрос."));

        string? qHint = task.Type switch
        {
            "fill" => "Место пропуска обозначь тремя подчёркиваниями: ___",
            "input" => "Можно с пропуском ___ — тогда ученик впишет пропущенное слово.",
            "branch" => "Общий вопрос кейса, например «Как поступить психологу?»",
            _ => null
        };
        s.Children.Add(Form.Field($"ВОПРОС {n}", task.Q, v => task.Q = v, multiline: true, hint: qHint));

        if (IsOptions) BuildOptions(s);
        else if (task.Type == "truefalse") BuildTrueFalse(s);
        else if (task.Type == "match") BuildPairs(s);
        else if (task.Type == "order") BuildOrder(s);
        else if (task.Type == "input")
            s.Children.Add(Form.Lines("ДОПУСТИМЫЕ ОТВЕТЫ", task.Options,
                "Каждый ответ с новой строки. Первый — основной, его покажем при ошибке. Регистр, «ё» и мелкие опечатки проверка прощает сама."));
        else if (task.Type == "branch") BuildSteps(s);

        s.Children.Add(Form.Field("ОБЪЯСНЕНИЕ", task.Explain, v => task.Explain = v, multiline: true,
            hint: "Показывается после ответа, и верного, и неверного."));

        var auto = string.IsNullOrWhiteSpace(task.Ref) ? Practice.FindRef(task) : null;
        s.Children.Add(Form.Field("СТАТЬЯ СПРАВОЧНИКА", task.Ref, v => task.Ref = v, hint:
            "Термин из справочника для кнопки «В справочник» после ответа. Пусто — найдётся сам по тексту задания" +
            (auto != null ? $" (сейчас: «{auto.Term}»)." : " (сейчас не находится).")));

        s.Children.Add(Form.Link("УДАЛИТЬ ЗАДАНИЕ", Pal.RedInk, async () =>
        {
            if (!await Dlg.Confirm(this, "УДАЛИТЬ?", $"Задание {n} будет удалено.", "УДАЛИТЬ", "ОТМЕНА"))
                return;
            lesson.Tasks.Remove(task);
            await Pop();
        }, TextAlignment.Center));

        var bottom = new VerticalStackLayout { Padding = new Thickness(16, 8, 16, 12), BackgroundColor = Pal.Paper };
        var ok = new Btn("ГОТОВО ◀", Pal.Amber);
        ok.Clicked += async (o, e) => await Leave();
        bottom.Children.Add(ok);

        Show("ЗАДАНИЕ", s, bottom);
    }

    // после смены типа готовим поля, без которых новый тип не заполнить
    void Prepare()
    {
        if (IsOptions)
        {
            while (task.Options.Count < 2) task.Options.Add("");
            if (Answer < 0 || Answer >= task.Options.Count) SetAnswer(0);
        }
        else if (task.Type == "truefalse" && task.Answer.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            task.Answer = JsonSerializer.SerializeToElement(true);
        }
        else if (task.Type == "match")
        {
            while (task.Pairs.Count < 2) task.Pairs.Add(new List<string> { "", "" });
        }
        else if (task.Type == "order")
        {
            while (task.Items.Count < 2) task.Items.Add("");
        }
        else if (task.Type == "branch" && (task.Steps == null || task.Steps.Count == 0))
        {
            task.Steps = new List<CaseStep> { NewStep() };
        }
    }

    static CaseStep NewStep() => new()
    {
        Choices = { new CaseChoice { Good = true }, new CaseChoice() }
    };

    // шаги кейса: ситуация шага и варианты; у варианта — что случилось, хороший ли он и куда ведёт
    void BuildSteps(VerticalStackLayout s)
    {
        var steps = task.Steps ??= new List<CaseStep>();
        s.Children.Add(Form.Section($"ШАГИ КЕЙСА ({steps.Count})"));
        s.Children.Add(Form.Hint("Кейс начинается с шага 1. Вариант ведёт на другой шаг или заканчивает кейс. " +
            "Ответ засчитан, если ученик ни разу не выбрал плохой вариант."));

        var targets = new List<(string Value, string Caption)> { ("0", "КОНЕЦ") };
        targets.AddRange(Enumerable.Range(1, steps.Count).Select(i => (i.ToString(), $"→ ШАГ {i}")));

        for (int si = 0; si < steps.Count; si++)
        {
            int sIdx = si;
            var step = steps[si];
            var box = new VerticalStackLayout { Spacing = 8 };
            box.Children.Add(Ui.Text($"ШАГ {si + 1}", 14, Pal.RedInk, true));
            box.Children.Add(Form.Field("ЧТО ПРОИСХОДИТ", step.Text, v => step.Text = v, multiline: true,
                hint: si == 0 ? "У первого шага можно оставить пустым, если всё сказано в ситуации." : null));

            for (int ci = 0; ci < step.Choices.Count; ci++)
            {
                int cIdx = ci;
                var c = step.Choices[ci];
                var cbox = new VerticalStackLayout { Spacing = 4 };
                cbox.Children.Add(Form.Field($"ВАРИАНТ {ci + 1}", c.Text, v => c.Text = v, multiline: true));
                cbox.Children.Add(Form.Field("ЧТО ПОСЛЕ ЭТОГО", c.Result, v => c.Result = v, multiline: true,
                    hint: "Последствие выбора, ученик увидит его сразу."));
                cbox.Children.Add(Form.Choice("ОЦЕНКА", new[] { ("1", "ХОРОШИЙ"), ("0", "ПЛОХОЙ") }, c.Good ? "1" : "0", v =>
                {
                    c.Good = v == "1";
                    Rebuild();
                }));
                cbox.Children.Add(Form.Choice("ДАЛЬШЕ", targets, c.Next.ToString(), v => c.Next = int.Parse(v)));
                cbox.Children.Add(Form.Link("✕ УДАЛИТЬ ВАРИАНТ", Pal.RedInk, () =>
                {
                    step.Choices.RemoveAt(cIdx);
                    Rebuild();
                }));
                box.Children.Add(Ui.Card(cbox, c.Good ? Pal.GreenSoft : Pal.RedSoft, 10));
            }

            var addChoice = new Btn("+ ВАРИАНТ", Pal.Paper2, null, 14);
            addChoice.Clicked += (o, e) =>
            {
                step.Choices.Add(new CaseChoice());
                Rebuild();
            };
            box.Children.Add(addChoice);

            if (steps.Count > 1)
                box.Children.Add(Form.Link("УДАЛИТЬ ШАГ", Pal.RedInk, async () =>
                {
                    if (!await Dlg.Confirm(this, "УДАЛИТЬ ШАГ?", $"Шаг {sIdx + 1} будет удалён, переходы на него станут «конец».", "УДАЛИТЬ", "ОТМЕНА"))
                        return;
                    RemoveStep(sIdx);
                    Rebuild();
                }));
            s.Children.Add(Ui.Card(box, Pal.Paper, 10));
        }

        var addStep = new Btn("+ ШАГ", Pal.Paper2, null, 14);
        addStep.Clicked += (o, e) =>
        {
            steps.Add(NewStep());
            Rebuild();
        };
        s.Children.Add(addStep);
    }

    // номера шагов сдвигаются: переходы на удалённый шаг — «конец», на следующие — на один меньше
    void RemoveStep(int idx)
    {
        var steps = task.Steps!;
        steps.RemoveAt(idx);
        int removed = idx + 1;
        foreach (var c in steps.SelectMany(x => x.Choices))
        {
            if (c.Next == removed) c.Next = 0;
            else if (c.Next > removed) c.Next--;
        }
    }

    void BuildOptions(VerticalStackLayout s)
    {
        s.Children.Add(Form.Section($"ВАРИАНТЫ ({task.Options.Count})"));
        s.Children.Add(Form.Hint("Отметь один верный. В уроке варианты перемешиваются."));
        for (int i = 0; i < task.Options.Count; i++)
        {
            int idx = i;
            bool correct = Answer == i;
            var box = new VerticalStackLayout { Spacing = 4 };
            box.Children.Add(Form.Field($"ВАРИАНТ {i + 1}", task.Options[i], v => task.Options[idx] = v, multiline: true));

            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            row.Children.Add(correct
                ? Ui.Text("✓ ВЕРНЫЙ ОТВЕТ", 12, Pal.GreenInk, true)
                : Form.Link("ОТМЕТИТЬ ВЕРНЫМ", Pal.TealInk, () =>
                {
                    SetAnswer(idx);
                    Rebuild();
                }));
            var remove = Form.Link("✕", Pal.RedInk, () =>
            {
                task.Options.RemoveAt(idx);
                int a = Answer;
                if (a == idx) SetAnswer(0);
                else if (a > idx) SetAnswer(a - 1);
                Rebuild();
            });
            Grid.SetColumn(remove, 1);
            row.Children.Add(remove);
            box.Children.Add(row);

            s.Children.Add(Ui.Card(box, correct ? Pal.GreenSoft : Pal.Paper, 10));
        }
        var add = new Btn("+ ВАРИАНТ", Pal.Paper2, null, 14);
        add.Clicked += (o, e) =>
        {
            task.Options.Add("");
            Rebuild();
        };
        s.Children.Add(add);
    }

    void BuildTrueFalse(VerticalStackLayout s)
    {
        string current = task.Answer.ValueKind == JsonValueKind.False ? "false" : "true";
        s.Children.Add(Form.Choice("ПРАВИЛЬНЫЙ ОТВЕТ", new[] { ("true", "ВЕРНО"), ("false", "НЕВЕРНО") }, current,
            v => task.Answer = JsonSerializer.SerializeToElement(v == "true")));
    }

    void BuildPairs(VerticalStackLayout s)
    {
        s.Children.Add(Form.Section($"ПАРЫ ({task.Pairs.Count})"));
        s.Children.Add(Form.Hint("Слева — понятие, справа — то, с чем его соединить. Правая колонка в уроке перемешивается."));
        for (int i = 0; i < task.Pairs.Count; i++)
        {
            int idx = i;
            var pair = task.Pairs[i];
            while (pair.Count < 2) pair.Add("");
            var box = new VerticalStackLayout { Spacing = 4 };
            box.Children.Add(Form.Field($"ПАРА {i + 1}: СЛЕВА", pair[0], v => pair[0] = v));
            box.Children.Add(Form.Field("СПРАВА", pair[1], v => pair[1] = v));
            box.Children.Add(Form.Link("✕ УДАЛИТЬ ПАРУ", Pal.RedInk, () =>
            {
                task.Pairs.RemoveAt(idx);
                Rebuild();
            }));
            s.Children.Add(Ui.Card(box, Pal.Paper, 10));
        }
        var add = new Btn("+ ПАРА", Pal.Paper2, null, 14);
        add.Clicked += (o, e) =>
        {
            task.Pairs.Add(new List<string> { "", "" });
            Rebuild();
        };
        s.Children.Add(add);
    }

    void BuildOrder(VerticalStackLayout s)
    {
        s.Children.Add(Form.Section($"ЭЛЕМЕНТЫ В ПРАВИЛЬНОМ ПОРЯДКЕ ({task.Items.Count})"));
        s.Children.Add(Form.Hint("В уроке они перемешиваются, ученик расставляет их сам."));
        for (int i = 0; i < task.Items.Count; i++)
        {
            int idx = i;
            var row = new Grid
            {
                ColumnSpacing = 2,
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                }
            };
            row.Children.Add(Form.Field($"{i + 1}", task.Items[i], v => task.Items[idx] = v));
            void Add(View v, int col)
            {
                v.VerticalOptions = LayoutOptions.End;
                Grid.SetColumn(v, col);
                row.Children.Add(v);
            }
            Add(Form.Link("▲", i > 0 ? Pal.Ink : Pal.Gray, () => MoveItem(idx, idx - 1)), 1);
            Add(Form.Link("▼", i < task.Items.Count - 1 ? Pal.Ink : Pal.Gray, () => MoveItem(idx, idx + 1)), 2);
            Add(Form.Link("✕", Pal.RedInk, () =>
            {
                task.Items.RemoveAt(idx);
                Rebuild();
            }), 3);
            s.Children.Add(row);
        }
        var add = new Btn("+ ЭЛЕМЕНТ", Pal.Paper2, null, 14);
        add.Clicked += (o, e) =>
        {
            task.Items.Add("");
            Rebuild();
        };
        s.Children.Add(add);
    }

    void MoveItem(int from, int to)
    {
        if (to < 0 || to >= task.Items.Count) return;
        (task.Items[from], task.Items[to]) = (task.Items[to], task.Items[from]);
        Rebuild();
    }

    protected override async Task Leave()
    {
        if (!await ConfirmLeaveWithErrors()) return;
        await Pop();
    }
}
