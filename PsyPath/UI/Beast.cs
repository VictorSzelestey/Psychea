using Microsoft.Maui.Graphics.Platform;
using PsyPath.Models;
using PsyPath.Services;
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
            rect.X - Frame % Beast.Columns * rect.Width,
            rect.Y - Frame / Beast.Columns * rect.Height,
            rect.Width * Beast.Columns,
            rect.Height * Beast.Rows);
        canvas.RestoreState();
    }
}

// животное темперамента: обезьяна, бык, слон или заяц. Проход в 25 кадров, между проходами сидит спокойно
public class Beast : GraphicsView
{
    public const int Columns = 5;
    public const int Rows = 5;
    const int FrameCount = Columns * Rows;
    const int RestMs = 4000;

    // анимация и её шанс в процентах; WithBlink — тот же проход с морганием, выпадает в половине случаев
    record Move(string Sheet, int Chance, string? WithBlink = null);
    record Kind(Move[] Clips, int[] FrameMs);

    static int[] Even(int ms) => Enumerable.Repeat(ms, FrameCount).ToArray();

    static readonly Dictionary<Temper, Kind> Kinds = new()
    {
        [Temper.Sanguine] = new(new Move[]
        {
            new("monkey_hop.png", 20, "monkey_hop_blink.png"),
            new("monkey_tail.png", 30, "monkey_tail_blink.png"),
            new("monkey_blink.png", 50)
        }, Even(80)),
        [Temper.Choleric] = new(new Move[]
        {
            new("bull_blink.png", 50),
            new("bull_tail.png", 30),
            new("bull_snort.png", 20)
        }, Even(80)),
        [Temper.Phlegmatic] = new(new Move[]
        {
            new("elephant_blink.png", 30),
            new("elephant_combined.png", 40),
            new("elephant_tail.png", 30)
        }, Even(62)), // 16 кадров/с
        // у зайца кадры разной длины: так задано в паке анимаций
        [Temper.Melancholic] = new(new Move[]
        {
            new("rabbit_blink.png", 50),
            new("rabbit_tap.png", 30, "rabbit_tap_blink.png"),
            new("rabbit_ear.png", 20, "rabbit_ear_blink.png")
        }, new[] { 80, 80, 80, 80, 96, 64, 80, 48, 32, 64, 16, 80, 144, 96, 80, 80, 32, 48, 48, 72, 120, 80, 80, 80, 240 })
    };

    // лист 1440×1440 в памяти — около 8 МБ, поэтому держим листы только одного животного.
    // Экран профиля пересобирается при каждом изменении данных, листы между пересборками не грузятся заново
    static readonly Dictionary<string, Task<IImage>> Cache = new();
    static Kind? cached;
    static readonly Random Rnd = new();

    readonly Kind kind;
    readonly SpriteDrawable drawable = new();
    IDispatcherTimer? timer;
    bool active;
    int run;   // номер запуска: отложенный кадр от прежнего запуска не трогает новый
    int frame;

    public Beast(Temper temper, double height)
    {
        kind = Kinds[temper];
        Drawable = drawable;
        BackgroundColor = Colors.Transparent;
        HeightRequest = height;
        WidthRequest = height;
        HorizontalOptions = LayoutOptions.Center;
        // рядом всегда есть подпись с темпераментом
        AutomationProperties.SetIsInAccessibleTree(this, false);
    }

    string Pick()
    {
        int roll = Rnd.Next(kind.Clips.Sum(c => c.Chance));
        foreach (var clip in kind.Clips)
        {
            if (roll < clip.Chance)
                return clip.WithBlink != null && Rnd.Next(2) == 0 ? clip.WithBlink : clip.Sheet;
            roll -= clip.Chance;
        }
        return kind.Clips[0].Sheet;
    }

    Task<IImage> Load(string file)
    {
        if (cached != kind)
        {
            Cache.Clear();
            cached = kind;
        }
        if (!Cache.TryGetValue(file, out var image))
            Cache[file] = image = Task.Run(async () =>
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(file);
                return PlatformImage.FromStream(stream);
            });
        return image;
    }

    // кадр 0 у всех листов одного животного одинаковый: смена листа в паузе незаметна
    async Task<bool> ChooseSheet()
    {
        string file = Pick();
        try
        {
            drawable.Sheet = await Load(file);
        }
        catch (Exception ex)
        {
            Cache.Remove(file);
            CrashLog.Write("Beast", ex);
            return false;
        }
        drawable.Frame = 0;
        Invalidate();
        return true;
    }

    public async void Start()
    {
        if (active) return;
        active = true;
        int my = ++run;
        bool ok = await ChooseSheet();
        // пока лист грузился, экран могли закрыть; при отключённых анимациях остаётся первый кадр
        if (!ok || my != run || Anim.Reduced) return;

        frame = 0;
        timer = Dispatcher.CreateTimer();
        timer.IsRepeating = false;
        timer.Tick += (s, e) => Advance(my);
        Schedule(kind.FrameMs[0]);
    }

    // кадр 0 — нейтральная поза: при возврате на экран и на картинке «Поделиться» глаза открыты
    public void Stop()
    {
        active = false;
        run++;
        timer?.Stop();
        timer = null;
        drawable.Frame = 0;
        Invalidate();
    }

    void Schedule(int ms)
    {
        if (timer == null) return;
        timer.Interval = TimeSpan.FromMilliseconds(ms);
        timer.Start();
    }

    async void Advance(int my)
    {
        if (my != run) return;
        frame++;
        if (frame < FrameCount)
        {
            drawable.Frame = frame;
            Invalidate();
            Schedule(kind.FrameMs[frame]);
        }
        else if (frame == FrameCount)
        {
            // проход закончен: спокойная поза и пауза
            drawable.Frame = 0;
            Invalidate();
            Schedule(RestMs);
        }
        else
        {
            bool ok = await ChooseSheet();
            if (my != run) return;
            // лист не загрузился — просто ещё одна пауза
            frame = ok ? 0 : FrameCount;
            Schedule(ok ? kind.FrameMs[0] : RestMs);
        }
    }
}
