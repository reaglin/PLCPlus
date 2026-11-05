using Plc.Engine;

namespace Plc.Dialects.Iec;

/// <summary>
/// Generic IEC 61131-3 — the only dialect in Phase 1 (spec 4.4). The Phase 1 instruction set (spec 4.3):
/// TON/TOF/TP, CTU/CTD/CTUD, comparison, arithmetic, MOVE and bit logic. Contacts and coils are
/// built into the engine.
/// </summary>
public sealed class IecDialect : IDialect
{
    public static readonly IecDialect Instance = new();

    private readonly Dictionary<string, IInstruction> _byName;

    private IecDialect()
    {
        IInstruction[] all =
        [
            new Ton(), new Tof(), new Tp(), new Tonr(),
            new Ctu(), new Ctd(), new Ctud(),
            new Compare("EQ", "equal to", (a, b) => a == b),
            new Compare("NE", "not equal to", (a, b) => a != b),
            new Compare("GT", "greater than", (a, b) => a > b),
            new Compare("GE", "greater than or equal to", (a, b) => a >= b),
            new Compare("LT", "less than", (a, b) => a < b),
            new Compare("LE", "less than or equal to", (a, b) => a <= b),
            new Arithmetic("ADD", "adds IN1 and IN2"),
            new Arithmetic("SUB", "subtracts IN2 from IN1"),
            new Arithmetic("MUL", "multiplies IN1 by IN2"),
            new Arithmetic("DIV", "divides IN1 by IN2 (whole numbers drop the remainder)"),
            new Arithmetic("MOD", "gives the remainder of IN1 divided by IN2"),
            new Move(),
            new BitLogic("AND"), new BitLogic("OR"), new BitLogic("XOR"), new BitNot(),
        ];
        _byName = all.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
    }

    public string Id => "iec61131-3";
    public string DisplayName => "Generic IEC 61131-3";
    public IReadOnlyCollection<IInstruction> Instructions => _byName.Values;
    public IInstruction? Find(string name) => _byName.GetValueOrDefault(name);
}

internal abstract class InstructionBase : IInstruction
{
    public abstract string Name { get; }
    public virtual string? FunctionBlockType => null;
    public abstract IReadOnlyList<OperandSpec> Operands { get; }
    public virtual IReadOnlyList<MemberSpec> Members => [];
    public abstract string Summary { get; }
    public virtual string? LessonId => null;
    public abstract bool Execute(InstructionContext context, bool powerIn);

    protected static OperandSpec In(string name, OperandKind kind, string help, bool required = true) => new(name, kind, OperandDirection.In, required, help);
    protected static OperandSpec Out(string name, OperandKind kind, string help) => new(name, kind, OperandDirection.Out, true, help);

    protected static FbInstance Fb(InstructionContext c) =>
        c.Instance ?? throw new PlcFaultException(FaultKind.Configuration, "A timer or counter has no tag to keep its state in.");
}

// ---- Timers ---------------------------------------------------------------------------------------
// All three time from the simulated clock. ET counts from the scan in which the timer started,
// so with a 10 ms scan a T#100ms TON turns Q on in the 11th scan after IN — every time.

internal sealed class Ton : InstructionBase
{
    public override string Name => "TON";
    public override string? FunctionBlockType => "TON";
    public override IReadOnlyList<OperandSpec> Operands { get; } = [In("PT", OperandKind.Time, "how long IN must stay on, such as T#2s.")];
    public override IReadOnlyList<MemberSpec> Members { get; } =
    [
        new("IN", DataType.Bool, "The power into the timer."),
        new("PT", DataType.Time, "The preset time."),
        new("Q", DataType.Bool, "On once IN has been on for PT."),
        new("ET", DataType.Time, "How long IN has been on (stops at PT)."),
    ];
    public override string Summary => "On-delay timer: Q turns on after IN has stayed on for the time PT, and off as soon as IN turns off.";

    public override bool Execute(InstructionContext c, bool powerIn)
    {
        var fb = Fb(c);
        var pt = c.Read("PT").AsLong;
        long et = 0;
        if (powerIn)
        {
            if (!fb.Get("IN", PlcValue.False).AsBool) fb.Set("_start", PlcValue.FromTime(c.NowMs));
            et = Math.Min(c.NowMs - fb.Get("_start", PlcValue.FromTime(c.NowMs)).I, pt);
        }
        var q = powerIn && et >= pt;
        fb.Set("IN", PlcValue.FromBool(powerIn));
        fb.Set("PT", PlcValue.FromTime(pt));
        fb.Set("ET", PlcValue.FromTime(et));
        fb.Set("Q", PlcValue.FromBool(q));
        return q;
    }
}

