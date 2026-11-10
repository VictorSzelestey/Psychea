using PsyPath.Services;
using PsyPath.UI;

namespace PsyPath.Views;

// настройки телефона: звук, размер текста, анимации, напоминание о серии, обновления, сброс и удаление
public class SettingsPage : AdminBase
{
    readonly double fontAtOpen = Ui.Scale;
    readonly Label updateStatus = Ui.Text("", 12, Pal.Ink2, true, TextAlignment.Center);
    // обновление, которое пользователь уже согласился поставить при запуске приложения
    AppUpdate? pending;
    bool updating;

    public SettingsPage(AppUpdate? pending = null)
    {
        this.pending = pending;
        Build();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (pending == null) return;
        var update = pending;
        pending = null;
        _ = Install(update);
    }

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

        s.Children.Add(Form.Section("ДАННЫЕ"));
        s.Children.Add(Form.Link("СБРОСИТЬ ПРОГРЕСС", Pal.RedInk, async () =>
        {
            bool yes = await Dlg.Confirm(this, "СБРОС", "Весь прогресс будет удалён. Продолжить?", "СБРОСИТЬ", "ОТМЕНА");
            if (!yes) return;
            AppState.ResetAll();
            await SwitchRoot(Start.Home());
        }, TextAlignment.Center));
        if (Api.User != null)
            s.Children.Add(Form.Link("УДАЛИТЬ АККАУНТ", Pal.RedInk, async () => await Push(new DeleteAccountPage()), TextAlignment.Center));

        s.Children.Add(Form.Section("ДИАГНОСТИКА"));
        s.Children.Add(Form.Hint("После сбоя снова открой приложение и отправь журнал разработчику. В нём — ошибки и последние открытые экраны."));
        var logs = new Btn("ОТПРАВИТЬ ЖУРНАЛ", Pal.Paper2, Pal.Ink, 15);
        logs.Clicked += async (_, _) =>
        {
            if (!logs.Enabled) return;
            logs.Enabled = false;
            try
            {
                string? file = await Task.Run(() => CrashLog.Export(FileSystem.CacheDirectory));
                if (file == null)
                    await Dlg.Alert(this, "ЖУРНАЛ", "Не удалось прочитать журнал. Проверь свободное место на телефоне.");
                else
                    await Share.Default.RequestAsync(new ShareFileRequest("Журнал Психеи", new ShareFile(file, "text/plain")));
            }
            catch (Exception ex)
            {
                CrashLog.Write("Share diagnostics", ex);
                await Dlg.Alert(this, "ЖУРНАЛ", "Не удалось открыть отправку журнала. Попробуй ещё раз.");
            }
            finally { logs.Enabled = true; }
        };
        s.Children.Add(logs);

        s.Children.Add(Form.Section("ОБНОВЛЕНИЯ"));
        s.Children.Add(Form.Choice("АВТООБНОВЛЕНИЕ", OnOff, Settings.AutoUpdate ? "1" : "0", v => Settings.AutoUpdate = v == "1"));
        s.Children.Add(Form.Hint("Раз в сутки при запуске приложение проверяет новую версию и предлагает её установить."));

        var check = new Btn("ПРОВЕРИТЬ ОБНОВЛЕНИЯ", Pal.Violet, Pal.Paper, 15);
        check.Clicked += async (o, e) => await CheckUpdates();
        s.Children.Add(check);
        // подпись одна на все пересборки экрана: на неё пишет идущая проверка
        (updateStatus.Parent as Layout)?.Children.Remove(updateStatus);
        s.Children.Add(updateStatus);
        s.Children.Add(Ui.VersionLabel());

        Show("НАСТРОЙКИ", s);
    }

    static readonly (string, string)[] OnOff = { ("1", "ВКЛ"), ("0", "ВЫКЛ") };

    void ShowUpdate(string text, Color color)
    {
        updateStatus.Text = text;
        updateStatus.TextColor = color;
    }

    async Task CheckUpdates()
    {
        if (updating) return;
        updating = true;
        ShowUpdate("Проверяю…", Pal.Ink2);
        var check = await Updater.Check();
        updating = false;

        if (!check.Ok)
        {
            ShowUpdate(check.Error, Pal.RedInk);
            return;
        }
        if (check.Update == null)
        {
            ShowUpdate("Установлена последняя версия", Pal.Ink2);
            return;
        }

        ShowUpdate($"Доступна версия {check.Update.Version}", Pal.Ink2);
        bool yes = await Dlg.Confirm(this, "ОБНОВЛЕНИЕ", Updater.Prompt(check.Update), "ОБНОВИТЬ", "ПОЗЖЕ");
        if (yes) await Install(check.Update);
    }

    async Task Install(AppUpdate update)
    {
        if (updating) return;
        updating = true;
        ShowUpdate("Скачиваю…", Pal.Ink2);
        string? error = await Updater.DownloadAndInstall(update, percent =>
            MainThread.BeginInvokeOnMainThread(() => ShowUpdate($"Скачиваю… {percent}%", Pal.Ink2)));
        updating = false;
        // процент мог прийти из очереди уже после завершения, поэтому итог пишем в следующем проходе
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (error != null) ShowUpdate(error, Pal.RedInk);
            else ShowUpdate("Подтверди установку в окне Android", Pal.Ink2);
        });
    }

    // новый размер текста нужен всем экранам — пересобираем вкладки и возвращаемся в профиль
    protected override async Task Leave()
    {
        if (Ui.Scale != fontAtOpen)
            await SwitchRoot(new AppShell(AppShell.ProfileTab));
        else
            await Pop();
    }
}
