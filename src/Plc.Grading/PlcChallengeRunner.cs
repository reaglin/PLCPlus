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
    /// <summary>The hint shown when this test fails (a visible test only); otherwise the challenge's hints, in turn.</summary>
    public string? Hint { get; set; }
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

/// <summary>
/// At time At (seconds), the tag must have the expected value. The tag can be a PLC tag, or a probe of
/// the machine itself: <c>@tank.overflow</c>, <c>@door.position</c>, <c>@diverter.metal</c>.
/// </summary>
public sealed class Assertion
{
    public double At { get; set; }
    public string Tag { get; set; } = "";
    public string Expect { get; set; } = "TRUE";
    /// <summary>What a learner reads when it fails, such as "The motor should be running 0.1 s after Start is pressed."</summary>
    public string? Says { get; set; }
    /// <summary>For a REAL value (a tank level, a door position): how far from Expect still counts as right.</summary>
    public double Tolerance { get; set; }
}

/// <summary>Something that must never happen, checked after every scan.</summary>
public sealed class Invariant
{
    /// <summary>
    /// These BOOL tags (or probes) must never all be TRUE at once ("the fill and drain valves are never both
    /// open"). One entry means it must never be TRUE at all (<c>@tank.overflow</c>).
    /// </summary>
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
        var results = challenge.Scenarios.Select(s => RunScenario(project, Scene.Load(sceneJson), s, null)).ToList();
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

    /// <summary>One check a scenario made: when, what, what it expected, what it found.</summary>
    public sealed record CheckResult(long AtMs, string Tag, PlcValue Expected, PlcValue Actual, bool Passed, string? Says);

    /// <summary>
    /// Runs one scenario again and records every input and output after each scan, so a failed test can be
    /// replayed and shown (spec 7.2 step 3). The program must be one that ran (no validation errors).
    /// </summary>
    public (ScenarioOutcome Outcome, TraceRecorder Trace) Replay(string challengeJson, string workJson, int scenarioIndex)
    {
        var (outcome, trace, _, _) = ReplayFull(challengeJson, workJson, scenarioIndex);
        return (outcome, trace);
    }

    private (ScenarioOutcome Outcome, TraceRecorder Trace, Scene Scene, List<CheckResult> Checks) ReplayFull(
        string challengeJson, string workJson, int scenarioIndex)
    {
        var challenge = LoadChallenge(challengeJson);
        var project = ProjectJson.Deserialize(workJson);
        var scene = Scene.Load(loadSceneJson(challenge.Scene));
        TraceRecorder? trace = null;
        var checks = new List<CheckResult>();
        var outcome = RunScenario(project, scene, challenge.Scenarios[scenarioIndex], rt => trace = new TraceRecorder(rt), checks);
        return (outcome, trace!, scene, checks);
    }

    /// <summary>
    /// What happened in one scenario, in words a beginner reads (spec 7.2 step 3): the test's own actions
    /// ("Start pressed"), every change of an output ("Motor turned off"), and each check at its moment with
    /// what it expected and what it found (✓ or ✗). At most <paramref name="maxLines"/> lines, then "… n more".
    /// </summary>
    public IReadOnlyList<string> Narrate(string challengeJson, string workJson, int scenarioIndex, int maxLines = 14)
    {
        var challenge = LoadChallenge(challengeJson);
        var scenario = challenge.Scenarios[scenarioIndex];
        var project = ProjectJson.Deserialize(workJson);
        var (_, trace, scene, checks) = ReplayFull(challengeJson, workJson, scenarioIndex);
        var outputs = project.Tags.Where(t => t.Io == IoDirection.Output).Select(t => t.Name).ToList();

        var moments = new List<(long Ms, int Order, string Text)>();
        foreach (var e in scenario.Events)
            moments.Add((Ms(e.T), 0, ActionWords(scene, e.Operate, e.Active)));
        PlcValue[]? last = null;
        foreach (var f in trace.Frames)
        {
            for (var i = 0; i < trace.Tags.Count; i++)
            {
                if (!outputs.Contains(trace.Tags[i], StringComparer.OrdinalIgnoreCase)) continue;
                var now = f.Values[i];
                var before = last?[i];
                if (before is null ? now.AsBool : !before.Value.Equals(now))
                    moments.Add((f.TimeMs, 1, $"{trace.Tags[i]} turned {(now.AsBool ? "on" : "off")}"));
            }
            last = [.. f.Values];
        }
        foreach (var c in checks)
            moments.Add((c.AtMs, 2, $"the test checks: {Name(scene, c.Tag)} should be {Words(c.Expected)}; it was {Words(c.Actual)} {(c.Passed ? "✓" : "✗")}"));

        // A test stops at its first failure: nothing after the last scan that ran is shown.
        var lastMs = trace.Frames.Count > 0 ? trace.Frames[^1].TimeMs : 0;
        var lines = moments.Where(m => m.Ms <= lastMs).OrderBy(m => m.Ms).ThenBy(m => m.Order)
            .Select(m => $"{(m.Ms / 1000.0).ToString("0.00", CultureInfo.InvariantCulture)} s: {m.Text}").ToList();
        if (lines.Count > maxLines)
        {
            // Keep the end, where the failure is: the first lines, a gap, then the last ones.
            var more = lines.Count - maxLines;
            var head = maxLines / 2;
            lines = [.. lines.Take(head), $"… {more} more {(more == 1 ? "line" : "lines")} …", .. lines.Skip(lines.Count - (maxLines - head))];
        }
        return lines;
    }

