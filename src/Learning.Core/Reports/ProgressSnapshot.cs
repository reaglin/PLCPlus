using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Learning.Core.Content;
using Learning.Core.Learners;
using Learning.Core.Progress;

namespace Learning.Core.Reports;

/// <summary>
/// What the learner typed for the cover of their reports (plan 8.4a, D19): only the name is required, and it is the name in
/// their profile. The class code is the instructor's (D12); with it, a report also proves it came from that class.
/// </summary>
public sealed class ReportDetails
{
    public string? StudentId { get; set; }
    public string? Course { get; set; }
    public string? Section { get; set; }
    public string? Email { get; set; }
    /// <summary>The instructor's e-mail, for Send to my instructor (plan 8.5).</summary>
    public string? InstructorEmail { get; set; }
    public string? ClassCode { get; set; }
    /// <summary>Put pictures of the programs that passed in the report (spec 9.2, program evidence; optional).</summary>
    public bool IncludePrograms { get; set; } = true;
    /// <summary>The learner has seen and saved these details once (the first report asks for them).</summary>
    public bool Confirmed { get; set; }
    /// <summary>The folder the last report was saved in, so the next one starts there.</summary>
    public string? Folder { get; set; }
}

public enum ReportStatus { Complete, Incomplete }

public sealed record ReportLearner(string Name, string? StudentId, string? Course, string? Section, string? Email);

/// <summary>One item's line in the report's exercise detail (spec 9.2).</summary>
public sealed record ExerciseDetail(string ItemId, string Number, string Title, string Kind, string Status,
    double? BestScore, double? MaxScore, int Attempts, TimeSpan Time, int HintsUsed, DateTimeOffset? Completed, IReadOnlyList<TestResult> Tests)
{
    [JsonIgnore] public double? Fraction => BestScore is { } s && MaxScore is > 0 ? s / MaxScore : null;
    [JsonIgnore] public bool Scored => Kind is ExerciseKinds.Exercise or ExerciseKinds.Questions or ExerciseKinds.Check;
}

public static class ExerciseKinds
{
    public const string Reading = "Reading";
    public const string Exercise = "Exercise";
    public const string Questions = "Questions";
    public const string Check = "Module check";
}

public sealed record OutcomeDetail(string Id, string Module, string Text, string State);

/// <summary>
/// A program that passed (spec 9.2 and 10, program evidence): its fingerprint, when it passed, and the program itself, so the
/// instructor can see how the problem was solved and run it through the same grader.
/// </summary>
public sealed record ProgramEvidence(string ItemId, string Title, string ChallengeId, string WorkHash, DateTimeOffset PassedAt, string Program);

public sealed record ReportSummary(double PercentComplete, int ItemsComplete, int Items, int OutcomesMastered, int Outcomes,
    TimeSpan Time, double? OverallScore, int ScoredItems);

/// <summary>
/// The progress snapshot (plan 8.1, spec 9.5): everything a report shows, in one format-neutral record. The PDF is drawn from it,
/// and the same record goes inside the PDF, signed, so the verifier can say what the report said when it was made. Other outputs
/// (CSV for a gradebook, an LMS connection) can be drawn from it later without touching the progress or the rules.
/// </summary>
public sealed class ProgressSnapshot
{
    public const string FormatName = "learning-report/1";

    public string Format { get; init; } = FormatName;
    public required string App { get; init; }
    public required string AppVersion { get; init; }
    public required string ContentId { get; init; }
    public required string ContentTitle { get; init; }
    public required string ContentVersion { get; init; }
    public required string ReportId { get; init; }
    /// <summary>The submission point (a module check's item ID), or <see cref="SubmissionRecord.FullReport"/>.</summary>
    public required string PointId { get; init; }
    /// <summary>"Module 3 check: Motors and starters", or "Everything so far".</summary>
    public required string PointTitle { get; init; }
    /// <summary>For a file name: "Module3Check" or "FullReport".</summary>
    public required string PointShortName { get; init; }
    public required ReportStatus Status { get; init; }
    /// <summary>What is not done yet, in words, when the report is <see cref="ReportStatus.Incomplete"/>.</summary>
    public IReadOnlyList<string> Missing { get; init; } = [];
    public required DateTimeOffset Generated { get; init; }
    public required ReportLearner Learner { get; init; }
    public required ReportSummary Summary { get; init; }
    public IReadOnlyList<ExerciseDetail> Exercises { get; init; } = [];
    public IReadOnlyList<OutcomeDetail> Outcomes { get; init; } = [];
    public IReadOnlyList<ProgramEvidence> Evidence { get; init; } = [];
    /// <summary>The computer key's fingerprint (it signs the report).</summary>
    public string? ComputerKey { get; init; }
    /// <summary>A class code was entered, so the report is signed with it too (D12). The code itself is never in the report.</summary>
    public bool ClassSigned { get; init; }

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static ProgressSnapshot? FromJson(string json)
    {
        try { return JsonSerializer.Deserialize<ProgressSnapshot>(json, Json); }
        catch (JsonException) { return null; }
    }

