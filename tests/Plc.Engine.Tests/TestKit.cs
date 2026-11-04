using Plc.Dialects.Iec;
using Plc.Engine;

namespace Plc.Engine.Tests;

/// <summary>Small builders so each test reads like the rung it tests.</summary>
internal static class K
{
    public static ContactElement No(string tag) => new() { Tag = tag, Contact = ContactKind.NormallyOpen };
    public static ContactElement Nc(string tag) => new() { Tag = tag, Contact = ContactKind.NormallyClosed };
    public static ContactElement P(string tag) => new() { Tag = tag, Contact = ContactKind.PositiveEdge };
    public static ContactElement N(string tag) => new() { Tag = tag, Contact = ContactKind.NegativeEdge };
    public static CoilElement Coil(string tag, CoilKind kind = CoilKind.Normal) => new() { Tag = tag, Coil = kind };
    public static SeriesElement S(params LadderElement[] items) => new() { Items = [.. items] };
    public static ParallelElement Par(params LadderElement[] branches) => new() { Branches = [.. branches] };

    public static BoxElement Box(string instruction, string? instance, params (string Name, string Value)[] operands)
    {
        var box = new BoxElement { Instruction = instruction, Instance = instance };
        foreach (var (n, v) in operands) box.Operands[n] = v;
        return box;
    }

    public static TagDeclaration Tag(string name, string type = "BOOL", IoDirection io = IoDirection.None, string? initial = null) =>
        new() { Name = name, Type = type, Io = io, Initial = initial };

    public static PlcProject Project(IEnumerable<TagDeclaration> tags, params LadderElement[] rungs) => new()
    {
        Name = "Test",
        Tags = [.. tags],
        Programs = [new ProgramUnit { Name = "Main", Rungs = [.. rungs.Select(r => new Rung { Logic = r })] }],
    };

    public static PlcRuntime Run(PlcProject project) => new(project, IecDialect.Instance);

    public static void Set(this PlcRuntime rt, string tag, bool value) => rt.Write(tag, PlcValue.FromBool(value));
    public static bool B(this PlcRuntime rt, string tag) => rt.Read(tag).AsBool;
    public static long L(this PlcRuntime rt, string tag) => rt.Read(tag).AsLong;

    public static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "PLCPlus.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Repo root not found.");
    }

    public static string Content(params string[] parts) =>
        File.ReadAllText(Path.Combine([RepoRoot(), "content", "plcplus-foundations", .. parts]));
}
