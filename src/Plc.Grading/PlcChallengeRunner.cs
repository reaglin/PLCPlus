using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Learning.Core;
using Plc.Dialects.Iec;
using Plc.Engine;
using Plc.Scenes;

namespace Plc.Grading;

// A challenge (spec 7.1) is graded by what the learner's program does, not by how it looks:
// each scenario resets the PLC and the scene, presses buttons at set times, and checks outputs.

public sealed class PlcChallenge
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>The task statement given to the learner.</summary>
    public string Brief { get; set; } = "";
    /// <summary>The scene the program is wired to (its ID).</summary>
    public string Scene { get; set; } = "";
    public ChallengeConstraints? Constraints { get; set; }
    public List<Scenario> Scenarios { get; set; } = [];
    /// <summary>The fraction of the points needed to pass, 0 to 1.</summary>
    public double PassThreshold { get; set; } = 1.0;
    /// <summary>Tiered hints, unlocked one by one after failed attempts.</summary>
    public List<string> Hints { get; set; } = [];
}

public sealed class ChallengeConstraints
{
    public List<string>? RequiredInstructions { get; set; }
    public List<string>? ForbiddenInstructions { get; set; }
    public int? MaxRungs { get; set; }
}

public sealed class Scenario
{
    public string Name { get; set; } = "";
    public double Points { get; set; } = 1;
    /// <summary>A hidden scenario is not shown to the learner, so a program cannot be fitted to the visible tests.</summary>
    public bool Hidden { get; set; }
    public double DurationSeconds { get; set; } = 1;
    public List<ScenarioEvent> Events { get; set; } = [];
    public List<Assertion> Assertions { get; set; } = [];
    public List<Invariant> Invariants { get; set; } = [];
}

/// <summary>At time T (seconds), operate a button or switch: active = pressed, released = not.</summary>
public sealed class ScenarioEvent
{
    public double T { get; set; }
    public string Operate { get; set; } = "";
    public bool Active { get; set; }
}

/// <summary>At time At (seconds), the tag must have the expected value.</summary>
public sealed class Assertion
{
    public double At { get; set; }
    public string Tag { get; set; } = "";
    public string Expect { get; set; } = "TRUE";
    /// <summary>What a learner reads when it fails, such as "The motor should be running 0.1 s after Start is pressed."</summary>
    public string? Says { get; set; }
}

/// <summary>Something that must never happen, checked after every scan.</summary>
public sealed class Invariant
{
    /// <summary>These BOOL tags must never all be TRUE at once ("the fill and drain valves are never both open").</summary>
    public List<string> NeverAllTrue { get; set; } = [];
    public string? Says { get; set; }
}

