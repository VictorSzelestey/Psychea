using PsyPath.Services;

namespace PsyPath.UI;

// заставка рубежа серии поверх экрана итогов: огонёк вырастает и горит, вверх летят искры, число дней набегает.
// Уходит по нажатию или сама через 3,5 с. При «Уменьшить движение» сразу виден готовый кадр
public static class StreakBurst
{
    static readonly string[][] Frames = { Icons.Flame, Icons.FlameB, Icons.FlameC };
    const int FrameMs = 160;
    const int BurnMs = 2400;
    const int StayMs = 3500;

    // цвет огонька растёт вместе с серией: оранжевый, жёлтый, бирюзовый, фиолетовый, золотой
    public static (Color Main, Color Accent) FlameColors(int days) => days switch
    {
        >= 100 => (Color.FromArgb("#D99A0B"), Color.FromArgb("#FFF0B3")),
        >= 50 => (Color.FromArgb("#8A5CD0"), Color.FromArgb("#E6D6FF")),
        >= 30 => (Pal.Teal, Color.FromArgb("#C6F4F5")),
        >= 10 => (Color.FromArgb("#F2A516"), Color.FromArgb("#FFE9A6")),
        _ => (Pal.Orange, Pal.Yellow)
    };

    public static void Show(Grid root, int days)
    {
        var (main, accent) = FlameColors(days);
        bool closed = false;

        var flame = new PixelIcon(Frames[0], main, 120, accent) { HorizontalOptions = LayoutOptions.Center };
        var number = Ui.Text(Anim.Reduced ? days.ToString() : "0", 48, Pal.Ink, true, TextAlignment.Center);
        int next = Streak.Next(days);
        int reward = Streak.Reward(days);

        var stack = new VerticalStackLayout { Spacing = 8 };
        stack.Children.Add(flame);
        stack.Children.Add(number);
        stack.Children.Add(Ui.Text("ДНЕЙ ПОДРЯД!", 18, Pal.Ink, true, TextAlignment.Center));
        var prize = Ui.Lcd($"+{reward}", 16, Icons.Crystal, Pal.Teal, Pal.Cyan);
        prize.HorizontalOptions = LayoutOptions.Center;
        prize.Padding = new Thickness(16, 8);
        stack.Children.Add(prize);
        stack.Children.Add(Ui.Text($"До {next} дней — ещё {next - days}", 13, Pal.Ink2, true, TextAlignment.Center));
        stack.Children.Add(Ui.Text("Нажми, чтобы продолжить", 11, Pal.Ink2, false, TextAlignment.Center));

        var card = Ui.Card(stack, Pal.Paper, 24, 24);
        card.WidthRequest = 290;
        card.HorizontalOptions = LayoutOptions.Center;
        card.VerticalOptions = LayoutOptions.Center;
        card.Shadow = Ui.SoftShadow();
        SemanticProperties.SetDescription(card, $"Серия {days} дней подряд, плюс {reward} {Crystals.Word(reward)}");

        var layer = new Grid { BackgroundColor = Color.FromRgba(0, 0, 0, 0.45) };
        layer.Children.Add(card);
        root.Children.Add(layer);

        async Task Close()
        {
            if (closed) return;
            closed = true;
            await layer.Fade(0, 150);
            root.Children.Remove(layer);
        }
        Ui.OnTap(layer, () => _ = Close());
        Sound.Play("win");

        if (!Anim.Reduced)
        {
            layer.Opacity = 0;
            card.Scale = 0.7;
            flame.Scale = 0;
            _ = layer.Fade(1, 180);
            _ = Pop(card, 1.06, 260);
            _ = Pop(flame, 1.2, 320);
            Anim.CountUp(number, days, v => v.ToString(), 700);
            _ = Burn(flame, main, accent, () => closed);
            Sparks(layer, main, accent);
        }

        root.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(StayMs), () => _ = Close());
    }

    // вырасти чуть больше нужного и осесть: «пружинка»
    static async Task Pop(VisualElement v, double over, uint ms)
    {
        await v.Zoom(over, ms, Easing.CubicOut);
        await v.Zoom(1, 140, Easing.CubicIn);
    }

    static async Task Burn(PixelIcon flame, Color main, Color accent, Func<bool> closed)
    {
        for (int t = 0, i = 0; t < BurnMs && !closed(); t += FrameMs)
        {
            await Task.Delay(FrameMs);
            i = (i + 1) % Frames.Length;
            flame.Drawable = new PixelDrawable(Frames[i], main, accent);
            flame.Invalidate();
        }
    }

    // квадратные пиксели-искры разлетаются вверх от огонька и гаснут
    static void Sparks(Grid layer, Color main, Color accent)
    {
        var rnd = new Random();
        var colors = new[] { main, accent, Pal.Yellow };
        for (int i = 0; i < 16; i++)
        {
            double size = 6 + rnd.Next(5);
            var spark = new BoxView
            {
                Color = colors[rnd.Next(colors.Length)],
                WidthRequest = size,
                HeightRequest = size,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                TranslationX = rnd.Next(-50, 51),
                TranslationY = -60,
                InputTransparent = true
            };
            layer.Children.Add(spark);
            int delay = rnd.Next(0, 900);
            uint ms = (uint)(900 + rnd.Next(600));
            double x = spark.TranslationX + rnd.Next(-90, 91);
            double y = -170 - rnd.Next(140);
            layer.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(delay), async () =>
            {
                await Task.WhenAll(spark.Move(x, y, ms, Easing.CubicOut), spark.Fade(0, ms));
                layer.Children.Remove(spark);
            });
        }
    }
}
