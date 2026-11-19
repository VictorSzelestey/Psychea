using PsyPath.Models;
using PsyPath.UI;
using SkiaSharp;

var cases = new (string Name, Action Test)[]
{
    ("wrong answer costs half a heart and rerolls", WrongAnswer),
    ("deadline wins over a click and costs a full heart", Timeout),
    ("four timeouts end the run", Defeat),
    ("perfect rooms heal, carry hearts and cap overheal", PerfectRun),
    ("regular arrows drain one quarter and flash for one second", Arrows),
    ("regular false answer adds a unique distractor", FalseAnswer),
    ("regular cipher alters exactly two cards at three seconds", RoomCipher),
    ("cleaver removes two seconds and stacks with a regular trap", Cleaver),
    ("boss cipher lasts three turns and blocks room cipher", BossCipher),
    ("dart adds two distractors and blocks room traps of either kind", Dart),
    ("shaman summons a trap independently of the room roll", Shaman),
    ("both room transitions and all warrior clips decode from the packaged ZIPs", DecodeClips),
    ("admin hints identify the correct card even when cursed", HintIdentity)
};

foreach (var (name, test) in cases)
{
    test();
    Console.WriteLine($"PASS {name}");
}
Console.WriteLine($"{cases.Length} dungeon scenarios passed.");

static void WrongAnswer()
{
    var run = Begin();
    int prior = Correct(run);
    int wrong = run.Turn!.Answers.First(a => a.Id != prior).Id;
    Expect(run.Answer(wrong, 1), "The wrong choice must resolve the turn.");
    Expect(run.Outcome == DungeonOutcome.Wrong && run.HalfHearts == 7, "A wrong choice costs half a heart.");
    run.Advance(1.63);
    Expect(run.Phase == DungeonPhase.Question && Correct(run) != prior, "Reroll must change the question.");
    Expect(run.RoomCorrect == 0, "An error must not advance the room.");
}

static void Timeout()
{
    var run = Begin();
    int prior = Correct(run);
    Expect(!run.Answer(prior, 15), "A tap at the exact deadline must be rejected.");
    Expect(run.Outcome == DungeonOutcome.Timeout && run.HalfHearts == 6, "A timeout costs one whole heart.");
    run.Advance(15.63);
    Expect(Correct(run) != prior, "Timeout must reroll.");
}

static void Defeat()
{
    var run = Begin();
    run.Advance(65);
    Expect(run.Phase == DungeonPhase.Defeat && run.HalfHearts == 0, "Four missed questions must end the run.");
    Expect(!run.Start(66) && !run.Answer(Correct(run), 66), "The terminal run accepts no second start or answer.");
}

static void PerfectRun()
{
    var run = Begin();
    double now = 0;
    Expect(SequenceEqual(DungeonDefinition.Jungle.Rooms.Select(r => r.Seconds), 15, 11, 9), "Wrong room times.");
    Expect(DungeonDefinition.Jungle.Rooms.Select(r => r.TrapChance).SequenceEqual(new[] { 0.25, 0.35, 0.40 }),
        "Wrong room trap odds.");
    for (int room = 0; room < 3; room++)
    {
        ClearRoom(run, ref now);
        Expect(run.HalfHearts == Math.Min(9 + room, 10), "A perfect room heals half a heart.");
        if (room < 2)
        {
            Expect(run.Phase == DungeonPhase.Passage && run.RoomIndex == room, "Each intermission keeps the old room until finished.");
            now += DungeonRun.PassageSeconds + 0.02;
            run.Advance(now);
            Expect(run.RoomIndex == room + 1 && run.RoomCorrect == 0, "A passage starts the next room.");
        }
    }
    Expect(run.Phase == DungeonPhase.Victory && run.Overheal, "The third perfect room must show overheal at the cap.");
    Expect(run.CompletionTime > TimeSpan.Zero && run.CompletionTime.TotalSeconds < now, "Completion time stops at the final answer.");
}

static void Arrows()
{
    var run = Begin(new ScriptedRandom([0], [(int)DungeonTrap.Arrows]));
    run.Advance(0.64);
    Expect(run.Flashes.Count == 0, "Trap must wait for its trigger.");
    run.Advance(0.65);
    Expect(Near(run.TimeFraction(0.65), (15 - 0.65) * 0.75 / 15), "Arrows must remove 25% of remaining time.");
    Expect(run.Flashes.Single().Trap == DungeonTrap.Arrows, "Arrows must publish a time-drain flash.");
    run.Advance(1.64);
    Expect(run.Flashes.Count == 1, "Flash must last one second.");
    run.Advance(1.651);
    Expect(run.Flashes.Count == 0, "Flash must go away after one second.");
}

