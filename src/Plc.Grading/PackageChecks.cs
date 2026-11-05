using System.Text.Json;
using Learning.Core.Content;
using Plc.Engine;

namespace Plc.Grading;

/// <summary>
/// The PLC half of checking a content package (plan 4.7): the challenges the manifest lists, the
/// programs the Example tabs name, and the widgets lessons use. The Learning Core's validator does
/// the rest; this tells it what a challenge and a PLC widget are.
/// </summary>
public static class PackageChecks
{
    /// <summary>The widgets a PLC+ lesson can use.</summary>
    public static readonly IReadOnlyList<string> WidgetNames = ["ladder", "truth-table"];

    /// <summary>Every problem in the package folder, Learning Core's and PLC+'s, one sentence each.</summary>
    public static IReadOnlyList<string> Validate(string folder)
    {
        ContentPackage package;
        try
        {
            package = ContentPackage.Load(folder);
        }
        catch (ContentException ex)
        {
            return [ex.Message];
        }

        var problems = new List<string>();
        var challenges = LoadChallenges(folder, problems);
        var validator = new ContentValidator
        {
            ChallengeExists = challenges.ContainsKey,
            CheckWidget = w => CheckWidget(folder, w),
            CheckFault = q => CheckFault(folder, q),
        };
        problems.AddRange(validator.Validate(package));

        foreach (var item in package.AllItems)
            if (item.Example is { } id && item.ExampleProgram is { } title && challenges.TryGetValue(id, out var programs) && !programs.Contains(title, StringComparer.OrdinalIgnoreCase))
                problems.Add($"{item.File}: example-program \"{title}\" is not one of the programs of {id} ({string.Join(", ", programs)}).");
        problems.AddRange(CheckAnswersStayHidden(folder, package));
        return problems;
    }

