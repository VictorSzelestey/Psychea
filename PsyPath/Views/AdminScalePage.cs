using PsyPath.Models;
using PsyPath.UI;

namespace PsyPath.Views;

// шкала опросника: тексты результата, ключи да/нет, диапазоны баллов
public class AdminScalePage : AdminFormPage
{
    readonly Questionnaire quiz;
    readonly QScale scale;
    readonly string oldKey;
    bool removed;

    public AdminScalePage(Questionnaire quiz, QScale scale)
    {
        this.quiz = quiz;
        this.scale = scale;
        oldKey = scale.Key;
    }

    protected override void Build()
    {
        var s = List();

        s.Children.Add(Form.Field("КЛЮЧ", scale.Key, v => scale.Key = v.Trim(),
            hint: "Коротко латиницей: san, hol, e. По ключу варианты ответа связаны со шкалой."));
        s.Children.Add(Form.Field("НАЗВАНИЕ", scale.Name, v => scale.Name = v));

        if (quiz.IsYesNo)
        {
            s.Children.Add(Form.Section("КЛЮЧ ОТВЕТОВ"));
            s.Children.Add(Form.Numbers("«ДА» ДАЁТ БАЛЛ В ВОПРОСАХ", scale.Yes, Track(), "Номера вопросов через запятую: 1, 5, 9"));
            s.Children.Add(Form.Numbers("«НЕТ» ДАЁТ БАЛЛ В ВОПРОСАХ", scale.No, Track()));
        }

        s.Children.Add(Form.Section("ОПИСАНИЕ РЕЗУЛЬТАТА"));
        s.Children.Add(Form.Hint(quiz.Mode == "dominant"
            ? "Показывается, когда эта шкала главная."
            : "В режиме «все шкалы» эти тексты не показываются, важны диапазоны ниже."));
        s.Children.Add(Form.Field("КОРОТКО", scale.Tagline, v => scale.Tagline = v));
        s.Children.Add(Form.Field("ОПИСАНИЕ", scale.Description, v => scale.Description = v, multiline: true));
        s.Children.Add(Form.Lines("СИЛЬНЫЕ СТОРОНЫ", scale.Strengths));
        s.Children.Add(Form.Lines("НА ЧТО ОБРАТИТЬ ВНИМАНИЕ", scale.WatchOut));
        s.Children.Add(Form.Lines("КАК УЧИТЬСЯ", scale.StudyTips));
        s.Children.Add(Form.Field("ЧТО ПОДОЙДЁТ В ПСИХОЛОГИИ", scale.PsychFit, v => scale.PsychFit = v, multiline: true));

        s.Children.Add(Form.Section($"ДИАПАЗОНЫ ({scale.Ranges.Count})"));
        s.Children.Add(Form.Hint("Балл до «макс.» включительно попадает в диапазон. При сохранении диапазоны сортируются по макс."));
        foreach (var range in scale.Ranges)
        {
            var r = range;
            var box = new VerticalStackLayout { Spacing = 6 };
            box.Children.Add(Form.Int("МАКС. БАЛЛ", r.Max, v => r.Max = v, Track()));
            box.Children.Add(Form.Field("ЗАГОЛОВОК", r.Title, v => r.Title = v));
            box.Children.Add(Form.Field("ТЕКСТ", r.Text, v => r.Text = v, multiline: true));
            box.Children.Add(Form.Link("✕ УДАЛИТЬ ДИАПАЗОН", Pal.RedInk, () =>
            {
                scale.Ranges.Remove(r);
                Rebuild();
            }));
            s.Children.Add(Ui.Card(box, Pal.Paper, 10));
        }
        var add = new Btn("+ ДИАПАЗОН", Pal.Paper2, null, 14);
        add.Clicked += (o, e) =>
        {
            scale.Ranges.Add(new QRange { Max = (scale.Ranges.Count == 0 ? 0 : scale.Ranges.Max(x => x.Max)) + 5 });
            Rebuild();
        };
        s.Children.Add(add);

        s.Children.Add(Form.Link("УДАЛИТЬ ШКАЛУ", Pal.RedInk, async () =>
        {
            if (!await Dlg.Confirm(this, "УДАЛИТЬ?", $"Шкала «{scale.Name}» будет удалена. Варианты ответа с ней нужно будет привязать к другой шкале.", "УДАЛИТЬ", "ОТМЕНА"))
                return;
            quiz.Scales.Remove(scale);
            removed = true;
            await Pop();
        }, TextAlignment.Center));

        var bottom = new VerticalStackLayout { Padding = new Thickness(16, 8, 16, 12), BackgroundColor = Pal.Paper };
        var ok = new Btn("ГОТОВО ◀", Pal.Amber);
        ok.Clicked += async (o, e) => await Leave();
        bottom.Children.Add(ok);

        Show("ШКАЛА", s, bottom);
    }

    protected override async Task Leave()
    {
        if (!await ConfirmLeaveWithErrors()) return;
        if (!removed && scale.Key != oldKey) RenameKey(oldKey, scale.Key);
        await Pop();
    }

    // новый ключ шкалы подставляется в варианты ответа и тексты смешанного типа
    void RenameKey(string from, string to)
    {
        foreach (var opt in quiz.Questions.SelectMany(q => q.Options).Where(o => o.Scale == from))
            opt.Scale = to;
        foreach (var key in quiz.BlendTexts.Keys.ToList())
        {
            var parts = key.Split('+');
            if (!parts.Contains(from)) continue;
            string text = quiz.BlendTexts[key];
            quiz.BlendTexts.Remove(key);
            quiz.BlendTexts[string.Join("+", parts.Select(p => p == from ? to : p))] = text;
        }
    }
}
