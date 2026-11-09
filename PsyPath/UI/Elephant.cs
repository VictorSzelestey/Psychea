using Microsoft.Maui.Graphics.Platform;
using IImage = Microsoft.Maui.Graphics.IImage;

namespace PsyPath.UI;

// у DrawImage нет исходного прямоугольника: лист сдвигается так, чтобы нужная ячейка попала в окно вида
class SpriteDrawable : IDrawable
{
    public IImage? Sheet { get; set; }
    public int Frame { get; set; }

    public void Draw(ICanvas canvas, RectF rect)
    {
        if (Sheet == null) return;
        canvas.SaveState();
        canvas.ClipRectangle(rect);
        canvas.DrawImage(Sheet,
            rect.X - Frame % Elephant.Columns * rect.Width,
            rect.Y - Frame / Elephant.Columns * rect.Height,
            rect.Width * Elephant.Columns,
            rect.Height * Elephant.Rows);
        canvas.RestoreState();
    }
}

// слон — значок темперамента (пока один на все типы): между проходами сидит спокойно
public class Elephant : GraphicsView
{
    public const int Columns = 5;
    public const int Rows = 5;
    const int FrameCount = Columns * Rows;
    const double Fps = 16;
    const int RestTicks = 64; // 4 с при 16 кадрах/с

    // анимации и их шансы в процентах; выбор заново перед каждым проходом
    static readonly (string File, int Chance)[] Sheets =
    {
        ("elephant_blink.png", 30),
        ("elephant_combined.png", 40),
        ("elephant_tail.png", 30)
    };

    // листы одни на всё приложение: экран профиля пересобирается при каждом изменении данных
    static readonly Dictionary<string, Task<IImage>> Cache = new();
    static readonly Random Rnd = new();

    readonly SpriteDrawable drawable = new();
    IDispatcherTimer? timer;
    bool active;
    bool choosing;
    int tick;

    public Elephant(double height)
    {
        Drawable = drawable;
        BackgroundColor = Colors.Transparent;
        HeightRequest = height;
        WidthRequest = height;
        HorizontalOptions = LayoutOptions.Center;
        // рядом всегда есть подпись с темпераментом
        AutomationProperties.SetIsInAccessibleTree(this, false);
    }

    static string Pick()
    {
        int roll = Rnd.Next(Sheets.Sum(s => s.Chance));
        foreach (var (file, chance) in Sheets)
        {
            if (roll < chance) return file;
            roll -= chance;
        }
        return Sheets[0].File;
    }

    static Task<IImage> Load(string file)
    {
        if (!Cache.TryGetValue(file, out var image))
            Cache[file] = image = Task.Run(async () =>
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(file);
                return PlatformImage.FromStream(stream);
            });
        return image;
    }

    // кадр 0 у всех листов одинаковый: смена листа в паузе незаметна
    async Task ChooseSheet()
    {
        choosing = true;
        drawable.Sheet = await Load(Pick());
        drawable.Frame = 0;
        choosing = false;
        Invalidate();
    }

    public async void Start()
    {
        if (active) return;
        active = true;
        await ChooseSheet();
        // пока лист грузился, экран могли закрыть; при отключённых анимациях остаётся первый кадр
        if (!active || timer != null || Anim.Reduced) return;

        tick = 0;
        timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1 / Fps);
        timer.Tick += (s, e) => Advance();
        timer.Start();
    }

    public void Stop()
    {
        active = false;
        timer?.Stop();
        timer = null;
        // кадр 0 — нейтральная поза: при возврате на экран слон не застынет с закрытыми глазами
        drawable.Frame = 0;
    }

    void Advance()
    {
        if (choosing) return;
        tick++;
        if (tick < FrameCount)
        {
            drawable.Frame = tick;
            Invalidate();
        }
        else if (tick >= FrameCount + RestTicks)
        {
            tick = 0;
            _ = ChooseSheet();
        }
    }
}
