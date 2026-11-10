using Microsoft.Extensions.Logging;
using PsyPath.Services;

namespace PsyPath;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        CrashLog.Initialize(Path.Combine(FileSystem.AppDataDirectory, "Psychea", "logs"),
            $"version={AppInfo.Current.VersionString}; build={AppInfo.Current.BuildString}; runtime={Environment.Version}");
        var builder = MauiApp.CreateBuilder();
        builder.Logging.AddProvider(new CrashLogProvider());
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