    /// <summary>
    /// The verification code printed on the report: the first 60 bits of the SHA-256 of the snapshot, as "XXXX-XXXX-XXXX".
    /// The verifier works it out again from the signed snapshot inside the file, so it matches only that report.
    /// </summary>
    public string VerificationCode() => VerificationCodeOf(ToJson());

    /// <summary>The verification code of a snapshot's JSON exactly as it is kept in the file.</summary>
    public static string VerificationCodeOf(string json) => Base32.Group(Base32.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(json)), 12));

    /// <summary>The default file name (spec 9.3): <c>PLCPlus_&lt;LastName&gt;_&lt;SubmissionPoint&gt;_&lt;yyyy-MM-dd&gt;.pdf</c>.</summary>
    public string FileName(string prefix)
    {
        var last = Learner.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "Learner";
        return $"{prefix}_{Safe(last)}_{PointShortName}_{Generated.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.pdf";
    }

    private static string Safe(string s)
    {
        var chars = s.Where(c => char.IsLetterOrDigit(c) || c is '-').ToArray();
        return chars.Length == 0 ? "Learner" : new string(chars);
    }
}

/// <summary>What the program says about itself on a report.</summary>
public sealed record ReportOptions(string App, string AppVersion, string LineMarker, string FilePrefix);

/// <summary>Builds the snapshot from a learner's file (plan 8.1, 8.6). Subject-neutral: the program's own parts stay in the shell.</summary>
public static class ReportBuilder
{
    /// <summary>The key a passing program is kept under in <see cref="LearnerData.Work"/>, by challenge ID.</summary>
    public static string PassedWorkKey(string challengeId) => "passed:" + challengeId;

