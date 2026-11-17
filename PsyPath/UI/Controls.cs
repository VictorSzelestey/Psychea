using Microsoft.Maui.Controls.Shapes;
using PsyPath.Services;

namespace PsyPath.UI;

public class Btn : ContentView
{
    readonly Border top;
    readonly Lifted g;
    readonly Label label;
    bool enabled = true;

    public event EventHandler? Clicked;

    public Btn(string text, Color fill, Color? textColor = null, double size = 16)
    {
        label = Ui.Text(text, size, textColor ?? Pal.Ink, true, TextAlignment.Center);
        label.VerticalTextAlignment = TextAlignment.Center;

        top = Ui.Card(label, fill, 13, 24);
        top.StrokeThickness = 0;

        g = new Lifted(top, 24);
        Content = g;

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) =>
        {
            if (!enabled) return;
            Sound.Play("tap");
            await top.Zoom(0.97, 50, Easing.CubicOut);
            await top.Zoom(1, 60, Easing.CubicOut);
            Clicked?.Invoke(this, EventArgs.Empty);
        };
        g.GestureRecognizers.Add(tap);
    }

    public string Text
    {
        get => label.Text;
        set => label.Text = value;
    }

    public bool Enabled
    {
        get => enabled;
        set
        {
            enabled = value;
            top.Opacity = value ? 1 : 0.45;
            g.Shade.Opacity = top.Opacity;
        }
    }

    public void SetColors(Color fill, Color? textColor = null)
    {
        top.BackgroundColor = fill;
        if (textColor != null) label.TextColor = textColor;
    }
}

public class Bar : Border
{
    // доля задаётся звёздными столбцами: ширина считается за один проход раскладки,
    // без SizeChanged и второго прохода, из-за которого полоса прыгала
    readonly ColumnDefinition filled = new(new GridLength(0, GridUnitType.Star));
    readonly ColumnDefinition rest = new(new GridLength(1, GridUnitType.Star));
    double val;
    double shown;

    public Bar(Color? color = null)
    {
        var fill = new BoxView
        {
            Color = color ?? Pal.Violet,
            CornerRadius = new CornerRadius(4)
        };
        var g = new Grid { ColumnSpacing = 0, ColumnDefinitions = { filled, rest } };
        g.Children.Add(fill);

        BackgroundColor = Pal.Paper2;
        Stroke = new SolidColorBrush(Pal.Line);
        StrokeThickness = 1;
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(7) };
        HeightRequest = 14;
        Padding = new Thickness(0);
        Content = g;
        // при появлении на экране полоса плавно заполняется от нуля
        Loaded += (s, e) => Animate(600);
    }

    public double Value
    {
        get => val;
        set
        {
            val = Math.Clamp(value, 0, 1);
            if (IsLoaded) Animate(350);
        }
    }

    void Animate(uint ms)
    {
        this.AbortAnimation("fill");
        if (Anim.Reduced)
        {
            Draw(val);
            return;
        }
        new Animation(Draw, shown, val, Easing.CubicOut).Commit(this, "fill", 16, ms);
    }

    void Draw(double f)
    {
        shown = f;
        filled.Width = new GridLength(f, GridUnitType.Star);
        rest.Width = new GridLength(1 - f, GridUnitType.Star);
    }
}

// заголовок экрана
public class Heading : Grid
{
    public Heading(string text, double size = 28, Color? color = null)
    {
        Children.Add(Ui.Text(text, size, color ?? Pal.Ink, true));
    }
}

// холмы и облака за содержимым экрана
class BackdropDrawable : IDrawable
{
    // гамма джунглей: лайм, олива и тёмная зелень. В приложении их можно взять из темы (Pal.Ink, Pal.Ink2)
    static readonly Color Lime = Color.FromArgb("#95C716");
    static readonly Color Olive = Color.FromArgb("#506322");
    static readonly Color Forest = Color.FromArgb("#22481F");

