using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

// выбор аватарки: она видна в профиле и в топе игроков
public class AvatarPage : AdminBase
{
    public AvatarPage() => Build();

    void Build()
    {
        string name = Api.User?.DisplayName ?? "";
        var s = List();
        s.Children.Add(Ui.Text("Аватарку видят все в топе игроков.", 13, Pal.Ink2, true));

        var grid = new Grid
        {
            ColumnSpacing = 10,
            RowSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };
        var items = Avatars.All.Select(a => (a.Id, a.Name)).Prepend(("", "Без картинки")).ToList();
        for (int i = 0; i < items.Count; i++)
        {
            var (id, caption) = items[i];
            bool current = AppState.P.Avatar == id || (id == "" && !Avatars.Exists(AppState.P.Avatar));
            if (i % 3 == 0) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var box = new VerticalStackLayout { Spacing = 6 };
            box.Children.Add(Avatars.View(id, name, 76));
            box.Children.Add(Ui.Text(caption, 11, Pal.Ink, current, TextAlignment.Center));
            // выбранная — светлый фон и галочка в подписи, не только цвет
            if (current) ((Label)box.Children[1]).Text = "✓ " + caption;
            var card = Ui.Card(box, current ? Pal.Amber : Pal.Paper, 8);
            SemanticProperties.SetDescription(card, caption + (current ? ", выбрана" : ""));
            Ui.OnTap(card, async () =>
            {
                AppState.P.Avatar = id;
                AppState.Save();
                Sound.Play("ok");
                await Pop();
            });
            Grid.SetRow(card, i / 3);
            Grid.SetColumn(card, i % 3);
            grid.Children.Add(card);
        }
        s.Children.Add(grid);
        Show("АВАТАРКА", s);
    }
}
