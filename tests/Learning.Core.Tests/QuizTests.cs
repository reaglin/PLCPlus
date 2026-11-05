using Learning.Core.Content;
using Learning.Core.Progress;

namespace Learning.Core.Tests;

/// <summary>Phase 5.5 and 5.6: questions of every kind, module checks with questions, and mastery per outcome.</summary>
public class QuizTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);
    private static ContentPackage P() => ContentPackage.Load(Repo.Foundations);

    private static QuestionAnswer Chose(params int[] i) => new() { Chosen = [.. i] };

    [Fact]
    public void ChoiceAndMultiple()
    {
        var q1 = new Question { Id = "a", Kind = QuestionKind.Choice, Options = ["x", "y"], Correct = [1], Explanation = "Because." };
        var q2 = new Question { Id = "b", Kind = QuestionKind.Multiple, Options = ["x", "y", "z"], Correct = [0, 2] };
        var quiz = new Quiz { Id = "t", Questions = [q1, q2], Practice = true };
        var right = QuizGrader.Grade(quiz, new Dictionary<string, QuestionAnswer> { ["a"] = Chose(1), ["b"] = Chose(2, 0) });
        Assert.True(right.Passed);
        Assert.StartsWith("Right. Because.", right.Questions[0].Feedback, StringComparison.Ordinal);

        var wrong = QuizGrader.Grade(quiz, new Dictionary<string, QuestionAnswer> { ["a"] = Chose(0), ["b"] = Chose(0) });
        Assert.Equal(0, wrong.Score);
        Assert.Contains("The answer is \"y\"", wrong.Questions[0].Feedback, StringComparison.Ordinal);
        Assert.Contains("\"x\", \"z\"", wrong.Questions[1].Feedback, StringComparison.Ordinal);
    }

    [Fact]
    public void ACheckDoesNotGiveTheAnswerAway()
    {
        var q = new Question { Id = "a", Kind = QuestionKind.Choice, Options = ["x", "y"], Correct = [1], Explanation = "Because y." };
        var quiz = new Quiz { Questions = [q] };
        var wrong = QuizGrader.Grade(quiz, new Dictionary<string, QuestionAnswer> { ["a"] = Chose(0) }).Questions[0].Feedback;
        Assert.DoesNotContain("y", wrong.Replace("you", "", StringComparison.Ordinal).Replace("your", "", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Equal("Not answered yet.", QuizGrader.Grade(quiz, new Dictionary<string, QuestionAnswer>()).Questions[0].Feedback);
        Assert.Contains("Because y.", QuizGrader.Grade(quiz, new Dictionary<string, QuestionAnswer> { ["a"] = Chose(1) }).Questions[0].Feedback, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("100", true)]
    [InlineData("100.5 mA", true)]
    [InlineData("99", true)]
    [InlineData("102", false)]
    [InlineData("0.1", false)]
    [InlineData("", false)]
    public void NumericWithinTolerance(string typed, bool right)
    {
        var q = new Question { Id = "n", Kind = QuestionKind.Numeric, Answer = 100, Tolerance = 1, Unit = "mA" };
        var r = QuizGrader.Grade(new Quiz { Questions = [q] }, new Dictionary<string, QuestionAnswer> { ["n"] = new() { Text = typed } });
        Assert.Equal(right, r.Questions[0].Correct);
        Assert.Equal(typed.Length > 0, r.Questions[0].Answered);
    }

    [Fact]
    public void TruthTableNeedsEveryRowRight()
    {
        var q = new Question { Id = "t", Kind = QuestionKind.TruthTable, Expression = "A AND B" };
        Dictionary<string, QuestionAnswer> A(params bool?[] rows) => new() { ["t"] = new() { Rows = [.. rows] } };
        var quiz = new Quiz { Questions = [q], Practice = true };
        Assert.True(QuizGrader.Grade(quiz, A(false, false, false, true)).Passed);
        Assert.False(QuizGrader.Grade(quiz, A(false, false, false, null)).Passed);   // one row blank
        Assert.False(QuizGrader.Grade(quiz, A(false, true, false, true)).Passed);
        Assert.Contains("FALSE, FALSE, FALSE, TRUE", QuizGrader.Grade(quiz, A(true, true, true, true)).Questions[0].Feedback, StringComparison.Ordinal);
    }

    [Fact]
    public void MatchUsesTheAlphabeticalChoices()
    {
        var q = new Question { Id = "m", Kind = QuestionKind.Match, Pairs = [new("-( )-", "Coil"), new("-| |-", "Normally open contact")] };
        Assert.Equal(["Coil", "Normally open contact"], q.MatchChoices);
        var quiz = new Quiz { Questions = [q] };
        Assert.True(QuizGrader.Grade(quiz, new Dictionary<string, QuestionAnswer> { ["m"] = new() { Matches = [0, 1] } }).Passed);
        Assert.False(QuizGrader.Grade(quiz, new Dictionary<string, QuestionAnswer> { ["m"] = new() { Matches = [1, 0] } }).Passed);
    }

    [Fact]
    public void ThePassMarkIsTheQuizzes()
    {
        var qs = Enumerable.Range(0, 5).Select(i => new Question { Id = $"q{i}", Kind = QuestionKind.Choice, Options = ["a", "b"], Correct = [0] }).ToList();
        var quiz = new Quiz { Questions = qs, PassThreshold = 0.8 };
        var four = qs.Take(4).ToDictionary(q => q.Id, _ => Chose(0));
        var three = qs.Take(3).ToDictionary(q => q.Id, _ => Chose(0));
        Assert.True(QuizGrader.Grade(quiz, four).Passed);
        Assert.False(QuizGrader.Grade(quiz, three).Passed);
        Assert.False(QuizGrader.Grade(quiz, three).Questions[4].Answered);
    }

    [Fact]
    public void TheShippedQuizzesLoad()
    {
        var p = P();
        Assert.Equal(6, p.FindItem("plc.m3.check")!.Quiz!.Questions.Count);
        Assert.Equal(2, p.FindItem("plc.m5.check")!.Quiz!.Questions.Count(q => q.Kind == QuestionKind.Fault));
        Assert.False(p.FindItem("plc.m2.and-or-not")!.CompletesByReading);
    }

    /// <summary>Every right answer to a quiz, as the learner would give them.</summary>
    internal static Dictionary<string, QuestionAnswer> AllRight(Quiz quiz) => quiz.Questions.ToDictionary(q => q.Id, q => q.Kind switch
    {
        QuestionKind.Numeric => new QuestionAnswer { Text = q.Answer!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) },
        QuestionKind.TruthTable => new QuestionAnswer { Rows = [.. BooleanExpression.Parse(q.Expression!).Rows().Select(r => (bool?)r.Result)] },
        QuestionKind.Match => new QuestionAnswer { Matches = [.. q.Pairs.Select(pair => q.MatchChoices.ToList().IndexOf(pair.Right))] },
        _ => new QuestionAnswer { Chosen = [.. q.Correct] },
    });

    [Fact]
    public void EveryShippedQuizCanBePassed()
    {
        foreach (var item in P().AllItems.Where(i => i.Quiz is not null))
            Assert.True(QuizGrader.Grade(item.Quiz!, AllRight(item.Quiz!)).Passed, item.ToString());
    }

    [Fact]
    public void AModuleCheckWithQuestionsNeedsThemPassedToo()
    {
        var p = P();
        var progress = new LearningProgress();
        var check = p.FindItem("plc.m3.check")!;
        foreach (var c in check.Covers) progress.MarkRead(p.FindItem(c)!, T0);
        Assert.Equal(ItemState.InProgress, progress.StateOf(check)); // the items are read, the questions are not passed

        var wrong = QuizGrader.Grade(check.Quiz!, new Dictionary<string, QuestionAnswer>());
        progress.Record(check, wrong.ToOutcome(), T0, machine: "LAB-PC-07");
        Assert.Equal(ItemState.InProgress, progress.StateOf(check));
        Assert.Equal("LAB-PC-07", progress.Attempts[^1].Machine);
        Assert.False(progress.Attempts[^1].Questions!["q1"]);

        progress.Record(check, QuizGrader.Grade(check.Quiz!, AllRight(check.Quiz!)).ToOutcome(), T0.AddMinutes(5));
        Assert.Equal(ItemState.Complete, progress.StateOf(check));
        Assert.Equal(ModuleState.Complete, progress.Of(check.Module).State);
    }

    [Fact]
    public void MasteryFollowsTheItemsAndTheQuestions()
    {
        var p = P();
        var progress = new LearningProgress();
        var m3 = p.FindModule("plc.m3")!;
        MasteryState Of(string id) => Mastery.Of(p, progress, m3.Outcomes.First(o => o.Id == id)).State;

        Assert.Equal(MasteryState.NotStarted, Of("plc.m3.o1"));
        progress.MarkRead(p.FindItem("plc.m3.relays")!, T0);
        Assert.Equal(MasteryState.InProgress, Of("plc.m3.o1"));     // read, but its questions are not answered

        var check = p.FindItem("plc.m3.check")!;
        var answers = AllRight(check.Quiz!);
        answers["q6"] = new QuestionAnswer { Text = "0.1" };         // the Ohm's law question wrong (amps, not milliamps)
        progress.Record(check, QuizGrader.Grade(check.Quiz!, answers).ToOutcome(), T0);
        var o1 = Mastery.Of(p, progress, m3.Outcomes[0]);
        Assert.Equal(MasteryState.NeedsReview, o1.State);           // the reading is done, but a question on it was wrong
        Assert.Contains(o1.Evidence, e => e.Contains("question 6 of 3.5", StringComparison.Ordinal) && e.Contains("Read 3.1", StringComparison.Ordinal));

        progress.Record(check, QuizGrader.Grade(check.Quiz!, AllRight(check.Quiz!)).ToOutcome(), T0.AddMinutes(3));
        Assert.Equal(MasteryState.Mastered, Of("plc.m3.o1"));
    }

    [Fact]
    public void TheValidatorFindsBrokenQuestions()
    {
        var dir = Repo.TempPackage(new()
        {
            ["manifest.json"] = """{ "id": "t", "version": "1", "title": "T", "modules": ["modules/a"] }""",
            ["modules/a/module.md"] = "---\nid: m.a\nnumber: 1\ntitle: A\noutcomes:\n  - m.a.o1: Do\n---\n",
            ["modules/a/01-x.md"] = "---\nid: m.a.x\ntitle: X\nquiz: q.json\n---\nText",
            ["q.json"] = """
                { "id": "q", "questions": [
                  { "id": "a", "kind": "choice", "prompt": "P", "options": ["one"], "correct": [3] },
                  { "id": "a", "kind": "numeric", "prompt": "P" },
                  { "id": "c", "kind": "truthTable", "prompt": "P", "expression": "A AND" },
                  { "id": "d", "kind": "fault", "prompt": "P", "options": ["x", "y"], "correct": [0] },
                  { "id": "e", "kind": "match", "prompt": "", "pairs": [{ "left": "x", "right": "y" }], "objective": "m.zz" }
                ] }
                """,
        });
        try
        {
            var problems = new ContentValidator().Validate(ContentPackage.Load(dir));
            string[] expected = ["at least two options", "exactly one right option", "its own id", "needs its answer", "truth table", "needs a machine",
                                 "at least two pairs", "no prompt", "m.zz"];
            foreach (var e in expected) Assert.Contains(problems, p => p.Contains(e, StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class QuizFeedbackTests
{
    [Fact]
    public void ACheckSaysWhereToLookAndExplainsHalfDoneAnswers()
    {
        var where = new Question { Id = "a", Kind = QuestionKind.Choice, Options = ["x", "y"], Correct = [1] };
        var fault = new Question { Id = "f", Kind = QuestionKind.Fault, Options = ["x", "y"], Correct = [1] };
        var table = new Question { Id = "t", Kind = QuestionKind.TruthTable, Expression = "A AND B" };
        var number = new Question { Id = "n", Kind = QuestionKind.Numeric, Answer = 1 };
        var quiz = new Quiz { Questions = [where, fault, table, number] };
        var r = QuizGrader.Grade(quiz, new Dictionary<string, QuestionAnswer>
        {
            ["a"] = new() { Chosen = [0] }, ["f"] = new() { Chosen = [0] },
            ["t"] = new() { Rows = [false, null, false, null] }, ["n"] = new() { Text = "abc" },
        }, q => q.Id == "a" ? "3.3 The seal-in circuit" : null);
        Assert.Contains("Look again at 3.3 The seal-in circuit", r.Questions[0].Feedback, StringComparison.Ordinal);
        Assert.Contains("Work the machine again", r.Questions[1].Feedback, StringComparison.Ordinal);
        Assert.Equal("Rows 2 and 4 are still ?: choose TRUE or FALSE for every row.", r.Questions[2].Feedback);
        Assert.Contains("\"abc\" is not a number", r.Questions[3].Feedback, StringComparison.Ordinal);
    }
}
