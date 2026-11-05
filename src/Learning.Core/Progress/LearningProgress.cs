using System.Text.Json;
using System.Text.Json.Serialization;
using Learning.Core.Content;

namespace Learning.Core.Progress;

public enum ItemState { NotStarted, InProgress, Complete }

public enum ModuleState { NotStarted, InProgress, Complete, ComingLater }

/// <summary>A module's progress: how many of its items are complete.</summary>
public sealed record ModuleProgress(ModuleState State, int Complete, int Total);

/// <summary>
/// One grading run of an exercise or a quiz (spec 7.2 step 4): the score, the program's fingerprint, how many hints the
/// learner had been shown, the computer it ran on, and for a quiz which questions were right.
/// </summary>
public sealed record Attempt(string ItemId, string ChallengeId, DateTimeOffset At, double Score, double MaxScore, bool Passed, string WorkHash,
    int HintsShown = 0, string? Machine = null, Dictionary<string, bool>? Questions = null, string? Id = null)
{
    /// <summary>The attempt's unique ID, so two copies of a progress file merge without losing or doubling one (spec 8.3).
    /// Attempts saved before IDs existed get one made from what they hold.</summary>
    [JsonIgnore] public string Key => Id ?? $"{ItemId}|{At.UtcTicks}|{WorkHash}";
}

/// <summary>
/// One entry in the learner's event log (spec 8.4), in the shape of an xAPI statement: the actor is the learner who owns
/// the file, <see cref="Kind"/> is the verb (see <see cref="EventKinds"/>), <see cref="Subject"/> the object (an item,
/// module or the file itself) and <see cref="Detail"/> the result in words. Kept for the report and for later analysis.
/// </summary>
public sealed record LearningEvent(DateTimeOffset At, string Kind, string Subject, string Detail, string? Id = null)
{
    [JsonIgnore] public string Key => Id ?? $"{Kind}|{Subject}|{At.UtcTicks}";
}

/// <summary>The verbs of the event log.</summary>
public static class EventKinds
{
    public const string SignedIn = "signed-in";
    public const string SignedOut = "signed-out";
    public const string Locked = "locked";
    public const string Opened = "opened";
    public const string Read = "read";
    public const string Attempted = "attempted";
    public const string Completed = "completed";
    public const string OpenedEarly = "opened-early";
    public const string BackedUp = "backed-up";
    public const string Merged = "merged";
    public const string PinChanged = "pin-changed";
    public const string PinReset = "pin-reset";
    public const string Created = "created";
}

