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
    // опросники, которые есть в этой версии приложения, но ещё не опубликованы на сервере
    List<JsonNode> unpublished = new();

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
        Show(title, Build(), animate: true);
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
