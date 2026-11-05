using System.Text.RegularExpressions;

namespace Learning.Core.Content;

/// <summary>A live part of a lesson page, written on a line of its own as <c>[[name: argument]]</c> (spec 6.3).</summary>
public sealed record WidgetTag(string Name, string Argument, int Line);

public static partial class Widgets
{
    [GeneratedRegex(@"^\s*\[\[\s*([a-z][a-z0-9-]*)\s*:\s*(.*?)\s*\]\]\s*$")]
    private static partial Regex TagPattern();

    /// <summary>The widget written on this line, or null if the line is ordinary text.</summary>
    public static WidgetTag? On(string line, int number = 0) =>
        TagPattern().Match(line) is { Success: true } m ? new WidgetTag(m.Groups[1].Value, m.Groups[2].Value, number) : null;

    public static IEnumerable<WidgetTag> In(string markdown) =>
        markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Select((line, i) => On(line, i + 1)).OfType<WidgetTag>();
}

/// <summary>
/// Checks a package before it is published (plan 4.7): every ID unique, every reference to a module,
/// item, outcome, challenge, reference page and widget points at something that exists. Each problem is
/// one sentence naming the file, so an author can fix it without reading code.
/// </summary>
public sealed class ContentValidator
{
    /// <summary>Does the subject module have a challenge with this ID? (Null: not checked.)</summary>
    public Func<string, bool>? ChallengeExists { get; init; }

    /// <summary>Checks one widget for the subject module: null if it is fine, or what is wrong.</summary>
    public Func<WidgetTag, string?>? CheckWidget { get; init; }

    /// <summary>Checks a "find the fault" question's machine for the subject module: null if it is fine.</summary>
    public Func<Question, string?>? CheckFault { get; init; }

    public IReadOnlyList<string> Validate(ContentPackage package)
    {
        var problems = new List<string>();
        if (package.Id.Length == 0) problems.Add("manifest.json has no id.");
        if (package.Version.Length == 0) problems.Add("manifest.json has no version.");
        if (package.Modules.Count == 0) problems.Add("manifest.json lists no modules.");

        var ids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Unique(string id, string file)
        {
            if (!ids.TryAdd(id, file)) problems.Add($"{file}: the ID {id} is already used in {ids[id]}. Every ID must be different.");
        }

        var seen = new List<LearningModule>();
        foreach (var module in package.Modules)
        {
            Unique(module.Id, module.File);
            foreach (var o in module.Outcomes) Unique(o.Id, module.File);
            foreach (var p in module.Prerequisites)
            {
                if (package.FindModule(p) is null) problems.Add($"{module.File}: the prerequisite {p} is not a module in this package.");
                else if (!seen.Any(s => s.Id.Equals(p, StringComparison.OrdinalIgnoreCase)))
                    problems.Add($"{module.File}: the prerequisite {p} comes later in the path. A module's prerequisites must come before it.");
            }
            if (module.Status != ModuleStatus.ComingLater && module.Outcomes.Count == 0)
                problems.Add($"{module.File}: the module has no learning outcomes. Its first page shows them.");
            if (module.Status != ModuleStatus.ComingLater && module.Items.Count == 0)
                problems.Add($"{module.File}: the module has no items.");
            if (module.Items.Count(i => i.Kind == ItemKind.Assess) > 1)
                problems.Add($"{module.File}: the module has more than one module check (kind: assess). It should have one, at the end.");
            seen.Add(module);
        }

        var outcomes = package.Modules.SelectMany(m => m.Outcomes).Select(o => o.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in package.AllItems)
        {
            Unique(item.Id, item.File);
            var written = item.Module.Status is ModuleStatus.Ready or ModuleStatus.Draft;
            foreach (var o in item.Objectives)
                if (!outcomes.Contains(o)) problems.Add($"{item.File}: the objective {o} is not an outcome of any module.");
            if (item.BuildsOn is { } b && package.FindItem(b) is null)
                problems.Add($"{item.File}: builds-on {b} is not an item in this package.");
            foreach (var r in item.References)
                if (!package.References.ContainsKey(r)) problems.Add($"{item.File}: the reference page {r} is missing (reference/{r}.md).");
            foreach (var c in new[] { item.Example, item.Exercise }.OfType<string>())
                if (ChallengeExists is { } exists && !exists(c)) problems.Add($"{item.File}: the challenge {c} is not in the manifest's challenges.");

            if (item.Kind == ItemKind.Assess)
            {
                if (item.Covers.Count == 0) problems.Add($"{item.File}: a module check must say which items it covers (covers: [...]).");
                foreach (var c in item.Covers)
                {
                    var covered = package.FindItem(c);
                    if (covered is null) problems.Add($"{item.File}: covers {c}, which is not an item in this package.");
                    else if (covered.Kind == ItemKind.Assess) problems.Add($"{item.File}: covers {c}, which is another module check.");
                }
            }
            else if (written && item.Learn.Trim().Length == 0)
                problems.Add($"{item.File}: the Learn page is empty, but the module is marked {item.Module.Status.ToString().ToLowerInvariant()}.");

            foreach (var w in Widgets.In(item.Learn)) CheckOne(w, item.File);
            if (item.Quiz is { } quiz) CheckQuiz(item, quiz, outcomes, problems);
            if (item.Quiz is not null && item.Exercise is not null)
                problems.Add($"{item.File}: an item has an exercise or questions, not both (the Exercise tab shows one).");
        }

        foreach (var page in package.References.Values)
            foreach (var w in Widgets.In(page.Body)) CheckOne(w, $"reference/{page.Name}.md");

        void CheckOne(WidgetTag w, string file)
        {
            if (CheckWidget?.Invoke(w) is { } problem) problems.Add($"{file} (line {w.Line} after the settings): {problem}");
        }

        return problems;
    }

