using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

public class QuestionnairesPage : DataPage
{
    protected override ScrollView Build()
    {
        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) }
        };

        var top = new VerticalStackLayout { Spacing = 0 };
        top.Children.Add(Ui.Stripes(4));
        var title = new Glitch("ОПРОСНИКИ", 24);
        title.Margin = new Thickness(18, 10, 18, 6);
        top.Children.Add(title);
        root.Children.Add(top);

        var stack = new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 6, 18, 30) };
        stack.Children.Add(Ui.Text("Ответы считаются сами. Результаты сохраняются в профиле, пройти можно сколько угодно раз.", 12, Pal.Ink2));

        foreach (var q in AppState.Questionnaires)
        {
            var box = new VerticalStackLayout { Spacing = 4 };
            var titleRow = new HorizontalStackLayout { Spacing = 8 };
            titleRow.Children.Add(new PixelIcon(Icons.Tape, Pal.Ink, 18));
            titleRow.Children.Add(Ui.Text(q.Title.ToUpperInvariant(), 16, Pal.Ink, true));
            box.Children.Add(titleRow);
            box.Children.Add(Ui.Text($"{q.Questions.Count} вопросов", 11, Pal.TealInk, true));
            box.Children.Add(Ui.Text(q.About, 12));

            var last = AppState.P.History.FirstOrDefault(h => h.Id == q.Id);
            int times = AppState.P.History.Count(h => h.Id == q.Id);
            if (last != null)
                box.Children.Add(Ui.Text($"Последний раз: {last.Headline} ({last.Date}), всего прохождений: {times}", 11, Pal.Ink2, true));

            var card = Ui.Box(box, Pal.Paper, 12);
            var item = q;
            Ui.OnTap(card, async () => await Push(new QuestionnairePage(item)));
            stack.Children.Add(card);
        }

        var scroll = new ScrollView { Content = stack };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        SetBody(root);
        return scroll;
    }
}
