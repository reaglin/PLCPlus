using Learning.Core;
using Learning.Core.Content;
using Learning.Core.Progress;

namespace Learning.Core.Tests;

internal static class Repo
{
    public static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "PLCPlus.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("PLCPlus.slnx not found above the test folder.");
    }

    public static string Foundations => Path.Combine(Root(), "content", "plcplus-foundations");

    /// <summary>A throwaway package folder for a test.</summary>
    public static string TempPackage(Dictionary<string, string> files)
    {
        var dir = Path.Combine(Path.GetTempPath(), "plcplus-test-" + Guid.NewGuid().ToString("N"));
        foreach (var (name, text) in files)
        {
            var path = Path.Combine(dir, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }
        return dir;
    }
}

public class FrontMatterTests
{
    [Fact]
    public void ReadsValuesListsAndTheBody()
    {
        var (m, body) = FrontMatter.Parse("---\nid: plc.m5\ntitle: The seal-in: start and stop\nlist: [a, b , c]\noutcomes:\n  - x.o1: First\n  - x.o2: Second\n---\n\n# Heading\nText", "test.md");
        Assert.Equal("plc.m5", m.Get("id"));
        Assert.Equal("The seal-in: start and stop", m.Get("title")); // only the first colon separates
        Assert.Equal(["a", "b", "c"], m.List("list"));
        Assert.Equal(["x.o1: First", "x.o2: Second"], m.List("outcomes"));
        Assert.Equal("# Heading\nText", body);
    }

    [Fact]
    public void AnUnclosedBlockOrABadLineIsReportedWithTheFile()
    {
        Assert.Contains("never end", Assert.Throws<ContentException>(() => FrontMatter.Parse("---\nid: x\n", "a.md")).Message);
        var bad = Assert.Throws<ContentException>(() => FrontMatter.Parse("---\njust words\n---\n", "b.md"));
        Assert.Contains("b.md, line 2", bad.Message);
    }

    [Fact]
    public void AFileWithoutSettingsIsAllBody() =>
        Assert.Equal("# Only text", FrontMatter.Parse("# Only text", "c.md").Body);
}

public class PackageTests
{
    private static ContentPackage Foundations() => ContentPackage.Load(Repo.Foundations);

    [Fact]
    public void TheFoundationsPackageHasTwelveModulesInOrder()
    {
        var p = Foundations();
        Assert.Equal(Enumerable.Range(1, 12), p.Modules.Select(m => m.Number));
        Assert.All(p.Modules.Where(m => m.Number >= 7), m => Assert.Equal(ModuleStatus.ComingLater, m.Status));
        Assert.Equal(ModuleStatus.Draft, p.Modules[2].Status);
        Assert.Equal(ModuleStatus.Draft, p.Modules[4].Status);
    }

    [Fact]
    public void ItemsAreNumberedInFileOrderAndEveryModuleEndsWithItsCheck()
    {
        var m5 = Foundations().FindModule("plc.m5")!;
        Assert.Equal(["5.1", "5.2", "5.3", "5.4", "5.5", "5.6"], m5.Items.Select(i => i.Number));
        Assert.Equal("u5.motor-start-stop", m5.Items[1].Exercise);
        Assert.Equal(ItemKind.Assess, m5.Items[^1].Kind);
        Assert.Equal(5, m5.Items[^1].Covers.Count);
        foreach (var m in Foundations().Modules.Where(m => m.Items.Count > 0))
            Assert.Equal(ItemKind.Assess, m.Items[^1].Kind);
    }

    [Fact]
    public void TheShippedPackageIsValid()
    {
        var p = Foundations();
        var problems = new ContentValidator().Validate(p);
        Assert.Empty(problems);
    }

