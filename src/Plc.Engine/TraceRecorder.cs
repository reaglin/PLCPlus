using System.Globalization;
using System.Text;

namespace Plc.Engine;

/// <summary>The watched values at the end of one scan, stamped with the time the scan ran.</summary>
public sealed record TraceFrame(long TimeMs, IReadOnlyList<PlcValue> Values);

/// <summary>
/// Records chosen tags after every scan (spec 4.2 housekeeping: trace capture). The grader uses it to
/// replay a failed test; the golden-file tests and the lab comparison use its change log, which lists
/// only the moments something changed.
/// </summary>
public sealed class TraceRecorder : IDisposable
{
    /// <summary>A trace stops growing here (about 16 simulated minutes at a 10 ms scan).</summary>
    public const int MaxFrames = 100_000;

    private readonly PlcRuntime _runtime;
    private readonly List<TraceFrame> _frames = [];

    public IReadOnlyList<string> Tags { get; }
    public IReadOnlyList<TraceFrame> Frames => _frames;
    public bool Truncated { get; private set; }

    /// <param name="runtime">The PLC to watch.</param>
    /// <param name="tags">What to record: tags, array elements, structure members or timer members (T1.Q).
    /// Default: every input and output tag, in declaration order.</param>
    public TraceRecorder(PlcRuntime runtime, IEnumerable<string>? tags = null)
    {
        _runtime = runtime;
        Tags = (tags ?? runtime.Project.Tags.Where(t => t.Io != IoDirection.None).Select(t => t.Name)).ToList();
        runtime.ScanCompleted += OnScan;
    }

    private void OnScan(object? sender, EventArgs e)
    {
        if (_frames.Count >= MaxFrames) { Truncated = true; return; }
        var values = new PlcValue[Tags.Count];
        for (var i = 0; i < Tags.Count; i++)
        {
            try { values[i] = _runtime.Read(Tags[i]); }
            catch (PlcFaultException) { values[i] = default; }
        }
        _frames.Add(new TraceFrame(_runtime.LastScanStartMs, values));
    }

    /// <summary>The value a tag had at the end of the last scan that ran at or before <paramref name="timeMs"/>.</summary>
    public PlcValue? ValueAt(string tag, long timeMs)
    {
        var index = IndexOf(tag);
        PlcValue? value = null;
        foreach (var f in _frames)
        {
            if (f.TimeMs > timeMs) break;
            value = f.Values[index];
        }
        return value;
    }

    /// <summary>
    /// One line per scan in which a watched value changed, with only the values that changed
    /// (the first line lists them all): <c>t=1.230s  Motor=TRUE  RunLight=TRUE</c>.
    /// </summary>
    public string ToChangeLog()
    {
        var sb = new StringBuilder();
        PlcValue[]? last = null;
        foreach (var f in _frames)
        {
            var changed = new List<string>();
            for (var i = 0; i < Tags.Count; i++)
                if (last is null || !last[i].Equals(f.Values[i]))
                    changed.Add($"{Tags[i]}={f.Values[i]}");
            if (changed.Count > 0)
                sb.Append("t=").Append((f.TimeMs / 1000.0).ToString("0.000", CultureInfo.InvariantCulture)).Append("s  ")
                  .AppendJoin("  ", changed).Append('\n');
            last = [.. f.Values];
        }
        if (Truncated) sb.Append("(trace stopped at ").Append(MaxFrames).Append(" scans)\n");
        return sb.ToString();
    }

    private int IndexOf(string tag)
    {
        for (var i = 0; i < Tags.Count; i++)
            if (Tags[i].Equals(tag, StringComparison.OrdinalIgnoreCase)) return i;
        throw new ArgumentException($"'{tag}' is not being recorded.", nameof(tag));
    }

    public void Dispose() => _runtime.ScanCompleted -= OnScan;
}
