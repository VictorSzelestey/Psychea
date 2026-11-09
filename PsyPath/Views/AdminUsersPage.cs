using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

public class AdminUsersPage : AdminBase
{
    public AdminUsersPage()
    {
        Show("ПОЛЬЗОВАТЕЛИ", Message("ЗАГРУЗКА…"));
        _ = Load();
    }

    async Task Load()
    {
        var r = await Api.AdminUsers();
        if (!r.Ok)
        {
            Show("ПОЛЬЗОВАТЕЛИ", Message(r.Error, Pal.RedInk));
            return;
        }

        var stack = List();
        int count = 0;
        foreach (var u in r.Body.EnumerateArray())
        {
            count++;
            string role = u.GetProperty("role").GetString() ?? "";
            var lastActive = u.GetProperty("lastActive");
            var box = new VerticalStackLayout { Spacing = 3 };
            box.Children.Add(Ui.Text(
                $"{u.GetProperty("displayName").GetString()}  @{u.GetProperty("login").GetString()}" + (role == "admin" ? "  //  АДМИН" : ""),
                14, Pal.Ink, true));
            box.Children.Add(Ui.Text(
                $"РЕЙТИНГ {u.GetProperty("rating").GetDouble():0}  //  XP {u.GetProperty("totalXp").GetInt32()}  //  СЕРИЯ {u.GetProperty("streak").GetInt32()}",
                12, Pal.Ink2, true));
            box.Children.Add(Ui.Text(
                $"Уроков: {u.GetProperty("lessonsFinished").GetInt64()}, точность {u.GetProperty("accuracy").GetInt32()}%, " +
                $"активность: {(lastActive.ValueKind == System.Text.Json.JsonValueKind.String ? lastActive.GetString() : "—")}",
                11));
            var card = Ui.Card(box, role == "admin" ? Pal.Paper2 : Pal.Paper, 12);
            long id = u.GetProperty("id").GetInt64();
            Ui.OnTap(card, async () => await Push(new AdminUserPage(id)));
            stack.Children.Add(card);
        }
        stack.Children.Insert(0, Ui.Text($"ВСЕГО: {count}. Нажми на пользователя, чтобы открыть его опросники и уроки.", 12, Pal.Ink2, true));
        Show("ПОЛЬЗОВАТЕЛИ", stack, animate: true);
    }
}
