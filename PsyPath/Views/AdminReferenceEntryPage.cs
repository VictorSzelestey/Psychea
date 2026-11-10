using System.Text.Json;
using PsyPath.Models;
using PsyPath.UI;

namespace PsyPath.Views;

public class AdminReferenceEntryPage : AdminBase
{
    readonly RefEntry entry;
    readonly string original;
    bool busy;
    bool saved;

    string Json() => JsonSerializer.Serialize(entry, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public AdminReferenceEntryPage(RefEntry entry, IReadOnlyCollection<string> otherTerms,
        Func<string, Task<string?>> save, Func<Task<string?>>? delete = null)
    {
        this.entry = entry;
        original = Json();
        var list = List();
        list.Children.Add(Form.Field("ТЕРМИН", entry.Term, text => entry.Term = text));
        list.Children.Add(Form.Field("КОРОТКОЕ ОПРЕДЕЛЕНИЕ", entry.Short, text => entry.Short = text, multiline: true));
        list.Children.Add(Form.Field("СТАТЬЯ", entry.Text, text => entry.Text = text, multiline: true));
        var status = Ui.Text("", 14, Pal.RedInk);
        list.Children.Add(status);
        var submit = new Btn("СОХРАНИТЬ СТАТЬЮ", Pal.Violet, Pal.Paper) { MinimumHeightRequest = 48 };
        submit.Clicked += async (_, _) =>
        {
            if (busy) return;
            if (string.IsNullOrWhiteSpace(entry.Term) || string.IsNullOrWhiteSpace(entry.Short) || string.IsNullOrWhiteSpace(entry.Text))
            { status.Text = "Заполни термин, определение и текст статьи."; return; }
            entry.Term = entry.Term.Trim();
            if (otherTerms.Any(term => string.Equals(term.Trim(), entry.Term, StringComparison.OrdinalIgnoreCase)))
            { status.Text = "Такой термин уже есть в справочнике."; return; }
            busy = true;
            submit.Enabled = false;
            try
            {
                status.Text = "Сохраняю…";
                string? error = await save(Json());
                if (error != null) { status.Text = error; return; }
                saved = true;
                await Pop();
            }
            finally { busy = false; submit.Enabled = true; }
        };
        list.Children.Add(submit);
        if (delete != null)
        {
            var remove = new Btn("УДАЛИТЬ СТАТЬЮ", Pal.Paper2, Pal.RedInk, 14) { MinimumHeightRequest = 48 };
            remove.Clicked += async (_, _) =>
            {
                if (busy) return;
                busy = true;
                try
                {
                    if (!await Dlg.Confirm(this, "УДАЛИТЬ СТАТЬЮ?", "Статья исчезнет из справочника у всех пользователей.", "УДАЛИТЬ", "ОТМЕНА")) return;
                    string? error = await delete();
                    if (error != null) { status.Text = error; return; }
                    saved = true;
                    await Pop();
                }
                finally { busy = false; }
            };
            list.Children.Add(remove);
        }
        Show("СТАТЬЯ СПРАВОЧНИКА", list);
    }

    protected override async Task Leave()
    {
        if (busy) return;
        if (!saved && original != Json()
            && !await Dlg.Confirm(this, "ВЫЙТИ?", "Несохранённые правки пропадут.", "ВЫЙТИ", "ОСТАТЬСЯ")) return;
        await Pop();
    }
}