static void FalseAnswer()
{
    var run = Begin(new ScriptedRandom([0], [(int)DungeonTrap.FalseAnswer]));
    var initial = run.Turn!.Answers.ToArray();
    run.Advance(0.65);
    var added = run.Turn.Answers.Except(initial).Single();
    Expect(added.Id != Correct(run) && run.Turn.Answers.Select(a => a.Id).Distinct().Count() == 4, "Distractors must be unique.");
    Expect(run.Flashes.Single().Trap == DungeonTrap.FalseAnswer, "A false answer must publish its flash.");
}

static void RoomCipher()
{
    var run = Begin(new ScriptedRandom([0], [(int)DungeonTrap.Cipher]));
    run.Advance(2.999);
    Expect(Cursed(run) == 0, "Room curse must wait three seconds.");
    run.Advance(3);
    Expect(Cursed(run) == 2 && run.Turn!.Answers.Count == 3, "Room curse affects exactly two existing cards.");
    Expect(run.Flashes.Single().Trap == DungeonTrap.Cipher, "A curse must publish its flash.");
    Expect(run.Answer(Correct(run), 3.1), "Cipher must preserve answer identity.");
}

static void Cleaver()
{
    var random = new ScriptedRandom();
    var (run, start) = StartBoss(random, [(int)DungeonAttack.Cleaver, (int)DungeonTrap.Arrows]);
    double impact = start + 1.74;
    run.Advance(impact - 0.01);
    double before = run.TimeFraction(impact);
    run.Advance(impact + 0.001);
    Expect(run.Attack?.Attack == DungeonAttack.Cleaver && Near(before - run.TimeFraction(impact), 2d / 9),
        "The boss strike subtracts two seconds after the independent room trap.");
}

static void BossCipher()
{
    var random = new ScriptedRandom();
    var (run, now) = StartBoss(random, [(int)DungeonAttack.CipherSpell, (int)DungeonTrap.Cipher]);
    for (int turn = 0; turn < 4; turn++)
    {
        double start = run.Turn!.StartedAt;
        run.Advance(start + 1.99);
        Expect(Cursed(run) == 0, "Spell must wait two seconds into the turn.");
        run.Advance(start + 2.01);
        Expect(Cursed(run) == (turn < 3 ? 2 : 0), "Spell must affect three questions including the triggering one.");
        run.Advance(start + 3.01);
        Expect(run.Flashes.Count == 0, "The room cipher must not apply while the boss spell is active.");
        now = start + 3.02;
        AnswerCorrect(run, ref now);
    }
    Expect(run.Phase == DungeonPhase.Victory, "Cursed answers must be playable.");
}

static void Dart()
{
    foreach (var trap in new[] { DungeonTrap.FalseAnswer, DungeonTrap.Cipher })
    {
        var random = new ScriptedRandom();
        var (run, start) = StartBoss(random, [(int)DungeonAttack.Dart, (int)trap]);
        run.Advance(start + 0.91);
        Expect(Cursed(run) == 0 && run.Turn!.Answers.Count == 3, "The regular false answer must be blocked.");
        run.Advance(start + 0.93);
        Expect(Cursed(run) == 2 && run.Turn!.Answers.Count == 5, "The dart must curse two cards and add two distractors.");
        Expect(run.Flashes.Count == 2, "Dart must publish both flashes at impact.");
        run.Advance(start + 3.01);
        Expect(Cursed(run) == 2 && run.Flashes.Count == 0, "The regular cipher must remain blocked.");
    }
}

static void Shaman()
{
    var random = new ScriptedRandom();
    var (run, start) = StartBoss(random, [(int)DungeonAttack.Shaman, (int)DungeonTrap.FalseAnswer, (int)DungeonTrap.FalseAnswer]);
    run.Advance(start + 0.45);
    Expect(run.Turn!.Answers.Count == 4, "The shaman must trigger a trap.");
    run.Advance(start + 0.66);
    Expect(run.Turn.Answers.Count == 5, "The room must still be able to trigger its own trap.");
}

