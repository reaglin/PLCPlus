namespace Plc.Engine;

public enum Severity { Error, Warning }

/// <summary>
/// A problem in a program, pointing to the exact rung and element (spec 5.1), written for a beginner.
/// <see cref="ElementPath"/> is the position inside the rung, such as "1.2" (first item, its second branch).
/// </summary>
public sealed record Diagnostic(Severity Severity, string Program, int Rung, string ElementPath, string Message)
{
    public override string ToString() => $"{Program}, rung {Rung}: {Message}";
}

/// <summary>Checks a project before it runs: every tag exists, every type fits, every box has what it needs.</summary>
public static class ProjectValidator
{
    public static IReadOnlyList<Diagnostic> Validate(PlcProject project, IDialect dialect)
    {
        var list = new List<Diagnostic>();
        var tags = new Dictionary<string, TagDeclaration>(StringComparer.OrdinalIgnoreCase);
        var roots = new Dictionary<string, TypeRef>(StringComparer.OrdinalIgnoreCase);
        var types = new TypeSystem(project, dialect);

        var structNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in project.Types)
        {
            if (string.IsNullOrWhiteSpace(s.Name) || PlcValue.TryParseType(s.Name, out _) || dialect.Find(s.Name) is not null)
                list.Add(new(Severity.Error, "Types", 0, "", $"'{s.Name}' cannot be the name of a structure: choose a name that is not a type or instruction."));
            else if (!structNames.Add(s.Name))
                list.Add(new(Severity.Error, "Types", 0, "", $"There are two structures called '{s.Name}'."));
            else if (types.Parse(s.Name, out var structError) is null)
                list.Add(new(Severity.Error, "Types", 0, "", structError!));
        }

        foreach (var tag in project.Tags)
        {
            if (string.IsNullOrWhiteSpace(tag.Name))
            {
                list.Add(new(Severity.Error, "Tags", 0, "", "A tag has no name. Give every tag a name, such as Start or Motor."));
                continue;
            }
            if (!tags.TryAdd(tag.Name, tag))
                list.Add(new(Severity.Error, "Tags", 0, "", $"There are two tags called '{tag.Name}'. Each tag needs its own name."));
            var typeRef = types.Parse(tag.Type, out var typeError);
            if (typeRef is null)
            {
                list.Add(new(Severity.Error, "Tags", 0, "", $"The tag '{tag.Name}': {typeError}"));
                continue;
            }
            roots[tag.Name] = typeRef;
            var isElementary = typeRef is TypeRef.Elementary;
            if (tag.Io != IoDirection.None && !isElementary)
                list.Add(new(Severity.Error, "Tags", 0, "", $"'{tag.Name}' is wired to the machine, so it must be a single value (BOOL), not {tag.Type}."));
            else if (tag.Io != IoDirection.None && typeRef is TypeRef.Elementary { Type: not DataType.Bool })
                list.Add(new(Severity.Warning, "Tags", 0, "", $"'{tag.Name}' is wired to the machine but is not BOOL. Analog I/O comes in a later unit."));
            if (tag.Initial is not null && !isElementary)
                list.Add(new(Severity.Error, "Tags", 0, "", $"'{tag.Name}' is {tag.Type}: only a single value (BOOL, INT, DINT, REAL, TIME) can have a starting value. Arrays and structures start at zero."));
            else if (tag.Initial is not null && !PlcValue.TryParseLiteral(tag.Initial, out _))
                list.Add(new(Severity.Error, "Tags", 0, "", $"The starting value of '{tag.Name}' ('{tag.Initial}') is not a value this PLC understands."));
        }

