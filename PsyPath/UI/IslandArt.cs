namespace PsyPath.UI;

// облака перехода между картой и островом: две гряды съезжаются с краёв и закрывают экран, потом разъезжаются.
// Progress 0 — облаков не видно, 1 — экран закрыт целиком
class CloudsDrawable : IDrawable
{
    public float Progress { get; set; }

    static readonly Color Body = Color.FromArgb("#FFFFFF");
    static readonly Color Shade = Color.FromArgb("#E6DEF2");

    public void Draw(ICanvas canvas, RectF rect)
    {
        if (Progress <= 0) return;
        float w = rect.Width, h = rect.Height;
        float r = Math.Max(w, h) * 0.13f;
        // кромка гряды при Progress = 1 — середина экрана; при 0 гряда целиком за краем вместе с клубами
        float travel = w * 0.5f + r * 1.6f;
        float shift = (1 - Ease(Progress)) * travel;
        Ridge(canvas, w, h, r, w * 0.5f - shift, -1);
        Ridge(canvas, w, h, r, w * 0.5f + shift, 1);
    }

    static float Ease(float t) => 1 - (1 - t) * (1 - t) * (1 - t);

    // гряда: сплошное тело от края экрана до кромки и клубы вдоль кромки. side −1 — левая, 1 — правая
    static void Ridge(ICanvas canvas, float w, float h, float r, float edge, int side)
    {
        int rows = (int)Math.Ceiling(h / (r * 1.1f)) + 1;
        canvas.FillColor = Shade;
        for (int i = 0; i < rows; i++)
        {
            float y = i * r * 1.1f;
            float x = edge + side * (i % 2 == 0 ? r * 0.35f : r * 0.75f);
            canvas.FillCircle(x, y + r * 0.18f, r);
        }
        canvas.FillColor = Body;
        if (side < 0) canvas.FillRectangle(edge - w - r * 2, 0, w + r * 2, h);
        else canvas.FillRectangle(edge, 0, w + r * 2, h);
        for (int i = 0; i < rows; i++)
        {
            float y = i * r * 1.1f;
            float x = edge + side * (i % 2 == 0 ? r * 0.35f : r * 0.75f);
            canvas.FillCircle(x, y, r);
            // клуб поменьше внутри гряды: кромка не выглядит ровным рядом кругов
            canvas.FillCircle(x - side * r * 0.9f, y + r * 0.55f, r * 0.8f);
        }
    }
}

public class Clouds : GraphicsView
{
    readonly CloudsDrawable drawable = new();

    public Clouds(bool closed)
    {
        Drawable = drawable;
        drawable.Progress = closed ? 1 : 0;
        BackgroundColor = Colors.Transparent;
        // закрытые облака не пропускают нажатия к экрану под ними
        InputTransparent = !closed;
    }

    public Task Close(uint ms = 520) => Run(1, ms);

    public Task Open(uint ms = 640) => Run(0, ms);

    Task Run(float to, uint ms)
    {
        InputTransparent = to == 0;
        float from = drawable.Progress;
        if (Anim.Reduced || from == to)
        {
            drawable.Progress = to;
            Invalidate();
            return Task.CompletedTask;
        }
        var done = new TaskCompletionSource();
        new Animation(v =>
        {
            drawable.Progress = (float)v;
            Invalidate();
        }, from, to).Commit(this, "clouds", 16, ms, Easing.Linear, (v, c) => done.TrySetResult());
        return done.Task;
    }
}

// украшение кружка урока на острове: кристаллы, лианы, звёзды или золото и мелочи с картинки острова
// (горы со снегом, цветы, папоротники). variant чередует рисунок, чтобы соседние кружки не были одинаковыми.
// Shine 0…1 — белый блик бежит по кристаллам сверху вниз; меньше нуля — блика нет
class DecorDrawable(string kind, int variant, float circle) : IDrawable
{
    public float Shine { get; set; } = -1;

