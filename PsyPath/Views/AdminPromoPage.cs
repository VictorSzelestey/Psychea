using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

// промокоды: админ задаёт название кода, награду (кристаллы или предмет магазина) и сколько раз его можно ввести.
// Коды хранятся на сервере, каждый пользователь вводит код один раз
public class AdminPromoPage : AdminBase
{
    bool loading;
    // черновик нового кода переживает перезагрузку списка
    string code = "";
    string reward = "crystals";
    int amount = 100;
    string item = "";
    int maxUses;
    bool amountOk = true;
    bool maxOk = true;

    public AdminPromoPage()
    {
        Show("ПРОМОКОДЫ", Message("ЗАГРУЗКА…"));
    }

    protected override void Refresh() => _ = Load();

    async Task Load()
    {
        if (loading) return;
        if (Api.User?.IsAdmin != true)
        {
            Show("ПРОМОКОДЫ", Message("Промокоды создаёт только админ.", Pal.RedInk));
            return;
        }
        loading = true;
        var r = await Api.AdminPromos();
        loading = false;
        if (!r.Ok)
        {
            Show("ПРОМОКОДЫ", Message(r.Error, Pal.RedInk));
            return;
        }

        var stack = List();
        stack.Children.Add(NewCodeCard());
        stack.Children.Add(Form.Section("ВСЕ ПРОМОКОДЫ"));
        int count = 0;
        foreach (var p in r.Body.EnumerateArray())
        {
            count++;
            string c = p.GetProperty("code").GetString() ?? "";
            var maxUsesValue = p.GetProperty("maxUses");
            string limit = maxUsesValue.ValueKind == System.Text.Json.JsonValueKind.Number ? $" из {maxUsesValue.GetInt32()}" : "";

            var box = new VerticalStackLayout { Spacing = 2 };
            box.Children.Add(Ui.Text(c, 15, Pal.Ink, true));
            box.Children.Add(Ui.Text(Describe(p.GetProperty("reward").GetString() ?? "", p.GetProperty("amount").GetInt32(), p.GetProperty("item").GetString() ?? ""), 12, Pal.Ink));
            box.Children.Add(Ui.Text($"Активаций: {p.GetProperty("uses").GetInt32()}{limit}", 11, Pal.Ink2, true));

            var row = new Grid
            {
                ColumnSpacing = 8,
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
            };
            row.Children.Add(box);
            var del = Form.Link("✕", Pal.RedInk, async () =>
            {
                if (!await Dlg.Confirm(this, "УДАЛИТЬ?", $"Промокод {c} перестанет работать. Уже выданные награды останутся у пользователей.", "УДАЛИТЬ", "ОТМЕНА")) return;
                var d = await Api.DeletePromo(c);
                if (!d.Ok) await Dlg.Alert(this, "НЕ ВЫШЛО", d.Error);
                await Load();
            }, TextAlignment.Center);
            SemanticProperties.SetDescription(del, $"Удалить промокод {c}");
            Grid.SetColumn(del, 1);
            row.Children.Add(del);
            stack.Children.Add(Ui.Card(row, Pal.Paper, 12));
        }
        if (count == 0) stack.Children.Add(Ui.Text("Промокодов пока нет.", 12, Pal.Ink2));
        Show("ПРОМОКОДЫ", stack, animate: true);
    }

    static string Describe(string reward, int amount, string item) =>
        reward == "crystals"
            ? $"+{amount} {Crystals.Word(amount)}"
            : Crystals.ShopItems.FirstOrDefault(x => x.Id == item).Name ?? $"Предмет {item}";

    View NewCodeCard()
    {
        var box = new VerticalStackLayout { Spacing = 10 };
        box.Children.Add(Ui.Text("НОВЫЙ ПРОМОКОД", 12, Pal.RedInk, true));
        box.Children.Add(Form.Field("НАЗВАНИЕ КОДА", code, v => code = v, hint: "3–32 символа: буквы, цифры, - и _. Регистр не важен."));

        var details = new VerticalStackLayout { Spacing = 10 };
        void Details()
        {
            details.Children.Clear();
            if (reward == "crystals")
                details.Children.Add(Form.Int("СКОЛЬКО КРИСТАЛЛОВ", amount, v => amount = v, ok => amountOk = ok));
            else
            {
                if (Crystals.ShopItems.All(x => x.Id != item)) item = Crystals.ShopItems[0].Id;
                details.Children.Add(Form.Choice("ПРЕДМЕТ", Crystals.ShopItems.Select(x => (x.Id, x.Name)).ToList(), item, v => item = v));
                details.Children.Add(Form.Hint("Если предмет уже куплен, пользователь получит кристаллы по его цене."));
            }
        }
        box.Children.Add(Form.Choice("НАГРАДА", new[] { ("crystals", "Кристаллы"), ("item", "Предмет магазина") }, reward, v =>
        {
            reward = v;
            Details();
        }));
        Details();
        box.Children.Add(details);
        box.Children.Add(Form.Int("СКОЛЬКО РАЗ МОЖНО ВВЕСТИ", maxUses, v => maxUses = v, ok => maxOk = ok,
            "0 — без ограничения. Один пользователь вводит код только один раз."));

        var create = new Btn("СОЗДАТЬ ПРОМОКОД", Pal.Violet, Pal.OnAccent, 15);
        create.Clicked += async (s, e) =>
        {
            if (!amountOk || !maxOk)
            {
                await Dlg.Alert(this, "ПРОВЕРЬ ПОЛЯ", "Числа должны быть целыми от 0.");
                return;
            }
            create.Enabled = false;
            var r = await Api.CreatePromo(code.Trim(), reward, amount, reward == "item" ? item : "", maxUses > 0 ? maxUses : null);
            create.Enabled = true;
            if (!r.Ok)
            {
                await Dlg.Alert(this, "НЕ ВЫШЛО", r.Error);
                return;
            }
            Sound.Play("ok");
            await Dlg.Alert(this, "ГОТОВО", $"Промокод {r.Body.GetProperty("code").GetString()} создан: {Describe(reward, amount, item)}.");
            code = "";
            await Load();
        };
        box.Children.Add(create);
        return Ui.Card(box, Pal.Paper, 12);
    }
}
