using PsyPath.Models;
using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

// карточки по справочнику: термин → «ПОКАЗАТЬ» → определение → оценка, как в Anki.
// Лёгкие карточки уходят на долгий срок, трудные возвращаются скоро, «снова» — ещё раз в этой же сессии
public class FlashcardsPage : AdminBase
{
    // через сколько карточек вернётся та, что получила «снова»
    const int RepeatGap = 3;

    readonly List<RefEntry> queue;
    readonly HashSet<RefEntry> missed = new();
    readonly int total;
    int firstTry;

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
        var back = new Btn("НАЗАД ◀", Pal.Violet, Pal.Paper);
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
            var show = new Btn("ПОКАЗАТЬ", Pal.Violet, Pal.Paper);
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
            var buttons = new Grid
            {
                ColumnSpacing = 8,
                RowSpacing = 4,
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
                RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) }
            };
            AddRating(buttons, e, CardRating.Again, "СНОВА", Pal.RedInk, Pal.Paper, 0, 0);
            AddRating(buttons, e, CardRating.Hard, "ТРУДНО", Pal.Orange, Pal.Ink, 0, 1);
            AddRating(buttons, e, CardRating.Good, "ХОРОШО", Pal.Violet, Pal.Paper, 1, 0);
            AddRating(buttons, e, CardRating.Easy, "ЛЕГКО", Pal.GreenInk, Pal.Paper, 1, 1);
            bottom.Children.Add(Ui.Text("Насколько быстро вспомнил?", 12, Pal.Ink2, true, TextAlignment.Center));
            bottom.Children.Add(buttons);
        }

        Show("КАРТОЧКИ", s, bottom);
        if (flipped)
        {
            card.Scale = 0.92;
            _ = card.Zoom(1, 140, Easing.CubicOut);
        }
    }

    // на кнопке — оценка и когда карточка вернётся
    void AddRating(Grid grid, RefEntry e, CardRating rating, string name, Color fill, Color text, int row, int column)
    {
        int days = Practice.PlanFor(e, rating).Days;
        string when = days == 0 ? "сегодня" : days == 1 ? "завтра" : $"через {days} дн.";
        var button = new Btn($"{name}\n{when}", fill, text, 14);
        button.Clicked += (o, ev) => Answer(e, rating);
        Grid.SetRow(button, row);
        Grid.SetColumn(button, column);
        grid.Children.Add(button);
    }

    void Answer(RefEntry e, CardRating rating)
    {
        Practice.Answer(e, rating);
        queue.RemoveAt(0);
        if (rating == CardRating.Again)
        {
            missed.Add(e);
            queue.Insert(Math.Min(RepeatGap, queue.Count), e);
        }
        else if (!missed.Contains(e))
        {
            firstTry++;
        }
        AppState.Save();

        if (queue.Count > 0) ShowCard(false);
        else Finish();
    }

    void Finish()
    {
        // 2 XP за карточку, вспомненную с первого раза: «снова» можно нажимать сколько угодно
        int xp = firstTry * 2;
        if (xp > 0) AppState.AddXp(xp);
        AppState.RegisterActivity();
        AppState.CheckAchievements();
        AppState.Save();
        Sound.Play("win");

        var s = List();
        s.Children.Add(new Glitch("ГОТОВО", 26));
        s.Children.Add(Ui.Mascot(150, idle: true));
        var stats = new VerticalStackLayout { Spacing = 4 };
        stats.Children.Add(Ui.Text($"С первого раза: {firstTry} из {total}", 16, Pal.Ink, true, TextAlignment.Center));
        stats.Children.Add(Ui.Text($"+{xp} XP", 20, Pal.Ink, true, TextAlignment.Center));
        s.Children.Add(Ui.Box(stats, Pal.Amber, 14));
        s.Children.Add(Ui.Text("Лёгкие карточки вернутся нескоро, трудные — раньше.", 12, Pal.Ink2));

        var back = new Btn("ПРОДОЛЖИТЬ ▶", Pal.GreenInk, Pal.Paper);
        back.Clicked += async (o, e) => await Pop();
        s.Children.Add(back);
        Show("КАРТОЧКИ", s);
    }
}
