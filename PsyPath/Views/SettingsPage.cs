using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

// настройки телефона: звук, размер текста, анимации, напоминание о серии
public class SettingsPage : AdminBase
{
    readonly double fontAtOpen = Ui.Scale;

    public SettingsPage() => Build();

    void Build()
    {
        var s = List();

        s.Children.Add(Form.Choice("ЗВУКИ", OnOff, Settings.Sound ? "1" : "0", v =>
        {
            Settings.Sound = v == "1";
            Sound.Play("ok");
        }));
        s.Children.Add(Form.Hint("Щелчок кнопок, сигнал верного и неверного ответа, мелодия в конце урока."));

        s.Children.Add(Form.Choice("РАЗМЕР ТЕКСТА", new[] { ("1", "ОБЫЧНЫЙ"), ("1.15", "КРУПНЕЕ"), ("1.3", "КРУПНЫЙ") },
            Settings.FontScale.ToString(System.Globalization.CultureInfo.InvariantCulture), v =>
            {
                double scale = double.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
                Settings.FontScale = scale;
                Ui.Scale = scale;
                Build();
            }));
        s.Children.Add(Ui.Text("Так выглядит текст задания.", 15));

        s.Children.Add(Form.Choice("АНИМАЦИИ", OnOff, Anim.Still ? "0" : "1", v =>
        {
            Anim.Still = v == "0";
            Settings.ReduceMotion = Anim.Still;
        }));
        s.Children.Add(Form.Hint("Без анимаций экраны сменяются сразу, ничего не прыгает и не качается."));

        s.Children.Add(Form.Section("НАПОМИНАНИЕ О СЕРИИ"));
        s.Children.Add(Form.Choice("УВЕДОМЛЕНИЕ", OnOff, Settings.Reminders ? "1" : "0", async v =>
        {
            bool on = v == "1";
            if (on && !await Reminders.RequestPermission())
            {
                await Dlg.Alert(this, "НЕТ РАЗРЕШЕНИЯ", "Разреши уведомления для Психеи в настройках телефона, иначе напоминание не придёт.");
                on = false;
            }
            Settings.Reminders = on;
            Reminders.Update();
            Build();
        }));
        s.Children.Add(Form.Choice("ВО СКОЛЬКО", new[] { ("19", "19:00"), ("20", "20:00"), ("21", "21:00"), ("22", "22:00") },
            Settings.ReminderHour.ToString(), v =>
            {
                Settings.ReminderHour = int.Parse(v);
                Reminders.Update();
            }));
        s.Children.Add(Form.Hint("Приходит, только если в этот день ты ещё не занимался. Серия сгорает в полночь."));

        Show("НАСТРОЙКИ", s);
    }

    static readonly (string, string)[] OnOff = { ("1", "ВКЛ"), ("0", "ВЫКЛ") };

    // новый размер текста нужен всем экранам — пересобираем вкладки и возвращаемся в профиль
    protected override async Task Leave()
    {
        if (Ui.Scale != fontAtOpen)
            await SwitchRoot(new AppShell(AppShell.ProfileTab));
        else
            await Pop();
    }
}