    private void CheckQuiz(LearningItem item, Quiz quiz, HashSet<string> outcomes, List<string> problems)
    {
        var file = item.QuizFile ?? item.File;
        if (quiz.Questions.Count == 0) problems.Add($"{file}: the quiz has no questions.");
        if (quiz.PassThreshold is <= 0 or > 1) problems.Add($"{file}: passThreshold must be between 0 and 1 (0.8 is 80 %).");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var q in quiz.Questions)
        {
            var at = $"{file}, question {(q.Id.Length > 0 ? q.Id : "(no id)")}";
            if (q.Id.Length == 0 || !ids.Add(q.Id)) problems.Add($"{at}: every question needs its own id.");
            if (q.Prompt.Trim().Length == 0) problems.Add($"{at}: the question has no prompt.");
            if (q.Objective is { } o && !outcomes.Contains(o)) problems.Add($"{at}: the objective {o} is not an outcome of any module.");
            switch (q.Kind)
            {
                case QuestionKind.Choice or QuestionKind.Fault:
                    if (q.Options.Count < 2) problems.Add($"{at}: a choice needs at least two options.");
                    if (q.Correct.Count != 1 || q.Correct[0] < 0 || q.Correct[0] >= q.Options.Count)
                        problems.Add($"{at}: give exactly one right option in correct (counting from 0).");
                    if (q.Kind == QuestionKind.Fault)
                    {
                        if (q.Machine is null || q.Fault is null) problems.Add($"{at}: a fault question needs a machine (a challenge ID) and a fault (such as Stop:open).");
                        else if (ChallengeExists is { } exists && !exists(q.Machine)) problems.Add($"{at}: the machine {q.Machine} is not in the manifest's challenges.");
                        else if (CheckFault?.Invoke(q) is { } fault) problems.Add($"{at}: {fault}");
                    }
                    break;
                case QuestionKind.Multiple:
                    if (q.Options.Count < 2 || q.Correct.Count == 0 || q.Correct.Any(c => c < 0 || c >= q.Options.Count))
                        problems.Add($"{at}: a multiple-answer question needs options and at least one right one in correct.");
                    break;
                case QuestionKind.Numeric:
                    if (q.Answer is null) problems.Add($"{at}: a numeric question needs its answer.");
                    if (q.Tolerance < 0) problems.Add($"{at}: the tolerance cannot be negative.");
                    break;
                case QuestionKind.TruthTable:
                    try { BooleanExpression.Parse(q.Expression ?? ""); }
                    catch (ContentException ex) { problems.Add($"{at}: {ex.Message}"); }
                    break;
                case QuestionKind.Match:
                    if (q.Pairs.Count < 2) problems.Add($"{at}: a match question needs at least two pairs.");
                    break;
                case QuestionKind.Text:
                    if (q.Base is { } b)
                    {
                        if (b is not (2 or 8 or 16)) problems.Add($"{at}: base can be 2, 8 or 16.");
                        if (q.Answer is not { } n || n < 0 || n != Math.Floor(n)) problems.Add($"{at}: a question with a base needs its answer as a whole number, 0 or more (written in decimal).");
                    }
                    else if (q.Answers.Count == 0 || q.Answers.Any(x => QuizGrader.Squeeze(x).Length == 0))
                        problems.Add($"{at}: a text question needs answers (every one it accepts), or a base and an answer.");
                    break;
            }
        }
    }
}
