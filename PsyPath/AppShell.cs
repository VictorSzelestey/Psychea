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
        tabs.Items.Add(MakeTab("Главная", "tab_home.png", typeof(PathPage)));
        tabs.Items.Add(MakeTab("Опросники", "tab_quiz.png", typeof(QuestionnairesPage)));
        tabs.Items.Add(MakeTab("Справочник", "tab_book.png", typeof(ReferencePage)));
        tabs.Items.Add(MakeTab("Профиль", "tab_profile.png", typeof(ProfilePage)));
        Items.Add(tabs);
        if (tab > 0 && tab < tabs.Items.Count) tabs.CurrentItem = tabs.Items[tab];
    }

    static ShellContent MakeTab(string title, string icon, Type pageType)
    {
        return new ShellContent
        {
            Title = title,
            Icon = icon,
            ContentTemplate = new DataTemplate(pageType)
        };
    }
}
