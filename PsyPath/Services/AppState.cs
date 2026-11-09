using System.Globalization;
using System.Text.Json;
using PsyPath.Models;

namespace PsyPath.Services;

public record AchDef(string Id, string Title, string Desc);

public static class AppState
{
    public static LessonsFile Lessons { get; private set; } = new();
    public static List<Questionnaire> Questionnaires { get; private set; } = new();
    public static RatingFile Rating { get; private set; } = new();
    public static List<RefEntry> Reference { get; private set; } = new();
    public static ProgressData P { get; set; } = new();

    // растёт при каждом сохранении: вкладки по нему понимают, что пора перестроиться
    public static int Version { get; private set; }

    static readonly JsonSerializerOptions Opt = new() { PropertyNameCaseInsensitive = true };

    // те же настройки, которыми читается контент: админка проверяет ими правки
    public static JsonSerializerOptions JsonOptions => Opt;
    static readonly Random Rnd = new();

    public static readonly AchDef[] AchDefs =
    {
        new("first", "Первый шаг", "Пройди первый урок"),
        new("streak3", "3 дня подряд", "Занимайся 3 дня подряд"),
        new("streak7", "7 дней подряд", "Занимайся 7 дней подряд"),
        new("perfect", "Без ошибок", "Пройди урок без единой ошибки"),
        new("ten", "10 уроков", "Пройди 10 уроков"),
        new("lv3", "3 уровень", "Достигни 3 уровня"),
        new("lv5", "5 уровень", "Достигни 5 уровня"),
        new("quiz", "Первый опросник", "Пройди любой опросник"),
    };

    public static async Task LoadAsync()
    {
        await Api.RestoreSession();

        // свежий контент с сервера; без связи ждём не дольше 3 с и берём кэш или встроенные JSON
        var update = UpdateContentCache();
        await Task.WhenAny(update, Task.Delay(3000));
        await LoadContent();

        var json = Preferences.Default.Get("progress", "");
        if (!string.IsNullOrEmpty(json))
        {
            try { P = JsonSerializer.Deserialize<ProgressData>(json, Opt) ?? new ProgressData(); }
            catch { P = new ProgressData(); }
        }
    }

    public static async Task LoadContent()
    {
        Lessons = await ReadContent<LessonsFile>("lessons");
        Questionnaires = (await ReadContent<QuestionnaireFile>("questionnaires")).Items;
        Rating = await ReadContent<RatingFile>("rating");
        Reference = (await ReadContent<RefFile>("reference")).Entries;
        Version++;
    }

    static string CacheDir => Path.Combine(FileSystem.AppDataDirectory, "content");

