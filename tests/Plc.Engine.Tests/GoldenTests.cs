using System.Globalization;
using System.Text;
using System.Text.Json;
using Plc.Dialects.Iec;
using Plc.Engine;

namespace Plc.Engine.Tests;

/// <summary>
/// The reference programs in tests/golden/ (plan 1.11). Each runs with its timed inputs and its change log
/// must match <c>&lt;id&gt;.trace.txt</c> exactly. These logs were checked by hand, and they are what the
/// Allen-Bradley PLC in the lab is compared against (docs/LAB-COMPARISON.md).
/// To rewrite them after a deliberate change: set PLCPLUS_UPDATE_GOLDEN=1, run the tests, and read the diff.
/// </summary>
public class GoldenTests
{
    public static IEnumerable<object[]> Cases() =>
        Directory.GetFiles(Path.Combine(K.RepoRoot(), "tests", "golden"), "R*.json")
            .Select(f => new object[] { Path.GetFileNameWithoutExtension(f) })
            .OrderBy(x => (string)x[0], StringComparer.Ordinal);

    [Theory]
    [MemberData(nameof(Cases))]
    public void ReferenceProgramMatchesItsTrace(string id)
    {
        var dir = Path.Combine(K.RepoRoot(), "tests", "golden");
        var actual = Run(File.ReadAllText(Path.Combine(dir, id + ".json")));
        var expectedFile = Path.Combine(dir, id + ".trace.txt");

        if (Environment.GetEnvironmentVariable("PLCPLUS_UPDATE_GOLDEN") == "1" || !File.Exists(expectedFile))
        {
            File.WriteAllText(expectedFile, actual);
            Assert.Fail($"{id}.trace.txt written; read it, check it by hand, and run the tests again.");
        }
        Assert.Equal(File.ReadAllText(expectedFile).ReplaceLineEndings("\n"), actual);
    }

    private sealed class Bench : IVirtualIo
    {
        public readonly Dictionary<string, bool> Inputs = new(StringComparer.OrdinalIgnoreCase);
        public PlcValue? ReadInput(string tag) => Inputs.TryGetValue(tag, out var v) ? PlcValue.FromBool(v) : null;
        public void WriteOutput(string tag, PlcValue value) { }
        public void Advance(long ms) { }
    }

    /// <summary>Runs a case and returns its header, its change log, and any fault.</summary>
    public static string Run(string caseJson)
    {
        using var doc = JsonDocument.Parse(caseJson);
        var root = doc.RootElement;
        var project = ProjectJson.Deserialize(root.GetProperty("project").GetRawText());
        var bench = new Bench();
        var rt = new PlcRuntime(project, IecDialect.Instance, bench);
        var watch = root.GetProperty("watch").EnumerateArray().Select(w => w.GetString()!).ToList();
        using var trace = new TraceRecorder(rt, watch);

        var events = root.GetProperty("stimulus").EnumerateArray()
            .Select(e => (T: (long)Math.Round(e.GetProperty("t").GetDouble() * 1000), Input: e.GetProperty("input").GetString()!, Value: e.GetProperty("value").GetBoolean()))
            .OrderBy(e => e.T).ToList();
        var end = (long)Math.Round(root.GetProperty("durationSeconds").GetDouble() * 1000);
        var next = 0;
        while (rt.NowMs < end && rt.State == RunState.Running)
        {
            while (next < events.Count && events[next].T <= rt.NowMs)
            {
                bench.Inputs[events[next].Input] = events[next].Value;
                next++;
            }
            rt.Scan();
        }

        var sb = new StringBuilder();
        sb.Append("# ").Append(root.GetProperty("id").GetString()).Append(' ').Append(root.GetProperty("title").GetString()).Append('\n');
        sb.Append("# scan ").Append(project.ScanTimeMs.ToString(CultureInfo.InvariantCulture)).Append(" ms; each line is a scan in which a watched value changed\n");
        sb.Append(trace.ToChangeLog());
        if (rt.Fault is { } f)
            sb.Append("FAULT at t=").Append((f.AtMs / 1000.0).ToString("0.000", CultureInfo.InvariantCulture))
              .Append("s, rung ").Append(f.Rung.ToString(CultureInfo.InvariantCulture)).Append(", ").Append(f.Kind).Append(": ").Append(f.Message).Append('\n');
        return sb.ToString();
    }
}
