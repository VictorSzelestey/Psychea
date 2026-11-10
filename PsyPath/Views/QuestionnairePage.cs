using PsyPath.Models;
using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

public class QuestionnairePage : BasePage
{
    readonly Questionnaire quiz;
    readonly Random rnd = new();
    readonly List<int> answers = new();
    int index;
    bool locked;
    Elephant? elephant;

    public QuestionnairePage(Questionnaire quiz) : base(true)
    {
        this.quiz = quiz;
        ShowIntro();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        elephant?.Stop();
    }

    static View Wrap(View content)
    {
        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) }
        };
        Grid.SetRow(content, 1);
        root.Children.Add(content);
        return root;
    }

    void ShowIntro()
    {
        var stack = new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 14, 18, 30) };
        stack.Children.Add(new Heading(quiz.Title.ToUpperInvariant(), 22));
        stack.Children.Add(Ui.Mascot(150, idle: true));
        stack.Children.Add(Ui.Box(Ui.Text(quiz.About, 14), Pal.Paper, 14));
        stack.Children.Add(Ui.Text($"Вопросов: {quiz.Questions.Count}", 12, Pal.Ink2, true));
        if (!string.IsNullOrEmpty(quiz.Source))
            stack.Children.Add(Ui.Text("Источник: " + quiz.Source, 11, Pal.Ink2));

        var go = new Btn("НАЧАТЬ ▶", Pal.Violet, Pal.Paper);
        go.Clicked += async (s, e) =>
        {
            if (locked) return;
            locked = true;
            go.Enabled = false;
            await ShowQuestion();
        };
        stack.Children.Add(go);

        var back = Ui.Text("НАЗАД", 12, Pal.Ink2, true, TextAlignment.Center);
        Ui.OnTap(back, async () => await Pop());
        stack.Children.Add(back);

        SetBody(Wrap(new ScrollView { Content = stack }));
    }

    async Task ShowQuestion()
    {
        locked = true;
        if (index >= quiz.Questions.Count)
        {
            await ShowResult();
            return;
        }
        var q = quiz.Questions[index];
        int questionIndex = index;
        int? selectedValue = null;
        var choices = new List<(Button Button, int Value, string Text)>();
        CrashLog.Write("Questionnaire", $"id={quiz.Id}; question={index + 1}/{quiz.Questions.Count}");

        var head = new VerticalStackLayout { Spacing = 0 };
        head.Children.Add(new Bar(Pal.Violet) { Value = (double)index / quiz.Questions.Count, Margin = new Thickness(18, 12, 18, 0) });
        var counter = Ui.Text($"ВОПРОС {index + 1:00}/{quiz.Questions.Count}", 11, Pal.Ink2, true, TextAlignment.End);
        counter.Margin = new Thickness(18, 4, 18, 0);
        head.Children.Add(counter);

        var body = new VerticalStackLayout { Spacing = 12, Padding = new Thickness(18, 10, 18, 28) };
        body.Children.Add(Ui.Text(q.Q, 19, Pal.Ink, true));

        var next = new Button
        {
            Text = "ДАЛЬШЕ", BackgroundColor = Pal.Violet, TextColor = Pal.Paper,
            FontFamily = "BodyBold", FontSize = 16 * Ui.Scale, CornerRadius = 24,
            MinimumHeightRequest = 52, Padding = new Thickness(16, 12), IsEnabled = false
        };
        SemanticProperties.SetHint(next, "Выбери ответ, затем переходи к следующему вопросу");
        next.Clicked += async (s, e) =>
        {
            if (locked || index != questionIndex || selectedValue == null) return;
            locked = true;
            next.IsEnabled = false;
            answers.Add(selectedValue.Value);
            index++;
            await ShowQuestion();
        };

        void AddAnswer(string text, int value)
        {
            var button = new Button
            {
                Text = text, BackgroundColor = Pal.Paper, TextColor = Pal.Ink,
                FontFamily = "BodyBold", FontSize = (quiz.IsYesNo ? 18 : 15) * Ui.Scale,
                CornerRadius = 24, BorderColor = Pal.Line, BorderWidth = 2,
                MinimumHeightRequest = 56, Padding = new Thickness(16, 14),
                LineBreakMode = LineBreakMode.WordWrap
            };
            choices.Add((button, value, text));
            button.Clicked += (s, e) =>
            {
                if (locked || index != questionIndex) return;
                selectedValue = value;
                Sound.Play("tap");
                foreach (var choice in choices)
                {
                    bool selected = choice.Value == value;
                    choice.Button.BackgroundColor = selected ? Pal.Amber : Pal.Paper;
                    choice.Button.BorderColor = selected ? Pal.Violet : Pal.Line;
                    choice.Button.Text = selected ? "✓ " + choice.Text : choice.Text;
                    SemanticProperties.SetDescription(choice.Button, choice.Text + (selected ? ", выбран" : ", не выбран"));
                }
                next.IsEnabled = true;
            };
            body.Children.Add(button);
        }

        if (quiz.IsYesNo)
        {
            AddAnswer("ДА", 1);
            AddAnswer("НЕТ", 0);
        }
        else
        {
            var order = Enumerable.Range(0, q.Options.Count).OrderBy(_ => rnd.Next()).ToList();
            foreach (int i in order)
                AddAnswer(q.Options[i].Text, i);
        }

        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) }
        };
        root.Children.Add(head);
        var scroll = new ScrollView { Content = body };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        var footer = new VerticalStackLayout { Padding = new Thickness(18, 8, 18, 24) };
        footer.Children.Add(next);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        await SwapBody(root);
        // пока идёт анимация, ответы не принимаются
        locked = false;
    }

    async Task ShowResult()
    {
        var res = AppState.Score(quiz, answers);
        AppState.SaveResult(quiz, res);

        Sound.Play("win");
        var stack = new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 10, 18, 30) };
        // карточка-итог для «Поделиться»: название, главный вывод, шкалы
        var card = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(0, 0, 0, 6), BackgroundColor = Pal.Paper };
        card.Children.Add(new Heading("РЕЗУЛЬТАТ", 24));
        card.Children.Add(Ui.Text(quiz.Title.ToUpperInvariant(), 12, Pal.Ink2, true));
        stack.Children.Add(card);

        if (AppState.IsTemperamentQuiz(quiz.Id))
        {
            elephant = new Elephant(150);
            card.Children.Add(elephant);
            elephant.Start();
        }

        if (res.Main != null) DominantView(card, stack, res);
        else ScalesView(card, res);
        card.Children.Add(Ui.Text("ПСИХЕЯ // УЧЕБНЫЙ ОПРОСНИК, НЕ ДИАГНОЗ", 10, Pal.Ink2, true, TextAlignment.Center));

        if (!string.IsNullOrEmpty(quiz.Disclaimer))
            stack.Children.Add(Ui.Text(quiz.Disclaimer, 11, Pal.Ink2));

        var done = new Btn("ГОТОВО", Pal.Violet, Pal.Paper);
        done.Clicked += async (s, e) => await Pop();
        stack.Children.Add(done);

        stack.Children.Add(Form.Link("ПОДЕЛИТЬСЯ РЕЗУЛЬТАТОМ", Pal.TealInk, async () =>
        {
            if (!await ShareCard.Share(card, "Психея: " + quiz.Title))
                await Dlg.Alert(this, "НЕ ВЫШЛО", "Не удалось сделать картинку. Попробуй ещё раз.");
        }, TextAlignment.Center));

        await SwapBody(Wrap(new ScrollView { Content = stack }));
    }

    // в карточку для картинки — вывод и шкалы, подробности — ниже на экране
    void DominantView(VerticalStackLayout card, VerticalStackLayout stack, QOutcome res)
    {
        var main = res.Main!.Scale;
        card.Children.Add(Ui.Text(main.Name.ToUpperInvariant(), 30, Pal.RedInk, true, TextAlignment.Center));
        card.Children.Add(Ui.Text(main.Tagline, 14, Pal.Ink2, true, TextAlignment.Center));

        double sum = Math.Max(1, res.Scores.Sum(x => x.Value));
        var rows = res.Scores.Select((x, i) => (
            x.Scale.Name,
            x.Value / sum,
            Math.Round(x.Value * 100 / sum) + "%",
            Pal.Cycle(i)));
        card.Children.Add(Ui.Card(Ui.Bars(rows), Pal.Paper, 14));

        string blend = AppState.BlendText(quiz, res);
        if (!string.IsNullOrEmpty(blend))
            stack.Children.Add(Ui.Card(Ui.Text(blend, 13), Pal.Amber, 12));

        stack.Children.Add(Ui.Card(Ui.Text(main.Description, 14), Pal.Paper2, 12));
        if (main.Strengths.Count > 0) stack.Children.Add(Ui.Bullets("СИЛЬНЫЕ СТОРОНЫ", main.Strengths));
        if (main.WatchOut.Count > 0) stack.Children.Add(Ui.Bullets("НА ЧТО ОБРАТИТЬ ВНИМАНИЕ", main.WatchOut));
        if (main.StudyTips.Count > 0) stack.Children.Add(Ui.Bullets("КАК ТЕБЕ УЧИТЬСЯ", main.StudyTips));
        if (!string.IsNullOrEmpty(main.PsychFit))
            stack.Children.Add(Ui.Bullets("ЧТО ПОДОЙДЁТ В ПСИХОЛОГИИ", new[] { main.PsychFit }));
    }

    void ScalesView(VerticalStackLayout stack, QOutcome res)
    {
        if (res.TypeTitle.Length > 0)
        {
            stack.Children.Add(Ui.Text(res.TypeTitle.ToUpperInvariant(), 30, Pal.RedInk, true, TextAlignment.Center));
            stack.Children.Add(Ui.Text(res.TypeText, 14, Pal.Ink2, false, TextAlignment.Center));
        }

        int i = 0;
        foreach (var sc in res.Scores)
        {
            var box = new VerticalStackLayout { Spacing = 6 };
            box.Children.Add(Ui.Text(sc.Scale.Name.ToUpperInvariant(), 14, Pal.Ink, true));
            box.Children.Add(Ui.Text($"{sc.Value} из {sc.Max}", 12, Pal.Ink2, true));
            box.Children.Add(new Bar(Pal.Cycle(i)) { Value = sc.Max == 0 ? 0 : (double)sc.Value / sc.Max });
            if (sc.Range != null)
            {
                box.Children.Add(Ui.Text(sc.Range.Title, 16, Pal.RedInk, true));
                box.Children.Add(Ui.Text(sc.Range.Text, 13));
            }
            stack.Children.Add(Ui.Box(box, Pal.Paper, 12));
            i++;
        }
    }
}
