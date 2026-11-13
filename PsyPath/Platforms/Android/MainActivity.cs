using Android.App;
using Android.Content.PM;
using Android.Graphics.Drawables;
using Android.OS;

namespace PsyPath;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    // поворот по датчику, но с учётом блокировки поворота в шторке
    ScreenOrientation = ScreenOrientation.FullUser,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // фон окна под страницами — цвет заставки (Pal.Bg): при смене корневой страницы
        // и в тёмной теме телефона между экранами больше не мелькает чёрный
        Window?.SetBackgroundDrawable(new ColorDrawable(Android.Graphics.Color.ParseColor("#E9DFF4")));
    }
}
