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
        new(1, "Остров разума", "reason", new[] { 1, 2 }, "lilac", new Rect(0.034, 0.014, 0.437, 0.41), "crystal"),
        new(2, "Джунгли психики", "jungle", new[] { 13, 14 }, "jungle", new Rect(0.523, 0.01, 0.452, 0.404), "vine"),
        new(3, "Остров ночи", "night", new[] { 15, 16 }, "cosmo", new Rect(0.024, 0.477, 0.464, 0.481), "star"),
        new(4, "Астрал", "astral", Array.Empty<int>(), "astral", new Rect(0.548, 0.506, 0.427, 0.443), "gold", Soon: true)
    };

    // манул в лодке посередине карты
    public static readonly Rect Boat = new(0.413, 0.357, 0.203, 0.227);

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