    public void Draw(ICanvas canvas, RectF rect)
    {
        float w = rect.Width, h = rect.Height;

        if (Pal.Space)
            Sky(canvas, w, h);
        else
        {
            canvas.FillColor = Pal.Hill;
            canvas.FillCircle(w * 0.84f, 70, 54);
            canvas.FillCircle(w * 0.62f, 90, 36);
            canvas.FillCircle(w, 100, 46);
            // в джунглях левое облако выше на 15% экрана
            float cloud = Pal.Scene == "jungle" ? h * 0.15f : 0;
            canvas.FillCircle(-6, h * 0.46f - cloud, 44);
            canvas.FillCircle(w * 0.14f, h * 0.48f - cloud, 30);
            canvas.FillRoundedRectangle(-20, h * 0.48f - cloud, w * 0.34f, 34, 17);
        }

        float top = h * 0.74f;
        switch (Pal.Scene)
        {
            case "jungle":
                Lianas(canvas, w, h);
                Jungle(canvas, w, h, top);
                break;
            case "astral":
                Temple(canvas, w, h, top);
                break;
            case "night":
                NightMountains(canvas, w, h, top);
                break;
            default:
                Mountains(canvas, w, h, top);
                if (Pal.Scene == "crystal") Crystals(canvas, w, top);
                break;
        }
        Hill(canvas, w, h, h * 0.86f, Pal.Hill);

        // передний план — поверх холма
        switch (Pal.Scene)
        {
            case "jungle":
                JungleFront(canvas, w, h);
                break;
            case "night":
                River(canvas, w, h, top);
                break;
            case "crystal":
                HillCrystals(canvas, w, h);
                break;
        }
    }

    // пейзажи островов — в тонах фона, без ярких цветов: они не спорят с карточками

    // Остров разума: у подножия гор пара кристаллов, тёмная грань и светлая
    static void Crystals(ICanvas canvas, float w, float top)
    {
        var dark = Pal.HillFar.AddLuminosity(-0.07f);
        var light = Pal.Hill.AddLuminosity(0.03f);
        Shard(canvas, w * 0.31f, top + 56, 13, 58, -10, dark, light);
        Shard(canvas, w * 0.36f, top + 58, 9, 36, 14, dark, light);
        Shard(canvas, w * 0.74f, top + 52, 11, 46, 8, dark, light);
        Shard(canvas, w * 0.69f, top + 54, 7, 26, -16, dark, light);
    }

    // по ближнему холму рассыпаны кристаллы: группы по два-три и одиночные, крупные ближе к краям
    static void HillCrystals(ICanvas canvas, float w, float h)
    {
        var dark = Pal.HillFar.AddLuminosity(-0.07f);
        var violet = Pal.Amber;
        var light = Pal.Hill.AddLuminosity(0.03f);
        (float X, float Y, float HalfW, float Height, float Angle, bool Violet)[] shards =
        {
            (0.07f, 0.965f, 12, 52, -12, false), (0.13f, 0.975f, 8, 30, 16, true), (0.02f, 0.985f, 7, 24, -28, true),
            (0.27f, 0.925f, 6, 22, -8, true), (0.31f, 0.93f, 4, 13, 20, false),
            (0.45f, 0.99f, 9, 34, 6, false), (0.5f, 0.995f, 6, 20, 26, true), (0.41f, 1.0f, 5, 16, -24, true),
            (0.62f, 0.94f, 5, 18, -14, false),
            (0.79f, 0.975f, 11, 44, 10, true), (0.74f, 0.985f, 7, 26, -18, false), (0.84f, 0.99f, 6, 18, 30, false),
            (0.95f, 0.925f, 8, 30, -6, false), (0.99f, 0.93f, 5, 16, 22, true),
        };
        foreach (var s in shards)
            Shard(canvas, w * s.X, h * s.Y, s.HalfW, s.Height, s.Angle, s.Violet ? violet : dark, light);
    }

    static void Shard(ICanvas canvas, float x, float y, float halfW, float height, float angle, Color body, Color face)
    {
        canvas.SaveState();
        canvas.Translate(x, y);
        canvas.Rotate(angle);
        var p = new PathF();
        p.MoveTo(-halfW, 0);
        p.LineTo(-halfW, -height * 0.62f);
        p.LineTo(0, -height);
        p.LineTo(halfW, -height * 0.62f);
        p.LineTo(halfW, 0);
        p.Close();
        canvas.FillColor = body;
        canvas.FillPath(p);
        var f = new PathF();
        f.MoveTo(-halfW, 0);
        f.LineTo(-halfW, -height * 0.62f);
        f.LineTo(0, -height);
        f.LineTo(-halfW * 0.1f, 0);
        f.Close();
        canvas.FillColor = face;
        canvas.FillPath(f);
        canvas.RestoreState();
    }

    static Color Mix(Color a, Color b, float t) =>
        new(a.Red + (b.Red - a.Red) * t, a.Green + (b.Green - a.Green) * t, a.Blue + (b.Blue - a.Blue) * t);

    static PointF Bezier(PointF a, PointF b, PointF c, PointF d, float t)
    {
        float u = 1 - t;
        return new(u * u * u * a.X + 3 * u * u * t * b.X + 3 * u * t * t * c.X + t * t * t * d.X,
                   u * u * u * a.Y + 3 * u * u * t * b.Y + 3 * u * t * t * c.Y + t * t * t * d.Y);
    }