    /// <summary>
    /// A report for one submission point (a module check), or for everything so far when <paramref name="point"/> is null.
    /// A report can be made before the work is complete (spec 9.1): it is then marked Incomplete and says what is missing.
    /// </summary>
    public static ProgressSnapshot Build(ContentPackage package, LearnerData data, LearningItem? point, ReportOptions options,
        DateTimeOffset now, string? computerKey = null)
    {
        var progress = data.Progress;
        var details = data.ReportDetails ?? new ReportDetails();
        List<LearningItem> items;
        List<LearningModule> modules;
        string title, shortName;
        if (point is not null)
        {
            items = [.. point.Covers.Select(package.FindItem).OfType<LearningItem>(), point];
            modules = [point.Module];
            title = $"Module {point.Module.Number} check: {point.Module.Title}";
            shortName = $"Module{point.Module.Number}Check";
        }
        else
        {
            modules = [.. package.Modules.Where(LearningProgress.Learnable)];
            items = [.. modules.SelectMany(m => m.Items)];
            title = "Everything so far";
            shortName = "FullReport";
        }

        var exercises = items.Select(i => Detail(progress, i)).ToList();
        var outcomes = modules.SelectMany(m => Mastery.Of(package, progress, m).Select(o => new OutcomeDetail(o.Outcome.Id, $"Module {m.Number}", o.Outcome.Text, o.State switch
        {
            MasteryState.Mastered => "Mastered",
            MasteryState.NeedsReview => "Review suggested",
            MasteryState.InProgress => "In progress",
            _ => "Not started",
        }))).ToList();

        var missing = new List<string>();
        foreach (var i in items.Where(i => progress.StateOf(i) != ItemState.Complete))
        {
            if (i == point)
            {
                if (i.Quiz is not null && !(progress.Items.TryGetValue(i.Id, out var own) && own.Completed is not null))
                    missing.Add($"{i}: the questions are not passed yet" + (progress.Best(i) is { } b ? $" (best {b.Score:0} of {b.MaxScore:0})" : ""));
                continue;
            }
            missing.Add($"{i}: " + (progress.StateOf(i) == ItemState.NotStarted ? "not started"
                : progress.Best(i) is { } best && !i.CompletesByReading ? $"started, not passed yet (best {best.Score:0} of {best.MaxScore:0})"
                : "started, not complete yet"));
        }
        var complete = point is not null ? progress.StateOf(point) == ItemState.Complete : missing.Count == 0;

        var scored = exercises.Where(e => e.Scored).ToList();
        var time = point is null ? progress.TimeOnTask() : TimeSpan.FromSeconds(items.Sum(i => progress.TimeOn(i.Id).TotalSeconds));
        var summary = new ReportSummary(
            items.Count == 0 ? 0 : 100.0 * exercises.Count(e => e.Status == "Complete") / items.Count,
            exercises.Count(e => e.Status == "Complete"), items.Count,
            outcomes.Count(o => o.State == "Mastered"), outcomes.Count, time,
            scored.Count == 0 ? null : 100.0 * scored.Average(e => e.Fraction ?? 0), scored.Count);

        var evidence = new List<ProgramEvidence>();
        if (details.IncludePrograms)
            foreach (var i in items.Where(i => i.Exercise is not null))
            {
                var passed = progress.Attempts.Where(a => a.ItemId.Equals(i.Id, StringComparison.OrdinalIgnoreCase) && a.Passed).ToList();
                if (passed.Count == 0) continue;
                // The program kept when it passed; or My program, if it is still the one that passed.
                string? program = null;
                Attempt? which = null;
                foreach (var key in new[] { PassedWorkKey(i.Exercise!), i.Exercise! })
                    if (data.Work.TryGetValue(key, out var w) && passed.LastOrDefault(a => a.WorkHash == WorkHash.Of(w.Content)) is { } a)
                    {
                        (program, which) = (w.Content, a);
                        break;
                    }
                if (program is not null) evidence.Add(new ProgramEvidence(i.Id, i.ToString(), i.Exercise!, which!.WorkHash, which.At, program));
            }

        return new ProgressSnapshot
        {
            App = options.App,
            AppVersion = options.AppVersion,
            ContentId = package.Id,
            ContentTitle = package.Title,
            ContentVersion = package.Version,
            ReportId = NewReportId(),
            PointId = point?.Id ?? SubmissionRecord.FullReport,
            PointTitle = title,
            PointShortName = shortName,
            Status = complete ? ReportStatus.Complete : ReportStatus.Incomplete,
            Missing = complete ? [] : missing,
            Generated = now,
            Learner = new ReportLearner(data.Profile.DisplayName, Blank(details.StudentId) ?? Blank(data.Profile.StudentId),
                Blank(details.Course), Blank(details.Section), Blank(details.Email)),
            Summary = summary,
            Exercises = exercises,
            Outcomes = outcomes,
            Evidence = evidence,
            ComputerKey = computerKey,
            ClassSigned = ClassCode.Normalize(details.ClassCode) is not null,
        };
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static ExerciseDetail Detail(LearningProgress progress, LearningItem i)
    {
        var attempts = progress.Attempts.Where(a => a.ItemId.Equals(i.Id, StringComparison.OrdinalIgnoreCase)).ToList();
        var best = progress.Best(i);
        var state = progress.StateOf(i);
        var kind = i.Kind == ItemKind.Assess ? ExerciseKinds.Check : i.Exercise is not null ? ExerciseKinds.Exercise : i.Quiz is not null ? ExerciseKinds.Questions : ExerciseKinds.Reading;
        IReadOnlyList<TestResult> tests = best?.Tests is { } t ? t
            : best?.Questions is { } q ? [.. q.Select((x, n) => new TestResult(Question(i, x.Key, n), x.Value))]
            : [];
        progress.Items.TryGetValue(i.Id, out var record);
        return new ExerciseDetail(i.Id, i.Number, i.Title, kind,
            state switch { ItemState.Complete => "Complete", ItemState.InProgress => "Started", _ => "Not started" },
            best?.Score, best?.MaxScore, attempts.Count, progress.TimeOn(i.Id),
            attempts.Count == 0 ? 0 : attempts.Max(a => a.HintsShown), record?.Completed, tests);
    }

    private static string Question(LearningItem item, string id, int n)
    {
        var index = item.Quiz?.Questions.ToList().FindIndex(q => q.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? -1;
        return $"Question {(index >= 0 ? index + 1 : n + 1)}";
    }

    /// <summary>"R-7K3M-9QX2": 40 random bits, in the letters a person can read out without confusion.</summary>
    public static string NewReportId() => "R-" + Base32.Group(Base32.Encode(RandomNumberGenerator.GetBytes(5), 8));
}

/// <summary>Crockford's base32 (no I, L, O or U), in groups of four, as the recovery code uses.</summary>
public static class Base32
{
    public const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string Encode(byte[] data, int chars)
    {
        var sb = new StringBuilder(chars);
        int buffer = 0, bits = 0, i = 0;
        while (sb.Length < chars)
        {
            if (bits < 5)
            {
                buffer = (buffer << 8) | (i < data.Length ? data[i] : 0);
                i++;
                bits += 8;
            }
            sb.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
            bits -= 5;
        }
        return sb.ToString();
    }

    public static string Group(string s) => string.Join('-', s.Chunk(4).Select(c => new string(c)));

    /// <summary>What a person typed, as the code it means: spaces and dashes dropped, O read as 0, I and L as 1. Null if not a code.</summary>
    public static string? Normalize(string? typed)
    {
        if (typed is null) return null;
        var sb = new StringBuilder();
        foreach (var c in typed.ToUpperInvariant())
        {
            if (c is ' ' or '-' or '\t') continue;
            var d = c switch { 'O' => '0', 'I' or 'L' => '1', _ => c };
            if (!Alphabet.Contains(d)) return null;
            sb.Append(d);
        }
        return sb.Length == 0 ? null : sb.ToString();
    }
}
