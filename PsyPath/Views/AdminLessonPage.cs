using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using PsyPath.Models;
using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

// визуальный редактор урока: название, уровень, список заданий.
// Правки идут в копию урока и уходят на сервер только по «СОХРАНИТЬ»
public class AdminLessonPage : AdminFormPage
{
    // тот же вид JSON, что в lessons.json
    static readonly JsonSerializerOptions Out = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static readonly (string Value, string Caption)[] Types =
    {
        ("choice", "ВЫБОР ОТВЕТА"),
        ("fill", "ЗАПОЛНИ ПРОПУСК"),
        ("truefalse", "ВЕРНО / НЕВЕРНО"),
        ("case", "МИНИ-КЕЙС"),
        ("match", "СОЕДИНИ ПАРЫ"),
        ("order", "ПО ПОРЯДКУ"),
        ("input", "ВВЕДИ ОТВЕТ"),
        ("branch", "КЕЙС С ВЕТВЛЕНИЕМ")
    };

    readonly Lesson lesson;
    readonly string sectionTitle;
    readonly bool isNew;
    readonly IReadOnlyCollection<string> otherIds;
    readonly Func<string, Task<string?>> save;
    readonly Func<Task<string?>>? delete;
    readonly string original;
    Label status = Ui.Text("", 12, Pal.RedInk, true);
    bool busy;
    bool done;

    public AdminLessonPage(Lesson lesson, string sectionTitle, bool isNew, IReadOnlyCollection<string> otherIds,
        Func<string, Task<string?>> save, Func<Task<string?>>? delete)
    {
        this.lesson = lesson;
        this.sectionTitle = sectionTitle;
        this.isNew = isNew;
        this.otherIds = otherIds;
        this.save = save;
        this.delete = delete;
        original = ToJson();
    }

    string ToJson()
    {
        // у «пар» и «порядка» ответа нет; пустой JsonElement сериализатор записать не может
        foreach (var t in lesson.Tasks.Where(t => t.Answer.ValueKind == JsonValueKind.Undefined))
            t.Answer = JsonSerializer.SerializeToElement<object?>(null);
        return JsonSerializer.Serialize(lesson, Out);
    }

    static string TheoryToText(List<TheoryCard>? cards) =>
        string.Join("\n\n", (cards ?? new()).Select(c => c.Title + "\n" + c.Text));

    // пустой текст — у урока нет теории, в JSON поле не пишется
    static List<TheoryCard>? TheoryFromText(string text)
    {
        var cards = Regex.Split(text.Replace("\r", ""), @"\n\s*\n")
            .Select(block => block.Trim())
            .Where(block => block.Length > 0)
            .Select(block =>
            {
                var lines = block.Split('\n').Select(x => x.Trim()).ToList();
                return new TheoryCard { Title = lines[0], Text = string.Join(" ", lines.Skip(1)) };
            })
            .ToList();
        return cards.Count > 0 ? cards : null;
    }

    public static string TypeName(string type) =>
        Types.FirstOrDefault(t => t.Value == type).Caption ?? type;

    protected override void Build()
    {
        var s = List();
        s.Children.Add(Form.Hint("ЮНИТ: " + sectionTitle));
        s.Children.Add(Form.Field("НАЗВАНИЕ", lesson.Title, v => lesson.Title = v));
        if (isNew)
            s.Children.Add(Form.Field("ID", lesson.Id, v => lesson.Id = v.Trim(),
                hint: "Например 2-7: номер юнита и урока. По нему считается прогресс и статистика, после сохранения не меняется."));
        else
            s.Children.Add(Form.Hint("ID: " + lesson.Id));

        var levels = Enumerable.Range(1, 5)
            .Select(l => (l.ToString(), $"{l} · {AppState.LevelInfoOf(l).Name}".ToUpperInvariant()))
            .ToList();
        s.Children.Add(Form.Choice("РЕКОМЕНДУЕМЫЙ УРОВЕНЬ", levels, lesson.RecommendedLevel.ToString(),
            v => lesson.RecommendedLevel = int.Parse(v)));
        s.Children.Add(Form.Hint("Ученик на 2 уровня выше рекомендуемого считается освоившим урок без прохождения."));

        s.Children.Add(Form.Section($"ТЕОРИЯ ({lesson.Theory?.Count ?? 0})"));
        s.Children.Add(Form.Field("КАРТОЧКИ", TheoryToText(lesson.Theory), v => lesson.Theory = TheoryFromText(v), multiline: true,
            hint: "Карточки разделяй пустой строкой. Первая строка карточки — заголовок, дальше текст. Ученик видит их перед заданиями."));

        s.Children.Add(Form.Section($"ЗАДАНИЯ ({lesson.Tasks.Count})"));
        s.Children.Add(Form.Hint("За один заход ученик получает до 8 заданий, подобранных под его уровень. Ошибки повторяются в конце."));
        for (int i = 0; i < lesson.Tasks.Count; i++)
            s.Children.Add(TaskRow(i));

        var add = new Btn("+ ЗАДАНИЕ", Pal.Paper2, null, 14);
        add.Clicked += async (o, e) =>
        {
            var t = new TaskItem
            {
                Type = "choice",
                Difficulty = Math.Clamp(lesson.RecommendedLevel, 1, 5),
                Options = { "", "" },
                Answer = JsonSerializer.SerializeToElement(0)
            };
            lesson.Tasks.Add(t);
            await Push(new AdminTaskPage(lesson, t));
        };
        s.Children.Add(add);

        var bottom = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(16, 8, 16, 12), BackgroundColor = Pal.Paper };
        status = Ui.Text(status.Text, 12, status.TextColor, true);
        bottom.Children.Add(status);
        var saveBtn = new Btn("СОХРАНИТЬ ✓", Pal.GreenInk, Pal.Paper);
        saveBtn.Clicked += async (o, e) => await DoSave();
        bottom.Children.Add(saveBtn);
        if (delete != null)
            bottom.Children.Add(Form.Link("УДАЛИТЬ УРОК", Pal.RedInk, async () => await DoDelete(), TextAlignment.Center));

