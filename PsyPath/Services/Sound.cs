namespace PsyPath.Services;

// звуки «кассеты»: файлы Resources/Raw/sfx_*.wav. Выключаются в настройках
public static class Sound
{
    static readonly string[] Names = { "tap", "ok", "bad", "win", "flip" };

    // SoundPool читает только файлы с диска, поэтому звуки один раз копируются из APK в кэш
    public static async Task Init()
    {
#if ANDROID
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
                Droid.Sfx.Load(name, path);
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
#if ANDROID
        try { Droid.Sfx.Play(name, 0.6f); }
        catch (Exception) { }
#endif
    }
}
