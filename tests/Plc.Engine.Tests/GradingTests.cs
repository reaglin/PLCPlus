using System.Text.Json;
using Plc.Engine;
using Plc.Grading;
using Plc.Scenes;
using static Plc.Engine.Tests.K;

namespace Plc.Engine.Tests;

public class SceneTests
{
    [Fact]
    public void MotorStationLoadsWithItsIoList()
    {
        var scene = Scene.Load(Content("scenes", "motor-station.json"));
        Assert.Equal(4, scene.Components.Count);
        Assert.Contains(scene.IoList, p => p.Tag == "Stop" && p.Kind == IoKind.Input && p.Meaning.Contains("NOT pressed", StringComparison.Ordinal));
        Assert.Equal(PlcValue.True, scene.ReadInput("Stop"));   // normally closed, not pressed
        scene.Operate("stop", true);
        Assert.Equal(PlcValue.False, scene.ReadInput("Stop"));
    }

    [Fact]
    public void MotorReachesSpeedOnTheSimulatedClock()
    {
        var motor = new Motor { RunTag = "M", RunningTag = "Fb", StartTimeMs = 100 };
        motor.WriteOutput("M", PlcValue.True);
        for (var i = 0; i < 9; i++) motor.Advance(10);
        Assert.False(motor.Running);
        motor.Advance(10);
        Assert.True(motor.Running);
        Assert.Equal(PlcValue.True, motor.ReadInput("Fb"));
    }

    [Fact]
    public void FailedMotorNeverTurns()
    {
        var motor = new Motor { RunTag = "M", Failed = true };
        motor.WriteOutput("M", PlcValue.True);
        motor.Advance(10_000);
        Assert.False(motor.Running);
    }

    [Fact]
    public void ReversingMotorRunsBackwardsAndSlowsDownToChangeDirection()
    {
        var motor = new Motor { RunTag = "Fwd", ReverseTag = "Rev", StartTimeMs = 100 };
        Assert.Equal(2, motor.Points.Count());
        motor.WriteOutput("Rev", PlcValue.True);
        motor.Advance(100);
        Assert.True(motor.Running);
        Assert.True(motor.Reversing);
        Assert.Equal(PlcValue.True, motor.Probe("reverse"));
        Assert.Contains("in reverse", motor.StateText, StringComparison.Ordinal);

        motor.WriteOutput("Rev", PlcValue.False);
        motor.WriteOutput("Fwd", PlcValue.True);
        motor.Advance(50);                       // half-way down to a stop, still turning backwards
        Assert.True(motor.Reversing);
        Assert.Contains("change direction", motor.StateText, StringComparison.Ordinal);
        motor.Advance(150);                      // stopped, then up to speed forwards
        Assert.True(motor.Running);
        Assert.False(motor.Reversing);
        Assert.False(motor.Conflict);
    }

    [Fact]
    public void ReversingMotorToldBothWaysShowsTheConflictAndDoesNotTurn()
    {
        var motor = new Motor { RunTag = "Fwd", ReverseTag = "Rev", StartTimeMs = 100 };
        motor.WriteOutput("Fwd", PlcValue.True);
        motor.WriteOutput("Rev", PlcValue.True);
        motor.Advance(1000);
        Assert.False(motor.Running);
        Assert.True(motor.Conflict);
        Assert.Equal(PlcValue.True, motor.Probe("conflict"));
        Assert.Contains("BOTH ways", motor.StateText, StringComparison.Ordinal);
    }

    [Fact]
    public void ThumbwheelReadsItsNumber_OrItsBcd()
    {
        var plain = new Thumbwheel { Tag = "Set", Digits = 2 };
        plain.Set(147);                                        // clamped to two digits
        Assert.Equal(99, plain.Value);
        Assert.Equal(99, plain.ReadInput("Set")!.Value.AsLong);
        Assert.Equal("INT", Assert.Single(plain.Points).Type);

        var bcd = new Thumbwheel { Tag = "Set", Digits = 3, Bcd = true };
        bcd.Set(259);
        Assert.Equal(0x259, bcd.ReadInput("Set")!.Value.AsLong);
        Assert.Contains("259", bcd.StateText, StringComparison.Ordinal);
    }