    // лист вдоль оси x от черенка (0, 0); bend > 0 — кончик клонится вниз
    static void Leaf(ICanvas canvas, float x, float y, float len, float wid, float angle, Color fill,
        Color? rib = null, float bend = 0, int veins = 0)
    {
        canvas.SaveState();
        canvas.Translate(x, y);
        canvas.Rotate(angle);
        float b = bend * len;
        var p = new PathF();
        p.MoveTo(0, 0);
        p.CurveTo(len * 0.25f, -wid + b * 0.2f, len * 0.65f, -wid * 0.8f + b * 0.7f, len, b);
        p.CurveTo(len * 0.65f, wid * 0.8f + b * 0.7f, len * 0.25f, wid + b * 0.2f, 0, 0);
        p.Close();
        canvas.FillColor = fill;
        canvas.FillPath(p);
        if (rib is Color r)
        {
            canvas.StrokeColor = r;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeSize = Math.Max(1f, wid * 0.07f);
            var m = new PathF();
            m.MoveTo(0, 0);
            m.QuadTo(len * 0.5f, b * 0.45f, len * 0.9f, b * 0.9f);
            canvas.DrawPath(m);
            canvas.StrokeSize = Math.Max(0.8f, wid * 0.04f);
            for (int i = 1; i <= veins; i++)
            {
                float t = i / (veins + 1f), vx = len * t * 0.9f, vy = b * t * t * 0.9f;
                float reach = wid * 0.75f * (1 - t * 0.6f);
                canvas.DrawLine(vx, vy, vx + reach * 0.7f, vy - reach);
                canvas.DrawLine(vx, vy, vx + reach * 0.7f, vy + reach);
            }
        }
        canvas.RestoreState();
    }

    // лиана: стебель-кривая, вдоль неё листочки попеременно с двух сторон
    static void Vine(ICanvas canvas, PointF a, PointF b, PointF c, PointF d, int leaves, float leaf)
    {
        canvas.StrokeColor = Olive;
        canvas.StrokeSize = 2.2f;
        canvas.StrokeLineCap = LineCap.Round;
        var stem = new PathF();
        stem.MoveTo(a);
        stem.CurveTo(b, c, d);
        canvas.DrawPath(stem);
        for (int i = 1; i <= leaves; i++)
        {
            float t = i / (leaves + 0.5f);
            var p = Bezier(a, b, c, d, t);
            var q = Bezier(a, b, c, d, t + 0.01f);
            float dir = MathF.Atan2(q.Y - p.Y, q.X - p.X) * 180 / MathF.PI;
            bool left = i % 2 == 0;
            Leaf(canvas, p.X, p.Y, leaf, leaf * 0.42f, dir + (left ? -50 : 50), i % 3 == 0 ? Olive : Lime, bend: left ? -0.1f : 0.1f);
        }
        // на конце — пара листьев
        Leaf(canvas, d.X, d.Y, leaf * 1.2f, leaf * 0.48f, 60, Lime, bend: 0.1f);
        Leaf(canvas, d.X, d.Y, leaf * 1.2f, leaf * 0.48f, 120, Olive, bend: -0.1f);
    }

    // Джунгли психики: лианы свисают с верхних углов по краям экрана, середина сверху свободна под карточки
    static void Lianas(ICanvas canvas, float w, float h)
    {
        // провисшая петля между краем и верхом
        Vine(canvas, new(-4, h * 0.05f), new(30, h * 0.2f), new(w * 0.24f, h * 0.15f), new(w * 0.3f, -4), 9, 11);
        Vine(canvas, new(16, -4), new(-8, h * 0.14f), new(36, h * 0.24f), new(18, h * 0.38f), 12, 12);
        Vine(canvas, new(52, -4), new(60, h * 0.08f), new(40, h * 0.15f), new(50, h * 0.22f), 7, 10);

        Vine(canvas, new(w + 4, h * 0.07f), new(w - 34, h * 0.19f), new(w * 0.78f, h * 0.12f), new(w * 0.72f, -4), 8, 11);
        Vine(canvas, new(w - 20, -4), new(w + 4, h * 0.12f), new(w - 44, h * 0.2f), new(w - 26, h * 0.31f), 10, 12);
        Vine(canvas, new(w - 58, -4), new(w - 66, h * 0.07f), new(w - 46, h * 0.12f), new(w - 54, h * 0.17f), 6, 10);

        // крупные листья в верхних углах, из-за края экрана
        Leaf(canvas, -14, -10, 58, 16, 58, Forest, Olive, 0.08f);
        Leaf(canvas, -10, -14, 54, 14, 28, Olive, Lime, 0.1f);
        Leaf(canvas, 4, -12, 42, 11, 80, Lime, Olive, -0.06f);
        Leaf(canvas, w + 14, -10, 58, 16, 122, Forest, Olive, -0.08f);
        Leaf(canvas, w + 10, -14, 54, 14, 152, Olive, Lime, -0.1f);
        Leaf(canvas, w - 4, -12, 42, 11, 100, Lime, Olive, 0.06f);
    }

