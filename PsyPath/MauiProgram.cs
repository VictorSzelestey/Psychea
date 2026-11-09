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
            });

        return builder.Build();
    }
}