    public void Draw(ICanvas canvas, RectF rect)
    {
        float cx = rect.Width / 2, cy = rect.Height / 2, r = circle / 2;
        switch (kind)
        {
            case "crystal": Reason(canvas, cx, cy, r); break;
            case "vine": Jungle(canvas, cx, cy, r); break;
            case "star": Night(canvas, cx, cy, r); break;
            case "gold": Laurel(canvas, cx, cy, r); break;
        }
    }

    // Остров разума: сиреневые горы со снегом, розовые кристаллы, цветы и трава
    void Reason(ICanvas canvas, float cx, float cy, float r)
    {
        var pink = Color.FromArgb("#F27BB8");
        var lilac = Color.FromArgb("#B07CE8");
        var light = Color.FromArgb("#FFD3EC");
        var mountain = Color.FromArgb("#8E5CC9");
        var grass = Color.FromArgb("#9CC24A");
        switch (variant % 3)
        {
            case 0:
                Shard(canvas, cx - r * 0.95f, cy + r * 0.75f, r * 0.34f, r * 0.85f, -18, pink, light);
                Shard(canvas, cx - r * 0.62f, cy + r * 0.92f, r * 0.24f, r * 0.55f, 12, lilac, light);
                Flower(canvas, cx + r * 0.95f, cy + r * 0.88f, r * 0.15f, pink, Color.FromArgb("#FFE07A"));
                Tuft(canvas, cx - r * 1.2f, cy + r * 1.02f, r * 0.3f, grass);
                break;
            case 1:
                Peak(canvas, cx - r * 1.05f, cy + r * 0.9f, r * 0.62f, r * 0.85f, mountain);
                Shard(canvas, cx + r * 0.95f, cy + r * 0.72f, r * 0.34f, r * 0.9f, 20, lilac, light);
                Shard(canvas, cx + r * 0.6f, cy + r * 0.95f, r * 0.22f, r * 0.5f, -10, pink, light);
                break;
            default:
                Shard(canvas, cx - r * 0.2f, cy - r * 0.98f, r * 0.2f, r * 0.5f, -14, pink, light);
                Shard(canvas, cx + r * 0.18f, cy - r * 1.02f, r * 0.24f, r * 0.62f, 10, lilac, light);
                Peak(canvas, cx + r * 1.1f, cy + r * 0.95f, r * 0.5f, r * 0.7f, mountain);
                Flower(canvas, cx - r * 1.0f, cy + r * 0.85f, r * 0.13f, light, pink);
                Tuft(canvas, cx - r * 0.75f, cy + r * 1.05f, r * 0.25f, grass);
                break;
        }
    }

    // кристалл: шестиугольник с острым верхом, светлая грань слева. (x, y) — основание
    void Shard(ICanvas canvas, float x, float y, float halfW, float height, float angle, Color fill, Color glint)
    {
        canvas.SaveState();
        canvas.Translate(x, y);
        canvas.Rotate(angle);
        var body = new PathF();
        body.MoveTo(-halfW, 0);
        body.LineTo(-halfW, -height * 0.62f);
        body.LineTo(0, -height);
        body.LineTo(halfW, -height * 0.62f);
        body.LineTo(halfW, 0);
        body.Close();
        canvas.FillColor = fill;
        canvas.FillPath(body);
        var face = new PathF();
        face.MoveTo(-halfW, 0);
        face.LineTo(-halfW, -height * 0.62f);
        face.LineTo(0, -height);
        face.LineTo(-halfW * 0.15f, 0);
        face.Close();
        canvas.FillColor = glint.WithAlpha(0.7f);
        canvas.FillPath(face);
        if (Shine >= 0)
        {
            // блик — косая белая полоса внутри кристалла, от вершины к основанию
            canvas.ClipPath(body);
            float by = -height + Shine * height * 1.2f;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeColor = Colors.White.WithAlpha(0.35f);
            canvas.StrokeSize = halfW * 0.9f;
            canvas.DrawLine(-halfW * 1.2f, by - halfW * 0.5f, halfW * 1.2f, by + halfW * 0.5f);
            canvas.StrokeColor = Colors.White.WithAlpha(0.95f);
            canvas.StrokeSize = Math.Max(1.5f, halfW * 0.28f);
            canvas.DrawLine(-halfW * 1.2f, by - halfW * 0.5f, halfW * 1.2f, by + halfW * 0.5f);
        }
        canvas.RestoreState();
    }

