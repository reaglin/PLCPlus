using Plc.Dialects.Iec;
using Plc.Engine;
using static Plc.Engine.Tests.K;

namespace Plc.Engine.Tests;

public class ValueTests
{
    [Theory]
    [InlineData("T#2s", 2000)]
    [InlineData("T#500ms", 500)]
    [InlineData("T#1m30s", 90_000)]
    [InlineData("TIME#1.5s", 1500)]
    [InlineData("t#1h", 3_600_000)]
    public void TimeLiterals(string text, long ms)
    {
        Assert.True(PlcValue.TryParseLiteral(text, out var v));
        Assert.Equal(DataType.Time, v.Type);
        Assert.Equal(ms, v.I);
    }

    [Fact]
    public void OtherLiterals()
    {
        Assert.True(PlcValue.TryParseLiteral("TRUE", out var t) && t.AsBool);
        Assert.True(PlcValue.TryParseLiteral("-12", out var i) && i.Type == DataType.DInt && i.I == -12);
        Assert.True(PlcValue.TryParseLiteral("2.5", out var r) && r.Type == DataType.Real && r.R == 2.5);
        Assert.False(PlcValue.TryParseLiteral("Start", out _));
        Assert.False(PlcValue.TryParseLiteral("T#2x", out _));
    }

    [Fact]
    public void IntWrapsAt16Bits() => Assert.Equal(-32768, PlcValue.FromDInt(32767 + 1).ConvertTo(DataType.Int).I);

    [Fact]
    public void TimeFormats() => Assert.Equal("T#1m30s250ms", PlcValue.FormatTime(90_250));
}

public class ContactAndCoilTests
{
    private static readonly TagDeclaration[] Bools = [Tag("A"), Tag("B"), Tag("Q"), Tag("Q2")];

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void SeriesIsAnd(bool a, bool b, bool q)
    {
        var rt = Run(Project(Bools, S(No("A"), No("B"), Coil("Q"))));
        rt.Set("A", a); rt.Set("B", b); rt.Scan();
        Assert.Equal(q, rt.B("Q"));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void ParallelIsOr(bool a, bool b, bool q)
    {
        var rt = Run(Project(Bools, S(Par(No("A"), No("B")), Coil("Q"))));
        rt.Set("A", a); rt.Set("B", b); rt.Scan();
        Assert.Equal(q, rt.B("Q"));
    }

    [Fact]
    public void NormallyClosedAndNegatedCoil()
    {
        var rt = Run(Project(Bools, S(Nc("A"), Coil("Q")), S(No("A"), Coil("Q2", CoilKind.Negated))));
        rt.Scan();
        Assert.True(rt.B("Q"));
        Assert.True(rt.B("Q2"));
        rt.Set("A", true); rt.Scan();
        Assert.False(rt.B("Q"));
        Assert.False(rt.B("Q2"));
    }

    [Fact]
    public void SetAndResetLatch()
    {
        var rt = Run(Project(Bools, S(No("A"), Coil("Q", CoilKind.Set)), S(No("B"), Coil("Q", CoilKind.Reset))));
        rt.Set("A", true); rt.Scan(); rt.Set("A", false); rt.Scan();
        Assert.True(rt.B("Q"));
        rt.Set("B", true); rt.Scan();
        Assert.False(rt.B("Q"));
    }

    [Fact]
    public void EdgeContactsPassForOneScan()
    {
        var rt = Run(Project(Bools, S(P("A"), Coil("Q")), S(N("A"), Coil("Q2"))));
        rt.Scan();
        Assert.False(rt.B("Q"));
        rt.Set("A", true); rt.Scan();
        Assert.True(rt.B("Q"));
        rt.Scan();
        Assert.False(rt.B("Q"));
        rt.Set("A", false); rt.Scan();
        Assert.True(rt.B("Q2"));
        rt.Scan();
        Assert.False(rt.B("Q2"));
    }

    [Fact]
    public void EveryParallelBranchRunsItsCoils()
    {
        var rt = Run(Project(Bools, S(No("A"), Par(Coil("Q"), Coil("Q2")))));
        rt.Set("A", true); rt.Scan();
        Assert.True(rt.B("Q") && rt.B("Q2"));
    }

    [Fact]
    public void PowerFlowIsRecordedForLiveView()
    {
        var a = No("A");
        var b = No("B");
        var rt = Run(Project(Bools, S(a, b, Coil("Q"))));
        rt.Set("A", true); rt.Scan();
        Assert.True(rt.PowerOut(a));
        Assert.False(rt.PowerOut(b));
    }
}

public class ScanCycleTests
{
    private sealed class Bench : IVirtualIo
    {
        public bool In;
        public bool Out;
        public long Advanced;
        public PlcValue? ReadInput(string tag) => tag == "In" ? PlcValue.FromBool(In) : null;
        public void WriteOutput(string tag, PlcValue value) { if (tag == "Out") Out = value.AsBool; }
        public void Advance(long ms) => Advanced += ms;
    }