    // дальний план — дымка: высокие тонкие стволы и кроны в цвете фона, ближе — бананы и пальмы
    static void Jungle(ICanvas canvas, float w, float h, float top)
    {
        var leafy = Mix(Lime, Olive, 0.5f);
        var far = Mix(Pal.Bg, leafy, 0.14f);
        var farTrunk = Mix(Pal.Bg, leafy, 0.22f);
        var ground = new PathF();
        ground.MoveTo(0, h);
        ground.LineTo(0, top + 30);
        ground.CurveTo(w * 0.3f, top + 10, w * 0.6f, top + 40, w, top + 20);
        ground.LineTo(w, h);
        ground.Close();
        canvas.FillColor = far;
        canvas.FillPath(ground);

        (float X, float Height, float Crown)[] trees =
        {
            (0.06f, 150, 30), (0.2f, 120, 24), (0.33f, 168, 34), (0.47f, 110, 22),
            (0.6f, 158, 30), (0.74f, 128, 26), (0.88f, 172, 34), (0.98f, 118, 22)
        };
        foreach (var t in trees)
        {
            float x = w * t.X, baseY = top + 34, crownY = baseY - t.Height, c = t.Crown;
            canvas.FillColor = farTrunk;
            canvas.FillRectangle(x - 2.5f, crownY, 5, t.Height);
            canvas.FillColor = far;
            canvas.FillCircle(x, crownY - c * 0.2f, c);
            canvas.FillCircle(x - c * 0.9f, crownY + c * 0.25f, c * 0.7f);
            canvas.FillCircle(x + c * 0.95f, crownY + c * 0.2f, c * 0.75f);
            canvas.FillRectangle(x - c * 1.6f, crownY + c * 0.2f, c * 3.3f, c * 0.6f);
        }

        // средний план
        var mid = Mix(Pal.Bg, Lime, 0.4f);
        var midDark = Mix(Pal.Bg, leafy, 0.42f);
        Palm(canvas, w * 0.14f, top + 64, 1.05f, midDark, midDark);
        Palm(canvas, w * 0.66f, top + 60, 0.9f, midDark, midDark);
        Banana(canvas, w * 0.32f, top + 70, 0.8f, mid, midDark);
        Banana(canvas, w * 0.86f, top + 72, 0.95f, mid, midDark);
        Banana(canvas, w * 0.5f, top + 76, 0.6f, mid, midDark);
    }

    // банан: несколько широких листьев веером на черенках
    static void Banana(ICanvas canvas, float x, float baseY, float s, Color leaf, Color rib)
    {
        canvas.StrokeColor = rib;
        canvas.StrokeSize = 3 * s;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(x, baseY, x, baseY - 40 * s);
        foreach (var (a, len, bend) in new[] { (-150f, 70f, -0.18f), (-115f, 82f, -0.1f), (-80f, 86f, 0.06f), (-45f, 78f, 0.14f), (-15f, 64f, 0.2f) })
            Leaf(canvas, x, baseY - 40 * s, len * s, 15 * s, a, leaf, rib, bend, 4);
    }

    // ближний план: у нижних углов большие тёмные листья поднимаются по краям, по низу — кусты лайма
    static void JungleFront(ICanvas canvas, float w, float h)
    {
        Leaf(canvas, -30, h + 10, 170, 30, -96, Mix(Olive, Lime, 0.4f), Lime, -0.1f);
        Leaf(canvas, -24, h + 10, 250, 42, -74, Forest, Olive, 0.12f);
        Leaf(canvas, -20, h + 6, 200, 36, -50, Olive, Lime, 0.16f);
        Leaf(canvas, 4, h + 14, 120, 24, -24, Forest, Olive, 0.2f);

        Leaf(canvas, w + 30, h + 10, 164, 30, -84, Mix(Olive, Lime, 0.4f), Lime, 0.1f);
        Leaf(canvas, w + 24, h + 10, 240, 42, -106, Forest, Olive, -0.12f);
        Leaf(canvas, w + 20, h + 6, 196, 36, -130, Olive, Lime, -0.16f);
        Leaf(canvas, w - 4, h + 14, 116, 24, -156, Forest, Olive, -0.2f);

        Bush(canvas, w * 0.3f, h + 4, 58, Lime, Olive);
        Bush(canvas, w * 0.52f, h + 6, 46, Olive, Lime);
        Bush(canvas, w * 0.7f, h + 4, 62, Lime, Forest);
    }

