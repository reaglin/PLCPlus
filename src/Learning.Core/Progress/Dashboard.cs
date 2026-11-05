using System.Globalization;
using Learning.Core.Content;

namespace Learning.Core.Progress;

/// <summary>
/// A module on the path map (spec 8.5). Prerequisites are recommended, not enforced (D15), so where the spec says "locked" a
/// module not yet recommended is <see cref="StartAfter"/>: it opens, and says what to finish first.
/// </summary>
public enum PathState { ComingLater, Outline, StartAfter, Available, InProgress, Complete, Mastered, NeedsReview }

public enum SubmissionState { NotReached, Ready, Generated }

/// <summary>One module on the path map: its state, how far, what to finish first, the time spent, and its outcomes.</summary>
public sealed record ModuleEntry(LearningModule Module, PathState State, int Complete, int Total,
    IReadOnlyList<LearningModule> RecommendedFirst, TimeSpan Time, IReadOnlyList<ObjectiveProgress> Outcomes);

/// <summary>A submission point (spec 9.1): a module check. Not reached until it is complete; then a report is ready to make.</summary>
public sealed record SubmissionPoint(LearningItem Check, SubmissionState State, string Detail, SubmissionRecord? Latest);

/// <summary>An outcome to revisit: the work was done, but a later answer or check went wrong.</summary>
public sealed record ReviewItem(LearningModule Module, ObjectiveProgress Outcome);

public sealed record DashboardTotals(int ItemsComplete, int Items, int ModulesComplete, int Modules, int OutcomesMastered, int Outcomes,
    TimeSpan Time, TimeSpan Today, int Sessions);

public sealed record DashboardSummary(IReadOnlyList<ModuleEntry> Modules, IReadOnlyList<SubmissionPoint> Submissions,
    IReadOnlyList<ReviewItem> ReviewSuggested, DashboardTotals Totals);

/// <summary>The learner's dashboard (plan 7.2–7.5, spec 8.5): worked out from the progress, never stored, so a merge or a content update keeps it right.</summary>
public static class Dashboard
{
    public static DashboardSummary Build(ContentPackage package, LearningProgress progress, DateTimeOffset now)
    {
        var modules = package.Modules.Select(m => Entry(package, progress, m)).ToList();
        var learnable = package.Modules.Where(LearningProgress.Learnable).ToList();

        var submissions = new List<SubmissionPoint>();
        foreach (var check in learnable.SelectMany(m => m.Items).Where(i => i.Kind == ItemKind.Assess))
        {
            var latest = progress.Submissions.Where(s => s.PointId.Equals(check.Id, StringComparison.OrdinalIgnoreCase)).MaxBy(s => s.Generated);
            var done = check.Covers.Count(c => package.FindItem(c) is { } i && progress.StateOf(i) == ItemState.Complete);
            var questionsPassed = check.Quiz is null || (progress.Items.TryGetValue(check.Id, out var own) && own.Completed is not null);
            var state = latest is not null ? SubmissionState.Generated
                : progress.StateOf(check) == ItemState.Complete ? SubmissionState.Ready : SubmissionState.NotReached;
            var detail = state switch
            {
                SubmissionState.Generated => $"Report made {latest!.Generated.ToLocalTime().ToString("ddd d MMM yyyy", CultureInfo.CurrentCulture)}",
                SubmissionState.Ready => "Complete: ready to hand in",
                _ => $"{done} of {check.Covers.Count} items done" + (check.Quiz is null ? "" : questionsPassed ? ", questions passed" : ", questions not passed yet"),
            };
            submissions.Add(new SubmissionPoint(check, state, detail, latest));
        }

        var review = modules.Where(e => LearningProgress.Learnable(e.Module))
            .SelectMany(e => e.Outcomes.Where(o => o.State == MasteryState.NeedsReview).Select(o => new ReviewItem(e.Module, o))).ToList();

        var items = learnable.SelectMany(m => m.Items).ToList();
        var outcomes = modules.Where(e => LearningProgress.Learnable(e.Module)).SelectMany(e => e.Outcomes).ToList();
        var today = now.ToLocalTime().Date;
        var totals = new DashboardTotals(
            items.Count(i => progress.StateOf(i) == ItemState.Complete), items.Count,
            modules.Count(e => LearningProgress.Learnable(e.Module) && e.State is PathState.Complete or PathState.Mastered or PathState.NeedsReview), learnable.Count,
            outcomes.Count(o => o.State == MasteryState.Mastered), outcomes.Count,
            progress.TimeOnTask(), progress.TimeOnTask(s => s.Start.ToLocalTime().Date == today), progress.Sessions.Count(s => s.ActiveSeconds >= 60));
        return new DashboardSummary(modules, submissions, review, totals);
    }

    public static ModuleEntry Entry(ContentPackage package, LearningProgress progress, LearningModule m)
    {
        var p = progress.Of(m);
        var time = TimeSpan.FromSeconds(m.Items.Sum(i => progress.TimeOn(i.Id).TotalSeconds));
        var outcomes = m.Status == ModuleStatus.ComingLater ? (IReadOnlyList<ObjectiveProgress>)[] : Mastery.Of(package, progress, m);
        var first = progress.MissingPrerequisites(package, m);
        PathState state;
        if (m.Status == ModuleStatus.ComingLater || m.Items.Count == 0) state = PathState.ComingLater;
        else if (m.Status == ModuleStatus.Outline) state = PathState.Outline;
        else if (p.State == ModuleState.Complete)
            state = outcomes.Any(o => o.State == MasteryState.NeedsReview) ? PathState.NeedsReview
                : outcomes.Count > 0 && outcomes.All(o => o.State == MasteryState.Mastered) ? PathState.Mastered
                : PathState.Complete;
        else if (p.State == ModuleState.InProgress) state = PathState.InProgress;
        else state = first.Count > 0 ? PathState.StartAfter : PathState.Available;
        return new ModuleEntry(m, state, p.Complete, p.Total, first, time, outcomes);
    }

    /// <summary>The mark beside a module: always with words, never colour alone (NFR-8).</summary>
    public static string Mark(PathState s) => s switch
    {
        PathState.ComingLater => "–",
        PathState.InProgress => "◐",
        PathState.Complete => "✓",
        PathState.Mastered => "★",
        PathState.NeedsReview => "↺",
        _ => "○",
    };

    public static string Words(ModuleEntry e) => e.State switch
    {
        PathState.ComingLater => "Coming later",
        PathState.Outline => "Outline: still being written",
        PathState.StartAfter => $"Not started; recommended after {string.Join(" and ", e.RecommendedFirst.Select(m => $"module {m.Number}"))}",
        PathState.Available => "Not started",
        PathState.InProgress => $"In progress: {e.Complete} of {e.Total} items complete",
        PathState.Complete => "Complete; not every outcome mastered yet (see Outcomes)",
        PathState.Mastered => "Mastered: complete, and every outcome mastered",
        PathState.NeedsReview => "Complete; review suggested",
        _ => "",
    };

    /// <summary>A length of time in words: "45 min", "2 h 05 min", "less than a minute".</summary>
    public static string Duration(TimeSpan t) =>
        t.TotalMinutes < 1 ? (t.TotalSeconds < 1 ? "none yet" : "less than a minute")
        : t.TotalHours < 1 ? $"{(int)t.TotalMinutes} min"
        : $"{(int)t.TotalHours} h {t.Minutes:00} min";
}
