using Android.App;
using Android.Content;

namespace PsyPath.Droid;

// напоминание о серии: будильник Android будит ReminderReceiver, тот показывает уведомление.
// Сервер не нужен; после перезагрузки телефона будильник ставится заново при запуске приложения
public static class Reminder
{
    const int RequestCode = 4201;
    public const string Channel = "streak";

    static PendingIntent Pending(Context ctx, string? title, string? text)
    {
        var intent = new Intent(ctx, typeof(ReminderReceiver));
        if (title != null)
        {
            intent.PutExtra("title", title);
            intent.PutExtra("text", text);
        }
        return PendingIntent.GetBroadcast(ctx, RequestCode, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    // Android.App.Application полным именем: в MAUI есть свой Application
    public static void Schedule(DateTime at, string title, string text)
    {
        var ctx = Android.App.Application.Context;
        if (ctx.GetSystemService(Context.AlarmService) is not AlarmManager alarms) return;
        long ms = new DateTimeOffset(at).ToUnixTimeMilliseconds();
        // неточный будильник: не нужно разрешение на точные будильники, срабатывает и в режиме сна
        alarms.SetAndAllowWhileIdle(AlarmType.RtcWakeup, ms, Pending(ctx, title, text));
    }

    public static void Cancel()
    {
        var ctx = Android.App.Application.Context;
        if (ctx.GetSystemService(Context.AlarmService) is AlarmManager alarms)
            alarms.Cancel(Pending(ctx, null, null));
    }
}

[BroadcastReceiver(Enabled = true, Exported = false)]
public class ReminderReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;
        if (context.GetSystemService(Context.NotificationService) is not NotificationManager nm) return;

        string title = intent.GetStringExtra("title") ?? "ПСИХЕЯ";
        string text = intent.GetStringExtra("text") ?? "";

        // каналы уведомлений появились в Android 8 (API 26)
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            nm.CreateNotificationChannel(new NotificationChannel(Reminder.Channel, "Серия дней", NotificationImportance.Default)
            {
                Description = "Напоминание, что серия скоро сгорит"
            });

        // нажатие открывает приложение
        var open = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
        var tap = open == null ? null : PendingIntent.GetActivity(context, 0, open, PendingIntentFlags.Immutable);

        // одноцветный значок уведомления; если ресурса нет — значок приложения
        int icon = context.Resources?.GetIdentifier("ic_notify", "drawable", context.PackageName) ?? 0;
        if (icon == 0) icon = context.ApplicationInfo?.Icon ?? Android.Resource.Drawable.IcDialogInfo;

        Notification.Builder builder;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            builder = new Notification.Builder(context, Reminder.Channel);
        else
            builder = new Notification.Builder(context);
        builder.SetSmallIcon(icon)
            .SetContentTitle(title)
            .SetContentText(text)
            .SetStyle(new Notification.BigTextStyle().BigText(text))
            .SetAutoCancel(true);
        if (tap != null) builder.SetContentIntent(tap);
        nm.Notify(1, builder.Build());
    }
}
