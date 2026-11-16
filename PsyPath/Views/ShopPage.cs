using Microsoft.Maui.Controls.Shapes;
using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

// магазин за кристаллы: темы оформления и аватарки. Купленное остаётся навсегда и переносится с аккаунтом.
// Темы островов не продаются — открываются вместе с островом. Промокод из админки дарит кристаллы или предмет
public class ShopPage : AdminBase
{
    // введённый промокод переживает пересборку экрана после покупки
    string promo = "";
    bool redeeming;

    public ShopPage() => Build();

    void Build()
    {
        var s = List();
        var balance = Ui.Lcd(Crystals.Balance.ToString(), 20, Icons.Crystal, Pal.Teal, Pal.Cyan);
        balance.HorizontalOptions = LayoutOptions.Center;
        balance.Padding = new Thickness(22, 10);
        SemanticProperties.SetDescription(balance, $"У тебя {Crystals.Balance} {Crystals.Word(Crystals.Balance)}");
        s.Children.Add(balance);
        s.Children.Add(Ui.Text($"Кристаллы дают за новый урок (+{Crystals.ForLesson}, без ошибок ещё +{Crystals.ForPerfect}), " +
            $"за опросник (+{Crystals.ForQuestionnaire} в день) и за рубежи серии: 5, 10, 20, 30, 50, 100 дней — столько, сколько дней.",
            12, Pal.Ink2));

        s.Children.Add(Form.Section("ПРОМОКОД"));
        s.Children.Add(PromoCard());

        s.Children.Add(Form.Section("ПОЛЕЗНОЕ"));
        s.Children.Add(FreezeCard());

        s.Children.Add(Form.Section("ТЕМЫ"));
        foreach (var theme in Themes.All)
            s.Children.Add(ThemeCard(theme));

        s.Children.Add(Form.Section("АВАТАРКИ"));
        var grid = new Grid
        {
            ColumnSpacing = 10,
            RowSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };
        var paid = Avatars.All.Where(a => a.Price > 0).ToList();
        for (int i = 0; i < paid.Count; i++)
        {
            if (i % 3 == 0) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var card = AvatarCard(paid[i].Id, paid[i].Name, paid[i].Price);
            Grid.SetRow(card, i / 3);
            Grid.SetColumn(card, i % 3);
            grid.Children.Add(card);
        }
        s.Children.Add(grid);

        s.Children.Add(Form.Section("РАМКИ АВАТАРКИ"));
        s.Children.Add(Ui.Text("Видны всем в топ-10. Нажми на надетую, чтобы снять.", 11, Pal.Ink2));
        foreach (var f in Cosmetics.Frames)
            s.Children.Add(FrameCard(f));

        s.Children.Add(Form.Section("НАРЯДЫ МАНУЛА"));
        s.Children.Add(Ui.Text("Манул носит наряд на главной и на экранах итогов. Нажми на надетый, чтобы снять.", 11, Pal.Ink2));
        var outfits = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };
        for (int i = 0; i < Cosmetics.Outfits.Length; i++)
        {
            var card = OutfitCard(Cosmetics.Outfits[i]);
            Grid.SetColumn(card, i);
            outfits.Children.Add(card);
        }
        s.Children.Add(outfits);
        Show("МАГАЗИН", s);
    }

    View PromoCard()
    {
        var entry = new Entry
        {
            Text = promo,
            Placeholder = "например, PSY2026",
            PlaceholderColor = Pal.Ink2,
            TextColor = Pal.Ink,
            FontFamily = "Body",
            FontSize = 15 * Ui.Scale,
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
            Keyboard = Keyboard.Plain,
            BackgroundColor = Colors.Transparent
        };
        entry.TextChanged += (s, e) => promo = e.NewTextValue ?? "";
        var field = new Border
        {
            Content = entry,
            BackgroundColor = Pal.Paper2,
            Stroke = new SolidColorBrush(Pal.Line),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
            Padding = new Thickness(12, 2)
        };
        SemanticProperties.SetDescription(field, "Поле для промокода");

        var go = new Btn("АКТИВИРОВАТЬ", Pal.Violet, Pal.OnAccent, 14);
        go.Clicked += async (s, e) => await Redeem();
        entry.Completed += async (s, e) => await Redeem();

        var box = new VerticalStackLayout { Spacing = 8 };
        box.Children.Add(Ui.Text("Промокод дарит кристаллы или предмет из магазина. Каждый код — один раз на аккаунт.", 11, Pal.Ink2));
        box.Children.Add(field);
        box.Children.Add(go);
        return Ui.Card(box, Pal.Paper, 12);
    }

    async Task Redeem()
    {
        string code = promo.Trim();
        if (redeeming || code.Length == 0) return;
        if (!Api.LoggedIn)
        {
            await Dlg.Alert(this, "НУЖЕН АККАУНТ", "Промокоды проверяет сервер — войди в аккаунт в профиле.");
            return;
        }
        redeeming = true;
        var r = await Api.RedeemPromo(code);
        redeeming = false;
        if (!r.Ok)
        {
            Sound.Play("bad");
            await Dlg.Alert(this, "НЕ ВЫШЛО", r.Error);
            return;
        }
        var b = r.Body;
        string got = Crystals.GrantPromo(b.GetProperty("reward").GetString() ?? "", b.GetProperty("amount").GetInt32(), b.GetProperty("item").GetString() ?? "");
        promo = "";
        Sound.Play("win");
        await Dlg.Alert(this, "ПРОМОКОД ПРИНЯТ", got);
        Build();
    }

    // заморозка — расходник: покупается много раз, но держать можно не больше двух
    View FreezeCard()
    {
        int have = AppState.P.Freezes;
        bool full = have >= Streak.MaxFreezes;
        var row = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        row.Children.Add(new PixelIcon(Icons.Snow, Pal.Cyan, 30, Pal.Teal));
        var text = new VerticalStackLayout { Spacing = 2 };
        text.Children.Add(Ui.Text("Заморозка серии", 15, Pal.Ink, true));
        text.Children.Add(Ui.Text($"Пропустил день — серия не сгорит. Тратится сама. Есть {have} из {Streak.MaxFreezes}", 11, Pal.Ink2));
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        var tag = full ? Ui.Text("МАКСИМУМ", 11, Pal.Ink2, true, TextAlignment.Center) : PriceTag(false, false, Streak.FreezePrice, "");
        tag.VerticalOptions = LayoutOptions.Center;
        Grid.SetColumn(tag, 2);
        row.Children.Add(tag);

        var card = Ui.Card(row, Pal.Paper, 12);
        SemanticProperties.SetDescription(card, $"Заморозка серии, есть {have} из {Streak.MaxFreezes}" + (full ? "" : $", цена {Streak.FreezePrice} {Crystals.Word(Streak.FreezePrice)}"));
        Ui.OnTap(card, async () =>
        {
            if (full) return;
            int lack = Streak.FreezePrice - Crystals.Balance;
            if (lack > 0)
            {
                Sound.Play("bad");
                await Dlg.Alert(this, "НЕ ХВАТАЕТ", $"Нужно ещё {lack} {Crystals.Word(lack)}.");
                return;
            }
            if (!await Dlg.Confirm(this, "ПОКУПКА", $"Купить заморозку серии за {Streak.FreezePrice} {Crystals.Word(Streak.FreezePrice)}?", "КУПИТЬ", "ОТМЕНА")) return;
            Streak.BuyFreeze();
            Sound.Play("win");
            Build();
        });
        return card;
    }

    View FrameCard(AvatarFrame f)
    {
        bool owned = Crystals.Owns(f.Id, f.Price);
        bool current = AppState.P.Frame == f.Id;
        var row = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        row.Children.Add(Avatars.View(AppState.P.Avatar, Api.User?.DisplayName ?? "", 48, f.Id));
        var name = Ui.Text(f.Name, 15, Pal.Ink, true);
        name.VerticalTextAlignment = TextAlignment.Center;
        Grid.SetColumn(name, 1);
        row.Children.Add(name);
        var tag = PriceTag(owned, current, f.Price, "НАДЕТА");
        tag.VerticalOptions = LayoutOptions.Center;
        Grid.SetColumn(tag, 2);
        row.Children.Add(tag);

        var card = Ui.Card(row, current ? Pal.Amber : Pal.Paper, 12);
        SemanticProperties.SetDescription(card, $"Рамка {f.Name}, " + (current ? "надета" : owned ? "куплена" : $"цена {f.Price} {Crystals.Word(f.Price)}"));
        Ui.OnTap(card, async () =>
        {
            if (!owned && !await Purchase(this, $"рамку «{f.Name}»", f.Id, f.Price, "Она сразу появится на аватарке.")) return;
            Cosmetics.ToggleFrame(f.Id);
            Sound.Play("ok");
            Build();
        });
        return card;
    }

    View OutfitCard(Outfit o)
    {
        bool owned = Crystals.Owns(o.Id, o.Price);
        bool current = AppState.P.Outfit == o.Id;
        var box = new VerticalStackLayout { Spacing = 6 };
        box.Children.Add(new Image { Source = o.Image, HeightRequest = 70, WidthRequest = 70 * 900 / 811, Aspect = Aspect.AspectFit, HorizontalOptions = LayoutOptions.Center });
        box.Children.Add(Ui.Text(o.Name, 11, Pal.Ink, current, TextAlignment.Center));
        box.Children.Add(PriceTag(owned, current, o.Price, "НАДЕТ"));
        var card = Ui.Card(box, current ? Pal.Amber : Pal.Paper, 8);
        SemanticProperties.SetDescription(card, o.Name + ", " + (current ? "надет" : owned ? "куплен" : $"цена {o.Price} {Crystals.Word(o.Price)}"));
        Ui.OnTap(card, async () =>
        {
            if (!owned && !await Purchase(this, $"наряд «{o.Name}»", o.Id, o.Price, "Манул сразу его наденет.")) return;
            Cosmetics.ToggleOutfit(o.Id);
            Sound.Play("ok");
            Build();
        });
        return card;
    }

    // цена с кристаллом или состояние покупки; нужна и выбору аватарки
    internal static View PriceTag(bool owned, bool current, int price, string usedCaption)
    {
        if (current) return Ui.Text("✓ " + usedCaption, 11, Pal.GreenInk, true, TextAlignment.Center);
        if (owned) return Ui.Text(price == 0 ? "БЕСПЛАТНО" : "КУПЛЕНО", 11, Pal.Ink2, true, TextAlignment.Center);
        var row = new HorizontalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.Center };
        row.Children.Add(new PixelIcon(Icons.Crystal, Pal.Teal, 14, Pal.Cyan));
        row.Children.Add(Ui.Text(price.ToString(), 13, Pal.Ink, true));
        return row;
    }

    View ThemeCard(Theme theme)
    {
        var island = Islands.Find(theme.Island);
        // тема острова: открыта вместе с ним, до этого — замок вместо цены
        bool closed = !Islands.ThemeOpen(theme);
        bool owned = !closed && Crystals.Owns(theme.Id, theme.Price);
        bool current = Themes.Find(AppState.P.Theme).Id == theme.Id;

        // образец темы: фон, карточка, главная кнопка и акцент
        var swatch = new HorizontalStackLayout { Spacing = -6, VerticalOptions = LayoutOptions.Center };
        foreach (var c in new[] { theme.Bg, theme.Paper, theme.Primary, theme.Accent })
            swatch.Children.Add(new Border
            {
                BackgroundColor = c,
                Stroke = new SolidColorBrush(theme.Line),
                StrokeThickness = 1,
                StrokeShape = new Ellipse(),
                WidthRequest = 26,
                HeightRequest = 26
            });

        var row = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        row.Children.Add(swatch);
        var name = new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center };
        name.Children.Add(Ui.Text(theme.Name, 15, Pal.Ink, true));
        if (island != null)
            name.Children.Add(Ui.Text(island.Soon ? $"Скоро: остров «{island.Name}»"
                : closed ? $"Откроется на острове «{island.Name}»" : $"Тема острова «{island.Name}»", 11, Pal.Ink2));
        if (theme.Music != null)
            name.Children.Add(Ui.Text("♪ своя музыка в меню", 11, Pal.Ink2));
        Grid.SetColumn(name, 1);
        row.Children.Add(name);
        View tag;
        if (closed)
            tag = new PixelIcon(Icons.Lock, Pal.Ink2, 22, Pal.Paper) { HorizontalOptions = LayoutOptions.Center };
        else if (island != null && !current)
            tag = Ui.Text("ОТКРЫТА", 11, Pal.Ink2, true, TextAlignment.Center);
        else
            tag = PriceTag(owned, current, theme.Price, "ВКЛЮЧЕНА");
        tag.VerticalOptions = LayoutOptions.Center;
        Grid.SetColumn(tag, 2);
        row.Children.Add(tag);

        var card = Ui.Card(row, current ? Pal.Amber : Pal.Paper, 12);
        SemanticProperties.SetDescription(card, $"Тема {theme.Name}, " +
            (current ? "включена" : closed ? "закрыта" : owned ? "открыта" : $"цена {theme.Price} {Crystals.Word(theme.Price)}"));
        Ui.OnTap(card, async () =>
        {
            if (current) return;
            if (closed)
            {
                Sound.Play("bad");
                await Dlg.Alert(this, "ЗАКРЫТО", island!.Soon
                    ? $"Остров «{island.Name}» пока закрыт — тема появится вместе с ним."
                    : $"Тема откроется вместе с островом «{island.Name}». Пройди уроки предыдущего острова.");
                return;
            }
            if (!owned && !await Purchase(this, $"тему «{theme.Name}»", theme.Id, theme.Price, "Она сразу включится.")) return;
            Crystals.UseTheme(theme);
            Sound.Play("ok");
            // новые цвета нужны всем экранам — пересобираем вкладки, как после смены размера текста
            await SwitchRoot(new AppShell(AppShell.ProfileTab));
        });
        return card;
    }

    View AvatarCard(string id, string caption, int price)
    {
        bool owned = Crystals.Owns(id, price);
        bool current = AppState.P.Avatar == id;

        var box = new VerticalStackLayout { Spacing = 6 };
        box.Children.Add(Avatars.View(id, "", 76));
        box.Children.Add(Ui.Text(caption, 11, Pal.Ink, current, TextAlignment.Center));
        box.Children.Add(PriceTag(owned, current, price, "НАДЕТА"));

        var card = Ui.Card(box, current ? Pal.Amber : Pal.Paper, 8);
        SemanticProperties.SetDescription(card, caption + ", " +
            (current ? "надета" : owned ? "куплена" : $"цена {price} {Crystals.Word(price)}"));
        Ui.OnTap(card, async () =>
        {
            if (current) return;
            if (!owned && !await Purchase(this, $"аватарку «{caption}»", id, price, "Она сразу станет твоей.")) return;
            AppState.P.Avatar = id;
            AppState.Save();
            Sound.Play("ok");
            Build();
        });
        return card;
    }

    // спросить и списать; false — передумал или не хватает кристаллов
    internal static async Task<bool> Purchase(Page page, string what, string id, int price, string after)
    {
        int lack = price - Crystals.Balance;
        if (lack > 0)
        {
            Sound.Play("bad");
            await Dlg.Alert(page, "НЕ ХВАТАЕТ", $"Нужно ещё {lack} {Crystals.Word(lack)}. Проходи новые уроки и держи серию — кристаллы набегут.");
            return false;
        }
        if (!await Dlg.Confirm(page, "ПОКУПКА", $"Купить {what} за {price} {Crystals.Word(price)}? {after}", "КУПИТЬ", "ОТМЕНА"))
            return false;
        Crystals.Buy(id, price);
        Sound.Play("win");
        return true;
    }
}
