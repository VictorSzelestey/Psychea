using Foundation;
using UserNotifications;

namespace PsyPath.Ios;

// напоминание о серии: одно локальное уведомление, новое заменяет старое по Id
public static class Reminder
{
    const string Id = "streak";

    public static void Schedule(DateTime at, string title, string text)
    {
        var content = new UNMutableNotificationContent
        {
            Title = title,
            Body = text,
            Sound = UNNotificationSound.Default
        };
        var date = new NSDateComponents { Year = at.Year, Month = at.Month, Day = at.Day, Hour = at.Hour, Minute = at.Minute };
        var trigger = UNCalendarNotificationTrigger.CreateTrigger(date, false);
        var request = UNNotificationRequest.FromIdentifier(Id, content, trigger);
        UNUserNotificationCenter.Current.AddNotificationRequest(request, _ => { });
    }

    public static void Cancel() =>
        UNUserNotificationCenter.Current.RemovePendingNotificationRequests(new[] { Id });

    public static async Task<bool> RequestPermission()
    {
        var result = await UNUserNotificationCenter.Current.RequestAuthorizationAsync(
            UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound);
        return result.Item1;
    }
}
