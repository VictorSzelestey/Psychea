using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using PsyPath.Models;

namespace PsyPath.Bot;

// что бот ждёт от пользователя следующим текстовым сообщением
public enum Step
{
    None,
    LoginName,
    LoginPassword,
    RegLogin,
    RegPassword,
    RegName,
    DeletePassword
}

public class ChatSession
{
    // сохраняется в файл: вход переживает перезапуск бота
    public long ChatId { get; set; }
    public string? Token { get; set; }
    public ApiUser? User { get; set; }

    // только в памяти
    [JsonIgnore] public ProgressData? P { get; set; }
    // прогресс изменён, но сервер его ещё не принял
    [JsonIgnore] public bool Dirty { get; set; }
    [JsonIgnore] public Step Step { get; set; }
    [JsonIgnore] public Dictionary<string, string> Draft { get; } = new();
    [JsonIgnore] public LessonRun? Lesson { get; set; }
    [JsonIgnore] public QuizRun? Quiz { get; set; }
    [JsonIgnore] public SemaphoreSlim Lock { get; } = new(1, 1);
    // номер прохождения: кнопки старого урока или опросника по нему отбрасываются
    [JsonIgnore] public int Seq { get; set; }

    [JsonIgnore] public bool LoggedIn => Token != null && User != null;

    public void Logout()
    {
        Token = null;
        User = null;
        P = null;
        Lesson = null;
        Quiz = null;
        Step = Step.None;
        Draft.Clear();
    }
}

// чаты и их токены в одном JSON-файле; файл доступен только владельцу процесса
public class SessionStore
{
    readonly string path;
    readonly ConcurrentDictionary<long, ChatSession> chats = new();
    readonly object fileLock = new();

    public SessionStore(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        path = Path.Combine(dataDir, "sessions.json");
        if (!File.Exists(path)) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<ChatSession>>(File.ReadAllText(path)) ?? new();
            foreach (var s in list) chats[s.ChatId] = s;
            Console.WriteLine($"сессий загружено: {list.Count(x => x.LoggedIn)}");
        }
        catch (JsonException)
        {
            Console.WriteLine("sessions.json повреждён, все входят заново");
        }
    }

    public ChatSession Get(long chatId) => chats.GetOrAdd(chatId, id => new ChatSession { ChatId = id });

    public void Save()
    {
        lock (fileLock)
        {
            var list = chats.Values.Where(x => x.LoggedIn).ToList();
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(list));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(tmp, path, true);
        }
    }
}
