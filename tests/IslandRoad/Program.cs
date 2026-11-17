global using Microsoft.Maui.Controls;
global using Microsoft.Maui.Graphics;
using System.Text.Json;
using PsyPath.UI;

// Exercise the real MAUI view without an Android device.
foreach (var (image, count) in new[] { ("reason", 29), ("jungle", 27), ("night", 18), ("unknown", 100), ("reason", 0), ("reason", 1) })
{
    var road = new IslandRoad(image);
    var stops = Enumerable.Range(0, count).Select(_ => new BoxView()).ToArray();
    foreach (var stop in stops) road.Add(stop, 56);
    foreach (double width in new[] { 339d, 376, 700 })
    {
        road.Layout(new Rect(0, 0, width, width * 3));
        double height = road.HeightRequest;
        road.Layout(new Rect(0, 0, width, height));
        var layer = (AbsoluteLayout)road.Content;
        var pictures = layer.Children.OfType<Image>().ToArray();
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
        Check(centers.Zip(centers.Skip(1)).All(p => p.First.Distance(p.Second) >= 56), "Lesson circles overlap");
        if (image == "night") Check(pictures.Length == 1 && Math.Abs(height - width * 3) < .01, "Night road changed");
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