        // Tags something writes: coils, and boxes' outputs (a later rung may use what an earlier one wrote).
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rung in project.Programs.SelectMany(p => p.Rungs)) CollectWrites(rung.Logic, written, dialect);

        foreach (var unit in project.Programs)
        {
            for (var r = 0; r < unit.Rungs.Count; r++)
            {
                var ctx = new Ctx(list, unit.Name, r + 1, tags, roots, types, dialect) { Written = written };
                Walk(unit.Rungs[r].Logic, "1", ctx);
                if (!HasOutput(unit.Rungs[r].Logic))
                    list.Add(new(Severity.Warning, unit.Name, r + 1, "", "This rung has no coil or box, so it does nothing. Add a coil at the right-hand end."));
            }
        }
        return list;
    }

    private sealed record Ctx(List<Diagnostic> List, string Program, int Rung, Dictionary<string, TagDeclaration> Tags,
        Dictionary<string, TypeRef> Roots, TypeSystem Types, IDialect Dialect)
    {
        public HashSet<string> Written { get; init; } = [];
        public void Error(string path, string message) => List.Add(new(Severity.Error, Program, Rung, path, message));
        public void Warn(string path, string message) => List.Add(new(Severity.Warning, Program, Rung, path, message));
    }

    private static void CollectWrites(LadderElement e, HashSet<string> written, IDialect dialect)
    {
        switch (e)
        {
            case SeriesElement s: foreach (var i in s.Items) CollectWrites(i, written, dialect); break;
            case ParallelElement p: foreach (var b in p.Branches) CollectWrites(b, written, dialect); break;
            case CoilElement c: written.Add(c.Tag); break;
            case BoxElement b:
                foreach (var op in dialect.Find(b.Instruction)?.Operands.Where(o => o.Direction == OperandDirection.Out) ?? [])
                    if (b.Operands.TryGetValue(op.Name, out var target)) written.Add(target);
                break;
        }
    }

    private static bool HasOutput(LadderElement e) => e switch
    {
        CoilElement or BoxElement => true,
        SeriesElement s => s.Items.Any(HasOutput),
        ParallelElement p => p.Branches.Any(HasOutput),
        _ => false,
    };

    private static void Walk(LadderElement element, string path, Ctx ctx)
    {
        switch (element)
        {
            case SeriesElement s:
                for (var i = 0; i < s.Items.Count; i++) Walk(s.Items[i], $"{path}.{i + 1}", ctx);
                break;
            case ParallelElement p:
                if (p.Branches.Count < 2) ctx.Warn(path, "A branch needs at least two paths side by side.");
                for (var i = 0; i < p.Branches.Count; i++) Walk(p.Branches[i], $"{path}.{i + 1}", ctx);
                break;
            case ContactElement c:
                CheckBoolTag(c.Tag, "contact", path, ctx, write: false);
                // A memory tag that nothing ever turns on: most often a typo of a wired tag's name.
                if (ctx.Tags.TryGetValue(c.Tag, out var read) && read.Io == IoDirection.None && read.Initial is null
                    && read.Type.Equals("BOOL", StringComparison.OrdinalIgnoreCase) && !ctx.Written.Contains(c.Tag))
                    ctx.Warn(path, $"Nothing ever turns '{c.Tag}' on: no coil writes it and it is not wired to the machine, so this contact " +
                        (c.Contact == ContactKind.NormallyClosed ? "always passes power" : "never passes power") +
                        ". Check the spelling against the machine's wiring.");
                break;
            case CoilElement coil:
                CheckBoolTag(coil.Tag, "coil", path, ctx, write: true);
                break;
            case BoxElement box:
                CheckBox(box, path, ctx);
                break;
        }
    }

    private static void CheckBoolTag(string name, string what, string path, Ctx ctx, bool write)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ctx.Error(path, $"This {what} has no tag. Type the name of the tag it {(write ? "turns on" : "checks")}.");
            return;
        }
        var type = TypeOfOperand(name, ctx, out var isMember, out var pathError);
        if (type is null)
        {
            ctx.Error(path, pathError is not null
                ? $"The {what}: {pathError}"
                : $"The {what} uses '{name}', but there is no tag with that name. Declare it in the tag list, or check the spelling.");
            return;
        }
        if (type != DataType.Bool)
            ctx.Error(path, $"The {what} uses '{name}', which is {type.Value.ToString().ToUpperInvariant()}. A {what} needs a BOOL (on/off) tag.");
        if (write && isMember)
            ctx.Error(path, $"A coil cannot write to '{name}': it belongs to a timer or counter, which sets it itself.");
        if (write && ctx.Tags.TryGetValue(name, out var tag) && tag.Io == IoDirection.Input)
            ctx.Warn(path, $"The coil writes to '{name}', which is an input. The next scan copies the machine's value over it, so this coil has no effect.");
    }

    private static void CheckBox(BoxElement box, string path, Ctx ctx)
    {
        var instruction = ctx.Dialect.Find(box.Instruction);
        if (instruction is null)
        {
            ctx.Error(path, $"'{box.Instruction}' is not an instruction this PLC knows.");
            return;
        }
        if (instruction.FunctionBlockType is { } fbType)
        {
            if (string.IsNullOrWhiteSpace(box.Instance))
                ctx.Error(path, $"{instruction.Name} needs a tag to keep its state in. Declare a tag of type {fbType} (for example T1) and name it here.");
            else if (!ctx.Tags.TryGetValue(box.Instance, out var tag))
                ctx.Error(path, $"{instruction.Name} uses '{box.Instance}', but there is no tag with that name. Declare it with the type {fbType}.");
            else if (!tag.Type.Equals(fbType, StringComparison.OrdinalIgnoreCase))
                ctx.Error(path, $"{instruction.Name} uses '{box.Instance}', which is a {tag.Type}. It needs a tag of type {fbType}.");
        }
        foreach (var spec in instruction.Operands)
        {
            box.Operands.TryGetValue(spec.Name, out var text);
            if (string.IsNullOrWhiteSpace(text))
            {
                if (spec.Required) ctx.Error(path, $"{instruction.Name} needs {spec.Name}: {spec.Help}");
                continue;
            }
            DataType? type;
            if (PlcValue.TryParseLiteral(text, out var literal))
            {
                if (spec.Direction == OperandDirection.Out)
                {
                    ctx.Error(path, $"{instruction.Name}'s {spec.Name} must be a tag to write the result to, not the value '{text}'.");
                    continue;
                }
                type = literal.Type;
            }
            else
            {
                type = TypeOfOperand(text, ctx, out var isMember, out var pathError);
                if (type is null)
                {
                    ctx.Error(path, pathError is not null
                        ? $"{instruction.Name}'s {spec.Name}: {pathError}"
                        : $"{instruction.Name}'s {spec.Name} uses '{text}', but there is no tag with that name.");
                    continue;
                }
                if (spec.Direction == OperandDirection.Out && isMember)
                    ctx.Error(path, $"{instruction.Name} cannot write its {spec.Name} to '{text}', which belongs to a timer or counter.");
            }
            if (!Fits(spec.Kind, type.Value))
                ctx.Error(path, $"{instruction.Name}'s {spec.Name} needs {Describe(spec.Kind)}, but '{text}' is {type.Value.ToString().ToUpperInvariant()}.");
        }
        foreach (var name in box.Operands.Keys)
            if (!instruction.Operands.Any(o => o.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                ctx.Warn(path, $"{instruction.Name} has no input or output called {name}; it is ignored.");
    }

    private static DataType? TypeOfOperand(string name, Ctx ctx, out bool isMember, out string? error)
    {
        isMember = false;
        error = null;
        name = name.Trim();

        // A timer or counter member, such as T1.Q.
        var dot = name.IndexOf('.', StringComparison.Ordinal);
        if (dot > 0 && !name.Contains('[', StringComparison.Ordinal)
            && ctx.Roots.TryGetValue(name[..dot], out var root) && root is TypeRef.FunctionBlock fb)
        {
            var member = ctx.Dialect.Find(fb.Name)?.Members
                .FirstOrDefault(m => m.Name.Equals(name[(dot + 1)..], StringComparison.OrdinalIgnoreCase));
            isMember = member is not null;
            if (member is null) error = $"'{name}': a {fb.Name} has no member called {name[(dot + 1)..]}.";
            return member?.Type;
        }

        // A tag, an array element or a structure member.
        var type = ctx.Types.TypeOfPath(name, ctx.Roots, out var pathError);
        error = pathError;
        switch (type)
        {
            case TypeRef.Elementary e:
                return e.Type;
            case null:
                return null;
            default:
                error = $"'{name}' is a whole {Describe(type)}. Use one element or member of it, such as {Example(name, type)}.";
                return null;
        }
    }

    private static string Describe(TypeRef type) => type switch
    {
        TypeRef.Array => "array",
        TypeRef.Struct s => $"structure ({s.Definition.Name})",
        TypeRef.FunctionBlock f => $"{f.Name}",
        _ => "value",
    };

    private static string Example(string name, TypeRef type) => type switch
    {
        TypeRef.Array a => $"{name}[{a.Low}]",
        TypeRef.Struct s when s.Definition.Members.Count > 0 => $"{name}.{s.Definition.Members[0].Name}",
        TypeRef.FunctionBlock f when f.Name.StartsWith('C') => $"{name}.CV",
        _ => $"{name}.Q",
    };

    private static bool Fits(OperandKind kind, DataType type) => kind switch
    {
        OperandKind.Bool => type == DataType.Bool,
        OperandKind.Number => type is DataType.Int or DataType.DInt or DataType.Real,
        OperandKind.Integer => type is DataType.Int or DataType.DInt,
        OperandKind.Time => type == DataType.Time,
        _ => true,
    };

    private static string Describe(OperandKind kind) => kind switch
    {
        OperandKind.Bool => "a BOOL (on/off)",
        OperandKind.Number => "a number (INT, DINT or REAL)",
        OperandKind.Integer => "a whole number (INT or DINT)",
        OperandKind.Time => "a time, written like T#2s (2 seconds) or T#500ms",
        _ => "a value",
    };
}
