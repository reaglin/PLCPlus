using System.Text.Json;
using Plc.Engine;
using Plc.Grading;
using Plc.Scenes;

namespace Plc.Engine.Tests;

/// <summary>Every challenge in the content package: the working program passes, the mistake fails where a learner sees it.</summary>
public class ContentTests
{
    private static readonly PlcChallengeRunner Runner = new(id => K.Content("scenes", id + ".json"));

    public static IEnumerable<object[]> Challenges()
    {
        using var doc = JsonDocument.Parse(K.Content("manifest.json"));
        foreach (var c in doc.RootElement.GetProperty("challenges").EnumerateArray())
        {
            var programs = c.GetProperty("programs").EnumerateArray().Select(p => p.GetProperty("file").GetString()!).ToList();
            yield return [c.GetProperty("file").GetString()!, programs[0], programs[1]];
        }
    }

    [Fact]
    public void ThereAreTheSpecScenesAndTheirExamples()
    {
        Assert.Equal(11, Challenges().Count());
        var scenes = Challenges().Select(c => PlcChallengeRunner.LoadChallenge(K.Content(((string)c[0]).Split('/'))).Scene).ToHashSet();
        Assert.Equal(
            new HashSet<string> { "pilot-station", "motor-station", "traffic-light", "garage-door", "tank", "conveyor-sorter", "press-guard", "pump-station", "reversing-conveyor", "overheat-alarm", "push-on-push-off" }, scenes);
    }

    [Theory]
    [MemberData(nameof(Challenges))]
    public void WorkingProgramPassesEveryTest(string challenge, string works, string mistake)
    {
        _ = mistake;
        var outcome = Runner.Run(K.Content(challenge.Split('/')), K.Content(works.Split('/')));
        Assert.True(outcome.Ran, string.Join("; ", outcome.Problems));
        Assert.True(outcome.Passed, string.Join("\n", outcome.Scenarios.Where(s => !s.Passed).Select(s => $"{s.Name}: {s.Failure} at {s.FailedAtSeconds}")));
    }

    [Theory]
    [MemberData(nameof(Challenges))]
    public void MistakeFailsATestTheLearnerCanSee(string challenge, string works, string mistake)
    {
        _ = works;
        var outcome = Runner.Run(K.Content(challenge.Split('/')), K.Content(mistake.Split('/')));
        Assert.True(outcome.Ran, string.Join("; ", outcome.Problems));
        Assert.Contains(outcome.Scenarios, s => !s.Passed && !s.Hidden);
    }

    [Theory]
    [MemberData(nameof(Challenges))]
    public void ProgramsAreCleanAndScenesHaveTheirTags(string challenge, string works, string mistake)
    {
        var c = PlcChallengeRunner.LoadChallenge(K.Content(challenge.Split('/')));
        var scene = Scene.Load(K.Content("scenes", c.Scene + ".json"));
        var sceneTags = scene.IoList.Select(p => p.Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in new[] { works, mistake })
        {
            var project = ProjectJson.Deserialize(K.Content(file.Split('/')));
            Assert.Empty(ProjectValidator.Validate(project, Plc.Dialects.Iec.IecDialect.Instance));
            foreach (var io in project.Tags.Where(t => t.Io != IoDirection.None))
                Assert.Contains(io.Name, sceneTags);
        }
        Assert.All(scene.Components, comp => Assert.False(string.IsNullOrWhiteSpace(comp.StateText)));
    }

    [Fact]
    public void FailureReplayExplainsTheGarageDoor()
    {
        var lines = Runner.Narrate(K.Content("challenges", "garage-door.json"), K.Content("programs", "garage-door-mistake.json"), 3);
        Assert.Contains(lines, l => l.Contains("something stepped into the Safety eye beam", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.EndsWith("✗", StringComparison.Ordinal));
    }
}

public class ComponentTests
{
    private static void Run(Scene scene, long ms)
    {
        for (long t = 0; t < ms; t += 10) scene.Advance(10);
    }

