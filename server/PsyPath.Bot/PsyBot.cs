using System.Text;
using System.Text.Json;
using PsyPath.Models;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using static PsyPath.Bot.Html;

namespace PsyPath.Bot;

// экраны приложения для обычного пользователя, перенесённые в чат; админка остаётся в приложении
public class PsyBot(ITelegramBotClient tg, PsyApi api, ContentStore content, SessionStore sessions)
{
    const string MenuLesson = "▶ Урок";
    const string MenuPath = "Путь";
    const string MenuQuiz = "Опросники";
    const string MenuRef = "Справочник";
    const string MenuProfile = "Профиль";

    static readonly ReplyKeyboardMarkup Menu = new(new[]
    {
        new[] { new KeyboardButton(MenuLesson), new KeyboardButton(MenuPath) },
        new[] { new KeyboardButton(MenuQuiz), new KeyboardButton(MenuRef), new KeyboardButton(MenuProfile) }
    })
    { ResizeKeyboard = true };

    static readonly JsonSerializerOptions Opt = new() { PropertyNameCaseInsensitive = true };

    public static string Version
    {
        get
        {
            var v = typeof(PsyBot).Assembly.GetName().Version;
            return v == null ? "1.0" : $"{v.Major}.{v.Minor}";
        }
    }

    static readonly string[] Intro =
    {
        "Привет! Я Манул, твой проводник по психологии. Сразу скажу: я не ставлю диагнозы. Это учебный тренажёр.",
        "Запомни: темперамент врождённый, а характер формируется в жизни. Это разные вещи.",
        "Проходи опросники, отвечай на вопросы без лимита попыток и держи серию дней. Всё просто!"
    };

    // ---------- приём обновлений ----------

    // разные чаты обрабатываются параллельно, сообщения одного чата — строго по очереди
    public Task HandleUpdate(ITelegramBotClient client, Update u, CancellationToken ct)
    {
        var chat = u.Message?.Chat ?? u.CallbackQuery?.Message?.Chat;
        if (chat == null || chat.Type != ChatType.Private) return Task.CompletedTask;

        _ = Task.Run(() => Process(chat.Id, u));
        return Task.CompletedTask;
    }