    // гора со снежной шапкой; (x, y) — середина основания
    static void Peak(ICanvas canvas, float x, float y, float halfW, float height, Color color)
    {
        var body = new PathF();
        body.MoveTo(x - halfW, y);
        body.LineTo(x, y - height);
        body.LineTo(x + halfW, y);
        body.Close();
        canvas.FillColor = color;
        canvas.FillPath(body);
        // тёмный склон справа — объём
        var side = new PathF();
        side.MoveTo(x, y - height);
        side.LineTo(x + halfW, y);
        side.LineTo(x + halfW * 0.15f, y);
        side.Close();
        canvas.FillColor = Colors.Black.WithAlpha(0.18f);
        canvas.FillPath(side);
        var snow = new PathF();
        float k = 0.36f;
        snow.MoveTo(x, y - height);
        snow.LineTo(x + halfW * k, y - height * (1 - k));
        snow.LineTo(x + halfW * k * 0.4f, y - height * (1 - k * 0.75f));
        snow.LineTo(x, y - height * (1 - k));
        snow.LineTo(x - halfW * k * 0.5f, y - height * (1 - k * 0.7f));
        snow.LineTo(x - halfW * k, y - height * (1 - k));
        snow.Close();
        canvas.FillColor = Color.FromArgb("#F6F1FF");
        canvas.FillPath(snow);
    }

    // цветок из пяти лепестков
    static void Flower(ICanvas canvas, float x, float y, float s, Color petal, Color middle)
    {
        canvas.FillColor = petal;
        for (int i = 0; i < 5; i++)
        {
            double a = (i * 72 - 90) * Math.PI / 180;
            canvas.FillCircle(x + (float)Math.Cos(a) * s, y + (float)Math.Sin(a) * s, s * 0.75f);
        }
        canvas.FillColor = middle;
        canvas.FillCircle(x, y, s * 0.55f);
    }

    // пучок травы: три изогнутых стебля
    static void Tuft(ICanvas canvas, float x, float y, float h, Color color)
    {
        canvas.StrokeColor = color;
        canvas.StrokeSize = 2;
        canvas.StrokeLineCap = LineCap.Round;
        foreach (float dx in new[] { -0.45f, 0, 0.45f })
        {
            var p = new PathF();
            p.MoveTo(x + dx * h * 0.4f, y);
            p.QuadTo(x + dx * h * 0.5f, y - h * 0.6f, x + dx * h * 1.2f, y - h * (dx == 0 ? 1.1f : 0.8f));
            canvas.DrawPath(p);
        }
    }

