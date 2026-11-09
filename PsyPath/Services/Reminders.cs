namespace PsyPath.Services;

// когда напомнить про серию: сегодня в ReminderHour, если сегодня ещё не занимался, иначе завтра
public static class Reminders
{
    public static void Update()
    {
#if ANDROID
        try
        {
            if (!Settings.Reminders || !AppState.P.Onboarded)
            {
                Droid.Reminder.Cancel();
                return;
            }

            var today = DateTime.Today;
            bool activeToday = AppState.P.LastActive == today.ToString("yyyy-MM-dd");
            var at = today.AddHours(Settings.ReminderHour);
            if (activeToday || DateTime.Now >= at) at = at.AddDays(1);

            int streak = AppState.DisplayStreak;
            int left = 24 - Settings.ReminderHour;
            string text = streak > 0
                ? $"Серия {streak} дн. сгорит через {left} ч. Один урок — и она продолжится."
                : "Пять минут на урок? Манул ждёт у магнитофона.";
            Droid.Reminder.Schedule(at, "ПСИХЕЯ", text);
        }
        catch (Exception)
        {
            // без напоминания приложение работает как обычно
        }
#endif
    }

    // Android 13+ спрашивает разрешение на уведомления; на старых версиях оно есть всегда
    public static async Task<bool> RequestPermission()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            return status == PermissionStatus.Granted;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
