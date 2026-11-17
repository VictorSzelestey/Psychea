namespace PsyPath.UI;

// дорога острова поверх фона темы: картинка road_<Image>.png (ширина к высоте 1:3), по дороге снизу вверх —
// остановки (знаки разделов и кружки уроков) на равных расстояниях. Если остановки не помещаются с шагом Gap,
// дорога вытягивается в длину и картинка растягивается. Над остановкой открывается одна подсказка
public class IslandRoad : ContentView
{
    const double BaseRatio = 3;
    // первая и последняя остановка — не у самого края дороги
    const double From = 0.03, To = 0.97;
    // наименьшее расстояние между остановками вдоль дороги, в долях ширины: как у Острова ночи с 18 остановками
    const double Gap = 0.24;

    // осевая линия дороги — точки (x, y) в долях ширины и высоты картинки сверху вниз,
    // сняты с картинок 1.7.3b по цвету дороги; мосты сшиты прямыми
    static readonly Dictionary<string, float[]> Lines = new()
    {
        ["reason"] = new[]
        {
            0.631f, 0.1776f, 0.709f, 0.1927f, 0.668f, 0.2163f, 0.577f, 0.2233f, 0.484f, 0.2272f, 0.393f, 0.2337f, 0.319f, 0.2506f, 0.346f, 0.2769f,
            0.435f, 0.2861f, 0.525f, 0.2944f, 0.614f, 0.303f, 0.699f, 0.3159f, 0.758f, 0.339f, 0.727f, 0.3662f, 0.643f, 0.3793f, 0.552f, 0.3868f,
            0.461f, 0.3923f, 0.37f, 0.3987f, 0.281f, 0.4081f, 0.198f, 0.4223f, 0.157f, 0.4491f, 0.203f, 0.4746f, 0.288f, 0.4872f, 0.379f, 0.495f,
            0.47f, 0.5011f, 0.56f, 0.5088f, 0.642f, 0.5232f, 0.659f, 0.5491f, 0.593f, 0.5683f, 0.501f, 0.5704f, 0.45f, 0.5815f, 0.432f, 0.6116f,
            0.354f, 0.6282f, 0.271f, 0.6421f, 0.216f, 0.6664f, 0.244f, 0.6949f, 0.323f, 0.7109f, 0.411f, 0.7208f, 0.5f, 0.7303f, 0.587f, 0.7412f,
            0.664f, 0.7585f, 0.701f, 0.7864f, 0.666f, 0.8148f, 0.594f, 0.8342f, 0.515f, 0.8504f, 0.452f, 0.8728f, 0.398f, 0.8975f, 0.387f, 0.9269f
        },
        ["jungle"] = new[]
        {
            0.625f, 0.1316f, 0.569f, 0.1537f, 0.648f, 0.1696f, 0.629f, 0.198f, 0.538f, 0.2078f, 0.443f, 0.2131f, 0.348f, 0.2195f, 0.261f, 0.2329f,
            0.24f, 0.2617f, 0.302f, 0.2844f, 0.394f, 0.2933f, 0.489f, 0.2991f, 0.583f, 0.3062f, 0.671f, 0.3192f, 0.726f, 0.3446f, 0.696f, 0.374f,
            0.616f, 0.3914f, 0.523f, 0.4009f, 0.43f, 0.4092f, 0.339f, 0.4199f, 0.259f, 0.4374f, 0.207f, 0.4641f, 0.227f, 0.4913f, 0.315f, 0.5045f,
            0.407f, 0.5146f, 0.501f, 0.5197f, 0.597f, 0.5222f, 0.686f, 0.5346f, 0.768f, 0.551f, 0.785f, 0.5791f, 0.707f, 0.5974f, 0.621f, 0.6122f,
            0.536f, 0.6274f, 0.517f, 0.6463f, 0.56f, 0.6723f, 0.481f, 0.6886f, 0.387f, 0.6957f, 0.296f, 0.7061f, 0.222f, 0.7262f, 0.213f, 0.7569f,
            0.284f, 0.7778f, 0.377f, 0.7864f, 0.471f, 0.7934f, 0.541f, 0.8124f, 0.515f, 0.843f, 0.449f, 0.8654f, 0.358f, 0.8755f, 0.262f, 0.8796f
        },
        ["night"] = new[]
        {
            0.56f, 0.177f, 0.574f, 0.2001f, 0.481f, 0.2102f, 0.388f, 0.2186f, 0.297f, 0.2307f, 0.235f, 0.2544f, 0.265f, 0.2829f, 0.354f, 0.2958f,
            0.449f, 0.3025f, 0.544f, 0.3086f, 0.638f, 0.317f, 0.722f, 0.3328f, 0.731f, 0.3627f, 0.65f, 0.3798f, 0.563f, 0.3939f, 0.479f, 0.409f,
            0.386f, 0.4186f, 0.298f, 0.4321f, 0.257f, 0.4593f, 0.327f, 0.4799f, 0.422f, 0.4872f, 0.518f, 0.4928f, 0.613f, 0.4997f, 0.705f, 0.5103f,
            0.774f, 0.5319f, 0.756f, 0.5618f, 0.671f, 0.5771f, 0.577f, 0.5848f, 0.482f, 0.5921f, 0.436f, 0.612f, 0.457f, 0.6437f, 0.473f, 0.6756f,
            0.418f, 0.7003f, 0.49f, 0.7178f, 0.585f, 0.7249f, 0.676f, 0.736f, 0.735f, 0.7606f, 0.696f, 0.7889f, 0.607f, 0.8019f, 0.513f, 0.8103f,
            0.421f, 0.8202f, 0.339f, 0.8371f, 0.296f, 0.8659f, 0.339f, 0.8938f, 0.418f, 0.9124f, 0.509f, 0.9237f, 0.605f, 0.9283f, 0.702f, 0.9302f
        }
    };

