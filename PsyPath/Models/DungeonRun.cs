namespace PsyPath.Models;

public sealed class DungeonRun(DungeonDefinition dungeon, Random random)
{
    public const double PassageSeconds = 2;
    const double ResolutionSeconds = 0.62;
    const int MaxHalfHearts = 10;
    const double SpellDelay = 2;

    readonly List<ScheduledEffect> effects = new(3);
    readonly List<DungeonFlash> flashes = new(3);
    int turnNumber, cipherThroughTurn, lastQuestion;
    bool roomPerfect = true;
    double startedAt, continuesAt;

    readonly record struct ScheduledEffect(double At, DungeonTrap? Trap = null, DungeonAttack? Attack = null);

    public DungeonPhase Phase { get; private set; } = DungeonPhase.Briefing;
    public int RoomIndex { get; private set; }
    public DungeonRoom Room => dungeon.Rooms[RoomIndex];
    public int HalfHearts { get; private set; } = 8;
    public bool Overheal { get; private set; }
    public int RoomCorrect { get; private set; }
    public DungeonTurn? Turn { get; private set; }
    public DungeonOutcome Outcome { get; private set; }
    public int? Selected { get; private set; }
    public DungeonAttackCue? Attack { get; private set; }
    public IReadOnlyList<DungeonFlash> Flashes => flashes;
    public TimeSpan CompletionTime { get; private set; }

    public bool Start(double now)
    {
        if (Phase != DungeonPhase.Briefing) return false;
        startedAt = now;
        NextQuestion(now);
        return true;
    }

    public double TimeFraction(double now) => Turn is { } turn
        ? Math.Clamp((turn.Deadline - (Phase == DungeonPhase.Question ? now : continuesAt - ResolutionSeconds)) / Room.Seconds, 0, 1)
        : 0;

    public double PassageFraction(double now) => Math.Clamp(1 - (continuesAt - now) / PassageSeconds, 0, 1);

    public double ResolutionFraction(double now) => Math.Clamp(1 - (continuesAt - now) / ResolutionSeconds, 0, 1);

    public bool Answer(int id, double now)
    {
        var turn = Turn;
        Advance(now);
        if (Phase != DungeonPhase.Question || turn != Turn || turn?.Answers.Any(a => a.Id == id) != true)
            return false;

        Resolve(turn.IsCorrect(id) ? DungeonOutcome.Correct : DungeonOutcome.Wrong, id, now);
        return true;
    }

    public void Advance(double now)
    {
        while (true)
        {
            if (Phase == DungeonPhase.Question)
            {
                var turn = Turn!;
                if (effects.Count != 0 && effects[0].At <= now && effects[0].At < turn.Deadline)
                {
                    var effect = effects[0];
                    effects.RemoveAt(0);
                    if (effect.Attack is { } attack) ApplyAttack(attack, effect.At);
                    else ApplyTrap(effect.Trap!.Value, effect.At);
                    continue;
                }
                if (now < turn.Deadline) break;
                Resolve(DungeonOutcome.Timeout, null, turn.Deadline);
            }
            else if (Phase == DungeonPhase.Resolution && now >= continuesAt)
            {
                FinishResolution(continuesAt);
            }
            else if (Phase == DungeonPhase.Passage && now >= continuesAt)
            {
                RoomIndex++;
                RoomCorrect = 0;
                roomPerfect = true;
                NextQuestion(continuesAt);
            }
            else break;
        }
        flashes.RemoveAll(flash => now - flash.At >= 1);
    }

    internal static double ImpactOffset(DungeonAttack attack) => attack switch
    {
        DungeonAttack.Cleaver => 1.74,
        DungeonAttack.CipherSpell => 1.20,
        DungeonAttack.Dart => 0.92,
        _ => 0.44
    };

    void NextQuestion(double at)
    {
        var pool = Enumerable.Range(Room.FirstQuestion, DungeonDefinition.QuestionsPerRoom).ToArray();
        var questions = pool.Where(id => id != lastQuestion).ToArray();
        int id = lastQuestion = questions[random.Next(questions.Length)];
        var distractors = pool.Where(candidate => candidate != id).ToArray();
        random.Shuffle(distractors);
        DungeonAnswer[] answers = [NewAnswer(id), NewAnswer(distractors[0]), NewAnswer(distractors[1])];
        random.Shuffle(answers);
        Turn = new DungeonTurn(id, answers.ToList(), at, Room.Seconds);
        Phase = DungeonPhase.Question;
        Selected = null;
        turnNumber++;
        effects.Clear();

        // A boss roll has priority over the room and any restrictions apply before either effect lands.
        DungeonAttack? attack = RoomIndex == dungeon.Rooms.Count - 1 && random.NextDouble() < 0.45
            ? (DungeonAttack)random.Next(4) : null;
        Attack = null;
        if (attack is { } kind)
        {
            double windup = ImpactOffset(kind);
            double impact = kind == DungeonAttack.CipherSpell ? SpellDelay : windup;
            Attack = new DungeonAttackCue(kind, at + impact - windup);
            effects.Add(new ScheduledEffect(at + impact, Attack: kind));
        }
        bool carryCipher = turnNumber <= cipherThroughTurn;
        if (carryCipher)
            effects.Add(new ScheduledEffect(at + SpellDelay, DungeonTrap.Cipher));

        if (random.NextDouble() < Room.TrapChance)
        {
            var trap = (DungeonTrap)random.Next(3);
            bool blocked = (trap == DungeonTrap.Cipher &&
                (carryCipher || attack is DungeonAttack.CipherSpell or DungeonAttack.Dart)) ||
                (trap == DungeonTrap.FalseAnswer && attack == DungeonAttack.Dart);
            if (!blocked)
                effects.Add(new ScheduledEffect(at + (trap == DungeonTrap.Cipher ? 3 : 0.65), trap));
        }
        // Preserve attack-first order at an identical timestamp.
        var ordered = effects.OrderBy(effect => effect.At).ToArray();
        effects.Clear();
        effects.AddRange(ordered);
    }

