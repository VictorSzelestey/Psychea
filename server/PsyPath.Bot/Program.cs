using System.Net;
using PsyPath.Bot;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

// настройки — переменные окружения (на сервере файл /opt/psypath-bot/bot.env)
static string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v.Trim() : null;

// на ПК те же настройки можно положить в bot.env рядом с проектом: строки КЛЮЧ=значение
static void LoadEnvFile()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    var candidates = new List<string> { Path.Combine(Directory.GetCurrentDirectory(), "bot.env") };
    for (int i = 0; i < 5 && dir != null; i++, dir = dir.Parent)
        candidates.Add(Path.Combine(dir.FullName, "bot.env"));

    var file = candidates.FirstOrDefault(File.Exists);
    if (file == null) return;
    foreach (var line in File.ReadAllLines(file))
    {
        int eq = line.IndexOf('=');
        if (line.TrimStart().StartsWith('#') || eq <= 0) continue;
        string key = line[..eq].Trim();
        // переменная окружения важнее файла
        if (Env(key) == null) Environment.SetEnvironmentVariable(key, line[(eq + 1)..].Trim());
    }
    Console.WriteLine("настройки из " + file);
}

LoadEnvFile();

var token = Env("BOT_TOKEN") ?? throw new InvalidOperationException("Не задан BOT_TOKEN — токен от @BotFather");
var apiUrl = Env("PSYPATH_API_URL") ?? "http://psypath_api:5080";
var proxy = Env("BOT_PROXY");
// свой адрес вместо api.telegram.org, например Cloudflare Worker, который пересылает запросы в Telegram
var tgUrl = Env("BOT_API_URL");
var dataDir = Env("BOT_DATA") ?? "data";
PsyPath.Bot.Game.SetTimeZone(Env("BOT_TZ") ?? "Asia/Novosibirsk");

// с сервера НГТУ api.telegram.org закрыт — тогда нужен прокси: http://host:port или socks5://host:port
var handler = new SocketsHttpHandler();
if (proxy != null)
{
    handler.Proxy = new WebProxy(proxy);
    handler.UseProxy = true;
}
var tg = new TelegramBotClient(new TelegramBotClientOptions(token, tgUrl), new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(100) });

var api = new PsyApi(apiUrl);
var content = new ContentStore(api);
var sessions = new SessionStore(dataDir);
var bot = new PsyBot(tg, api, content, sessions);

// без using: ProcessExit срабатывает уже после выхода из Main
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

User me;
try { me = await tg.GetMe(cts.Token); }
catch (Telegram.Bot.Exceptions.RequestException ex) when (ex.InnerException is HttpRequestException or TaskCanceledException)
{
    Console.WriteLine($"Нет связи с {tgUrl ?? "api.telegram.org"}{(proxy != null ? " через прокси " + proxy : "")}: {ex.InnerException.Message}");
    Console.WriteLine("Telegram закрыт в этой сети. Включи VPN на весь компьютер, впиши в bot.env BOT_PROXY=socks5://host:port (или http://host:port) или BOT_API_URL — см. README.");
    return 1;
}
await content.Get();
if (!content.Loaded) Console.WriteLine($"API {apiUrl} пока недоступен, контент загрузится при первом запросе");

await tg.SetMyCommands(new[]
{
    new BotCommand { Command = "lesson", Description = "Продолжить уроки" },
    new BotCommand { Command = "path", Description = "Путь уроков" },
    new BotCommand { Command = "quiz", Description = "Опросники" },
    new BotCommand { Command = "ref", Description = "Справочник" },
    new BotCommand { Command = "profile", Description = "Профиль" },
    new BotCommand { Command = "cancel", Description = "Отменить ввод" },
    new BotCommand { Command = "help", Description = "Все команды" }
}, cancellationToken: cts.Token);

tg.StartReceiving(bot.HandleUpdate, bot.HandleError,
    new ReceiverOptions { AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery } }, cts.Token);
Console.WriteLine($"@{me.Username} v{PsyBot.Version} запущен, API {apiUrl}{(proxy != null ? ", через прокси" : "")}");

try { await Task.Delay(Timeout.Infinite, cts.Token); }
catch (TaskCanceledException) { }
sessions.Save();
Console.WriteLine("бот остановлен");
return 0;
