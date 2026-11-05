using Learning.Core.Content;

namespace Learning.Core.Progress;

/// <summary>An outcome's state (spec 7.4): not started, in progress, mastered, or needs review.</summary>
public enum MasteryState { NotStarted, InProgress, Mastered, NeedsReview }

/// <summary>One outcome, its state, and the evidence behind it in words.</summary>
public sealed record ObjectiveProgress(Outcome Outcome, MasteryState State, IReadOnlyList<string> Evidence);

/// <summary>
/// Mastery per outcome (plan 5.6, spec 7.4). The evidence for an outcome is every item that lists it in
/// <c>objectives</c>, and every question that names it. The rule, for every outcome until the instructors ask
/// for others:
/// <list type="bullet">
/// <item><b>Mastered:</b> every linked item is complete, and the latest answer to every linked question was right.</item>
/// <item><b>Needs review:</b> the items are complete, but the latest answer to a linked question was wrong, or a
/// linked exercise was checked again after it was passed and failed. The work was done; something has slipped.</item>
/// <item><b>In progress:</b> some evidence, not all of it yet.</item>
/// <item><b>Not started:</b> nothing yet.</item>
/// </list>
/// </summary>
public static class Mastery
{
    public static IReadOnlyList<ObjectiveProgress> Of(ContentPackage package, LearningProgress progress, LearningModule module) =>
        [.. module.Outcomes.Select(o => Of(package, progress, o))];

    public static ObjectiveProgress Of(ContentPackage package, LearningProgress progress, Outcome outcome)
    {
        var items = package.AllItems.Where(i => i.Objectives.Contains(outcome.Id, StringComparer.OrdinalIgnoreCase)).ToList();
        var questions = package.AllItems
            .SelectMany(i => (i.Quiz?.Questions ?? []).Where(q => string.Equals(q.Objective, outcome.Id, StringComparison.OrdinalIgnoreCase)).Select(q => (Item: i, Question: q)))
            .ToList();
        if (items.Count == 0 && questions.Count == 0) return new ObjectiveProgress(outcome, MasteryState.NotStarted, ["Nothing in the path teaches it yet."]);

        var evidence = new List<string>();
        var itemsDone = items.All(i => progress.StateOf(i) == ItemState.Complete);
        var anyStarted = items.Any(i => progress.StateOf(i) != ItemState.NotStarted);
        foreach (var i in items)
            evidence.Add($"{i}: {(progress.StateOf(i) == ItemState.Complete ? "complete" : progress.StateOf(i) == ItemState.InProgress ? "started" : "not started")}");

        var slipped = false;
        foreach (var i in items.Where(i => i.Exercise is not null && progress.StateOf(i) == ItemState.Complete))
        {
            var last = progress.Attempts.LastOrDefault(a => a.ItemId.Equals(i.Id, StringComparison.OrdinalIgnoreCase));
            if (last is { Passed: false })
            {
                slipped = true;
                evidence.Add($"{i} passed before, but the latest check of it did not pass: open its Exercise tab and check it again");
            }
        }

        var unanswered = 0;
        foreach (var (item, q) in questions)
        {
            var last = progress.Attempts.LastOrDefault(a => a.ItemId.Equals(item.Id, StringComparison.OrdinalIgnoreCase) && a.Questions?.ContainsKey(q.Id) == true);
            if (last is null) { unanswered++; continue; }
            anyStarted = true;
            if (!last.Questions![q.Id])
            {
                slipped = true;
                var number = item.Quiz!.Questions.IndexOf(q) + 1;
                var prompt = q.Prompt.Replace("**", "", StringComparison.Ordinal);
                if (prompt.Length > 60) prompt = prompt[..57].TrimEnd() + "…";
                var teacher = items.FirstOrDefault(i => i.Kind == ItemKind.Lesson);
                evidence.Add($"question {number} of {item} (\"{prompt}\") was wrong when you last checked. " +
                             (teacher is null ? "" : $"Read {teacher} again, then ") + $"answer it again on {item.Number}'s Exercise tab");
            }
        }
        if (questions.Count > 0)
            evidence.Add(unanswered == 0 ? $"{questions.Count} question{(questions.Count == 1 ? "" : "s")} answered" : $"{unanswered} of {questions.Count} questions not answered yet");

        var state = !anyStarted ? MasteryState.NotStarted
            : itemsDone && slipped ? MasteryState.NeedsReview
            : itemsDone && unanswered == 0 && !slipped ? MasteryState.Mastered
            : MasteryState.InProgress;
        return new ObjectiveProgress(outcome, state, evidence);
    }
}