    void ApplyAttack(DungeonAttack attack, double at)
    {
        switch (attack)
        {
            case DungeonAttack.Cleaver:
                Turn!.Deadline = Math.Max(at, Turn.Deadline - 2);
                Flash(DungeonTrap.Arrows, at);
                break;
            case DungeonAttack.CipherSpell:
                cipherThroughTurn = Math.Max(cipherThroughTurn, turnNumber + 2);
                ApplyTrap(DungeonTrap.Cipher, at);
                break;
            case DungeonAttack.Dart:
                ApplyTrap(DungeonTrap.Cipher, at);
                AddFalseAnswers(2, at);
                break;
            case DungeonAttack.Shaman:
                var trap = (DungeonTrap)random.Next(3);
                if (trap == DungeonTrap.Cipher)
                    effects.Add(new ScheduledEffect(Turn!.StartedAt + 3, trap));
                else
                    ApplyTrap(trap, at);
                break;
        }
    }

    void ApplyTrap(DungeonTrap trap, double at)
    {
        var turn = Turn!;
        switch (trap)
        {
            case DungeonTrap.Arrows:
                turn.Deadline = at + (turn.Deadline - at) * 0.75;
                Flash(trap, at);
                break;
            case DungeonTrap.FalseAnswer:
                AddFalseAnswers(1, at);
                break;
            case DungeonTrap.Cipher:
                if (turn.Cursed) return;
                turn.Cursed = true;
                var positions = Enumerable.Range(0, turn.Answers.Count).ToArray();
                random.Shuffle(positions);
                foreach (int position in positions.Take(2))
                {
                    var answer = turn.Answers[position];
                    var letters = answer.Text.ToCharArray();
                    letters[random.Next(5)] = '◆';
                    turn.Set(position, answer with { Text = new string(letters), Cursed = true });
                }
                Flash(trap, at);
                break;
        }
    }

    void AddFalseAnswers(int count, double at)
    {
        var turn = Turn!;
        var pool = Enumerable.Range(Room.FirstQuestion, DungeonDefinition.QuestionsPerRoom)
            .Where(id => turn.Answers.All(answer => answer.Id != id)).ToArray();
        random.Shuffle(pool);
        foreach (int id in pool.Take(count))
            turn.Insert(random.Next(turn.Answers.Count + 1), NewAnswer(id));
        Flash(DungeonTrap.FalseAnswer, at);
    }

    void Flash(DungeonTrap trap, double at)
    {
        flashes.RemoveAll(flash => flash.Trap == trap);
        flashes.Add(new DungeonFlash(trap, at));
    }

    static DungeonAnswer NewAnswer(int id) => new(id, $"Ответ {id}");

    void Resolve(DungeonOutcome outcome, int? selected, double at)
    {
        Phase = DungeonPhase.Resolution;
        Outcome = outcome;
        Selected = selected;
        continuesAt = at + ResolutionSeconds;
        effects.Clear();
        if (outcome == DungeonOutcome.Correct)
        {
            RoomCorrect++;
            if (RoomCorrect != DungeonDefinition.CorrectPerRoom) return;
            if (roomPerfect)
            {
                Overheal = HalfHearts == MaxHalfHearts;
                HalfHearts = Math.Min(MaxHalfHearts, HalfHearts + 1);
            }
            if (RoomIndex == dungeon.Rooms.Count - 1)
                CompletionTime = TimeSpan.FromSeconds(at - startedAt);
        }
        else
        {
            roomPerfect = false;
            Overheal = false;
            HalfHearts = Math.Max(0, HalfHearts - (outcome == DungeonOutcome.Timeout ? 2 : 1));
        }
    }

    void FinishResolution(double at)
    {
        if (HalfHearts == 0)
        {
            Phase = DungeonPhase.Defeat;
            return;
        }
        if (RoomCorrect < DungeonDefinition.CorrectPerRoom)
        {
            NextQuestion(at);
            return;
        }
        if (RoomIndex == dungeon.Rooms.Count - 1)
        {
            Phase = DungeonPhase.Victory;
            return;
        }
        Phase = DungeonPhase.Passage;
        continuesAt = at + PassageSeconds;
        Turn = null;
        Attack = null;
    }
}