    // куст: узкие листья звездой из одной точки
    static void Bush(ICanvas canvas, float x, float y, float size, Color main, Color second)
    {
        int i = 0;
        for (float a = -165; a <= -15; a += 18, i++)
        {
            float len = size * (i % 2 == 0 ? 1f : 0.78f);
            Leaf(canvas, x, y, len, len * 0.14f, a, i % 2 == 0 ? main : second, bend: a < -90 ? -0.12f : 0.12f);
        }
    }

    // пальма: изогнутый ствол и веер листьев
    static void Palm(ICanvas canvas, float x, float baseY, float s, Color leaf, Color trunk)
    {
        float topX = x + 14 * s, topY = baseY - 78 * s;
        canvas.StrokeColor = trunk;
        canvas.StrokeSize = 6 * s;
        canvas.StrokeLineCap = LineCap.Round;
        var stem = new PathF();
        stem.MoveTo(x, baseY);
        stem.QuadTo(x - 4 * s, baseY - 44 * s, topX, topY);
        canvas.DrawPath(stem);
        canvas.FillColor = leaf;
        foreach (float a in new[] { -170f, -135f, -95f, -55f, -15f })
        {
            canvas.SaveState();
            canvas.Translate(topX, topY);
            canvas.Rotate(a);
            float len = 40 * s;
            var p = new PathF();
            p.MoveTo(0, 0);
            p.CurveTo(len * 0.3f, -len * 0.3f, len * 0.8f, -len * 0.15f, len, len * 0.25f);
            p.CurveTo(len * 0.7f, len * 0.05f, len * 0.3f, len * 0.12f, 0, 0);
            canvas.FillPath(p);
            canvas.RestoreState();
        }
    }

    // Остров ночи: четыре вершины, правые склоны подсвечены планетой — у каждой своя грань
    static void NightMountains(ICanvas canvas, float w, float h, float top)
    {
        (float X, float Y)[] ridge =
        {
            (0, top + 50), (0.12f, top - 12), (0.25f, top + 40), (0.4f, top - 30), (0.5f, top + 12),
            (0.65f, top - 56), (0.8f, top + 38), (0.92f, top - 6), (1, top + 22)
        };
        var path = new PathF();
        path.MoveTo(0, h);
        foreach (var p in ridge)
            path.LineTo(w * p.X, p.Y);
        path.LineTo(w, h);
        path.Close();
        canvas.FillColor = Pal.HillFar;
        canvas.FillPath(path);

        // Ridge — докуда грань идёт по склону к седловине, Inner и Outer — где её края у подножия
        // (доля пути от вершины к седловине): края расходятся под разными углами
        (float Ridge, float Inner, float Outer)[] faces = { (1f, -0.25f, 0.8f), (0.75f, 0.45f, 1.15f), (1f, 0.05f, 0.7f), (1f, 0.55f, 1.4f) };
        canvas.FillColor = Mix(Pal.HillFar, Pal.Hill, 0.35f);
        for (int i = 1, f = 0; i < ridge.Length - 1; i += 2, f++)
        {
            var (px, py) = (w * ridge[i].X, ridge[i].Y);
            var (vx, vy) = (w * ridge[i + 1].X, ridge[i + 1].Y);
            var face = new PathF();
            face.MoveTo(px, py);
            face.LineTo(px + (vx - px) * faces[f].Ridge, py + (vy - py) * faces[f].Ridge);
            face.LineTo(px + (vx - px) * faces[f].Outer, h);
            face.LineTo(px + (vx - px) * faces[f].Inner, h);
            face.Close();
            canvas.FillPath(face);
        }
    }

    // река от подножия самой высокой вершины: одним цветом, виляет и расширяется к низу экрана
    static void River(ICanvas canvas, float w, float h, float top)
    {
        float x = w * 0.7f, src = top + 44;
        int n = 40;
        var left = new PointF[n + 1];
        var right = new PointF[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n, y = src + (h + 4 - src) * t;
            float cx = x - 40 * t - 36 * t * MathF.Sin(t * 5f);
            float half = 0.5f + 20 * t + 72 * t * t;
            left[i] = new(cx - half, y);
            right[i] = new(cx + half, y);
        }
        var river = new PathF();
        river.MoveTo(left[0]);
        for (int i = 1; i <= n; i++) river.LineTo(left[i]);
        for (int i = n; i >= 0; i--) river.LineTo(right[i]);
        river.Close();
        canvas.FillColor = Mix(Pal.Gray, Pal.Hill, 0.25f);
        canvas.FillPath(river);
    }

