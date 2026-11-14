namespace PsyPath.Services;

// фоновая музыка: Resources/Raw/music_menu.mp3 по кругу. Вкл/выкл, громкость
// и тишина в опросниках и карточках — в настройках
public static class Music
{
    public enum Zone { Menu, Quiz, Cards }

    static Zone zone = Zone.Menu;
    static bool ready;
    static bool asleep;

    // MediaPlayer, как и SoundPool, читает файл с диска, поэтому трек один раз копируется из APK в кэш
    public static async Task Init()
    {
#if ANDROID
        if (ready) return;
        try
        {
            string path = Path.Combine(FileSystem.CacheDirectory, $"music_menu_{AppInfo.Current.BuildString}.mp3");
            if (!File.Exists(path))
            {
                // недокачанная копия не должна остаться под рабочим именем
                string part = path + ".part";
                await using (var src = await FileSystem.OpenAppPackageFileAsync("music_menu.mp3"))
                await using (var dst = File.Create(part))
                    await src.CopyToAsync(dst);
                File.Move(part, path, true);
            }
            Droid.Bgm.Load(path);
            ready = true;
            Apply();
        }
        catch (Exception ex)
        {
            // без музыки приложение работает как обычно
            CrashLog.Write("Music", ex);
        }
#else
        await Task.CompletedTask;
#endif
    }

    // открылся экран: в опросниках и карточках музыка молчит, если так выбрано в настройках
    public static void Enter(Zone next)
    {
        zone = next;
        Apply();
    }

    // приложение ушло в фон и вернулось
    public static void Sleep()
    {
        asleep = true;
        Apply();
    }

    public static void Wake()
    {
        asleep = false;
        Apply();
    }

    // привести плеер к настройкам; вызывается после каждой их смены
    public static void Apply()
    {
        if (!ready) return;
        bool here = zone switch
        {
            Zone.Quiz => Settings.MusicInQuiz,
            Zone.Cards => Settings.MusicInCards,
            _ => true
        };
        bool play = Settings.MusicOn && Settings.MusicVolume > 0 && here && !asleep;
#if ANDROID
        try { Droid.Bgm.Set(play, Volume(Settings.MusicVolume)); }
        catch (Exception ex) { CrashLog.Write("Music", ex); }
#endif
    }

    // громкость на слух растёт нелинейно: квадрат делает шаги ползунка примерно равными
    static float Volume(int percent) => (float)Math.Pow(Math.Clamp(percent, 0, 100) / 100.0, 2);
}
