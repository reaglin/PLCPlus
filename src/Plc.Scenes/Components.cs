using System.Globalization;
using System.Text.Json.Serialization;
using Plc.Engine;

namespace Plc.Scenes;

// The component library (spec 5.3). Every one runs on the engine's simulated clock, in whole
// milliseconds, so a scene behaves exactly the same every time (NFR-3). Sensors read the state of
// the part of the machine they watch when the PLC reads its inputs. Faults for troubleshooting
// lessons: StuckAt on sensors and buttons, Failed on motors, and the scene's wiring faults.

// ---- Operator inputs ------------------------------------------------------------------------------

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
    [JsonIgnore] public bool Active => Pressed;
    [JsonIgnore] public bool Momentary => true;
    public void Operate(bool active) => Pressed = active;

    public override IEnumerable<IoPoint> Points =>
        [new(Tag, IoKind.Input, NormallyClosed
            ? $"{Label} push button (normally closed): TRUE while it is NOT pressed"
            : $"{Label} push button (normally open): TRUE while it is pressed")];

    public override PlcValue? ReadInput(string tag) =>
        Is(tag, Tag) ? PlcValue.FromBool(StuckAt ?? (Pressed ^ NormallyClosed)) : null;

    public override string StateText => $"{Label} button: {(Pressed ? "pressed" : "not pressed")}";
}

/// <summary>A two-position selector switch that stays where it is turned (Off/On, Manual/Auto).</summary>
public sealed class SelectorSwitch : SceneComponent, IOperatorInput
{
    public string Tag { get; set; } = "";
    /// <summary>The words for the two positions: [FALSE position, TRUE position].</summary>
    public List<string> Positions { get; set; } = ["Off", "On"];
    public bool On { get; set; }
    public bool? StuckAt { get; set; }

    [JsonIgnore] public bool Active => On;
    [JsonIgnore] public bool Momentary => false;
    public void Operate(bool active) => On = active;

    private string Word(bool on) => Positions.Count == 2 ? Positions[on ? 1 : 0] : (on ? "On" : "Off");

    public override IEnumerable<IoPoint> Points =>
        [new(Tag, IoKind.Input, $"{Label} selector switch: TRUE in {Word(true)}, FALSE in {Word(false)}")];

    public override PlcValue? ReadInput(string tag) => Is(tag, Tag) ? PlcValue.FromBool(StuckAt ?? On) : null;
    public override string StateText => $"{Label} switch: {Word(On)}";
}

/// <summary>
/// An emergency-stop mushroom button. It latches when pressed and stays pressed until it is pulled out.
/// Wired normally closed: TRUE while it is not pressed.
/// </summary>
public sealed class EmergencyStop : SceneComponent, IOperatorInput
{
    public string Tag { get; set; } = "";
    public bool Pressed { get; set; }

    [JsonIgnore] public bool Active => Pressed;
    [JsonIgnore] public bool Momentary => false;
    public void Operate(bool active) => Pressed = active;

    public override IEnumerable<IoPoint> Points =>
        [new(Tag, IoKind.Input, $"{Label} emergency stop (normally closed): TRUE while it is NOT pressed")];

    public override PlcValue? ReadInput(string tag) => Is(tag, Tag) ? PlcValue.FromBool(!Pressed) : null;
    public override string StateText => $"{Label}: {(Pressed ? "PRESSED (latched)" : "released")}";
}

// ---- Indicators -----------------------------------------------------------------------------------

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

    public override PlcValue? Probe(string property) => property == "lit" ? PlcValue.FromBool(Lit) : null;
    public override string StateText => $"{Label} light: {(Lit ? "on" : "off")}";
}

/// <summary>A horn, sounding while its output is TRUE (shown, not played).</summary>
public sealed class Horn : SceneComponent
{
    public string Tag { get; set; } = "";
    [JsonIgnore] public bool Sounding { get; private set; }

    public override IEnumerable<IoPoint> Points => [new(Tag, IoKind.Output, $"{Label} horn: sounds while TRUE")];

    public override void WriteOutput(string tag, PlcValue value)
    {
        if (Is(tag, Tag)) Sounding = value.AsBool;
    }

    public override string StateText => $"{Label} horn: {(Sounding ? "SOUNDING" : "quiet")}";
}