    [Fact]
    public void TankFillsDrainsAndRecordsOverflow()
    {
        var scene = new Scene();
        var tank = scene.Add(new Tank { Id = "t", InitialLevelPercent = 90 });
        scene.Add(new SolenoidValve { Id = "in", Tag = "In", Tank = "t", Inlet = true, RatePercentPerSecond = 10 });
        scene.Add(new SolenoidValve { Id = "out", Tag = "Out", Tank = "t", Inlet = false, RatePercentPerSecond = 5 });
        var high = scene.Add(new FloatSwitch { Id = "h", Tag = "High", Tank = "t", LevelPercent = 95 });
        scene.WriteOutput("In", PlcValue.True);
        Run(scene, 500);
        Assert.Equal(95.0, tank.LevelPercent, 6);
        Assert.Equal(PlcValue.True, scene.ReadInput("High"));
        Run(scene, 1000);
        Assert.True(tank.Overflowed);
        Assert.Equal(PlcValue.True, scene.Probe("@t.overflow"));
        scene.WriteOutput("In", PlcValue.False);
        scene.WriteOutput("Out", PlcValue.True);
        Run(scene, 30_000);
        Assert.Equal(PlcValue.True, scene.Probe("@t.empty"));
        Assert.False(high.Made);
    }

    [Fact]
    public void GarageDoorTravelsLimitsConflictAndObstruction()
    {
        var scene = new Scene();
        var door = scene.Add(new GarageDoor { Id = "d", UpTag = "Up", DownTag = "Down", TravelTimeMs = 1000, SafetyEye = "eye" });
        scene.Add(new LimitSwitch { Id = "top", Tag = "Top", Door = "d", At = "open" });
        scene.Add(new LimitSwitch { Id = "bot", Tag = "Bottom", Door = "d", At = "closed" });
        var eye = scene.Add(new SafetyEye { Id = "eye", Tag = "Clear" });
        Assert.Equal(PlcValue.True, scene.ReadInput("Bottom"));
        scene.WriteOutput("Up", PlcValue.True);
        Run(scene, 1000);
        Assert.Equal(PlcValue.True, scene.ReadInput("Top"));
        Assert.Equal(PlcValue.False, scene.ReadInput("Bottom"));

        scene.WriteOutput("Down", PlcValue.True);
        Run(scene, 100);
        Assert.True(door.Conflict);
        Assert.Equal(1.0, door.Position);

        scene.WriteOutput("Up", PlcValue.False);
        eye.Operate(true);
        Assert.Equal(PlcValue.False, scene.ReadInput("Clear"));
        Run(scene, 2000);
        Assert.True(door.HitObstruction);
        Assert.Equal(GarageDoor.ObstructionHeight, door.Position, 6);
    }

    [Fact]
    public void ConveyorFeedsMovesSensesAndDiverts()
    {
        var scene = new Scene();
        var belt = scene.Add(new Conveyor { Id = "b", RunTag = "Run", CrossTimeMs = 1000, FeedIntervalMs = 400, FeedPattern = ["metal", "plastic"] });
        scene.Add(new ProximitySensor { Id = "p", Tag = "Prox", Conveyor = "b", At = 0.3, Zone = 0.05 });
        scene.Add(new PhotoEye { Id = "e", Tag = "Eye", Conveyor = "b", At = 0.3, Zone = 0.05 });
        var div = scene.Add(new Diverter { Id = "dv", Tag = "Push", Conveyor = "b", At = 0.7, Zone = 0.05, StrokeTimeMs = 0 });

        Run(scene, 100);
        Assert.Empty(belt.Parts); // not running: nothing fed
        scene.WriteOutput("Run", PlcValue.True);
        Run(scene, 300);           // metal part at 0.29
        Assert.Equal(PlcValue.True, scene.ReadInput("Prox"));
        Assert.Equal(PlcValue.True, scene.ReadInput("Eye"));
        Run(scene, 400);           // metal at 0.69, plastic at 0.29
        Assert.Equal(PlcValue.False, scene.ReadInput("Prox"));
        Assert.Equal(PlcValue.True, scene.ReadInput("Eye"));
        scene.WriteOutput("Push", PlcValue.True);
        Run(scene, 10);
        Assert.Equal(1, div.Bin["metal"]);
        scene.WriteOutput("Push", PlcValue.False);
        Run(scene, 1000);
        Assert.Equal(PlcValue.FromDInt(1), scene.Probe("@b.plastic"));
        Assert.Equal(PlcValue.FromDInt(0), scene.Probe("@b.metal"));
    }

