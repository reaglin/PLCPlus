using System.Text;
using Learning.Core.Content;
using Learning.Core.Learners;
using Learning.Core.Progress;
using Learning.Core.Reports;
using Learning.Reports.Pdf;

namespace Learning.Core.Tests;

/// <summary>Plan 8.1–8.8: the snapshot, the PDF, the seal, the result line and the verifier.</summary>
public class ReportTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 5, 14, 0, 0, TimeSpan.Zero);
    private static readonly ContentPackage P = ContentPackage.Load(Repo.Foundations);
    private static readonly ReportOptions Options = new("PLC+", "0.8.0", "PLC1", "PLCPlus");
    private static readonly ReportWords Words = new("In PLC+: File ▸ Check a report.", "Counted only while the learner was using PLC+.");
    private const string Program = "{\"rungs\":[\"XIC Start OTE Lamp\"]}";

    private static LearnerData Learner(string? classCode = null)
    {
        var data = new LearnerData { Profile = new LearnerProfile("p1", "Maria  Gomez", null, T0) };
        data.ReportDetails = new ReportDetails { StudentId = "N0123", Course = "ETD 1100", ClassCode = classCode };
        return data;
    }

    private static ChallengeOutcome Passed(string challenge, string work) =>
        new(challenge, true, [], [new ScenarioOutcome("Start lights the lamp", false, true, 1, 1, null, null), new ScenarioOutcome("Power fails", true, true, 1, 1, null, null)],
            2, 2, true, WorkHash.Of(work));

    private static LearnerData HalfOfModule5(string? classCode = null)
    {
        var data = Learner(classCode);
        var first = P.FindItem("plc.m5.contacts-coils")!;
        data.Progress.Record(first, Passed("u5.pilot-lights", Program), T0, hintsShown: 2);
        data.Work[ReportBuilder.PassedWorkKey("u5.pilot-lights")] = new SavedWork { Content = Program, Saved = T0 };
        var second = P.FindItem("plc.m5.seal-in")!;
        data.Progress.Record(second, new ChallengeOutcome("u5.motor-start-stop", true, [], [new ScenarioOutcome("Stop wins", false, false, 0, 1, "Stop", 1)], 0, 1, false, "x"), T0);
        LearningProgress.AddActiveTime(data.Progress.StartSession(T0, "lab-1"), first.Id, 600, T0);
        return data;
    }

    private static (byte[] Pdf, SealedReport Sealed, ProgressSnapshot Report, ComputerKey Key) Make(LearnerData data, string? point = "plc.m5.check")
    {
        var key = ComputerKey.New();
        var report = ReportBuilder.Build(P, data, point is null ? null : P.FindItem(point), Options, T0, key.Fingerprint);
        var pdf = ReportPdf.Render(report, key.PublicKey, Words, _ => null);
        return (pdf, ReportSeal.Seal(pdf, key, data.ReportDetails?.ClassCode), report, key);
    }

    [Fact]
    public void A_report_before_the_check_is_complete_is_incomplete_and_says_what_is_missing()
    {
        var r = ReportBuilder.Build(P, HalfOfModule5(), P.FindItem("plc.m5.check"), Options, T0);
        Assert.Equal(ReportStatus.Incomplete, r.Status);
        Assert.Equal("Module 5 check: " + P.FindItem("plc.m5.check")!.Module.Title, r.PointTitle);
        Assert.Equal(6, r.Exercises.Count);                       // five exercises and the check
        Assert.Equal(5, r.Missing.Count);                         // four items and the questions
        Assert.Contains(r.Missing, m => m.Contains("not passed yet (best 0 of 1)"));
        Assert.Contains(r.Missing, m => m.Contains("questions are not passed yet"));
        var first = r.Exercises[0];
        Assert.Equal("Complete", first.Status);
        Assert.Equal(2, first.HintsUsed);
        Assert.Equal(TimeSpan.FromMinutes(10), first.Time);
        Assert.Equal(["Start lights the lamp", "Hidden test 1"], first.Tests.Select(t => t.Name));   // D9: a hidden test is not named
        Assert.Equal(100.0 / 6, r.Summary.OverallScore!.Value, 3);   // one of six scored items at 100%, the rest 0
        Assert.Equal("N0123", r.Learner.StudentId);
        Assert.Single(r.Evidence);
        Assert.Equal(Program, r.Evidence[0].Program);
        Assert.Equal("PLCPlus_Gomez_Module5Check_2026-11-05.pdf", r.FileName("PLCPlus"));
    }

    [Fact]
    public void Program_evidence_is_left_out_when_the_learner_says_so_or_the_passing_program_is_not_kept()
    {
        var data = HalfOfModule5();
        data.ReportDetails!.IncludePrograms = false;
        Assert.Empty(ReportBuilder.Build(P, data, P.FindItem("plc.m5.check"), Options, T0).Evidence);
        data.ReportDetails.IncludePrograms = true;
        data.Work.Clear();
        data.Work["u5.pilot-lights"] = new SavedWork { Content = "{\"changed\":true}", Saved = T0 };   // My program changed since it passed
        Assert.Empty(ReportBuilder.Build(P, data, P.FindItem("plc.m5.check"), Options, T0).Evidence);
    }

    [Fact]
    public void The_full_report_covers_every_written_module()
    {
        var r = ReportBuilder.Build(P, HalfOfModule5(), null, Options, T0);
        Assert.Equal(SubmissionRecord.FullReport, r.PointId);
        Assert.Equal(11, r.Exercises.Count);
        Assert.Equal("PLCPlus_Gomez_FullReport_2026-11-05.pdf", r.FileName("PLCPlus"));
    }

    [Fact]
    public void A_sealed_report_is_genuine_and_tagged_and_says_what_it_said()
    {
        var (_, s, report, key) = Make(HalfOfModule5());
        var v = ReportSeal.Verify(s.Pdf);
        Assert.Equal(ReportCheck.Genuine, v.Outcome);
        Assert.Equal(key.Fingerprint, v.KeyFingerprint);
        Assert.Equal(report.ReportId, v.Snapshot!.ReportId);
        Assert.Equal(report.VerificationCode(), v.VerificationCode);
        Assert.Equal("Maria  Gomez", v.Snapshot.Learner.Name);
        var text = Encoding.Latin1.GetString(s.Pdf);
        Assert.StartsWith("%PDF-", text);
        Assert.Contains("/StructTreeRoot", text);   // tagged for screen readers
        Assert.Contains("/Lang", text);
        Assert.Contains("pdfaid", text);            // PDF/A identification in the metadata
        Assert.Equal(64, s.FileHash.Length);
    }

    [Fact]
    public void Any_change_to_the_file_shows()
    {
        var (_, s, _, _) = Make(HalfOfModule5());
        var text = Encoding.Latin1.GetString(s.Pdf);
        var edited = (byte[])s.Pdf.Clone();
        var at = text.IndexOf("/Author", StringComparison.Ordinal) + 10;
        edited[at] ^= 0x01;
        Assert.Equal(ReportCheck.Altered, ReportSeal.Verify(edited).Outcome);
        // An edit saved at the end of the file (as PDF editors do) shows too.
        Assert.Equal(ReportCheck.Altered, ReportSeal.Verify([.. s.Pdf, .. "\n% edited\n"u8.ToArray()]).Outcome);
        Assert.Equal(ReportCheck.NotAReport, ReportSeal.Verify("%PDF-1.7\n%%EOF"u8.ToArray()).Outcome);
    }

    [Fact]
    public void A_class_code_signs_the_report_and_its_line()
    {
        var code = ClassCode.New();
        var data = HalfOfModule5(code);
        var (_, s, report, _) = Make(data);
        Assert.True(report.ClassSigned);
        Assert.Equal(ClassCheck.Matches, ReportSeal.Verify(s.Pdf, code.ToLowerInvariant().Replace("0", "o")).Class);   // typed loosely
        Assert.Equal(ClassCheck.DoesNotMatch, ReportSeal.Verify(s.Pdf, ClassCode.New()).Class);

        var line = ResultLine.Issue(report, "PLC1", s.Signature, code).Text();
        Assert.StartsWith("PLC1|Maria Gomez|Module 5 check: ", line);
        Assert.Contains("|Incomplete, score 17%|2026-11-05|" + report.ReportId + "|", line);
        Assert.Equal(LineCheck.Matches, ResultLine.Check("My result: " + line + "\nthanks", "PLC1", code, null).Outcome);
        Assert.Equal(LineCheck.DoesNotMatch, ResultLine.Check(line.Replace("score 17%", "score 97%"), "PLC1", code, null).Outcome);
        Assert.Equal(LineCheck.CannotCheck, ResultLine.Check(line, "PLC1", null, s.Pdf).Outcome);   // genuine report, but the line needs the code
        Assert.Equal(ClassCheck.NotSigned, ReportSeal.Verify(Make(HalfOfModule5()).Sealed.Pdf, code).Class);
    }

    [Fact]
    public void Without_a_class_code_the_line_checks_against_its_own_report()
    {
        var (_, s, report, _) = Make(HalfOfModule5());
        var line = ResultLine.Issue(report, "PLC1", s.Signature, null).Text();
        Assert.Equal(LineCheck.Matches, ResultLine.Check(line, "PLC1", null, s.Pdf).Outcome);
        Assert.Equal(LineCheck.CannotCheck, ResultLine.Check(line, "PLC1", null, null).Outcome);
        Assert.Equal(LineCheck.DoesNotMatch, ResultLine.Check(line.Replace("Maria Gomez", "Mario Gomez"), "PLC1", null, s.Pdf).Outcome);
        Assert.Equal(LineCheck.DoesNotMatch, ResultLine.Check(line, "PLC1", null, Make(HalfOfModule5()).Sealed.Pdf).Outcome);   // another report
        Assert.Equal(LineCheck.NotALine, ResultLine.Check("8/10 done", "PLC1", null, null).Outcome);
    }

    [Fact]
    public void A_complete_module_check_makes_a_complete_report()
    {
        var data = Learner();
        foreach (var id in new[] { "plc.m3.relays", "plc.m3.ladder-diagrams", "plc.m3.seal-in", "plc.m3.interlocks" })
            data.Progress.MarkRead(P.FindItem(id)!, T0);
        var check = P.FindItem("plc.m3.check")!;
        data.Progress.Record(check, new ChallengeOutcome("quiz", true, [],
            [.. check.Quiz!.Questions.Select(q => new ScenarioOutcome(q.Id, false, true, 1, 1, null, null))], 6, 6, true, "answers"), T0);
        var r = ReportBuilder.Build(P, data, check, Options, T0);
        Assert.Equal(ReportStatus.Complete, r.Status);
        Assert.Empty(r.Missing);
        Assert.Equal(100, r.Summary.PercentComplete);
        Assert.Equal(100, r.Summary.OverallScore);                 // the check's questions are the only scored item
        Assert.Equal("Question 1", r.Exercises[^1].Tests[0].Name);
        Assert.All(r.Outcomes, o => Assert.Equal("Mastered", o.State));
    }

    [Fact]
    public void Class_codes_and_report_ids_read_the_way_people_type_them()
    {
        var code = ClassCode.New();
        Assert.Matches("^[0-9A-Z]{4}-[0-9A-Z]{4}-[0-9A-Z]{4}-[0-9A-Z]{4}$", code);
        Assert.Equal(code, ClassCode.Format(" " + code.Replace("-", " ").ToLowerInvariant() + " "));
        Assert.Null(ClassCode.Normalize("ABCD"));
        Assert.Null(ClassCode.Normalize("UUUU-UUUU-UUUU-UUUU"));   // U is not in the alphabet
        Assert.Matches("^R-[0-9A-Z]{4}-[0-9A-Z]{4}$", ReportBuilder.NewReportId());
    }

    [Fact]
    public void Test_results_and_report_details_survive_the_learners_file_and_a_merge()
    {
        var a = HalfOfModule5("AAAA-BBBB-CCCC-DDDD");
        var json = a.Progress.ToJson();
        Assert.Equal("Hidden test 1", LearningProgress.FromJson(json).Attempts[0].Tests![1].Name);
        var merged = LearnerData.Merge(new LearnerData { Profile = a.Profile }, a);
        Assert.Equal("ETD 1100", merged.ReportDetails!.Course);
    }
}