/// <summary>
/// A retentive on-delay timer (TONR, as Siemens names it; RTO on the Allen-Bradley PLC). Like TON, but ET keeps its time while
/// IN is off, and adds to it when IN comes back on; Q stays on once ET reaches PT, even when IN turns off. Only R clears it.
/// ET counts from the scan in which IN turned on, as TON's does, and a stretch ends in the scan that sees IN off.
/// </summary>
internal sealed class Tonr : InstructionBase
{
    public override string Name => "TONR";
    public override string? FunctionBlockType => "TONR";
    public override IReadOnlyList<OperandSpec> Operands { get; } =
    [
        In("PT", OperandKind.Time, "the total time IN must be on, added up over every time it is on, such as T#10s."),
        In("R", OperandKind.Bool, "a BOOL that clears the time (ET back to 0, Q off) while it is on.", required: false),
    ];
    public override IReadOnlyList<MemberSpec> Members { get; } =
    [
        new("IN", DataType.Bool, "The power into the timer."),
        new("PT", DataType.Time, "The preset time."),
        new("Q", DataType.Bool, "On once IN has been on for PT in all, and stays on until R."),
        new("ET", DataType.Time, "How long IN has been on in all (stops at PT); kept while IN is off."),
    ];
    public override string Summary => "Retentive on-delay timer: adds up the time IN is on, keeping it while IN is off; Q turns on when the total reaches PT and stays on until R clears it (RTO on Allen-Bradley).";

    public override bool Execute(InstructionContext c, bool powerIn)
    {
        var fb = Fb(c);
        var pt = c.Read("PT").AsLong;
        var wasIn = fb.Get("IN", PlcValue.False).AsBool;
        var acc = fb.Get("_acc", PlcValue.FromTime(0)).I;
        long et;
        if (c.Has("R") && c.Read("R").AsBool)
        {
            acc = 0;
            et = 0;
            if (powerIn) fb.Set("_start", PlcValue.FromTime(c.NowMs));
        }
        else if (powerIn)
        {
            if (!wasIn) fb.Set("_start", PlcValue.FromTime(c.NowMs));
            et = Math.Min(acc + c.NowMs - fb.Get("_start", PlcValue.FromTime(c.NowMs)).I, pt);
        }
        else
        {
            if (wasIn) acc = Math.Min(acc + c.NowMs - fb.Get("_start", PlcValue.FromTime(c.NowMs)).I, pt);
            et = acc;
        }
        var q = et >= pt;
        fb.Set("_acc", PlcValue.FromTime(acc));
        fb.Set("IN", PlcValue.FromBool(powerIn));
        fb.Set("PT", PlcValue.FromTime(pt));
        fb.Set("ET", PlcValue.FromTime(et));
        fb.Set("Q", PlcValue.FromBool(q));
        return q;
    }
}

internal sealed class Tof : InstructionBase
{
    public override string Name => "TOF";
    public override string? FunctionBlockType => "TOF";
    public override IReadOnlyList<OperandSpec> Operands { get; } = [In("PT", OperandKind.Time, "how long Q stays on after IN turns off, such as T#2s.")];
    public override IReadOnlyList<MemberSpec> Members { get; } =
    [
        new("IN", DataType.Bool, "The power into the timer."),
        new("PT", DataType.Time, "The preset time."),
        new("Q", DataType.Bool, "On while IN is on, and for PT after it turns off."),
        new("ET", DataType.Time, "How long IN has been off (stops at PT)."),
    ];
    public override string Summary => "Off-delay timer: Q turns on with IN, and stays on for the time PT after IN turns off.";

    public override bool Execute(InstructionContext c, bool powerIn)
    {
        var fb = Fb(c);
        var pt = c.Read("PT").AsLong;
        var wasIn = fb.Get("IN", PlcValue.False).AsBool;
        bool q;
        long et;
        if (powerIn)
        {
            q = true;
            et = 0;
            fb.Set("_running", PlcValue.True);
        }
        else
        {
            if (wasIn) fb.Set("_start", PlcValue.FromTime(c.NowMs));
            var running = fb.Get("_running", PlcValue.False).AsBool;
            et = running ? Math.Min(c.NowMs - fb.Get("_start", PlcValue.FromTime(c.NowMs)).I, pt) : fb.Get("ET", PlcValue.FromTime(0)).I;
            q = running && et < pt;
            if (!q) fb.Set("_running", PlcValue.False);
        }
        fb.Set("IN", PlcValue.FromBool(powerIn));
        fb.Set("PT", PlcValue.FromTime(pt));
        fb.Set("ET", PlcValue.FromTime(et));
        fb.Set("Q", PlcValue.FromBool(q));
        return q;
    }
}

