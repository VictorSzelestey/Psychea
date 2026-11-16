namespace PsyPath.UI;

// заставка при запуске, ~1 с: лого «подпрыгивает», снизу проявляется название
public class Intro : VerticalStackLayout
{
    readonly Image logo;
    readonly Label title;

    public Intro()
    {
        Spacing = 10;
        VerticalOptions = LayoutOptions.Center;
        HorizontalOptions = LayoutOptions.Center;

        logo = new Image { Source = "logo_psi.png", WidthRequest = 200, HeightRequest = 200, HorizontalOptions = LayoutOptions.Center };
        // раньше здесь был Heading — это Grid со «звёздной» колонкой, которая растягивается
        // на всю ширину экрана даже при HorizontalOptions = Center, из-за чего текст внутри
        // (он выровнен по левому краю) уезжал влево. Обычный Label с центрированием текста
        // и центрированием самого Label — без этого бага.
        title = Ui.Text("ПСИХЕЯ", 38, Pal.Ink, true, TextAlignment.Center);
        title.HorizontalOptions = LayoutOptions.Center;

        Children.Add(logo);
        Children.Add(title);

        if (!Anim.Reduced)
        {
            title.Opacity = 0;
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
            await Task.WhenAll(title.Fade(1, 220), title.Move(0, 0, 260, Easing.CubicOut));
            await Task.Delay(120);
        }
        catch (ArgumentException)
        {
            // вид уже убран с экрана — заставка больше не нужна
        }
    }
}