// ---- Actuators ------------------------------------------------------------------------------------

/// <summary>
/// A motor on a contactor. The run output pulls the contactor in; the motor reaches speed after
/// <see cref="StartTimeMs"/>, and an optional running feedback input reports it. With a
/// <see cref="ReverseTag"/> it is a reversing motor (spec 5.3, "motor forward/reverse"): a second
/// contactor runs it backwards, and telling it to go both ways at once is a fault it shows (on a real
/// panel the two contactors would short the supply), so interlock lessons can see it.
/// </summary>
public sealed class Motor : SceneComponent
{
    public string RunTag { get; set; } = "";
    /// <summary>Optional: the output that runs the motor in reverse.</summary>
    public string? ReverseTag { get; set; }
    public string? RunningTag { get; set; }
    public int StartTimeMs { get; set; } = 500;
    /// <summary>Fault injection: the motor does not turn even when told to run.</summary>
    public bool Failed { get; set; }

    [JsonIgnore] public bool Commanded { get; private set; }
    [JsonIgnore] public bool CommandedReverse { get; private set; }
    /// <summary>Both directions were told to run at the same time (stays TRUE once it has happened, so a test sees it).</summary>
    [JsonIgnore] public bool Conflict { get; private set; }
    /// <summary>0 (stopped) to 1 (full speed), in whichever direction it turns.</summary>
    [JsonIgnore] public double Speed { get; private set; }
    [JsonIgnore] public bool Running => Speed >= 1;
    /// <summary>Turning backwards (only a reversing motor).</summary>
    [JsonIgnore] public bool Reversing => _spinMs < 0;
    /// <summary>The shaft angle in degrees, for the animation.</summary>
    [JsonIgnore] public double Angle { get; private set; }

    // Signed: forward is positive, reverse negative. A motor told to change direction slows to a stop, then speeds up the other way.
    private long _spinMs;

    public override IEnumerable<IoPoint> Points
    {
        get
        {
            yield return new(RunTag, IoKind.Output, ReverseTag is null ? $"{Label}: runs while TRUE" : $"{Label}: runs forward while TRUE");
            if (ReverseTag is not null) yield return new(ReverseTag, IoKind.Output, $"{Label}: runs in reverse while TRUE (never with forward)");
            if (RunningTag is not null) yield return new(RunningTag, IoKind.Input, $"{Label} running feedback: TRUE once it is up to speed");
        }
    }

    public override PlcValue? ReadInput(string tag) => Is(tag, RunningTag) ? PlcValue.FromBool(Running) : null;

    public override void WriteOutput(string tag, PlcValue value)
    {
        if (Is(tag, RunTag)) Commanded = value.AsBool;
        else if (Is(tag, ReverseTag)) CommandedReverse = value.AsBool;
    }

    public override void Advance(long ms)
    {
        // Whole milliseconds, so speed reaches exactly 1 at StartTimeMs (no rounding drift).
        var full = Math.Max(0, StartTimeMs);
        var both = Commanded && CommandedReverse;
        if (both) Conflict = true;
        var target = Failed || both ? 0 : Commanded ? full : CommandedReverse ? -full : 0;
        _spinMs = target > _spinMs ? Math.Min(target, _spinMs + ms) : Math.Max(target, _spinMs - ms);
        Speed = full == 0 ? (target == 0 ? 0 : 1) : Math.Abs((double)_spinMs) / full;
        if (full == 0) _spinMs = Math.Sign(target);
        Angle = ((Angle + (Reversing ? -1 : 1) * Speed * 0.72 * ms) % 360 + 360) % 360; // 2 turns a second at full speed
    }

    public override PlcValue? Probe(string property) => property switch
    {
        "running" => PlcValue.FromBool(Running && !Reversing),
        "reverse" => PlcValue.FromBool(Running && Reversing),
        "speed" => PlcValue.FromReal(Speed),
        "conflict" => PlcValue.FromBool(Conflict),
        _ => null,
    };