internal sealed class Tp : InstructionBase
{
    public override string Name => "TP";
    public override string? FunctionBlockType => "TP";
    public override IReadOnlyList<OperandSpec> Operands { get; } = [In("PT", OperandKind.Time, "how long the pulse lasts, such as T#2s.")];
    public override IReadOnlyList<MemberSpec> Members { get; } =
    [
        new("IN", DataType.Bool, "The power into the timer."),
        new("PT", DataType.Time, "The pulse length."),
        new("Q", DataType.Bool, "On for PT from the moment IN turns on."),
        new("ET", DataType.Time, "How far into the pulse it is."),
    ];
    public override string Summary => "Pulse timer: when IN turns on, Q turns on for exactly the time PT, whatever IN does meanwhile.";

    public override bool Execute(InstructionContext c, bool powerIn)
    {
        var fb = Fb(c);
        var pt = c.Read("PT").AsLong;
        var wasIn = fb.Get("IN", PlcValue.False).AsBool;
        var pulsing = fb.Get("_pulsing", PlcValue.False).AsBool;
        var et = fb.Get("ET", PlcValue.FromTime(0)).I;

        if (powerIn && !wasIn && !pulsing)
        {
            pulsing = true;
            fb.Set("_start", PlcValue.FromTime(c.NowMs));
        }
        if (pulsing)
        {
            et = Math.Min(c.NowMs - fb.Get("_start", PlcValue.FromTime(c.NowMs)).I, pt);
            if (et >= pt) pulsing = false;
        }
        else if (!powerIn)
        {
            et = 0;
        }
        var q = pulsing && et < pt;
        fb.Set("_pulsing", PlcValue.FromBool(pulsing));
        fb.Set("IN", PlcValue.FromBool(powerIn));
        fb.Set("PT", PlcValue.FromTime(pt));
        fb.Set("ET", PlcValue.FromTime(et));
        fb.Set("Q", PlcValue.FromBool(q));
        return q;
    }
}

// ---- Counters (CV is an INT, as in IEC) ------------------------------------------------------------

internal sealed class Ctu : InstructionBase
{
    public override string Name => "CTU";
    public override string? FunctionBlockType => "CTU";
    public override IReadOnlyList<OperandSpec> Operands { get; } =
    [
        In("PV", OperandKind.Integer, "the count at which Q turns on, such as 10."),
        In("R", OperandKind.Bool, "a BOOL that sets the count back to 0 while it is on.", required: false),
    ];
    public override IReadOnlyList<MemberSpec> Members { get; } =
    [
        new("CU", DataType.Bool, "The power into the counter; each time it turns on, CV goes up by 1."),
        new("PV", DataType.Int, "The preset count."),
        new("Q", DataType.Bool, "On while CV is at least PV."),
        new("CV", DataType.Int, "The count so far."),
    ];
    public override string Summary => "Up counter: each time the power turns on, the count CV goes up by one; Q turns on when CV reaches PV.";

    public override bool Execute(InstructionContext c, bool powerIn)
    {
        var fb = Fb(c);
        var pv = c.Read("PV").AsLong;
        var cv = fb.Get("CV", PlcValue.FromInt(0)).I;
        var wasCu = fb.Get("CU", PlcValue.False).AsBool;
        if (c.Has("R") && c.Read("R").AsBool) cv = 0;
        else if (powerIn && !wasCu && cv < short.MaxValue) cv++;
        var q = cv >= pv;
        fb.Set("CU", PlcValue.FromBool(powerIn));
        fb.Set("PV", PlcValue.FromInt(pv));
        fb.Set("CV", PlcValue.FromInt(cv));
        fb.Set("Q", PlcValue.FromBool(q));
        return q;
    }
}

