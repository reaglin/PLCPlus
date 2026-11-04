using System.Text.Json;
using System.Text.Json.Serialization;

namespace Plc.Engine;

// The neutral program model (spec 4.1). Every editor — ladder now, Structured Text and SFC later —
// produces this same tree, and the engine, the grader and the report printouts read only this.
// It is saved as JSON, so projects are easy to diff, save and inspect.

/// <summary>A PLC project: its tags, and its programs in the order the task runs them.</summary>
public sealed class PlcProject
{
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "";
    /// <summary>The dialect the project is written in (spec 4.4). Phase 1 has only "iec61131-3".</summary>
    public string Dialect { get; set; } = "iec61131-3";
    /// <summary>The fixed scan time of the simulated clock, in milliseconds (spec 4.2; default 10 ms).</summary>
    public int ScanTimeMs { get; set; } = 10;
    /// <summary>Structure types the tags can use (spec 4.1).</summary>
    public List<StructType> Types { get; set; } = [];
    public List<TagDeclaration> Tags { get; set; } = [];
    public List<ProgramUnit> Programs { get; set; } = [];
}

public enum IoDirection
{
    /// <summary>A memory tag, not wired to the machine.</summary>
    None,
    /// <summary>Wired to a sensor or button in the scene; the scan copies it in at the start.</summary>
    Input,
    /// <summary>Wired to a light, motor or valve in the scene; the scan copies it out at the end.</summary>
    Output,
}

/// <summary>
/// A tag (variable). Its type is an elementary type (BOOL, INT, …), <c>ARRAY[0..9] OF INT</c>, a structure
/// declared in <see cref="PlcProject.Types"/>, or a function block type (TON, CTU, …).
/// </summary>
public sealed class TagDeclaration
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "BOOL";
    public string? Initial { get; set; }
    public IoDirection Io { get; set; }
    public string? Comment { get; set; }
}

/// <summary>A program unit (spec 4.1). Phase 1 has programs made of ladder rungs.</summary>
public sealed class ProgramUnit
{
    public string Name { get; set; } = "Main";
    public List<Rung> Rungs { get; set; } = [];
}

/// <summary>One rung: power flows from the left rail through <see cref="Logic"/>.</summary>
public sealed class Rung
{
    public string? Comment { get; set; }
    public LadderElement Logic { get; set; } = new SeriesElement();
}

/// <summary>A node in a rung. Each takes power in from its left and passes power out to its right.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SeriesElement), "series")]
[JsonDerivedType(typeof(ParallelElement), "parallel")]
[JsonDerivedType(typeof(ContactElement), "contact")]
[JsonDerivedType(typeof(CoilElement), "coil")]
[JsonDerivedType(typeof(BoxElement), "box")]
public abstract class LadderElement;

/// <summary>Elements one after another: power passes through each in turn (AND).</summary>
public sealed class SeriesElement : LadderElement
{
    public List<LadderElement> Items { get; set; } = [];
}

/// <summary>Branches side by side: each gets the same power in; power out is on if any branch passes it (OR).</summary>
public sealed class ParallelElement : LadderElement
{
    public List<LadderElement> Branches { get; set; } = [];
}

public enum ContactKind
{
    /// <summary>-| |- passes power when the tag is TRUE.</summary>
    NormallyOpen,
    /// <summary>-|/|- passes power when the tag is FALSE.</summary>
    NormallyClosed,
    /// <summary>-|P|- passes power for one scan when the tag turns TRUE.</summary>
    PositiveEdge,
    /// <summary>-|N|- passes power for one scan when the tag turns FALSE.</summary>
    NegativeEdge,
}

public sealed class ContactElement : LadderElement
{
    public string Tag { get; set; } = "";
    public ContactKind Contact { get; set; }
}

public enum CoilKind
{
    /// <summary>-( )- the tag follows the power.</summary>
    Normal,
    /// <summary>-(/)- the tag is the opposite of the power.</summary>
    Negated,
    /// <summary>-(S)- power turns the tag on, and it stays on (latch).</summary>
    Set,
    /// <summary>-(R)- power turns the tag off (unlatch).</summary>
    Reset,
}

/// <summary>A coil writes the power it receives to a BOOL tag, and passes the power on.</summary>
public sealed class CoilElement : LadderElement
{
    public string Tag { get; set; } = "";
    public CoilKind Coil { get; set; }
}

/// <summary>
/// A box: a function or function block (TON, CTU, ADD, GT, MOVE, …). Its first input is the power
/// flowing in; the rest are named operands — a tag name or a literal such as T#2s or 10.
/// Function blocks keep their state in <see cref="Instance"/>, a tag of the block's type.
/// </summary>
public sealed class BoxElement : LadderElement
{
    public string Instruction { get; set; } = "";
    public string? Instance { get; set; }
    public Dictionary<string, string> Operands { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Reads and writes projects as JSON (camelCase, enums as words).</summary>
public static class ProjectJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(PlcProject project) => JsonSerializer.Serialize(project, Options);

    public static PlcProject Deserialize(string json) =>
        JsonSerializer.Deserialize<PlcProject>(json, Options) ?? throw new JsonException("The project file is empty.");
}
