using System.Globalization;
using System.Text.RegularExpressions;

namespace Plc.Engine;

/// <summary>The IEC 61131-3 elementary types the engine supports (spec 4.1). Arrays and structures come later.</summary>
public enum DataType
{
    Bool,
    Int,   // 16-bit signed
    DInt,  // 32-bit signed
    Real,  // stored as double, shown with up to 3 decimals
    Time,  // milliseconds
}

/// <summary>
/// One value in the PLC. Integers wrap the way a PLC's do (INT is 16 bits, DINT 32);
/// TIME is held in milliseconds.
/// </summary>
public readonly record struct PlcValue(DataType Type, long I, double R)
{
    public static PlcValue FromBool(bool b) => new(DataType.Bool, b ? 1 : 0, 0);
    public static PlcValue FromInt(long v) => new(DataType.Int, unchecked((short)v), 0);
    public static PlcValue FromDInt(long v) => new(DataType.DInt, unchecked((int)v), 0);
    public static PlcValue FromReal(double v) => new(DataType.Real, 0, v);
    public static PlcValue FromTime(long ms) => new(DataType.Time, ms, 0);

    public static readonly PlcValue False = FromBool(false);
    public static readonly PlcValue True = FromBool(true);

    public bool AsBool => Type == DataType.Real ? R != 0 : I != 0;
    public double AsReal => Type == DataType.Real ? R : I;
    public long AsLong => Type == DataType.Real ? (long)Math.Round(R, MidpointRounding.ToEven) : I;
    public bool IsInteger => Type is DataType.Int or DataType.DInt or DataType.Time;

    public static PlcValue Default(DataType type) => type switch
    {
        DataType.Real => FromReal(0),
        _ => new PlcValue(type, 0, 0),
    };

    /// <summary>Converts to another type, wrapping integers the way a PLC does.</summary>
    public PlcValue ConvertTo(DataType type) => type switch
    {
        DataType.Bool => FromBool(AsBool),
        DataType.Int => FromInt(AsLong),
        DataType.DInt => FromDInt(AsLong),
        DataType.Real => FromReal(AsReal),
        DataType.Time => FromTime(AsLong),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public override string ToString() => Type switch
    {
        DataType.Bool => I != 0 ? "TRUE" : "FALSE",
        DataType.Real => R.ToString("0.###", CultureInfo.InvariantCulture),
        DataType.Time => FormatTime(I),
        _ => I.ToString(CultureInfo.InvariantCulture),
    };

    public static string FormatTime(long ms)
    {
        if (ms == 0) return "T#0s";
        var sign = ms < 0 ? "-" : "";
        ms = Math.Abs(ms);
        var parts = new List<string>();
        if (ms >= 3_600_000) { parts.Add($"{ms / 3_600_000}h"); ms %= 3_600_000; }
        if (ms >= 60_000) { parts.Add($"{ms / 60_000}m"); ms %= 60_000; }
        if (ms >= 1000) { parts.Add($"{ms / 1000}s"); ms %= 1000; }
        if (ms > 0) parts.Add($"{ms}ms");
        return "T#" + sign + string.Concat(parts);
    }

    /// <summary>Parses a type name (BOOL, INT, DINT, REAL, TIME), case-insensitive.</summary>
    public static bool TryParseType(string? name, out DataType type)
    {
        switch (name?.Trim().ToUpperInvariant())
        {
            case "BOOL": type = DataType.Bool; return true;
            case "INT": type = DataType.Int; return true;
            case "DINT": type = DataType.DInt; return true;
            case "REAL": type = DataType.Real; return true;
            case "TIME": type = DataType.Time; return true;
            default: type = default; return false;
        }
    }

    private static readonly Regex TimePart = new(@"(\d+(?:\.\d+)?)(ms|d|h|m|s)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Parses a literal: TRUE / FALSE, a whole number (DINT), a number with a point (REAL),
    /// or a time such as T#2s, T#500ms, T#1m30s, TIME#1.5s.
    /// </summary>
    public static bool TryParseLiteral(string? text, out PlcValue value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim().Replace("_", "", StringComparison.Ordinal);

        if (t.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) { value = True; return true; }
        if (t.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) { value = False; return true; }

        var hash = t.IndexOf('#', StringComparison.Ordinal);
        if (hash > 0)
        {
            var prefix = t[..hash].ToUpperInvariant();
            if (prefix is "T" or "TIME")
            {
                var body = t[(hash + 1)..];
                var negative = body.StartsWith('-');
                if (negative) body = body[1..];
                var matches = TimePart.Matches(body);
                if (matches.Count == 0 || string.Concat(matches.Select(m => m.Value)).Length != body.Length) return false;
                double ms = 0;
                foreach (Match m in matches)
                {
                    var n = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    ms += m.Groups[2].Value.ToLowerInvariant() switch
                    {
                        "ms" => n,
                        "s" => n * 1000,
                        "m" => n * 60_000,
                        "h" => n * 3_600_000,
                        _ => n * 86_400_000,
                    };
                }
                value = FromTime((long)Math.Round(negative ? -ms : ms));
                return true;
            }
            return false;
        }

        if (long.TryParse(t, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l))
        {
            value = FromDInt(l);
            return true;
        }
        if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && t.Contains('.', StringComparison.Ordinal))
        {
            value = FromReal(d);
            return true;
        }
        return false;
    }
}
