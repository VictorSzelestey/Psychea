using System.IO.Compression;
using System.Text.Json;
using SkiaSharp;

namespace PsyPath.UI;

internal sealed class DungeonClip(int width, int height, DungeonClip.Frame[] frames) : IDisposable
{
    internal sealed record Frame(int EndsAt, int X, int Y, SKImage? Pixels);

    public int FrameAt(double seconds)
    {
        if (seconds < 0) return -1;
        int ms = (int)(seconds * 1000);
        return Array.FindIndex(frames, frame => frame.EndsAt > ms);
    }

    public void Draw(SKCanvas canvas, int index, SKImageInfo viewport)
    {
        var frame = frames[index];
        if (frame.Pixels is not { } image) return;
        float scale = Math.Max((float)viewport.Width / width, (float)viewport.Height / height);
        float left = (viewport.Width - width * scale) / 2 + frame.X * scale;
        float top = (viewport.Height - height * scale) / 2 + frame.Y * scale;
        canvas.DrawImage(image, new SKRect(left, top, left + image.Width * scale, top + image.Height * scale),
            new SKSamplingOptions(SKFilterMode.Linear));
    }

    public static async Task<DungeonClip> Decode(Stream source, string name, CancellationToken token)
    {
        using var memory = new MemoryStream();
        await source.CopyToAsync(memory, token).ConfigureAwait(false);
        memory.Position = 0;
        using var archive = new ZipArchive(memory);
        using var manifest = archive.GetEntry("frames.json")!.Open();
        using var document = JsonDocument.Parse(manifest);
        var root = document.RootElement;
        var frames = new List<Frame>();
        using var frameBytes = new MemoryStream();
        int end = 0;
        try
        {
            foreach (var item in root.GetProperty("frames").EnumerateArray())
            {
                token.ThrowIfCancellationRequested();
                end += item.GetProperty("duration").GetInt32();
                if (!item.TryGetProperty("file", out var nameValue))
                {
                    frames.Add(new Frame(end, 0, 0, null));
                    continue;
                }
                string entryName = nameValue.GetString()!;
                using var stream = archive.GetEntry(entryName)!.Open();
                // A ZIP entry stream cannot seek, which the Skia WebP decoder needs.
                frameBytes.SetLength(0);
                await stream.CopyToAsync(frameBytes, token).ConfigureAwait(false);
                using var bitmap = SKBitmap.Decode(frameBytes.GetBuffer().AsSpan(0, (int)frameBytes.Length)) ??
                    throw new InvalidDataException($"{name}/{entryName}: WebP decode failed");
                bitmap.SetImmutable();
                frames.Add(new Frame(end, item.GetProperty("x").GetInt32(), item.GetProperty("y").GetInt32(),
                    SKImage.FromBitmap(bitmap)));
            }
            return new DungeonClip(root.GetProperty("width").GetInt32(), root.GetProperty("height").GetInt32(), frames.ToArray());
        }
        catch
        {
            foreach (var frame in frames) frame.Pixels?.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        foreach (var frame in frames) frame.Pixels?.Dispose();
    }
}