    private static PlcProject Wire() =>
        Project([Tag("In", io: IoDirection.Input), Tag("Out", io: IoDirection.Output)], S(No("In"), Coil("Out")));

    [Fact]
    public void InputsInProgramOutputsOutClockOn()
    {
        var bench = new Bench { In = true };
        var rt = new PlcRuntime(Wire(), IecDialect.Instance, bench);
        rt.Scan();
        Assert.True(bench.Out);
        Assert.Equal(10, rt.NowMs);
        Assert.Equal(10, bench.Advanced);
    }

    [Fact]
    public void ForcingOverridesTheMachineAndTheProgram()
    {
        var bench = new Bench { In = false };
        var rt = new PlcRuntime(Wire(), IecDialect.Instance, bench);
        rt.Force("In", PlcValue.True);
        rt.Scan();
        Assert.True(bench.Out);
        rt.Force("Out", PlcValue.False);
        rt.Scan();
        Assert.False(bench.Out);
        Assert.True(rt.B("Out")); // the program's own value is unchanged; only the wire is forced
    }

    [Fact]
    public void ScanTimeIsConfigurable()
    {
        var p = Wire();
        p.ScanTimeMs = 25;
        var rt = Run(p);
        rt.RunFor(100);
        Assert.Equal(4, rt.ScanCount);
    }

    [Fact]
    public void DivideByZeroFaultsAndTurnsOutputsOff()
    {
        var bench = new Bench { In = true };
        var p = Project([Tag("In", io: IoDirection.Input), Tag("Out", io: IoDirection.Output), Tag("X", "INT")],
            S(No("In"), Coil("Out")),
            S(No("In"), Box("DIV", null, ("IN1", "10"), ("IN2", "0"), ("OUT", "X"))));
        var rt = new PlcRuntime(p, IecDialect.Instance, bench);
        rt.Scan();
        Assert.Equal(RunState.Faulted, rt.State);
        Assert.Equal(FaultKind.Math, rt.Fault!.Kind);
        Assert.Equal(2, rt.Fault.Rung);
        Assert.False(bench.Out);
        rt.Reset();
        Assert.Equal(RunState.Running, rt.State);
    }

    [Fact]
    public void ProjectsRoundTripThroughJson()
    {
        var p = Project([Tag("A"), Tag("T1", "TON"), Tag("Q")], S(No("A"), Box("TON", "T1", ("PT", "T#2s")), Coil("Q")));
        var again = ProjectJson.Deserialize(ProjectJson.Serialize(p));
        Assert.Equal(ProjectJson.Serialize(p), ProjectJson.Serialize(again));
        Assert.IsType<BoxElement>(((SeriesElement)again.Programs[0].Rungs[0].Logic).Items[1]);
    }
}

public class TimerTests
{
    private static PlcRuntime Timer(string type, string pt = "T#100ms") =>
        Run(Project([Tag("A"), Tag("T1", type), Tag("Q")], S(No("A"), Box(type, "T1", ("PT", pt)), Coil("Q"))));