    [Fact]
    public void NumericDisplayShowsTheNumber_AndErrForABadBcdCode()
    {
        var d = new NumericDisplay { Tag = "Show", Digits = 4 };
        d.WriteOutput("Show", PlcValue.FromInt(42));
        Assert.Equal("42", d.Shown);
        d.WriteOutput("Show", PlcValue.FromInt(12345));
        Assert.Equal("----", d.Shown);

        var b = new NumericDisplay { Tag = "Show", Digits = 2, Bcd = true };
        b.WriteOutput("Show", PlcValue.FromInt(0x47));
        Assert.Equal("47", b.Shown);
        b.WriteOutput("Show", PlcValue.FromInt(0x4A));
        Assert.Equal("Err", b.Shown);
    }

    [Fact]
    public void SingleSolenoidCylinderExtendsAndSpringsBack()
    {
        var c = new Cylinder { ExtendTag = "Push", ExtendedTag = "Out", RetractedTag = "In", StrokeTimeMs = 500 };
        Assert.Equal(PlcValue.True, c.ReadInput("In"));
        c.WriteOutput("Push", PlcValue.True);
        c.Advance(250);
        Assert.Equal(PlcValue.False, c.ReadInput("In"));
        Assert.Equal(PlcValue.False, c.ReadInput("Out"));
        c.Advance(250);
        Assert.Equal(PlcValue.True, c.ReadInput("Out"));
        Assert.Equal(1, c.Strokes);
        c.WriteOutput("Push", PlcValue.False);
        c.Advance(500);
        Assert.Equal(PlcValue.True, c.ReadInput("In"));
    }

    [Fact]
    public void DoubleSolenoidCylinderHoldsWhereItIs_AndAStuckOneNeverMoves()
    {
        var c = new Cylinder { ExtendTag = "Ext", RetractTag = "Ret", StrokeTimeMs = 400 };
        c.WriteOutput("Ext", PlcValue.True);
        c.Advance(200);
        c.WriteOutput("Ext", PlcValue.False);
        c.Advance(1000);                                        // neither solenoid: it stays half out
        Assert.Equal(0.5, c.Position, 3);
        c.WriteOutput("Ext", PlcValue.True);
        c.WriteOutput("Ret", PlcValue.True);
        c.Advance(1000);                                        // both: it does not move
        Assert.Equal(0.5, c.Position, 3);
        Assert.Contains("both solenoids", c.StateText, StringComparison.Ordinal);

        var stuck = new Cylinder { ExtendTag = "Ext", StrokeTimeMs = 400, Stuck = true };
        stuck.WriteOutput("Ext", PlcValue.True);
        stuck.Advance(1000);
        Assert.Equal(0, stuck.Position);
        Assert.Contains("stuck", stuck.StateText, StringComparison.Ordinal);
    }

    [Fact]
    public void AFailedDeviceNeverGetsItsOutput()
    {
        var scene = new Scene();
        var motor = scene.Add(new Motor { Id = "m", RunTag = "Motor", StartTimeMs = 0 });
        scene.WiringFaults.Add(WiringFault.Parse("Motor:failed")!);
        scene.WriteOutput("Motor", PlcValue.True);
        scene.Advance(100);
        Assert.False(motor.Running);
        Assert.Equal(WiringFaultKind.Failed, WiringFault.Parse("Motor:FAILED")!.Kind);
    }