    /// <summary>
    /// D21: an exercise is the learner's to write, so nothing shows its working program before they have done it. An Example tab
    /// runs a machine like the exercise's, never an exercise's own; a ladder picture never draws an exercise's working program;
    /// a find-the-fault question (which runs the machine's working program) comes only after that machine's exercise.
    /// </summary>
    public static IEnumerable<string> CheckAnswersStayHidden(string folder, ContentPackage package)
    {
        var works = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "manifest.json"))))
            foreach (var c in doc.RootElement.GetProperty("challenges").EnumerateArray())
            {
                var path = Path.Combine(folder, c.GetProperty("file").GetString()!);
                if (!File.Exists(path)) continue;
                var first = c.GetProperty("programs").EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("file", out var f))
                    works[PlcChallengeRunner.LoadChallenge(File.ReadAllText(path)).Id] = f.GetString()!.Replace("\\", "/", StringComparison.Ordinal);
            }
        var items = package.AllItems.ToList();
        var exerciseAt = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < items.Count; i++)
            if (items[i].Exercise is { } e) exerciseAt.TryAdd(e, i);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.Example is { } ex && exerciseAt.TryGetValue(ex, out var at))
                yield return $"{item.File}: its Example runs {ex}, the exercise of {items[at].Number} {items[at].Title}, which shows that exercise's answer. Use a similar machine of its own (D21).";
            foreach (var w in Widgets.In(item.Learn).Where(w => w.Name == "ladder"))
                foreach (var (id, file) in works)
                    if (exerciseAt.TryGetValue(id, out var e) && string.Equals(w.Argument.Trim().Replace("\\", "/", StringComparison.Ordinal), file, StringComparison.OrdinalIgnoreCase))
                        yield return $"{item.File}: the ladder picture {w.Argument} is the answer to the exercise of {items[e].Number} {items[e].Title}.";
            if (item.Quiz is { } quiz)
                foreach (var q in quiz.Questions.Where(q => q.Machine is not null))
                    if (exerciseAt.TryGetValue(q.Machine!, out var e) && e > i)
                        yield return $"{item.File}: question {q.Id} runs {q.Machine}'s working program before its exercise ({items[e].Number}) is done.";
        }
    }

    /// <summary>Null if the widget is fine; otherwise what is wrong with it.</summary>
    public static string? CheckWidget(string folder, WidgetTag w)
    {
        switch (w.Name)
        {
            case "ladder":
                var path = Path.Combine(folder, w.Argument);
                if (!File.Exists(path)) return $"the ladder widget's program {w.Argument} is not in the package.";
                try
                {
                    ProjectJson.Deserialize(File.ReadAllText(path));
                    return null;
                }
                catch (JsonException ex)
                {
                    return $"the ladder widget's program {w.Argument} is not a program PLC+ can read: {ex.Message}";
                }
            case "truth-table":
                try
                {
                    BooleanExpression.Parse(w.Argument);
                    return null;
                }
                catch (ContentException ex)
                {
                    return ex.Message;
                }
            default:
                return $"PLC+ has no widget called {w.Name}. It has: {string.Join(", ", WidgetNames)}.";
        }
    }

    /// <summary>A "find the fault" question: its fault can be read, names an input of the machine, and its program exists.</summary>
    public static string? CheckFault(string folder, Learning.Core.Content.Question q)
    {
        var fault = Plc.Scenes.WiringFault.Parse(q.Fault);
        if (fault is null) return $"the fault \"{q.Fault}\" cannot be read. Write it as Tag:open, Tag:shorted, Tag:swapped:OtherTag or Tag:failed.";
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "manifest.json")));
        foreach (var c in doc.RootElement.GetProperty("challenges").EnumerateArray())
        {
            var challenge = PlcChallengeRunner.LoadChallenge(File.ReadAllText(Path.Combine(folder, c.GetProperty("file").GetString()!)));
            if (!challenge.Id.Equals(q.Machine, StringComparison.OrdinalIgnoreCase)) continue;
            var scene = Plc.Scenes.Scene.Load(File.ReadAllText(Path.Combine(folder, "scenes", challenge.Scene + ".json")));
            var inputs = scene.IoList.Where(p => p.Kind == Plc.Scenes.IoKind.Input).Select(p => p.Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var outputs = scene.IoList.Where(p => p.Kind == Plc.Scenes.IoKind.Output).Select(p => p.Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (fault.Kind == Plc.Scenes.WiringFaultKind.Failed)
            {
                if (!outputs.Contains(fault.Tag)) return $"the fault names {fault.Tag}, which is not an output of the {scene.Title} ({string.Join(", ", outputs)}).";
            }
            else foreach (var tag in new[] { fault.Tag, fault.Other }.OfType<string>())
                if (!inputs.Contains(tag)) return $"the fault names {tag}, which is not an input of the {scene.Title} ({string.Join(", ", inputs)}).";
            var titles = c.GetProperty("programs").EnumerateArray().Select(p => p.GetProperty("title").GetString()).ToList();
            if (q.Program is { } program && !titles.Contains(program, StringComparer.OrdinalIgnoreCase))
                return $"the program \"{program}\" is not one of {challenge.Id}'s ({string.Join(", ", titles)}).";
            return null;
        }
        return $"the machine {q.Machine} is not in the manifest's challenges.";
    }

    /// <summary>The manifest's challenges: ID → the titles of its programs.</summary>
    private static Dictionary<string, List<string>> LoadChallenges(string folder, List<string> problems)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "manifest.json")));
        if (!doc.RootElement.TryGetProperty("challenges", out var list)) return result;
        foreach (var c in list.EnumerateArray())
        {
            var file = c.GetProperty("file").GetString() ?? "";
            var path = Path.Combine(folder, file);
            if (!File.Exists(path)) { problems.Add($"manifest.json lists the challenge {file}, which is missing."); continue; }
            PlcChallenge challenge;
            try
            {
                challenge = PlcChallengeRunner.LoadChallenge(File.ReadAllText(path));
            }
            catch (JsonException ex)
            {
                problems.Add($"{file} is not a challenge PLC+ can read: {ex.Message}");
                continue;
            }
            var titles = new List<string>();
            foreach (var p in c.GetProperty("programs").EnumerateArray())
            {
                titles.Add(p.GetProperty("title").GetString() ?? "");
                var programFile = p.GetProperty("file").GetString() ?? "";
                if (!File.Exists(Path.Combine(folder, programFile))) problems.Add($"manifest.json: {challenge.Id}'s program {programFile} is missing.");
            }
            if (!File.Exists(Path.Combine(folder, "scenes", challenge.Scene + ".json"))) problems.Add($"{file}: its scene {challenge.Scene} is missing (scenes/{challenge.Scene}.json).");
            if (challenge.StartProgram is { } start && !File.Exists(Path.Combine(folder, start))) problems.Add($"{file}: its startProgram {start} is missing.");
            if (!result.TryAdd(challenge.Id, titles)) problems.Add($"{file}: the challenge ID {challenge.Id} is used twice.");
        }
        return result;
    }
}
