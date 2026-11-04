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
    public List<SceneComponent> Components { get; set; } = [];

    public SceneComponent? Find(string id) =>
        Components.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The I/O list a learner is given: every tag the scene reads or drives.</summary>
    public IEnumerable<IoPoint> IoList => Components.SelectMany(c => c.Points);

    public PlcValue? ReadInput(string tag)
    {
        foreach (var c in Components)
            if (c.ReadInput(tag) is { } v) return v;
        return null;
    }

    public void WriteOutput(string tag, PlcValue value)
    {
        foreach (var c in Components) c.WriteOutput(tag, value);
    }

    public void Advance(long ms)
    {
        foreach (var c in Components) c.Advance(ms);
    }

    /// <summary>Operates an operator input (press or release a push button, turn a switch).</summary>
    public void Operate(string componentId, bool active)
    {
        if (Find(componentId) is IOperatorInput input) input.Operate(active);
        else throw new ArgumentException($"The scene has no button or switch called '{componentId}'.", nameof(componentId));
    }

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static Scene Load(string json) =>
        JsonSerializer.Deserialize<Scene>(json, JsonOptions) ?? throw new JsonException("The scene file is empty.");

    public string Save() => JsonSerializer.Serialize(this, JsonOptions);
}

public enum IoKind { Input, Output }

/// <summary>One wire between the machine and the PLC: which tag, which way, and what it means.</summary>
public sealed record IoPoint(string Tag, IoKind Kind, string Meaning);

/// <summary>A button or switch a person (or a test scenario) operates.</summary>
public interface IOperatorInput
{
    void Operate(bool active);
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PushButton), "pushButton")]
[JsonDerivedType(typeof(PilotLight), "pilotLight")]
[JsonDerivedType(typeof(Motor), "motor")]
public abstract class SceneComponent
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    /// <summary>Position on the scene panel, in device-independent units.</summary>
    public double X { get; set; }
    public double Y { get; set; }

    [JsonIgnore] public abstract IEnumerable<IoPoint> Points { get; }
    public virtual PlcValue? ReadInput(string tag) => null;
    public virtual void WriteOutput(string tag, PlcValue value) { }
    public virtual void Advance(long ms) { }

    protected static bool Is(string? a, string b) => a is not null && a.Equals(b, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// A momentary push button. A normally-closed button (a Stop button, wired for safety) sends TRUE
/// while it is <em>not</em> pressed — so a broken wire stops the machine. Lessons teach this.
/// </summary>
public sealed class PushButton : SceneComponent, IOperatorInput
{
    public string Tag { get; set; } = "";
    public bool NormallyClosed { get; set; }
    public string Color { get; set; } = "green";
    /// <summary>Fault injection: the contact is stuck open or closed whatever the button does.</summary>
    public bool? StuckAt { get; set; }

    [JsonIgnore] public bool Pressed { get; private set; }

    public void Operate(bool active) => Pressed = active;

    public override IEnumerable<IoPoint> Points =>
        [new(Tag, IoKind.Input, NormallyClosed
            ? $"{Label} push button (normally closed): TRUE while it is NOT pressed"
            : $"{Label} push button (normally open): TRUE while it is pressed")];

    public override PlcValue? ReadInput(string tag) =>
        Is(tag, Tag) ? PlcValue.FromBool(StuckAt ?? (Pressed ^ NormallyClosed)) : null;
}

/// <summary>A pilot light, lit while its output is TRUE.</summary>
public sealed class PilotLight : SceneComponent
{
    public string Tag { get; set; } = "";
    public string Color { get; set; } = "green";

    [JsonIgnore] public bool Lit { get; private set; }

    public override IEnumerable<IoPoint> Points => [new(Tag, IoKind.Output, $"{Label} pilot light: lit while TRUE")];

    public override void WriteOutput(string tag, PlcValue value)
    {
        if (Is(tag, Tag)) Lit = value.AsBool;
    }
}

/// <summary>
/// A motor on a contactor. The run output pulls the contactor in; the motor reaches speed after
/// <see cref="StartTimeMs"/>, and an optional running feedback input reports it.
/// </summary>
public sealed class Motor : SceneComponent
{
    public string RunTag { get; set; } = "";
    public string? RunningTag { get; set; }
    public int StartTimeMs { get; set; } = 500;
    /// <summary>Fault injection: the motor does not turn even when told to run.</summary>
    public bool Failed { get; set; }

    [JsonIgnore] public bool Commanded { get; private set; }
    /// <summary>0 (stopped) to 1 (full speed).</summary>
    [JsonIgnore] public double Speed { get; private set; }
    [JsonIgnore] public bool Running => Speed >= 1;
    /// <summary>The shaft angle in degrees, for the animation.</summary>
    [JsonIgnore] public double Angle { get; private set; }

    public override IEnumerable<IoPoint> Points
    {
        get
        {
            yield return new(RunTag, IoKind.Output, $"{Label}: runs while TRUE");
            if (RunningTag is not null) yield return new(RunningTag, IoKind.Input, $"{Label} running feedback: TRUE once it is up to speed");
        }
    }

    public override PlcValue? ReadInput(string tag) => Is(tag, RunningTag ?? "") ? PlcValue.FromBool(Running) : null;

    public override void WriteOutput(string tag, PlcValue value)
    {
        if (Is(tag, RunTag)) Commanded = value.AsBool;
    }

    public override void Advance(long ms)
    {
        // Whole milliseconds, so speed reaches exactly 1 at StartTimeMs (no rounding drift).
        var full = Math.Max(0, StartTimeMs);
        _spinMs = Commanded && !Failed ? Math.Min(full, _spinMs + ms) : Math.Max(0, _spinMs - ms);
        Speed = full == 0 ? (Commanded && !Failed ? 1 : 0) : (double)_spinMs / full;
        Angle = (Angle + Speed * 0.72 * ms) % 360; // 2 turns a second at full speed
    }

    private long _spinMs;
}