    public override string StateText =>
        Commanded && CommandedReverse ? $"{Label}: told to run BOTH ways at once (the contactors would short the supply)"
        : Failed && (Commanded || CommandedReverse) ? $"{Label}: told to run, but not turning"
        : Running ? $"{Label}: running" + (ReverseTag is null ? "" : Reversing ? " in reverse" : " forward")
        : (Commanded || CommandedReverse) && Speed > 0 && Reversing == CommandedReverse ? $"{Label}: starting ({Pct(Speed)} speed)"
        : Commanded || CommandedReverse ? (Speed > 0 ? $"{Label}: slowing down to change direction" : $"{Label}: starting ({Pct(Speed)} speed)")
        : Speed > 0 ? $"{Label}: slowing down"
        : $"{Label}: stopped";
}

/// <summary>A solenoid valve on a tank: open while its output is TRUE, letting liquid in (inlet) or out (outlet).</summary>
public sealed class SolenoidValve : SceneComponent
{
    public string Tag { get; set; } = "";
    public string Tank { get; set; } = "";
    public bool Inlet { get; set; } = true;
    /// <summary>How fast it fills or drains the tank when open, in percent of the tank per second.</summary>
    public double RatePercentPerSecond { get; set; } = 10;
    /// <summary>Fault injection: the valve stays shut whatever it is told.</summary>
    public bool Failed { get; set; }

    [JsonIgnore] public bool Open { get; private set; }

    public override IEnumerable<IoPoint> Points =>
        [new(Tag, IoKind.Output, $"{Label} ({(Inlet ? "fills" : "drains")} the tank): open while TRUE")];

    public override void WriteOutput(string tag, PlcValue value)
    {
        if (Is(tag, Tag)) Open = value.AsBool && !Failed;
    }

    public override PlcValue? Probe(string property) => property == "open" ? PlcValue.FromBool(Open) : null;
    public override string StateText => $"{Label}: {(Open ? "open" : "closed")}";
}

/// <summary>
/// A diverter (a pusher cylinder) beside a conveyor. Its output extends it over <see cref="StrokeTimeMs"/>;
/// once fully out, it pushes every part in its zone off the belt into its bin.
/// </summary>
public sealed class Diverter : SceneComponent
{
    public string Tag { get; set; } = "";
    public string Conveyor { get; set; } = "";
    /// <summary>Where it stands along the belt, 0 (start) to 1 (end).</summary>
    public double At { get; set; } = 0.6;
    /// <summary>Half the width of the zone it pushes from, as a fraction of the belt.</summary>
    public double Zone { get; set; } = 0.03;
    public int StrokeTimeMs { get; set; } = 200;
    public string BinLabel { get; set; } = "Bin A";

    [JsonIgnore] public bool Commanded { get; private set; }
    [JsonIgnore] public double Extension => StrokeTimeMs <= 0 ? (Commanded ? 1 : 0) : (double)_strokeMs / StrokeTimeMs;
    [JsonIgnore] public Dictionary<string, int> Bin { get; } = new(StringComparer.OrdinalIgnoreCase);

    private long _strokeMs;

    public override IEnumerable<IoPoint> Points => [new(Tag, IoKind.Output, $"{Label}: pushes parts into {BinLabel} while TRUE")];

    public override void WriteOutput(string tag, PlcValue value)
    {
        if (Is(tag, Tag)) Commanded = value.AsBool;
    }

    public override void Advance(long ms)
    {
        _strokeMs = Commanded ? Math.Min(StrokeTimeMs, _strokeMs + ms) : Math.Max(0, _strokeMs - ms);
        if (Extension < 1 || Owner?.Find<Conveyor>(Conveyor) is not { } belt) return;
        foreach (var part in belt.PartsIn(At, Zone).ToList())
        {
            belt.Remove(part);
            Bin[part.Kind] = Bin.GetValueOrDefault(part.Kind) + 1;
        }
    }

    public override PlcValue? Probe(string property) => property switch
    {
        "extended" => PlcValue.FromBool(Extension >= 1),
        "total" => PlcValue.FromDInt(Bin.Values.Sum()),
        _ => PlcValue.FromDInt(Bin.GetValueOrDefault(property)),
    };

    public override string StateText =>
        $"{Label}: {(Extension >= 1 ? "out" : Extension > 0 ? "moving" : "in")}; {BinLabel}: {Describe(Bin)}";

    internal static string Describe(Dictionary<string, int> bin) =>
        bin.Count == 0 ? "empty" : string.Join(", ", bin.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Value} {k.Key}"));
}

// ---- Process elements -----------------------------------------------------------------------------