/// <summary>The PLC module's grader, behind the Learning Core's <see cref="IChallengeRunner"/> contract.</summary>
public sealed class PlcChallengeRunner(Func<string, string> loadSceneJson) : IChallengeRunner
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string Domain => "plc";

    public static PlcChallenge LoadChallenge(string json) =>
        JsonSerializer.Deserialize<PlcChallenge>(json, JsonOptions) ?? throw new JsonException("The challenge file is empty.");

    public ChallengeOutcome Run(string challengeJson, string workJson)
    {
        var challenge = LoadChallenge(challengeJson);
        var hash = WorkHash.Of(workJson);
        var max = challenge.Scenarios.Sum(s => s.Points);

        PlcProject project;
        try
        {
            project = ProjectJson.Deserialize(workJson);
        }
        catch (JsonException ex)
        {
            return NotRun(challenge, max, hash, [$"The program could not be read: {ex.Message}"]);
        }

        // 1. Compile and check against the constraints.
        var problems = ProjectValidator.Validate(project, IecDialect.Instance)
            .Where(d => d.Severity == Severity.Error).Select(d => d.ToString()).ToList();
        problems.AddRange(CheckConstraints(project, challenge.Constraints));
        if (problems.Count > 0) return NotRun(challenge, max, hash, problems);

        // 2. Each scenario, headless, from a clean reset.
        var sceneJson = loadSceneJson(challenge.Scene);
        var results = challenge.Scenarios.Select(s => RunScenario(project, Scene.Load(sceneJson), s)).ToList();
        var score = results.Sum(r => r.Points);
        return new ChallengeOutcome(challenge.Id, true, [], results, score, max,
            max > 0 && score / max >= challenge.PassThreshold - 1e-9, hash);
    }

    private static ChallengeOutcome NotRun(PlcChallenge c, double max, string hash, IReadOnlyList<string> problems) =>
        new(c.Id, false, problems, [], 0, max, false, hash);

    private static IEnumerable<string> CheckConstraints(PlcProject project, ChallengeConstraints? constraints)
    {
        if (constraints is null) yield break;
        var used = project.Programs.SelectMany(p => p.Rungs).SelectMany(r => Boxes(r.Logic))
            .Select(b => b.Instruction.ToUpperInvariant()).ToHashSet();
        foreach (var name in constraints.RequiredInstructions ?? [])
            if (!used.Contains(name.ToUpperInvariant()))
                yield return $"This challenge asks you to use {name.ToUpperInvariant()}.";
        foreach (var name in constraints.ForbiddenInstructions ?? [])
            if (used.Contains(name.ToUpperInvariant()))
                yield return $"This challenge asks you to solve it without {name.ToUpperInvariant()}.";
        var rungs = project.Programs.Sum(p => p.Rungs.Count);
        if (constraints.MaxRungs is { } maxRungs && rungs > maxRungs)
            yield return $"This challenge allows at most {maxRungs} rungs; your program has {rungs}.";
    }

    private static IEnumerable<BoxElement> Boxes(LadderElement e) => e switch
    {
        BoxElement b => [b],
        SeriesElement s => s.Items.SelectMany(Boxes),
        ParallelElement p => p.Branches.SelectMany(Boxes),
        _ => [],
    };

    private static ScenarioOutcome RunScenario(PlcProject project, Scene scene, Scenario scenario)
    {
        var runtime = new PlcRuntime(project, IecDialect.Instance, scene);
        var events = scenario.Events.OrderBy(e => e.T).ToList();
        var asserts = scenario.Assertions.OrderBy(a => a.At).ToList();
        var endMs = Ms(scenario.DurationSeconds);
        int nextEvent = 0, nextAssert = 0;
        string? failure = null;
        double? failedAt = null;

        while (runtime.NowMs <= endMs && failure is null)
        {
            var t = runtime.NowMs;
            while (nextEvent < events.Count && Ms(events[nextEvent].T) <= t)
            {
                var ev = events[nextEvent++];
                scene.Operate(ev.Operate, ev.Active);
            }

            runtime.Scan();
            if (runtime.State == RunState.Faulted)
            {
                failure = $"The PLC stopped with a fault: {runtime.Fault!.Message}";
                failedAt = t / 1000.0;
                break;
            }

            foreach (var inv in scenario.Invariants)
            {
                if (inv.NeverAllTrue.Count > 0 && inv.NeverAllTrue.All(tag => runtime.Read(tag).AsBool))
                {
                    failure = inv.Says ?? $"{string.Join(" and ", inv.NeverAllTrue)} must never all be on at once.";
                    failedAt = t / 1000.0;
                    break;
                }
            }

            while (failure is null && nextAssert < asserts.Count && Ms(asserts[nextAssert].At) <= t)
            {
                var a = asserts[nextAssert++];
                var actual = runtime.Read(a.Tag);
                if (!PlcValue.TryParseLiteral(a.Expect, out var expected) || !Same(actual, expected))
                {
                    failure = (a.Says ?? $"{a.Tag} should be {a.Expect}.") + $" At {Seconds(a.At)} s it was {actual}.";
                    failedAt = a.At;
                }
            }
        }
        if (failure is null && nextAssert < asserts.Count)
        {
            failure = $"The scenario ended before the check at {Seconds(asserts[nextAssert].At)} s.";
            failedAt = asserts[nextAssert].At;
        }

        var passed = failure is null;
        return new ScenarioOutcome(
            scenario.Name, scenario.Hidden, passed, passed ? scenario.Points : 0, scenario.Points,
            scenario.Hidden ? (passed ? null : "A hidden test failed. Think about what else the machine might do.") : failure,
            scenario.Hidden ? null : failedAt);
    }

    private static bool Same(PlcValue actual, PlcValue expected) =>
        expected.Type == DataType.Real || actual.Type == DataType.Real
            ? Math.Abs(actual.AsReal - expected.AsReal) < 1e-6
            : actual.AsLong == expected.AsLong;

    private static long Ms(double seconds) => (long)Math.Round(seconds * 1000);
    private static string Seconds(double s) => s.ToString("0.###", CultureInfo.InvariantCulture);
}
