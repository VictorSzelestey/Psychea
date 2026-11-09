namespace PsyPath.Services;

// настройки этого телефона: в аккаунт не уходят и при выходе не сбрасываются
public static class Settings
{
    public static bool Sound
    {
        get => Preferences.Default.Get("set_sound", true);
        set => Preferences.Default.Set("set_sound", value);
    }

    // множитель размера всего текста: 1, 1.15 или 1.3
    public static double FontScale
    {
        get => Preferences.Default.Get("set_font", 1.0);
        set => Preferences.Default.Set("set_font", value);
    }

    // анимации выключены в приложении; системное «Отключить анимацию» учитывается отдельно
    public static bool ReduceMotion
    {
        get => Preferences.Default.Get("set_still", false);
        set => Preferences.Default.Set("set_still", value);
    }

    // раз в сутки при запуске проверять, вышла ли новая версия
    public static bool AutoUpdate
    {
        get => Preferences.Default.Get("set_autoupdate", true);
        set => Preferences.Default.Set("set_autoupdate", value);
    }

    public static bool Reminders
    {
        get => Preferences.Default.Get("set_remind", true);
        set => Preferences.Default.Set("set_remind", value);
    }

    // час напоминания; к полуночи серия сгорает, так что 21 — «за 3 часа»
    public static int ReminderHour
    {
        get => Preferences.Default.Get("set_remind_h", 21);
        set => Preferences.Default.Set("set_remind_h", value);
    }
}
