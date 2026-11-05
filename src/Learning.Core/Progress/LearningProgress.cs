using System.Text.Json;
using System.Text.Json.Serialization;
using Learning.Core.Content;

namespace Learning.Core.Progress;

public enum ItemState { NotStarted, InProgress, Complete }

public enum ModuleState { NotStarted, InProgress, Complete, ComingLater }

/// <summary>A module's progress: how many of its items are complete.</summary>
public sealed record ModuleProgress(ModuleState State, int Complete, int Total);

/// <summary>One grading run of an exercise (spec 7.2 step 4).</summary>
public sealed record Attempt(string ItemId, string ChallengeId, DateTimeOffset At, double Score, double MaxScore, bool Passed, string WorkHash);

/// <summary>Something an instructor may want to see in the report, such as a module opened before its prerequisites.</summary>
public sealed record LearningEvent(DateTimeOffset At, string Kind, string Subject, string Detail);

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
    public const string OpenedEarly = "opened-early";

    public Dictionary<string, ItemRecord> Items { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<Attempt> Attempts { get; init; } = [];
    public List<LearningEvent> Events { get; init; } = [];

    public ItemState StateOf(LearningItem item)
    {
        if (item.Kind == ItemKind.Assess)
        {
            var covered = item.Covers.Select(id => Items.TryGetValue(id, out var r) ? r : null).ToList();
            if (covered.Count > 0 && covered.All(r => r?.Completed is not null)) return ItemState.Complete;
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
                Events.Add(new LearningEvent(now, OpenedEarly, item.Module.Id,
                    $"Started {item.Module} before finishing {string.Join(", ", missing.Select(m => m.ToString()))}."));
        }
        record.LastOpened = now;
    }

    /// <summary>The learner says they have read a lesson that has no exercise.</summary>
    public bool MarkRead(LearningItem item, DateTimeOffset now)
    {
        if (!item.CompletesByReading) return false;
        var record = Items.TryGetValue(item.Id, out var r) ? r : Items[item.Id] = new ItemRecord { FirstOpened = now, LastOpened = now };
        record.Completed ??= now;
        record.CompletedBy ??= "read";
        return true;
    }

    /// <summary>Records a grading run of the item's exercise; a pass completes the item.</summary>
    public void Record(LearningItem item, ChallengeOutcome outcome, DateTimeOffset now)
    {
        Attempts.Add(new Attempt(item.Id, outcome.ChallengeId, now, outcome.Score, outcome.MaxScore, outcome.Passed, outcome.WorkHash));
        var record = Items.TryGetValue(item.Id, out var r) ? r : Items[item.Id] = new ItemRecord { FirstOpened = now, LastOpened = now };
        if (outcome.Passed && record.Completed is null)
        {
            record.Completed = now;
            record.CompletedBy = "passed";
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

    // ---- The file (phase 6 moves this inside the learner's encrypted progress file) ---------------

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
