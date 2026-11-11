using System.Globalization;
using SkiaSharp;

namespace PsyPath.Services;

public sealed record LessonShareResult(int Xp, int Accuracy, int RatingDelta, bool IsPractice = false)
{
    public string Heading => IsPractice ? "ПОВТОР ПРОЙДЕН" : "УРОК ПРОЙДЕН";
    public string XpText => Xp.ToString("+0;-0;+0", CultureInfo.InvariantCulture);
    public string AccuracyText => Math.Clamp(Accuracy, 0, 100).ToString(CultureInfo.InvariantCulture) + "%";
    public string RatingText => RatingDelta.ToString("+0;-0;+0", CultureInfo.InvariantCulture);
}

// Рендер не зависит от экрана, анимации счётчиков и системного окна «Поделиться».
public static class LessonShareCardRenderer
{
    public const int Width = 1200;
    public const int Height = 1360;
    public const string DownloadUrl = "https://github.com/VictorSzelestey/Psychea/releases/latest";

    static readonly SKColor Ink = SKColor.Parse("#36134F");
    static readonly SKColor Violet = SKColor.Parse("#795093");
    static readonly SKColor Paper = SKColor.Parse("#FFF9F4");

    public static byte[] Render(LessonShareResult result, Stream mascot, Stream regularFont, Stream boldFont)
    {
        ArgumentNullException.ThrowIfNull(result);
        using var hero = SKBitmap.Decode(mascot) ?? throw new InvalidDataException("Не удалось прочитать иллюстрацию карточки.");
        using var regular = SKTypeface.FromStream(regularFont) ?? throw new InvalidDataException("Не удалось прочитать шрифт карточки.");
        using var bold = SKTypeface.FromStream(boldFont) ?? throw new InvalidDataException("Не удалось прочитать шрифт карточки.");
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new InvalidOperationException("Не удалось создать изображение карточки.");
        var canvas = surface.Canvas;
        canvas.Clear(Paper);
        Background(canvas);

        Line(canvas, 76, 80, 345, 80, SKColor.Parse("#AF91C2"), 2);
        Line(canvas, 856, 80, 1124, 80, SKColor.Parse("#AF91C2"), 2);
        Logo(canvas, 403, 28, 108);
        Text(canvas, "Психея", 536, 87, 53, bold, Ink);
        Text(canvas, "Тесты. Психология. Ты.", 538, 116, 24, regular, Ink);

        Text(canvas, result.Heading, 600, 246, 108, bold, Ink, SKTextAlign.Center, 1080);
        Text(canvas, "Узнай больше о себе", 600, 301, 39, regular, Violet, SKTextAlign.Center);

        using (var blob = new SKPath())
        {
            blob.MoveTo(205, 620);
            blob.CubicTo(122, 472, 300, 462, 389, 476);
            blob.CubicTo(544, 502, 685, 305, 877, 340);
            blob.CubicTo(1060, 371, 964, 592, 1006, 681);
            blob.CubicTo(1084, 837, 803, 896, 600, 860);
            blob.CubicTo(381, 830, 231, 848, 205, 620);
            Fill(canvas, blob, SKColor.Parse("#EBDCF3"));
        }
        Sparkle(canvas, 272, 616, 21);
        Sparkle(canvas, 936, 550, 26);
        Sparkle(canvas, 848, 417, 16);
        DrawHero(canvas, hero, new SKRect(165, 330, 1035, 887));

        Stat(canvas, 56, "XP", result.XpText, regular, bold, 0);
        Stat(canvas, 428, "Точность", result.AccuracyText, regular, bold, 1);
        Stat(canvas, 800, "Рейтинг", result.RatingText, regular, bold, 2);
        Footer(canvas, regular, bold);
        Grain(canvas);

        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    static void Text(SKCanvas canvas, string text, float x, float baseline, float size,
        SKTypeface face, SKColor color, SKTextAlign align = SKTextAlign.Left, float maxWidth = float.MaxValue)
    {
        using var font = new SKFont(face, size) { Edging = SKFontEdging.Antialias };
        float measured = font.MeasureText(text);
        if (measured > maxWidth) font.Size *= maxWidth / measured;
        using var paint = new SKPaint { IsAntialias = true, Color = color };
        canvas.DrawText(text, x, baseline, align, font, paint);
    }

    static void Line(SKCanvas canvas, float x1, float y1, float x2, float y2, SKColor color, float width)
    {
        using var paint = new SKPaint { IsAntialias = true, Color = color, StrokeWidth = width, StrokeCap = SKStrokeCap.Round };
        canvas.DrawLine(x1, y1, x2, y2, paint);
    }

    static void Fill(SKCanvas canvas, SKPath path, SKColor color)
    {
        using var paint = new SKPaint { IsAntialias = true, Color = color };
        canvas.DrawPath(path, paint);
    }

    static void Background(SKCanvas canvas)
    {
        // Мягкие бумажные волны по краям, как в присланном макете.
        using var left = new SKPath();
        left.MoveTo(0, 0); left.LineTo(226, 0);
        left.CubicTo(196, 84, 127, 55, 112, 151);
        left.CubicTo(95, 240, 31, 251, 0, 265); left.Close();
        Fill(canvas, left, SKColor.Parse("#E2CFEF"));
        using var leftInner = new SKPath();
        leftInner.MoveTo(0, 0); leftInner.LineTo(153, 0);
        leftInner.CubicTo(124, 52, 47, 46, 28, 122);
        leftInner.CubicTo(19, 157, 10, 168, 0, 181); leftInner.Close();
        Fill(canvas, leftInner, SKColor.Parse("#D3B9E4"));
        using var right = new SKPath();
        right.MoveTo(1200, 246);
        right.CubicTo(1034, 327, 1080, 515, 1200, 661); right.Close();
        Fill(canvas, right, SKColor.Parse("#E3D0F0"));
        using var rightInner = new SKPath();
        rightInner.MoveTo(1200, 320);
        rightInner.CubicTo(1100, 401, 1145, 523, 1200, 567); rightInner.Close();
        Fill(canvas, rightInner, SKColor.Parse("#D6BDE9"));
        using var bottom = new SKPath();
        bottom.MoveTo(0, 1105);
        bottom.CubicTo(119, 1158, 64, 1261, 264, 1360);
        bottom.LineTo(0, 1360); bottom.Close();
        Fill(canvas, bottom, SKColor.Parse("#E6D3F1"));
        using var bottomRight = new SKPath();
        bottomRight.MoveTo(1200, 1145);
        bottomRight.CubicTo(1118, 1184, 1108, 1290, 1010, 1360);
        bottomRight.LineTo(1200, 1360); bottomRight.Close();
        Fill(canvas, bottomRight, SKColor.Parse("#E6D3F1"));
    }

    static void Sparkle(SKCanvas canvas, float x, float y, float radius)
    {
        using var path = new SKPath();
        path.MoveTo(x, y - radius);
        path.QuadTo(x + 3, y - 3, x + radius, y);
        path.QuadTo(x + 3, y + 3, x, y + radius);
        path.QuadTo(x - 3, y + 3, x - radius, y);
        path.QuadTo(x - 3, y - 3, x, y - radius);
        Fill(canvas, path, SKColors.White.WithAlpha(210));
    }

    static void Logo(SKCanvas canvas, float x, float y, float size)
    {
        using var shader = SKShader.CreateLinearGradient(new SKPoint(x, y), new SKPoint(x + size, y + size),
            [SKColor.Parse("#63388C"), SKColor.Parse("#39154F")], null, SKShaderTileMode.Clamp);
        using var paint = new SKPaint { IsAntialias = true, Shader = shader };
        canvas.DrawRoundRect(new SKRect(x, y, x + size, y + size), size * .26f, size * .26f, paint);
        // Ψ рисуется вектором: в комплектном Nunito нет греческого глифа.
        canvas.Save();
        canvas.Translate(x + size / 2, y + size / 2);
        canvas.Scale(size / 100);
        using var symbol = new SKPaint { IsAntialias = true, Color = Paper, Style = SKPaintStyle.Stroke, StrokeWidth = 7 };
        using var arms = new SKPath();
        arms.MoveTo(-29, -29);
        arms.CubicTo(-24, -25, -28, -14, -23, -2);
        arms.CubicTo(-18, 12, -11, 17, 0, 17);
        arms.CubicTo(11, 17, 18, 12, 23, -2);
        arms.CubicTo(28, -14, 24, -25, 29, -29);
        canvas.DrawPath(arms, symbol);
        canvas.DrawLine(0, -36, 0, 38, symbol);
        symbol.StrokeWidth = 4;
        canvas.DrawLine(-37, -31, -19, -31, symbol);
        canvas.DrawLine(19, -31, 37, -31, symbol);
        canvas.DrawLine(-14, -37, 14, -37, symbol);
        canvas.DrawLine(-17, 39, 17, 39, symbol);
        canvas.Restore();
    }

    static void DrawHero(SKCanvas canvas, SKBitmap bitmap, SKRect box)
    {
        float scale = Math.Min(box.Width / bitmap.Width, box.Height / bitmap.Height);
        float width = bitmap.Width * scale, height = bitmap.Height * scale;
        var target = SKRect.Create(box.MidX - width / 2, box.Bottom - height, width, height);
        using var image = SKImage.FromBitmap(bitmap);
        canvas.DrawImage(image, target, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
    }

    static void Stat(SKCanvas canvas, float left, string label, string value, SKTypeface regular, SKTypeface bold, int icon)
    {
        var rect = new SKRect(left, 904, left + 344, 1116);
        using var shader = SKShader.CreateLinearGradient(new SKPoint(left, 904), new SKPoint(left + 344, 1116),
            [SKColor.Parse("#F1E5F6"), SKColor.Parse("#EAD8F2")], null, SKShaderTileMode.Clamp);
        using var paint = new SKPaint { IsAntialias = true, Shader = shader };
        canvas.DrawRoundRect(rect, 54, 54, paint);
        using var border = new SKPaint { IsAntialias = true, Color = SKColor.Parse("#CFB1E2"), Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f };
        canvas.DrawRoundRect(rect, 54, 54, border);
        float center = left + 172;
        Icon(canvas, icon, center, 949);
        Text(canvas, label, center, 1009, 34, regular, Violet, SKTextAlign.Center);
        Text(canvas, value, center, 1088, 84, bold, Ink, SKTextAlign.Center, 300);
    }

    static void Icon(SKCanvas canvas, int icon, float x, float y)
    {
        using var paint = new SKPaint { IsAntialias = true, Color = Violet };
        if (icon == 0)
        {
            using var star = new SKPath();
            for (int i = 0; i < 10; i++)
            {
                double angle = -Math.PI / 2 + i * Math.PI / 5;
                float radius = i % 2 == 0 ? 26 : 12;
                var point = new SKPoint(x + (float)Math.Cos(angle) * radius, y + (float)Math.Sin(angle) * radius);
                if (i == 0) star.MoveTo(point); else star.LineTo(point);
            }
            star.Close(); canvas.DrawPath(star, paint);
        }
        else if (icon == 1)
        {
            for (int i = 0; i < 3; i++)
                canvas.DrawRoundRect(new SKRect(x - 26 + i * 19, y + 22 - 22 - i * 10, x - 12 + i * 19, y + 22), 3, 3, paint);
        }
        else
        {
            using var cup = new SKPath();
            cup.MoveTo(x - 21, y - 22); cup.LineTo(x + 21, y - 22);
            cup.CubicTo(x + 21, y + 5, x + 12, y + 14, x, y + 14);
            cup.CubicTo(x - 12, y + 14, x - 21, y + 5, x - 21, y - 22); cup.Close();
            canvas.DrawPath(cup, paint);
            Line(canvas, x, y + 11, x, y + 24, Violet, 8);
            Line(canvas, x - 14, y + 25, x + 14, y + 25, Violet, 6);
            using var handle = new SKPath();
            handle.MoveTo(x - 20, y - 15); handle.LineTo(x - 31, y - 15);
            handle.CubicTo(x - 34, y + 1, x - 23, y + 5, x - 13, y + 6);
            handle.MoveTo(x + 20, y - 15); handle.LineTo(x + 31, y - 15);
            handle.CubicTo(x + 34, y + 1, x + 23, y + 5, x + 13, y + 6);
            paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = 5; canvas.DrawPath(handle, paint);
        }
    }

    static void Footer(SKCanvas canvas, SKTypeface regular, SKTypeface bold)
    {
        var rect = new SKRect(72, 1152, 1128, 1316);
        using var shader = SKShader.CreateLinearGradient(new SKPoint(72, 1152), new SKPoint(1128, 1316),
            [SKColor.Parse("#956DAE"), SKColor.Parse("#4F2673")], null, SKShaderTileMode.Clamp);
        using var paint = new SKPaint { IsAntialias = true, Shader = shader };
        canvas.DrawRoundRect(rect, 58, 58, paint);
        Logo(canvas, 104, 1176, 116);
        Line(canvas, 239, 1186, 239, 1285, SKColor.Parse("#DCC8ED"), 1.5f);
        Text(canvas, "Психея", 260, 1236, 49, bold, Paper);
        Text(canvas, "Тесты. Психология. Ты.", 262, 1270, 23, regular, Paper);
        using var pill = new SKPaint { IsAntialias = true, Color = SKColor.Parse("#E9D9F5") };
        canvas.DrawRoundRect(new SKRect(663, 1192, 1096, 1279), 44, 44, pill);
        Text(canvas, "Скачать приложение", 703, 1248, 31, bold, Ink, maxWidth: 330);
        Line(canvas, 1053, 1220, 1067, 1236, Ink, 6);
        Line(canvas, 1067, 1236, 1053, 1252, Ink, 6);
        Text(canvas, DownloadUrl.Replace("https://", ""), 600, 1343, 18, regular, Violet, SKTextAlign.Center, 1050);
    }

    static void Grain(SKCanvas canvas)
    {
        using var tile = new SKBitmap(96, 96);
        var random = new Random(73);
        for (int y = 0; y < tile.Height; y++)
            for (int x = 0; x < tile.Width; x++)
                tile.SetPixel(x, y, random.Next(2) == 0 ? new SKColor(91, 57, 93, 9) : new SKColor(255, 255, 255, 19));
        using var shader = SKShader.CreateBitmap(tile, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
        using var paint = new SKPaint { Shader = shader };
        canvas.DrawRect(0, 0, Width, Height, paint);
    }
}
