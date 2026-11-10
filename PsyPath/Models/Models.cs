using System.Text.Json;
using System.Text.Json.Serialization;
using PsyPath.Services;
namespace PsyPath.Models;

public class LessonsFile
{
    public List<Section> Sections { get; set; } = new();
}

public class Section
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Icon { get; set; } = "";
    public List<Lesson> Lessons { get; set; } = new();
}

public class Lesson
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public int RecommendedLevel { get; set; } = 1;
    public List<TaskItem> Tasks { get; set; } = new();
}

public class TaskItem
{
    public string Type { get; set; } = "";
    public string Q { get; set; } = "";
    public string Story { get; set; } = "";
    public string Explain { get; set; } = "";
    public int Difficulty { get; set; } = 1;
    public List<string> Options { get; set; } = new();
    public JsonElement Answer { get; set; }
    public List<List<string>> Pairs { get; set; } = new();
    public List<string> Items { get; set; } = new();

    // термин справочника для кнопки «В справочник»; пусто — ищется по тексту задания
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ref { get; set; }

    // шаги кейса с ветвлением (type "branch"); у остальных типов null и в JSON не пишется
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<CaseStep>? Steps { get; set; }

    [JsonIgnore]
    public int AnswerIndex => Answer.ValueKind == JsonValueKind.Number ? Answer.GetInt32() : 0;
    [JsonIgnore]
    public bool AnswerBool => Answer.ValueKind == JsonValueKind.True;
}

// шаг кейса с ветвлением: ситуация и варианты действий
public class CaseStep
{
    public string Text { get; set; } = "";
    public List<CaseChoice> Choices { get; set; } = new();
}

public class CaseChoice
{
    public string Text { get; set; } = "";
    // что произошло после выбора
    public string Result { get; set; } = "";
    public bool Good { get; set; }
    // номер следующего шага с 1; 0 — кейс закончен
    public int Next { get; set; }
}

public class QuestionnaireFile
{
    public List<Questionnaire> Items { get; set; } = new();
}

public class Questionnaire
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Emoji { get; set; } = "";
    public string Kind { get; set; } = "choice";
    public string Mode { get; set; } = "scales";
    // режим quadrants: граница, выше которой шкала считается высокой
    public int Split { get; set; }
    public string About { get; set; } = "";
    public string Source { get; set; } = "";
    public string Disclaimer { get; set; } = "";
    public string BlendTemplate { get; set; } = "";
    public Dictionary<string, string> BlendTexts { get; set; } = new();
    public List<QScale> Scales { get; set; } = new();
    public List<QQuestion> Questions { get; set; } = new();

    [JsonIgnore]
    public bool IsYesNo => Kind == "yesno";
}

public class QScale
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Emoji { get; set; } = "";
    public string Tagline { get; set; } = "";
    public string Description { get; set; } = "";
    public string PsychFit { get; set; } = "";
    public List<string> Strengths { get; set; } = new();
    public List<string> WatchOut { get; set; } = new();
    public List<string> StudyTips { get; set; } = new();
    public List<int> Yes { get; set; } = new();
    public List<int> No { get; set; } = new();
    public List<QRange> Ranges { get; set; } = new();
}

public class QRange
{
    public int Max { get; set; }
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
}

public class QQuestion
{
    public string Q { get; set; } = "";
    public List<QOption> Options { get; set; } = new();
}

public class QOption
{
    public string Text { get; set; } = "";
    public string Scale { get; set; } = "";
    public int Points { get; set; } = 1;
}

public class QScore
{
    public QScale Scale { get; set; } = new();
    public int Value { get; set; }
    public int Max { get; set; }
    public QRange? Range { get; set; }
}

public class QOutcome
{
    public List<QScore> Scores { get; set; } = new();
    public QScore? Main { get; set; }
    public QScore? Second { get; set; }
    public string Headline { get; set; } = "";
    // режим quadrants: тип по двум шкалам
    public string TypeTitle { get; set; } = "";
    public string TypeText { get; set; } = "";
}

public class QHistoryItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Date { get; set; } = "";
    public string Headline { get; set; } = "";
    public List<string> Lines { get; set; } = new();
}

public class RatingFile
{
    public List<DiffInfo> DifficultyScale { get; set; } = new();
    public List<LevelInfo> Levels { get; set; } = new();
    public List<SectionUnlock> SectionUnlock { get; set; } = new();
}

public class DiffInfo
{
    public int D { get; set; }
    public string Name { get; set; } = "";
    public int QuestionRating { get; set; }
}

public class LevelInfo
{
    public int Level { get; set; }
    public string Name { get; set; } = "";
    public string Emoji { get; set; } = "";
    public int RatingFrom { get; set; }
    public int RatingTo { get; set; }
    public string About { get; set; } = "";
}

public class SectionUnlock
{
    public int Section { get; set; }
    public string Title { get; set; } = "";
    public int UnlockLevel { get; set; } = 1;
}

public class RefFile
{
    public List<RefEntry> Entries { get; set; } = new();
}

public class RefEntry
{
    public string Term { get; set; } = "";
    public string Short { get; set; } = "";
    public string Text { get; set; } = "";
}

public class ProgressData
{
    public bool Onboarded { get; set; }
    public int DailyGoalMin { get; set; } = 10;
    public string Role { get; set; } = "";   // ← вот эта строка новая
    public double Rating { get; set; } = 100;
    public int Answered { get; set; }
    public int Correct { get; set; }
    public List<QHistoryItem> History { get; set; } = new();

    public int TotalXp { get; set; }
    public int TodayXp { get; set; }
    public string XpDate { get; set; } = "";
    public int Streak { get; set; }
    public string LastActive { get; set; } = "";

    public List<string> Done { get; set; } = new();
    public List<string> Achievements { get; set; } = new();
    public int PerfectLessons { get; set; }

    // задания с ошибкой для «работы над ошибками»: ключ задания → дата ошибки
    public Dictionary<string, string> Mistakes { get; set; } = new();

    // карточки справочника: термин → коробка и дата следующего повтора
    public Dictionary<string, CardState> Cards { get; set; } = new();
}

public class CardState
{
    // коробка из прежней системы: нужна, только пока у карточки нет Interval
    public int Box { get; set; }
    public string Due { get; set; } = "";
    public int Interval { get; set; }
    public double Ease { get; set; } = Services.CardScheduler.StartEase;
}
