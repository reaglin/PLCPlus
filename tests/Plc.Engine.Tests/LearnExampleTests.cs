using Learning.Core.Content;
using Plc.Dialects.Iec;
using Plc.Grading;

namespace Plc.Engine.Tests;

/// <summary>
/// Plan 13.14: the live ladders of the Learn pages (<c>[[try-ladder: programs/learn/…]]</c>). Every one loads with no errors, has an
/// input, runs through every combination of its buttons without a fault, and is used by a Learn page; a few behave as their pages say.
/// </summary>
public class LearnExampleTests
{
    private static readonly string Folder = Path.Combine(K.RepoRoot(), "content", "plcplus-foundations");

    /// <summary>The buttons the learner holds, as the page's widget gives them to the PLC: a normally closed one is TRUE until pressed.</summary>
    private sealed class Panel(PlcProject project) : IVirtualIo
    {
        public readonly HashSet<string> Pressed = new(StringComparer.OrdinalIgnoreCase);

        public PlcValue? ReadInput(string tag)
        {
            var t = project.Tags.FirstOrDefault(x => x.Name.Equals(tag, StringComparison.OrdinalIgnoreCase));
            if (t is null || !t.Type.Equals("BOOL", StringComparison.OrdinalIgnoreCase)) return null;
            var closed = t.Initial is not null && PlcValue.TryParseLiteral(t.Initial, out var v) && v.AsBool;
            return PlcValue.FromBool(Pressed.Contains(tag) != closed);
        }

        public void WriteOutput(string tag, PlcValue value) { }
        public void Advance(long ms) { }
    }

    public static TheoryData<string> Programs()
    {
        var data = new TheoryData<string>();
        foreach (var f in Directory.GetFiles(Path.Combine(Folder, "programs", "learn"), "*.json").Order()) data.Add(Path.GetFileName(f));
        return data;
    }

    private static PlcProject Load(string file) => ProjectJson.Deserialize(File.ReadAllText(Path.Combine(Folder, "programs", "learn", file)));

    [Theory]
    [MemberData(nameof(Programs))]
    public void Every_live_ladder_loads_and_runs_every_combination_of_its_buttons_without_a_fault(string file)
    {
        var project = Load(file);
        Assert.DoesNotContain(ProjectValidator.Validate(project, IecDialect.Instance), d => d.Severity == Severity.Error);
        Assert.False(string.IsNullOrWhiteSpace(project.Name));
        Assert.All(project.Programs.SelectMany(p => p.Rungs), r => Assert.False(string.IsNullOrWhiteSpace(r.Comment)));
        var buttons = project.Tags.Where(t => t.Io == IoDirection.Input && t.Type.Equals("BOOL", StringComparison.OrdinalIgnoreCase)).Select(t => t.Name).ToList();
        Assert.Contains(project.Tags, t => t.Io == IoDirection.Input);
        var panel = new Panel(project);
        var plc = new PlcRuntime(project, IecDialect.Instance, panel);
        var n = Math.Min(buttons.Count, 5);
        for (var mask = 0; mask < 1 << n; mask++)
        {
            panel.Pressed.Clear();
            for (var b = 0; b < n; b++) if ((mask >> b & 1) == 1) panel.Pressed.Add(buttons[b]);
            plc.RunFor(1500);
            Assert.True(plc.State == RunState.Running, $"{file}: faulted with {string.Join("+", panel.Pressed)}: {plc.Fault?.Message}");
        }
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public void Every_live_ladder_is_on_a_Learn_page(string file)
    {
        var package = ContentPackage.Load(Folder);
        Assert.Contains(package.AllItems.SelectMany(i => Widgets.In(i.Learn)), w => w.Name == "try-ladder" && w.Argument.Trim() == $"programs/learn/{file}");
    }

    [Fact]
    public void The_content_package_has_no_problems() => Assert.Empty(PackageChecks.Validate(Folder));

    [Fact]
    public void The_on_delay_timer_lights_Done_only_after_Go_is_held_3_seconds()
    {
        var project = Load("m6-ton-delay.json");
        var panel = new Panel(project);
        var plc = new PlcRuntime(project, IecDialect.Instance, panel);
        panel.Pressed.Add("Go");
        plc.RunFor(2000);
        Assert.False(plc.Read("Done").AsBool);
        panel.Pressed.Clear();
        plc.RunFor(100);
        panel.Pressed.Add("Go");
        plc.RunFor(2000);
        Assert.False(plc.Read("Done").AsBool);   // letting go started it again from 0
        plc.RunFor(1100);
        Assert.True(plc.Read("Done").AsBool);
    }

    [Fact]
    public void The_relay_seal_in_keeps_its_lamp_on_after_Go_is_let_go_and_Halt_stops_it()
    {
        var project = Load("m3-seal-in.json");
        var panel = new Panel(project);
        var plc = new PlcRuntime(project, IecDialect.Instance, panel);
        panel.Pressed.Add("Go");
        plc.RunFor(200);
        Assert.True(plc.Read("LampPlain").AsBool);
        Assert.True(plc.Read("LampSealed").AsBool);
        panel.Pressed.Clear();
        plc.RunFor(200);
        Assert.False(plc.Read("LampPlain").AsBool);
        Assert.True(plc.Read("LampSealed").AsBool);
        panel.Pressed.Add("Halt");
        plc.RunFor(200);
        Assert.False(plc.Read("LampSealed").AsBool);
    }

    [Fact]
    public void The_counter_lights_Three_on_the_third_ticket_and_Reset_clears_it()
    {
        var project = Load("m6-ctu-reset.json");
        var panel = new Panel(project);
        var plc = new PlcRuntime(project, IecDialect.Instance, panel);
        for (var i = 0; i < 3; i++)
        {
            Assert.False(plc.Read("Three").AsBool);
            panel.Pressed.Add("Ticket");
            plc.RunFor(100);
            panel.Pressed.Clear();
            plc.RunFor(100);
        }
        Assert.True(plc.Read("Three").AsBool);
        panel.Pressed.Add("Reset");
        plc.RunFor(100);
        Assert.False(plc.Read("Three").AsBool);
    }

    [Fact]
    public void Set_and_reset_latch_and_the_lower_rung_wins_when_both_are_held()
    {
        var project = Load("m5-set-reset.json");
        var panel = new Panel(project);
        var plc = new PlcRuntime(project, IecDialect.Instance, panel);
        panel.Pressed.Add("SetPB");
        plc.RunFor(100);
        panel.Pressed.Clear();
        plc.RunFor(100);
        Assert.True(plc.Read("Latched").AsBool);
        Assert.False(plc.Read("Follows").AsBool);
        panel.Pressed.UnionWith(["SetPB", "ResetPB"]);
        plc.RunFor(100);
        Assert.False(plc.Read("Latched").AsBool);
    }
}
