using Android.Media;

namespace PsyPath.Droid;

// фоновая музыка: один MediaPlayer по кругу, включение и выключение — плавным изменением громкости
public static class Bgm
{
    const int FadeSteps = 10;
    const int FadeStepMs = 30;

    static MediaPlayer? player;
    static float current;
    static int fadeId;

    public static void Load(string path)
    {
        if (player != null) return;
        var p = new MediaPlayer();
        p.SetAudioAttributes(new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Game)!
            .SetContentType(AudioContentType.Music)!
            .Build()!);
        p.SetDataSource(path);
        p.Looping = true;
        p.Prepare();
        player = p;
    }

    public static void Set(bool play, float volume)
    {
        if (player == null) return;
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
            if (pauseAfter && player.IsPlaying) player.Pause();
        }
        catch (Exception ex)
        {
            Services.CrashLog.Write("Music", ex);
        }
    }
}