    public async Task Process(long chatId, Update u)
    {
        var s = sessions.Get(chatId);
        await s.Lock.WaitAsync();
        try
        {
            if (u.CallbackQuery is { } cq) await OnCallback(s, cq);
            else if (u.Message is { Text: { } } m) await OnText(s, m);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} чат {s.ChatId}: {ex}");
            try { await Send(s, E("Что-то пошло не так. Попробуй ещё раз или нажми /start.")); }
            catch { /* Telegram недоступен — сообщить некуда */ }
        }
        finally { s.Lock.Release(); }
    }

    public async Task HandleError(ITelegramBotClient client, Exception ex, HandleErrorSource source, CancellationToken ct)
    {
        Console.WriteLine($"{DateTime.Now:HH:mm:ss} Telegram ({source}): {ex.Message}");
        // без связи с Telegram не долбим его запросами
        try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
        catch (TaskCanceledException) { }
    }

    // ---------- текст ----------

    async Task OnText(ChatSession s, Message m)
    {
        string text = m.Text!.Trim();
        string cmd = text.Split(' ', '@')[0].ToLowerInvariant();

        if (cmd is "/cancel" || text.Equals("отмена", StringComparison.OrdinalIgnoreCase))
        {
            bool waiting = s.Step != Step.None;
            ResetStep(s);
            await Send(s, E(waiting ? "Отменил." : "Нечего отменять."), s.LoggedIn ? Menu : null);
            return;
        }

        if (s.Step != Step.None && !text.StartsWith('/'))
        {
            await OnStepInput(s, m, text);
            return;
        }
        ResetStep(s);

        if (cmd is "/start" or "/menu")
        {
            await Home(s);
            return;
        }
        if (cmd == "/help")
        {
            await Help(s);
            return;
        }
        if (!s.LoggedIn)
        {
            await Welcome(s);
            return;
        }

        switch (text)
        {
            case MenuLesson: await ContinueLesson(s); return;
            case MenuPath: await ShowPath(s); return;
            case MenuQuiz: await ShowQuizzes(s); return;
            case MenuRef: await ShowReference(s); return;
            case MenuProfile: await ShowProfile(s); return;
        }
        switch (cmd)
        {
            case "/lesson": await ContinueLesson(s); return;
            case "/path": await ShowPath(s); return;
            case "/quiz": await ShowQuizzes(s); return;
            case "/ref": await ShowReference(s); return;
            case "/profile": await ShowProfile(s); return;
        }

        if (text.StartsWith('/')) await Help(s);
        else await SearchReference(s, text);
    }

    static void ResetStep(ChatSession s)
    {
        s.Step = Step.None;
        s.Draft.Clear();
    }

    async Task OnStepInput(ChatSession s, Message m, string text)
    {
        switch (s.Step)
        {
            case Step.LoginName:
                s.Draft["login"] = text;
                s.Step = Step.LoginPassword;
                await Send(s, E("Пароль. Сообщение с ним я сразу удалю из чата."));
                break;

            case Step.LoginPassword:
            {
                await TryDelete(s, m);
                string login = s.Draft.GetValueOrDefault("login", "");
                ResetStep(s);
                var r = await api.Login(login, text);
                if (!r.Ok)
                {
                    await Send(s, E("Не вышло: " + r.Error), AuthButtons());
                    return;
                }
                await AfterAuth(s, r.Body);
                break;
            }

            case Step.RegLogin:
                s.Draft["login"] = text;
                s.Step = Step.RegPassword;
                await Send(s, E("Пароль, не короче 6 символов. Сообщение с ним я сразу удалю."));
                break;

            case Step.RegPassword:
                await TryDelete(s, m);
                s.Draft["password"] = text;
                s.Step = Step.RegName;
                await Send(s, E("Как к тебе обращаться? Отправь имя или «-», чтобы пропустить."));
                break;

            case Step.RegName:
            {
                string login = s.Draft.GetValueOrDefault("login", "");
                string password = s.Draft.GetValueOrDefault("password", "");
                ResetStep(s);
                var r = await api.Register(login, password, text == "-" ? "" : text);
                if (!r.Ok)
                {
                    await Send(s, E("Не вышло: " + r.Error), AuthButtons());
                    return;
                }
                await AfterAuth(s, r.Body);
                break;
            }

            case Step.DeletePassword:
                await TryDelete(s, m);
                s.Step = Step.None;
                s.Draft["password"] = text;
                await Send(s, B("УДАЛИТЬ АККАУНТ НАВСЕГДА?") + "\n" +
                              E("Профиль, прогресс и результаты опросников сотрутся. Вернуть их будет нельзя."),
                    new InlineKeyboardMarkup(new[]
                    {
                        InlineKeyboardButton.WithCallbackData("УДАЛИТЬ НАВСЕГДА", "d:yes"),
                        InlineKeyboardButton.WithCallbackData("ОТМЕНА", "d:no")
                    }));
                break;
        }
    }

    // ---------- кнопки ----------

    async Task OnCallback(ChatSession s, CallbackQuery cq)
    {
        var parts = (cq.Data ?? "").Split(':');
        string kind = parts[0];
        string toast = "";

        if (!s.LoggedIn && kind != "a")
        {
            await Answer(cq, "Сначала войди в аккаунт");
            await Welcome(s);
            return;
        }

        int msgId = cq.Message?.Id ?? 0;
        switch (kind)
        {
            case "a":
                ResetStep(s);
                if (Part(parts, 1) == "reg")
                {
                    s.Step = Step.RegLogin;
                    await Send(s, E("Придумай логин: 3–32 символа, латиница, цифры, _ . -\n/cancel — отмена."));
                }
                else
                {
                    s.Step = Step.LoginName;
                    await Send(s, E("Логин:\n/cancel — отмена."));
                }
                break;
            case "g": await SetGoal(s, msgId, Num(parts, 1)); break;
            case "go": await ContinueLesson(s); break;
            case "path": await ShowPath(s); break;
            case "p": await StartLesson(s, Part(parts, 1)); break;
            case "l": toast = await OnLesson(s, msgId, parts); break;
            case "z": await OnQuizMenu(s, msgId, parts); break;
            case "q": toast = await OnQuiz(s, msgId, parts); break;
            case "r": await ShowEntry(s, Num(parts, 1)); break;
            case "pf": await OnProfile(s, msgId, Part(parts, 1)); break;
            case "d": await OnDelete(s, msgId, Part(parts, 1)); break;
        }
        await Answer(cq, toast);
    }

    static string Part(string[] parts, int i) => i < parts.Length ? parts[i] : "";
    static int Num(string[] parts, int i) => int.TryParse(Part(parts, i), out var n) ? n : -1;

    // ---------- вход ----------

    static InlineKeyboardMarkup AuthButtons() => new(new[]
    {
        InlineKeyboardButton.WithCallbackData("ВОЙТИ", "a:login"),
        InlineKeyboardButton.WithCallbackData("РЕГИСТРАЦИЯ", "a:reg")
    });

    async Task Welcome(ChatSession s)
    {
        var sb = new StringBuilder();
        sb.Append(B("ПСИХЕЯ")).Append(E(" — тренажёр для будущих психологов.")).Append("\n\n");
        sb.Append(E(Intro[0])).Append("\n\n");
        sb.Append(E("Аккаунт общий с приложением: прогресс, серия и результаты опросников одни и те же."));
        await Send(s, sb.ToString(), AuthButtons());
    }

    async Task AfterAuth(ChatSession s, JsonElement body)
    {
        s.Token = body.GetProperty("token").GetString();
        s.User = PsyApi.ReadUser(body.GetProperty("user"));
        s.P = null;
        sessions.Save();
        if (!await Sync(s)) return;
        await Send(s, E($"Вошёл как @{s.User!.Login}."), Menu);
        await Home(s);
    }

    async Task Help(ChatSession s)
    {
        var sb = new StringBuilder();
        sb.Append(B("КОМАНДЫ")).Append('\n');
        sb.Append(E("/lesson — продолжить уроки\n/path — путь уроков\n/quiz — опросники\n/ref — справочник\n/profile — профиль\n/cancel — отменить ввод\n\n"));
        sb.Append(E("Любое слово без команды — поиск по справочнику."));
        await Send(s, sb.ToString(), s.LoggedIn ? Menu : null);
    }

    // ---------- прогресс ----------

    // свежий прогресс с сервера: его мог поменять телефон. Неотправленные правки бота важнее
    async Task<bool> Sync(ChatSession s)
    {
        if (s.Dirty && s.P != null && await Push(s)) return true;
        var r = await api.Me(s.Token!);
        if (r.Status == 401 || r.Status == 404)
        {
            await Expired(s);
            return false;
        }
        if (!r.Ok)
        {
            if (s.P != null) return true;
            await Send(s, E(r.Error));
            return false;
        }

        s.User = PsyApi.ReadUser(r.Body.GetProperty("user"));
        var p = r.Body.GetProperty("progress");
        // новый аккаунт: на сервере пустой объект
        s.P = p.ValueKind == JsonValueKind.Object && p.EnumerateObject().Any()
            ? p.Deserialize<ProgressData>(Opt) ?? new ProgressData()
            : new ProgressData();
        return true;
    }

    async Task<bool> Push(ChatSession s)
    {
        if (s.P == null || s.Token == null) return false;
        var r = await api.PushProgress(s.Token, s.P);
        s.Dirty = !r.Ok;
        if (r.Status == 401) await Expired(s);
        return r.Ok;
    }

    async Task Expired(ChatSession s)
    {
        s.Logout();
        sessions.Save();
        await Send(s, E("Сессия истекла или аккаунт удалён. Войди заново."), new ReplyKeyboardRemove());
        await Welcome(s);
    }

    string Hud(ProgressData p, Content c)
    {
        int level = Game.LevelOf(p.Rating);
        var lv = Game.LevelInfoOf(c, level);
        int goal = Game.DailyGoalXp(p);
        int today = Game.TodayXp(p);
        return E($"СЕРИЯ {Game.DisplayStreak(p)} ДН. · XP {p.TotalXp} · LV {level} {lv.Name.ToUpperInvariant()}") + "\n" +
               E($"ЦЕЛЬ ДНЯ {Bar(goal == 0 ? 0 : (double)today / goal, 8)} {Math.Min(today, goal)}/{goal} XP");
    }

    // ---------- главный экран и цель ----------

    async Task Home(ChatSession s)
    {
        if (!s.LoggedIn)
        {
            await Welcome(s);
            return;
        }
        if (!await Sync(s)) return;
        var p = s.P!;

        // как в приложении: у админа нет знакомства с приложением
        if (!p.Onboarded && !s.User!.IsAdmin)
        {
            await Onboarding(s);
            return;
        }

        var c = await content.Get();
        var current = Game.Path(p, c).FirstOrDefault(x => x.State == LessonState.Current);
        var sb = new StringBuilder();
        sb.Append(B($"ПРИВЕТ, {Name(s).ToUpperInvariant()}!")).Append("\n\n").Append(Hud(p, c)).Append("\n\n");
        InlineKeyboardMarkup? kb = null;
        if (current != null)
        {
            int streak = Game.DisplayStreak(p);
            sb.Append(E(streak >= 3 ? $"Серия {streak} дн.! " : "")).Append(E($"Следующий урок: «{current.Lesson.Title}». Продолжим?"));
            kb = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("▶ НАЧАТЬ УРОК", "go"));
        }
        else
        {
            sb.Append(E("Ты прошёл всё, что сейчас доступно. Новые разделы скоро появятся!"));
        }
        if (kb == null) await Send(s, sb.ToString(), Menu);
        else await Send(s, sb.ToString(), kb);
    }

    static string Name(ChatSession s) =>
        string.IsNullOrWhiteSpace(s.User?.DisplayName) ? s.User?.Login ?? "" : s.User!.DisplayName;

    static InlineKeyboardMarkup GoalButtons() => new(Game.Goals
        .Select(g => InlineKeyboardButton.WithCallbackData($"{g.Min} МИН · {g.Name.ToUpperInvariant()}", "g:" + g.Min))
        .Chunk(2));

    async Task Onboarding(ChatSession s)
    {
        var sb = new StringBuilder();
        sb.Append(B("ПСИХЕЯ // СТАРТ")).Append("\n\n");
        foreach (var line in Intro) sb.Append(E(line)).Append("\n\n");
        sb.Append(B("Сколько минут в день будем заниматься?"));
        await Send(s, sb.ToString(), GoalButtons());
    }

    async Task SetGoal(ChatSession s, int messageId, int min)
    {
        if (!Game.Goals.Any(g => g.Min == min) || !await Sync(s)) return;
        var p = s.P!;
        bool first = !p.Onboarded;
        p.DailyGoalMin = min;
        if (first)
        {
            p.Onboarded = true;
            p.Rating = 100;
        }
        await Push(s);
        await Edit(s, messageId, E($"Цель на день: {min} мин (≈{Game.DailyGoalXp(p)} XP)."), null);
        if (first) await Home(s);
    }

    // ---------- путь и уроки ----------

    static string Glyph(LessonState st) => st switch
    {
        LessonState.Current => "▶",
        LessonState.Done => "✓",
        LessonState.Mastered => "✓",
        LessonState.Open => "○",
        _ => "▒"
    };

    async Task ShowPath(ChatSession s)
    {
        if (!await Sync(s)) return;
        var p = s.P!;
        var c = await content.Get();
        var nodes = Game.Path(p, c);

        var sb = new StringBuilder();
        sb.Append(B("ВОПРОСЫ // ПУТЬ")).Append('\n').Append(Hud(p, c));
        foreach (var sec in nodes.GroupBy(x => x.Section))
        {
            sb.Append("\n\n").Append(B($"ЮНИТ {sec.Key.Id:00} · {sec.Key.Title.ToUpperInvariant()}"));
            int unlock = Game.UnlockLevel(c, sec.Key.Id);
            if (Game.LevelOf(p.Rating) < unlock) sb.Append(E($"  (откроется на LV {unlock})"));
            foreach (var n in sec)
            {
                string line = $"{Glyph(n.State)} {n.Lesson.Title}" + (n.State == LessonState.Mastered ? " (освоен уровнем)" : "");
                sb.Append('\n').Append(n.State == LessonState.Current ? B(line) : E(line));
            }
        }
        var known = c.Lessons.Sections.Select(x => x.Id).ToHashSet();
        var soon = c.Rating.SectionUnlock.Where(u => !known.Contains(u.Section)).ToList();
        if (soon.Count > 0)
        {
            sb.Append("\n\n").Append(B("СКОРО"));
            foreach (var u in soon)
                sb.Append('\n').Append(E($"▒ {u.Section:00} · {u.Title} · LV {u.UnlockLevel}"));
        }
        sb.Append("\n\n").Append(I("▶ текущий · ✓ пройден · ○ открыт · ▒ закрыт"));

        var rows = nodes.Where(n => n.State != LessonState.Locked)
            .Select(n => new[] { InlineKeyboardButton.WithCallbackData(Short($"{Glyph(n.State)} {n.Lesson.Title}"), "p:" + n.Lesson.Id) })
            .ToList();
        foreach (var part in Split(sb.ToString()).SkipLast(1)) await Send(s, part);
        await Send(s, Split(sb.ToString()).Last(), rows.Count > 0 ? new InlineKeyboardMarkup(rows) : Menu);
    }

    async Task ContinueLesson(ChatSession s)
    {
        // урок уже идёт — показываем его заново внизу чата
        if (s.Lesson is { Cur: not null } run)
        {
            var screen = run.Render();
            var m = await Send(s, screen.Text, screen.Keyboard);
            run.MessageId = m.Id;
            return;
        }
        if (!await Sync(s)) return;
        var c = await content.Get();
        var current = Game.Path(s.P!, c).FirstOrDefault(x => x.State == LessonState.Current);
        if (current == null)
        {
            await Send(s, E("Ты прошёл всё, что сейчас доступно. Повторить любой урок можно из пути: /path"), Menu);
            return;
        }
        await StartLesson(s, current.Lesson.Id, synced: true);
    }

    async Task StartLesson(ChatSession s, string lessonId, bool synced = false)
    {
        if (!synced && !await Sync(s)) return;
        var c = await content.Get();
        var node = Game.Path(s.P!, c).FirstOrDefault(x => x.Lesson.Id == lessonId);
        if (node == null)
        {
            await Send(s, E("Такого урока больше нет. Открой путь заново: /path"));
            return;
        }
        if (node.State == LessonState.Locked)
        {
            await Send(s, E("Закрыто: сначала пройди предыдущие уроки или повысь уровень."));
            return;
        }

        s.Quiz = null;
        var run = new LessonRun(++s.Seq, node.Section, node.Lesson, s.P!);
        if (!run.Next())
        {
            await Send(s, E("В этом уроке пока нет заданий."));
            return;
        }
        s.Lesson = run;
        var screen = run.Render();
        var m = await Send(s, screen.Text, screen.Keyboard);
        run.MessageId = m.Id;
    }

    // l:<номер прохождения>:<действие>:<аргумент>; вернуть — текст всплывающей подсказки
    async Task<string> OnLesson(ChatSession s, int msgId, string[] parts)
    {
        var run = s.Lesson;
        if (run == null || run.Id != Num(parts, 1)) return "Этот урок уже закрыт";
        if (msgId != run.MessageId) return "Отвечай в последнем сообщении урока";
        var p = s.P!;
        int arg = Num(parts, 3);
        bool wasChecked = run.IsChecked;

        switch (Part(parts, 2))
        {
            case "c":
                if (!run.Choose(p, arg)) return "";
                break;
            case "o":
                if (!run.PickOrder(p, arg)) return "";
                break;
            case "r":
                if (!run.ResetOrder()) return "";
                break;
            case "m":
                var hint = run.PickMatch(p, arg);
                if (hint == null) return "";
                if (hint.Length > 0) return hint;
                break;
            case "n":
                if (!run.IsChecked) return "";
                if (!run.Next())
                {
                    await FinishLesson(s, run);
                    return "";
                }
                break;
            case "x":
                run.AskingQuit = true;
                break;
            case "s":
                run.AskingQuit = false;
                break;
            case "y":
                s.Lesson = null;
                await Edit(s, msgId, B($"УРОК {run.Lesson.Id} · {run.Lesson.Title}") + "\n" + E("Урок прерван."), null);
                return "";
        }

        // ответ засчитан: рейтинг и счётчики сразу на сервер, как в приложении после каждого ответа
        if (!wasChecked && run.IsChecked) await Push(s);
        var screen = run.Render();
        await Edit(s, msgId, screen.Text, screen.Keyboard);
        return "";
    }

    async Task FinishLesson(ChatSession s, LessonRun run)
    {
        var c = await content.Get();
        var result = run.Finish(s.P!, c);
        s.Lesson = null;
        await Push(s);
        _ = api.PostLesson(s.Token!, run.Lesson.Id, result.Accuracy, result.Mistakes, result.Xp);

        var next = Game.Path(s.P!, c).FirstOrDefault(x => x.State == LessonState.Current);
        var buttons = new List<InlineKeyboardButton>();
        if (next != null) buttons.Add(InlineKeyboardButton.WithCallbackData("▶ ДАЛЬШЕ", "go"));
        buttons.Add(InlineKeyboardButton.WithCallbackData("ПУТЬ", "path"));
        string text = result.Text + (next != null ? "\n\n" + E($"Следующий: «{next.Lesson.Title}»") : "");
        await Edit(s, run.MessageId, text, new InlineKeyboardMarkup(buttons));
    }

    // ---------- опросники ----------

    async Task ShowQuizzes(ChatSession s)
    {
        if (!await Sync(s)) return;
        var p = s.P!;
        var c = await content.Get();
        var sb = new StringBuilder();
        sb.Append(B("ОПРОСНИКИ")).Append('\n');
        sb.Append(E("Ответы считаются сами. Результаты сохраняются в профиле, пройти можно сколько угодно раз."));
        for (int i = 0; i < c.Questionnaires.Count; i++)
        {
            var q = c.Questionnaires[i];
            sb.Append("\n\n").Append(B($"▸ {q.Title.ToUpperInvariant()}")).Append(E($" · {q.Questions.Count} вопросов"));
            if (!string.IsNullOrWhiteSpace(q.About)) sb.Append('\n').Append(E(q.About));
            var last = p.History.FirstOrDefault(h => h.Id == q.Id);
            if (last != null)
                sb.Append('\n').Append(I($"Последний раз: {last.Headline} ({last.Date}), всего: {p.History.Count(h => h.Id == q.Id)}"));
        }
        if (c.Questionnaires.Count == 0) sb.Append("\n\n").Append(E("Пока нет ни одного опросника."));

        var rows = c.Questionnaires
            .Select((q, i) => new[] { InlineKeyboardButton.WithCallbackData(Short(q.Title), "z:open:" + i) })
            .ToList();
        await Send(s, sb.ToString(), rows.Count > 0 ? new InlineKeyboardMarkup(rows) : Menu);
    }

    async Task OnQuizMenu(ChatSession s, int msgId, string[] parts)
    {
        var c = await content.Get();
        string verb = Part(parts, 1);
        if (verb == "list")
        {
            await ShowQuizzes(s);
            return;
        }
        int i = Num(parts, 2);
        if (i < 0 || i >= c.Questionnaires.Count) return;
        var quiz = c.Questionnaires[i];

        if (verb == "open")
        {
            var sb = new StringBuilder();
            sb.Append(B(quiz.Title.ToUpperInvariant())).Append("\n\n");
            if (!string.IsNullOrWhiteSpace(quiz.About)) sb.Append(E(quiz.About)).Append("\n\n");
            sb.Append(E($"Вопросов: {quiz.Questions.Count}"));
            if (!string.IsNullOrWhiteSpace(quiz.Source)) sb.Append('\n').Append(I("Источник: " + quiz.Source));
            await Send(s, sb.ToString(), new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithCallbackData("НАЧАТЬ ▶", "z:start:" + i),
                InlineKeyboardButton.WithCallbackData("◀ К СПИСКУ", "z:list")
            }));
            return;
        }

        if (verb == "start")
        {
            if (quiz.Questions.Count == 0) return;
            if (!await Sync(s)) return;
            s.Lesson = null;
            var run = new QuizRun(++s.Seq, quiz);
            s.Quiz = run;
            var screen = run.Render();
            // экран описания превращается в первый вопрос
            run.MessageId = await Edit(s, msgId, screen.Text, screen.Keyboard);
        }
    }

    async Task<string> OnQuiz(ChatSession s, int msgId, string[] parts)
    {
        var run = s.Quiz;
        if (run == null || run.Id != Num(parts, 1)) return "Этот опросник уже закрыт";
        if (msgId != run.MessageId) return "Отвечай в последнем сообщении опросника";

        switch (Part(parts, 2))
        {
            case "a":
                if (!run.Answer(Num(parts, 3))) return "";
                if (run.Done)
                {
                    await FinishQuiz(s, run);
                    return "";
                }
                break;
            case "b":
                if (!run.Back()) return "";
                break;
            case "x":
                s.Quiz = null;
                await Edit(s, msgId, B(run.Quiz.Title.ToUpperInvariant()) + "\n" + E("Опросник прерван."), null);
                return "";
        }
        var screen = run.Render();
        await Edit(s, msgId, screen.Text, screen.Keyboard);
        return "";
    }

    async Task FinishQuiz(ChatSession s, QuizRun run)
    {
        s.Quiz = null;
        var res = Game.Score(run.Quiz, run.Answers);
        Game.SaveResult(s.P!, run.Quiz, res);
        await Push(s);
        _ = api.PostQuestionnaire(s.Token!, run.Quiz.Id, res.Headline,
            res.Scores.Select(x => new { scale = x.Scale.Key, value = x.Value, max = x.Max }).ToList());

        var parts = Split(QuizRun.ResultText(run.Quiz, res));
        var kb = new InlineKeyboardMarkup(new[]
        {
            InlineKeyboardButton.WithCallbackData("ОПРОСНИКИ", "z:list"),
            InlineKeyboardButton.WithCallbackData("ПРОФИЛЬ", "pf:show")
        });
        await Edit(s, run.MessageId, parts[0], parts.Count == 1 ? kb : null);
        for (int i = 1; i < parts.Count; i++)
            await Send(s, parts[i], i == parts.Count - 1 ? kb : null);
    }

    // ---------- справочник ----------

    async Task ShowReference(ChatSession s)
    {
        var c = await content.Get();
        var rows = c.Reference
            .Select((e, i) => InlineKeyboardButton.WithCallbackData(Short(e.Term, 30), "r:" + i))
            .Chunk(2)
            .ToList();
        string text = B("СПРАВОЧНИК") + "\n" + E("Нажми термин или просто напиши слово — найду.") + "\n" +
                      I("Учебные материалы. Не клиническая диагностика.");
        await Send(s, text, rows.Count > 0 ? new InlineKeyboardMarkup(rows) : Menu);
    }

    async Task ShowEntry(ChatSession s, int i)
    {
        var c = await content.Get();
        if (i < 0 || i >= c.Reference.Count) return;
        var e = c.Reference[i];
        var sb = new StringBuilder();
        sb.Append(B("▸ " + e.Term.ToUpperInvariant()));
        if (!string.IsNullOrWhiteSpace(e.Short)) sb.Append('\n').Append(I(e.Short));
        if (!string.IsNullOrWhiteSpace(e.Text)) sb.Append("\n\n").Append(E(e.Text));
        foreach (var part in Split(sb.ToString())) await Send(s, part);
    }

    async Task SearchReference(ChatSession s, string query)
    {
        var c = await content.Get();
        string q = query.ToLowerInvariant();
        var found = c.Reference
            .Select((e, i) => (e, i))
            .Where(x => x.e.Term.ToLowerInvariant().Contains(q) || x.e.Short.ToLowerInvariant().Contains(q))
            .ToList();

        if (found.Count == 0)
        {
            await Send(s, E($"В справочнике ничего не нашёл по «{Short(query, 40)}». Весь список: /ref, все команды: /help"), Menu);
            return;
        }
        if (found.Count == 1)
        {
            await ShowEntry(s, found[0].i);
            return;
        }
        var rows = found.Take(20)
            .Select(x => new[] { InlineKeyboardButton.WithCallbackData(Short(x.e.Term), "r:" + x.i) });
        await Send(s, E($"Нашёл в справочнике: {found.Count}"), new InlineKeyboardMarkup(rows));
    }

    // ---------- профиль ----------

    static InlineKeyboardMarkup ProfileButtons() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("ЦЕЛЬ ДНЯ", "pf:goal") },
        new[] { InlineKeyboardButton.WithCallbackData("СБРОСИТЬ ПРОГРЕСС", "pf:reset") },
        new[] { InlineKeyboardButton.WithCallbackData("ВЫЙТИ ИЗ АККАУНТА", "pf:logout") },
        new[] { InlineKeyboardButton.WithCallbackData("УДАЛИТЬ АККАУНТ", "pf:delete") }
    });

    async Task ShowProfile(ChatSession s)
    {
        if (!await Sync(s)) return;
        var p = s.P!;
        var c = await content.Get();
        int level = Game.LevelOf(p.Rating);
        var lv = Game.LevelInfoOf(c, level);
        var u = s.User!;

        var sb = new StringBuilder();
        sb.Append(B(Name(s).ToUpperInvariant())).Append('\n').Append(E("@" + u.Login + (u.IsAdmin ? " · АДМИН" : "")));
        if (u.IsAdmin) sb.Append('\n').Append(I("Админ-панель — в приложении."));

        double span = Math.Max(1, lv.RatingTo - lv.RatingFrom + 1);
        sb.Append("\n\n").Append(B($"LV {level} · {lv.Name.ToUpperInvariant()}")).Append('\n');
        sb.Append(E($"РЕЙТИНГ {(int)p.Rating} {Bar((p.Rating - lv.RatingFrom) / span)}"));
        if (!string.IsNullOrWhiteSpace(lv.About)) sb.Append('\n').Append(E(lv.About));

        int acc = p.Answered == 0 ? 0 : (int)Math.Round(p.Correct * 100.0 / p.Answered);
        sb.Append("\n\n").Append(E($"XP {p.TotalXp} · СЕРИЯ {Game.DisplayStreak(p)} ДН. · ТОЧНОСТЬ {acc}% · ОПРОСНИКОВ {p.History.Count}"));
        sb.Append('\n').Append(E($"ЦЕЛЬ ДНЯ: {p.DailyGoalMin} МИН (≈{Game.DailyGoalXp(p)} XP), сегодня {Game.TodayXp(p)} XP"));

        sb.Append("\n\n").Append(B("ИСТОРИЯ ОПРОСНИКОВ"));
        if (p.History.Count == 0) sb.Append('\n').Append(E("Пока пусто. Пройди любой опросник: /quiz"));
        foreach (var h in p.History.Take(5))
            sb.Append('\n').Append(E($"▸ {h.Title} · {h.Date}: ")).Append(B(h.Headline));

        sb.Append("\n\n").Append(B("ДОСТИЖЕНИЯ"));
        foreach (var def in Game.AchDefs)
        {
            bool got = p.Achievements.Contains(def.Id);
            string line = $"{(got ? "★" : "☆")} {def.Title} — {def.Desc}";
            sb.Append('\n').Append(got ? B(line) : E(line));
        }

        sb.Append("\n\n").Append(I("Психея — учебный тренажёр. Это не клиническая диагностика. Результаты опросников носят учебный характер."));
        sb.Append('\n').Append(E($"ПСИХЕЯ-бот v{Version}"));

        var parts = Split(sb.ToString());
        for (int i = 0; i < parts.Count; i++)
            await Send(s, parts[i], i == parts.Count - 1 ? ProfileButtons() : null);
    }

    async Task OnProfile(ChatSession s, int msgId, string verb)
    {
        switch (verb)
        {
            case "show":
                await ShowProfile(s);
                break;
            case "goal":
                await Send(s, E("Сколько минут в день будем заниматься?"), GoalButtons());
                break;
            case "reset":
                await Send(s, B("СБРОС") + "\n" + E("Весь прогресс будет удалён: XP, рейтинг, уроки, серия, история опросников. Продолжить?"),
                    new InlineKeyboardMarkup(new[]
                    {
                        InlineKeyboardButton.WithCallbackData("СБРОСИТЬ", "pf:resetok"),
                        InlineKeyboardButton.WithCallbackData("ОТМЕНА", "pf:no")
                    }));
                break;
            case "resetok":
                s.Lesson = null;
                s.Quiz = null;
                s.P = new ProgressData();
                if (!await Push(s)) return;
                await Edit(s, msgId, E("Прогресс сброшен."), null);
                await Home(s);
                break;
            case "logout":
                await Send(s, B("ВЫХОД") + "\n" + E("Прогресс сохранён в аккаунте. Выйти?"),
                    new InlineKeyboardMarkup(new[]
                    {
                        InlineKeyboardButton.WithCallbackData("ВЫЙТИ", "pf:logoutok"),
                        InlineKeyboardButton.WithCallbackData("ОТМЕНА", "pf:no")
                    }));
                break;
            case "logoutok":
                if (s.Dirty) await Push(s);
                s.Logout();
                sessions.Save();
                await Edit(s, msgId, E("Ты вышел из аккаунта."), null);
                await Send(s, E("До встречи!"), new ReplyKeyboardRemove());
                await Welcome(s);
                break;
            case "delete":
                s.Step = Step.DeletePassword;
                await Send(s, B("УДАЛЕНИЕ АККАУНТА") + "\n" +
                              E("Отправь пароль от аккаунта для подтверждения. Сообщение с ним я сразу удалю.\n/cancel — отмена."));
                break;
            case "no":
                await Edit(s, msgId, E("Отменено."), null);
                break;
        }
    }

    async Task OnDelete(ChatSession s, int msgId, string verb)
    {
        string password = s.Draft.GetValueOrDefault("password", "");
        s.Draft.Clear();
        if (verb != "yes" || password.Length == 0)
        {
            await Edit(s, msgId, E("Удаление отменено."), null);
            return;
        }
        var r = await api.DeleteAccount(s.Token!, password);
        if (!r.Ok)
        {
            await Edit(s, msgId, E("Не удалось удалить: " + r.Error), null);
            return;
        }
        s.Logout();
        sessions.Save();
        await Edit(s, msgId, E("Аккаунт удалён."), null);
        await Send(s, E("Профиль, прогресс и результаты стёрты."), new ReplyKeyboardRemove());
        await Welcome(s);
    }

    // ---------- Telegram ----------

    Task<Message> Send(ChatSession s, string html, ReplyMarkup? markup = null) =>
        tg.SendMessage(s.ChatId, html, ParseMode.Html, replyMarkup: markup);

    // правка экрана; если сообщение уже не поправить (старое, удалено) — присылаем новое. Возвращает id экрана
    async Task<int> Edit(ChatSession s, int messageId, string html, InlineKeyboardMarkup? kb)
    {
        try
        {
            await tg.EditMessageText(s.ChatId, messageId, html, ParseMode.Html, replyMarkup: kb);
            return messageId;
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("not modified"))
        {
            return messageId;
        }
        catch (ApiRequestException)
        {
            var m = await Send(s, html, kb);
            if (s.Lesson?.MessageId == messageId) s.Lesson.MessageId = m.Id;
            if (s.Quiz?.MessageId == messageId) s.Quiz.MessageId = m.Id;
            return m.Id;
        }
    }

    async Task TryDelete(ChatSession s, Message m)
    {
        try { await tg.DeleteMessage(s.ChatId, m.Id); }
        catch (ApiRequestException) { /* старше 48 часов или уже удалено */ }
    }

    async Task Answer(CallbackQuery cq, string text)
    {
        try { await tg.AnswerCallbackQuery(cq.Id, string.IsNullOrEmpty(text) ? null : text); }
        catch (ApiRequestException) { /* запрос устарел — кнопку нажали давно */ }
    }
}
