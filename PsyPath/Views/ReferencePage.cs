using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

public class ReferencePage : BasePage
{
    readonly VerticalStackLayout listBox = new() { Spacing = 10 };
    readonly Entry search;
    readonly Label cardsInfo = Ui.Text("", 11, Pal.Ink, false);

    public ReferencePage() : base(false)
    {
        search = new Entry
        {
            Placeholder = "ПОИСК ТЕРМИНА...",
            PlaceholderColor = Pal.Ink2,
            TextColor = Pal.Ink,
            FontFamily = "Mono",
            FontSize = 14 * Ui.Scale,
            BackgroundColor = Colors.Transparent
        };
        search.TextChanged += (s, e) => RebuildList();

        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) }
        };

        var top = new VerticalStackLayout { Spacing = 0 };
        top.Children.Add(Ui.Stripes(4));
        var title = new Glitch("СПРАВОЧНИК", 24);
        title.Margin = new Thickness(18, 10, 18, 4);
        top.Children.Add(title);
        var searchCard = Ui.Card(search, Pal.Paper2, 4);
        searchCard.Margin = new Thickness(18, 4, 18, 8);
        top.Children.Add(searchCard);
        root.Children.Add(top);

        // карточки: термин → определение, повтор с интервалами
        var cardsBox = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        var cardsText = new VerticalStackLayout { Spacing = 2 };
        cardsText.Children.Add(Ui.Text("КАРТОЧКИ", 14, Pal.Ink, true));
        cardsText.Children.Add(cardsInfo);
        cardsBox.Children.Add(cardsText);
        var cardsGo = Ui.Text("▶", 22, Pal.Ink, true);
        cardsGo.VerticalTextAlignment = TextAlignment.Center;
        Grid.SetColumn(cardsGo, 1);
        cardsBox.Children.Add(cardsGo);
        var cards = Ui.Box(cardsBox, Pal.Amber, 12);
        Ui.OnTap(cards, async () => await Push(new FlashcardsPage()));

        var stack = new VerticalStackLayout { Padding = new Thickness(18, 4, 18, 28), Spacing = 10 };
        stack.Children.Add(cards);
        stack.Children.Add(listBox);
        stack.Children.Add(Ui.Text("Учебные материалы. Не клиническая диагностика.", 11, Pal.Ink2));
        var scroll = new ScrollView { Content = stack };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        SetBody(root);
        RebuildList();
    }

    // после карточек число к повтору поменялось; контент могли обновить из админки
    protected override void Refresh()
    {
        int due = Practice.DueCards;
        cardsInfo.Text = due > 0
            ? $"К повтору сегодня: {due}. Термин → определение, «знаю» или «не знаю»."
            : "На сегодня всё повторено" + (Practice.NextCardDate() is { } d ? $", следующие — {d}." : ".");
        RebuildList();
    }

    void RebuildList()
    {
        listBox.Children.Clear();
        string q = (search.Text ?? "").Trim().ToLowerInvariant();
        var entries = AppState.Reference
            .Where(e => q.Length == 0 || e.Term.ToLowerInvariant().Contains(q) || e.Short.ToLowerInvariant().Contains(q))
            .ToList();

        if (entries.Count == 0)
        {
            listBox.Children.Add(Ui.Mood("confused", 100));
            listBox.Children.Add(Ui.Text("НИЧЕГО НЕ НАЙДЕНО", 13, Pal.Ink2, true, TextAlignment.Center));
            return;
        }

        foreach (var entry in entries)
        {
            var box = new VerticalStackLayout { Spacing = 4 };
            box.Children.Add(Ui.Text("▸ " + entry.Term.ToUpperInvariant(), 14, Pal.Ink, true));
            box.Children.Add(Ui.Text(entry.Short, 12, Pal.Ink2));

            var detail = Ui.Text(entry.Text, 13);
            detail.IsVisible = false;
            detail.Margin = new Thickness(0, 6, 0, 0);
            box.Children.Add(detail);

            var card = Ui.Box(box, Pal.Paper, 12);
            Ui.OnTap(card, () => detail.IsVisible = !detail.IsVisible);
            listBox.Children.Add(card);
        }
    }
}