    // Джунгли: лиана обвивает кружок дугой, на ней листья; ещё тропический цветок или папоротник
    void Jungle(ICanvas canvas, float cx, float cy, float r)
    {
        var stem = Color.FromArgb("#6E8B2E");
        var leaf = Color.FromArgb("#8CC04A");
        var dark = Color.FromArgb("#4F7A23");
        float start = variant % 3 switch { 0 => 150, 1 => 200, _ => 250 };
        float sweep = 150;
        float rr = r + 3;
        canvas.StrokeColor = stem;
        canvas.StrokeSize = 3;
        canvas.StrokeLineCap = LineCap.Round;
        var arc = new PathF();
        for (int i = 0; i <= 20; i++)
        {
            double a = (start + sweep * i / 20) * Math.PI / 180;
            // лиана слегка волнится
            float wave = (float)Math.Sin(i * 1.3) * 1.6f;
            float x = cx + (float)Math.Cos(a) * (rr + wave), y = cy + (float)Math.Sin(a) * (rr + wave);
            if (i == 0) arc.MoveTo(x, y);
            else arc.LineTo(x, y);
        }
        canvas.DrawPath(arc);
        for (int i = 1; i < 5; i++)
        {
            double a = (start + sweep * i / 5) * Math.PI / 180;
            float x = cx + (float)Math.Cos(a) * (rr + 2), y = cy + (float)Math.Sin(a) * (rr + 2);
            Leaf(canvas, x, y, r * 0.3f, (float)(a * 180 / Math.PI) + (i % 2 == 0 ? 40 : -40), i % 2 == 0 ? leaf : dark);
        }
        if (variant % 3 != 0)
        {
            // свисающий побег с двумя листьями
            float hx = cx + (variant % 3 == 1 ? -r * 0.55f : r * 0.6f), hy = cy + r * 0.82f;
            canvas.StrokeColor = stem;
            canvas.DrawLine(hx, hy, hx + 2, hy + r * 0.55f);
            Leaf(canvas, hx + 2, hy + r * 0.55f, r * 0.26f, 100, leaf);
            Leaf(canvas, hx + 1, hy + r * 0.28f, r * 0.22f, 20, dark);
        }
        // тропический цветок на лиане или папоротник у основания
        if (variant % 2 == 0)
            Flower(canvas, cx - r * 0.98f, cy - r * 0.35f, r * 0.17f, Color.FromArgb("#E05A9A"), Color.FromArgb("#FFD24A"));
        else
            Fern(canvas, cx + r * 1.05f, cy + r * 1.0f, r * 0.75f, -60, Color.FromArgb("#5FA83A"));
    }

    // папоротник: стебель и пары листочков, к концу меньше. (x, y) — основание
    static void Fern(ICanvas canvas, float x, float y, float len, float angle, Color color)
    {
        canvas.SaveState();
        canvas.Translate(x, y);
        canvas.Rotate(angle);
        canvas.StrokeColor = color;
        canvas.StrokeSize = 2;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(0, 0, len, 0);
        for (int i = 1; i <= 5; i++)
        {
            float t = i / 6f, size = len * 0.32f * (1 - t * 0.6f);
            Leaf(canvas, len * t, 0, size, -55, color);
            Leaf(canvas, len * t, 0, size, 55, color);
        }
        canvas.RestoreState();
    }

    static void Leaf(ICanvas canvas, float x, float y, float len, float angle, Color color)
    {
        canvas.SaveState();
        canvas.Translate(x, y);
        canvas.Rotate(angle);
        var p = new PathF();
        p.MoveTo(0, 0);
        p.CurveTo(len * 0.3f, -len * 0.45f, len * 0.8f, -len * 0.35f, len, 0);
        p.CurveTo(len * 0.8f, len * 0.35f, len * 0.3f, len * 0.45f, 0, 0);
        canvas.FillColor = color;
        canvas.FillPath(p);
        canvas.RestoreState();
    }

    // Остров ночи: четырёхлучевые звёзды, месяц, индиговая вершина со снегом и бирюзовые кусты
    void Night(ICanvas canvas, float cx, float cy, float r)
    {
        var star = Color.FromArgb("#9CC2FF");
        var warm = Color.FromArgb("#F6E7A6");
        var teal = Color.FromArgb("#4FD1D9");
        var indigo = Color.FromArgb("#4A4FB0");
        switch (variant % 3)
        {
            case 0:
                Sparkle(canvas, cx - r * 1.12f, cy - r * 0.55f, r * 0.3f, star);
                Sparkle(canvas, cx - r * 0.82f, cy + r * 0.95f, r * 0.18f, warm);
                Bush(canvas, cx + r * 1.0f, cy + r * 0.98f, r * 0.42f, teal);
                break;
            case 1:
                Moon(canvas, cx + r * 0.98f, cy - r * 0.78f, r * 0.3f, warm);
                Peak(canvas, cx - r * 1.05f, cy + r * 0.92f, r * 0.55f, r * 0.8f, indigo);
                Sparkle(canvas, cx + r * 1.15f, cy + r * 0.5f, r * 0.2f, star);
                break;
            default:
                Sparkle(canvas, cx, cy - r * 1.22f, r * 0.24f, warm);
                Sparkle(canvas, cx - r * 1.1f, cy + r * 0.3f, r * 0.17f, star);
                Bush(canvas, cx - r * 0.95f, cy + r * 1.0f, r * 0.36f, teal);
                Sparkle(canvas, cx + r * 1.08f, cy + r * 0.62f, r * 0.22f, star);
                break;
        }
    }

