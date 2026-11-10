using System.Globalization;
using System.Text.Json;

namespace PsyPath.Services;

public record ReleaseNote(Version Version, DateOnly? Date, IReadOnlyList<string> Changes);

// Один формат списка изменений для предложения обновления и истории внутри APK.
public static class ReleaseNotes
{
    public static IReadOnlyList<string> ReadChanges(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("changelog", out var items)
            || items.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
        return items.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()?.Trim() ?? "")
            .Where(x => x.Length > 0)
            .ToArray();
    }

    public static string Format(IReadOnlyList<string> changes) => changes.Count == 0
        ? "Описание изменений для этой версии пока не опубликовано."
        : string.Join("\n", changes.Select(x => "• " + x));

    public static async Task<IReadOnlyList<ReleaseNote>> Load()
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync("changelog.json");
        using var reader = new StreamReader(stream);
        return ParseHistory(await reader.ReadToEndAsync(), AppInfo.Current.Version);
    }

    public static IReadOnlyList<ReleaseNote> ParseHistory(string json, Version installed)
    {
        using var doc = JsonDocument.Parse(json);
        var notes = new List<ReleaseNote>();
        void Add(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("version", out var v) || v.ValueKind != JsonValueKind.String
                || !Version.TryParse(v.GetString(), out var version)) return;
            version = Normalize(version);
            if (version > Normalize(installed) || notes.Any(n => n.Version == version)) return;
            DateOnly? date = null;
            if (item.TryGetProperty("date", out var d) && d.ValueKind == JsonValueKind.String
                && DateOnly.TryParseExact(d.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                date = parsed;
            notes.Add(new ReleaseNote(version, date, ReadChanges(item)));
        }
        Add(doc.RootElement);
        if (doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("history", out var history) && history.ValueKind == JsonValueKind.Array)
            foreach (var item in history.EnumerateArray()) Add(item);
        return notes.OrderByDescending(n => n.Version).ToArray();
    }

    public static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