    // Астрал: облачная гряда и храм с колоннами
    static void Temple(ICanvas canvas, float w, float h, float top)
    {
        canvas.FillColor = Pal.HillFar;
        for (int i = 0; i <= 8; i++)
            canvas.FillCircle(w * i / 8f, top + 40 + (i % 2) * 10, 34);
        canvas.FillRectangle(0, top + 40, w, h - top);

        canvas.FillColor = Pal.HillFar.AddLuminosity(-0.07f);
        float left = w * 0.6f, right = w * 0.92f, floor = top + 20, colH = 62;
        canvas.FillRectangle(left - 8, floor, right - left + 16, 8);
        for (int i = 0; i < 4; i++)
        {
            float cx = left + (right - left) * i / 3f;
            canvas.FillRectangle(cx - 5, floor - colH, 10, colH);
        }
        canvas.FillRectangle(left - 8, floor - colH - 8, right - left + 16, 8);
        var roof = new PathF();
        roof.MoveTo(left - 10, floor - colH - 8);
        roof.LineTo((left + right) / 2, floor - colH - 30);
        roof.LineTo(right + 10, floor - colH - 8);
        roof.Close();
        canvas.FillPath(roof);
        // две одинокие колонны слева
        canvas.FillRectangle(w * 0.12f - 5, floor - 46, 10, 48);
        canvas.FillRectangle(w * 0.2f - 5, floor - 34, 10, 36);
    }

    // тема «Космос»: звёзды, планета с кольцом и луна вместо облаков.
    // Зерно постоянное — звёзды на всех экранах стоят на одних и тех же местах
    static void Sky(ICanvas canvas, float w, float h)
    {
        var rnd = new Random(7);
        for (int i = 0; i < 70; i++)
        {
            float x = (float)rnd.NextDouble() * w;
            float y = (float)rnd.NextDouble() * h * 0.8f;
            float r = rnd.Next(4) == 0 ? 1.6f : 0.9f;
            var color = rnd.Next(6) == 0 ? Pal.Yellow : Pal.Ink;
            canvas.FillColor = color.WithAlpha(rnd.Next(3) == 0 ? 0.9f : 0.45f);
            canvas.FillCircle(x, y, r);
        }

        float px = w * 0.82f, py = 84;
        canvas.StrokeColor = Pal.Gray;
        canvas.StrokeSize = 3;
        canvas.DrawEllipse(px - 66, py - 11, 132, 22);
        canvas.FillColor = Pal.Hill;
        canvas.FillCircle(px, py, 40);
        canvas.FillColor = Pal.HillFar;
        canvas.FillCircle(px - 14, py - 8, 7);
        canvas.FillCircle(px + 12, py + 14, 5);
        var front = new PathF();
        for (int i = 0; i <= 24; i++)
        {
            float a = MathF.PI * i / 24;
            var p = new PointF(px + 66 * MathF.Cos(a), py + 11 * MathF.Sin(a));
            if (i == 0) front.MoveTo(p); else front.LineTo(p);
        }
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawPath(front);

        canvas.FillColor = Pal.Hill;
        canvas.FillCircle(w * 0.12f, h * 0.46f, 14);
    }

    static void Mountains(ICanvas canvas, float w, float h, float top)
    {
        var path = new PathF();
        path.MoveTo(0, h);
        path.LineTo(0, top + 50);
        path.LineTo(w * 0.17f, top - 14);
        path.LineTo(w * 0.33f, top + 44);
        path.LineTo(w * 0.53f, top - 46);
        path.LineTo(w * 0.72f, top + 40);
        path.LineTo(w * 0.87f, top - 8);
        path.LineTo(w, top + 30);
        path.LineTo(w, h);
        path.Close();
        canvas.FillColor = Pal.HillFar;
        canvas.FillPath(path);
    }

    static void Hill(ICanvas canvas, float w, float h, float top, Color color)
    {
        var path = new PathF();
        path.MoveTo(0, h);
        path.LineTo(0, top + 40);
        path.CurveTo(w * 0.25f, top - 50, w * 0.45f, top + 60, w * 0.7f, top + 10);
        path.CurveTo(w * 0.85f, top - 20, w * 0.95f, top + 20, w, top);
        path.LineTo(w, h);
        path.Close();
        canvas.FillColor = color;
        canvas.FillPath(path);
    }
}

public class Backdrop : GraphicsView
{
    public Backdrop()
    {
        Drawable = new BackdropDrawable();
        BackgroundColor = Colors.Transparent;
        InputTransparent = true;
    }
}

// штрихи-«лучи» у текущего раздела на пути
class BurstDrawable : IDrawable
{
    public void Draw(ICanvas canvas, RectF rect)
    {
        canvas.StrokeColor = Pal.Violet;
        canvas.StrokeSize = 3.5f;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(4, 25, 12, 26);
        canvas.DrawLine(8, 12, 15, 17);
        canvas.DrawLine(20, 3, 24, 11);
    }
}