    // бирюзовый куст: веер листьев из одной точки, как растения у водопада
    static void Bush(ICanvas canvas, float x, float y, float len, Color color)
    {
        foreach (float a in new[] { -150f, -120f, -90f, -60f, -30f })
            Leaf(canvas, x, y, len * (a == -90 ? 1 : 0.8f), a, color);
    }

    static void Sparkle(ICanvas canvas, float x, float y, float s, Color color)
    {
        var p = new PathF();
        p.MoveTo(x, y - s);
        p.QuadTo(x, y, x + s, y);
        p.QuadTo(x, y, x, y + s);
        p.QuadTo(x, y, x - s, y);
        p.QuadTo(x, y, x, y - s);
        p.Close();
        canvas.FillColor = color;
        canvas.FillPath(p);
    }

    // месяц: светлый круг, перекрытый кругом фона острова
    static void Moon(ICanvas canvas, float x, float y, float s, Color color)
    {
        canvas.FillColor = color;
        canvas.FillCircle(x, y, s);
        canvas.FillColor = Pal.Bg;
        canvas.FillCircle(x + s * 0.45f, y - s * 0.3f, s * 0.85f);
    }

    // золотые лавровые ветви по бокам кружка — для Астрала
    void Laurel(ICanvas canvas, float cx, float cy, float r)
    {
        var gold = Color.FromArgb("#D9A93A");
        var light = Color.FromArgb("#F3D27A");
        foreach (int side in new[] { -1, 1 })
            for (int i = 0; i < 4; i++)
            {
                double a = (90 + side * (40 + i * 28)) * Math.PI / 180;
                float x = cx + (float)Math.Cos(a) * (r + 4), y = cy + (float)Math.Sin(a) * (r + 4);
                Leaf(canvas, x, y, r * 0.32f, (float)(a * 180 / Math.PI) - side * 70, i % 2 == 0 ? gold : light);
            }
    }
}

public class Decor : GraphicsView
{
    // блик пробегает за долю ShineShare периода, потом пауза
    const uint ShinePeriodMs = 3200;
    const double ShineShare = 0.3;

    readonly DecorDrawable drawable;

    // circle — диаметр кружка; украшение выходит за него на треть с каждой стороны.
    // Блик — у каждого пятого кристального кружка
    public Decor(string kind, int variant, double circle)
    {
        Drawable = drawable = new DecorDrawable(kind, variant, (float)circle);
        BackgroundColor = Colors.Transparent;
        InputTransparent = true;
        WidthRequest = circle * 1.7;
        HeightRequest = circle * 1.7;
        Margin = new Thickness(-circle * 0.35);
        HorizontalOptions = LayoutOptions.Center;
        VerticalOptions = LayoutOptions.Center;
        if (kind == "crystal" && variant % 5 == 0 && !Anim.Reduced)
        {
            Loaded += (s, e) => StartShine(variant);
            Unloaded += (s, e) => this.AbortAnimation("shine");
        }
    }

    void StartShine(int seed)
    {
        // у соседних бликов разный старт, чтобы они не мигали хором
        float phase = (seed * 0.37f) % 1;
        new Animation(v =>
        {
            double t = (v + phase) % 1;
            float shine = t < ShineShare ? (float)(t / ShineShare) : -1;
            if (shine < 0 && drawable.Shine < 0) return;
            drawable.Shine = shine;
            Invalidate();
        }).Commit(this, "shine", 16, ShinePeriodMs, Easing.Linear, null, () => IsLoaded);
    }
}
