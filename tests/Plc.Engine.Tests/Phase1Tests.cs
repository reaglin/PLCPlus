using Plc.Dialects.Iec;
using Plc.Engine;
using static Plc.Engine.Tests.K;

namespace Plc.Engine.Tests;

public class ArrayAndStructTests
{
    private static PlcProject WithTypes(PlcProject p)
    {
        p.Types.Add(new StructType
        {
            Name = "PumpData",
            Members = [Tag("Running"), Tag("Speed", "REAL"), Tag("Starts", "INT")],
        });
        return p;
    }

    [Fact]
    public void ArrayElementsByLiteralAndByTag()
    {
        var p = Project([Tag("Values", "ARRAY[1..3] OF INT"), Tag("I", "INT", initial: "2")],
            S(Box("MOVE", null, ("IN", "7"), ("OUT", "Values[1]"))),
            S(Box("MOVE", null, ("IN", "9"), ("OUT", "Values[I]"))));
        var rt = Run(p);
        rt.Scan();
        Assert.Equal(7, rt.L("Values[1]"));
        Assert.Equal(9, rt.L("Values[2]"));
        Assert.Equal(0, rt.L("Values[3]"));
        Assert.Equal(9, rt.L("Values[I]"));
        Assert.Equal("Values[2]", rt.Resolve("values[i]".Replace("values", "Values", StringComparison.Ordinal)));
    }

