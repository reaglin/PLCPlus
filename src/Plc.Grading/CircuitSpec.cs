using System.Globalization;

namespace Plc.Grading;

/// <summary>
/// What a <c>[[try-circuit: …]]</c> line asks for: the kind, then optionally the supply and the parts it starts with, so the Example
/// tab can start at the Learn page's worked example (Ron, 2026-11-17). For example:
/// <code>
/// [[try-circuit: ohms-law 24V 480]]
/// [[try-circuit: series 24V 200 400]]
/// [[try-circuit: parallel 24V Coil=480 Light1=960 Light2=960 Light3=960]]
/// [[try-circuit: ac-dc]]
/// </code>
/// A part is ohms, written plain (480) or with a name (Coil=480); the supply ends in V. Ohm's law has one part, series two to four,
/// parallel one to four. Values left out take the kind's own starting values.
/// </summary>
public sealed record CircuitSpec(string Kind, double Volts, IReadOnlyList<(string Name, double Ohms)> Parts)
{
    public static readonly IReadOnlyList<string> Kinds = ["ohms-law", "series", "parallel", "ac-dc"];

    /// <summary>The smallest and largest resistance a slider allows, in ohms.</summary>
    public const double MinOhms = 10, MaxOhms = 2000;

    /// <summary>Reads the argument, or says in one sentence what is wrong with it.</summary>
    public static CircuitSpec? Parse(string argument, out string? problem)
    {
        problem = null;
        var words = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var kind = words.Length == 0 ? "" : words[0].ToLowerInvariant();
        if (!Kinds.Contains(kind))
        {
            problem = $"try-circuit starts with one of: {string.Join(", ", Kinds)}.";
            return null;
        }
        double? volts = null;
        var parts = new List<(string, double)>();
        foreach (var w in words.Skip(1))
        {
            if (w.EndsWith('V') || w.EndsWith('v'))
            {
                if (!double.TryParse(w[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v < 0 || v > 48)
                {
                    problem = $"try-circuit's supply \"{w}\" should be 0V to 48V.";
                    return null;
                }
                volts = v;
                continue;
            }
            var eq = w.IndexOf('=');
            var name = eq > 0 ? w[..eq] : $"R{parts.Count + 1}";
            var text = eq > 0 ? w[(eq + 1)..] : w;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var ohms) || ohms < MinOhms || ohms > MaxOhms)
            {
                problem = $"try-circuit's part \"{w}\" should be a resistance from {MinOhms} to {MaxOhms} ohms, such as 480 or Coil=480.";
                return null;
            }
            parts.Add((name, ohms));
        }
        if (kind == "ac-dc" && (parts.Count > 0 || volts is not null))
        {
            problem = "try-circuit: ac-dc takes no values.";
            return null;
        }
        var (min, max, start) = kind switch
        {
            "ohms-law" => (1, 1, new[] { ("R", 480.0) }),
            "series" => (2, 4, new[] { ("R1", 200.0), ("R2", 400.0) }),
            "parallel" => (1, 4, new[] { ("R1", 480.0), ("R2", 960.0) }),
            _ => (0, 0, Array.Empty<(string, double)>()),
        };
        if (parts.Count == 0) parts.AddRange(start);
        if (parts.Count < min || parts.Count > max)
        {
            problem = $"try-circuit: {kind} takes {(min == max ? $"{min} part" : $"{min} to {max} parts")}, not {parts.Count}.";
            return null;
        }
        return new CircuitSpec(kind, volts ?? 24, parts);
    }
}
