using PsyPath.Models;
using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

public class QuestionnairesPage : DataPage
{
    VerticalStackLayout listBox = new();
    // текст поиска переживает пересборку экрана
    string query = "";

    protected override ScrollView Build()
    {
        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) }
        };

        var top = new VerticalStackLayout { Spacing = 0 };
        var title = new Heading("ОПРОСНИКИ", 24);
        title.Margin = new Thickness(18, 10, 18, 4);
        top.Children.Add(title);
        var search = new Entry
        {
            Text = query,
            Placeholder = "ПОИСК ОПРОСНИКА...",
            PlaceholderColor = Pal.Ink2,
            TextColor = Pal.Ink,
            FontFamily = "Body",
            FontSize = 14 * Ui.Scale,
            BackgroundColor = Colors.Transparent
        };
        search.TextChanged += (s, e) =>
        {
            query = e.NewTextValue ?? "";
            RebuildList();
        };
        var searchCard = Ui.Card(search, Pal.Paper2, 4);
        searchCard.Margin = new Thickness(18, 4, 18, 8);
        top.Children.Add(searchCard);
        root.Children.Add(top);

        var stack = new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 6, 18, 30) };
        stack.Children.Add(Ui.Text("Ответы считаются сами. Результаты сохраняются в профиле, пройти можно сколько угодно раз.", 12, Pal.Ink2));
        listBox = new VerticalStackLayout { Spacing = 14 };
        stack.Children.Add(listBox);
        RebuildList();

        var scroll = new ScrollView { Content = stack };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        SetBody(root);
        return scroll;
    }

    // поиск по названию и описанию опросника
    void RebuildList()
    {
        listBox.Children.Clear();
        string q0 = query.Trim();
        var found = AppState.Questionnaires
            .Where(q => q0.Length == 0
                || q.Title.Contains(q0, StringComparison.OrdinalIgnoreCase)
                || q.About.Contains(q0, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (found.Count == 0)
        {
            listBox.Children.Add(Ui.Mood("confused", 100));
            listBox.Children.Add(Ui.Text("НИЧЕГО НЕ НАЙДЕНО", 13, Pal.Ink2, true, TextAlignment.Center));
            return;
        }

        foreach (var q in found)
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
            listBox.Children.Add(card);
        }
    }
}
