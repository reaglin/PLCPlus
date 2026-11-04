namespace Plc.Engine;

/// <summary>
/// The machine the PLC is wired to: a simulation scene (spec 5.3), or a grader's test bench.
/// </summary>
public interface IVirtualIo
{
    /// <summary>The value the machine presents on an input tag, or null if nothing is wired to it.</summary>
    PlcValue? ReadInput(string tag);

    /// <summary>The PLC drives an output tag (a light, a motor, a valve).</summary>
    void WriteOutput(string tag, PlcValue value);

    /// <summary>Moves the machine's physics on by one scan, on the same simulated clock.</summary>
    void Advance(long ms);
}

public enum RunState { Running, Faulted }

/// <summary>
/// Runs a project, one scan at a time, on a simulated clock (spec 4.2):
/// 1. copy the machine's inputs into the input tags; 2. run the programs in order;
/// 3. copy the output tags to the machine; 4. housekeeping — the clock moves on by the scan time.
/// Time never comes from the wall clock, so a program and a scenario always behave the same way.
/// </summary>
public sealed class PlcRuntime
{
    private readonly Dictionary<string, PlcValue> _tags = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DataType> _types = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FbInstance> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PlcValue> _forces = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<LadderElement, bool> _edgeMemory = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<LadderElement, bool> _powerOut = new(ReferenceEqualityComparer.Instance);
    private readonly List<TagDeclaration> _inputs;
    private readonly List<TagDeclaration> _outputs;

    public PlcProject Project { get; }
    public IDialect Dialect { get; }
    public IVirtualIo? Io { get; set; }

    public long NowMs { get; private set; }
    public long ScanCount { get; private set; }
    public RunState State { get; private set; } = RunState.Running;
    public PlcFault? Fault { get; private set; }

    /// <summary>Raised after every scan (for live view and traces).</summary>
    public event EventHandler? ScanCompleted;

    /// <summary>Builds a runtime. Throws <see cref="ProjectInvalidException"/> if the project has errors.</summary>
    public PlcRuntime(PlcProject project, IDialect dialect, IVirtualIo? io = null)
    {
        var diagnostics = ProjectValidator.Validate(project, dialect);
        if (diagnostics.Any(d => d.Severity == Severity.Error))
            throw new ProjectInvalidException(diagnostics);

        Project = project;
        Dialect = dialect;
        Io = io;
        _inputs = project.Tags.Where(t => t.Io == IoDirection.Input).ToList();
        _outputs = project.Tags.Where(t => t.Io == IoDirection.Output).ToList();
        Reset();
    }

    /// <summary>Back to power-up: tags to their initial values, timers and counters cleared, clock at zero.</summary>
    public void Reset()
    {
        _tags.Clear();
        _types.Clear();
        _instances.Clear();
        _edgeMemory.Clear();
        _powerOut.Clear();
        foreach (var tag in Project.Tags)
        {
            if (PlcValue.TryParseType(tag.Type, out var type))
            {
                _types[tag.Name] = type;
                var value = PlcValue.Default(type);
                if (tag.Initial is not null && PlcValue.TryParseLiteral(tag.Initial, out var initial))
                    value = initial.ConvertTo(type);
                _tags[tag.Name] = value;
            }
            else
            {
                _instances[tag.Name] = new FbInstance(tag.Name, tag.Type.ToUpperInvariant());
            }
        }
        NowMs = 0;
        ScanCount = 0;
        State = RunState.Running;
        Fault = null;
    }

    // ---- Tags -------------------------------------------------------------------------------

    /// <summary>Reads a tag, or a function block member such as T1.Q.</summary>
    public PlcValue Read(string name)
    {
        if (_tags.TryGetValue(name, out var v)) return v;
        var dot = name.IndexOf('.', StringComparison.Ordinal);
        if (dot > 0 && _instances.TryGetValue(name[..dot], out var fb))
        {
            var member = name[(dot + 1)..];
            var spec = Dialect.Find(fb.Type)?.Members.FirstOrDefault(m => m.Name.Equals(member, StringComparison.OrdinalIgnoreCase));
            if (spec is not null) return fb.Get(member, PlcValue.Default(spec.Type));
        }
        throw new PlcFaultException(FaultKind.Configuration, $"There is no tag called '{name}'.");
    }

    public DataType TypeOf(string name)
    {
        if (_types.TryGetValue(name, out var t)) return t;
        return Read(name).Type;
    }

    /// <summary>Writes a tag; the value is converted to the tag's declared type.</summary>
    public void Write(string name, PlcValue value)
    {
        if (!_types.TryGetValue(name, out var type))
            throw new PlcFaultException(FaultKind.Configuration, $"'{name}' cannot be written to.");
        _tags[name] = value.ConvertTo(type);
    }

    public IReadOnlyDictionary<string, PlcValue> Tags => _tags;
    public FbInstance? InstanceOf(string name) => _instances.GetValueOrDefault(name);