    [Fact]
    public void TonTurnsOnAfterPtAndOffWithIn()
    {
        var rt = Timer("TON");
        rt.Set("A", true);
        for (var i = 0; i < 10; i++) { rt.Scan(); Assert.False(rt.B("Q")); } // t = 0 … 90 ms
        rt.Scan();                                                           // t = 100 ms
        Assert.True(rt.B("Q"));
        Assert.Equal(100, rt.L("T1.ET"));
        rt.Set("A", false); rt.Scan();
        Assert.False(rt.B("Q"));
        Assert.Equal(0, rt.L("T1.ET"));
    }

    [Fact]
    public void TonRestartsWhenInDropsEarly()
    {
        var rt = Timer("TON");
        rt.Set("A", true); rt.RunFor(50);
        rt.Set("A", false); rt.Scan();
        rt.Set("A", true); rt.RunFor(90);
        Assert.False(rt.B("Q"));
        rt.RunFor(20);
        Assert.True(rt.B("Q"));
    }

    [Fact]
    public void TofHoldsForPtAfterInDrops()
    {
        var rt = Timer("TOF");
        rt.Scan();
        Assert.False(rt.B("Q"));
        rt.Set("A", true); rt.Scan();
        Assert.True(rt.B("Q"));
        rt.Set("A", false);
        for (var i = 0; i < 10; i++) { rt.Scan(); Assert.True(rt.B("Q")); }
        rt.Scan();
        Assert.False(rt.B("Q"));
    }

    [Fact]
    public void TonrAddsUpTheTimeInIsOnAndKeepsQUntilReset()
    {
        var rt = Run(Project([Tag("A"), Tag("Clear"), Tag("T1", "TONR"), Tag("Q")],
            S(No("A"), Box("TONR", "T1", ("PT", "T#100ms"), ("R", "Clear")), Coil("Q"))));
        rt.Set("A", true); rt.RunFor(60);        // on for 60 ms (scans at 0 … 50)
        rt.Set("A", false); rt.RunFor(200);       // off: the time is kept
        Assert.Equal(60, rt.L("T1.ET"));
        Assert.False(rt.B("T1.Q"));
        rt.Set("A", true); rt.RunFor(30);         // 60 + 30
        Assert.False(rt.B("T1.Q"));
        rt.RunFor(20);                            // reaches 100
        Assert.True(rt.B("T1.Q"));
        Assert.Equal(100, rt.L("T1.ET"));
        rt.Set("A", false); rt.RunFor(50);        // Q stays on with IN off
        Assert.True(rt.B("T1.Q"));
        Assert.True(rt.B("Q"));                   // the box passes Q on down the rung, as TON does
        rt.Set("Clear", true); rt.Scan();
        Assert.False(rt.B("T1.Q"));
        Assert.Equal(0, rt.L("T1.ET"));
    }

    [Fact]
    public void TpPulsesForPtWhateverInDoes()
    {
        var rt = Timer("TP");
        rt.Set("A", true); rt.Scan();
        Assert.True(rt.B("T1.Q"));
        rt.Set("A", false); rt.RunFor(50);
        Assert.True(rt.B("T1.Q"));           // IN dropped, pulse carries on
        rt.RunFor(60);
        Assert.False(rt.B("T1.Q"));
        rt.Set("A", true); rt.Scan();         // a new rising edge starts a new pulse
        Assert.True(rt.B("T1.Q"));
    }

    [Fact]
    public void SameProgramSameResultEveryTime()
    {
        string Trace()
        {
            var rt = Timer("TON", "T#230ms");
            var log = new System.Text.StringBuilder();
            for (var i = 0; i < 60; i++)
            {
                rt.Set("A", i % 37 < 30);
                rt.Scan();
                log.Append(rt.B("Q") ? '1' : '0');
            }
            return log.ToString();
        }
        Assert.Equal(Trace(), Trace());
    }
}

public class CounterTests
{
    [Fact]
    public void CtuCountsRisingEdgesAndResets()
    {
        var rt = Run(Project([Tag("A"), Tag("R"), Tag("C1", "CTU"), Tag("Q")],
            S(No("A"), Box("CTU", "C1", ("PV", "3"), ("R", "R")), Coil("Q"))));
        for (var i = 0; i < 3; i++)
        {
            rt.Set("A", true); rt.Scan(); rt.Scan(); // held for two scans counts once
            rt.Set("A", false); rt.Scan();
        }
        Assert.Equal(3, rt.L("C1.CV"));
        Assert.True(rt.B("Q"));
        rt.Set("R", true); rt.Scan();
        Assert.Equal(0, rt.L("C1.CV"));
    }

