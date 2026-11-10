using System.Globalization;
using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

public class ReleaseNotesPage : AdminBase
{
    public ReleaseNotesPage()
    {
        Show("ЧТО НОВОГО", Message("Загружаю историю изменений…"));
        _ = Load();
    }

    async Task Load()
    {
        try
        {
            var notes = await ReleaseNotes.Load();
            var list = List();
            list.Spacing = 16;
            list.Children.Add(Ui.Text($"Установлена версия {AppInfo.Current.VersionString}", 14, Pal.Ink2));
            foreach (var note in notes)
            {
                bool current = note.Version == ReleaseNotes.Normalize(AppInfo.Current.Version);
                var content = new VerticalStackLayout { Spacing = 12 };
                var heading = Ui.Text($"Версия {note.Version}", 20, Pal.Ink, true);
                SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
                content.Children.Add(heading);
                string date = note.Date?.ToString("dd MMMM yyyy", CultureInfo.GetCultureInfo("ru-RU")) ?? "";
                content.Children.Add(Ui.Text(string.Join(" · ", new[] { current ? "Текущая версия" : "Предыдущая версия", date }
                    .Where(x => x.Length > 0)), 12, Pal.Ink2, true));
                if (note.Changes.Count == 0)
                    content.Children.Add(Ui.Text(ReleaseNotes.Format(note.Changes), 15));
                foreach (string change in note.Changes)
                {
                    var text = Ui.Text("• " + change, 15);
                    text.LineHeight = 1.5;
                    content.Children.Add(text);
                }
                list.Children.Add(Ui.Box(content, current ? Pal.Paper : Pal.Paper2, 16));
            }
            if (notes.Count == 0) list.Children.Add(Ui.Text("Для этой версии пока нет записей об изменениях.", 15));
            Show("ЧТО НОВОГО", list);
        }
        catch (Exception ex)
        {
            CrashLog.Write("Release notes", ex);
            Show("ЧТО НОВОГО", Message("Не удалось прочитать историю изменений этой версии.", Pal.RedInk));
        }
    }
}
