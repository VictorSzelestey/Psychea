using Android.Content;
using Android.Media;

namespace PsyPath.Droid;

// фоновая музыка: один MediaPlayer по кругу, включение и выключение — плавным изменением громкости.
// При смене устройства вывода (Bluetooth, наушники) MediaPlayer может уйти в состояние ошибки и замолчать
// до перезапуска приложения, поэтому плеер пересоздаётся с той же позиции трека
public static class Bgm
{
    const int FadeSteps = 10;
    const int FadeStepMs = 30;
    // при подключении Bluetooth система шлёт несколько событий подряд — пересоздаём один раз после них
    const int RouteSettleMs = 700;

    static MediaPlayer? player;
    static string? source;
    static float current;
    static int fadeId;
    static int routeId;
    // что просили в последний раз: после пересоздания плеер возвращается в это состояние
    static bool wantPlay;
    static float wantVolume;
    static DateTime lastErrorRestart;

    public static void Load(string path)
    {
        if (player != null) return;
        source = path;
        player = Create(path);
        var audio = (AudioManager?)Android.App.Application.Context.GetSystemService(Context.AudioService);
        audio?.RegisterAudioDeviceCallback(new RouteWatcher(), null);
    }

    static MediaPlayer Create(string path)
    {
        var p = new MediaPlayer();
        p.SetAudioAttributes(new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Game)!
            .SetContentType(AudioContentType.Music)!
            .Build()!);
        p.SetDataSource(path);
        p.Looping = true;
        p.Error += (s, e) =>
        {
            // true — ошибка обработана, иначе MediaPlayer ещё вызовет «трек закончился»
            e.Handled = true;
            Services.CrashLog.Write("Music", $"MediaPlayer error {e.What}/{e.Extra}");
            // повторная ошибка сразу после пересоздания — файл не читается, не крутимся по кругу
            if (DateTime.UtcNow - lastErrorRestart < TimeSpan.FromSeconds(5)) return;
            lastErrorRestart = DateTime.UtcNow;
            MainThread.BeginInvokeOnMainThread(Restart);
        };
        p.Prepare();
        return p;
    }

    public static void Set(bool play, float volume)
    {
        wantPlay = play;
        wantVolume = volume;
        if (player == null) return;
        try
        {
            if (play)
            {
                if (!player.IsPlaying)
                {
                    current = 0;
                    player.SetVolume(0, 0);
                    player.Start();
                }
                Fade(volume, pauseAfter: false);
            }
            else if (player.IsPlaying)
                Fade(0, pauseAfter: true);
        }
        catch (Java.Lang.IllegalStateException ex)
        {
            // плеер в состоянии ошибки: Start и Pause у него не работают
            Services.CrashLog.Write("Music", ex);
            Restart();
        }
    }

    // новый плеер с той же позиции; старый освобождается, идущее затухание отменяется
    static void Restart()
    {
        if (source == null) return;
        var old = player;
        player = null;
        fadeId++;
        int position = 0;
        try { position = old?.CurrentPosition ?? 0; } catch (Java.Lang.IllegalStateException) { }
        try { old?.Release(); } catch (Exception) { }

        try
        {
            var p = Create(source);
            if (position > 0) p.SeekTo(position);
            player = p;
            current = 0;
            Set(wantPlay, wantVolume);
        }
        catch (Exception ex)
        {
            Services.CrashLog.Write("Music", ex);
        }
    }

    static async void RouteChanged()
    {
        int id = ++routeId;
        await Task.Delay(RouteSettleMs);
        if (id != routeId || player == null) return;
        Services.CrashLog.Write("Music", "Audio route changed");
        Restart();
    }

    // новый вызов отменяет идущий: ползунок громкости и смена экранов не спорят друг с другом
    static async void Fade(float to, bool pauseAfter)
    {
        int id = ++fadeId;
        float from = current;
        try
        {
            for (int i = 1; i <= FadeSteps; i++)
            {
                await Task.Delay(FadeStepMs);
                if (id != fadeId || player == null) return;
                current = from + (to - from) * i / FadeSteps;
                player.SetVolume(current, current);
            }
            if (pauseAfter && player?.IsPlaying == true) player.Pause();
        }
        catch (Exception ex)
        {
            Services.CrashLog.Write("Music", ex);
        }
    }

    // подключили или отключили наушники, колонку, Bluetooth
    class RouteWatcher : AudioDeviceCallback
    {
        // сразу после регистрации система перечисляет уже подключённые устройства — это не смена
        bool initial = true;

        public override void OnAudioDevicesAdded(AudioDeviceInfo[]? addedDevices)
        {
            if (initial)
            {
                initial = false;
                return;
            }
            if (Matters(addedDevices)) MainThread.BeginInvokeOnMainThread(RouteChanged);
        }

        public override void OnAudioDevicesRemoved(AudioDeviceInfo[]? removedDevices)
        {
            if (Matters(removedDevices)) MainThread.BeginInvokeOnMainThread(RouteChanged);
        }

        // только устройства вывода: микрофон гарнитуры приходит отдельным событием
        static bool Matters(AudioDeviceInfo[]? devices) => devices?.Any(d => d.IsSink) == true;
    }
}
