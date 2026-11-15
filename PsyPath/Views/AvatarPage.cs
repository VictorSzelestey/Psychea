using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

// выбор аватарки: она видна в профиле и в топе игроков
public class AvatarPage : AdminBase
{
    // перестраивается при каждом показе: после магазина в списке появляются купленные
    protected override void Refresh()
    {
        base.Refresh();
        Build();
    }

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
        // все аватарки: некупленные — с ценой, нажатие предлагает купить
        var items = Avatars.All.Prepend(("", "Без картинки", 0)).ToList();
        for (int i = 0; i < items.Count; i++)
        {
            var (id, caption, price) = items[i];
            bool owned = Crystals.Owns(id, price);
            bool current = AppState.P.Avatar == id || (id == "" && !Avatars.Exists(AppState.P.Avatar));
            if (i % 3 == 0) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var box = new VerticalStackLayout { Spacing = 6 };
            box.Children.Add(Avatars.View(id, name, 76));
            box.Children.Add(Ui.Text(caption, 11, Pal.Ink, current, TextAlignment.Center));
            // выбранная — светлый фон и галочка в подписи, не только цвет
            if (current) ((Label)box.Children[1]).Text = "✓ " + caption;
            if (!owned) box.Children.Add(ShopPage.PriceTag(false, false, price, ""));
            var card = Ui.Card(box, current ? Pal.Amber : Pal.Paper, 8);
            SemanticProperties.SetDescription(card, caption + (current ? ", выбрана" : owned ? "" : $", цена {price} {Crystals.Word(price)}"));
            Ui.OnTap(card, async () =>
            {
                if (!owned && !await ShopPage.Purchase(this, $"аватарку «{caption}»", id, price, "Она сразу станет твоей.")) return;
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
        s.Children.Add(Form.Link("ТЕМЫ ОФОРМЛЕНИЯ — В МАГАЗИНЕ ▶", Pal.TealInk, async () => await Push(new ShopPage()), TextAlignment.Center));
        Show("АВАТАРКА", s);
    }
}
