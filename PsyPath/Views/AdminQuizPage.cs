using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using PsyPath.Models;
using PsyPath.UI;

namespace PsyPath.Views;

// визуальный редактор опросника: основное, шкалы, вопросы, смешанный тип.
// Правки идут в копию опросника и уходят на сервер только по «СОХРАНИТЬ»
public class AdminQuizPage : AdminFormPage
{
    // тот же вид JSON, что в questionnaires.json
    static readonly JsonSerializerOptions Out = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    readonly Questionnaire quiz;
    readonly bool isNew;
    readonly IReadOnlyCollection<string> otherIds;
    readonly Func<string, Task<string?>> save;
    readonly Func<Task<string?>>? delete;
    readonly string original;
    Label status = Ui.Text("", 12, Pal.RedInk, true);
    bool busy;
    bool done;

    public AdminQuizPage(Questionnaire quiz, bool isNew, IReadOnlyCollection<string> otherIds,
        Func<string, Task<string?>> save, Func<Task<string?>>? delete)
    {
        this.quiz = quiz;
        this.isNew = isNew;
        this.otherIds = otherIds;
        this.save = save;
        this.delete = delete;
        original = ToJson();
    }

    string ToJson() => JsonSerializer.Serialize(quiz, Out);

    protected override void Build()
    {
        var s = List();

        s.Children.Add(Form.Section("ОСНОВНОЕ"));
        s.Children.Add(Form.Field("НАЗВАНИЕ", quiz.Title, v => quiz.Title = v));
        if (isNew)
            s.Children.Add(Form.Field("ID", quiz.Id, v => quiz.Id = v.Trim(),
                hint: "Латиница в нижнем регистре, цифры, дефис. По нему считается статистика, после сохранения не меняется."));
        else
            s.Children.Add(Form.Hint("ID: " + quiz.Id));

        s.Children.Add(Form.Choice("ТИП ОТВЕТОВ",
            new[] { ("choice", "ВАРИАНТЫ ОТВЕТА"), ("yesno", "ДА / НЕТ") }, quiz.Kind, v => quiz.Kind = v));
        s.Children.Add(Form.Hint("Варианты: у каждого варианта своя шкала и баллы. Да/нет: в шкалах указываются номера вопросов, где «да» или «нет» даёт балл."));

        s.Children.Add(Form.Choice("РЕЗУЛЬТАТ",
            new[] { ("dominant", "ГЛАВНЫЙ ТИП"), ("scales", "ВСЕ ШКАЛЫ") }, quiz.Mode, v =>
            {
                quiz.Mode = v;
                Rebuild();
            }));
        s.Children.Add(Form.Hint(quiz.Mode == "dominant"
            ? "Показывается шкала с наибольшим баллом: её описание, сильные стороны и советы."
            : "Показываются все шкалы: баллы и текст диапазона, в который попал результат."));

        s.Children.Add(Form.Field("ОПИСАНИЕ", quiz.About, v => quiz.About = v, multiline: true));
        s.Children.Add(Form.Field("ИСТОЧНИК", quiz.Source, v => quiz.Source = v));
        s.Children.Add(Form.Field("ДИСКЛЕЙМЕР", quiz.Disclaimer, v => quiz.Disclaimer = v, multiline: true));

        s.Children.Add(Form.Section($"ШКАЛЫ ({quiz.Scales.Count})"));
        foreach (var scale in quiz.Scales)
        {
            var box = new VerticalStackLayout { Spacing = 2 };
            box.Children.Add(Ui.Text(scale.Name, 14, Pal.Ink, true));
            box.Children.Add(Ui.Text($"ключ {scale.Key}  //  диапазонов: {scale.Ranges.Count}", 10, Pal.Ink2, true));
            var card = Ui.Card(box, Pal.Paper, 10);
            var sc = scale;
            Ui.OnTap(card, async () => await Push(new AdminScalePage(quiz, sc)));
            s.Children.Add(card);
        }
        var addScale = new Btn("+ ШКАЛА", Pal.Paper2, null, 14);
        addScale.Clicked += async (o, e) =>
        {
            var sc = new QScale { Key = FreeKey(), Name = "Новая шкала" };
            quiz.Scales.Add(sc);
            await Push(new AdminScalePage(quiz, sc));
        };
        s.Children.Add(addScale);

        s.Children.Add(Form.Section($"ВОПРОСЫ ({quiz.Questions.Count})"));
        for (int i = 0; i < quiz.Questions.Count; i++)
            s.Children.Add(QuestionRow(i));
        var addQuestion = new Btn("+ ВОПРОС", Pal.Paper2, null, 14);
        addQuestion.Clicked += async (o, e) =>
        {
            var q = new QQuestion();
            if (!quiz.IsYesNo)
            {
                string key = quiz.Scales.FirstOrDefault()?.Key ?? "";
                q.Options.Add(new QOption { Scale = key, Points = 1 });
                q.Options.Add(new QOption { Scale = key, Points = 1 });
            }
            quiz.Questions.Add(q);
            await Push(new AdminQuestionPage(quiz, q));
        };
        s.Children.Add(addQuestion);

        if (quiz.Mode == "dominant" && quiz.Scales.Count >= 2)
        {
            s.Children.Add(Form.Section("СМЕШАННЫЙ ТИП"));
            s.Children.Add(Form.Hint("Показывается, если вторая шкала набрала не меньше 80% от главной."));
            s.Children.Add(Form.Field("ШАБЛОН", quiz.BlendTemplate, v => quiz.BlendTemplate = v, multiline: true,
                hint: "{main} заменится на главную шкалу, {secondary} — на вторую."));
            for (int a = 0; a < quiz.Scales.Count; a++)
                for (int b = a + 1; b < quiz.Scales.Count; b++)
                {
                    string key = quiz.Scales[a].Key + "+" + quiz.Scales[b].Key;
                    s.Children.Add(Form.Field($"{quiz.Scales[a].Name} + {quiz.Scales[b].Name}",
                        quiz.BlendTexts.GetValueOrDefault(key, ""), v =>
                        {
                            if (string.IsNullOrWhiteSpace(v)) quiz.BlendTexts.Remove(key);
                            else quiz.BlendTexts[key] = v;
                        }, multiline: true));
                }
        }

        var bottom = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(16, 8, 16, 12), BackgroundColor = Pal.Paper };
        status = Ui.Text(status.Text, 12, status.TextColor, true);
        bottom.Children.Add(status);
        var saveBtn = new Btn("СОХРАНИТЬ ✓", Pal.GreenInk, Pal.Paper);
        saveBtn.Clicked += async (o, e) => await DoSave();
        bottom.Children.Add(saveBtn);
        if (delete != null)
            bottom.Children.Add(Form.Link("УДАЛИТЬ ОПРОСНИК", Pal.RedInk, async () => await DoDelete(), TextAlignment.Center));