    // контент, скачанный с сервера, важнее встроенного в APK
    static async Task<T> ReadContent<T>(string name) where T : new()
    {
        var path = Path.Combine(CacheDir, name + ".json");
        if (File.Exists(path))
        {
            try { return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path), Opt) ?? new T(); }
            catch { /* битый кэш — берём встроенный */ }
        }
        return await ReadAsset<T>(name + ".json");
    }

    public static void WriteCache(string name, string json, int version)
    {
        Directory.CreateDirectory(CacheDir);
        File.WriteAllText(Path.Combine(CacheDir, name + ".json"), json);
        Preferences.Default.Set("content_v_" + name, version);
    }

    // после правки или отката в админке: свежий документ с сервера сразу в приложение
    public static async Task RefreshContent(string name)
    {
        var c = await Api.Content(name);
        if (!c.Ok) return;
        WriteCache(name, c.Body.GetProperty("data").GetRawText(), c.Body.GetProperty("version").GetInt32());
        await LoadContent();
    }

    // качаем только документы, у которых на сервере версия новее
    static async Task UpdateContentCache()
    {
        var versions = await Api.ContentVersions();
        if (!versions.Ok) return;
        foreach (var doc in versions.Body.EnumerateObject())
        {
            int server = doc.Value.GetInt32();
            if (server <= Preferences.Default.Get("content_v_" + doc.Name, 0)) continue;
            var c = await Api.Content(doc.Name);
            if (c.Ok) WriteCache(doc.Name, c.Body.GetProperty("data").GetRawText(), server);
        }
    }

    static async Task<T> ReadAsset<T>(string name) where T : new()
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync(name);
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<T>(text, Opt) ?? new T();
    }

    public static void Save()
    {
        Preferences.Default.Set("progress", JsonSerializer.Serialize(P));
        Version++;
        if (Api.LoggedIn) Sync.MarkDirty();
        SchedulePush();
        Reminders.Update();
    }

    public static void ResetAll()
    {
        pushDelay?.Cancel();
        Preferences.Default.Remove("progress");
        P = new ProgressData();
        Version++;
        Sync.Clear();
        Reminders.Update();
    }

    static CancellationTokenSource? pushDelay;

    // на сервер уходит не каждое сохранение, а последнее за 2 с; без связи — когда сеть появится (Sync)
    static async void SchedulePush()
    {
        if (!Api.LoggedIn) return;
        pushDelay?.Cancel();
        var cts = pushDelay = new CancellationTokenSource();
        try { await Task.Delay(2000, cts.Token); }
        catch (TaskCanceledException) { return; }
        await Sync.Flush();
    }

    public static async Task PushNow()
    {
        pushDelay?.Cancel();
        await Sync.Flush();
    }

    // после входа: на сервере есть прогресс — берём его, новый аккаунт — отправляем текущий
    public static async Task AdoptServerProgress()
    {
        var me = await Api.Me();
        if (!me.Ok) return;
        var server = me.Body.GetProperty("progress");
        if (server.ValueKind == JsonValueKind.Object && server.EnumerateObject().Any())
        {
            P = server.Deserialize<ProgressData>(Opt) ?? new ProgressData();
            Preferences.Default.Set("progress", JsonSerializer.Serialize(P));
            Version++;
        }
        else
        {
            await Api.PushProgress(P);
        }
    }

    // 1 -> 100, 2 -> 300 ... 5 -> 900
    public static int QuestionRating(int d) => d * 200 - 100;

    public static int LevelOf(double rating)
    {
        if (rating >= 800) return 5;
        if (rating >= 600) return 4;
        if (rating >= 400) return 3;
        if (rating >= 200) return 2;
        return 1;
    }

    public static int Level => LevelOf(P.Rating);

    public static LevelInfo LevelInfoOf(int level)
    {
        return Rating.Levels.FirstOrDefault(x => x.Level == level)
               ?? new LevelInfo { Level = level, Name = "Уровень " + level, Emoji = "★", RatingFrom = (level - 1) * 200, RatingTo = level * 200 - 1 };
    }

    public static double UpdateRating(double r, int difficulty, bool correct, double k = 32)
    {
        double qr = QuestionRating(difficulty);
        double expected = 1.0 / (1.0 + Math.Pow(10, (qr - r) / 400.0));
        double next = r + k * ((correct ? 1.0 : 0.0) - expected);
        return Math.Clamp(next, 0, 1000);
    }

    public static int UnlockLevel(int sectionId)
    {
        var u = Rating.SectionUnlock.FirstOrDefault(x => x.Section == sectionId);
        return u?.UnlockLevel ?? 1;
    }

    public static bool IsDone(Lesson l) => P.Done.Contains(l.Id);
    public static bool IsMastered(Lesson l) => !IsDone(l) && l.RecommendedLevel <= Level - 2;
    public static bool IsPassed(Lesson l) => IsDone(l) || IsMastered(l);

    public static List<TaskItem> PickTasks(Lesson lesson, int size = 8)
    {
        int level = Level;
        var chosen = lesson.Tasks
            .OrderBy(t => Math.Abs(t.Difficulty - level) + Rnd.NextDouble() * 0.9)
            .Take(size)
            .ToList();
        return chosen.OrderBy(t => t.Difficulty).ThenBy(_ => Rnd.Next()).ToList();
    }

    public static int DailyGoalXp => P.DailyGoalMin switch
    {
        5 => 20,
        10 => 40,
        15 => 60,
        20 => 80,
        _ => 40
    };

    public static int TodayXp => P.XpDate == DateTime.Today.ToString("yyyy-MM-dd") ? P.TodayXp : 0;

    public static void AddXp(int n)
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        if (P.XpDate != today)
        {
            P.XpDate = today;
            P.TodayXp = 0;
        }
        P.TodayXp += n;
        P.TotalXp += n;
    }

    public static int DisplayStreak
    {
        get
        {
            if (!DateTime.TryParseExact(P.LastActive, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last))
                return 0;
            int diff = (DateTime.Today - last.Date).Days;
            return diff <= 1 ? P.Streak : 0;
        }
    }

    public static void RegisterActivity()
    {
        var today = DateTime.Today;
        if (DateTime.TryParseExact(P.LastActive, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last))
        {
            int diff = (today - last.Date).Days;
            if (diff == 0) return;
            P.Streak = diff == 1 ? P.Streak + 1 : 1;
        }
        else
        {
            P.Streak = 1;
        }
        P.LastActive = today.ToString("yyyy-MM-dd");
    }

    public static List<string> CheckAchievements()
    {
        var earned = new List<string>();

        void Give(string id, bool cond)
        {
            if (!cond || P.Achievements.Contains(id)) return;
            P.Achievements.Add(id);
            earned.Add(AchDefs.First(x => x.Id == id).Title);
        }

        Give("first", P.Done.Count >= 1);
        Give("streak3", P.Streak >= 3);
        Give("streak7", P.Streak >= 7);
        Give("perfect", P.PerfectLessons >= 1);
        Give("ten", P.Done.Count >= 10);
        Give("lv3", Level >= 3);
        Give("lv5", Level >= 5);
        Give("quiz", P.History.Count >= 1);
        return earned;
    }

    public static QOutcome Score(Questionnaire q, List<int> answers)
    {
        var values = q.Scales.ToDictionary(x => x.Key, _ => 0);
        var maxes = q.Scales.ToDictionary(x => x.Key, _ => 0);

        if (q.IsYesNo)
        {
            foreach (var sc in q.Scales)
            {
                maxes[sc.Key] = sc.Yes.Count + sc.No.Count;
                foreach (int n in sc.Yes)
                    if (n >= 1 && n <= answers.Count && answers[n - 1] == 1) values[sc.Key]++;
                foreach (int n in sc.No)
                    if (n >= 1 && n <= answers.Count && answers[n - 1] == 0) values[sc.Key]++;
            }
        }
        else
        {
            for (int i = 0; i < q.Questions.Count && i < answers.Count; i++)
            {
                var opts = q.Questions[i].Options;
                foreach (var g in opts.GroupBy(o => o.Scale))
                    if (maxes.ContainsKey(g.Key)) maxes[g.Key] += g.Max(o => o.Points);
                int a = answers[i];
                if (a >= 0 && a < opts.Count && values.ContainsKey(opts[a].Scale))
                    values[opts[a].Scale] += opts[a].Points;
            }
        }

        var res = new QOutcome();
        foreach (var sc in q.Scales)
        {
            int v = values[sc.Key];
            var range = sc.Ranges.OrderBy(r => r.Max).FirstOrDefault(r => v <= r.Max) ?? sc.Ranges.LastOrDefault();
            res.Scores.Add(new QScore { Scale = sc, Value = v, Max = maxes[sc.Key], Range = range });
        }

        if (q.Mode == "dominant")
        {
            var ordered = res.Scores.OrderByDescending(x => x.Value).ToList();
            res.Main = ordered[0];
            if (ordered.Count > 1 && ordered[1].Value > 0 && ordered[1].Value >= 0.8 * ordered[0].Value)
                res.Second = ordered[1];
            res.Headline = res.Main.Scale.Name;
            if (res.Second != null) res.Headline += " + " + res.Second.Scale.Name;
        }
        else
        {
            res.Headline = string.Join("; ", res.Scores.Select(x => x.Scale.Name + ": " + (x.Range?.Title ?? x.Value.ToString())));
        }

        if (q.Mode == "quadrants" && res.Scores.Count >= 2) ApplyQuadrant(q, res);
        return res;
    }

    // тип по двум первым шкалам: в BlendTexts ключ из двух букв, h — шкала выше границы Split, l — нет;
    // значение — название и описание, разделённые переводом строки
    static void ApplyQuadrant(Questionnaire q, QOutcome res)
    {
        string key = (res.Scores[0].Value > q.Split ? "h" : "l") + (res.Scores[1].Value > q.Split ? "h" : "l");
        if (!q.BlendTexts.TryGetValue(key, out var text)) return;
        var parts = text.Split('\n', 2);
        res.TypeTitle = parts[0];
        res.TypeText = parts.Length > 1 ? parts[1] : "";
        res.Headline = res.TypeTitle;
    }

    public static string BlendText(Questionnaire q, QOutcome r)
    {
        if (r.Main == null || r.Second == null) return "";
        var pair = new[] { r.Main.Scale, r.Second.Scale }
            .OrderBy(x => q.Scales.IndexOf(x))
            .Select(x => x.Key)
            .ToArray();
        string template = q.BlendTemplate
            .Replace("{main}", r.Main.Scale.Name)
            .Replace("{secondary}", r.Second.Scale.Name);
        string extra = q.BlendTexts.TryGetValue(pair[0] + "+" + pair[1], out var t) ? t : "";
        return (template + " " + extra).Trim();
    }

    public static void SaveResult(Questionnaire q, QOutcome r)
    {
        var item = new QHistoryItem
        {
            Id = q.Id,
            Title = q.Title,
            Date = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
            Headline = r.Headline,
            Lines = r.Scores.Select(x => $"{x.Scale.Name}: {x.Value}/{x.Max}").ToList()
        };
        P.History.Insert(0, item);
        if (P.History.Count > 50) P.History.RemoveRange(50, P.History.Count - 50);
        AddXp(15);
        RegisterActivity();
        CheckAchievements();
        Save();

        Sync.Questionnaire(q.Id, r.Headline,
            r.Scores.Select(x => new { scale = x.Scale.Key, value = x.Value, max = x.Max }).ToList());
    }
}
