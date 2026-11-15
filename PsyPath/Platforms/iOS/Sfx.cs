using AVFoundation;
using Foundation;

namespace PsyPath.Ios;

// по плееру на звук: после PrepareToPlay короткий wav играет без задержки
public static class Sfx
{
    static readonly Dictionary<string, AVAudioPlayer> Players = new();

    public static void Load(string name, string path)
    {
        var player = AVAudioPlayer.FromUrl(NSUrl.FromFilename(path), out _);
        if (player == null) return;
        player.PrepareToPlay();
        Players[name] = player;
    }

    public static void Play(string name, float volume)
    {
        if (!Players.TryGetValue(name, out var player)) return;
        player.Volume = volume;
        player.CurrentTime = 0;
        player.Play();
    }
}
