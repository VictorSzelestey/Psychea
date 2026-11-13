using System.Text.Json;
using System.Text.Json.Nodes;
using PsyPath.Models;
using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

// список уроков или опросников с сервера; элемент открывается в редакторе JSON
public class AdminContentPage : AdminBase
{
    readonly string name;
    readonly string title;
    JsonNode? root;
    int version;
    // опросники (или юниты с уроками), которые есть в этой версии приложения, но ещё не опубликованы на сервере
    List<JsonNode> unpublished = new();
    // теория уроков из этой версии приложения для уроков, которые на сервере есть, но без теории: id урока → карточки
    Dictionary<string, JsonNode> theoryUnpublished = new();

    public AdminContentPage(string name)
    {
        this.name = name;
        title = name switch { "lessons" => "УРОКИ", "reference" => "СПРАВОЧНИК", "rating" => "РЕЙТИНГ И УРОВНИ", _ => "ОПРОСНИКИ" };
        Show(title, Message("ЗАГРУЗКА…"));
    }

    // после возврата из редактора берём свежую версию с сервера
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var r = await Api.Content(name);
        if (!r.Ok)
        {
            Show(title, Message(r.Error, Pal.RedInk));
            return;
        }
        version = r.Body.GetProperty("version").GetInt32();
        root = JsonNode.Parse(r.Body.GetProperty("data").GetRawText());
        if (name == "questionnaires") await FindUnpublished();
        if (name == "lessons") await FindUnpublishedLessons();
        Show(title, Build(), animate: true);
    }

    // по юниту на карточку: в копии юнита только уроки, id которых на сервере нет
    async Task FindUnpublishedLessons()
    {
        var onServer = LessonIds(null);
        var builtIn = JsonNode.Parse(await AppState.ReadAssetText("lessons.json"))!["sections"]!.AsArray();
        unpublished = new();
        foreach (var sec in builtIn)
        {
            var fresh = sec!["lessons"]!.AsArray().Where(l => !onServer.Contains(Str(l!, "id"))).Select(l => l!.DeepClone()).ToArray();
            if (fresh.Length == 0) continue;
            var part = sec.DeepClone();
            part["lessons"] = new JsonArray(fresh);
            unpublished.Add(part);
        }

        // уроки на сервере без теории, у которых она есть во встроенном lessons.json
        var builtInTheory = builtIn.SelectMany(sec => sec!["lessons"]!.AsArray())
            .Where(l => l!["theory"] is JsonArray a && a.Count > 0)
            .ToDictionary(l => Str(l!, "id"), l => l!["theory"]!);
        theoryUnpublished = root!["sections"]!.AsArray()
            .SelectMany(sec => sec!["lessons"]!.AsArray())
            .Where(l => !(l!["theory"] is JsonArray a && a.Count > 0) && builtInTheory.ContainsKey(Str(l!, "id")))
            .ToDictionary(l => Str(l!, "id"), l => builtInTheory[Str(l!, "id")]);
    }

    // одна карточка на все уроки без теории: задания на сервере не трогаются, дописывается только теория
    View UnpublishedTheoryCard()
    {
        var box = new VerticalStackLayout { Spacing = 6 };
        box.Children.Add(Ui.Text("ТЕОРИЯ К УРОКАМ", 14, Pal.Ink, true));
        box.Children.Add(Ui.Text($"Уроков без теории на сервере: {theoryUnpublished.Count}. В приложении она уже есть. Задания и правки на сервере не изменятся — добавится только теория.", 12, Pal.Ink));

        var publish = new Btn("ДОБАВИТЬ ТЕОРИЮ ДЛЯ ВСЕХ", Pal.Violet, Pal.Paper, 14);
        publish.Clicked += async (s, e) =>
        {
            if (!await Dlg.Confirm(this, "ДОБАВИТЬ ТЕОРИЮ?", $"Теория появится в {theoryUnpublished.Count} уроках у всех пользователей при следующем запуске приложения.", "ДОБАВИТЬ", "ОТМЕНА"))
                return;
            publish.Enabled = false;
            var add = theoryUnpublished;
            var error = await SaveDoc(doc =>
            {
                foreach (var lesson in doc["sections"]!.AsArray().SelectMany(sec => sec!["lessons"]!.AsArray()))
                    if (add.TryGetValue(Str(lesson!, "id"), out var theory) && !(lesson!["theory"] is JsonArray a && a.Count > 0))
                        lesson!["theory"] = theory.DeepClone();
                return doc;
            });
            if (error != null)
            {
                publish.Enabled = true;
                await Dlg.Alert(this, "НЕ ВЫШЛО", error);
                return;
            }
            theoryUnpublished = new();
            Show(title, Build());
        };
        box.Children.Add(publish);
        return Ui.Card(box, Pal.Amber, 12);
    }

    View UnpublishedSectionCard(JsonNode sec)
    {
        string secTitle = Str(sec, "title");
        int secId = sec["id"]!.GetValue<int>();
        var lessons = sec["lessons"]!.AsArray();
        int tasks = lessons.Sum(l => l!["tasks"]?.AsArray().Count ?? 0);
        string json = lessons.ToJsonString();

        var box = new VerticalStackLayout { Spacing = 6 };
        box.Children.Add(Ui.Text($"ЮНИТ {secId}  //  {secTitle.ToUpperInvariant()}", 14, Pal.Ink, true));
        box.Children.Add(Ui.Text($"{lessons.Count} уроков, {tasks} заданий. Есть в приложении, на сервере ещё нет.", 12, Pal.Ink));

        var publish = new Btn("ОПУБЛИКОВАТЬ ДЛЯ ВСЕХ", Pal.Violet, Pal.Paper, 14);
        publish.Clicked += async (s, e) =>
        {
            if (!await Dlg.Confirm(this, "ОПУБЛИКОВАТЬ?", $"Уроки юнита «{secTitle}» ({lessons.Count}) появятся у всех пользователей при следующем запуске приложения.", "ОПУБЛИКОВАТЬ", "ОТМЕНА"))
                return;
            publish.Enabled = false;
            var error = await SaveDoc(doc =>
            {
                var sections = doc["sections"]!.AsArray();
                var target = sections.FirstOrDefault(n => n!["id"]!.GetValue<int>() == secId);
                if (target == null)
                {
                    // новый юнит встаёт по порядку id, даже если юниты публикуют вразнобой
                    target = JsonNode.Parse(sec.ToJsonString())!;
                    target["lessons"] = new JsonArray();
                    sections.Insert(sections.Count(n => n!["id"]!.GetValue<int>() < secId), target);
                }
                foreach (var lesson in JsonNode.Parse(json)!.AsArray().ToArray())
                    target["lessons"]!.AsArray().Add(lesson!.DeepClone());
                return doc;
            });
            if (error != null)
            {
                publish.Enabled = true;
                await Dlg.Alert(this, "НЕ ВЫШЛО", error);
                return;
            }
            unpublished.Remove(sec);
            Show(title, Build());
        };
        box.Children.Add(publish);
        return Ui.Card(box, Pal.Amber, 12);
    }

    async Task FindUnpublished()
    {
        var onServer = Ids(-1);
        var builtIn = JsonNode.Parse(await AppState.ReadAssetText("questionnaires.json"))!["items"]!.AsArray();
        unpublished = builtIn.Where(n => !onServer.Contains(Str(n!, "id"))).Select(n => n!).ToList();
    }

    // кнопка публикации: сервер важнее встроенного контента, поэтому без неё новый опросник никто не увидит
    View UnpublishedCard(JsonNode item)
    {
        string quizTitle = Str(item, "title");
        string json = item.ToJsonString();

        var box = new VerticalStackLayout { Spacing = 6 };
        box.Children.Add(Ui.Text(quizTitle, 14, Pal.Ink, true));
        box.Children.Add(Ui.Text($"{item["questions"]?.AsArray().Count ?? 0} вопросов. Есть в приложении, на сервере ещё нет.", 12, Pal.Ink));

        var publish = new Btn("ОПУБЛИКОВАТЬ ДЛЯ ВСЕХ", Pal.Violet, Pal.Paper, 14);
        publish.Clicked += async (s, e) =>
        {
            if (!await Dlg.Confirm(this, "ОПУБЛИКОВАТЬ?", $"Опросник «{quizTitle}» появится у всех пользователей при следующем запуске приложения.", "ОПУБЛИКОВАТЬ", "ОТМЕНА"))
                return;
            publish.Enabled = false;
            var error = await SaveDoc(doc => { doc["items"]!.AsArray().Add(JsonNode.Parse(json)); return doc; });
            if (error != null)
            {
                publish.Enabled = true;
                await Dlg.Alert(this, "НЕ ВЫШЛО", error);
                return;
            }
            unpublished.Remove(item);
            Show(title, Build());
        };
        box.Children.Add(publish);
        return Ui.Card(box, Pal.Amber, 12);
    }

    View Build()
    {
        var stack = List();
        stack.Children.Add(Ui.Text($"ВЕРСИЯ {version}. Нажми на элемент, чтобы открыть редактор.", 11, Pal.Ink2, true));

        if (name == "lessons")
        {
            foreach (var sec in unpublished)
                stack.Children.Add(UnpublishedSectionCard(sec));
            if (theoryUnpublished.Count > 0)
                stack.Children.Add(UnpublishedTheoryCard());

            var sections = root!["sections"]!.AsArray();
            for (int si = 0; si < sections.Count; si++)
            {
                var sec = sections[si]!;
                stack.Children.Add(Ui.Box(Ui.Text($"ЮНИТ {sec["id"]}  //  {Str(sec, "title").ToUpperInvariant()}", 14, Pal.Ink, true), Pal.Amber, 10));
                var lessons = sec["lessons"]!.AsArray();
                for (int li = 0; li < lessons.Count; li++)
                    AddLesson(stack, sec, lessons[li]!, si, li);

                int sIdx = si;
                string secTitle = Str(sec, "title");
                var add = new Btn("+ УРОК", Pal.Paper2, null, 14);
                add.Clicked += async (o, e) =>
                {
                    var lesson = new Lesson { Id = $"{sec["id"]}-{lessons.Count + 1}", RecommendedLevel = 1 };
                    await Push(new AdminLessonPage(lesson, secTitle, true, LessonIds(null),
                        json => Save(json, typeof(Lesson), (doc, item) => { doc["sections"]![sIdx]!["lessons"]!.AsArray().Add(item); return doc; }),
                        null));
                };
                stack.Children.Add(add);
            }
        }
        else if (name == "questionnaires")
        {
            foreach (var item in unpublished)
                stack.Children.Add(UnpublishedCard(item));

            var items = root!["items"]!.AsArray();
            for (int i = 0; i < items.Count; i++)
                AddQuiz(stack, items[i]!, i);

            var add = new Btn("+ НОВЫЙ ОПРОСНИК", Pal.Violet, Pal.Paper);
            add.Clicked += async (s, e) =>
            {
                var quiz = new Questionnaire
                {
                    Emoji = "",
                    Kind = "choice",
                    Mode = "scales",
                    Scales = { new QScale { Key = "s1", Name = "Шкала 1" } }
                };
                await Push(new AdminQuizPage(quiz, true, Ids(-1),
                    json => Save(json, typeof(Questionnaire), (doc, item) => { doc["items"]!.AsArray().Add(item); return doc; }),
                    null));
            };
            stack.Children.Add(add);
        }
        else if (name == "reference")
        {
            var entries = root!["entries"]!.AsArray();
            var add = new Btn("+ НОВАЯ СТАТЬЯ", Pal.Violet, Pal.Paper);
            add.Clicked += async (_, _) => await Push(new AdminReferenceEntryPage(new RefEntry(),
                entries.Select(n => Str(n!, "term")).ToList(),
                text => Save(text, typeof(RefEntry), (doc, item) => { doc["entries"]!.AsArray().Add(item); return doc; })));
            stack.Children.Add(add);
            for (int i = 0; i < entries.Count; i++)
            {
                int index = i;
                string json = entries[i]!.ToJsonString();
                var edit = new Btn(Str(entries[i]!, "term"), Pal.Paper, Pal.Ink, 14) { MinimumHeightRequest = 48 };
                edit.Clicked += async (_, _) => await Push(new AdminReferenceEntryPage(
                    JsonSerializer.Deserialize<RefEntry>(json, AppState.JsonOptions)!,
                    entries.Where((_, j) => j != index).Select(n => Str(n!, "term")).ToList(),
                    text => Save(text, typeof(RefEntry), (doc, item) => { doc["entries"]![index] = item; return doc; }),
                    () => SaveDoc(doc => { doc["entries"]!.AsArray().RemoveAt(index); return doc; })));
                stack.Children.Add(edit);
            }
        }
        else
            stack.Children.Add(Ui.Text("Уровни, пороги рейтинга и открытие разделов редактируются через «Весь файл».", 14));

        var whole = new Btn("ВЕСЬ ФАЙЛ ✎", Pal.Paper2);
        whole.Clicked += async (s, e) => await Push(new AdminEditPage(title + " // ФАЙЛ", root!.ToJsonString(Pretty),
            json => Save(json, name switch { "lessons" => typeof(LessonsFile), "reference" => typeof(RefFile),
                "rating" => typeof(RatingFile), _ => typeof(QuestionnaireFile) }, (doc, item) => item)));
        stack.Children.Add(whole);

        var history = new Btn("ИСТОРИЯ ВЕРСИЙ ▶", Pal.Paper2);
        history.Clicked += async (s, e) => await Push(new AdminHistoryPage(name, title));
        stack.Children.Add(history);

        return stack;
    }

    // урок открывается в визуальном редакторе
    void AddLesson(VerticalStackLayout stack, JsonNode sec, JsonNode node, int si, int li)
    {
        var box = new VerticalStackLayout { Spacing = 2 };
        box.Children.Add(Ui.Text($"▸ {Str(node, "title")}", 14, Pal.Ink, true));
        box.Children.Add(Ui.Text($"{node["tasks"]?.AsArray().Count ?? 0} заданий  //  id {Str(node, "id")}", 11, Pal.Ink2, true));
        var card = Ui.Card(box, Pal.Paper, 12);
        string json = node.ToJsonString();
        string secTitle = Str(sec, "title");
        string id = Str(node, "id");
        Ui.OnTap(card, async () =>
        {
            Lesson? lesson;
            try { lesson = JsonSerializer.Deserialize<Lesson>(json, AppState.JsonOptions); }
            catch (JsonException) { lesson = null; }
            if (lesson == null)
            {
                await Dlg.Alert(this, "ОШИБКА", "Урок не читается. Исправь его через «ВЕСЬ ФАЙЛ ✎».");
                return;
            }
            await Push(new AdminLessonPage(lesson, secTitle, false, LessonIds(id),
                text => Save(text, typeof(Lesson), (doc, item) => { doc["sections"]![si]!["lessons"]![li] = item; return doc; }),
                () => SaveDoc(doc => { doc["sections"]![si]!["lessons"]!.AsArray().RemoveAt(li); return doc; })));
        });
        stack.Children.Add(card);
    }

    // id всех уроков, кроме редактируемого: новый не должен с ними совпасть
    List<string> LessonIds(string? except) => root!["sections"]!.AsArray()
        .SelectMany(s => s!["lessons"]!.AsArray())
        .Select(l => Str(l!, "id"))
        .Where(x => x != except)
        .ToList();

    // опросник открывается в визуальном редакторе
    void AddQuiz(VerticalStackLayout stack, JsonNode node, int index)
    {
        var box = new VerticalStackLayout { Spacing = 2 };
        box.Children.Add(Ui.Text($"{Str(node, "emoji")} {Str(node, "title")}", 14, Pal.Ink, true));
        box.Children.Add(Ui.Text($"{node["questions"]?.AsArray().Count ?? 0} вопросов  //  id {Str(node, "id")}", 11, Pal.Ink2, true));
        var card = Ui.Card(box, Pal.Paper, 12);
        string json = node.ToJsonString();
        Ui.OnTap(card, async () =>
        {
            Questionnaire? quiz;
            try { quiz = JsonSerializer.Deserialize<Questionnaire>(json, AppState.JsonOptions); }
            catch (JsonException) { quiz = null; }
            if (quiz == null)
            {
                await Dlg.Alert(this, "ОШИБКА", "Опросник не читается. Исправь его через «ВЕСЬ ФАЙЛ ✎».");
                return;
            }
            await Push(new AdminQuizPage(quiz, false, Ids(index),
                text => Save(text, typeof(Questionnaire), (doc, item) => { doc["items"]![index] = item; return doc; }),
                () => SaveDoc(doc => { doc["items"]!.AsArray().RemoveAt(index); return doc; })));
        });
        stack.Children.Add(card);
    }

    // id остальных опросников: новый не должен с ними совпасть
    List<string> Ids(int except) => root!["items"]!.AsArray()
        .Select((n, i) => (n, i)).Where(x => x.i != except)
        .Select(x => Str(x.n!, "id")).ToList();

    // правка проверяется моделью приложения, подставляется в копию документа и уходит на сервер;
    // null — сохранено, иначе текст ошибки для редактора
    async Task<string?> Save(string json, Type model, Func<JsonNode, JsonNode, JsonNode> place)
    {
        JsonNode item;
        try
        {
            item = JsonNode.Parse(json) ?? throw new JsonException("пустой JSON");
            JsonSerializer.Deserialize(json, model, AppState.JsonOptions);
        }
        catch (JsonException ex)
        {
            return "Ошибка JSON: " + ex.Message;
        }

        return await SaveDoc(doc => place(doc, item));
    }

    // меняет копию документа и отправляет её на сервер; null — сохранено, иначе текст ошибки
    async Task<string?> SaveDoc(Func<JsonNode, JsonNode> change)
    {
        var doc = change(JsonNode.Parse(root!.ToJsonString())!);
        string docJson = doc.ToJsonString();
        var r = await Api.SaveContent(name, version, docJson);
        if (!r.Ok) return r.Error;

        version = r.Body.GetProperty("version").GetInt32();
        root = doc;
        AppState.WriteCache(name, docJson, version);
        await AppState.LoadContent();
        return null;
    }

    static string Str(JsonNode node, string key) =>
        node[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
}