    [Fact]
    public void AScenarioCanSetAThumbwheel()
    {
        var scene = new Scene();
        var tw = scene.Add(new Thumbwheel { Id = "batch", Tag = "Batch", Digits = 2 });
        scene.SetValue("batch", 12);
        Assert.Equal(12, tw.Value);
        Assert.Throws<ArgumentException>(() => scene.SetValue("nothing", 1));
        var ev = System.Text.Json.JsonSerializer.Deserialize<ScenarioEvent>("""{ "t": 0, "operate": "batch", "value": 7 }""", Scene.JsonOptions)!;
        Assert.Equal(7, ev.Value);
    }

    [Fact]
    public void SealInProgramRunsTheRealScene()
    {
        var scene = Scene.Load(Content("scenes", "motor-station.json"));
        var rt = new PlcRuntime(ProjectJson.Deserialize(Content("programs", "motor-seal-in.json")), Plc.Dialects.Iec.IecDialect.Instance, scene);
        var motor = (Motor)scene.Find("m1")!;
        var light = (PilotLight)scene.Find("running")!;
        scene.Operate("start", true); rt.RunFor(100);
        scene.Operate("start", false); rt.RunFor(600);
        Assert.True(motor.Running);
        Assert.True(light.Lit);
        scene.Operate("stop", true); rt.RunFor(20);
        Assert.False(motor.Commanded);
    }
}

public class GradingTests
{
    private static readonly PlcChallengeRunner Runner = new(id => Content("scenes", id + ".json"));
    private static readonly string Challenge = Content("challenges", "motor-start-stop.json");

    [Fact]
    public void CorrectProgramPassesEveryScenario()
    {
        var outcome = Runner.Run(Challenge, Content("programs", "motor-seal-in.json"));
        Assert.True(outcome.Ran);
        Assert.True(outcome.Passed, string.Join("; ", outcome.Scenarios.Select(s => s.Failure)));
        Assert.Equal(3, outcome.Score);
        Assert.Equal(64, outcome.WorkHash.Length);
    }

