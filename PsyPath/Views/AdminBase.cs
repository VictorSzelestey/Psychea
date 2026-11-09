using System.Text.Encodings.Web;
using System.Text.Json;
using PsyPath.UI;

namespace PsyPath.Views;

// общий каркас экранов админки и других экранов поверх вкладок (настройки, карточки, статья справочника):
// шапка со стрелкой назад, прокрутка, необязательная нижняя панель
public abstract class AdminBase : BasePage
{
    // кириллица в редакторе остаётся читаемой, а не а
    protected static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    protected AdminBase() : base(true)
    {
    }

    // animate — карточки списка появляются по очереди (для экранов, загруженных с сервера)
    protected void Show(string title, View content, View? bottom = null, bool animate = false)
    {
        if (animate && content is Layout list) Anim.Stagger(list);

        var back = Ui.Text("◀", 22, Pal.Ink, true, TextAlignment.Center);
        back.VerticalTextAlignment = TextAlignment.Center;
        // зона нажатия 48×48 dp, хотя сам значок маленький
        back.WidthRequest = 48;
        back.HeightRequest = 48;
        Ui.OnTap(back, async () => await Leave());

        var header = new Grid
        {
            ColumnSpacing = 12,
            Padding = new Thickness(16, 10, 16, 6),
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }
        };
        header.Children.Add(back);
        var t = new Glitch(title, 20);
        Grid.SetColumn(t, 1);
        header.Children.Add(t);

        var top = new VerticalStackLayout { Spacing = 0 };
        top.Children.Add(Ui.Stripes(4));
        top.Children.Add(header);

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };
        root.Children.Add(top);
        var scroll = new ScrollView { Content = content };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        if (bottom != null)
        {
            Grid.SetRow(bottom, 2);
            root.Children.Add(bottom);
        }
        SetBody(root);
    }

    // экран редактора переопределяет, чтобы спросить про несохранённые правки
    protected virtual Task Leave() => Pop();

    protected override bool OnBackButtonPressed()
    {
        _ = Leave();
        return true;
    }

    protected static View Message(string text, Color? color = null)
    {
        var box = new VerticalStackLayout { Padding = new Thickness(18, 16) };
        box.Children.Add(Ui.Text(text, 13, color ?? Pal.Ink2, true));
        return box;
    }

    protected static VerticalStackLayout List() => new() { Spacing = 10, Padding = new Thickness(18, 8, 18, 28) };
}
