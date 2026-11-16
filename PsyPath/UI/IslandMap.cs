using Microsoft.Maui.Controls.Shapes;
using PsyPath.Services;

namespace PsyPath.UI;

// карта на главной: четыре острова 2×2 на розовой воде и манул в лодке посередине.
// Закрытый остров накрыт своей серой копией и замком; при открытии замок падает, а серая копия тает.
// Нажатие на открытый остров — Opened, на закрытый — всплывающая подпись поверх карты
public class IslandMap : ContentView
{
    readonly AbsoluteLayout layer = new();
    readonly Dictionary<int, (Image Gray, View Lock, Label Caption)> locked = new();
    readonly Image boat;
    bool revealing;

    public event Action<Island>? Opened;

    public IslandMap()
    {
        layer.Children.Add(Place(new Image { Source = "map_sea.jpg", Aspect = Aspect.Fill, InputTransparent = true }, new Rect(0, 0, 1, 1)));

        foreach (var island in Islands.All)
        {
            bool open = Islands.IsOpen(island);
            // остров, который открылся, пока карта не показывалась, рисуется закрытым: замок упадёт в Reveal
            bool showLocked = !open || !AppState.P.IslandsShown.Contains(island.Number) && island.Number > 1;

            var gray = new Image { Source = $"island_{island.Image}_gray.png", Aspect = Aspect.Fill, InputTransparent = true, Opacity = showLocked ? 1 : 0 };
            layer.Children.Add(Place(gray, island.Box));

            var caption = Ui.Text(Caption(island, !showLocked), 10, Pal.Ink, true, TextAlignment.Center);
            var pill = new Border
            {
                Content = caption,
                BackgroundColor = Color.FromArgb("#FFFBFD").WithAlpha(0.92f),
                Stroke = new SolidColorBrush(Color.FromArgb("#E9C9D6")),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
                Padding = new Thickness(8, 3),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End,
                InputTransparent = true
            };
            // подпись внизу острова; цвета свои, а не темы: карта всегда на светлой воде
            caption.TextColor = Color.FromArgb("#4A2E4F");
            var box = island.Box;
            var pillHost = new Grid { InputTransparent = true };
            pillHost.Children.Add(pill);
            layer.Children.Add(Place(pillHost, new Rect(box.X, box.Y, box.Width, box.Height)));

            var lockBadge = LockBadge(island.Soon);
            lockBadge.Opacity = showLocked ? 1 : 0;
            layer.Children.Add(Place(lockBadge, Center(box, 0.2)));

            if (showLocked) locked[island.Number] = (gray, lockBadge, caption);

            var hit = new BoxView { Color = Colors.Transparent };
            SemanticProperties.SetDescription(hit, island.Name + (open ? ", открыт" : ", закрыт"));
            AutomationProperties.SetIsInAccessibleTree(hit, true);
            var target = island;
            Ui.OnTap(hit, () => Tap(target));
            layer.Children.Add(Place(hit, Inset(box, 0.12)));
        }

        boat = new Image { Source = "map_manul.png", Aspect = Aspect.Fill };
        SemanticProperties.SetDescription(boat, "Манул в лодке");
        Ui.OnTap(boat, () =>
        {
            Sound.Play("tap");
            _ = boat.Bounce();
            Float("Выбери остров — поплыли!", Islands.Boat);
        });
        layer.Children.Add(Place(boat, Islands.Boat));
        // лодка покачивается на волнах несколько раз при появлении карты
        Anim.Idle(boat, 3, 2600, cycles: 4);

        var frame = new Border
        {
            Content = layer,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(26) },
            Shadow = Ui.SoftShadow()
        };
        Content = frame;
        // карта квадратная: высота равна ширине
        SizeChanged += (s, e) =>
        {
            if (Width > 0 && Math.Abs(HeightRequest - Width) > 0.5) HeightRequest = Width;
        };
    }

    static string Caption(Island island, bool open)
    {
        if (island.Soon) return island.Name.ToUpperInvariant() + " · СКОРО";
        if (!open) return island.Name.ToUpperInvariant();
        var lessons = Islands.LessonsOf(island);
        return $"{island.Name.ToUpperInvariant()} · {lessons.Count(AppState.IsPassed)}/{lessons.Count}";
    }

    // пропорциональные координаты AbsoluteLayout: x и y — доля свободного места, а не стороны, поэтому пересчёт
    static View Place(View v, Rect r)
    {
        double x = r.Width >= 1 ? 0 : r.X / (1 - r.Width);
        double y = r.Height >= 1 ? 0 : r.Y / (1 - r.Height);
        AbsoluteLayout.SetLayoutFlags(v, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.All);
        AbsoluteLayout.SetLayoutBounds(v, new Rect(x, y, r.Width, r.Height));
        return v;
    }

    // квадрат со стороной size (доля карты) в центре острова, чуть выше середины — там вершины, а не берег
    static Rect Center(Rect box, double size) =>
        new(box.X + box.Width / 2 - size / 2, box.Y + box.Height * 0.42 - size / 2, size, size);

    static Rect Inset(Rect box, double k) =>
        new(box.X + box.Width * k, box.Y + box.Height * k, box.Width * (1 - 2 * k), box.Height * (1 - 2 * k));

    static View LockBadge(bool soon)
    {
        var icon = new PixelIcon(Icons.Lock, Color.FromArgb("#FFFFFF"), 26, Color.FromArgb("#7A6E86"))
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        return new Border
        {
            Content = icon,
            BackgroundColor = soon ? Color.FromArgb("#B9A15C") : Color.FromArgb("#7A6E86"),
            Stroke = new SolidColorBrush(Color.FromArgb("#FFFFFF")),
            StrokeThickness = 3,
            StrokeShape = new Ellipse(),
            InputTransparent = true,
            Shadow = new Shadow { Brush = new SolidColorBrush(Colors.Black), Opacity = 0.25f, Radius = 10, Offset = new Point(0, 4) }
        };
    }

    void Tap(Island island)
    {
        if (island.Soon)
        {
            Sound.Play("bad");
            if (locked.TryGetValue(island.Number, out var soon)) _ = soon.Lock.Shake();
            Float("Пока закрыто", island.Box);
            return;
        }
        if (!Islands.IsOpen(island))
        {
            Sound.Play("bad");
            if (locked.TryGetValue(island.Number, out var l)) _ = l.Lock.Shake();
            var prev = Islands.Find(island.Number - 1);
            Float(prev != null ? $"Пройди «{prev.Name}», чтобы открыть" : "Пока закрыто", island.Box);
            return;
        }
        Opened?.Invoke(island);
    }

    // подпись всплывает над местом нажатия и тает — не окно, игру она не останавливает
    void Float(string text, Rect over)
    {
        var label = Ui.Text(text, 13, Color.FromArgb("#FFFFFF"), true, TextAlignment.Center);
        var bubble = new Border
        {
            Content = label,
            BackgroundColor = Color.FromArgb("#4A2E4F").WithAlpha(0.88f),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
            Padding = new Thickness(12, 6),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true
        };
        var host = new Grid { InputTransparent = true };
        host.Children.Add(bubble);
        // по ширине подпись может выйти за остров, но не за карту
        layer.Children.Add(Place(host, new Rect(Math.Clamp(over.X + over.Width / 2 - 0.35, 0, 0.3), over.Y + over.Height * 0.3, 0.7, 0.14)));
        _ = FloatAway(host);
    }

    async Task FloatAway(View v)
    {
        v.Opacity = 0;
        v.TranslationY = 10;
        await Task.WhenAll(v.Fade(1, 180), v.Move(0, 0, 220, Easing.CubicOut));
        await Task.Delay(Anim.Reduced ? 1600 : 1100);
        await Task.WhenAll(v.Fade(0, 420), v.Move(0, -26, 420, Easing.CubicIn));
        layer.Children.Remove(v);
    }

    // открытые, но ещё не показанные острова: замок трясётся и падает, серая копия тает, подпись меняется.
    // Потом остров считается показанным
    public async Task Reveal()
    {
        var fresh = Islands.NotShown();
        if (fresh.Count == 0 || revealing) return;
        revealing = true;
        await Task.Delay(Anim.Reduced ? 0 : 450);
        foreach (var island in fresh)
        {
            if (locked.TryGetValue(island.Number, out var l))
            {
                await l.Lock.Shake();
                Sound.Play("win");
                var fall = Task.WhenAll(
                    l.Lock.Move(0, 90, 620, Easing.CubicIn),
                    l.Lock.Spin(38, 620),
                    l.Lock.Fade(0, 620));
                var color = l.Gray.Fade(0, 1100);
                await Task.WhenAll(fall, color);
                l.Caption.Text = Caption(island, true);
                var theme = Islands.ThemeOf(island);
                Float($"Открыто: {island.Name}! Тема «{theme.Name}» — в магазине", island.Box);
                locked.Remove(island.Number);
            }
            AppState.P.IslandsShown.Add(island.Number);
        }
        AppState.Save();
        revealing = false;
    }
}
