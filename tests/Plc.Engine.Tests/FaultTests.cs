using Learning.Core.Content;
using Plc.Dialects.Iec;
using Plc.Grading;
using Plc.Scenes;

namespace Plc.Engine.Tests;

/// <summary>Phase 5.5: "find the fault" questions — the fault is read, checked, and the faulted machine behaves as the answer says.</summary>
public class FaultTests
{
    private static readonly string Folder = Path.Combine(K.RepoRoot(), "content", "plcplus-foundations");

    [Theory]
    [InlineData("Stop:open", "Stop", WiringFaultKind.Open, null)]
    [InlineData("HighFloat:shorted", "HighFloat", WiringFaultKind.Shorted, null)]
    [InlineData("PB1:swapped:PB2", "PB1", WiringFaultKind.SwappedWith, "PB2")]
    public void FaultsAreReadTheShortWay(string text, string tag, WiringFaultKind kind, string? other)
    {
        var f = WiringFault.Parse(text)!;
        Assert.Equal((tag, kind, other), (f.Tag, f.Kind, f.Other));
    }

    [Theory]
    [InlineData("Stop")]
    [InlineData("Stop:melted")]
    [InlineData("PB1:swapped")]
    [InlineData(null)]
    public void BadFaultsAreNull(string? text) => Assert.Null(WiringFault.Parse(text));

    [Fact]
    public void AFaultQuestionIsChecked()
    {
        Question Q(string fault, string? program = "Works") => new() { Machine = "u5.motor-start-stop", Program = program, Fault = fault };
        Assert.Null(PackageChecks.CheckFault(Folder, Q("Stop:open")));
        Assert.Contains("cannot be read", PackageChecks.CheckFault(Folder, Q("Stop")));
        Assert.Contains("not an input", PackageChecks.CheckFault(Folder, Q("Motor:open")));
        Assert.Contains("not one of", PackageChecks.CheckFault(Folder, Q("Stop:open", "Nope")));
    }

    /// <summary>The machine with the question's fault, running the working program, after holding a button for half a second.</summary>
    private static PlcRuntime Faulted(string challengeId, string program, string fault, string press)
    {
        var challenge = PlcChallengeRunner.LoadChallenge(K.Content("challenges", challengeId.Split('.')[1] + ".json"));
        var scene = Scene.Load(K.Content("scenes", challenge.Scene + ".json"));
        scene.WiringFaults.Add(WiringFault.Parse(fault)!);
        var rt = new PlcRuntime(ProjectJson.Deserialize(K.Content("programs", program)), IecDialect.Instance, scene);
        var button = scene.Components.First(c => c.Label == press).Id;
        scene.Operate(button, true);
        for (var i = 0; i < 50; i++) rt.Scan();
        return rt;
    }

    [Fact]
    public void WithStopsWireBrokenTheMotorCannotStart() =>
        Assert.False(Faulted("u5.motor-start-stop", "motor-seal-in.json", "Stop:open", "Start").Read("Motor").AsBool);

    [Fact]
    public void WithTheTopLimitShortedTheDoorCannotOpen() =>
        Assert.False(Faulted("u5.garage-door", "garage-door.json", "OpenLimit:shorted", "Open").Read("MotorUp").AsBool);
}
