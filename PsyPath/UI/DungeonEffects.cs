using SkiaSharp;
using PsyPath.Models;

namespace PsyPath.UI;

internal static class DungeonEffects
{
    static readonly SKColor Gold = new(244, 200, 111);
    static readonly SKColor Heart = new(252, 95, 107);
    static readonly SKColor Overheal = new(95, 245, 223);
    static readonly SKColor EmptyHeart = new(46, 40, 34);
    static readonly SKColor ClockTrack = new(38, 46, 38);
    public const float ClockLength = 144 * 1.30f;

    public static void DrawHearts(SKCanvas canvas, SKImageInfo info, int halfHearts, bool overheal)
    {
        const float unit = 24, spacing = 31;
        int count = halfHearts > 8 ? 5 : 4;
        float left = (info.Width - unit - (count - 1) * spacing) / 2;
        float top = (info.Height - unit) / 2;
        using var paint = new SKPaint { IsAntialias = true, StrokeWidth = 1.4f };
        using var heart = new SKPath();
        heart.MoveTo(12, 22);
        heart.CubicTo(7, 18, 1, 14, 1, 8);
        heart.CubicTo(1, 2, 8, 0, 12, 5);
        heart.CubicTo(16, 0, 23, 2, 23, 8);
        heart.CubicTo(23, 14, 17, 18, 12, 22);
        heart.Close();
        for (int i = 0; i < count; i++)
        {
            canvas.Save();
            canvas.Translate(left + i * spacing, top);
            paint.Style = SKPaintStyle.Fill;
            paint.Color = EmptyHeart;
            canvas.DrawPath(heart, paint);
            int fill = Math.Clamp(halfHearts - i * 2, 0, 2);
            if (fill != 0)
            {
                canvas.Save();
                canvas.ClipRect(new SKRect(0, 0, unit * fill / 2, unit));
                paint.Color = overheal ? Overheal : Heart;
                canvas.DrawPath(heart, paint);
                canvas.Restore();
            }
            paint.Style = SKPaintStyle.Stroke;
            paint.Color = overheal ? new SKColor(204, 255, 242) : Gold;
            canvas.DrawPath(heart, paint);
            canvas.Restore();
        }
    }

    public static void DrawClock(SKCanvas canvas, SKImageInfo info, double fraction, double elapsed,
        double shakePhase, bool reduced)
    {
        const float ThicknessScale = 1.15f;
        float length = Math.Min(info.Width - 36, ClockLength);
        if (length <= 0) return;
        using var paint = new SKPaint { IsAntialias = true, StrokeCap = SKStrokeCap.Round };
        canvas.Translate(info.Width / 2f, info.Height / 2f);
        if (!reduced)
        {
            double urgency = 1 - fraction;
            float amplitude = (float)(1.3 + 4.4 * urgency * urgency);
            canvas.Translate((float)Math.Sin(shakePhase) * amplitude, (float)Math.Cos(shakePhase) * amplitude);
        }
        canvas.RotateDegrees(20);
        float start = -length / 2, end = length / 2;
        float limiter = end - (float)fraction * length;
        paint.StrokeWidth = 12 * ThicknessScale;
        paint.Color = Gold.WithAlpha(165);
        canvas.DrawLine(start, 0, end, 0, paint);
        paint.StrokeWidth = 9 * ThicknessScale;
        paint.Color = ClockTrack;
        canvas.DrawLine(start, 0, end, 0, paint);
        if (fraction > 0)
        {
            paint.Color = ClockColor(fraction);
            canvas.DrawLine(limiter, 0, end, 0, paint);
        }
        byte alpha = fraction <= 0.2 && !reduced
            ? (byte)(155 + 100 * (1 + Math.Sin(elapsed * 4 * Math.PI)) / 2) : (byte)255;
        paint.Color = SKColors.White.WithAlpha(alpha);
        canvas.DrawOval(limiter, 0, 3.5f * ThicknessScale, 8 * ThicknessScale, paint);
    }

    static SKColor ClockColor(double fraction)
    {
        if (fraction > 0.65) return Mix(new SKColor(250, 222, 87), new SKColor(80, 220, 131), (fraction - 0.65) / 0.35);
        if (fraction > 0.32) return Mix(new SKColor(255, 151, 72), new SKColor(250, 222, 87), (fraction - 0.32) / 0.33);
        return Mix(new SKColor(249, 78, 89), new SKColor(255, 151, 72), fraction / 0.32);
    }