        Show(isNew ? "НОВЫЙ ОПРОСНИК" : "ОПРОСНИК", s, bottom);
    }

    View QuestionRow(int i)
    {
        var q = quiz.Questions[i];
        string text = string.IsNullOrWhiteSpace(q.Q) ? "(без текста)" : q.Q;
        var box = new VerticalStackLayout { Spacing = 2 };
        box.Children.Add(Ui.Text($"{i + 1}. {text}", 13, Pal.Ink, true));
        if (!quiz.IsYesNo) box.Children.Add(Ui.Text($"вариантов: {q.Options.Count}", 10, Pal.Ink2, true));
        var card = Ui.Card(box, Pal.Paper, 10);
        Ui.OnTap(card, async () => await Push(new AdminQuestionPage(quiz, q)));

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
        var down = Form.Link("▼", i < quiz.Questions.Count - 1 ? Pal.Ink : Pal.Gray, () => Move(i, i + 1));
        down.VerticalOptions = LayoutOptions.Center;
        Grid.SetColumn(down, 2);
        row.Children.Add(down);
        return row;
    }

    void Move(int from, int to)
    {
        if (to < 0 || to >= quiz.Questions.Count) return;
        (quiz.Questions[from], quiz.Questions[to]) = (quiz.Questions[to], quiz.Questions[from]);
        SwapNumbers(quiz, from + 1, to + 1);
        Rebuild();
    }

    string FreeKey()
    {
        int n = quiz.Scales.Count + 1;
        while (quiz.Scales.Any(x => x.Key == "s" + n)) n++;
        return "s" + n;
    }

    // в да/нет-опроснике шкалы ссылаются на номера вопросов: при перестановке номера меняются местами
    public static void SwapNumbers(Questionnaire quiz, int a, int b)
    {
        static void Swap(List<int> list, int a, int b)
        {
            for (int i = 0; i < list.Count; i++)
                list[i] = list[i] == a ? b : list[i] == b ? a : list[i];
            list.Sort();
        }
        foreach (var sc in quiz.Scales)
        {
            Swap(sc.Yes, a, b);
            Swap(sc.No, a, b);
        }
    }

    // после удаления вопроса n ссылки на него убираются, следующие номера сдвигаются на 1
    public static void RemoveNumber(Questionnaire quiz, int n)
    {
        static void Fix(List<int> list, int n)
        {
            list.RemoveAll(x => x == n);
            for (int i = 0; i < list.Count; i++)
                if (list[i] > n) list[i]--;
        }
        foreach (var sc in quiz.Scales)
        {
            Fix(sc.Yes, n);
            Fix(sc.No, n);
        }
    }

    List<string> Validate()
    {
        var e = new List<string>();
        if (HasInvalid) e.Add("Исправь поля, подсвеченные красным");
        if (string.IsNullOrWhiteSpace(quiz.Title)) e.Add("Нет названия");
        if (!Regex.IsMatch(quiz.Id ?? "", "^[a-z0-9-]{2,32}$")) e.Add("ID: 2–32 символа: латиница в нижнем регистре, цифры, дефис");
        else if (otherIds.Contains(quiz.Id)) e.Add("Такой ID уже есть у другого опросника");

        if (quiz.Scales.Count == 0) e.Add("Нужна хотя бы одна шкала");
        var keys = new HashSet<string>();
        foreach (var sc in quiz.Scales)
        {
            string name = string.IsNullOrWhiteSpace(sc.Name) ? sc.Key ?? "" : sc.Name;
            if (!Regex.IsMatch(sc.Key ?? "", "^[a-z0-9_]{1,20}$")) e.Add($"Шкала «{name}»: ключ — латиница, цифры, _");
            else if (!keys.Add(sc.Key)) e.Add($"Ключ шкалы «{sc.Key}» повторяется");
            if (string.IsNullOrWhiteSpace(sc.Name)) e.Add($"Шкала «{sc.Key}»: нет названия");
            if (quiz.IsYesNo && sc.Yes.Concat(sc.No).Any(n => n > quiz.Questions.Count))
                e.Add($"Шкала «{name}»: указан номер вопроса больше {quiz.Questions.Count}");
            if (sc.Ranges.Any(r => string.IsNullOrWhiteSpace(r.Title)))
                e.Add($"Шкала «{name}»: у диапазона нет заголовка");
        }

        if (quiz.Questions.Count == 0) e.Add("Нужен хотя бы один вопрос");
        for (int i = 0; i < quiz.Questions.Count; i++)
        {
            var q = quiz.Questions[i];
            if (string.IsNullOrWhiteSpace(q.Q)) e.Add($"Вопрос {i + 1}: нет текста");
            if (quiz.IsYesNo) continue;
            if (q.Options.Count < 2) e.Add($"Вопрос {i + 1}: нужно минимум 2 варианта");
            if (q.Options.Any(o => string.IsNullOrWhiteSpace(o.Text))) e.Add($"Вопрос {i + 1}: есть пустой вариант");
            if (q.Options.Any(o => !keys.Contains(o.Scale))) e.Add($"Вопрос {i + 1}: у варианта не выбрана шкала");
        }
        return e;
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
        // тексты смешанного типа для удалённых шкал больше не нужны
        var pairs = new HashSet<string>();
        for (int a = 0; a < quiz.Scales.Count; a++)
            for (int b = a + 1; b < quiz.Scales.Count; b++)
                pairs.Add(quiz.Scales[a].Key + "+" + quiz.Scales[b].Key);
        foreach (var key in quiz.BlendTexts.Keys.Where(k => !pairs.Contains(k)).ToList())
            quiz.BlendTexts.Remove(key);
        foreach (var sc in quiz.Scales)
            sc.Ranges = sc.Ranges.OrderBy(r => r.Max).ToList();

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
        if (!await Dlg.Confirm(this, "УДАЛИТЬ?", $"Опросник «{quiz.Title}» пропадёт из приложения. Результаты в статистике останутся.", "УДАЛИТЬ", "ОТМЕНА"))
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
            !await Dlg.Confirm(this, "ВЫЙТИ?", "Несохранённые правки опросника пропадут.", "ВЫЙТИ", "ОСТАТЬСЯ"))
            return;
        await Pop();
    }
}