    [Fact]
    public void CtdLoadsAndCountsDown()
    {
        var rt = Run(Project([Tag("A"), Tag("LD"), Tag("C1", "CTD"), Tag("Q")],
            S(No("A"), Box("CTD", "C1", ("PV", "2"), ("LD", "LD")), Coil("Q"))));
        rt.Set("LD", true); rt.Scan(); rt.Set("LD", false);
        Assert.Equal(2, rt.L("C1.CV"));
        rt.Set("A", true); rt.Scan(); rt.Set("A", false); rt.Scan();
        rt.Set("A", true); rt.Scan();
        Assert.Equal(0, rt.L("C1.CV"));
        Assert.True(rt.B("Q"));
    }

    [Fact]
    public void CtudCountsBothWays()
    {
        var rt = Run(Project([Tag("Up"), Tag("Dn"), Tag("C1", "CTUD"), Tag("Q")],
            S(No("Up"), Box("CTUD", "C1", ("PV", "2"), ("CD", "Dn")), Coil("Q"))));
        rt.Set("Up", true); rt.Scan(); rt.Set("Up", false); rt.Scan();
        rt.Set("Up", true); rt.Scan(); rt.Set("Up", false); rt.Scan();
        Assert.True(rt.B("C1.QU"));
        rt.Set("Dn", true); rt.Scan();
        Assert.Equal(1, rt.L("C1.CV"));
        Assert.False(rt.B("Q"));
    }
}

public class FunctionTests
{
    private static PlcRuntime Math1(string op, string in1, string in2, string outType = "DINT") =>
        Run(Project([Tag("En", initial: "TRUE"), Tag("R", outType)], S(No("En"), Box(op, null, ("IN1", in1), ("IN2", in2), ("OUT", "R")))));

    [Theory]
    [InlineData("ADD", "7", "5", 12)]
    [InlineData("SUB", "7", "5", 2)]
    [InlineData("MUL", "7", "5", 35)]
    [InlineData("DIV", "7", "2", 3)]
    [InlineData("MOD", "7", "5", 2)]
    [InlineData("AND", "12", "10", 8)]
    [InlineData("OR", "12", "10", 14)]
    [InlineData("XOR", "12", "10", 6)]
    public void IntegerMathAndBitLogic(string op, string a, string b, long expected)
    {
        var rt = Math1(op, a, b);
        rt.Scan();
        Assert.Equal(expected, rt.L("R"));
    }

    [Fact]
    public void RealDivisionKeepsTheFraction()
    {
        var rt = Math1("DIV", "7", "2", "REAL");
        rt.Scan();
        Assert.Equal(3.5, rt.Read("R").AsReal);
    }

    [Fact]
    public void IntResultWraps()
    {
        var rt = Math1("ADD", "32767", "1", "INT");
        rt.Scan();
        Assert.Equal(-32768, rt.L("R"));
    }

    [Theory]
    [InlineData("GT", "5", "3", true)]
    [InlineData("GT", "3", "5", false)]
    [InlineData("GE", "5", "5", true)]
    [InlineData("LT", "2.5", "3", true)]
    [InlineData("LE", "4", "3", false)]
    [InlineData("EQ", "3", "3", true)]
    [InlineData("NE", "3", "3", false)]
    public void ComparisonsPassPower(string op, string a, string b, bool expected)
    {
        var rt = Run(Project([Tag("Q")], S(Box(op, null, ("IN1", a), ("IN2", b)), Coil("Q"))));
        rt.Scan();
        Assert.Equal(expected, rt.B("Q"));
    }