    [Fact]
    public void IndexOutOfRangeFaultsAtTheRung()
    {
        var p = Project([Tag("Values", "ARRAY[0..3] OF INT"), Tag("I", "INT", initial: "4")],
            S(Box("MOVE", null, ("IN", "1"), ("OUT", "Values[I]"))));
        var rt = Run(p);
        rt.Scan();
        Assert.Equal(RunState.Faulted, rt.State);
        Assert.Equal(FaultKind.IndexOutOfRange, rt.Fault!.Kind);
        Assert.Contains("only has elements 0 to 3", rt.Fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StructureMembersAndArraysOfStructures()
    {
        var p = WithTypes(Project([Tag("Pump", "PumpData"), Tag("Pumps", "ARRAY[0..1] OF PumpData"), Tag("Go")],
            S(No("Go"), Coil("Pump.Running")),
            S(No("Pump.Running"), Box("MOVE", null, ("IN", "42.5"), ("OUT", "Pumps[1].Speed")))));
        var rt = Run(p);
        rt.Set("Go", true);
        rt.Scan();
        Assert.True(rt.B("Pump.Running"));
        Assert.Equal(42.5, rt.Read("Pumps[1].Speed").AsReal);
        Assert.Equal(DataType.Int, rt.TypeOf("Pumps[0].Starts"));
    }

    [Fact]
    public void ValidatorCatchesBadPaths()
    {
        var p = WithTypes(Project([Tag("Values", "ARRAY[0..3] OF INT"), Tag("Pump", "PumpData"), Tag("R", "REAL"), Tag("Q")],
            S(Box("MOVE", null, ("IN", "1"), ("OUT", "Values[9]")), Coil("Q")),
            S(No("Pump.Speedy"), Coil("Q")),
            S(Box("MOVE", null, ("IN", "1"), ("OUT", "Values[R]")), Coil("Q")),
            S(Box("MOVE", null, ("IN", "1"), ("OUT", "Values")), Coil("Q")),
            S(No("Q[1]"), Coil("Q"))));
        var errors = ProjectValidator.Validate(p, IecDialect.Instance).Where(d => d.Severity == Severity.Error).ToList();
        Assert.Contains(errors, e => e.Rung == 1 && e.Message.Contains("only has elements 0 to 3", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Rung == 2 && e.Message.Contains("no member called Speedy", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Rung == 3 && e.Message.Contains("whole-number tag", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Rung == 4 && e.Message.Contains("whole array", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Rung == 5 && e.Message.Contains("not an array", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("ARRAY[3..1] OF INT", "smaller than the first")]
    [InlineData("ARRAY[0..20000] OF INT", "too big")]
    [InlineData("ARRAY[0..3] OF TON", "must be BOOL")]
    [InlineData("WIDGET", "not a type this PLC knows")]
    public void ValidatorExplainsBadTypes(string type, string expected)
    {
        var errors = ProjectValidator.Validate(Project([Tag("X", type)]), IecDialect.Instance);
        Assert.Contains(errors, e => e.Severity == Severity.Error && e.Message.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void ArraysCannotBeWiredOrGivenAStartingValue()
    {
        var errors = ProjectValidator.Validate(Project([
            new TagDeclaration { Name = "A", Type = "ARRAY[0..1] OF BOOL", Io = IoDirection.Input },
            new TagDeclaration { Name = "B", Type = "ARRAY[0..1] OF INT", Initial = "3" }]), IecDialect.Instance);
        Assert.Contains(errors, e => e.Message.Contains("wired to the machine", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Message.Contains("start at zero", StringComparison.Ordinal));
    }

    [Fact]
    public void StructureThatContainsItselfIsRejected()
    {
        var p = Project([Tag("X", "Loop")]);
        p.Types.Add(new StructType { Name = "Loop", Members = [Tag("Inner", "Loop")] });
        var errors = ProjectValidator.Validate(p, IecDialect.Instance);
        Assert.Contains(errors, e => e.Message.Contains("contains itself", StringComparison.Ordinal));
    }

    [Fact]
    public void ArrayProjectsRoundTripThroughJson()
    {
        var p = WithTypes(Project([Tag("Pumps", "ARRAY[0..1] OF PumpData")]));
        var again = ProjectJson.Deserialize(ProjectJson.Serialize(p));
        Assert.Equal("PumpData", Assert.Single(again.Types).Name);
        Run(again).Scan();
    }
}

public class BreakpointTests
{
    [Fact]
    public void RunsToTheFirstScanInWhichTheRungPassesPower()
    {
        var p = Project([Tag("A"), Tag("T1", "TON"), Tag("Q")],
            S(Box("TON", "T1", ("PT", "T#250ms"))),
            S(No("T1.Q"), Coil("Q")));
        p.Programs[0].Rungs[0].Logic = S(Nc("Q"), Box("TON", "T1", ("PT", "T#250ms")));
        var rt = Run(p);
        rt.SetBreakpoint("Main", 2);
        Assert.True(rt.RunToBreakpoint(10_000));
        Assert.Equal(("Main", 2), rt.BreakpointHit);
        Assert.Equal(250, rt.LastScanStartMs);
        Assert.True(rt.B("Q"));
    }

    [Fact]
    public void StopsAtTheTimeLimitWhenTheRungNeverPasses()
    {
        var rt = Run(Project([Tag("A"), Tag("Q")], S(No("A"), Coil("Q"))));
        rt.SetBreakpoint("Main", 1);
        Assert.False(rt.RunToBreakpoint(500));
        Assert.Equal(500, rt.NowMs);
        rt.ClearBreakpoints();
        Assert.Empty(rt.Breakpoints);
    }
}

public class TraceTests
{
    [Fact]
    public void ChangeLogListsOnlyChanges()
    {
        var rt = Run(Project([Tag("A", io: IoDirection.Input), Tag("Q", io: IoDirection.Output)], S(No("A"), Coil("Q"))));
        using var trace = new TraceRecorder(rt);
        rt.RunFor(30);
        rt.Force("A", PlcValue.True);
        rt.RunFor(20);
        Assert.Equal(new[] { "A", "Q" }, trace.Tags);
        Assert.Equal(5, trace.Frames.Count);
        Assert.Equal("t=0.000s  A=FALSE  Q=FALSE\nt=0.030s  A=TRUE  Q=TRUE\n", trace.ToChangeLog());
        Assert.Equal(PlcValue.False, trace.ValueAt("Q", 25));
        Assert.Equal(PlcValue.True, trace.ValueAt("Q", 30));
    }

    [Fact]
    public void ReplayOfAFailedScenarioShowsWhatHappened()
    {
        var runner = new Plc.Grading.PlcChallengeRunner(id => Content("scenes", id + ".json"));
        var (outcome, trace) = runner.Replay(Content("challenges", "motor-start-stop.json"), Content("programs", "motor-no-seal-in.json"), 0);
        Assert.False(outcome.Passed);
        Assert.Equal(PlcValue.True, trace.ValueAt("Motor", 100));
        Assert.Equal(PlcValue.False, trace.ValueAt("Motor", 1000));
        Assert.Contains("Motor=FALSE", trace.ToChangeLog(), StringComparison.Ordinal);
    }

    [Fact]
    public void NarrationIsInPlainWords()
    {
        var runner = new Plc.Grading.PlcChallengeRunner(id => Content("scenes", id + ".json"));
        var lines = runner.Narrate(Content("challenges", "motor-start-stop.json"), Content("programs", "motor-no-seal-in.json"), 0);
        Assert.Equal(
        [
            "0.00 s: Start pressed",
            "0.00 s: Motor turned on",
            "0.00 s: RunLight turned on",
            "0.10 s: the test checks: Motor should be on; it was on ✓",
            "0.20 s: Start released",
            "0.20 s: Motor turned off",
            "0.20 s: RunLight turned off",
            "1.00 s: the test checks: Motor should be on; it was off ✗",
        ], lines);
        var cut = runner.Narrate(Content("challenges", "motor-start-stop.json"), Content("programs", "motor-no-seal-in.json"), 0, maxLines: 3);
        Assert.Equal("… 5 more lines", cut[^1]);
    }
}
