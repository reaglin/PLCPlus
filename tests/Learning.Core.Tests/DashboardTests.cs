using Learning.Core;
using Learning.Core.Content;
using Learning.Core.Progress;
using Xunit;

namespace Learning.Core.Tests;

/// <summary>Plan 7.2–7.5: the dashboard (path map, submission points, review suggested, time on task).</summary>
public class DashboardTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly ContentPackage P = ContentPackage.Load(Repo.Foundations);

    private static ChallengeOutcome Questions(params (string Id, bool Right)[] answers) =>
        new("quiz", true, [], [.. answers.Select(a => new ScenarioOutcome(a.Id, false, a.Right, a.Right ? 1 : 0, 1, null, null))],
            answers.Count(a => a.Right), answers.Length, answers.Count(a => a.Right) >= 0.8 * answers.Length, "answers");

    private static void ReadModule3(LearningProgress progress)
    {
        foreach (var id in new[] { "plc.m3.relays", "plc.m3.ladder-diagrams", "plc.m3.seal-in", "plc.m3.interlocks" })
            progress.MarkRead(P.FindItem(id)!, T0);
    }

    private static readonly string[] Q = ["q1", "q2", "q3", "q4", "q5", "q6"];

    private static SubmissionPoint M3(DashboardSummary d) => d.Submissions.Single(s => s.Check.Id == "plc.m3.check");

    [Fact]
    public void A_new_learner_sees_every_module_and_nothing_done()
    {
        var d = Dashboard.Build(P, new LearningProgress(), T0);
        Assert.Equal(12, d.Modules.Count);
        Assert.Equal(PathState.Available, d.Modules[0].State);
        Assert.Equal(PathState.ComingLater, d.Modules.Single(e => e.Module.Id == "plc.m9").State);
        Assert.All(d.Submissions, s => Assert.Equal(SubmissionState.NotReached, s.State));
        Assert.Equal(["plc.m1.check", "plc.m2.check", "plc.m3.check", "plc.m4.check", "plc.m5.check", "plc.m6.check", "plc.m7.check", "plc.m8.check", "plc.m11.check"],
            d.Submissions.Select(s => s.Check.Id));
        Assert.Empty(d.ReviewSuggested);
        Assert.Equal(0, d.Totals.ItemsComplete);
        Assert.Equal(50, d.Totals.Items);       // the written modules only: 1-8 and 11
        Assert.Equal(9, d.Totals.Modules);
        Assert.Equal(TimeSpan.Zero, d.Totals.Time);
    }

    [Fact]
    public void A_module_with_every_question_right_is_mastered_and_its_check_is_ready()
    {
        var progress = new LearningProgress();
        ReadModule3(progress);
        progress.Record(P.FindItem("plc.m3.check")!, Questions([.. Q.Select(q => (q, true))]), T0);
        var d = Dashboard.Build(P, progress, T0);
        var m3 = d.Modules.Single(e => e.Module.Id == "plc.m3");
        Assert.Equal(PathState.Mastered, m3.State);
        Assert.Equal("★", Dashboard.Mark(m3.State));
        Assert.Equal(SubmissionState.Ready, M3(d).State);
        Assert.Equal(1, d.Totals.ModulesComplete);
        Assert.Equal(4, d.Totals.OutcomesMastered);
    }

    [Fact]
    public void A_wrong_answer_in_a_passed_check_makes_the_module_review_suggested_and_lists_the_outcome()
    {
        var progress = new LearningProgress();
        ReadModule3(progress);
        // 5 of 6 passes the check (80%), but q5 (outcome o4) was wrong.
        progress.Record(P.FindItem("plc.m3.check")!, Questions([.. Q.Select(q => (q, q != "q5"))]), T0);
        var d = Dashboard.Build(P, progress, T0);
        Assert.Equal(PathState.NeedsReview, d.Modules.Single(e => e.Module.Id == "plc.m3").State);
        var review = Assert.Single(d.ReviewSuggested);
        Assert.Equal("plc.m3.o4", review.Outcome.Outcome.Id);
        Assert.Contains(review.Outcome.Evidence, e => e.Contains("question 5", StringComparison.Ordinal));
    }

    [Fact]
    public void Started_but_not_finished_is_in_progress_and_the_check_says_how_far()
    {
        var progress = new LearningProgress();
        progress.MarkRead(P.FindItem("plc.m3.relays")!, T0);
        var d = Dashboard.Build(P, progress, T0);
        Assert.Equal(PathState.InProgress, d.Modules.Single(e => e.Module.Id == "plc.m3").State);
        Assert.Equal("1 of 4 items done, questions not passed yet", M3(d).Detail);
    }

    [Fact]
    public void A_report_made_shows_on_its_submission_point()
    {
        var progress = new LearningProgress();
        ReadModule3(progress);
        progress.Record(P.FindItem("plc.m3.check")!, Questions([.. Q.Select(q => (q, true))]), T0);
        progress.Submissions.Add(new SubmissionRecord("plc.m3.check", "r1", T0.AddHours(1), "hash"));
        Assert.Equal(SubmissionState.Generated, M3(Dashboard.Build(P, progress, T0)).State);
    }

    [Fact]
    public void Time_on_task_counts_active_time_per_item_and_per_module_and_today()
    {
        var progress = new LearningProgress();
        var yesterday = progress.StartSession(T0.AddDays(-1), "LAB-1");
        LearningProgress.AddActiveTime(yesterday, "plc.m3.relays", 600, T0.AddDays(-1));
        var now = progress.StartSession(T0, "LAB-1");
        LearningProgress.AddActiveTime(now, "plc.m3.relays", 300, T0);
        LearningProgress.AddActiveTime(now, "plc.m3.seal-in", 120, T0);
        LearningProgress.AddActiveTime(now, null, 60, T0);   // on a module page

        Assert.Equal(TimeSpan.FromSeconds(900), progress.TimeOn("plc.m3.relays"));
        var d = Dashboard.Build(P, progress, T0);
        Assert.Equal(TimeSpan.FromSeconds(1080), d.Totals.Time);
        Assert.Equal(TimeSpan.FromSeconds(1020), d.Modules.Single(e => e.Module.Id == "plc.m3").Time);
        Assert.Equal(2, d.Totals.Sessions);
        Assert.Equal("18 min", Dashboard.Duration(d.Totals.Time));
        Assert.Equal("2 h 05 min", Dashboard.Duration(TimeSpan.FromMinutes(125)));
    }

    [Fact]
    public void Merging_two_copies_counts_each_session_once()
    {
        var a = new LearningProgress();
        var s = a.StartSession(T0, "LAB-1");
        LearningProgress.AddActiveTime(s, "plc.m3.relays", 300, T0);
        var b = LearningProgress.FromJson(a.ToJson());          // a copy taken home
        LearningProgress.AddActiveTime(a.Sessions[0], "plc.m3.relays", 60, T0);   // the lab copy went on a little longer
        LearningProgress.AddActiveTime(b.StartSession(T0.AddHours(5), "HOME"), "plc.m3.seal-in", 200, T0.AddHours(5));
        var m = LearningProgress.Merge(a, b);
        Assert.Equal(2, m.Sessions.Count);
        Assert.Equal(TimeSpan.FromSeconds(560), m.TimeOnTask());
    }
}
