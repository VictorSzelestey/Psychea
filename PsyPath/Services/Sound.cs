#if ANDROID
using Native = PsyPath.Droid;
#elif IOS
using Native = PsyPath.Ios;
#endif

namespace PsyPath.Services;

// звуковые эффекты: файлы Resources/Raw/sfx_*.wav. Выключаются в настройках
public static class Sound
{
    static readonly string[] Names = { "tap", "ok", "bad", "win", "flip" };

    // SoundPool читает только файлы с диска, поэтому звуки один раз копируются из APK в кэш
    public static async Task Init()
    {
#if ANDROID || IOS
        try
        {
            foreach (var name in Names)
            {
                string path = Path.Combine(FileSystem.CacheDirectory, $"sfx_{name}_{AppInfo.Current.BuildString}.wav");
                if (!File.Exists(path))
                {
                    await using var src = await FileSystem.OpenAppPackageFileAsync($"sfx_{name}.wav");
                    await using var dst = File.Create(path);
                    await src.CopyToAsync(dst);
                }
                Native.Sfx.Load(name, path);
            }
        }
        catch (Exception)
        {
            // без звука приложение работает как обычно
        }
#else
        await Task.CompletedTask;
#endif
    }

    public static void Play(string name)
    {
        if (!Settings.Sound) return;
#if ANDROID || IOS
        try { Native.Sfx.Play(name, 0.6f); }
        catch (Exception) { }
#endif
    }
}
