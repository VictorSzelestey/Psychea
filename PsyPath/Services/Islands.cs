using PsyPath.Models;
using PsyPath.UI;

namespace PsyPath.Services;

// остров на главной: разделы уроков, тема и картинка. Number — порядок открытия.
// Image — картинки island_<Image>.png и island_<Image>_gray.png, Box — где остров на карте (доли её стороны),
// Decor — чем украшены кружки уроков на острове. Soon — остров показан, но пока закрыт для всех
public record Island(int Number, string Name, string Image, int[] Sections, string Theme, Rect Box, string Decor, bool Soon = false);

// четыре острова вместо общего списка разделов: открыт первый, следующий — когда пройдены все уроки предыдущего.
// Открытый остров не закрывается, даже если рейтинг упал, и открывает свою тему в магазине
public static class Islands
{
    public static readonly Island[] All =
    {
        new(1, "Остров психологии", "reason", new[] { 1, 2, 13, 14 }, "lilac", new Rect(0.022, 0.014, 0.44, 0.445), "crystal"),
        new(2, "Джунгли физиологии", "jungle", new[] { 17, 18, 19 }, "jungle", new Rect(0.538, 0.006, 0.44, 0.444), "vine"),
        new(3, "Остров ночи", "night", new[] { 15, 16 }, "cosmo", new Rect(0.018, 0.508, 0.452, 0.455), "star"),
        new(4, "Астрал", "astral", Array.Empty<int>(), "astral", new Rect(0.56, 0.536, 0.412, 0.412), "gold", Soon: true)
    };

    // манул в лодке посередине карты
    public static readonly Rect Boat = new(0.398, 0.352, 0.205, 0.241);

    // остров, на котором сейчас игрок; null — карта. Не сохраняется: приложение открывается на карте
    public static Island? Active { get; private set; }

    public static Island? Find(int number) => All.FirstOrDefault(i => i.Number == number);

    // разделы острова в порядке lessons.json. Раздел, которого нет ни на одном острове (новый из админки),
    // попадает на последний открываемый остров, чтобы уроки не пропали с главной
    public static List<Section> SectionsOf(Island island)
    {
        var last = All.Last(i => !i.Soon);
        return AppState.Lessons.Sections
            .Where(s => island.Sections.Contains(s.Id) || (island == last && !All.Any(i => i.Sections.Contains(s.Id))))
            .ToList();
    }

    public static List<Lesson> LessonsOf(Island island) => SectionsOf(island).SelectMany(s => s.Lessons).ToList();

    public static bool Finished(Island island)
    {
        var lessons = LessonsOf(island);
        return lessons.Count > 0 && lessons.All(AppState.IsPassed);
    }

    // админ открывает остров без прохождения: замок упадёт при следующем показе карты
    public static void OpenByAdmin(Island island)
    {
        if (island.Soon || IsOpen(island)) return;
        AppState.P.Islands.Add(island.Number);
        AppState.Save();
    }

    public static bool IsOpen(Island island) =>
        !island.Soon && (island.Number == 1 || AppState.P.Islands.Contains(island.Number));

    // открывает остров, предыдущий которого пройден; true — что-то открылось и прогресс надо сохранить
    public static bool Update()
    {
        bool changed = false;
        for (int i = 1; i < All.Length; i++)
        {
            var island = All[i];
            if (island.Soon || AppState.P.Islands.Contains(island.Number)) continue;
            var prev = All[i - 1];
            if (!IsOpen(prev) || !Finished(prev)) continue;
            AppState.P.Islands.Add(island.Number);
            changed = true;
        }
        return changed;
    }

    // открытые острова, для которых карта ещё не показала, как падает замок
    public static List<Island> NotShown() =>
        All.Where(i => i.Number > 1 && IsOpen(i) && !AppState.P.IslandsShown.Contains(i.Number)).ToList();

    public static Theme ThemeOf(Island island) => Themes.Find(island.Theme);

    // тема на экране: на острове — его, на карте — выбранная в магазине
    public static Theme CurrentTheme => Active != null ? ThemeOf(Active) : Themes.Find(AppState.P.Theme);

    // тема острова доступна в магазине, когда остров открыт; остальные темы — по цене
    public static bool ThemeOpen(Theme t) => t.Island == 0 || (Find(t.Island) is { } island && IsOpen(island));

    // на остров или обратно на карту (null): тема и музыка меняются, экраны перестраивает вызывающий
    public static void Enter(Island? island)
    {
        Active = island;
        Pal.Apply(CurrentTheme);
        _ = Music.UseTheme();
    }
}
