namespace PsyPath.Models;

public sealed record DungeonRoom(string Title, double Seconds, double TrapChance, int FirstQuestion);

public sealed record DungeonDefinition(string Id, string Title, IReadOnlyList<DungeonRoom> Rooms)
{
    public static DungeonDefinition Jungle { get; } = new("jungle-temple", "Храм джунглей",
        Array.AsReadOnly(new DungeonRoom[]
        {
            new("Вход в храм", 15, 0.25, 1),
            new("Лестница храма", 11, 0.35, 13),
            new("Зал тотема", 9, 0.40, 25)
        }));

    public const int QuestionsPerRoom = 12;
    public const int CorrectPerRoom = 4;
    public const int ExperienceReward = 50;
    public const int RatingReward = 100;
    public const int FirstClearCrystals = 25;
}

public enum DungeonPhase { Briefing, Question, Resolution, Passage, Defeat, Victory }
public enum DungeonOutcome { Correct, Wrong, Timeout }
public enum DungeonTrap { Arrows, Cipher, FalseAnswer }
public enum DungeonAttack { Cleaver, CipherSpell, Dart, Shaman }

public sealed record DungeonAnswer(int Id, string Text, bool Cursed = false);
public readonly record struct DungeonFlash(DungeonTrap Trap, double At);
public readonly record struct DungeonAttackCue(DungeonAttack Attack, double At);

public sealed class DungeonTurn
{
    readonly List<DungeonAnswer> answers;

    internal DungeonTurn(int id, List<DungeonAnswer> answers, double at, double seconds)
    {
        Id = id;
        this.answers = answers;
        StartedAt = at;
        Deadline = at + seconds;
    }

    internal int Id { get; }
    internal double Deadline { get; set; }
    internal double StartedAt { get; }
    internal bool Cursed { get; set; }
    public string Prompt => $"Тестовый вопрос {Id}";
    public IReadOnlyList<DungeonAnswer> Answers => answers;
    public int Revision { get; private set; }
    public bool IsCorrect(int answerId) => answerId == Id;

    internal void Insert(int index, DungeonAnswer answer)
    {
        answers.Insert(index, answer);
        Revision++;
    }

    internal void Set(int index, DungeonAnswer answer)
    {
        answers[index] = answer;
        Revision++;
    }
}