    [Fact]
    public void SelectorEstopAndHorn()
    {
        var scene = new Scene();
        var sel = scene.Add(new SelectorSwitch { Id = "s", Tag = "Auto", Positions = ["Manual", "Auto"] });
        var es = scene.Add(new EmergencyStop { Id = "es", Tag = "EStopOk" });
        var horn = scene.Add(new Horn { Id = "h", Tag = "Horn" });
        Assert.Equal(PlcValue.False, scene.ReadInput("Auto"));
        Assert.Equal(PlcValue.True, scene.ReadInput("EStopOk"));
        scene.Operate("s", true);
        scene.Operate("es", true);
        scene.WriteOutput("Horn", PlcValue.True);
        Assert.Equal(PlcValue.True, scene.ReadInput("Auto"));
        Assert.Equal(PlcValue.False, scene.ReadInput("EStopOk"));
        Assert.True(horn.Sounding);
        Assert.Contains("Auto", sel.StateText, StringComparison.Ordinal);
        Assert.Contains("PRESSED", es.StateText, StringComparison.Ordinal);
        Assert.False(((IOperatorInput)sel).Momentary);
    }

    [Theory]
    [InlineData(WiringFaultKind.Open, false, false)]
    [InlineData(WiringFaultKind.Shorted, false, true)]
    [InlineData(WiringFaultKind.Shorted, true, true)]
    public void WiringFaultsChangeWhatThePlcReads(WiringFaultKind kind, bool pressed, bool reads)
    {
        var scene = new Scene();
        scene.Add(new PushButton { Id = "b", Tag = "B", NormallyClosed = kind == WiringFaultKind.Open });
        scene.WiringFaults.Add(new WiringFault { Tag = "B", Kind = kind });
        scene.Operate("b", pressed);
        Assert.Equal(PlcValue.FromBool(reads), scene.ReadInput("B"));
    }

    [Fact]
    public void SwappedWiringReadsTheOtherInput()
    {
        var scene = new Scene();
        scene.Add(new PushButton { Id = "a", Tag = "A" });
        scene.Add(new PushButton { Id = "b", Tag = "B" });
        scene.WiringFaults.Add(new WiringFault { Tag = "A", Kind = WiringFaultKind.SwappedWith, Other = "B" });
        scene.Operate("b", true);
        Assert.Equal(PlcValue.True, scene.ReadInput("A"));
    }

    [Fact]
    public void ScenesRoundTripThroughJsonWithEveryComponentType()
    {
        foreach (var file in Directory.GetFiles(Path.Combine(K.RepoRoot(), "content", "plcplus-foundations", "scenes")))
        {
            var scene = Scene.Load(File.ReadAllText(file));
            var again = Scene.Load(scene.Save());
            Assert.Equal(scene.Components.Select(c => c.GetType()), again.Components.Select(c => c.GetType()));
            Assert.All(again.Components, c => Assert.Same(again, c.Owner));
        }
    }
}

/// <summary>Phase 4: the learning path's PLC side — challenges, example programs and widgets all exist.</summary>
public class PackageCheckTests
{
    [Fact]
    public void TheFoundationsPackagePassesEveryCheck() =>
        Assert.Empty(PackageChecks.Validate(Path.Combine(K.RepoRoot(), "content", "plcplus-foundations")));

    [Fact]
    public void ABadWidgetIsReported()
    {
        var folder = Path.Combine(K.RepoRoot(), "content", "plcplus-foundations");
        Assert.Null(PackageChecks.CheckWidget(folder, new Learning.Core.Content.WidgetTag("ladder", "programs/motor-seal-in.json", 1)));
        Assert.Contains("not in the package", PackageChecks.CheckWidget(folder, new Learning.Core.Content.WidgetTag("ladder", "programs/nope.json", 1)));
        Assert.Contains("no widget called", PackageChecks.CheckWidget(folder, new Learning.Core.Content.WidgetTag("video", "x", 1)));
        Assert.NotNull(PackageChecks.CheckWidget(folder, new Learning.Core.Content.WidgetTag("truth-table", "A AND", 1)));
    }
}