    private static string ActionWords(Scene scene, string id, bool active)
    {
        var c = scene.Find(id);
        var label = c?.Label ?? id;
        return c switch
        {
            SelectorSwitch s => $"{label} switch turned to {(s.Positions.Count == 2 ? s.Positions[active ? 1 : 0] : active ? "On" : "Off")}",
            SafetyEye => active ? $"something stepped into the {label} beam" : $"the {label} beam is clear again",
            EmergencyStop => active ? $"{label} pressed (latched)" : $"{label} pulled out (released)",
            _ => $"{label} {(active ? "pressed" : "released")}",
        };
    }

    /// <summary>A tag, or a probe such as @tank.overflow, in words: "Tank overflow".</summary>
    private static string Name(Scene scene, string tag)
    {
        if (!tag.StartsWith('@')) return tag;
        var p = tag[1..];
        var dot = p.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0) return tag;
        var label = scene.Find(p[..dot])?.Label ?? p[..dot];
        return $"{label} {p[(dot + 1)..]}";
    }

    private static string Words(PlcValue v) => v.Type == DataType.Bool ? (v.AsBool ? "on" : "off") : v.ToString();

    /// <summary>Reads a PLC tag, or a probe of the machine itself (<c>@tank.overflow</c>).</summary>
    private static PlcValue ReadAny(PlcRuntime runtime, Scene scene, string tag) =>
        tag.StartsWith('@')
            ? scene.Probe(tag) ?? throw new InvalidOperationException($"The scene has nothing called {tag}.")
            : runtime.Read(tag);

    private static ScenarioOutcome RunScenario(PlcProject project, Scene scene, Scenario scenario,
        Action<PlcRuntime>? attach, List<CheckResult>? checks = null)
    {
        var runtime = new PlcRuntime(project, IecDialect.Instance, scene);
        attach?.Invoke(runtime);
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
                if (inv.NeverAllTrue.Count > 0 && inv.NeverAllTrue.All(tag => ReadAny(runtime, scene, tag).AsBool))
                {
                    failure = inv.Says ?? $"{string.Join(" and ", inv.NeverAllTrue.Select(x => Name(scene, x)))} must never all be on at once.";
                    failedAt = t / 1000.0;
                    break;
                }
            }

            while (failure is null && nextAssert < asserts.Count && Ms(asserts[nextAssert].At) <= t)
            {
                var a = asserts[nextAssert++];
                var actual = ReadAny(runtime, scene, a.Tag);
                var parsed = PlcValue.TryParseLiteral(a.Expect, out var expected);
                var ok = parsed && Same(actual, expected, a.Tolerance);
                checks?.Add(new CheckResult(Ms(a.At), a.Tag, expected, actual, ok, a.Says));
                if (!ok)
                {
                    failure = (a.Says ?? $"{Name(scene, a.Tag)} should be {a.Expect}.") + $" At {Seconds(a.At)} s it was {Words(actual)}.";
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

    private static bool Same(PlcValue actual, PlcValue expected, double tolerance = 1e-6) =>
        expected.Type == DataType.Real || actual.Type == DataType.Real
            ? Math.Abs(actual.AsReal - expected.AsReal) <= Math.Max(tolerance, 1e-6)
            : actual.AsLong == expected.AsLong;

    private static long Ms(double seconds) => (long)Math.Round(seconds * 1000);
    private static string Seconds(double s) => s.ToString("0.###", CultureInfo.InvariantCulture);
}
