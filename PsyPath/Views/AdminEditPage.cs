using PsyPath.UI;

namespace PsyPath.Views;

// редактор JSON одного элемента; save возвращает null при успехе или текст ошибки
public class AdminEditPage : AdminBase
{
    readonly Func<string, Task<string?>> save;
    readonly Editor editor;
    readonly Label status = Ui.Text("", 12, Pal.RedInk, true);
    string saved;
    bool busy;

    public AdminEditPage(string title, string json, Func<string, Task<string?>> save)
    {
        this.save = save;
        saved = json;

        editor = new Editor
        {
            Text = json,
            FontFamily = "Mono",
            FontSize = 12,
            TextColor = Pal.Ink,
            BackgroundColor = Colors.Transparent,
            AutoSize = EditorAutoSizeOption.TextChanges,
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false
        };
        var content = new VerticalStackLayout { Padding = new Thickness(18, 8, 18, 16) };
        content.Children.Add(Ui.Card(editor, Pal.Paper2, 8));

        var saveBtn = new Btn("СОХРАНИТЬ ✓", Pal.GreenInk, Pal.Paper);
        saveBtn.Clicked += async (s, e) => await DoSave();
        var bottom = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(16, 8, 16, 16), BackgroundColor = Pal.Paper };
        bottom.Children.Add(status);
        bottom.Children.Add(saveBtn);

        Show(title.ToUpperInvariant(), content, bottom);
    }

    async Task DoSave()
    {
        if (busy) return;
        busy = true;
        status.TextColor = Pal.Ink2;
        status.Text = "Сохраняю…";
        string text = editor.Text ?? "";
        var error = await save(text);
        busy = false;
        if (error != null)
        {
            status.TextColor = Pal.RedInk;
            status.Text = error;
            return;
        }
        saved = text;
        await Pop();
    }

    protected override async Task Leave()
    {
        if (busy) return;
        if (editor.Text != saved &&
            !await Dlg.Confirm(this, "ВЫЙТИ?", "Несохранённые правки пропадут.", "ВЫЙТИ", "ОСТАТЬСЯ"))
            return;
        await Pop();
    }
}
