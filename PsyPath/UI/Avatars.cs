using Microsoft.Maui.Controls.Shapes;

namespace PsyPath.UI;

// аватарки игрока: три кота, животные темпераментов в спокойной позе и платные из магазина (Price > 0).
// Картинка — Resources/Images/ava_<id>.png. Все id остаются в списке: топ показывает чужие купленные аватарки
public static class Avatars
{
    public static readonly (string Id, string Name, int Price)[] All =
    {
        ("cat_santa", "Кот с подарком", 0),
        ("cat_wizard", "Кот-волшебник", 0),
        ("cat_peek", "Любопытный кот", 0),
        ("monkey", "Обезьяна — сангвиник", 0),
        ("bull", "Бык — холерик", 0),
        ("elephant", "Слон — флегматик", 0),
        ("rabbit", "Заяц — меланхолик", 0),
        ("owl", "Сова-мудрец", 100),
        ("manul_scholar", "Манул-психолог", 150),
        ("cat_dream", "Кот-сновидец", 150),
        ("raccoon", "Енот-детектив", 200),
        ("fox_zen", "Лис на медитации", 200),
        ("cat_king", "Кот-король", 300)
    };

    public static bool Exists(string? id) => All.Any(a => a.Id == id);

    // круглая аватарка; без картинки — первая буква имени на сиреневом круге
    public static Border View(string? id, string name, double size)
    {
        View content;
        if (Exists(id))
            content = new Image { Source = $"ava_{id}.png", Aspect = Aspect.AspectFill, WidthRequest = size, HeightRequest = size };
        else
        {
            string letter = name.Trim().Length > 0 ? name.Trim()[..1].ToUpperInvariant() : "?";
            var label = Ui.Text(letter, size * 0.42 / Ui.Scale, Pal.Ink, true, TextAlignment.Center);
            label.VerticalTextAlignment = TextAlignment.Center;
            content = label;
        }
        var circle = new Border
        {
            Content = content,
            WidthRequest = size,
            HeightRequest = size,
            Padding = 0,
            BackgroundColor = Pal.Amber,
            Stroke = new SolidColorBrush(Pal.Line),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(size / 2) },
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };
        // имя игрока всегда написано рядом
        AutomationProperties.SetIsInAccessibleTree(circle, false);
        return circle;
    }
}
