using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
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
            FontFamily = bold ? "BodyBold" : "Body",
            HorizontalTextAlignment = align,
            LineBreakMode = LineBreakMode.WordWrap
        };
    }

    public static Border Card(View content, Color? fill = null, double pad = 14, double radius = 18)
    {
        return new Border
        {
            Content = content,
            BackgroundColor = fill ?? Pal.Paper,
            Stroke = new SolidColorBrush(Pal.Line),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(radius) },
            Padding = new Thickness(pad)
        };
    }

    // карточка, приподнятая над фоном
    public static Grid Box(View content, Color? fill = null, double pad = 14) => new Lifted(Card(content, fill, pad), 18);


    // idle — манул спокойно покачивается, пока на экране
    public static Image Mascot(double height, bool idle = false)
    {
        var img = new Image
        {
            // наряд из магазина, если надет
            Source = Cosmetics.MascotImage,
            HeightRequest = height,
            // ширина известна заранее (картинка 900×811), раскладка не сдвигается после загрузки
            WidthRequest = height * 900 / 811,
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Center
        };
        if (idle) Anim.Idle(img, height / 40);
        return img;
    }

    // экран ожидания: манул покачивается, пока грузится следующий экран
    public static View Waiting(string text)
    {
        var cat = Mascot(170);
        Anim.Idle(cat, 170 / 40.0, cycles: 1000);
        var box = new VerticalStackLayout { Spacing = 16, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center };
        box.Children.Add(cat);
        box.Children.Add(Text(text, 14, Pal.Ink2, true, TextAlignment.Center));
        return box;
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

    // версия приложения внизу экрана: «ПСИХЕЯ v1.1 (2)» и под ней пометка о бете
    public static View VersionLabel()
    {
        var box = new VerticalStackLayout { Spacing = 2, Margin = new Thickness(0, 4, 0, 0) };
        box.Children.Add(Text($"ПСИХЕЯ v{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})", 11, Pal.Ink2, true, TextAlignment.Center));
        box.Children.Add(Text("Это бета-версия, она не отражает финальную версию продукта", 10, Pal.Ink2, false, TextAlignment.Center));
        return box;
    }

    // плашка-«таблетка»: число или короткий текст, слева может быть пиксельная иконка
    public static Border Lcd(string text, double size = 15, string[]? icon = null, Color? iconColor = null, Color? accent = null)
    {
        var label = Text(text, size, Pal.Ink, true, TextAlignment.Center);
        label.VerticalTextAlignment = TextAlignment.Center;
        View content = label;
        if (icon != null)
        {
            var row = new HorizontalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.Center };
            row.Children.Add(new PixelIcon(icon, iconColor ?? Pal.Violet, size, accent ?? Pal.Orange));
            row.Children.Add(label);
            content = row;
        }
        return new Border
        {
            Content = content,
            BackgroundColor = Pal.Paper,
            Stroke = new SolidColorBrush(Pal.Line),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(20) },
            Padding = new Thickness(8, 10)
        };
    }

    // текст варианта ответа; withIcon — слева смысловая иконка термина
    public static View Term(string text, double size, bool withIcon)
    {
        var label = Text(text, size);
        var icon = withIcon ? Icons.ForTerm(text) : null;
        if (icon == null) return label;

        label.VerticalTextAlignment = TextAlignment.Center;
        var row = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }
        };
        row.Children.Add(new PixelIcon(icon, Pal.Violet, 20, Pal.Orange));
        Grid.SetColumn(label, 1);
        row.Children.Add(label);
        return row;
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

// элемент с мягкой тенью под ним. Тень рисуется внутри своих полей: Shadow из MAUI на Android выходит за край
// элемента, а родитель обрезает её по этому краю — под кнопками и карточками оставались прямоугольники.
// radius < 0 — круг
public class Lifted : Grid
{
    // поля под тень: по бокам и снизу, сверху почти без неё
    public static readonly Thickness Below = new(5, 2, 5, 9);

    public GraphicsView Shade { get; }
    readonly View body;

    public Lifted(View body, double radius, Color? color = null, Thickness? pad = null)
    {
        this.body = body;
        Padding = pad ?? Below;
        Shade = new GraphicsView
        {
            Drawable = new Drawable(Padding, radius, color ?? Pal.Ink),
            InputTransparent = true,
            BackgroundColor = Colors.Transparent
        };
        Children.Add(Shade);
        Children.Add(body);
    }

    // размер задаёт только элемент с полями: холст тени в Grid растягивался на всё свободное место,
    // и знак раздела на дороге становился квадратом во всю клетку
    protected override ILayoutManager CreateLayoutManager() => new Manager(this);

    sealed class Manager(Lifted owner) : ILayoutManager
    {
        public Size Measure(double widthConstraint, double heightConstraint)
        {
            var p = owner.Padding;
            var size = ((IView)owner.body).Measure(widthConstraint - p.HorizontalThickness, heightConstraint - p.VerticalThickness);
            IView self = owner;
            double w = LayoutManager.ResolveConstraints(widthConstraint, self.Width, size.Width + p.HorizontalThickness, self.MinimumWidth, self.MaximumWidth);
            double h = LayoutManager.ResolveConstraints(heightConstraint, self.Height, size.Height + p.VerticalThickness, self.MinimumHeight, self.MaximumHeight);
            ((IView)owner.Shade).Measure(w, h);
            return new Size(w, h);
        }

        public Size ArrangeChildren(Rect bounds)
        {
            var p = owner.Padding;
            // холст на всю площадь вместе с полями, элемент — внутри полей
            ((IView)owner.Shade).Arrange(bounds);
            ((IView)owner.body).Arrange(new Rect(bounds.X + p.Left, bounds.Y + p.Top,
                Math.Max(0, bounds.Width - p.HorizontalThickness), Math.Max(0, bounds.Height - p.VerticalThickness)));
            return bounds.Size;
        }
    }

    // несколько скруглённых слоёв, каждый шире и прозрачнее: к краю полей тень сходит на нет
    sealed class Drawable(Thickness pad, double radius, Color color) : IDrawable
    {
        const int Steps = 8;
        const float Strength = 0.2f;

        public void Draw(ICanvas canvas, RectF rect)
        {
            float w = rect.Width - (float)pad.HorizontalThickness, h = rect.Height - (float)pad.VerticalThickness;
            if (w <= 0 || h <= 0) return;
            // тень чуть ниже элемента, как от света сверху
            float drop = (float)Math.Max(0, (pad.Bottom - pad.Top) / 3);
            float reach = (float)Math.Max(1, Math.Min(Math.Min(pad.Left, pad.Right), Math.Min(pad.Top + drop, pad.Bottom - drop)));
            float corner = radius < 0 ? Math.Min(w, h) / 2 : (float)radius;
            canvas.FillColor = color.WithAlpha(Strength / Steps);
            for (int i = 1; i <= Steps; i++)
            {
                float grow = reach * i / Steps;
                canvas.FillRoundedRectangle((float)pad.Left - grow, (float)pad.Top + drop - grow, w + grow * 2, h + grow * 2, corner + grow);
            }
        }
    }
}
