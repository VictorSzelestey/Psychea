using Microsoft.Maui.Controls.Shapes;

namespace PsyPath.UI;

// аватарки игрока: три кота и животные темпераментов в спокойной позе. Картинка — Resources/Images/ava_<id>.png
public static class Avatars
{
    public static readonly (string Id, string Name)[] All =
    {
        ("cat_santa", "Кот с подарком"),
        ("cat_wizard", "Кот-волшебник"),
        ("cat_peek", "Любопытный кот"),
        ("monkey", "Обезьяна — сангвиник"),
        ("bull", "Бык — холерик"),
        ("elephant", "Слон — флегматик"),
        ("rabbit", "Заяц — меланхолик")
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
