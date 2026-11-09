using System.Text.Json;
using PsyPath.Models;

namespace PsyPath.Bot;

// весь контент одной версией: урок, начатый до правки в админке, доигрывается на старом
public class Content
{
    public LessonsFile Lessons { get; init; } = new();
    public List<Questionnaire> Questionnaires { get; init; } = new();
    public RatingFile Rating { get; init; } = new();
    public List<RefEntry> Reference { get; init; } = new();
}

// контент берётся с сервера и перечитывается, когда админ сохраняет правку
public class ContentStore(PsyApi api)
{
    static readonly JsonSerializerOptions Opt = new() { PropertyNameCaseInsensitive = true };
    static readonly TimeSpan CheckEvery = TimeSpan.FromMinutes(2);

    readonly Dictionary<string, int> versions = new();
    readonly SemaphoreSlim gate = new(1, 1);
    DateTime checkedAt = DateTime.MinValue;

    public Content Current { get; private set; } = new();
    public bool Loaded { get; private set; }

    public async Task<Content> Get()
    {
        if (Loaded && DateTime.UtcNow - checkedAt < CheckEvery) return Current;
        await gate.WaitAsync();
        try
        {
            if (!Loaded || DateTime.UtcNow - checkedAt >= CheckEvery) await Refresh();
        }
        finally { gate.Release(); }
        return Current;
    }

    async Task Refresh()
    {
        var v = await api.ContentVersions();
        // нет связи — остаёмся на том, что уже загружено
        if (!v.Ok) return;
        checkedAt = DateTime.UtcNow;

        var server = v.Body.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetInt32());
        if (Loaded && server.All(x => versions.TryGetValue(x.Key, out var old) && old == x.Value)) return;

        var docs = new Dictionary<string, JsonElement>();
        foreach (var name in new[] { "lessons", "questionnaires", "rating", "reference" })
        {
            var c = await api.Content(name);
            if (!c.Ok) return;
            docs[name] = c.Body.GetProperty("data");
        }

        Current = new Content
        {
            Lessons = docs["lessons"].Deserialize<LessonsFile>(Opt) ?? new(),
            Questionnaires = (docs["questionnaires"].Deserialize<QuestionnaireFile>(Opt) ?? new()).Items,
            Rating = docs["rating"].Deserialize<RatingFile>(Opt) ?? new(),
            Reference = (docs["reference"].Deserialize<RefFile>(Opt) ?? new()).Entries
        };
        foreach (var kv in server) versions[kv.Key] = kv.Value;
        Loaded = true;
        Console.WriteLine($"{DateTime.Now:HH:mm:ss} контент: " + string.Join(", ", server.Select(x => $"{x.Key} v{x.Value}")));
    }
}