        Show(isNew ? "НОВЫЙ УРОК" : "УРОК", s, bottom);
    }

    View TaskRow(int i)
    {
        var t = lesson.Tasks[i];
        var box = new VerticalStackLayout { Spacing = 2 };
        box.Children.Add(Ui.Text($"{TypeName(t.Type)}  //  D{t.Difficulty}", 10, Pal.TealInk, true));
        box.Children.Add(Ui.Text($"{i + 1}. {(string.IsNullOrWhiteSpace(t.Q) ? "(без вопроса)" : t.Q)}", 13, Pal.Ink, true));
        var card = Ui.Card(box, Pal.Paper, 10);
        Ui.OnTap(card, async () => await Push(new AdminTaskPage(lesson, t)));

        var row = new Grid
        {
            ColumnSpacing = 4,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        row.Children.Add(card);
        var up = Form.Link("▲", i > 0 ? Pal.Ink : Pal.Gray, () => Move(i, i - 1));
        up.VerticalOptions = LayoutOptions.Center;
        Grid.SetColumn(up, 1);
        row.Children.Add(up);
        var down = Form.Link("▼", i < lesson.Tasks.Count - 1 ? Pal.Ink : Pal.Gray, () => Move(i, i + 1));
        down.VerticalOptions = LayoutOptions.Center;
        Grid.SetColumn(down, 2);
        row.Children.Add(down);
        return row;
    }

    void Move(int from, int to)
    {
        if (to < 0 || to >= lesson.Tasks.Count) return;
        (lesson.Tasks[from], lesson.Tasks[to]) = (lesson.Tasks[to], lesson.Tasks[from]);
        Rebuild();
    }

    List<string> Validate()
    {
        var e = new List<string>();
        if (HasInvalid) e.Add("Исправь поля, подсвеченные красным");
        if (string.IsNullOrWhiteSpace(lesson.Title)) e.Add("Нет названия");
        if (!Regex.IsMatch(lesson.Id ?? "", "^[A-Za-z0-9_-]{1,40}$")) e.Add("ID: латиница, цифры, дефис или _");
        else if (otherIds.Contains(lesson.Id)) e.Add("Такой ID уже есть у другого урока");
        if (lesson.Tasks.Count == 0) e.Add("Нужно хотя бы одно задание");

        for (int i = 0; i < lesson.Tasks.Count; i++)
        {
            var t = lesson.Tasks[i];
            string n = $"Задание {i + 1}";
            if (string.IsNullOrWhiteSpace(t.Q)) e.Add($"{n}: нет вопроса");
            if (!string.IsNullOrWhiteSpace(t.Ref) && Practice.FindRef(t) == null) e.Add($"{n}: в справочнике нет термина «{t.Ref.Trim()}»");
            switch (t.Type)
            {
                case "choice":
                case "fill":
                case "case":
                    if (t.Options.Count < 2) e.Add($"{n}: нужно минимум 2 варианта");
                    if (t.Options.Any(string.IsNullOrWhiteSpace)) e.Add($"{n}: есть пустой вариант");
                    if (t.Answer.ValueKind != JsonValueKind.Number || t.AnswerIndex < 0 || t.AnswerIndex >= t.Options.Count)
                        e.Add($"{n}: не отмечен верный вариант");
                    if (t.Type == "case" && string.IsNullOrWhiteSpace(t.Story)) e.Add($"{n}: нет ситуации для кейса");
                    if (t.Type == "fill" && !t.Q.Contains("___")) e.Add($"{n}: в вопросе нет пропуска ___");
                    break;
                case "truefalse":
                    if (t.Answer.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) e.Add($"{n}: не выбран правильный ответ");
                    break;
                case "match":
                    if (t.Pairs.Count < 2) e.Add($"{n}: нужно минимум 2 пары");
                    if (t.Pairs.Any(p => p.Count < 2 || string.IsNullOrWhiteSpace(p[0]) || string.IsNullOrWhiteSpace(p[1])))
                        e.Add($"{n}: в паре пустая сторона");
                    break;
                case "order":
                    if (t.Items.Count < 2) e.Add($"{n}: нужно минимум 2 элемента");
                    if (t.Items.Any(string.IsNullOrWhiteSpace)) e.Add($"{n}: есть пустой элемент");
                    if (t.Items.Distinct().Count() != t.Items.Count) e.Add($"{n}: элементы порядка не должны повторяться");
                    break;
                case "input":
                    if (t.Options.Count == 0) e.Add($"{n}: нет ни одного допустимого ответа");
                    break;
                case "branch":
                    ValidateBranch(t, n, e);
                    break;
                default:
                    e.Add($"{n}: неизвестный тип {t.Type}");
                    break;
            }
        }
        return e;
    }

    static void ValidateBranch(TaskItem t, string n, List<string> e)
    {
        var steps = t.Steps ?? new List<CaseStep>();
        if (steps.Count == 0)
        {
            e.Add($"{n}: в кейсе нет шагов");
            return;
        }
        for (int s = 0; s < steps.Count; s++)
        {
            var step = steps[s];
            string sn = $"{n}, шаг {s + 1}";
            if (step.Choices.Count < 2) e.Add($"{sn}: нужно минимум 2 варианта");
            if (step.Choices.Any(c => string.IsNullOrWhiteSpace(c.Text))) e.Add($"{sn}: есть пустой вариант");
            if (!step.Choices.Any(c => c.Good)) e.Add($"{sn}: нет ни одного хорошего варианта — кейс нельзя пройти верно");
            if (step.Choices.Any(c => c.Next < 0 || c.Next > steps.Count)) e.Add($"{sn}: переход на несуществующий шаг");
        }
    }

    // поля, которые не нужны выбранному типу, в файл не попадают
    void Clean()
    {
        foreach (var t in lesson.Tasks)
        {
            if (t.Type is not ("case" or "branch")) t.Story = "";
            if (t.Type is "truefalse" or "match" or "order" or "branch") t.Options.Clear();
            if (t.Type == "input")
            {
                var answers = t.Options.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToList();
                t.Options.Clear();
                t.Options.AddRange(answers);
            }
            if (t.Type != "match") t.Pairs.Clear();
            if (t.Type != "order") t.Items.Clear();
            if (t.Type != "branch") t.Steps = null;
            if (string.IsNullOrWhiteSpace(t.Ref)) t.Ref = null;
            if (t.Type is "match" or "order" or "input" or "branch") t.Answer = JsonSerializer.SerializeToElement<object?>(null);
        }
    }

    async Task DoSave()
    {
        if (busy) return;
        var errors = Validate();
        if (errors.Count > 0)
        {
            SetStatus(string.Join("\n", errors.Take(6)) + (errors.Count > 6 ? $"\n…и ещё {errors.Count - 6}" : ""), Pal.RedInk);
            return;
        }
        busy = true;
        SetStatus("Сохраняю…", Pal.Ink2);
        Clean();
        var error = await save(ToJson());
        busy = false;
        if (error != null)
        {
            SetStatus(error, Pal.RedInk);
            return;
        }
        done = true;
        await Pop();
    }

    async Task DoDelete()
    {
        if (busy || delete == null) return;
        if (!await Dlg.Confirm(this, "УДАЛИТЬ?", $"Урок «{lesson.Title}» пропадёт из приложения. Прохождения в статистике останутся.", "УДАЛИТЬ", "ОТМЕНА"))
            return;
        busy = true;
        var error = await delete();
        busy = false;
        if (error != null)
        {
            SetStatus(error, Pal.RedInk);
            return;
        }
        done = true;
        await Pop();
    }

    void SetStatus(string text, Color color)
    {
        status.Text = text;
        status.TextColor = color;
    }

    protected override async Task Leave()
    {
        if (busy) return;
        if (!done && ToJson() != original &&
            !await Dlg.Confirm(this, "ВЫЙТИ?", "Несохранённые правки урока пропадут.", "ВЫЙТИ", "ОСТАТЬСЯ"))
            return;
        await Pop();
    }
}
