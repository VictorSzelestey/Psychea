using System.Text.Json;
using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

// топ игроков по XP: список приходит с сервера при каждом открытии
public class TopPage : AdminBase
{
    bool loading;

    public TopPage() => Show("ТОП-10 ИГРОКОВ", Message("Загружаю…"));

    protected override void Refresh() => _ = Load();

    async Task Load()
    {
        if (loading) return;
        loading = true;
        // сначала уходит свой свежий прогресс; без сети ждём не дольше 3 с
        await Task.WhenAny(AppState.PushNow(), Task.Delay(3000));
        var r = await Api.Top();
        loading = false;

        if (r.Ok) ShowTop(r.Body);
        else ShowError(r.Error);
    }

    void ShowError(string error)
    {
        var s = List();
        s.Children.Add(Ui.Text(error, 13, Pal.RedInk, true));
        var again = new Btn("ПОВТОРИТЬ", Pal.Violet, Pal.Paper);
        again.Clicked += async (o, e) => await Load();
        s.Children.Add(again);
        Show("ТОП-10 ИГРОКОВ", s);
    }

    void ShowTop(JsonElement body)
    {
        bool hidden = body.GetProperty("hidden").GetBoolean();
        long? place = body.GetProperty("place").ValueKind == JsonValueKind.Number ? body.GetProperty("place").GetInt64() : null;
        bool admin = Api.User?.IsAdmin == true;

        var s = List();
        if (!admin)
        {
            s.Children.Add(Form.Choice("ПОКАЗЫВАТЬ МЕНЯ В ТОПЕ", new[] { ("1", "ДА"), ("0", "НЕТ") }, hidden ? "0" : "1", async v =>
            {
                await Api.SetTopHidden(v == "0");
                await Load();
            }));
            string? note = hidden ? "Ты скрыт: другие не видят твоё имя и XP."
                : place == null ? "Набери первые XP, чтобы попасть в топ."
                : place > 10 ? $"Твоё место: {place}."
                : null;
            if (note != null) s.Children.Add(Ui.Text(note, 13, Pal.Ink2, true));
        }

        // Ограничение и на клиенте: старый сервер пока может присылать 50 строк.
        var top = body.GetProperty("top").EnumerateArray().Take(10).ToList();
        if (top.Count == 0) s.Children.Add(Ui.Text("Пока здесь никого нет.", 13, Pal.Ink2));
        foreach (var row in top)
            s.Children.Add(Row(row.GetProperty("place").GetInt64(), row.GetProperty("name").GetString() ?? "",
                row.GetProperty("xp").GetInt32(), row.GetProperty("me").GetBoolean()));

        Show("ТОП-10 ИГРОКОВ", s, animate: true);
    }

    static View Row(long place, string name, int xp, bool me)
    {
        var g = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(58)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        var rank = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
        if (place <= 3)
            rank.Children.Add(new PixelIcon(Icons.Cup, place == 1 ? Pal.Orange : place == 2 ? Pal.Gray : Pal.Violet, 20, Pal.Paper));
        rank.Children.Add(Ui.Text(place.ToString(), 14, Pal.Ink2, true));
        g.Children.Add(rank);

        var title = Ui.Text(me ? name + " (ты)" : name, 14, Pal.Ink, me);
        title.LineBreakMode = LineBreakMode.TailTruncation;
        title.VerticalTextAlignment = TextAlignment.Center;
        Grid.SetColumn(title, 1);
        g.Children.Add(title);

        var value = Ui.Text($"{xp} XP", 13, Pal.Ink, true, TextAlignment.End);
        value.VerticalTextAlignment = TextAlignment.Center;
        Grid.SetColumn(value, 2);
        g.Children.Add(value);

        return Ui.Card(g, me ? Pal.Amber : Pal.Paper, 12);
    }
}
