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
    private readonly Dictionary<string, TypeRef> _roots = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<(string Program, int Rung)> _breakpoints = [];
    private readonly TypeSystem _typeSystem;
    private readonly List<TagDeclaration> _inputs;
    private readonly List<TagDeclaration> _outputs;

    public PlcProject Project { get; }
    public IDialect Dialect { get; }
    public IVirtualIo? Io { get; set; }

    public long NowMs { get; private set; }
    /// <summary>The time at which the last scan ran (NowMs has already moved on by the scan time).</summary>
    public long LastScanStartMs { get; private set; }
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
        _typeSystem = new TypeSystem(project, dialect);
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
        _roots.Clear();
        foreach (var tag in Project.Tags)
        {
            var typeRef = _typeSystem.Parse(tag.Type, out _)!; // the validator has checked every type
            _roots[tag.Name] = typeRef;
            if (typeRef is TypeRef.FunctionBlock fb)
            {
                _instances[tag.Name] = new FbInstance(tag.Name, fb.Name);
                continue;
            }
            foreach (var (name, type) in _typeSystem.Flatten(tag.Name, typeRef))
            {
                _types[name] = type;
                _tags[name] = PlcValue.Default(type);
            }
            if (typeRef is TypeRef.Elementary e && tag.Initial is not null && PlcValue.TryParseLiteral(tag.Initial, out var initial))
                _tags[tag.Name] = initial.ConvertTo(e.Type);
        }
        NowMs = 0;
        LastScanStartMs = 0;
        ScanCount = 0;
        State = RunState.Running;
        Fault = null;
    }

    // ---- Tags -------------------------------------------------------------------------------

    /// <summary>Reads a tag, or a function block member such as T1.Q.</summary>
    public PlcValue Read(string name)
    {
        name = Resolve(name);
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
        name = Resolve(name);
        if (_types.TryGetValue(name, out var t)) return t;
        return Read(name).Type;
    }

    /// <summary>Writes a tag; the value is converted to the tag's declared type.</summary>
    public void Write(string name, PlcValue value)
    {
        name = Resolve(name);
        if (!_types.TryGetValue(name, out var type))
            throw new PlcFaultException(FaultKind.Configuration, $"'{name}' cannot be written to.");
        _tags[name] = value.ConvertTo(type);
    }

    /// <summary>
    /// The stored name of a tag path: <c>Levels[i]</c> becomes <c>Levels[3]</c> when i is 3. An index outside
    /// the array raises an index-out-of-range fault.
    /// </summary>
    public string Resolve(string name) =>
        name.Contains('[', StringComparison.Ordinal)
            ? _typeSystem.Canonical(name, _roots, index => Read(index).AsLong)
            : name;

    /// <summary>Every stored value, by canonical name (array elements and structure members each have their own).</summary>
    public IReadOnlyDictionary<string, PlcValue> Tags => _tags;
    public FbInstance? InstanceOf(string name) => _instances.GetValueOrDefault(name);

    // ---- Forcing ------------------------------------------------------------------------------

    /// <summary>Forces an input or output: the PLC sees (input) or drives (output) this value whatever happens.</summary>
    public void Force(string tag, PlcValue value) => _forces[tag] = value;
    public void Unforce(string tag) => _forces.Remove(tag);
    public void UnforceAll() => _forces.Clear();
    public IReadOnlyDictionary<string, PlcValue> Forces => _forces;

    // ---- Breakpoints (spec 4.2: run-to-breakpoint) ----------------------------------------------
    // A breakpoint is on a rung. Running to it stops at the end of the first scan in which that rung
    // passed power to its outputs, so the learner sees the whole machine at that moment.

    public void SetBreakpoint(string program, int rung) => _breakpoints.Add((program, rung));
    public void ClearBreakpoint(string program, int rung) => _breakpoints.Remove((program, rung));
    public void ClearBreakpoints() => _breakpoints.Clear();
    public IReadOnlyCollection<(string Program, int Rung)> Breakpoints => _breakpoints;

    /// <summary>The breakpoint rung that passed power in the last scan, if any (1-based rung number).</summary>
    public (string Program, int Rung)? BreakpointHit { get; private set; }

    /// <summary>
    /// Runs scans until a breakpoint rung passes power, the PLC faults, or <paramref name="maxMs"/> of simulated
    /// time has gone by. Returns true if it stopped at a breakpoint.
    /// </summary>
    public bool RunToBreakpoint(long maxMs)
    {
        var until = NowMs + maxMs;
        while (NowMs < until && State == RunState.Running)
        {
            Scan();
            if (BreakpointHit is not null) return true;
        }
        return false;
    }

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
        BreakpointHit = null;
        try
        {
            foreach (var unit in Project.Programs)
            {
                program = unit.Name;
                for (rung = 0; rung < unit.Rungs.Count; rung++)
                {
                    var passed = Evaluate(unit.Rungs[rung].Logic, true);
                    if (passed && BreakpointHit is null && _breakpoints.Count > 0 && _breakpoints.Contains((unit.Name, rung + 1)))
                        BreakpointHit = (unit.Name, rung + 1);
                }
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
        LastScanStartMs = NowMs;
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
