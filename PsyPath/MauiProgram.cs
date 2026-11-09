namespace PsyPath;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("DejaVuSansMono.ttf", "Mono");
                fonts.AddFont("DejaVuSansMono-Bold.ttf", "MonoBold");
                fonts.AddFont("Nunito-Regular.ttf", "Body");
                fonts.AddFont("Nunito-ExtraBold.ttf", "BodyBold");
            });

        return builder.Build();
    }
}
