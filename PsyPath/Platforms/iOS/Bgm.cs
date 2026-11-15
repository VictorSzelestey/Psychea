using AVFoundation;
using Foundation;

namespace PsyPath.Ios;

// фоновая музыка по кругу. Ambient: не глушит другие приложения и молчит в беззвучном режиме
public static class Bgm
{
    static AVAudioPlayer? player;

    public static void Load(string path)
    {
        var session = AVAudioSession.SharedInstance();
        session.SetCategory(AVAudioSessionCategory.Ambient, AVAudioSessionCategoryOptions.MixWithOthers, out _);
        session.SetActive(true, out _);

        player = AVAudioPlayer.FromUrl(NSUrl.FromFilename(path), out var error)
            ?? throw new IOException(error?.LocalizedDescription ?? "AVAudioPlayer не открыл трек");
        player.NumberOfLoops = -1;
        player.PrepareToPlay();

        // звонок или Siri ставят плеер на паузу; после них музыка возвращается по настройкам
        AVAudioSession.Notifications.ObserveInterruption((_, e) =>
        {
            if (e.InterruptionType == AVAudioSessionInterruptionType.Ended)
                MainThread.BeginInvokeOnMainThread(Services.Music.Apply);
        });
    }

    public static void Set(bool play, float volume)
    {
        if (player == null) return;
        player.Volume = volume;
        if (play && !player.Playing) player.Play();
        else if (!play && player.Playing) player.Pause();
    }
}
