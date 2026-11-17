global using Microsoft.Maui.Controls;
global using Microsoft.Maui.Graphics;
using System.Text.Json;
using PsyPath.UI;

// Exercise the real MAUI view without an Android device.
foreach (var (image, count, compact) in new[] { ("reason", 25, true), ("jungle", 24, true), ("night", 18, false), ("unknown", 25, true), ("reason", 0, true), ("reason", 1, true) })
{
    var road = new IslandRoad(image, compact);
    var stops = Enumerable.Range(0, count).Select(_ => new BoxView()).ToArray();
    double diameter = compact ? 44 : 56;
    foreach (var stop in stops) road.Add(stop, diameter);
    foreach (double width in compact ? new[] { 284d, 324, 339, 376, 700 } : new[] { 324d, 339, 376, 700 })
    {
        road.Layout(new Rect(0, 0, width, width * 3));
        double height = road.HeightRequest;
        road.Layout(new Rect(0, 0, width, height));
        var layer = (AbsoluteLayout)road.Content;
        var pictures = layer.Children.OfType<Image>().ToArray();
        Check(pictures.Length == 1 && Math.Abs(height - width * 3) < .01, "Road must use one original picture at 1:3, without repetition");
        foreach (var picture in pictures)
        {
            var bounds = AbsoluteLayout.GetLayoutBounds(picture);
            double imageRatio = bounds.Height * height / (bounds.Width * width);
            Check(Math.Abs(imageRatio - 3) < 0.001,
                $"{image}/{count}: image stretched to {imageRatio:F2}:1 instead of 3:1");
        }
        var centers = stops.Select(s => AbsoluteLayout.GetLayoutBounds(s).Center).ToArray();
        Check(centers.All(p => p.X >= 0 && p.X <= width && p.Y >= 0 && p.Y <= height), "Stop outside road");
        Check(centers.Zip(centers.Skip(1)).All(p => p.First.Y > p.Second.Y), "Lesson order changed");
        for (int i = 0; i < centers.Length; i++)
            for (int j = i + 1; j < centers.Length; j++)
                Check(centers[i].Distance(centers[j]) >= diameter - .01, $"{image}/{count}/{width}: lesson circles overlap");
        if (image == "night") Check(pictures.Length == 1 && Math.Abs(height - width * 3) < .01, "Night road changed");
        if (args.Length == 1 && width == 339 && image != "unknown" && count > 1)
        {
            Directory.CreateDirectory(args[0]);
            File.WriteAllText(Path.Combine(args[0], image + ".json"), JsonSerializer.Serialize(new {
                width, height, compact, diameter, points = centers.Select(p => new[] { p.X, p.Y }).ToArray()
            }));
        }
        if (count > 0)
        {
            road.Show(stops[0], new BoxView());
            Check(road.IsOpen(stops[0]), "Lesson card did not open");
            road.Hide();
            Check(!road.IsOpen(stops[0]), "Lesson card did not close");
        }
    }
    Console.WriteLine($"PASS {image}: {count} stops, phone and landscape widths");
}

foreach (var (name, hash) in new[] {
    ("reason", "a672c0cfd15eec739e5e1d049dbc9350d81d6b168582a3a2072c5ddabc86341e"),
    ("jungle", "450fe3a68dfe49fbc4adceebfa737e306dacd750df208e122af61fa6fae5482c"),
    ("night", "8711e4ad7eb64fe343e5e6dae6a9f789679c5243aa730b22a9a377354ae70f05") })
{
    var path = Path.Combine(AppContext.BaseDirectory, "../../../../../PsyPath/Resources/Images", $"road_{name}.png");
    Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).Equals(hash, StringComparison.OrdinalIgnoreCase), $"{name}: original 1.7.4 background changed");
}
Console.WriteLine("PASS all road images match 1.7.4 byte for byte");

using var lessons = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "lessons.json")));
var sections = lessons.RootElement.GetProperty("sections").EnumerateArray().Where(s => s.GetProperty("id").GetInt32() is >= 17 and <= 19).ToArray();
Check(sections.Length == 3, "Physiology needs three sections");
int total = 0;
var types = new HashSet<string>();
foreach (var section in sections)
{
    var items = section.GetProperty("lessons").EnumerateArray().ToArray();
    Check(items.Length is >= 6 and <= 8, "Physiology section needs 6–8 lessons");
    foreach (var lesson in items)
    {
        Check(lesson.GetProperty("theory").GetArrayLength() == 4, "Lesson needs four theory cards");
        var tasks = lesson.GetProperty("tasks").EnumerateArray().ToArray();
        Check(tasks.Length == 10, "Lesson needs ten tasks");
        foreach (var task in tasks) types.Add(task.GetProperty("type").GetString()!);
        total++;
    }
}
Check(types.IsSupersetOf(new[] { "choice", "truefalse", "match", "order", "fill", "case" }), "Missing exercise type");
Console.WriteLine($"PASS physiology: {total} lessons, {total * 4} theory cards, {total * 10} tasks");

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

namespace PsyPath.UI
{
    // App-specific interaction/animation helpers; layout uses real MAUI controls.
    static class Ui
    {
        public static void OnTap(View view, Action action) => view.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(action) });
    }
    static class Animations
    {
        public static Task Fade(this View view, double opacity, uint duration) { view.Opacity = opacity; return Task.CompletedTask; }
        public static Task Zoom(this View view, double scale, uint duration, Easing easing) { view.Scale = scale; return Task.CompletedTask; }
    }
}
