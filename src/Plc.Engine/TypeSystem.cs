using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Plc.Engine;

/// <summary>A structure type declared in the project (spec 4.1): named members, each of any type.</summary>
public sealed class StructType
{
    public string Name { get; set; } = "";
    public List<TagDeclaration> Members { get; set; } = [];
}

/// <summary>A parsed tag type: elementary, ARRAY[lo..hi] OF t, a structure, or a function block.</summary>
public abstract record TypeRef
{
    public sealed record Elementary(DataType Type) : TypeRef;
    public sealed record Array(long Low, long High, TypeRef Element) : TypeRef;
    public sealed record Struct(StructType Definition) : TypeRef;
    public sealed record FunctionBlock(string Name) : TypeRef;
}

/// <summary>
/// Arrays and structures, kept simple for the engine: every element and member is stored as its own
/// scalar under a canonical name (<c>Levels[3]</c>, <c>Pump.Speed</c>, <c>Pumps[2].Speed</c>), and a
/// name with a variable index (<c>Levels[i]</c>) is turned into the canonical name when it is read or
/// written — with an index-out-of-range fault, as a real PLC raises, when the index is outside the array.
/// </summary>
public sealed class TypeSystem
{
    private static readonly Regex ArrayType = new(
        @"^\s*ARRAY\s*\[\s*(-?\d+)\s*\.\.\s*(-?\d+)\s*\]\s*OF\s+(.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly Dictionary<string, StructType> _structs;
    private readonly IDialect _dialect;

    public TypeSystem(PlcProject project, IDialect dialect)
    {
        _structs = new(StringComparer.OrdinalIgnoreCase);
        foreach (var s in project.Types) _structs.TryAdd(s.Name, s);
        _dialect = dialect;
    }

    /// <summary>Parses a type name, or returns null with a reason a beginner can act on.</summary>
    public TypeRef? Parse(string? text, out string? error) => Parse(text, out error, depth: 0);

    private TypeRef? Parse(string? text, out string? error, int depth)
    {
        error = null;
        text = text?.Trim() ?? "";
        if (depth > 8) { error = "Structures inside structures go too deep (or one contains itself)."; return null; }
        if (PlcValue.TryParseType(text, out var elementary)) return new TypeRef.Elementary(elementary);

        var m = ArrayType.Match(text);
        if (m.Success)
        {
            var lo = long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var hi = long.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            if (hi < lo) { error = $"In '{text}' the last index is smaller than the first. Write it as ARRAY[0..9] OF INT."; return null; }
            if (hi - lo >= 10_000) { error = $"'{text}' is too big: an array can have at most 10,000 elements."; return null; }
            var element = Parse(m.Groups[3].Value, out error, depth + 1);
            if (element is null) return null;
            if (element is TypeRef.Array or TypeRef.FunctionBlock)
            {
                error = $"'{text}': an array's elements must be BOOL, INT, DINT, REAL, TIME or a structure.";
                return null;
            }
            return new TypeRef.Array(lo, hi, element);
        }

        if (_structs.TryGetValue(text, out var s))
        {
            foreach (var member in s.Members)
            {
                var mt = Parse(member.Type, out error, depth + 1);
                if (mt is null) return null;
                if (mt is TypeRef.FunctionBlock) { error = $"Structure {s.Name}: a member cannot be a timer or counter."; return null; }
            }
            return new TypeRef.Struct(s);
        }

        if (_dialect.Find(text)?.FunctionBlockType is not null) return new TypeRef.FunctionBlock(text.ToUpperInvariant());

        error = $"'{text}' is not a type this PLC knows. Use BOOL, INT, DINT, REAL, TIME, ARRAY[0..9] OF INT, a structure you declared, or a timer or counter type such as TON.";
        return null;
    }

    /// <summary>Every scalar a tag of this type is stored as: canonical name and type.</summary>
    public IEnumerable<(string Name, DataType Type)> Flatten(string prefix, TypeRef type)
    {
        switch (type)
        {
            case TypeRef.Elementary e:
                yield return (prefix, e.Type);
                break;
            case TypeRef.Array a:
                for (var i = a.Low; i <= a.High; i++)
                    foreach (var x in Flatten($"{prefix}[{i.ToString(CultureInfo.InvariantCulture)}]", a.Element)) yield return x;
                break;
            case TypeRef.Struct s:
                foreach (var m in s.Definition.Members)
                {
                    var mt = Parse(m.Type, out _) ?? throw new InvalidOperationException($"{s.Definition.Name}.{m.Name}: bad type.");
                    foreach (var x in Flatten($"{prefix}.{m.Name}", mt)) yield return x;
                }
                break;
        }
    }

    // ---- Paths: Name, Name[index], Name.member, combined ------------------------------------------

    /// <summary>One step of a path: a member name, or an index (a literal or a tag path).</summary>
    public abstract record Step
    {
        public sealed record Member(string Name) : Step;
        public sealed record Index(long? Literal, string? Tag) : Step;
    }

    /// <summary>Splits a path such as <c>Pumps[i].Speed</c> into its root and steps, or returns null if it is not a path.</summary>
    public static (string Root, List<Step> Steps)? ParsePath(string path)
    {
        var i = 0;
        string Ident()
        {
            var start = i;
            while (i < path.Length && (char.IsLetterOrDigit(path[i]) || path[i] == '_')) i++;
            return path[start..i];
        }

        path = path.Trim();
        var root = Ident();
        if (root.Length == 0 || char.IsDigit(root[0])) return null;
        var steps = new List<Step>();
        while (i < path.Length)
        {
            if (path[i] == '.')
            {
                i++;
                var name = Ident();
                if (name.Length == 0) return null;
                steps.Add(new Step.Member(name));
            }
            else if (path[i] == '[')
            {
                var close = FindClose(path, i);
                if (close < 0) return null;
                var inner = path[(i + 1)..close].Trim();
                i = close + 1;
                if (long.TryParse(inner, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var lit))
                    steps.Add(new Step.Index(lit, null));
                else if (ParsePath(inner) is not null)
                    steps.Add(new Step.Index(null, inner));
                else
                    return null;
            }
            else return null;
        }
        return (root, steps);
    }

    private static int FindClose(string s, int open)
    {
        var depth = 0;
        for (var j = open; j < s.Length; j++)
        {
            if (s[j] == '[') depth++;
            else if (s[j] == ']' && --depth == 0) return j;
        }
        return -1;
    }

    /// <summary>True when the path has an index that is a tag (so it must be resolved every time).</summary>
    public static bool HasVariableIndex(string path) => path.Contains('[', StringComparison.Ordinal) &&
        (ParsePath(path)?.Steps.Any(s => s is Step.Index { Tag: not null }) ?? false);

    /// <summary>
    /// Turns a path into its canonical stored name, reading variable indexes with <paramref name="readIndex"/>
    /// and checking bounds against the declared types. Throws an index-out-of-range fault.
    /// </summary>
    public string Canonical(string path, IReadOnlyDictionary<string, TypeRef> roots, Func<string, long> readIndex)
    {
        var parsed = ParsePath(path) ?? throw new PlcFaultException(FaultKind.Configuration, $"'{path}' is not a tag name.");
        if (!roots.TryGetValue(parsed.Root, out var type))
            throw new PlcFaultException(FaultKind.Configuration, $"There is no tag called '{parsed.Root}'.");
        var sb = new StringBuilder(parsed.Root);
        foreach (var step in parsed.Steps)
        {
            switch (step, type)
            {
                case (Step.Index ix, TypeRef.Array a):
                    var index = ix.Literal ?? readIndex(ix.Tag!);
                    if (index < a.Low || index > a.High)
                        throw new PlcFaultException(FaultKind.IndexOutOfRange,
                            $"'{path}' asked for element {index}, but {sb} only has elements {a.Low} to {a.High}. A real PLC stops here too: check the index before using it.");
                    sb.Append('[').Append(index.ToString(CultureInfo.InvariantCulture)).Append(']');
                    type = a.Element;
                    break;
                case (Step.Member m, TypeRef.Struct s):
                    var member = s.Definition.Members.FirstOrDefault(x => x.Name.Equals(m.Name, StringComparison.OrdinalIgnoreCase))
                        ?? throw new PlcFaultException(FaultKind.Configuration, $"{s.Definition.Name} has no member called {m.Name}.");
                    sb.Append('.').Append(member.Name);
                    type = Parse(member.Type, out _) ?? throw new PlcFaultException(FaultKind.Configuration, $"Bad type on {member.Name}.");
                    break;
                default:
                    throw new PlcFaultException(FaultKind.Configuration, $"'{path}' does not fit the type of {sb}.");
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// The type a path refers to, for the validator, or an error a beginner can act on.
    /// Literal indexes are bounds-checked here; tag indexes must be whole-number tags.
    /// </summary>
    public TypeRef? TypeOfPath(string path, IReadOnlyDictionary<string, TypeRef> roots, out string? error)
    {
        error = null;
        var parsed = ParsePath(path);
        if (parsed is null) { error = $"'{path}' is not a tag name."; return null; }
        if (!roots.TryGetValue(parsed.Value.Root, out var type)) return null;
        var sb = new StringBuilder(parsed.Value.Root);
        foreach (var step in parsed.Value.Steps)
        {
            switch (step, type)
            {
                case (Step.Index ix, TypeRef.Array a):
                    if (ix.Literal is { } lit && (lit < a.Low || lit > a.High))
                    {
                        error = $"'{path}' uses element {lit}, but {sb} only has elements {a.Low} to {a.High}.";
                        return null;
                    }
                    if (ix.Tag is { } tagIndex)
                    {
                        var it = TypeOfPath(tagIndex, roots, out error);
                        if (it is not TypeRef.Elementary { Type: DataType.Int or DataType.DInt })
                        {
                            error ??= $"The index '{tagIndex}' in '{path}' must be a whole-number tag (INT or DINT).";
                            return null;
                        }
                    }
                    sb.Append('[').Append(ix.Literal?.ToString(CultureInfo.InvariantCulture) ?? ix.Tag).Append(']');
                    type = a.Element;
                    break;
                case (Step.Member m, TypeRef.Struct s):
                    var member = s.Definition.Members.FirstOrDefault(x => x.Name.Equals(m.Name, StringComparison.OrdinalIgnoreCase));
                    if (member is null) { error = $"'{path}': {s.Definition.Name} has no member called {m.Name}."; return null; }
                    sb.Append('.').Append(member.Name);
                    type = Parse(member.Type, out error);
                    if (type is null) return null;
                    break;
                case (Step.Index, _):
                    error = $"'{path}': {sb} is not an array, so it has no [index].";
                    return null;
                default:
                    error = $"'{path}': {sb} has no members.";
                    return null;
            }
        }
        return type;
    }
}
