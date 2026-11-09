using PsyPath.Models;
using PsyPath.UI;

namespace PsyPath.Views;

// статья справочника целиком: открывается из урока кнопкой «В справочник»
public class RefEntryPage : AdminBase
{
    public RefEntryPage(RefEntry entry)
    {
        var s = List();
        var box = new VerticalStackLayout { Spacing = 8 };
        box.Children.Add(Ui.Text(entry.Term.ToUpperInvariant(), 20, Pal.Ink, true));
        box.Children.Add(Ui.Text(entry.Short, 13, Pal.Ink2, true));
        box.Children.Add(Ui.Text(entry.Text, 14));
        s.Children.Add(Ui.Box(box, Pal.Paper, 14));
        s.Children.Add(Ui.Text("Учебные материалы. Не клиническая диагностика.", 11, Pal.Ink2));

        var back = new Btn("НАЗАД К ЗАДАНИЮ ◀", Pal.Violet, Pal.Paper);
        back.Clicked += async (o, e) => await Pop();
        s.Children.Add(back);

        Show("СПРАВОЧНИК", s);
    }
}
