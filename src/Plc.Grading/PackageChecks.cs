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
        return problems;
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
        if (fault is null) return $"the fault \"{q.Fault}\" cannot be read. Write it as Tag:open, Tag:shorted or Tag:swapped:OtherTag.";
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "manifest.json")));
        foreach (var c in doc.RootElement.GetProperty("challenges").EnumerateArray())
        {
            var challenge = PlcChallengeRunner.LoadChallenge(File.ReadAllText(Path.Combine(folder, c.GetProperty("file").GetString()!)));
            if (!challenge.Id.Equals(q.Machine, StringComparison.OrdinalIgnoreCase)) continue;
            var scene = Plc.Scenes.Scene.Load(File.ReadAllText(Path.Combine(folder, "scenes", challenge.Scene + ".json")));
            var inputs = scene.IoList.Where(p => p.Kind == Plc.Scenes.IoKind.Input).Select(p => p.Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var tag in new[] { fault.Tag, fault.Other }.OfType<string>())
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
            if (!result.TryAdd(challenge.Id, titles)) problems.Add($"{file}: the challenge ID {challenge.Id} is used twice.");
        }
        return result;
    }
}
