namespace Plc.Engine;

// Instruction plug-ins (spec 4.3). Each instruction is a self-contained definition: its signature,
// its behaviour (Execute, once per scan), its documentation and the lesson that teaches it.
// Its reference tests live in Plc.Engine.Tests. Its symbol for each editor comes with the ladder editor.

public enum OperandDirection { In, Out }

public enum OperandKind
{
    /// <summary>BOOL.</summary>
    Bool,
    /// <summary>INT, DINT or REAL.</summary>
    Number,
    /// <summary>INT or DINT (counters, MOD).</summary>
    Integer,
    /// <summary>TIME.</summary>
    Time,
    /// <summary>Any elementary type (MOVE, the bit-logic words).</summary>
    Any,
}

/// <summary>A named operand of a box, other than the power that flows in.</summary>
public sealed record OperandSpec(string Name, OperandKind Kind, OperandDirection Direction, bool Required, string Help);

/// <summary>A member of a function block instance that a program can read, such as T1.Q or C1.CV.</summary>
public sealed record MemberSpec(string Name, DataType Type, string Help);

public interface IInstruction
{
    /// <summary>The name used in the program (TON, ADD, GT, …).</summary>
    string Name { get; }

    /// <summary>The function block type its instance tag must have (TON for TON), or null for a function.</summary>
    string? FunctionBlockType { get; }

    IReadOnlyList<OperandSpec> Operands { get; }

    /// <summary>The instance members a program can read (empty for a function).</summary>
    IReadOnlyList<MemberSpec> Members { get; }

    /// <summary>One sentence for a beginner: what it does.</summary>
    string Summary { get; }

    /// <summary>The lesson that teaches it (a stable activity ID), once that lesson exists.</summary>
    string? LessonId { get; }

    /// <summary>Runs once per scan. Returns the power that flows out of the box (its Q or ENO).</summary>
    bool Execute(InstructionContext context, bool powerIn);
}

/// <summary>A dialect: addressing, instruction names and behaviour where vendors differ (spec 4.4).</summary>
public interface IDialect
{
    string Id { get; }
    string DisplayName { get; }
    IReadOnlyCollection<IInstruction> Instructions { get; }
    IInstruction? Find(string name);
}

/// <summary>
/// The state of one function block instance (one TON, one CTU, …). Members with names that start
/// with an underscore are private to the instruction and are not readable from the program.
/// </summary>
public sealed class FbInstance(string name, string type)
{
    public string Name { get; } = name;
    public string Type { get; } = type;
    public Dictionary<string, PlcValue> Members { get; } = new(StringComparer.OrdinalIgnoreCase);

    public PlcValue Get(string member, PlcValue fallback) => Members.TryGetValue(member, out var v) ? v : fallback;
    public void Set(string member, PlcValue value) => Members[member] = value;
}

/// <summary>What an instruction can see and do while it runs.</summary>
public sealed class InstructionContext
{
    private readonly PlcRuntime _runtime;
    private readonly BoxElement _box;

    internal InstructionContext(PlcRuntime runtime, BoxElement box, FbInstance? instance)
    {
        _runtime = runtime;
        _box = box;
        Instance = instance;
    }

    /// <summary>The simulated clock, in milliseconds since the last reset.</summary>
    public long NowMs => _runtime.NowMs;

    /// <summary>The instance state of a function block (null for a function).</summary>
    public FbInstance? Instance { get; }

    public bool Has(string operand) => _box.Operands.TryGetValue(operand, out var o) && !string.IsNullOrWhiteSpace(o);

    /// <summary>Reads an operand: a literal, or the current value of the tag it names.</summary>
    public PlcValue Read(string operand)
    {
        if (!_box.Operands.TryGetValue(operand, out var text) || string.IsNullOrWhiteSpace(text))
            throw new PlcFaultException(FaultKind.Configuration, $"{_box.Instruction} needs a value for {operand}.");
        return PlcValue.TryParseLiteral(text, out var literal) ? literal : _runtime.Read(text);
    }

    /// <summary>The declared type of the tag an output operand writes to.</summary>
    public DataType TypeOf(string operand) => _runtime.TypeOf(_box.Operands[operand]);

    /// <summary>Writes an output operand; the value is converted to the tag's type.</summary>
    public void Write(string operand, PlcValue value) => _runtime.Write(_box.Operands[operand], value);

    /// <summary>Stops the PLC with a fault, the way a real PLC does (divide by zero, …).</summary>
    public void Fault(FaultKind kind, string message) => throw new PlcFaultException(kind, message);
}

public enum FaultKind
{
    /// <summary>A math fault, such as dividing by zero.</summary>
    Math,
    /// <summary>An index outside an array (arrays come later).</summary>
    IndexOutOfRange,
    /// <summary>A scan took too long (comes with loops and jumps).</summary>
    Watchdog,
    /// <summary>The program asks for something the engine cannot do (a missing operand, …).</summary>
    Configuration,
}

public sealed class PlcFaultException(FaultKind kind, string message) : Exception(message)
{
    public FaultKind Kind { get; } = kind;
}

/// <summary>A fault the PLC stopped on, with where it happened.</summary>
public sealed record PlcFault(FaultKind Kind, string Message, string Program, int Rung, long AtMs);