    static SKColor Mix(SKColor from, SKColor to, double value) => new(
        (byte)(from.Red + (to.Red - from.Red) * value),
        (byte)(from.Green + (to.Green - from.Green) * value),
        (byte)(from.Blue + (to.Blue - from.Blue) * value));

    public static void DrawFlashes(SKCanvas canvas, SKImageInfo info, IReadOnlyList<DungeonFlash> flashes, double now, bool reduced)
    {
        float width = info.Width, height = info.Height;
        float edge = Math.Min(width, height) * 0.18f;
        using var paint = new SKPaint();
        foreach (var flash in flashes)
        {
            double age = Math.Clamp(now - flash.At, 0, 1);
            double shimmer = reduced ? 0.6 : 0.72 + 0.28 * Math.Cos(age * Math.PI * 4);
            var tint = flash.Trap switch
            {
                DungeonTrap.Cipher => new SKColor(177, 86, 255),
                DungeonTrap.FalseAnswer => new SKColor(255, 66, 79),
                _ => SKColors.White
            };
            var color = tint.WithAlpha((byte)((reduced ? 115 : 205) * (1 - age * age) * shimmer));
            DrawEdge(canvas, paint, new SKRect(0, 0, edge, height), new SKPoint(0, 0), new SKPoint(edge, 0), color);
            DrawEdge(canvas, paint, new SKRect(width - edge, 0, width, height), new SKPoint(width, 0), new SKPoint(width - edge, 0), color);
            DrawEdge(canvas, paint, new SKRect(0, 0, width, edge), new SKPoint(0, 0), new SKPoint(0, edge), color);
            DrawEdge(canvas, paint, new SKRect(0, height - edge, width, height), new SKPoint(0, height), new SKPoint(0, height - edge), color);
        }
    }

    static void DrawEdge(SKCanvas canvas, SKPaint paint, SKRect bounds, SKPoint start, SKPoint end, SKColor tint)
    {
        using var gradient = SKShader.CreateLinearGradient(start, end, [tint, tint.WithAlpha(0)], null, SKShaderTileMode.Clamp);
        paint.Shader = gradient;
        canvas.DrawRect(bounds, paint);
        paint.Shader = null;
    }
}

internal sealed class DungeonBricks
{
    const double Lifespan = 0.53;
    readonly Brick[] bricks;
    readonly double at;
    readonly record struct Brick(float X, float Y, float VelocityX, float VelocityY, float Spin, SKColor Color);

    public DungeonBricks(Rect bounds, double at, bool correct)
    {
        this.at = at;
        var color = correct ? new SKColor(226, 181, 91) : new SKColor(238, 115, 112);
        bricks = Enumerable.Range(0, 24).Select(i =>
        {
            var random = Random.Shared;
            float x = (float)bounds.X + (i % 6 + 0.5f) * (float)bounds.Width / 6;
            float y = (float)bounds.Y + (i / 6 + 0.5f) * (float)bounds.Height / 4;
            return new Brick(x, y, random.Next(-150, 151), random.Next(-220, -25), random.Next(-520, 521),
                color.WithAlpha((byte)random.Next(150, 256)));
        }).ToArray();
    }

    public bool IsAlive(double now) => now - at < Lifespan;

    public void Draw(SKCanvas canvas, double now)
    {
        float age = (float)(now - at);
        float opacity = (float)Math.Clamp(1 - age / Lifespan, 0, 1);
        using var paint = new SKPaint { IsAntialias = true };
        foreach (var brick in bricks)
        {
            canvas.Save();
            canvas.Translate(brick.X + age * brick.VelocityX, brick.Y + age * brick.VelocityY + 455 * age * age);
            canvas.RotateDegrees(age * brick.Spin);
            paint.Color = brick.Color.WithAlpha((byte)(brick.Color.Alpha * opacity));
            canvas.DrawRoundRect(new SKRect(-6, -3.5f, 6, 3.5f), 1, 1, paint);
            canvas.Restore();
        }
    }
}