public class Burst : GraphicsView
{
    public Burst()
    {
        Drawable = new BurstDrawable();
        BackgroundColor = Colors.Transparent;
        WidthRequest = 30;
        HeightRequest = 30;
        Margin = new Thickness(-16, -10, 0, 0);
        HorizontalOptions = LayoutOptions.Start;
        VerticalOptions = LayoutOptions.Start;
        InputTransparent = true;
    }
}

// линии «соедини пары»: от правого края левой плашки к левому краю правой, цветом пары.
// Слой лежит под плашками: от точек на концах видны полукруглые «разъёмы»
class PairLinesDrawable : IDrawable
{
    public List<(PointF From, PointF To, Color Color)> Links { get; } = new();

    public void Draw(ICanvas canvas, RectF rect)
    {
        canvas.StrokeSize = 3;
        canvas.StrokeLineCap = LineCap.Round;
        foreach (var (a, b, color) in Links)
        {
            canvas.StrokeColor = color;
            canvas.FillColor = color;
            float bend = (b.X - a.X) / 2;
            var path = new PathF();
            path.MoveTo(a);
            path.CurveTo(a.X + bend, a.Y, b.X - bend, b.Y, b.X, b.Y);
            canvas.DrawPath(path);
            canvas.FillCircle(a, 4);
            canvas.FillCircle(b, 4);
        }
    }
}

public class PairLines : GraphicsView
{
    readonly PairLinesDrawable drawable = new();

    public PairLines()
    {
        Drawable = drawable;
        BackgroundColor = Colors.Transparent;
        InputTransparent = true;
    }

    public void Set(IEnumerable<(PointF From, PointF To, Color Color)> links)
    {
        drawable.Links.Clear();
        drawable.Links.AddRange(links);
        Invalidate();
    }
}

public abstract class BasePage : ContentPage
{
    // шире этого контент не растягивается (горизонтальный режим, планшеты)
    const double MaxContentWidth = 640;

    protected readonly Grid Root = new();
    readonly bool isTab;
    View? body;
    bool? landscape;
    bool navigating;

    protected BasePage(bool hideTabs)
    {
        isTab = !hideTabs;
        BackgroundColor = Pal.Bg;
        Shell.SetNavBarIsVisible(this, false);
        NavigationPage.SetHasNavigationBar(this, false);
        if (hideTabs) Shell.SetTabBarIsVisible(this, false);
        SizeChanged += (s, e) => OnResize();
    }

    protected bool IsLandscape => Width > Height;

    protected void SetBody(View body)
    {
        this.body = body;
        ApplyWidth();
        Root.Children.Clear();
        Root.Children.Add(new Backdrop());
        Root.Children.Add(body);
        Content = Root;
    }

    // смена содержимого внутри экрана: старое уходит влево, новое приходит справа
    protected async Task SwapBody(View next)
    {
        // страница ещё не на экране: анимировать нечего, её покажет PlayEnter
        bool shown = Width > 0;
        if (body != null && shown)
            await Task.WhenAll(body.Fade(0, 120), body.Move(-28, 0, 120, Easing.CubicIn));
        SetBody(next);
        if (!shown) return;
        next.Opacity = 0;
        next.TranslationX = 28;
        await Task.WhenAll(next.Fade(1, 220), next.Move(0, 0, 220, Easing.CubicOut));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        CrashLog.Write("Page", GetType().Name);
        Music.Enter(MusicZone);
        navigating = false;
        Refresh();
        _ = PlayEnter();
    }

    // вызывается перед каждым показом экрана, до анимации входа
    protected virtual void Refresh() { }

    // опросники и карточки переопределяют: там музыку можно выключить в настройках
    protected virtual Music.Zone MusicZone => Music.Zone.Menu;

    // false — экран показывается сразу, без въезда (заставка при запуске)
    protected virtual bool AnimateEnter => true;

    // вкладки проявляются, открытые поверх экраны въезжают справа
    async Task PlayEnter()
    {
        if (!AnimateEnter || Content is not View v) return;
        v.CancelAnimations();
        v.Opacity = 0;
        if (isTab)
        {
            // вкладка проявляется и чуть всплывает
            v.TranslationY = 10;
            await Task.WhenAll(v.Fade(1, 200), v.Move(0, 0, 240, Easing.CubicOut));
            return;
        }
        v.TranslationX = 48;
        await Task.WhenAll(v.Fade(1, 240), v.Move(0, 0, 280, Easing.CubicOut));
    }

    // переходы без системной анимации: свою играет PlayEnter нового экрана
    protected async Task Push(Page page)
    {
        if (navigating) return;
        navigating = true;
        await Navigation.PushAsync(page, false);
    }