/// <summary>Writes sample reports to look at, when PLCPLUS_REPORT_SAMPLES names a folder (not part of the normal run).</summary>
public class ReportSamples
{
    [Fact]
    public void Write_samples_when_asked()
    {
        var folder = Environment.GetEnvironmentVariable("PLCPLUS_REPORT_SAMPLES");
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);
        var p = ContentPackage.Load(Repo.Foundations);
        var data = new LearnerData { Profile = new LearnerProfile("p1", "Maria Gomez", null, DateTimeOffset.Now) };
        data.ReportDetails = new ReportDetails { StudentId = "N0123", Course = "ETD 1100 PLC Fundamentals", Section = "02", ClassCode = ClassCode.New() };
        var item = p.FindItem("plc.m5.contacts-coils")!;
        data.Progress.Record(item, new ChallengeOutcome("u5.pilot-lights", true, [],
            [new ScenarioOutcome("Start lights the lamp", false, true, 1, 1, null, null), new ScenarioOutcome("Power fails", true, true, 1, 1, null, null)], 2, 2, true, WorkHash.Of("{}")), DateTimeOffset.Now, 1);
        var key = ComputerKey.New();
        foreach (var point in new[] { "plc.m5.check", null })
        {
            var r = ReportBuilder.Build(p, data, point is null ? null : p.FindItem(point), new ReportOptions("PLC+", "0.8.0", "PLC1", "PLCPlus"), DateTimeOffset.Now, key.Fingerprint);
            var pdf = ReportPdf.Render(r, key.PublicKey, new ReportWords("To check it, an instructor opens PLC+ and chooses File ▸ Check a report (for instructors).",
                "Counted only while the learner was using PLC+ (a key or the mouse in the last two minutes), so it is a lower bound."));
            File.WriteAllBytes(Path.Combine(folder, r.FileName("PLCPlus")), ReportSeal.Seal(pdf, key, data.ReportDetails.ClassCode).Pdf);
        }
    }

    /// <summary>
    /// A learner to look at the report screens with, when PLCPLUS_SAMPLE_LEARNERS names a learners' folder (for window captures):
    /// "Maria Gomez", PIN 2468, module 3 complete and two exercises of module 5 passed with the working programs.
    /// </summary>
    [Fact]
    public void Write_a_sample_learner_when_asked()
    {
        var folder = Environment.GetEnvironmentVariable("PLCPLUS_SAMPLE_LEARNERS");
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);
        var p = ContentPackage.Load(Repo.Foundations);
        var now = DateTimeOffset.Now.AddHours(-2);
        var data = new LearnerData { Profile = null! };
        foreach (var id in new[] { "plc.m3.relays", "plc.m3.ladder-diagrams", "plc.m3.seal-in", "plc.m3.interlocks" })
            data.Progress.MarkRead(p.FindItem(id)!, now);
        var check = p.FindItem("plc.m3.check")!;
        data.Progress.Record(check, new ChallengeOutcome("quiz", true, [],
            [.. check.Quiz!.Questions.Select(q => new ScenarioOutcome(q.Id, false, true, 1, 1, null, null))], 6, 6, true, "answers"), now);
        foreach (var (item, challenge, file) in new[] { ("plc.m5.contacts-coils", "u5.pilot-lights", "pilot-lights.json"), ("plc.m5.seal-in", "u5.motor-start-stop", "motor-seal-in.json") })
        {
            var program = File.ReadAllText(Path.Combine(Repo.Foundations, "programs", file));
            data.Progress.Record(p.FindItem(item)!, new ChallengeOutcome(challenge, true, [],
                [new ScenarioOutcome("Works as asked", false, true, 1, 1, null, null), new ScenarioOutcome("x", true, true, 1, 1, null, null)], 2, 2, true, WorkHash.Of(program)), now, 1);
            data.Work[ReportBuilder.PassedWorkKey(challenge)] = new SavedWork { Content = program, Saved = now };
        }
        var session = data.Progress.StartSession(now, "lab-1");
        LearningProgress.AddActiveTime(session, "plc.m5.seal-in", 1500, now);
        LearningProgress.AddActiveTime(session, "plc.m3.relays", 900, now);
        var store = new LearnerStore(folder, new LearnerFileOptions("PLCPlus", ".plcprogress", "0.8.0", "test"));
        var (file2, _) = store.Create("Maria Gomez", null, "2468", now, data);
        file2.Data.LastBackup = DateTimeOffset.Now;
        file2.Save(DateTimeOffset.Now);
        file2.Dispose();
    }
}