/// <summary>
/// A tank. Its level changes with the valves that name it; it cannot go below empty, and
/// liquid that would go over the top is an overflow (latched, so a test can check it never happened).
/// </summary>
public sealed class Tank : SceneComponent
{
    public double InitialLevelPercent { get; set; } = 10;
    public double Width { get; set; } = 120;
    public double Height { get; set; } = 160;

    /// <summary>The level in thousandths of a percent (whole numbers keep it exact).</summary>
    private long _milli = -1;

    [JsonIgnore] public double LevelPercent => Milli / 1000.0;
    [JsonIgnore] public bool Overflowed { get; private set; }
    private long Milli => _milli < 0 ? _milli = (long)Math.Round(InitialLevelPercent * 1000) : _milli;

    public override IEnumerable<IoPoint> Points => [];

    public override void Advance(long ms)
    {
        var level = Milli;
        foreach (var v in Owner?.Components.OfType<SolenoidValve>() ?? [])
        {
            if (!v.Open || !Is(v.Tank, Id)) continue;
            var change = (long)Math.Round(v.RatePercentPerSecond * ms); // percent/s × ms = thousandths of a percent
            level += v.Inlet ? change : -change;
        }
        if (level > 100_000) { Overflowed = true; level = 100_000; }
        _milli = Math.Max(0, level);
    }

    public override PlcValue? Probe(string property) => property switch
    {
        "level" => PlcValue.FromReal(LevelPercent),
        "overflow" => PlcValue.FromBool(Overflowed),
        "empty" => PlcValue.FromBool(Milli == 0),
        _ => null,
    };

    public override string StateText =>
        $"{Label}: {LevelPercent.ToString("0", CultureInfo.InvariantCulture)} % full{(Overflowed ? " — OVERFLOWED" : "")}";
}

/// <summary>
/// A garage door driven by a reversing motor: one output runs it up, another down; it takes
/// <see cref="TravelTimeMs"/> to go all the way. Both outputs at once would short the reversing
/// contactors: the door does not move and the conflict is recorded. A blocked safety eye means
/// something is under the door: the door cannot close onto it, and trying is recorded.
/// </summary>
public sealed class GarageDoor : SceneComponent
{
    public string UpTag { get; set; } = "";
    public string DownTag { get; set; } = "";
    public int TravelTimeMs { get; set; } = 4000;
    /// <summary>0 = closed, 1 = fully open.</summary>
    public double InitialPosition { get; set; }
    /// <summary>The safety eye across the opening, if any.</summary>
    public string? SafetyEye { get; set; }
    public double Width { get; set; } = 160;
    public double Height { get; set; } = 150;

    private long _openMs = -1;
    [JsonIgnore] public bool Up { get; private set; }
    [JsonIgnore] public bool Down { get; private set; }
    [JsonIgnore] public bool Conflict { get; private set; }
    [JsonIgnore] public bool HitObstruction { get; private set; }
    [JsonIgnore] public double Position => TravelTimeMs <= 0 ? 0 : (double)OpenMs / TravelTimeMs;
    private long OpenMs => _openMs < 0 ? _openMs = (long)Math.Round(InitialPosition * TravelTimeMs) : _openMs;

    /// <summary>How high the obstruction is, as a fraction of the opening: the door stops on it.</summary>
    public const double ObstructionHeight = 0.25;

    public override IEnumerable<IoPoint> Points =>
    [
        new(UpTag, IoKind.Output, $"{Label} motor up: the door opens while TRUE"),
        new(DownTag, IoKind.Output, $"{Label} motor down: the door closes while TRUE"),
    ];

    public override void WriteOutput(string tag, PlcValue value)
    {
        if (Is(tag, UpTag)) Up = value.AsBool;
        if (Is(tag, DownTag)) Down = value.AsBool;
    }

    public override void Advance(long ms)
    {
        var pos = OpenMs;
        if (Up && Down) { Conflict = true; return; }
        if (Up) pos = Math.Min(TravelTimeMs, pos + ms);
        if (Down)
        {
            var target = pos - ms;
            if (Owner?.Find<SafetyEye>(SafetyEye) is { Blocked: true })
            {
                // Something is standing in the doorway: the door comes down on it and can go no lower.
                var floor = (long)Math.Round(ObstructionHeight * TravelTimeMs);
                if (target < floor) { HitObstruction = true; target = Math.Min(pos, floor); }
            }
            pos = Math.Max(0, target);
        }
        _openMs = pos;
    }