    // ---- Forcing ------------------------------------------------------------------------------

    /// <summary>Forces an input or output: the PLC sees (input) or drives (output) this value whatever happens.</summary>
    public void Force(string tag, PlcValue value) => _forces[tag] = value;
    public void Unforce(string tag) => _forces.Remove(tag);
    public void UnforceAll() => _forces.Clear();
    public IReadOnlyDictionary<string, PlcValue> Forces => _forces;

    // ---- Live view ----------------------------------------------------------------------------

    /// <summary>Whether power flowed out of an element in the last scan (the live power-flow view).</summary>
    public bool PowerOut(LadderElement element) => _powerOut.TryGetValue(element, out var p) && p;

    // ---- The scan -----------------------------------------------------------------------------

    /// <summary>One scan. A faulted PLC does nothing until <see cref="Reset"/>, as a real one stays stopped.</summary>
    public void Scan()
    {
        if (State == RunState.Faulted) return;

        // 1. Inputs into the input image.
        foreach (var tag in _inputs)
        {
            PlcValue? v = _forces.TryGetValue(tag.Name, out var forced) ? forced : Io?.ReadInput(tag.Name);
            if (v is { } value) _tags[tag.Name] = value.ConvertTo(_types[tag.Name]);
        }

        // 2. The programs, in task order.
        string program = "";
        int rung = 0;
        try
        {
            foreach (var unit in Project.Programs)
            {
                program = unit.Name;
                for (rung = 0; rung < unit.Rungs.Count; rung++)
                    Evaluate(unit.Rungs[rung].Logic, true);
            }
        }
        catch (PlcFaultException ex)
        {
            State = RunState.Faulted;
            Fault = new PlcFault(ex.Kind, ex.Message, program, rung + 1, NowMs);
        }

        // 3. The output image to the machine. A faulted PLC turns its outputs off.
        foreach (var tag in _outputs)
        {
            var value = State == RunState.Faulted
                ? PlcValue.Default(_types[tag.Name])
                : _forces.TryGetValue(tag.Name, out var forced) ? forced : _tags[tag.Name];
            Io?.WriteOutput(tag.Name, value);
        }

        // 4. Housekeeping: the clock moves on, and the machine with it.
        NowMs += Project.ScanTimeMs;
        ScanCount++;
        Io?.Advance(Project.ScanTimeMs);
        ScanCompleted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Runs scans until the clock has moved on by at least <paramref name="ms"/>.</summary>
    public void RunFor(long ms)
    {
        var until = NowMs + ms;
        while (NowMs < until) Scan();
    }

    private bool Evaluate(LadderElement element, bool power)
    {
        bool result;
        switch (element)
        {
            case SeriesElement s:
                result = power;
                foreach (var item in s.Items) result = Evaluate(item, result);
                break;

            case ParallelElement p:
                result = false;
                foreach (var branch in p.Branches) result |= Evaluate(branch, power); // every branch runs
                break;

            case ContactElement c:
                result = power && ContactPasses(c);
                break;

            case CoilElement coil:
                switch (coil.Coil)
                {
                    case CoilKind.Normal: Write(coil.Tag, PlcValue.FromBool(power)); break;
                    case CoilKind.Negated: Write(coil.Tag, PlcValue.FromBool(!power)); break;
                    case CoilKind.Set: if (power) Write(coil.Tag, PlcValue.True); break;
                    case CoilKind.Reset: if (power) Write(coil.Tag, PlcValue.False); break;
                }
                result = power;
                break;

            case BoxElement box:
                var instruction = Dialect.Find(box.Instruction)
                    ?? throw new PlcFaultException(FaultKind.Configuration, $"Unknown instruction {box.Instruction}.");
                var instance = box.Instance is null ? null : _instances.GetValueOrDefault(box.Instance);
                result = instruction.Execute(new InstructionContext(this, box, instance), power);
                break;

            default:
                throw new PlcFaultException(FaultKind.Configuration, $"Unknown element {element.GetType().Name}.");
        }
        _powerOut[element] = result;
        return result;
    }

    private bool ContactPasses(ContactElement c)
    {
        var value = Read(c.Tag).AsBool;
        switch (c.Contact)
        {
            case ContactKind.NormallyOpen: return value;
            case ContactKind.NormallyClosed: return !value;
            default:
                var before = _edgeMemory.TryGetValue(c, out var b) && b;
                _edgeMemory[c] = value;
                return c.Contact == ContactKind.PositiveEdge ? value && !before : !value && before;
        }
    }
}

public sealed class ProjectInvalidException(IReadOnlyList<Diagnostic> diagnostics)
    : Exception("The program has errors: " + string.Join(" ", diagnostics.Where(d => d.Severity == Severity.Error).Select(d => d.Message)))
{
    public IReadOnlyList<Diagnostic> Diagnostics { get; } = diagnostics;
}