    [Fact]
    public void WithoutSealInFailsWithTheAssertionAndItsTime()
    {
        var outcome = Runner.Run(Challenge, Content("programs", "motor-no-seal-in.json"));
        Assert.True(outcome.Ran);
        Assert.False(outcome.Passed);
        var first = outcome.Scenarios[0];
        Assert.False(first.Passed);
        Assert.Equal(1.0, first.FailedAtSeconds);
        Assert.Contains("keep running after Start is released", first.Failure, StringComparison.Ordinal);
        Assert.Contains("it was off", first.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public void HiddenScenarioDoesNotGiveItsDetailsAway()
    {
        // Start in parallel with everything: Stop does not win.
        var p = ProjectJson.Deserialize(Content("programs", "motor-seal-in.json"));
        p.Programs[0].Rungs[0].Logic = S(Par(No("Start"), S(No("Motor"), No("Stop"))), Coil("Motor"));
        var outcome = Runner.Run(Challenge, ProjectJson.Serialize(p));
        var hidden = Assert.Single(outcome.Scenarios, s => s.Hidden);
        Assert.False(hidden.Passed);
        Assert.Null(hidden.FailedAtSeconds);
        Assert.DoesNotContain("Stop held", hidden.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramWithErrorsIsNotRunAndSaysWhy()
    {
        var p = ProjectJson.Deserialize(Content("programs", "motor-seal-in.json"));
        p.Programs[0].Rungs[0].Logic = S(No("Strat"), Coil("Motor"));
        var outcome = Runner.Run(Challenge, ProjectJson.Serialize(p));
        Assert.False(outcome.Ran);
        Assert.Contains(outcome.Problems, m => m.Contains("'Strat'", StringComparison.Ordinal));
    }

    [Fact]
    public void ConstraintsAreChecked()
    {
        var p = ProjectJson.Deserialize(Content("programs", "motor-seal-in.json"));
        for (var i = 0; i < 4; i++) p.Programs[0].Rungs.Add(new Rung { Logic = S(No("Motor"), Coil("RunLight")) });
        var outcome = Runner.Run(Challenge, ProjectJson.Serialize(p));
        Assert.False(outcome.Ran);
        Assert.Contains(outcome.Problems, m => m.Contains("at most 4 rungs", StringComparison.Ordinal));
    }

    [Fact]
    public void GradingIsRepeatable()
    {
        var work = Content("programs", "motor-no-seal-in.json");
        Assert.Equal(Runner.Run(Challenge, work).Scenarios, Runner.Run(Challenge, work).Scenarios);
    }

    [Fact]
    public void InvariantViolationFails()
    {
        const string json = """
            { "id": "x", "scene": "motor-station", "scenarios": [ {
                "name": "light and motor never together", "durationSeconds": 0.5,
                "events": [ { "t": 0.2, "operate": "start", "active": true } ],
                "invariants": [ { "neverAllTrue": ["Motor", "RunLight"], "says": "Never both." } ] } ] }
            """;
        var outcome = Runner.Run(json, Content("programs", "motor-seal-in.json"));
        var s = Assert.Single(outcome.Scenarios);
        Assert.False(s.Passed);
        Assert.Equal("Never both.", s.Failure);
        Assert.Equal(0.2, s.FailedAtSeconds);
    }
}

/// <summary>Phase 5.2: timing windows ("on between 5.0 and 5.1 s") and ordering ("Yellow before Red").</summary>
public class TimingAndOrderTests
{
    private static readonly PlcChallengeRunner Runner = new(id => K.Content("scenes", id + ".json"));
    private static readonly string Working = K.Content("programs", "traffic-light.json");

    /// <summary>The traffic light's first scenario (Run on at 0 s) with only the given windows and orderings.</summary>
    private static string With(List<TimingWindow> windows, List<Ordering> order, double seconds = 13)
    {
        var c = PlcChallengeRunner.LoadChallenge(K.Content("challenges", "traffic-light.json"));
        var s = c.Scenarios[0];
        s.Assertions.Clear();
        s.Invariants.Clear();
        s.Windows = windows;
        s.Order = order;
        s.DurationSeconds = seconds;
        c.Scenarios = [s];
        return JsonSerializer.Serialize(c, PlcChallengeRunner.JsonOptions);
    }

    private static Learning.Core.ScenarioOutcome One(string challenge, string program) => Assert.Single(Runner.Run(challenge, program).Scenarios);

    [Fact]
    public void YellowComesOnInItsWindow() =>
        Assert.True(One(With([new TimingWindow { Tag = "Yellow", From = 4.9, To = 5.1 }], []), Working).Passed);

    [Fact]
    public void TooEarlyAndTooLateAreBothReported()
    {
        var early = One(With([new TimingWindow { Tag = "Yellow", From = 5.5, To = 6 }], []), Working);
        Assert.False(early.Passed);
        Assert.Contains("too early", early.Failure, StringComparison.Ordinal);

        var late = One(With([new TimingWindow { Tag = "Yellow", From = 3, To = 4 }], []), Working);
        Assert.False(late.Passed);
        Assert.Contains("By 4 s it had not", late.Failure, StringComparison.Ordinal);
        Assert.Equal(4.01, late.FailedAtSeconds!.Value, 2);
    }

    [Fact]
    public void YellowBeforeRedPasses_RedBeforeYellowFails()
    {
        Assert.True(One(With([], [new Ordering { First = "Yellow", Then = "Red" }]), Working).Passed);
        var wrong = One(With([], [new Ordering { First = "Red", Then = "Yellow" }]), Working);
        Assert.False(wrong.Passed);
        Assert.Contains("Yellow came on first", wrong.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOrderThatNeverHappensFailsAtTheEnd()
    {
        var never = One(With([], [new Ordering { First = "Green", Then = "Red" }], seconds: 3), Working);
        Assert.False(never.Passed);
        Assert.Contains("had not come on", never.Failure, StringComparison.Ordinal);
    }
}
