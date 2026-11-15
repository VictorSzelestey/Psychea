using Microsoft.Maui.Controls.Shapes;
using PsyPath.Services;

namespace PsyPath.UI;

// рамка аватарки: один цвет — сплошная обводка, несколько — переход по кругу
public record AvatarFrame(string Id, string Name, int Price, Color[] Colors);

// наряд манула: та же картинка snowcat.png с шапкой, очками или шарфом, 900×811
public record Outfit(string Id, string Name, int Price, string Image);

// рамки и наряды из магазина. Покупки — в ProgressData.Owned, надетое — Frame и Outfit
public static class Cosmetics
{
    public static readonly AvatarFrame[] Frames =
    {
        new("frame_bronze", "Бронза", 100, new[] { Color.FromArgb("#B8733A") }),
        new("frame_silver", "Серебро", 150, new[] { Color.FromArgb("#8E9AA8"), Color.FromArgb("#DDE3EA") }),
        new("frame_gold", "Золото", 250, new[] { Color.FromArgb("#D99A0B"), Color.FromArgb("#FFE58A") }),
        new("frame_fire", "Огонь", 400, new[] { Color.FromArgb("#E0517E"), Color.FromArgb("#F08A24"), Color.FromArgb("#F6C453") })
    };

    public static readonly Outfit[] Outfits =
    {
        new("outfit_hat", "Шапка с помпоном", 120, "snowcat_hat.png"),
        new("outfit_glasses", "Очки отличника", 150, "snowcat_glasses.png"),
        new("outfit_scarf", "Полосатый шарф", 120, "snowcat_scarf.png")
    };

    public static AvatarFrame? FindFrame(string? id) => Frames.FirstOrDefault(f => f.Id == id);

    // манул в надетом наряде; без наряда — обычный
    public static string MascotImage =>
        Outfits.FirstOrDefault(o => o.Id == AppState.P.Outfit)?.Image ?? "snowcat.png";

    public static Brush FrameBrush(AvatarFrame f)
    {
        if (f.Colors.Length == 1) return new SolidColorBrush(f.Colors[0]);
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        for (int i = 0; i < f.Colors.Length; i++)
            brush.GradientStops.Add(new GradientStop(f.Colors[i], i / (float)(f.Colors.Length - 1)));
        return brush;
    }

    // надеть или снять: повторное нажатие на надетое снимает его
    public static void ToggleFrame(string id)
    {
        AppState.P.Frame = AppState.P.Frame == id ? "" : id;
        AppState.Save();
    }

    public static void ToggleOutfit(string id)
    {
        AppState.P.Outfit = AppState.P.Outfit == id ? "" : id;
        AppState.Save();
    }
}
