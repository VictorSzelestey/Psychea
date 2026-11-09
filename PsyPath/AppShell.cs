using PsyPath.UI;
using PsyPath.Views;

namespace PsyPath;

public class AppShell : Shell
{
    public const int ProfileTab = 3;

    // tab — какая вкладка открыта сразу (после смены размера текста — профиль)
    public AppShell(int tab = 0)
    {
        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarBackgroundColor(this, Pal.Paper);
        Shell.SetTabBarForegroundColor(this, Pal.Violet);
        Shell.SetTabBarTitleColor(this, Pal.Violet);
        Shell.SetTabBarUnselectedColor(this, Pal.Ink2);

        var tabs = new TabBar();
        tabs.Items.Add(MakeTab("ОПРОСНИКИ", "▣", typeof(QuestionnairesPage)));
        tabs.Items.Add(MakeTab("СПРАВОЧНИК", "≡", typeof(ReferencePage)));
        tabs.Items.Add(MakeTab("ВОПРОСЫ", "▶", typeof(PathPage)));
        tabs.Items.Add(MakeTab("ПРОФИЛЬ", "●", typeof(ProfilePage)));
        Items.Add(tabs);
        if (tab > 0 && tab < tabs.Items.Count) tabs.CurrentItem = tabs.Items[tab];
    }

    static ShellContent MakeTab(string title, string glyph, Type pageType)
    {
        return new ShellContent
        {
            Title = title,
            Icon = new FontImageSource
            {
                Glyph = glyph,
                FontFamily = "MonoBold",
                Size = 20,
                Color = Pal.Violet
            },
            ContentTemplate = new DataTemplate(pageType)
        };
    }
}
