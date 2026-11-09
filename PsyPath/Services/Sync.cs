using System.Text.Json;

namespace PsyPath.Services;

// офлайн-режим: прогресс и результаты копятся в телефоне и уходят на сервер, когда появится сеть
public static class Sync
{
    record OutItem(string Path, string Json);

    static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    static readonly SemaphoreSlim Gate = new(1, 1);
    static bool again;
    static bool started;

    // что-то поменялось в очереди: профиль обновляет строку «ждёт сети»
    public static event Action? Changed;

    // прогресс сохранён в телефоне, но ещё не отправлен
    static bool Dirty
    {
        get => Preferences.Default.Get("sync_dirty", false);
        set => Preferences.Default.Set("sync_dirty", value);
    }

    static List<OutItem> Outbox
    {
        get
        {
            try { return JsonSerializer.Deserialize<List<OutItem>>(Preferences.Default.Get("outbox", "[]")) ?? new(); }
            catch (JsonException) { return new(); }
        }
        set => Preferences.Default.Set("outbox", JsonSerializer.Serialize(value));
    }

    // сколько записей ждёт отправки (прогресс считается одной)
    public static int Pending => Outbox.Count + (Dirty ? 1 : 0);

    public static void Start()
    {
        if (started) return;
        started = true;
        Connectivity.Current.ConnectivityChanged += (s, e) =>
        {
            if (e.NetworkAccess == NetworkAccess.Internet) _ = Flush();
        };
    }

    public static void MarkDirty() => Dirty = true;

    public static void Lesson(string lessonId, int accuracy, int mistakes, int xp) =>
        Enqueue("/api/me/lessons", new { lessonId, accuracy, mistakes, xp });

    public static void Questionnaire(string questionnaireId, string headline, object scores) =>
        Enqueue("/api/me/questionnaires", new { questionnaireId, headline, scores });

    static void Enqueue(string path, object body)
    {
        if (!Api.LoggedIn) return;
        var list = Outbox;
        list.Add(new OutItem(path, JsonSerializer.Serialize(body, Web)));
        // очередь не растёт бесконечно: самые старые записи уходят первыми и первыми теряются
        if (list.Count > 200) list.RemoveRange(0, list.Count - 200);
        Outbox = list;
        Changed?.Invoke();
        _ = Flush();
    }

    // при выходе из аккаунта и сбросе: чужие записи новому аккаунту не нужны
    public static void Clear()
    {
        Dirty = false;
        Outbox = new();
        Changed?.Invoke();
    }

    // нет сети, сервер упал, лимит запросов — запись остаётся в очереди
    static bool Transient(ApiResult r) => r.Status is 0 or 401 or 408 or 429 || r.Status >= 500;

    public static async Task Flush()
    {
        if (!Api.LoggedIn) return;
        // уже отправляем — пройдём очередь ещё раз, когда закончим
        if (!await Gate.WaitAsync(0))
        {
            again = true;
            return;
        }
        try
        {
            do
            {
                again = false;
                if (Dirty)
                {
                    // флаг снимается до запроса: сохранение во время отправки поставит его снова
                    Dirty = false;
                    var r = await Api.PushProgress(AppState.P);
                    if (!r.Ok && Transient(r))
                    {
                        Dirty = true;
                        break;
                    }
                }

                bool stop = false;
                while (!stop && Outbox is { Count: > 0 } list)
                {
                    var r = await Api.PostJson(list[0].Path, list[0].Json);
                    if (!r.Ok && Transient(r))
                    {
                        stop = true;
                        continue;
                    }
                    // принято или сервер отказал насовсем (400) — убираем; очередь могла пополниться
                    var now = Outbox;
                    if (now.Count > 0 && now[0] == list[0]) now.RemoveAt(0);
                    Outbox = now;
                }
                if (stop) break;
            } while (again);
        }
        finally
        {
            Gate.Release();
            MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke());
        }
    }
}