/// <summary>
/// One sitting with the program (spec 8.5, time on task): when it began and last saw activity, on which computer, and the
/// time the learner was active, in all and per item. Time counts only while the learner is using the program (the program
/// says how), not while it sits idle. Sessions have IDs, so two copies of a file merge without counting a minute twice.
/// </summary>
public sealed class StudySession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public string? Computer { get; set; }
    public double ActiveSeconds { get; set; }
    /// <summary>Active seconds per item ID (time on the start page or a module page counts in <see cref="ActiveSeconds"/> only).</summary>
    public Dictionary<string, double> Items { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>A report made at a submission point (spec 8.4, SubmissionRecord). Phase 8 makes them; the dashboard shows them.</summary>
public sealed record SubmissionRecord(string PointId, string ReportId, DateTimeOffset Generated, string ReportHash);

public sealed class ItemRecord
{
    public DateTimeOffset FirstOpened { get; set; }
    public DateTimeOffset LastOpened { get; set; }
    public DateTimeOffset? Completed { get; set; }
    /// <summary>"read" or "passed".</summary>
    public string? CompletedBy { get; set; }
}

/// <summary>
/// One learner's progress through a package. The rules are subject-neutral: a lesson with an exercise
/// is complete when the exercise passes; one without is complete when the learner says they have read
/// it; a module check is complete when every item it covers is. Records key off stable item IDs, so a
/// content update keeps a learner's progress (spec 6.3).
///
/// Phase 4 keeps it in a plain JSON file for one learner. Phase 6 puts the same record inside each
/// learner's PIN-protected progress file.
/// </summary>
public sealed class LearningProgress
{
    public const string OpenedEarly = EventKinds.OpenedEarly;

    public Dictionary<string, ItemRecord> Items { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<Attempt> Attempts { get; init; } = [];
    public List<LearningEvent> Events { get; init; } = [];
    public List<StudySession> Sessions { get; init; } = [];
    public List<SubmissionRecord> Submissions { get; init; } = [];

    /// <summary>A new sitting begins (signing in).</summary>
    public StudySession StartSession(DateTimeOffset now, string? computer)
    {
        var s = new StudySession { Start = now, End = now, Computer = computer };
        Sessions.Add(s);
        return s;
    }

    /// <summary>The learner was active for this long, on this item (or on no item: the start page, a module page).</summary>
    public static void AddActiveTime(StudySession session, string? itemId, double seconds, DateTimeOffset now)
    {
        if (seconds <= 0) return;
        session.ActiveSeconds += seconds;
        session.End = now;
        if (itemId is not null) session.Items[itemId] = session.Items.GetValueOrDefault(itemId) + seconds;
    }

    /// <summary>Active time in all, or on one item, or since a moment (today).</summary>
    public TimeSpan TimeOnTask(Func<StudySession, bool>? which = null) =>
        TimeSpan.FromSeconds(Sessions.Where(s => which?.Invoke(s) ?? true).Sum(s => s.ActiveSeconds));

    public TimeSpan TimeOn(string itemId) => TimeSpan.FromSeconds(Sessions.Sum(s => s.Items.GetValueOrDefault(itemId)));

    /// <summary>When the learner last did something that counts (read, checked, answered): a backup made before this is out of date.</summary>
    public DateTimeOffset? LastChanged { get; set; }

    /// <summary>Adds an entry to the event log.</summary>
    public void Log(string kind, string subject, string detail, DateTimeOffset now) =>
        Events.Add(new LearningEvent(now, kind, subject, detail, Guid.NewGuid().ToString("N")));

    public ItemState StateOf(LearningItem item)
    {
        if (item.Kind == ItemKind.Assess)
        {
            // Complete when every item it covers is, and its own questions (if it has any) have been passed.
            var covered = item.Covers.Select(id => Items.TryGetValue(id, out var r) ? r : null).ToList();
            var ownPassed = item.Quiz is null || (Items.TryGetValue(item.Id, out var own) && own.Completed is not null);
            if (covered.Count > 0 && covered.All(r => r?.Completed is not null) && ownPassed) return ItemState.Complete;
            return Items.ContainsKey(item.Id) || covered.Any(r => r is not null) ? ItemState.InProgress : ItemState.NotStarted;
        }
        if (!Items.TryGetValue(item.Id, out var record)) return ItemState.NotStarted;
        return record.Completed is null ? ItemState.InProgress : ItemState.Complete;
    }

    public ModuleProgress Of(LearningModule module)
    {
        if (module.Status == ModuleStatus.ComingLater || module.Items.Count == 0) return new ModuleProgress(ModuleState.ComingLater, 0, 0);
        var states = module.Items.Select(StateOf).ToList();
        var done = states.Count(s => s == ItemState.Complete);
        var state = done == states.Count ? ModuleState.Complete
            : states.Any(s => s != ItemState.NotStarted) ? ModuleState.InProgress
            : ModuleState.NotStarted;
        return new ModuleProgress(state, done, states.Count);
    }

    /// <summary>
    /// The prerequisites of a module that are not complete yet (recommended, not enforced: Ron, question 6).
    /// A module that cannot be completed yet (an outline, or one coming later) is not counted.
    /// </summary>
    public IReadOnlyList<LearningModule> MissingPrerequisites(ContentPackage package, LearningModule module) =>
        [.. module.Prerequisites.Select(package.FindModule).OfType<LearningModule>()
            .Where(m => Learnable(m) && Of(m).State != ModuleState.Complete)];

    /// <summary>A module a learner can work through now: written (ready or a draft), not an outline or still to come.</summary>
    public static bool Learnable(LearningModule m) => m.Status is ModuleStatus.Ready or ModuleStatus.Draft;

    /// <summary>
    /// The learner opened an item. The first time an item of a module is opened before the module's
    /// prerequisites are complete, that is recorded, so the report can show the instructor.
    /// </summary>
    public void Opened(ContentPackage package, LearningItem item, DateTimeOffset now)
    {
        if (!Items.TryGetValue(item.Id, out var record))
        {
            var firstInModule = !item.Module.Items.Any(i => Items.ContainsKey(i.Id));
            Items[item.Id] = record = new ItemRecord { FirstOpened = now };
            var missing = MissingPrerequisites(package, item.Module);
            if (firstInModule && missing.Count > 0)
                Log(OpenedEarly, item.Module.Id, $"Started {item.Module} before finishing {string.Join(", ", missing.Select(m => m.ToString()))}.", now);
        }
        record.LastOpened = now;
    }

    /// <summary>The learner says they have read a lesson that has no exercise.</summary>
    public bool MarkRead(LearningItem item, DateTimeOffset now)
    {
        if (!item.CompletesByReading) return false;
        var record = Items.TryGetValue(item.Id, out var r) ? r : Items[item.Id] = new ItemRecord { FirstOpened = now, LastOpened = now };
        if (record.Completed is null)
        {
            record.Completed = now;
            record.CompletedBy = "read";
            LastChanged = now;
            Log(EventKinds.Read, item.Id, $"Read {item}.", now);
        }
        return true;
    }

    /// <summary>
    /// Records a grading run of the item's exercise or quiz; a pass completes the item (a module check's pass is
    /// kept, and the check is complete once everything it covers is too).
    /// </summary>
    public void Record(LearningItem item, ChallengeOutcome outcome, DateTimeOffset now, int hintsShown = 0, string? machine = null)
    {
        var questions = item.Quiz is null ? null : outcome.Scenarios.ToDictionary(s => s.Name, s => s.Passed, StringComparer.OrdinalIgnoreCase);
        Attempts.Add(new Attempt(item.Id, outcome.ChallengeId, now, outcome.Score, outcome.MaxScore, outcome.Passed, outcome.WorkHash,
            hintsShown, machine, questions, Guid.NewGuid().ToString("N")));
        LastChanged = now;
        var record = Items.TryGetValue(item.Id, out var r) ? r : Items[item.Id] = new ItemRecord { FirstOpened = now, LastOpened = now };
        Log(EventKinds.Attempted, item.Id, $"{(outcome.Passed ? "Passed" : "Did not pass")}: {outcome.Score:0.#} of {outcome.MaxScore:0.#}.", now);
        if (outcome.Passed && record.Completed is null)
        {
            record.Completed = now;
            record.CompletedBy = "passed";
            Log(EventKinds.Completed, item.Id, $"Completed {item}.", now);
        }
    }

    /// <summary>The best score so far on an item's exercise, or null if it was never checked.</summary>
    public Attempt? Best(LearningItem item) =>
        Attempts.Where(a => a.ItemId.Equals(item.Id, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.Passed).ThenByDescending(a => a.MaxScore <= 0 ? 0 : a.Score / a.MaxScore).FirstOrDefault();

    /// <summary>The first item not complete, in path order, in the modules a learner can work through now.</summary>
    public LearningItem? Next(ContentPackage package) =>
        package.Modules.Where(Learnable).SelectMany(m => m.Items).FirstOrDefault(i => StateOf(i) != ItemState.Complete);

    /// <summary>
    /// What comes after this item: the next one not complete after it in the path (in a module a learner can work
    /// through), or, after the last, the first not complete from the start.
    /// </summary>
    public LearningItem? NextAfter(ContentPackage package, LearningItem item)
    {
        var all = package.Modules.Where(Learnable).SelectMany(m => m.Items).ToList();
        var at = all.IndexOf(item);
        return all.Skip(at + 1).Concat(all.Take(Math.Max(at, 0)))
            .FirstOrDefault(i => i != item && StateOf(i) != ItemState.Complete);
    }

    /// <summary>Continue where I left off: the item last opened if it is not complete, else what comes after it.</summary>
    public LearningItem? Continue(ContentPackage package)
    {
        var last = Items.Where(kv => package.FindItem(kv.Key) is not null)
            .OrderByDescending(kv => kv.Value.LastOpened).Select(kv => package.FindItem(kv.Key)!).FirstOrDefault();
        if (last is null || !Learnable(last.Module)) return Next(package);
        return StateOf(last) != ItemState.Complete ? last : NextAfter(package, last);
    }

    /// <summary>
    /// Two copies of one learner's progress, combined (spec 8.3): every attempt and event from both (each kept once, by its
    /// ID), each item's earliest opening and completion and latest visit. Mastery is worked out from these, so it follows.
    /// Nothing is lost.
    /// </summary>
    public static LearningProgress Merge(LearningProgress a, LearningProgress b)
    {
        var merged = new LearningProgress { LastChanged = Max(a.LastChanged, b.LastChanged) };
        foreach (var (id, r) in a.Items.Concat(b.Items))
        {
            if (!merged.Items.TryGetValue(id, out var m))
            {
                merged.Items[id] = new ItemRecord { FirstOpened = r.FirstOpened, LastOpened = r.LastOpened, Completed = r.Completed, CompletedBy = r.CompletedBy };
                continue;
            }
            if (r.FirstOpened < m.FirstOpened) m.FirstOpened = r.FirstOpened;
            if (r.LastOpened > m.LastOpened) m.LastOpened = r.LastOpened;
            if (r.Completed is { } c && (m.Completed is null || c < m.Completed)) (m.Completed, m.CompletedBy) = (c, r.CompletedBy);
        }
        merged.Attempts.AddRange(a.Attempts.Concat(b.Attempts).DistinctBy(x => x.Key).OrderBy(x => x.At));
        // A session in both copies: the longer is the later save of it.
        merged.Sessions.AddRange(a.Sessions.Concat(b.Sessions).GroupBy(s => s.Id).Select(g => g.MaxBy(s => s.ActiveSeconds)!).OrderBy(s => s.Start));
        merged.Submissions.AddRange(a.Submissions.Concat(b.Submissions).DistinctBy(x => x.ReportId).OrderBy(x => x.Generated));
        merged.Events.AddRange(a.Events.Concat(b.Events).DistinctBy(x => x.Key).OrderBy(x => x.At));
        return merged;
    }

    private static DateTimeOffset? Max(DateTimeOffset? x, DateTimeOffset? y) => x is null ? y : y is null ? x : x > y ? x : y;

    // ---- The preview's plain JSON file (phase 4); learners' files are Learning.Core.Learners.LearnerFile ----------------

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // readable words in the file, not escape codes
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static LearningProgress FromJson(string json) => JsonSerializer.Deserialize<LearningProgress>(json, Json) ?? new LearningProgress();

    /// <summary>Reads the file, or starts afresh if there is none. A damaged file is kept beside it (.damaged) and progress starts afresh.</summary>
    public static LearningProgress Load(string path) => Load(path, out _);

    /// <summary>As <see cref="Load(string)"/>, and says what went wrong when the file was damaged, so the learner can be told.</summary>
    public static LearningProgress Load(string path, out string? problem)
    {
        problem = null;
        if (!File.Exists(path)) return new LearningProgress();
        try
        {
            return FromJson(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            File.Copy(path, path + ".damaged", overwrite: true);
            problem = $"The progress file was damaged, so PLC+ starts afresh. The damaged file is kept as {path}.damaged.";
            return new LearningProgress();
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, ToJson());
        File.Move(temp, path, overwrite: true);
    }
}
