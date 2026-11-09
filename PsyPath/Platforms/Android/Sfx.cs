using Android.Media;

namespace PsyPath.Droid;

// короткие звуки интерфейса: SoundPool держит их в памяти и играет без задержки
public static class Sfx
{
    static SoundPool? pool;
    static readonly Dictionary<string, int> Ids = new();

    public static void Load(string name, string path)
    {
        pool ??= new SoundPool.Builder()
            .SetMaxStreams(3)!
            .SetAudioAttributes(new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Game)!
                .SetContentType(AudioContentType.Sonification)!
                .Build())!
            .Build();
        Ids[name] = pool!.Load(path, 1);
    }

    public static void Play(string name, float volume)
    {
        if (pool != null && Ids.TryGetValue(name, out int id))
            pool.Play(id, volume, volume, 1, 0, 1f);
    }
}