    protected async Task Pop()
    {
        if (navigating) return;
        navigating = true;
        if (Content is View v)
            await Task.WhenAll(v.Fade(0, 180), v.Move(48, 0, 180, Easing.CubicIn));
        await Navigation.PopAsync(false);
    }

    // замена корневой страницы окна: загрузка → вкладки, сброс → онбординг
    // fade: false — старый экран остаётся видимым, пока строится новый (без чёрной паузы)
    protected async Task SwitchRoot(Page next, bool fade = true)
    {
        if (fade && Content is View v) await v.Fade(0, 200);
        Application.Current!.Windows[0].Page = next;
    }

    protected override bool OnBackButtonPressed()
    {
        if (note != null)
        {
            note.TrySetResult();
            return true;
        }
        if (isTab || Navigation.NavigationStack.Count <= 1) return base.OnBackButtonPressed();
        _ = Pop();
        return true;
    }

    TaskCompletionSource? note;

    // открыто сообщение Note: «Назад» закрывает его, а не экран
    protected bool NoteOpen => note != null;

    // сообщение от манула поверх экрана: затемнение, карточка с манулом, текст и кнопка.
    // Не системное окно — в стиле приложения. Ждёт кнопку или «Назад»
    protected async Task Note(string mood, string title, string text, string button = "ПОНЯТНО")
    {
        if (note != null) return;
        var done = note = new TaskCompletionSource();

        var box = new VerticalStackLayout { Spacing = 12 };
        box.Children.Add(Ui.Mood(mood, 120));
        box.Children.Add(Ui.Text(title, 18, Pal.Ink, true, TextAlignment.Center));
        box.Children.Add(Ui.Text(text, 14, Pal.Ink, false, TextAlignment.Center));
        var ok = new Btn(button, Pal.Violet, Pal.OnAccent, 15);
        ok.Clicked += (s, e) => done.TrySetResult();
        box.Children.Add(ok);

        var card = new Lifted(Ui.Card(box, Pal.Paper, 20, 26), 26);
        card.Margin = new Thickness(19, 22, 19, 15);
        card.MaximumWidthRequest = 430;
        card.VerticalOptions = LayoutOptions.Center;

        var dim = new Grid { BackgroundColor = Colors.Black.WithAlpha(0.45f), Opacity = 0 };
        // нажатия не проходят к экрану под затемнением
        dim.GestureRecognizers.Add(new TapGestureRecognizer());
        dim.Children.Add(card);
        // экран перестроили под сообщением — оно пропало вместе со старым содержимым
        dim.Unloaded += (s, e) => done.TrySetResult();
        Root.Children.Add(dim);

        card.Scale = 0.9;
        await Task.WhenAll(dim.Fade(1, 180), card.Zoom(1, 220, Easing.CubicOut));
        await done.Task;
        await dim.Fade(0, 160);
        Root.Children.Remove(dim);
        note = null;
    }

    void OnResize()
    {
        ApplyWidth();
        bool now = IsLandscape;
        // первый замер в портрете — не смена ориентации, в горизонтали — да: экран собран под портрет
        bool changed = landscape.HasValue ? landscape != now : now;
        landscape = now;
        if (changed) OnOrientationChanged();
    }

    protected virtual void OnOrientationChanged() { }

    void ApplyWidth()
    {
        if (body == null || Width <= 0) return;
        bool wide = Width > MaxContentWidth;
        body.WidthRequest = wide ? MaxContentWidth : -1;
        body.HorizontalOptions = wide ? LayoutOptions.Center : LayoutOptions.Fill;
    }
}

// вкладка, которая перестраивается только когда поменялись данные:
// раньше каждое переключение собирало экран заново, и он дёргался
public abstract class DataPage : BasePage
{
    string builtFor = "";
    ScrollView? scroll;

    protected DataPage() : base(false)
    {
    }

    // собирает экран и возвращает его ScrollView, чтобы сохранить прокрутку
    protected abstract ScrollView Build();

    protected override void Refresh()
    {
        string key = AppState.Version + "|" + DateTime.Today.ToString("yyyy-MM-dd");
        if (key == builtFor) return;
        builtFor = key;
        bool first = scroll == null;
        double y = scroll?.ScrollY ?? 0;
        scroll = Build();
        if (y > 0) RestoreScroll(scroll, y);
        // при первом показе карточки появляются по очереди; при обновлении данных — без этого
        if (first && scroll.Content is Layout list) Anim.Stagger(list);
    }

    static void RestoreScroll(ScrollView s, double y)
    {
        void OnSize(object? sender, EventArgs e)
        {
            s.SizeChanged -= OnSize;
            s.Dispatcher.Dispatch(async () => await s.ScrollToAsync(0, y, false));
        }
        s.SizeChanged += OnSize;
    }
}