internal sealed class Ctd : InstructionBase
{
    public override string Name => "CTD";
    public override string? FunctionBlockType => "CTD";
    public override IReadOnlyList<OperandSpec> Operands { get; } =
    [
        In("PV", OperandKind.Integer, "the count LD loads, such as 10."),
        In("LD", OperandKind.Bool, "a BOOL that loads PV into the count while it is on.", required: false),
    ];
    public override IReadOnlyList<MemberSpec> Members { get; } =
    [
        new("CD", DataType.Bool, "The power into the counter; each time it turns on, CV goes down by 1."),
        new("PV", DataType.Int, "The preset count."),
        new("Q", DataType.Bool, "On while CV is 0 or less."),
        new("CV", DataType.Int, "The count."),
    ];
    public override string Summary => "Down counter: LD loads the count PV; each time the power turns on the count goes down by one; Q turns on at zero.";

    public override bool Execute(InstructionContext c, bool powerIn)
    {
        var fb = Fb(c);
        var pv = c.Read("PV").AsLong;
        var cv = fb.Get("CV", PlcValue.FromInt(0)).I;
        var wasCd = fb.Get("CD", PlcValue.False).AsBool;
        if (c.Has("LD") && c.Read("LD").AsBool) cv = pv;
        else if (powerIn && !wasCd && cv > short.MinValue) cv--;
        var q = cv <= 0;
        fb.Set("CD", PlcValue.FromBool(powerIn));
        fb.Set("PV", PlcValue.FromInt(pv));
        fb.Set("CV", PlcValue.FromInt(cv));
        fb.Set("Q", PlcValue.FromBool(q));
        return q;
    }
}

internal sealed class Ctud : InstructionBase
{
    public override string Name => "CTUD";
    public override string? FunctionBlockType => "CTUD";
    public override IReadOnlyList<OperandSpec> Operands { get; } =
    [
        In("PV", OperandKind.Integer, "the preset count, such as 10."),
        In("CD", OperandKind.Bool, "a BOOL; each time it turns on, the count goes down by one.", required: false),
        In("R", OperandKind.Bool, "a BOOL that sets the count to 0.", required: false),
        In("LD", OperandKind.Bool, "a BOOL that loads PV into the count.", required: false),
    ];
    public override IReadOnlyList<MemberSpec> Members { get; } =
    [
        new("CU", DataType.Bool, "The power into the counter (counts up)."),
        new("CD", DataType.Bool, "The count-down input."),
        new("PV", DataType.Int, "The preset count."),
        new("QU", DataType.Bool, "On while CV is at least PV."),
        new("QD", DataType.Bool, "On while CV is 0 or less."),
        new("CV", DataType.Int, "The count."),
    ];
    public override string Summary => "Up/down counter: the power counts up, CD counts down; QU is on at PV or more, QD at zero or less.";

    public override bool Execute(InstructionContext c, bool powerIn)
    {
        var fb = Fb(c);
        var pv = c.Read("PV").AsLong;
        var cd = c.Has("CD") && c.Read("CD").AsBool;
        var cv = fb.Get("CV", PlcValue.FromInt(0)).I;
        var wasCu = fb.Get("CU", PlcValue.False).AsBool;
        var wasCd = fb.Get("CD", PlcValue.False).AsBool;
        if (c.Has("R") && c.Read("R").AsBool) cv = 0;
        else if (c.Has("LD") && c.Read("LD").AsBool) cv = pv;
        else
        {
            var up = powerIn && !wasCu;
            var down = cd && !wasCd;
            if (up && !down && cv < short.MaxValue) cv++;
            else if (down && !up && cv > short.MinValue) cv--;
        }
        var qu = cv >= pv;
        fb.Set("CU", PlcValue.FromBool(powerIn));
        fb.Set("CD", PlcValue.FromBool(cd));
        fb.Set("PV", PlcValue.FromInt(pv));
        fb.Set("CV", PlcValue.FromInt(cv));
        fb.Set("QU", PlcValue.FromBool(qu));
        fb.Set("QD", PlcValue.FromBool(cv <= 0));
        return qu;
    }
}

// ---- Comparison: power passes when it flows in and the comparison is true ----------------------------

internal sealed class Compare(string name, string words, Func<double, double, bool> test) : InstructionBase
{
    public override string Name => name;
    public override IReadOnlyList<OperandSpec> Operands { get; } =
    [
        In("IN1", OperandKind.Any, "the first value: a tag or a number."),
        In("IN2", OperandKind.Any, "the second value: a tag or a number."),
    ];
    public override string Summary => $"Passes power when IN1 is {words} IN2.";