    public override PlcValue? Probe(string property) => property switch
    {
        "position" => PlcValue.FromReal(Position),
        "open" => PlcValue.FromBool(OpenMs >= TravelTimeMs),
        "closed" => PlcValue.FromBool(OpenMs <= 0),
        "conflict" => PlcValue.FromBool(Conflict),
        "hit" => PlcValue.FromBool(HitObstruction),
        _ => null,
    };

    public override string StateText
    {
        get
        {
            var where = OpenMs >= TravelTimeMs ? "fully open" : OpenMs <= 0 ? "closed" : $"{Pct(Position)} open";
            var motion = Up && Down ? "BOTH motor directions on" : Up ? "going up" : Down ? "going down" : "still";
            return $"{Label}: {where}, {motion}{(HitObstruction ? " — came down on something" : "")}";
        }
    }
}

/// <summary>One part on a conveyor.</summary>
public sealed class Part(int number, string kind)
{
    public int Number { get; } = number;
    public string Kind { get; } = kind;
    /// <summary>How far along the belt, in milliseconds of travel.</summary>
    public long TravelMs { get; set; }
}

/// <summary>
/// A conveyor belt. While its motor output is TRUE the belt moves every part along; a part takes
/// <see cref="CrossTimeMs"/> to go from one end to the other, and falls into the end bin. While it runs,
/// the feeder puts a new part on the start of the belt every <see cref="FeedIntervalMs"/>, the kinds
/// taken in turn from <see cref="FeedPattern"/> — so every run sees the same parts.
/// </summary>
public sealed class Conveyor : SceneComponent
{
    public string RunTag { get; set; } = "";
    public int CrossTimeMs { get; set; } = 10_000;
    public int FeedIntervalMs { get; set; } = 4000;
    public List<string> FeedPattern { get; set; } = ["metal", "plastic"];
    public double Length { get; set; } = 360;
    public string EndBinLabel { get; set; } = "Bin B";

    private readonly List<Part> _parts = [];
    private long _runMs;
    private long _nextFeedMs;
    private int _fed;

    [JsonIgnore] public bool Running { get; private set; }
    [JsonIgnore] public IReadOnlyList<Part> Parts => _parts;
    [JsonIgnore] public Dictionary<string, int> EndBin { get; } = new(StringComparer.OrdinalIgnoreCase);

    public double PositionOf(Part p) => CrossTimeMs <= 0 ? 0 : (double)p.TravelMs / CrossTimeMs;

    public IEnumerable<Part> PartsIn(double at, double halfWidth) =>
        _parts.Where(p => Math.Abs(PositionOf(p) - at) <= halfWidth + 1e-9);

    internal void Remove(Part p) => _parts.Remove(p);

    public override IEnumerable<IoPoint> Points => [new(RunTag, IoKind.Output, $"{Label} motor: the belt moves while TRUE")];

    public override void WriteOutput(string tag, PlcValue value)
    {
        if (Is(tag, RunTag)) Running = value.AsBool;
    }

    public override void Advance(long ms)
    {
        if (!Running) return;
        foreach (var p in _parts) p.TravelMs += ms;
        foreach (var done in _parts.Where(p => p.TravelMs >= CrossTimeMs).ToList())
        {
            _parts.Remove(done);
            EndBin[done.Kind] = EndBin.GetValueOrDefault(done.Kind) + 1;
        }
        if (FeedPattern.Count > 0 && _runMs >= _nextFeedMs)
        {
            _parts.Add(new Part(++_fed, FeedPattern[(_fed - 1) % FeedPattern.Count]));
            _nextFeedMs += FeedIntervalMs;
        }
        _runMs += ms;
    }

    public override PlcValue? Probe(string property) => property switch
    {
        "running" => PlcValue.FromBool(Running),
        "onbelt" => PlcValue.FromDInt(_parts.Count),
        "fed" => PlcValue.FromDInt(_fed),
        _ => PlcValue.FromDInt(EndBin.GetValueOrDefault(property)),
    };

    public override string StateText =>
        $"{Label}: {(Running ? "moving" : "stopped")}, {_parts.Count} on the belt; {EndBinLabel}: {Diverter.Describe(EndBin)}";
}

