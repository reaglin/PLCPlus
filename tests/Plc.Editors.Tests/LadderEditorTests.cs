using Plc.Dialects.Iec;
using Plc.Editors;
using Plc.Engine;
using Plc.Grading;

namespace Plc.Editors.Tests;

public class LadderEditorTests
{
    private static string Content(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "PLCPlus.slnx"))) dir = Path.GetDirectoryName(dir);
        return File.ReadAllText(Path.Combine([dir!, "content", "plcplus-foundations", .. parts]));
    }

    private static LadderEditor MotorBlank() => new(LadderEditor.Blank("Mine",
    [
        ("Start", true, "Start button"), ("Stop", true, "Stop button (normally closed)"),
        ("Motor", false, "Motor"), ("RunLight", false, "Running light"),
    ]), IecDialect.Instance);

    private static IReadOnlyList<int> P(params int[] p) => p;

    [Fact]
    public void BuildTheSealInByEditingAndItPassesTheChallenge()
    {
        var e = MotorBlank();
        Assert.Equal(new LadderSelection(0, P(0)), e.Selection); // the blank rung's contact
        Assert.True(e.SetTag("Start").Done);
        Assert.True(e.AddBranch().Done);                          // a branch around Start …
        Assert.True(e.SetTag("Motor").Done);                      // … with the seal-in contact on it
        e.Select(new LadderSelection(0, P(0, 0, 0)));             // back to Start
        Assert.True(e.InsertContact(ContactKind.NormallyOpen).Done); // goes after Start, inside the top path …
        Assert.True(e.DeleteSelected().Done);                     // … not what we want: undo it by deleting
        e.Select(new LadderSelection(0, P(1)));                   // the coil
        Assert.True(e.InsertContact(ContactKind.NormallyOpen).Done); // a contact added at a coil goes before it
        Assert.Equal(new LadderSelection(0, P(1)), e.Selection);
        Assert.True(e.SetTag("Stop").Done);
        e.Select(new LadderSelection(0, P(2)));
        Assert.True(e.SetTag("Motor").Done);

        Assert.True(e.AddRung().Done);
        Assert.True(e.SetTag("Motor").Done);
        e.MoveSelection(+1);
        Assert.True(e.SetTag("RunLight").Done);

        Assert.Empty(e.Diagnostics);
        var outcome = new PlcChallengeRunner(id => Content("scenes", id + ".json"))
            .Run(Content("challenges", "motor-start-stop.json"), ProjectJson.Serialize(e.Project));
        Assert.True(outcome.Passed, string.Join("; ", outcome.Scenarios.Select(s => s.Failure)));
    }

    [Fact]
    public void NewNamesAreDeclaredOnFirstUse()
    {
        var e = MotorBlank();
        var r = e.SetTag("Jog");
        Assert.Contains("declared as BOOL", r.Message, StringComparison.Ordinal);
        Assert.Equal("BOOL", e.Find("Jog")!.Type);
        Assert.False(e.SetTag("2fast").Done);
        Assert.Contains("is a value", e.SetTag("TRUE").Message, StringComparison.Ordinal);
        Assert.Null(e.Find("TRUE"));
    }

    [Fact]
    public void TimerBoxGetsItsInstanceAndOperandsAreTyped()
    {
        var e = MotorBlank();
        e.Select(new LadderSelection(0, P(0)));
        var r = e.InsertBox("TON");
        Assert.True(r.Done);
        Assert.Contains("T1", r.Message, StringComparison.Ordinal);
        Assert.Equal("TON", e.Find("T1")!.Type);
        var box = Assert.IsType<BoxElement>(e.SelectedElement);
        Assert.Equal("T#1s", box.Operands["PT"]);
        Assert.True(e.InsertBox("TON").Done);
        Assert.Equal("T2", ((BoxElement)e.SelectedElement!).Instance);

        Assert.True(e.InsertBox("ADD").Done);
        Assert.True(e.SetOperand("OUT", "Total").Done);
        Assert.Equal("INT", e.Find("Total")!.Type);
        Assert.True(e.SetOperand("IN1", "Total").Done);
        Assert.False(e.SetOperand("IN2", "5 apples").Done);
        Assert.Contains(e.BoolTagNames(), n => n == "T1.Q");
    }

    [Fact]
    public void UndoAndRedoWalkBackAndForth()
    {
        var e = MotorBlank();
        e.SetTag("Start");
        e.AddRung();
        Assert.Equal(2, e.Rungs.Count);
        e.Undo();
        Assert.Single(e.Rungs);
        e.Undo();
        Assert.Equal("", ((ContactElement)((SeriesElement)e.Rungs[0].Logic).Items[0]).Tag);
        Assert.False(e.CanUndo);
        e.Redo();
        e.Redo();
        Assert.Equal(2, e.Rungs.Count);
        Assert.False(e.CanRedo);
        e.DeleteRung();
        Assert.False(e.CanRedo); // a new edit clears redo
    }

    [Fact]
    public void BranchAroundSeveralAndAnotherPath()
    {
        var e = MotorBlank();
        e.SetTag("Start");
        e.InsertContact(ContactKind.NormallyClosed);
        e.SetTag("Stop");
        e.Select(new LadderSelection(0, P(0)));
        e.ExtendSelection(+1);
        Assert.Equal(2, e.Selection!.Length);
        e.AddBranch();                                   // around Start and Stop
        var par = Assert.IsType<ParallelElement>(((SeriesElement)e.Rungs[0].Logic).Items[0]);
        Assert.Equal(2, ((SeriesElement)par.Branches[0]).Items.Count);
        Assert.Equal(new LadderSelection(0, P(0, 1, 0)), e.Selection);
        e.AddBranch();                                   // the selection fills a path: a third path
        Assert.Equal(3, ((ParallelElement)((SeriesElement)e.Rungs[0].Logic).Items[0]).Branches.Count);
    }

    [Fact]
    public void DeletingAPathUnwrapsTheBranch()
    {
        var e = MotorBlank();
        e.SetTag("Start");
        e.AddBranch();
        e.SetTag("Motor");
        e.DeleteSelected();                              // the second path goes, so the branch does
        var root = (SeriesElement)e.Rungs[0].Logic;
        Assert.IsType<ContactElement>(root.Items[0]);
        Assert.Equal(2, root.Items.Count);
        Assert.Equal(new LadderSelection(0, P(0)), e.Selection);
    }

    [Fact]
    public void CopyPasteAndMoveRungs()
    {
        var e = MotorBlank();
        e.SetTag("Start");
        e.SetRungComment(0, "first");
        var copied = e.CopyRung();
        Assert.True(e.PasteRung(copied).Done);
        Assert.Equal(2, e.Rungs.Count);
        Assert.Equal("first", e.Rungs[1].Comment);
        Assert.False(e.PasteRung("not a rung").Done);
        e.SetRungComment(1, "second");
        e.MoveRung(-1);
        Assert.Equal("second", e.Rungs[0].Comment);
        Assert.False(e.MoveRung(-1).Done);
    }

    [Fact]
    public void KindsChange()
    {
        var e = MotorBlank();
        Assert.True(e.SetContactKind(ContactKind.NormallyClosed).Done);
        Assert.False(e.SetCoilKind(CoilKind.Set).Done);
        e.MoveSelection(+1);
        Assert.True(e.SetCoilKind(CoilKind.Set).Done);
        Assert.Equal(CoilKind.Set, ((CoilElement)e.SelectedElement!).Coil);
    }

    [Fact]
    public void SelectionMovesAcrossRungs()
    {
        var e = MotorBlank();
        e.AddRung();
        e.Select(new LadderSelection(0, P(0)));
        e.MoveSelection(+1);
        e.MoveSelection(+1);
        Assert.Equal(new LadderSelection(1, P(0)), e.Selection);
        e.MoveSelection(-1);
        Assert.Equal(new LadderSelection(0, P(1)), e.Selection);
        e.MoveRungSelection(+1);
        Assert.Equal(1, e.Selection!.Rung);
    }

    [Fact]
    public void RenameReachesEveryUse()
    {
        var e = MotorBlank();
        e.SetTag("Start");
        e.InsertBox("TON");
        Assert.True(e.RenameTag("T1", "Delay").Done);
        Assert.Equal("Delay", ((BoxElement)e.SelectedElement!).Instance);
        Assert.True(e.RenameTag("Start", "Go").Done);
        Assert.Equal("Go", ((ContactElement)((SeriesElement)e.Rungs[0].Logic).Items[0]).Tag);
        Assert.False(e.RenameTag("Go", "Motor").Done);
    }

    [Fact]
    public void ExistingProgramsAreNormalizedWithoutChangingWhatTheyDo()
    {
        var original = ProjectJson.Deserialize(Content("programs", "motor-seal-in.json"));
        var e = new LadderEditor(original, IecDialect.Instance);
        var root = (SeriesElement)e.Rungs[0].Logic;
        var par = Assert.IsType<ParallelElement>(root.Items[0]);
        Assert.All(par.Branches, b => Assert.IsType<SeriesElement>(b));
        var outcome = new PlcChallengeRunner(id => Content("scenes", id + ".json"))
            .Run(Content("challenges", "motor-start-stop.json"), ProjectJson.Serialize(e.Project));
        Assert.True(outcome.Passed);
        Assert.Equal(5, e.Leaves(0).Count); // Start, Motor, the whole branch, Stop, the coil
    }

    [Fact]
    public void TheWholeBranchCanBeSelectedAndThingsGoAfterIt()
    {
        var e = MotorBlank();
        e.SetTag("Start");
        e.AddBranch();
        e.SetTag("Motor");
        e.MoveSelection(+1);                              // from Motor to the whole branch
        Assert.IsType<ParallelElement>(e.SelectedElement);
        e.InsertContact(ContactKind.NormallyOpen);        // after the branch, not inside it
        Assert.Equal(new LadderSelection(0, P(1)), e.Selection);
        Assert.Equal(3, ((SeriesElement)e.Rungs[0].Logic).Items.Count);
    }

    [Fact]
    public void UndoSaysWhatItUndidAndReplacingIsUndoable()
    {
        var e = MotorBlank();
        e.AddBranch();
        Assert.Equal("Undone: added a branch.", e.Undo().Message);
        e.ReplaceProgram(new PlcProject(), "Started again");
        Assert.Empty(e.Rungs);
        Assert.Equal("Undone: Started again.", e.Undo().Message);
        Assert.Single(e.Rungs);
    }

    [Fact]
    public void ATypoIsCaughtTwice()
    {
        var e = MotorBlank();
        var r = e.SetTag("Strat");
        Assert.Contains("Did you mean Start?", r.Message, StringComparison.Ordinal);
        Assert.Contains(e.Diagnostics, d => d.Severity == Severity.Warning && d.ElementPath == "1.1"
            && d.Message.Contains("Nothing ever turns 'Strat' on", StringComparison.Ordinal));
        Assert.DoesNotContain("Did you mean", e.SetTag("Conveyor").Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProblemsPointAtTheElement()
    {
        var e = MotorBlank();
        var d = e.Diagnostics.Where(x => x.Severity == Severity.Error).ToList();
        Assert.Contains(d, x => x.Rung == 1 && x.ElementPath == "1.1" && x.Message.Contains("no tag", StringComparison.Ordinal));
        Assert.Contains(d, x => x.Rung == 1 && x.ElementPath == "1.2");
    }
}
