using PsyPath.Models;
using PsyPath.UI;

namespace PsyPath.Views;

// вопрос опросника: текст и варианты ответа со шкалой и баллами
public class AdminQuestionPage : AdminFormPage
{
    readonly Questionnaire quiz;
    readonly QQuestion question;

    public AdminQuestionPage(Questionnaire quiz, QQuestion question)
    {
        this.quiz = quiz;
        this.question = question;
    }

    protected override void Build()
    {
        int n = quiz.Questions.IndexOf(question) + 1;
        var s = List();
        s.Children.Add(Form.Field($"ВОПРОС {n}", question.Q, v => question.Q = v, multiline: true));

        if (quiz.IsYesNo)
        {
            s.Children.Add(Form.Hint($"Ответ «да» или «нет». Какой ответ даёт балл, задаётся в шкалах: этот вопрос — №{n}."));
        }
        else
        {
            s.Children.Add(Form.Section($"ВАРИАНТЫ ОТВЕТА ({question.Options.Count})"));
            s.Children.Add(Form.Hint("В приложении варианты показываются в случайном порядке."));
            var scales = quiz.Scales.Select(x => (x.Key, x.Name)).ToList();
            for (int i = 0; i < question.Options.Count; i++)
            {
                var opt = question.Options[i];
                var box = new VerticalStackLayout { Spacing = 6 };
                box.Children.Add(Form.Field($"ВАРИАНТ {i + 1}", opt.Text, v => opt.Text = v, multiline: true));
                if (scales.Count == 0) box.Children.Add(Form.Hint("Сначала добавь шкалу в опроснике."));
                else box.Children.Add(Form.Choice("ШКАЛА", scales, opt.Scale, v => opt.Scale = v));
                box.Children.Add(Form.Int("БАЛЛЫ", opt.Points, v => opt.Points = v, Track()));
                box.Children.Add(Form.Link("✕ УДАЛИТЬ ВАРИАНТ", Pal.RedInk, () =>
                {
                    question.Options.Remove(opt);
                    Rebuild();
                }));
                s.Children.Add(Ui.Card(box, Pal.Paper, 10));
            }
            var add = new Btn("+ ВАРИАНТ", Pal.Paper2, null, 14);
            add.Clicked += (o, e) =>
            {
                question.Options.Add(new QOption { Scale = quiz.Scales.FirstOrDefault()?.Key ?? "", Points = 1 });
                Rebuild();
            };
            s.Children.Add(add);
        }

        s.Children.Add(Form.Link("УДАЛИТЬ ВОПРОС", Pal.RedInk, async () =>
        {
            if (!await Dlg.Confirm(this, "УДАЛИТЬ?", $"Вопрос {n} будет удалён.", "УДАЛИТЬ", "ОТМЕНА"))
                return;
            quiz.Questions.Remove(question);
            AdminQuizPage.RemoveNumber(quiz, n);
            await Pop();
        }, TextAlignment.Center));

        var bottom = new VerticalStackLayout { Padding = new Thickness(16, 8, 16, 12), BackgroundColor = Pal.Paper };
        var ok = new Btn("ГОТОВО ◀", Pal.Violet, Pal.Paper);
        ok.Clicked += async (o, e) => await Leave();
        bottom.Children.Add(ok);

        Show("ВОПРОС", s, bottom);
    }

    protected override async Task Leave()
    {
        if (!await ConfirmLeaveWithErrors()) return;
        await Pop();
    }
}
