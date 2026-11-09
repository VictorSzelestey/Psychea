namespace PsyPath.UI;

// заставка при запуске, ~1 с: лого «подпрыгивает», снизу проявляется название
public class Intro : VerticalStackLayout
{
    readonly Image logo;
    readonly View title;
    readonly View stripes;

    public Intro()
    {
        Spacing = 10;
        VerticalOptions = LayoutOptions.Center;
        HorizontalOptions = LayoutOptions.Center;

        logo = new Image { Source = "appiconfg.png", WidthRequest = 200, HeightRequest = 200, HorizontalOptions = LayoutOptions.Center };
        title = new Glitch("ПСИХЕЯ", 38) { HorizontalOptions = LayoutOptions.Center };
        stripes = Ui.Stripes(3);
        stripes.WidthRequest = 170;
        stripes.HorizontalOptions = LayoutOptions.Center;

        Children.Add(logo);
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

        try
        {
            await logo.Zoom(1.08, 170, Easing.CubicOut);
            await logo.Zoom(1, 170, Easing.CubicIn);

            title.TranslationY = 14;
            new Animation(v => stripes.ScaleX = v, 0, 1, Easing.CubicOut).Commit(stripes, "sweep", 16, 320);
            await Task.WhenAll(title.Fade(1, 220), title.Move(0, 0, 260, Easing.CubicOut));
            await Task.Delay(120);
        }
        catch (ArgumentException)
        {
            // вид уже убран с экрана — заставка больше не нужна
        }
    }
}