static void DecodeClips()
{
    var durations = new (string Name, int Milliseconds)[]
    {
        ("transition_1", 2000), ("transition_2", 2000), ("cleaver", 2740),
        ("dart", 1870), ("cipher", 1600), ("shaman", 1000)
    };
    var size = new SKImageInfo(270, 480);
    using var bitmap = new SKBitmap(size);
    using var canvas = new SKCanvas(bitmap);
    foreach (var (name, milliseconds) in durations)
    {
        using var source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "dungeon", name + ".zip"));
        using var clip = DungeonClip.Decode(source, name, CancellationToken.None).GetAwaiter().GetResult();
        var seen = new HashSet<int>();
        for (int time = 0; time < milliseconds; time += 10)
        {
            int frame = clip.FrameAt(time / 1000d);
            if (seen.Add(frame)) clip.Draw(canvas, frame, size);
        }
        Expect(seen.SetEquals(Enumerable.Range(0, 25)), $"{name} must contain 25 drawable frames.");
        Expect(clip.FrameAt(milliseconds / 1000d + 0.01) == -1, $"{name} must stop after its recorded duration.");
    }
}

static void HintIdentity()
{
    var run = Begin(new ScriptedRandom([0], [(int)DungeonTrap.Cipher]));
    var turn = run.Turn!;
    int id = turn.Answers.Single(answer => turn.IsCorrect(answer.Id)).Id;
    run.Advance(3);
    Expect(turn.Answers.Single(answer => turn.IsCorrect(answer.Id)).Id == id, "An altered display label must not change the hint.");
    Expect(run.Answer(id, 3.1) && run.Outcome == DungeonOutcome.Correct, "Hints and scoring must share the same answer identity.");
}

static DungeonRun Begin(ScriptedRandom? random = null)
{
    var run = new DungeonRun(DungeonDefinition.Jungle, random ?? new ScriptedRandom());
    Expect(run.Phase == DungeonPhase.Briefing && run.HalfHearts == 8, "The run starts with four hearts at the briefing.");
    Expect(run.Start(0), "The run must start from the briefing.");
    return run;
}

static (DungeonRun Run, double StartedAt) StartBoss(ScriptedRandom random, int[] kinds)
{
    var run = Begin(random);
    double now = 0;
    ClearRoom(run, ref now);
    now += DungeonRun.PassageSeconds + 0.02;
    run.Advance(now);
    ClearRoom(run, ref now);
    random.Set([0, 0], kinds);
    now += DungeonRun.PassageSeconds + 0.02;
    run.Advance(now);
    Expect(run.RoomIndex == 2 && run.Attack != null, "Expected a boss attack in room three.");
    return (run, run.Turn!.StartedAt);
}

static void ClearRoom(DungeonRun run, ref double now)
{
    for (int i = 0; i < DungeonDefinition.CorrectPerRoom; i++)
    {
        now += 0.03;
        AnswerCorrect(run, ref now);
    }
}

static void AnswerCorrect(DungeonRun run, ref double now)
{
    Expect(run.Answer(Correct(run), now), "Correct answer must be accepted.");
    Expect(run.Outcome == DungeonOutcome.Correct, "Expected the correct outcome.");
    now += 0.63;
    run.Advance(now);
}

static int Correct(DungeonRun run) => int.Parse(run.Turn!.Prompt.Split(' ')[^1]);
static int Cursed(DungeonRun run) => run.Turn!.Answers.Count(answer => answer.Cursed);
static bool Near(double actual, double expected) => Math.Abs(actual - expected) < 0.00001;
static bool SequenceEqual(IEnumerable<double> actual, params double[] expected) => actual.SequenceEqual(expected);
static void Expect(bool condition, string detail)
{
    if (!condition) throw new InvalidOperationException(detail);
}

sealed class ScriptedRandom : Random
{
    readonly Queue<double> rolls = new();
    readonly Queue<int> kinds = new();

    public ScriptedRandom(double[]? rolls = null, int[]? kinds = null) : base(901)
    {
        Set(rolls ?? [], kinds ?? []);
    }

    public void Set(double[] values, int[] eventKinds)
    {
        foreach (var roll in values) rolls.Enqueue(roll);
        foreach (var kind in eventKinds) kinds.Enqueue(kind);
    }

    public override double NextDouble() => rolls.TryDequeue(out double value) ? value : 0.99;
    public override int Next(int maxValue) =>
        maxValue is 3 or 4 && kinds.TryDequeue(out int kind) ? kind : base.Next(maxValue);
}
