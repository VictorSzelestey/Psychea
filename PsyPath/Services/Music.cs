using PsyPath.UI;

namespace PsyPath.Services;

// фоновая музыка: Resources/Raw/music_menu.mp3 по кругу, у темы со своей музыкой («Джунгли», «Космос») — её трек.
// На острове играет музыка его темы, на карте — выбранной в магазине.
// Вкл/выкл, громкость и тишина в опросниках и карточках — в настройках, они общие для всех треков
public static class Music
{
    public enum Zone { Menu, Quiz, Cards, JungleDungeon }

    const string DefaultTrack = "music_menu.mp3";
    const string DungeonTrack = "music_jungle_dungeon.mp3";
    static readonly SemaphoreSlim switching = new(1, 1);

    static Zone zone = Zone.Menu;
    static bool ready;
    static bool asleep;
    // какой трек сейчас загружен в плеер
    static string loaded = "";

    static string Track => zone == Zone.JungleDungeon ? DungeonTrack : Islands.CurrentTheme.Music ?? DefaultTrack;

    public static async Task Init()
    {
#if ANDROID
        await switching.WaitAsync();
        try
        {
            if (ready) return;
            string track = Track;
            Droid.Bgm.Load(await Cached(track));
            loaded = track;
            ready = true;
            Apply();
        }
        catch (Exception ex)
        {
            // без музыки приложение работает как обычно
            CrashLog.Write("Music", ex);
        }
        finally { switching.Release(); }
        if (ready && loaded != Track) await UseTheme();
#else
        await Task.CompletedTask;
#endif
    }

    // тема сменилась (магазин, остров, вход, выход): у темы может быть своя музыка
    public static async Task UseTheme()
    {
#if ANDROID
        if (!ready || loaded == Track) return;
        await switching.WaitAsync();
        try
        {
            // Навигация может сменить трек, пока предыдущий копируется из APK.
            while (loaded != Track)
            {
                string track = Track;
                string path = await Cached(track);
                if (track != Track) continue;
                Droid.Bgm.Switch(path);
                loaded = track;
            }
            Apply();
        }
        catch (Exception ex)
        {
            CrashLog.Write("Music", ex);
        }
        finally { switching.Release(); }
#else
        await Task.CompletedTask;
#endif
    }

#if ANDROID
    // MediaPlayer, как и SoundPool, читает файл с диска, поэтому трек один раз копируется из APK в кэш
    static async Task<string> Cached(string track)
    {
        string path = Path.Combine(FileSystem.CacheDirectory, $"{Path.GetFileNameWithoutExtension(track)}_{AppInfo.Current.BuildString}.mp3");
        if (File.Exists(path)) return path;
        // недокачанная копия не должна остаться под рабочим именем
        string part = path + ".part";
        await using (var src = await FileSystem.OpenAppPackageFileAsync(track))
        await using (var dst = File.Create(part))
            await src.CopyToAsync(dst);
        File.Move(part, path, true);
        return path;
    }
#endif

    // открылся экран: в опросниках и карточках музыка молчит, если так выбрано в настройках
    public static void Enter(Zone next)
    {
        zone = next;
        Apply();
        _ = UseTheme();
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
        bool matchingDungeon = (zone == Zone.JungleDungeon) == (loaded == DungeonTrack);
        bool play = Settings.MusicOn && Settings.MusicVolume > 0 && here && !asleep && matchingDungeon;
#if ANDROID
        try { Droid.Bgm.Set(play, Volume(Settings.MusicVolume)); }
        catch (Exception ex) { CrashLog.Write("Music", ex); }
#endif
    }

    // громкость на слух растёт нелинейно: квадрат делает шаги ползунка примерно равными
    static float Volume(int percent) => (float)Math.Pow(Math.Clamp(percent, 0, 100) / 100.0, 2);
}