// ---- Discrete sensors -----------------------------------------------------------------------------

/// <summary>Common to sensors: the tag, normally-closed wiring, and a stuck fault.</summary>
public abstract class SensorBase : SceneComponent
{
    public string Tag { get; set; } = "";
    /// <summary>Wired so the input is TRUE when the sensor is NOT made.</summary>
    public bool NormallyClosed { get; set; }
    /// <summary>Fault injection: the input is stuck at this value.</summary>
    public bool? StuckAt { get; set; }

    /// <summary>Whether the sensor senses its target now.</summary>
    [JsonIgnore] public abstract bool Made { get; }
    protected abstract string Sensing { get; }

    public override IEnumerable<IoPoint> Points =>
        [new(Tag, IoKind.Input, $"{Label}: TRUE when {(NormallyClosed ? "it does NOT sense " : "it senses ")}{Sensing}")];

    public override PlcValue? ReadInput(string tag) => Is(tag, Tag) ? PlcValue.FromBool(StuckAt ?? (Made ^ NormallyClosed)) : null;
    public override PlcValue? Probe(string property) => property == "made" ? PlcValue.FromBool(Made) : null;
    public override string StateText => $"{Label}: {(Made ? "senses " + Sensing : "nothing")}";
}

/// <summary>A limit switch on a garage door: made when the door is fully open (At = open) or fully closed (At = closed).</summary>
public sealed class LimitSwitch : SensorBase
{
    public string Door { get; set; } = "";
    /// <summary>"open" or "closed".</summary>
    public string At { get; set; } = "closed";

    public override bool Made => Owner?.Find<GarageDoor>(Door)?.Probe(At.ToLowerInvariant()) is { } v && v.AsBool;
    protected override string Sensing => $"the door fully {At.ToLowerInvariant()}";
}

/// <summary>A float switch in a tank: made when the liquid is at or above its height.</summary>
public sealed class FloatSwitch : SensorBase
{
    public string Tank { get; set; } = "";
    public double LevelPercent { get; set; } = 50;

    public override bool Made => Owner?.Find<Tank>(Tank) is { } t && t.LevelPercent >= LevelPercent;
    protected override string Sensing => $"liquid at {LevelPercent.ToString("0", CultureInfo.InvariantCulture)} % or above";
}

/// <summary>A photo-eye across a conveyor: made while any part is in its beam.</summary>
public class PhotoEye : SensorBase
{
    public string Conveyor { get; set; } = "";
    public double At { get; set; } = 0.5;
    public double Zone { get; set; } = 0.03;

    public override bool Made => Owner?.Find<Conveyor>(Conveyor) is { } c && c.PartsIn(At, Zone).Any(Senses);
    protected virtual bool Senses(Part p) => true;
    protected override string Sensing => "a part in front of it";
}

/// <summary>An inductive proximity sensor beside a conveyor: made only by a metal part in front of it.</summary>
public sealed class ProximitySensor : PhotoEye
{
    protected override bool Senses(Part p) => p.Kind.Equals("metal", StringComparison.OrdinalIgnoreCase);
    protected override string Sensing => "a metal part in front of it";
}

/// <summary>
/// A safety eye across a doorway. Its input is TRUE while the beam is clear. A person (or a test)
/// blocks it by walking through; it stays blocked until they move on.
/// </summary>
public sealed class SafetyEye : SceneComponent, IOperatorInput
{
    public string Tag { get; set; } = "";
    public bool Blocked { get; set; }
    public bool? StuckAt { get; set; }

    [JsonIgnore] public bool Active => Blocked;
    [JsonIgnore] public bool Momentary => false;
    public void Operate(bool active) => Blocked = active;

    public override IEnumerable<IoPoint> Points => [new(Tag, IoKind.Input, $"{Label}: TRUE while the beam is clear, FALSE when something is in the way")];
    public override PlcValue? ReadInput(string tag) => Is(tag, Tag) ? PlcValue.FromBool(StuckAt ?? !Blocked) : null;
    public override PlcValue? Probe(string property) => property == "blocked" ? PlcValue.FromBool(Blocked) : null;
    public override string StateText => $"{Label}: {(Blocked ? "something is in the way" : "beam clear")}";
}
