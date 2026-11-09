using System.Globalization;
using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

// прошлые версии документа; откат делает выбранную версию текущей, а текущая уходит в историю
public class AdminHistoryPage : AdminBase
{
    readonly string name;
    readonly string title;
    bool busy;

    public AdminHistoryPage(string name, string title)
    {
        this.name = name;
        this.title = title + " // ИСТОРИЯ";
        Show(this.title, Message("ЗАГРУЗКА…"));
        _ = Load();
    }

    async Task Load()
    {
        var r = await Api.ContentHistory(name);
        if (!r.Ok)
        {
            Show(title, Message(r.Error, Pal.RedInk));
            return;
        }

        var stack = List();
        stack.Children.Add(Ui.Text("Нажми на версию, чтобы вернуть её.", 11, Pal.Ink2, true));
        int count = 0;
        foreach (var h in r.Body.EnumerateArray())
        {
            count++;
            int version = h.GetProperty("version").GetInt32();
            var at = DateTime.Parse(h.GetProperty("changedAt").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var box = new VerticalStackLayout { Spacing = 2 };
            box.Children.Add(Ui.Text($"ВЕРСИЯ {version}", 14, Pal.Ink, true));
            box.Children.Add(Ui.Text($"заменена {at.ToLocalTime():dd.MM.yyyy HH:mm}, {h.GetProperty("changedBy").GetString()}", 11, Pal.Ink2));
            var card = Ui.Card(box, Pal.Paper, 12);
            Ui.OnTap(card, async () => await Restore(version));
            stack.Children.Add(card);
        }
        if (count == 0) stack.Children.Add(Ui.Text("Правок ещё не было.", 12, Pal.Ink2));
        Show(title, stack, animate: true);
    }

    async Task Restore(int version)
    {
        if (busy) return;
        if (!await Dlg.Confirm(this, "ОТКАТ", $"Вернуть версию {version}? Текущая сохранится в истории.", "ВЕРНУТЬ", "ОТМЕНА"))
            return;
        busy = true;
        var r = await Api.Rollback(name, version);
        if (r.Ok) await AppState.RefreshContent(name);
        busy = false;
        await Dlg.Alert(this, r.Ok ? "ГОТОВО" : "ОШИБКА",
            r.Ok ? $"Теперь текущая версия {r.Body.GetProperty("version").GetInt32()}." : r.Error);
        await Load();
    }
}