    [Fact]
    public void MoveAndNot()
    {
        var rt = Run(Project([Tag("X", "INT"), Tag("Y", "INT"), Tag("B"), Tag("NB")],
            S(Box("MOVE", null, ("IN", "42"), ("OUT", "X")), Box("NOT", null, ("IN", "B"), ("OUT", "NB")), Box("NOT", null, ("IN", "X"), ("OUT", "Y")))));
        rt.Scan();
        Assert.Equal(42, rt.L("X"));
        Assert.True(rt.B("NB"));
        Assert.Equal(-43, rt.L("Y"));
    }

    [Fact]
    public void NoPowerNoExecution()
    {
        var rt = Run(Project([Tag("En"), Tag("X", "INT")], S(No("En"), Box("MOVE", null, ("IN", "5"), ("OUT", "X")))));
        rt.Scan();
        Assert.Equal(0, rt.L("X"));
    }

    [Fact]
    public void EveryInstructionHasASummaryAndOperandHelp()
    {
        foreach (var i in IecDialect.Instance.Instructions)
        {
            Assert.False(string.IsNullOrWhiteSpace(i.Summary), i.Name);
            Assert.All(i.Operands, o => Assert.False(string.IsNullOrWhiteSpace(o.Help), $"{i.Name}.{o.Name}"));
        }
    }
}

public class ValidatorTests
{
    private static IReadOnlyList<Diagnostic> Check(PlcProject p) => ProjectValidator.Validate(p, IecDialect.Instance);

    [Fact]
    public void MissingTagPointsToRungAndElement()
    {
        var d = Check(Project([Tag("Q")], S(No("Q")), S(No("Strt"), Coil("Q"))));
        var e = Assert.Single(d, x => x.Severity == Severity.Error);
        Assert.Equal(2, e.Rung);
        Assert.Equal("1.1", e.ElementPath);
        Assert.Contains("'Strt'", e.Message);
        Assert.Contains("spelling", e.Message);
    }

    [Fact]
    public void ContactNeedsBool()
    {
        var d = Check(Project([Tag("X", "INT"), Tag("Q")], S(No("X"), Coil("Q"))));
        Assert.Contains(d, x => x.Severity == Severity.Error && x.Message.Contains("BOOL", StringComparison.Ordinal));
    }

    [Fact]
    public void TimerNeedsAnInstanceOfItsType()
    {
        var d = Check(Project([Tag("A"), Tag("C1", "CTU"), Tag("Q")], S(No("A"), Box("TON", "C1", ("PT", "T#1s")), Coil("Q"))));
        Assert.Contains(d, x => x.Message.Contains("type TON", StringComparison.Ordinal));
    }

    [Fact]
    public void TimerPresetMustBeATime()
    {
        var d = Check(Project([Tag("A"), Tag("T1", "TON"), Tag("Q")], S(No("A"), Box("TON", "T1", ("PT", "5")), Coil("Q"))));
        Assert.Contains(d, x => x.Message.Contains("T#2s", StringComparison.Ordinal));
    }

    [Fact]
    public void CoilCannotWriteATimerMember()
    {
        var d = Check(Project([Tag("A"), Tag("T1", "TON")], S(No("A"), Coil("T1.Q"))));
        Assert.Contains(d, x => x.Severity == Severity.Error && x.Message.Contains("timer or counter", StringComparison.Ordinal));
    }

    [Fact]
    public void RungWithNoOutputIsAWarning()
    {
        var d = Check(Project([Tag("A")], S(No("A"))));
        Assert.Contains(d, x => x.Severity == Severity.Warning && x.Message.Contains("does nothing", StringComparison.Ordinal));
    }

    [Fact]
    public void CoilOnAnInputIsAWarning()
    {
        var d = Check(Project([Tag("A"), Tag("I", io: IoDirection.Input)], S(No("A"), Coil("I"))));
        Assert.Contains(d, x => x.Severity == Severity.Warning && x.Message.Contains("is an input", StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidProjectDoesNotRun()
    {
        var ex = Assert.Throws<ProjectInvalidException>(() => Run(Project([Tag("Q")], S(No("Nope"), Coil("Q")))));
        Assert.Single(ex.Diagnostics);
    }
}
