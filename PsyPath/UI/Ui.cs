using Microsoft.Maui.Controls.Shapes;
using PsyPath.Services;

namespace PsyPath.UI;

public static class Ui
{
    // размер текста из настроек (1, 1.15, 1.3); применяется ко всему, что создано после смены
    public static double Scale { get; set; } = Settings.FontScale;

    public static Label Text(string text, double size = 14, Color? color = null, bool bold = false, TextAlignment align = TextAlignment.Start)
    {
        return new Label
        {
            Text = text,
            FontSize = size * Scale,
            TextColor = color ?? Pal.Ink,
            FontFamily = bold ? "MonoBold" : "Mono",
            HorizontalTextAlignment = align,
            LineBreakMode = LineBreakMode.WordWrap
        };
    }

    public static Border Card(View content, Color? fill = null, double pad = 14, double radius = 8)
    {
        return new Border
        {
            Content = content,
            BackgroundColor = fill ?? Pal.Paper,
            Stroke = new SolidColorBrush(Pal.Ink),
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(radius) },
            Padding = new Thickness(pad)
        };
    }

    public static Grid Box(View content, Color? fill = null, double pad = 14, double off = 4)
    {
        var shadow = new Border
        {
            BackgroundColor = Pal.Ink,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) },
            Margin = new Thickness(off, off, 0, 0)
        };
        var top = Card(content, fill, pad);
        top.Margin = new Thickness(0, 0, off, off);

        var g = new Grid();
        g.Children.Add(shadow);
        g.Children.Add(top);
        return g;
    }

    public static View Stripes(double h = 5)
    {
        var g = new Grid { RowSpacing = 0, ColumnSpacing = 0 };
        var cols = new[] { Pal.Amber, Pal.Orange, Pal.Red, Pal.Teal };
        for (int i = 0; i < cols.Length; i++)
        {
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(h) });
            var b = new BoxView { Color = cols[i] };
            Grid.SetRow(b, i);
            g.Children.Add(b);
        }
        return g;
    }


    // idle — манул спокойно покачивается, пока на экране
    public static Image Mascot(double height, bool idle = false)
    {
        var img = new Image
        {
            Source = "snowcat.png",
            HeightRequest = height,
            // ширина известна заранее (картинка 900×811), раскладка не сдвигается после загрузки
            WidthRequest = height * 900 / 811,
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Center
        };
        if (idle) Anim.Idle(img, height / 40);
        return img;
    }

    // маскот с настроением под конкретный экран: файл mood_*.png в Resources/Images
    public static Image Mood(string name, double height)
    {
        return new Image
        {
            Source = "mood_" + name + ".png",
            HeightRequest = height,
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Center
        };
    }

    // версия приложения внизу экрана: «ПСИХЕЯ v1.1 (2)»
    public static Label VersionLabel()
    {
        var v = Text($"ПСИХЕЯ v{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})", 11, Pal.Ink2, true, TextAlignment.Center);
        v.Margin = new Thickness(0, 4, 0, 0);
        return v;
    }

    // табло: светящийся текст на тёмном, слева может быть пиксельная иконка
    public static Border Lcd(string text, double size = 15, string[]? icon = null)
    {
        var label = Text(text, size, Pal.Phosphor, true, TextAlignment.Center);
        label.VerticalTextAlignment = TextAlignment.Center;
        View content = label;
        if (icon != null)
        {
            var row = new HorizontalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.Center };
            row.Children.Add(new PixelIcon(icon, Pal.Phosphor, size, Pal.Orange));
            row.Children.Add(label);
            content = row;
        }
        return new Border
        {
            Content = content,
            BackgroundColor = Pal.Ink,
            Stroke = new SolidColorBrush(Pal.Ink),
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(6) },
            Padding = new Thickness(6, 8)
        };
    }

    public static void OnTap(View v, Action action)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, e) =>
        {
            _ = v.Press();
            action();
        };
        v.GestureRecognizers.Add(tap);
    }

    public static View Bullets(string title, IEnumerable<string> lines, Color? fill = null)
    {
        var box = new VerticalStackLayout { Spacing = 4 };
        box.Children.Add(Text(title, 12, Pal.RedInk, true));
        foreach (var line in lines)
            box.Children.Add(Text("▸ " + line, 13));
        return Card(box, fill ?? Pal.Paper2, 12);
    }

    public static View Bars(IEnumerable<(string label, double fraction, string value, Color color)> rows)
    {
        var box = new VerticalStackLayout { Spacing = 8 };
        foreach (var r in rows)
        {
            var row = new Grid
            {
                ColumnSpacing = 8,
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(130)),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(50))
                }
            };

            var name = Text(r.label, 12, Pal.Ink, true);
            name.VerticalTextAlignment = TextAlignment.Center;

            var bar = new Bar(r.color) { Value = Math.Clamp(r.fraction, 0, 1), VerticalOptions = LayoutOptions.Center };
            Grid.SetColumn(bar, 1);

            var val = Text(r.value, 12, Pal.Ink, true, TextAlignment.End);
            val.VerticalTextAlignment = TextAlignment.Center;
            Grid.SetColumn(val, 2);

            row.Children.Add(name);
            row.Children.Add(bar);
            row.Children.Add(val);
            box.Children.Add(row);
        }
        return box;
    }
}