    [Fact]
    public void TheValidatorFindsBrokenReferences()
    {
        var dir = Repo.TempPackage(new()
        {
            ["manifest.json"] = """{ "id": "t", "version": "1", "title": "T", "modules": ["modules/a", "modules/b"] }""",
            ["modules/a/module.md"] = "---\nid: m.a\nnumber: 1\ntitle: A\nprerequisites: [m.b, m.zz]\noutcomes:\n  - m.a.o1: Do a thing\n---\nIntro",
            ["modules/a/01-x.md"] = "---\nid: m.a.x\ntitle: X\nobjectives: [m.a.o9]\nreference: [nope]\nexercise: no-such-challenge\n---\n\n[[ladder: missing.json]]",
            ["modules/a/02-x.md"] = "---\nid: m.a.x\ntitle: Same ID\n---\nText",
            ["modules/a/03-check.md"] = "---\nid: m.a.check\ntitle: Check\nkind: assess\ncovers: [m.a.x, m.a.gone]\n---\n",
            ["modules/b/module.md"] = "---\nid: m.b\nnumber: 2\ntitle: B\nstatus: draft\noutcomes:\n  - m.b.o1: B\n---\n",
            ["modules/b/01-empty.md"] = "---\nid: m.b.empty\ntitle: Empty\n---\n",
        });
        try
        {
            var validator = new ContentValidator
            {
                ChallengeExists = id => id == "real",
                CheckWidget = w => w.Name == "ladder" && w.Argument == "missing.json" ? "the program file missing.json is not in the package." : null,
            };
            var problems = validator.Validate(ContentPackage.Load(dir));
            string[] expected =
            [
                "prerequisite m.b comes later", "prerequisite m.zz is not a module", "objective m.a.o9", "reference page nope",
                "challenge no-such-challenge", "ID m.a.x is already used", "covers m.a.gone", "Learn page is empty", "missing.json",
            ];
            foreach (var e in expected) Assert.Contains(problems, p => p.Contains(e, StringComparison.Ordinal));
            Assert.Equal(expected.Length, problems.Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void WidgetsAreFoundOnTheirOwnLines()
    {
        var found = Widgets.In("Text\n[[truth-table: A AND B]]\nnot [[ladder: x]] inline\n  [[ladder: programs/a.json]]  ").ToList();
        Assert.Equal([("truth-table", "A AND B", 2), ("ladder", "programs/a.json", 4)], found.Select(w => (w.Name, w.Argument, w.Line)));
    }
}

public class BooleanExpressionTests
{
    [Fact]
    public void TheSealInTable()
    {
        var e = BooleanExpression.Parse("(Start OR Motor) AND Stop");
        Assert.Equal(["Start", "Motor", "Stop"], e.Variables);
        var rows = e.Rows().ToList();
        Assert.Equal(8, rows.Count);
        Assert.Equal([false, false, false, true, false, true, false, true], rows.Select(r => r.Result));
    }

    [Theory]
    [InlineData("NOT A AND B", "FTFF")]   // NOT binds tighter than AND
    [InlineData("A OR B AND NOT B", "FFTT")]
    [InlineData("A XOR B", "FTTF")]
    public void Precedence(string text, string expected) =>
        Assert.Equal(expected, string.Concat(BooleanExpression.Parse(text).Rows().Select(r => r.Result ? 'T' : 'F')));

    [Theory]
    [InlineData("A AND")]
    [InlineData("(A OR B")]
    [InlineData("A & B")]
    [InlineData("A B")]
    [InlineData("A AND B AND C AND D AND E")]
    public void MistakesAreReported(string text) => Assert.Throws<ContentException>(() => BooleanExpression.Parse(text));
}

public class ProgressTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private static ChallengeOutcome Outcome(bool passed, double score = 3) =>
        new("c", true, [], [], passed ? 3 : score, 3, passed, "hash");

    [Fact]
    public void AReadingIsCompleteWhenRead_AnExerciseWhenItPasses()
    {
        var p = ContentPackage.Load(Repo.Foundations);
        var progress = new LearningProgress();
        var reading = p.FindItem("plc.m3.relays")!;
        var exercise = p.FindItem("plc.m5.seal-in")!;

        progress.Opened(p, reading, T0);
        Assert.Equal(ItemState.InProgress, progress.StateOf(reading));
        Assert.True(progress.MarkRead(reading, T0));
        Assert.Equal(ItemState.Complete, progress.StateOf(reading));

        Assert.False(progress.MarkRead(exercise, T0)); // an exercise cannot be "read" to completion
        progress.Record(exercise, Outcome(false, 1), T0);
        Assert.Equal(ItemState.InProgress, progress.StateOf(exercise));
        progress.Record(exercise, Outcome(true), T0.AddMinutes(5));
        Assert.Equal(ItemState.Complete, progress.StateOf(exercise));
        Assert.Equal(2, progress.Attempts.Count);
        Assert.True(progress.Best(exercise)!.Passed);
    }

    [Fact]
    public void TheModuleCheckIsCompleteWhenEverythingItCoversIs()
    {
        var p = ContentPackage.Load(Repo.Foundations);
        var progress = new LearningProgress();
        var m3 = p.FindModule("plc.m3")!;
        var check = m3.Items[^1];
        Assert.Equal(new ModuleProgress(ModuleState.NotStarted, 0, 5), progress.Of(m3));

        foreach (var item in m3.Items.Take(3)) progress.MarkRead(item, T0);
        Assert.Equal(ItemState.InProgress, progress.StateOf(check));
        Assert.Equal(new ModuleProgress(ModuleState.InProgress, 3, 5), progress.Of(m3));

        progress.MarkRead(m3.Items[3], T0);
        Assert.Equal(ItemState.InProgress, progress.StateOf(check)); // its own questions are still to pass
        progress.Record(check, QuizGrader.Grade(check.Quiz!, QuizTests.AllRight(check.Quiz!)).ToOutcome(), T0);
        Assert.Equal(ItemState.Complete, progress.StateOf(check));
        Assert.Equal(new ModuleProgress(ModuleState.Complete, 5, 5), progress.Of(m3));
        Assert.Equal(ModuleState.ComingLater, progress.Of(p.FindModule("plc.m9")!).State);
    }

    [Fact]
    public void OpeningAModuleEarlyIsAllowedAndRecordedOnce()
    {
        var p = ContentPackage.Load(Repo.Foundations);
        var progress = new LearningProgress();
        var m6 = p.FindModule("plc.m6")!;
        Assert.Equal(["plc.m5"], progress.MissingPrerequisites(p, m6).Select(m => m.Id));

        progress.Opened(p, m6.Items[0], T0);
        progress.Opened(p, m6.Items[1], T0);
        progress.Opened(p, m6.Items[0], T0.AddHours(1));
        var e = Assert.Single(progress.Events);
        Assert.Equal(LearningProgress.OpenedEarly, e.Kind);
        Assert.Contains("Module 5", e.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOutlineIsNotAMissingPrerequisite()
    {
        var p = ContentPackage.Load(Repo.Foundations);
        Assert.Empty(new LearningProgress().MissingPrerequisites(p, p.FindModule("plc.m5")!)); // module 4 is an outline
    }

    [Fact]
    public void NextIsTheFirstWrittenItemNotComplete()
    {
        var p = ContentPackage.Load(Repo.Foundations);
        var progress = new LearningProgress();
        Assert.Equal("3.1", progress.Next(p)!.Number); // modules 1 and 2 are outlines
        foreach (var item in p.FindModule("plc.m3")!.Items) progress.MarkRead(item, T0);
        Assert.Equal("3.5", progress.Next(p)!.Number); // the module check's questions come next
    }

    [Fact]
    public void NextAfterAnItemIsTheNextNotCompleteAfterIt_AndContinueGoesBackToWhereYouWere()
    {
        var p = ContentPackage.Load(Repo.Foundations);
        var progress = new LearningProgress();
        progress.MarkRead(p.FindItem("plc.m3.relays")!, T0);
        progress.MarkRead(p.FindItem("plc.m3.seal-in")!, T0);
        Assert.Equal("3.4", progress.NextAfter(p, p.FindItem("plc.m3.seal-in")!)!.Number);
        Assert.Equal("3.2", progress.NextAfter(p, p.FindItem("plc.m5.check")!)!.Number); // past the end: back to the start

        progress.Opened(p, p.FindItem("plc.m5.seal-in")!, T0.AddMinutes(1));
        Assert.Equal("5.2", progress.Continue(p)!.Number);
        progress.Record(p.FindItem("plc.m5.seal-in")!, Outcome(true), T0.AddMinutes(2));
        Assert.Equal("5.3", progress.Continue(p)!.Number);
    }

    [Fact]
    public void ProgressSurvivesTheFile()
    {
        var p = ContentPackage.Load(Repo.Foundations);
        var progress = new LearningProgress();
        progress.Opened(p, p.FindItem("plc.m6.ton")!, T0); // early: module 5 is not complete
        progress.Record(p.FindItem("plc.m5.seal-in")!, Outcome(true), T0);
        var path = Path.Combine(Path.GetTempPath(), "plcplus-progress-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            progress.Save(path);
            var back = LearningProgress.Load(path);
            Assert.Equal(ItemState.Complete, back.StateOf(p.FindItem("plc.m5.seal-in")!));
            Assert.Single(back.Attempts);
            Assert.Single(back.Events);

            File.WriteAllText(path, "{ not json");
            Assert.Empty(LearningProgress.Load(path, out var problem).Items); // a damaged file starts afresh…
            Assert.True(File.Exists(path + ".damaged"));                     // …is kept…
            Assert.Contains(".damaged", problem, StringComparison.Ordinal);   // …and the learner is told
        }
        finally
        {
            foreach (var f in new[] { path, path + ".damaged" }) if (File.Exists(f)) File.Delete(f);
        }
    }
}
