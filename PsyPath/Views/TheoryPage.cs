using PsyPath.Models;
using PsyPath.UI;

namespace PsyPath.Views;

// конспект урока: вся теория одним списком, открывается книжкой в шапке урока
public class TheoryPage : AdminBase
{
    public TheoryPage(Lesson lesson)
    {
        var list = List();
        list.Spacing = 14;
        list.Children.Add(Ui.Text(lesson.Title, 14, Pal.Ink2, true));
        foreach (var card in lesson.Theory ?? new())
            list.Children.Add(LessonPage.TheoryCardView(card));

        var bottom = new VerticalStackLayout { Padding = new Thickness(16, 8, 16, 16), BackgroundColor = Pal.Paper };
        var back = new Btn("◀ К ЗАДАНИЮ", Pal.Violet, Pal.Paper);
        back.Clicked += async (s, e) => await Pop();
        bottom.Children.Add(back);

        Show("ТЕОРИЯ", list, bottom);
    }
}
