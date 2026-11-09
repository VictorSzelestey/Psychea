namespace PsyPath.UI;

// кассета с заставки (Resources/Splash/splash.svg), нарисованная кодом, чтобы её можно было оживить:
// Reel — угол поворота катушек в градусах
class CassetteDrawable : IDrawable
{
    public float Reel { get; set; }

    public void Draw(ICanvas canvas, RectF rect)
    {
        // координаты как в splash.svg (456×456), масштаб под размер вида
        float k = Math.Min(rect.Width, rect.Height) / 456f;
        canvas.SaveState();
        canvas.Translate(rect.X + (rect.Width - 456 * k) / 2, rect.Y + (rect.Height - 456 * k) / 2);
        canvas.Scale(k, k);

        canvas.FillColor = Pal.Ink;
        canvas.FillRoundedRectangle(88, 130, 280, 190, 16);
        canvas.FillColor = Pal.Paper;
        canvas.FillRoundedRectangle(110, 150, 236, 74, 6);
        canvas.FillColor = Pal.Amber;
        canvas.FillRectangle(110, 160, 236, 8);
        canvas.FillColor = Pal.Red;
        canvas.FillRectangle(110, 172, 236, 8);
        canvas.FillColor = Pal.Ink2;
        canvas.FillRoundedRectangle(190, 248, 76, 44, 6);

        foreach (float cx in new[] { 170f, 286f })
        {
            canvas.FillColor = Pal.Paper;
            canvas.FillCircle(cx, 270, 28);
            canvas.FillColor = Pal.Red;
            canvas.FillCircle(cx, 270, 11);
            // три зубца втулки: по ним видно, что катушка крутится
            canvas.StrokeColor = Pal.Ink;
            canvas.StrokeSize = 4;
            canvas.StrokeLineCap = LineCap.Round;
            for (int i = 0; i < 3; i++)
            {
                double a = (Reel + i * 120) * Math.PI / 180;
                canvas.DrawLine(cx + (float)Math.Cos(a) * 14, 270 + (float)Math.Sin(a) * 14,
                                cx + (float)Math.Cos(a) * 24, 270 + (float)Math.Sin(a) * 24);
            }
        }
        canvas.RestoreState();
    }
}

// заставка при запуске, ~1 с: кассета «подпрыгивает», катушки крутятся, снизу проявляется название
public class Intro : VerticalStackLayout
{
    readonly CassetteDrawable drawable = new();
    readonly GraphicsView cassette;
    readonly View title;
    readonly View stripes;

    public Intro()
    {
        Spacing = 10;
        VerticalOptions = LayoutOptions.Center;
        HorizontalOptions = LayoutOptions.Center;

        cassette = new GraphicsView { Drawable = drawable, WidthRequest = 200, HeightRequest = 200, HorizontalOptions = LayoutOptions.Center };
        title = new Glitch("ПСИХЕЯ", 38) { HorizontalOptions = LayoutOptions.Center };
        stripes = Ui.Stripes(3);
        stripes.WidthRequest = 170;
        stripes.HorizontalOptions = LayoutOptions.Center;

        // кассета чуть выше центра, как на системной заставке; название и полоски ниже
        Children.Add(cassette);
        Children.Add(title);
        Children.Add(stripes);

        if (!Anim.Reduced)
        {
            title.Opacity = 0;
            stripes.AnchorX = 0;
            stripes.ScaleX = 0;
        }
    }

    public async Task Play()
    {
        if (Anim.Reduced) return;

        // катушки: разгон и остановка за 900 мс
        new Animation(v =>
        {
            drawable.Reel = (float)v;
            cassette.Invalidate();
        }, 0, 540, Easing.CubicInOut).Commit(cassette, "reels", 16, 900);

        try
        {
            await cassette.Zoom(1.08, 170, Easing.CubicOut);
            await cassette.Zoom(1, 170, Easing.CubicIn);

            title.TranslationY = 14;
            new Animation(v => stripes.ScaleX = v, 0, 1, Easing.CubicOut).Commit(stripes, "sweep", 16, 320);
            await Task.WhenAll(title.Fade(1, 220), title.Move(0, 0, 260, Easing.CubicOut));

            // короткий «сбой» картинки, как на перемотке кассеты
            foreach (double x in new[] { -4.0, 3, 0 })
                await title.Move(x, 0, 35);
            await Task.Delay(120);
        }
        catch (ArgumentException)
        {
            // вид уже убран с экрана — заставка больше не нужна
        }
    }
}
