using PsyPath.Models;
using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

// карточки по справочнику: термин → «ПОКАЗАТЬ» → определение → «ЗНАЮ» / «НЕ ЗНАЮ».
// «Знаю» отодвигает повтор на 1, 3, 7, 14, 30 дней, «не знаю» — в конец этой же сессии и завтра заново
public class FlashcardsPage : AdminBase
{
    readonly List<RefEntry> queue;
    readonly HashSet<RefEntry> retried = new();
    readonly int total;
    int known;

    public FlashcardsPage()
    {
        queue = Practice.PickCards();
        total = queue.Count;
        if (total == 0) ShowEmpty();
        else ShowCard(false);
    }

    void ShowEmpty()
    {
        var s = List();
        s.Children.Add(Ui.Mascot(150, idle: true));
        string next = Practice.NextCardDate() is { } d ? $" Следующие карточки — {d}." : "";
        s.Children.Add(Ui.Box(Ui.Text("На сегодня все карточки повторены." + next, 14), Pal.Paper, 14));
        var back = new Btn("НАЗАД ◀", Pal.Amber);
        back.Clicked += async (o, e) => await Pop();
        s.Children.Add(back);
        Show("КАРТОЧКИ", s);
    }

    void ShowCard(bool flipped)
    {
        var e = queue[0];
        var s = List();
        int done = total - queue.Count;
        s.Children.Add(Ui.Text($"КАРТОЧКА {Math.Min(done + 1, total)}/{total}", 11, Pal.Ink2, true, TextAlignment.End));
        s.Children.Add(new Bar(Pal.Teal) { Value = (double)done / total });

        var face = new VerticalStackLayout { Spacing = 10, MinimumHeightRequest = 220 };
        face.Children.Add(Ui.Text(flipped ? "ОПРЕДЕЛЕНИЕ" : "ТЕРМИН", 11, Pal.TealInk, true));
        face.Children.Add(Ui.Text(e.Term.ToUpperInvariant(), 22, Pal.Ink, true, TextAlignment.Center));
        if (flipped)
        {
            face.Children.Add(Ui.Text(e.Short, 14, Pal.RedInk, true, TextAlignment.Center));
            face.Children.Add(Ui.Text(e.Text, 13));
        }
        else
        {
            face.Children.Add(Ui.Text("Вспомни определение, потом открой карточку.", 12, Pal.Ink2, false, TextAlignment.Center));
        }
        var card = Ui.Box(face, flipped ? Pal.Paper : Pal.Paper2, 16);
        s.Children.Add(card);

        var bottom = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(16, 8, 16, 12), BackgroundColor = Pal.Paper };
        if (!flipped)
        {
            var show = new Btn("ПОКАЗАТЬ", Pal.Amber);
            show.Clicked += async (o, ev) =>
            {
                Sound.Play("flip");
                // «переворот»: карточка сжимается по ширине и раскрывается уже с определением
                await card.Zoom(0.92, 90, Easing.CubicIn);
                ShowCard(true);
            };
            bottom.Children.Add(show);
        }
        else
        {
            var row = new Grid
            {
                ColumnSpacing = 10,
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
            };
            var no = new Btn("НЕ ЗНАЮ", Pal.RedInk, Pal.Paper, 15);
            no.Clicked += (o, ev) => Answer(e, false);
            var yes = new Btn("ЗНАЮ", Pal.GreenInk, Pal.Paper, 15);
            yes.Clicked += (o, ev) => Answer(e, true);
            Grid.SetColumn(yes, 1);
            row.Children.Add(no);
            row.Children.Add(yes);
            bottom.Children.Add(row);
        }

        Show("КАРТОЧКИ", s, bottom);
        if (flipped)
        {
            card.Scale = 0.92;
            _ = card.Zoom(1, 140, Easing.CubicOut);
        }
    }

    void Answer(RefEntry e, bool know)
    {
        Practice.Answer(e, know);
        queue.RemoveAt(0);
        if (know) known++;
        // забытую карточку показываем ещё раз в конце, но только один раз
        else if (retried.Add(e)) queue.Add(e);
        AppState.Save();

        if (queue.Count > 0) ShowCard(false);
        else Finish();
    }

    void Finish()
    {
        // 2 XP за каждую вспомненную карточку
        int xp = known * 2;
        if (xp > 0) AppState.AddXp(xp);
        AppState.RegisterActivity();
        AppState.CheckAchievements();
        AppState.Save();
        Sound.Play("win");

        var s = List();
        s.Children.Add(new Glitch("ГОТОВО", 26));
        s.Children.Add(Ui.Mascot(150, idle: true));
        var stats = new VerticalStackLayout { Spacing = 4 };
        stats.Children.Add(Ui.Text($"Вспомнил: {known} из {total}", 16, Pal.Ink, true, TextAlignment.Center));
        stats.Children.Add(Ui.Text($"+{xp} XP", 20, Pal.Ink, true, TextAlignment.Center));
        s.Children.Add(Ui.Box(stats, Pal.Amber, 14));
        s.Children.Add(Ui.Text("Вспомненные карточки вернутся позже: через день, три, неделю, две и месяц.", 12, Pal.Ink2));

        var back = new Btn("ПРОДОЛЖИТЬ ▶", Pal.GreenInk, Pal.Paper);
        back.Clicked += async (o, e) => await Pop();
        s.Children.Add(back);
        Show("КАРТОЧКИ", s);
    }
}
