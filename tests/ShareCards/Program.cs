using PsyPath.Services;
using SkiaSharp;

string output = args.Length > 0 ? Path.GetFullPath(args[0]) : AppContext.BaseDirectory;
Directory.CreateDirectory(output);

byte[] Render(LessonShareResult result)
{
    using var mascot = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "share_mascot.png"));
    using var regular = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "regular.ttf"));
    using var bold = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "bold.ttf"));
    return LessonShareCardRenderer.Render(result, mascot, regular, bold);
}

void Check(bool ok, string message)
{
    if (!ok) throw new Exception(message);
    Console.WriteLine("PASS: " + message);
}

var reference = new LessonShareResult(100, 100, 53);
Check(reference.XpText == "+100" && reference.AccuracyText == "100%" && reference.RatingText == "+53", "reference statistics");
Check(new LessonShareResult(0, -5, -12).XpText == "+0" && new LessonShareResult(0, -5, -12).RatingText == "-12", "zero XP and negative rating");
Check(new LessonShareResult(0, -5, 0).AccuracyText == "0%" && new LessonShareResult(0, 150, 0).AccuracyText == "100%", "accuracy limits");

byte[] png = Render(reference);
File.WriteAllBytes(Path.Combine(output, "psychea-share-lesson.png"), png);
using var bitmap = SKBitmap.Decode(png);
Check(bitmap.Width == LessonShareCardRenderer.Width && bitmap.Height == LessonShareCardRenderer.Height, "fixed high-resolution PNG");
Check(bitmap.GetPixel(0, 0).Alpha == 255, "opaque paper background");
Check(bitmap.GetPixel(457, 82).Red > 220 && bitmap.GetPixel(162, 1234).Red > 220,
    "Psi logos render without missing-font boxes");

bool Different(SKBitmap a, SKBitmap b, int left, int top, int right, int bottom)
{
    for (int y = top; y < bottom; y++)
        for (int x = left; x < right; x++)
            if (a.GetPixel(x, y) != b.GetPixel(x, y)) return true;
    return false;
}

foreach (var (data, left, label) in new[]
{
    (reference with { Xp = 275 }, 56, "XP"),
    (reference with { Accuracy = 63 }, 428, "accuracy"),
    (reference with { RatingDelta = -17 }, 800, "rating"),
})
{
    using var changed = SKBitmap.Decode(Render(data));
    Check(Different(bitmap, changed, left, 1020, left + 344, 1105), "real " + label + " changes the exported number");
    Check(!Different(bitmap, changed, 0, 0, 1200, 180), "statistics do not change branding");
}

byte[] practice = Render(new LessonShareResult(25, 72, -17, true));
File.WriteAllBytes(Path.Combine(output, "psychea-share-practice.png"), practice);
using var practiceBitmap = SKBitmap.Decode(practice);
Check(Different(bitmap, practiceBitmap, 50, 160, 1150, 270), "practice has its own heading");
Check(Render(new LessonShareResult(int.MaxValue, 0, int.MinValue)).Length > 0, "large signed numbers render");
Check(LessonShareCardRenderer.DownloadUrl.StartsWith("https://github.com/VictorSzelestey/Psychea/"), "download link uses the project repository");
Console.WriteLine("Preview: " + Path.Combine(output, "psychea-share-lesson.png"));