    public override bool Execute(InstructionContext c, bool powerIn)
    {
        if (!powerIn) return false;
        var a = c.Read("IN1");
        var b = c.Read("IN2");
        return a.IsInteger && b.IsInteger || a.Type == DataType.Bool && b.Type == DataType.Bool
            ? test(a.I, b.I)
            : test(a.AsReal, b.AsReal);
    }
}

// ---- Arithmetic and MOVE: run while power flows in; power passes on (ENO) ----------------------------

internal sealed class Arithmetic(string name, string words) : InstructionBase
{
    public override string Name => name;
    public override IReadOnlyList<OperandSpec> Operands { get; } =
    [
        In("IN1", name == "MOD" ? OperandKind.Integer : OperandKind.Number, "the first number: a tag or a number."),
        In("IN2", name == "MOD" ? OperandKind.Integer : OperandKind.Number, "the second number: a tag or a number."),
        Out("OUT", name == "MOD" ? OperandKind.Integer : OperandKind.Number, "the tag that receives the result."),
    ];
    public override string Summary => $"While power flows in, {words} and puts the result in OUT.";

    public override bool Execute(InstructionContext c, bool powerIn)
    {
        if (!powerIn) return false;
        var a = c.Read("IN1");
        var b = c.Read("IN2");
        var outType = c.TypeOf("OUT");
        var real = outType == DataType.Real || a.Type == DataType.Real || b.Type == DataType.Real;
        if (name is "DIV" or "MOD" && (real ? b.AsReal == 0 : b.AsLong == 0))
            c.Fault(FaultKind.Math, $"{name} divided by zero (IN2 is 0). A real PLC stops here too: check IN2 before dividing.");

        PlcValue result;
        if (real && name != "MOD")
        {
            var x = a.AsReal;
            var y = b.AsReal;
            result = PlcValue.FromReal(name switch { "ADD" => x + y, "SUB" => x - y, "MUL" => x * y, _ => x / y });
        }
        else
        {
            var x = a.AsLong;
            var y = b.AsLong;
            result = PlcValue.FromDInt(name switch
            {
                "ADD" => x + y,
                "SUB" => x - y,
                "MUL" => x * y,
                "DIV" => x / y,
                _ => x % y,
            });
        }
        c.Write("OUT", result);
        return true;
    }
}

internal sealed class Move : InstructionBase
{
    public override string Name => "MOVE";
    public override IReadOnlyList<OperandSpec> Operands { get; } =
    [
        In("IN", OperandKind.Any, "the value to copy: a tag or a number."),
        Out("OUT", OperandKind.Any, "the tag that receives the copy."),
    ];
    public override string Summary => "While power flows in, copies IN into OUT.";

    public override bool Execute(InstructionContext c, bool powerIn)
    {
        if (powerIn) c.Write("OUT", c.Read("IN"));
        return powerIn;
    }
}

internal sealed class BitLogic(string name) : InstructionBase
{
    public override string Name => name;
    public override IReadOnlyList<OperandSpec> Operands { get; } =
    [
        In("IN1", OperandKind.Any, "the first value (BOOL, INT or DINT)."),
        In("IN2", OperandKind.Any, "the second value (BOOL, INT or DINT)."),
        Out("OUT", OperandKind.Any, "the tag that receives the result."),
    ];
    public override string Summary => $"While power flows in, combines IN1 and IN2 bit by bit with {name} and puts the result in OUT.";

    public override bool Execute(InstructionContext c, bool powerIn)
    {
        if (!powerIn) return false;
        var a = c.Read("IN1").AsLong;
        var b = c.Read("IN2").AsLong;
        var r = name switch { "AND" => a & b, "OR" => a | b, _ => a ^ b };
        c.Write("OUT", PlcValue.FromDInt(r));
        return true;
    }
}

internal sealed class BitNot : InstructionBase
{
    public override string Name => "NOT";
    public override IReadOnlyList<OperandSpec> Operands { get; } =
    [
        In("IN", OperandKind.Any, "the value to invert (BOOL, INT or DINT)."),
        Out("OUT", OperandKind.Any, "the tag that receives the result."),
    ];
    public override string Summary => "While power flows in, turns every bit of IN over (TRUE becomes FALSE) and puts the result in OUT.";

    public override bool Execute(InstructionContext c, bool powerIn)
    {
        if (!powerIn) return false;
        var v = c.Read("IN");
        c.Write("OUT", v.Type == DataType.Bool ? PlcValue.FromBool(!v.AsBool) : PlcValue.FromDInt(~v.AsLong));
        return true;
    }
}