    readonly AbsoluteLayout layer = new();
    readonly float[] line;
    readonly List<(View View, double Size)> stops = new();
    Point[] spots = Array.Empty<Point>();
    // высота дороги к ширине: у картинки 3, при многих остановках дорога вытягивается в длину вместе с картинкой
    double ratio = BaseRatio;
    (View Bubble, int Stop)? bubble;

    // у острова без своей дороги — дорога Острова психологии
    public IslandRoad(string image)
    {
        line = Lines.TryGetValue(image, out var l) ? l : Lines["reason"];
        var picture = new Image { Source = $"road_{(Lines.ContainsKey(image) ? image : "reason")}.png", Aspect = Aspect.Fill };
        AbsoluteLayout.SetLayoutFlags(picture, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.All);
        AbsoluteLayout.SetLayoutBounds(picture, new Rect(0, 0, 1, 1));
        // нажатие мимо остановок закрывает подсказку
        Ui.OnTap(picture, Hide);
        layer.Children.Add(picture);
        Content = layer;
        SizeChanged += (s, e) =>
        {
            if (Width <= 0) return;
            if (Math.Abs(HeightRequest - Width * ratio) > 0.5) HeightRequest = Width * ratio;
            Arrange();
        };
    }

    // остановка — квадрат со стороной size по центру точки на дороге; первая добавленная — внизу, у начала дороги
    public void Add(View stop, double size)
    {
        stops.Add((stop, size));
        layer.Children.Add(stop);
        ratio = BaseRatio;
        while (stops.Count > 1 && Length(ratio) * (To - From) / (stops.Count - 1) < Gap) ratio += 0.1;
        if (Width > 0) HeightRequest = Width * ratio;
    }

    public bool IsOpen(View stop) => bubble is { } b && stops[b.Stop].View == stop;

    // подсказка под остановкой, у нижнего края дороги — над ней. Прошлая закрывается
    public void Show(View stop, View content)
    {
        Hide();
        int i = stops.FindIndex(s => s.View == stop);
        if (i < 0) return;
        bubble = (content, i);
        // высота подсказки известна только после разметки, а от неё зависит, встанет ли она над остановкой
        content.SizeChanged += (s, e) => Arrange();
        layer.Children.Add(content);
        Arrange();
        content.Opacity = 0;
        content.Scale = 0.92;
        _ = Task.WhenAll(content.Fade(1, 140), content.Zoom(1, 180, Easing.CubicOut));
    }

    public void Hide()
    {
        if (bubble is not { } b) return;
        layer.Children.Remove(b.Bubble);
        bubble = null;
    }

    void Arrange()
    {
        double w = Width, h = w * ratio;
        if (w <= 0) return;
        // точки идут сверху вниз, а дорога — снизу вверх
        if (spots.Length != stops.Count) spots = Spread(stops.Count).Reverse().ToArray();
        for (int i = 0; i < stops.Count; i++)
        {
            var (view, size) = stops[i];
            var p = spots[i];
            AbsoluteLayout.SetLayoutBounds(view, new Rect(p.X * w - size / 2, p.Y * h - size / 2, size, size));
        }
        if (bubble is { } b)
        {
            var (_, size) = stops[b.Stop];
            var p = spots[b.Stop];
            double bw = Math.Min(w - 24, 300);
            double bh = b.Bubble.Height > 0 ? b.Bubble.Height : 150;
            double x = Math.Clamp(p.X * w - bw / 2, 12, w - bw - 12);
            double below = p.Y * h + size * 0.38;
            double y = below + bh > h - 12 ? p.Y * h - size * 0.38 - bh : below;
            AbsoluteLayout.SetLayoutBounds(b.Bubble, new Rect(x, y, bw, AbsoluteLayout.AutoSize));
        }
    }

    // n точек на равных расстояниях вдоль дороги, длина считается в пропорциях картинки
    Point[] Spread(int n)
    {
        int count = line.Length / 2;
        var len = new double[count];
        for (int i = 1; i < count; i++)
        {
            double dx = line[2 * i] - line[2 * i - 2], dy = (line[2 * i + 1] - line[2 * i - 1]) * ratio;
            len[i] = len[i - 1] + Math.Sqrt(dx * dx + dy * dy);
        }
        var result = new Point[n];
        for (int k = 0; k < n; k++)
        {
            double at = len[^1] * (n == 1 ? From : From + (To - From) * k / (n - 1));
            int i = 1;
            while (i < count - 1 && len[i] < at) i++;
            double t = len[i] == len[i - 1] ? 0 : (at - len[i - 1]) / (len[i] - len[i - 1]);
            result[k] = new Point(line[2 * i - 2] + (line[2 * i] - line[2 * i - 2]) * t,
                line[2 * i - 1] + (line[2 * i + 1] - line[2 * i - 1]) * t);
        }
        return result;
    }

    // длина осевой линии в долях ширины, если высота дороги — r ширин
    double Length(double r)
    {
        double sum = 0;
        for (int i = 2; i < line.Length; i += 2)
        {
            double dx = line[i] - line[i - 2], dy = (line[i + 1] - line[i - 1]) * r;
            sum += Math.Sqrt(dx * dx + dy * dy);
        }
        return sum;
    }
}
