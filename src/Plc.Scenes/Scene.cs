using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Plc.Engine;

namespace Plc.Scenes;

/// <summary>
/// A simulated machine wired to the PLC's virtual I/O (spec 5.3): components laid out on a 2D panel,
/// each exposing typed I/O points mapped to PLC tags. Physics runs on the engine's simulated clock.
/// A scene is a JSON file — content, not code.
/// </summary>
public sealed class Scene : IVirtualIo
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>The size of the drawing, in device-independent units.</summary>
    public double Width { get; set; } = 440;
    public double Height { get; set; } = 360;
    public List<SceneComponent> Components { get; set; } = [];
    /// <summary>Wiring faults for troubleshooting lessons (spec 5.3): applied to inputs as the PLC reads them.</summary>
    public List<WiringFault> WiringFaults { get; set; } = [];

    public SceneComponent? Find(string id) =>
        Components.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public T? Find<T>(string? id) where T : SceneComponent => id is null ? null : Find(id) as T;

    /// <summary>The I/O list a learner is given: every tag the scene reads or drives.</summary>
    public IEnumerable<IoPoint> IoList => Components.SelectMany(c => c.Points);

    public PlcValue? ReadInput(string tag)
    {
        var fault = WiringFaults.FirstOrDefault(f => f.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));
        if (fault?.Kind == WiringFaultKind.SwappedWith && fault.Other is not null)
            tag = fault.Other;
        PlcValue? value = null;
        foreach (var c in Components)
            if (c.ReadInput(tag) is { } v) { value = v; break; }
        return fault?.Kind switch
        {
            WiringFaultKind.Open when value is not null => PlcValue.False,
            WiringFaultKind.Shorted when value is not null => PlcValue.True,
            _ => value,
        };
    }

    public void WriteOutput(string tag, PlcValue value)
    {
        foreach (var c in Components) c.WriteOutput(tag, value);
    }

    public void Advance(long ms)
    {
        foreach (var c in Components) c.Advance(ms);
    }

    /// <summary>Operates an operator input (press or release a push button, turn a switch, block a beam).</summary>
    public void Operate(string componentId, bool active)
    {
        if (Find(componentId) is IOperatorInput input) input.Operate(active);
        else throw new ArgumentException($"The scene has no button or switch called '{componentId}'.", nameof(componentId));
    }

    /// <summary>
    /// Reads something about the machine itself, not a PLC tag: <c>@tank.level</c>, <c>@door.position</c>,
    /// <c>@diverter.metal</c>. Tests use probes to check what the machine did ("the tank never overflowed").
    /// </summary>
    public PlcValue? Probe(string path)
    {
        var p = path.TrimStart('@');
        var dot = p.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0) return null;
        return Find(p[..dot])?.Probe(p[(dot + 1)..].ToLowerInvariant());
    }

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static Scene Load(string json)
    {
        var scene = JsonSerializer.Deserialize<Scene>(json, JsonOptions) ?? throw new JsonException("The scene file is empty.");
        foreach (var c in scene.Components) c.Owner = scene;
        return scene;
    }

    public string Save() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Adds a component made in code (tests), linking it to this scene.</summary>
    public T Add<T>(T component) where T : SceneComponent
    {
        component.Owner = this;
        Components.Add(component);
        return component;
    }
}

public enum IoKind { Input, Output }

/// <summary>One wire between the machine and the PLC: which tag, which way, and what it means.</summary>
public sealed record IoPoint(string Tag, IoKind Kind, string Meaning);

/// <summary>A button, switch or obstruction a person (or a test scenario) operates.</summary>
public interface IOperatorInput
{
    void Operate(bool active);
    bool Active { get; }
    /// <summary>True for a momentary push button (held down), false for something that stays where it is put.</summary>
    bool Momentary { get; }
}

public enum WiringFaultKind
{
    /// <summary>A broken wire: the input always reads FALSE.</summary>
    Open,
    /// <summary>A short to the supply: the input always reads TRUE.</summary>
    Shorted,
    /// <summary>Two inputs wired to each other's terminals.</summary>
    SwappedWith,
}

public sealed class WiringFault
{
    public string Tag { get; set; } = "";
    public WiringFaultKind Kind { get; set; }
    public string? Other { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PushButton), "pushButton")]
[JsonDerivedType(typeof(SelectorSwitch), "selectorSwitch")]
[JsonDerivedType(typeof(EmergencyStop), "emergencyStop")]
[JsonDerivedType(typeof(PilotLight), "pilotLight")]
[JsonDerivedType(typeof(Horn), "horn")]
[JsonDerivedType(typeof(Motor), "motor")]
[JsonDerivedType(typeof(LimitSwitch), "limitSwitch")]
[JsonDerivedType(typeof(FloatSwitch), "floatSwitch")]
[JsonDerivedType(typeof(PhotoEye), "photoEye")]
[JsonDerivedType(typeof(ProximitySensor), "proximitySensor")]
[JsonDerivedType(typeof(SafetyEye), "safetyEye")]
[JsonDerivedType(typeof(Tank), "tank")]
[JsonDerivedType(typeof(SolenoidValve), "solenoidValve")]
[JsonDerivedType(typeof(GarageDoor), "garageDoor")]
[JsonDerivedType(typeof(Conveyor), "conveyor")]
[JsonDerivedType(typeof(Diverter), "diverter")]
public abstract class SceneComponent
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    /// <summary>Position on the scene panel, in device-independent units.</summary>
    public double X { get; set; }
    public double Y { get; set; }

    [JsonIgnore] public Scene? Owner { get; internal set; }
    [JsonIgnore] public abstract IEnumerable<IoPoint> Points { get; }

    /// <summary>The component's state in words, for the scene view and screen readers ("Motor M1: running").</summary>
    [JsonIgnore] public abstract string StateText { get; }

    public virtual PlcValue? ReadInput(string tag) => null;
    public virtual void WriteOutput(string tag, PlcValue value) { }
    public virtual void Advance(long ms) { }
    /// <summary>A named property of the machine for tests (lower case), or null.</summary>
    public virtual PlcValue? Probe(string property) => null;

    protected static bool Is(string? a, string? b) => a is not null && b is not null && a.Equals(b, StringComparison.OrdinalIgnoreCase);
    protected static string Pct(double fraction) => (fraction * 100).ToString("0", CultureInfo.InvariantCulture) + " %";
}
