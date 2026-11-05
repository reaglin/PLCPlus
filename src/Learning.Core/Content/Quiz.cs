using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Learning.Core.Content;

// Questions (spec 7.3): multiple choice, numeric answers with a tolerance, truth-table fill-in,
// match the symbol, and "find the fault". The core grades them all; only "find the fault" needs the
// subject module, to show the faulted machine (to the core it is a choice question with a machine).

[JsonConverter(typeof(JsonStringEnumConverter<QuestionKind>))]
public enum QuestionKind
{
    /// <summary>One right answer among the options.</summary>
    Choice,
    /// <summary>Every right answer among the options, and no wrong one.</summary>
    Multiple,
    /// <summary>A number, right within <see cref="Question.Tolerance"/>.</summary>
    Numeric,
    /// <summary>The result column of the truth table of <see cref="Question.Expression"/>.</summary>
    TruthTable,
    /// <summary>Each left-hand item matched to its right-hand item (a symbol to its name).</summary>
    Match,
    /// <summary>A machine with a fault in it (the subject module shows it); the learner chooses which fault.</summary>
    Fault,
    /// <summary>
    /// A typed answer, such as 1F or 0010 0101. Right when it is one of <see cref="Question.Answers"/>, ignoring case, spaces and
    /// underscores; or, with <see cref="Question.Base"/>, when it is the number <see cref="Question.Answer"/> written in that base.
    /// </summary>
    Text,
}

public sealed record MatchPair(string Left, string Right);

public sealed class Question
{
    public string Id { get; set; } = "";
    public QuestionKind Kind { get; set; }
    /// <summary>The question, in Markdown.</summary>
    public string Prompt { get; set; } = "";
    public List<string> Options { get; set; } = [];
    /// <summary>The right options, by position (from 0).</summary>
    public List<int> Correct { get; set; } = [];
    public double? Answer { get; set; }
    public double Tolerance { get; set; }
    public string? Unit { get; set; }
    /// <summary>For a text question: the answers accepted (see <see cref="QuestionKind.Text"/>).</summary>
    public List<string> Answers { get; set; } = [];
    /// <summary>
    /// For a text question: 2, 8 or 16 when the answer is <see cref="Answer"/> written in that base. Leading zeros, spaces and the
    /// usual marks (2#, 0b, 8#, 16#, 0x, a trailing h) are allowed.
    /// </summary>
    public int? Base { get; set; }
    public string? Expression { get; set; }
    public List<MatchPair> Pairs { get; set; } = [];
    /// <summary>Why the right answer is right: shown after the learner answers.</summary>
    public string? Explanation { get; set; }
    /// <summary>The module outcome this question gives evidence for (mastery, spec 7.4).</summary>
    public string? Objective { get; set; }
    public double Points { get; set; } = 1;

    /// <summary>For a fault question: the challenge whose machine is shown, which of its programs runs, and the fault ("Stop:open").</summary>
    public string? Machine { get; set; }
    public string? Program { get; set; }
    public string? Fault { get; set; }

    /// <summary>The right-hand items of a match question, in the order they are offered (alphabetical, so the order gives nothing away).</summary>
    [JsonIgnore]
    public IReadOnlyList<string> MatchChoices => [.. Pairs.Select(p => p.Right).Distinct().Order(StringComparer.CurrentCulture)];
}

public sealed class Quiz
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>The share of the points needed to pass, 0 to 1.</summary>
    public double PassThreshold { get; set; } = 0.8;
    /// <summary>
    /// Practice questions show the right answer when one is wrong. A check (the default) does not: it can be tried again
    /// as often as the learner likes, so it says "not quite" and gives the reason only once the answer is right.
    /// </summary>
    public bool Practice { get; set; }
    public List<Question> Questions { get; set; } = [];

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <exception cref="ContentException">The file is not a quiz PLC+ can read.</exception>
    public static Quiz Parse(string json, string file)
    {
        try
        {
            return JsonSerializer.Deserialize<Quiz>(json, Json) ?? throw new ContentException($"{file} is empty.");
        }
        catch (JsonException ex)
        {
            throw new ContentException($"{file} is not a quiz PLC+ can read: {ex.Message}");
        }
    }
}

/// <summary>What the learner answered to one question. Only the part for its kind is filled in.</summary>
public sealed class QuestionAnswer
{
    /// <summary>Choice, Multiple, Fault: the options chosen, by position.</summary>
    public List<int> Chosen { get; set; } = [];
    /// <summary>Numeric, Text: what was typed.</summary>
    public string? Text { get; set; }
    /// <summary>TruthTable: the result given for each row (null: not answered).</summary>
    public List<bool?> Rows { get; set; } = [];
    /// <summary>Match: for each pair, in order, the position in <see cref="Question.MatchChoices"/> chosen (-1: not answered).</summary>
    public List<int> Matches { get; set; } = [];
}

public sealed record QuestionResult(string Id, bool Correct, double Points, double MaxPoints, string Feedback, bool Answered);

public sealed record QuizResult(string QuizId, IReadOnlyList<QuestionResult> Questions, double Score, double MaxScore, bool Passed)
{
    /// <summary>The same result in the shape every graded attempt has, so progress records it like an exercise.</summary>
    public ChallengeOutcome ToOutcome() => new(QuizId, true, [],
        [.. Questions.Select(q => new ScenarioOutcome(q.Id, false, q.Correct, q.Points, q.MaxPoints, q.Correct ? null : q.Feedback, null))],
        Score, MaxScore, Passed, "");
}

public static class QuizGrader
{
    /// <param name="whereToLook">
    /// For a check (not practice): what to read again when an answer is wrong, such as "3.3 The seal-in circuit". The
    /// subject module knows which item teaches a question's objective; null says "the item this question is about".
    /// </param>
    public static QuizResult Grade(Quiz quiz, IReadOnlyDictionary<string, QuestionAnswer> answers, Func<Question, string?>? whereToLook = null)
    {
        var results = quiz.Questions.Select(q => One(q, answers.TryGetValue(q.Id, out var a) ? a : null, quiz.Practice, whereToLook)).ToList();
        var score = results.Sum(r => r.Points);
        var max = results.Sum(r => r.MaxPoints);
        return new QuizResult(quiz.Id, results, score, max, max > 0 && score / max >= quiz.PassThreshold - 1e-9);
    }

    private static QuestionResult One(Question q, QuestionAnswer? a, bool practice, Func<Question, string?>? whereToLook)
    {
        var (answered, correct, right, problem) = q.Kind switch
        {
            QuestionKind.Choice or QuestionKind.Fault => (a?.Chosen.Count > 0, a?.Chosen.Count == 1 && q.Correct.Contains(a.Chosen[0]),
                string.Join(" or ", q.Correct.Where(i => i < q.Options.Count).Select(i => $"\"{q.Options[i]}\"")), null),
            QuestionKind.Multiple => (a?.Chosen.Count > 0, a is not null && a.Chosen.Order().SequenceEqual(q.Correct.Order()),
                string.Join(", ", q.Correct.Where(i => i < q.Options.Count).Select(i => $"\"{q.Options[i]}\"")), null),
            QuestionKind.Numeric => Numeric(q, a),
            QuestionKind.TruthTable => TruthTable(q, a),
            QuestionKind.Match => Match(q, a),
            QuestionKind.Text => Text(q, a),
            _ => (false, false, "", (string?)null),
        };
        var look = q.Kind == QuestionKind.Fault
            ? "Not quite. Work the machine again and watch which contact on the rungs never changes, then change your answer and check again."
            : $"Not quite. Look again at {whereToLook?.Invoke(q) ?? "the item this question is about"}, then change your answer and check again.";
        var feedback = problem is not null ? problem
            : !answered ? "Not answered yet."
            : correct ? "Right." + (q.Explanation is null ? "" : " " + q.Explanation)
            : practice ? $"Not quite. The answer is {right}." + (q.Explanation is null ? "" : " " + q.Explanation)
            : look;
        return new QuestionResult(q.Id, correct, correct ? q.Points : 0, q.Points, feedback, answered && problem is null);
    }

    private static (bool, bool, string, string?) Numeric(Question q, QuestionAnswer? a)
    {
        var right = $"{q.Answer?.ToString(CultureInfo.InvariantCulture)}{(q.Unit is null ? "" : " " + q.Unit)}";
        if (a?.Text is not { } text || text.Trim().Length == 0) return (false, false, right, null);
        var number = new string([.. text.Trim().TakeWhile(c => char.IsDigit(c) || c is '.' or '-' or '+' or 'e' or 'E')]);
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return (true, false, right, $"\"{text.Trim()}\" is not a number PLC+ can read. Type digits, with a point for decimals, such as 100 or 0.5.");
        var ok = q.Answer is { } answer && Math.Abs(value - answer) <= Math.Max(q.Tolerance, 1e-9);
        return (true, ok, right, null);
    }

    /// <summary>Upper case, without spaces or underscores: how typed answers are compared.</summary>
    public static string Squeeze(string text) => new([.. text.Where(c => !char.IsWhiteSpace(c) && c != '_').Select(char.ToUpperInvariant)]);

    /// <summary>The number <paramref name="text"/> is in <paramref name="numberBase"/>, or null if it is not one.</summary>
    public static long? InBase(string text, int numberBase)
    {
        var s = Squeeze(text);
        foreach (var mark in numberBase switch { 2 => new[] { "2#", "0B" }, 8 => ["8#", "0O"], 16 => ["16#", "0X"], _ => [] })
            if (s.StartsWith(mark, StringComparison.Ordinal)) { s = s[mark.Length..]; break; }
        if (numberBase == 16 && s.EndsWith('H')) s = s[..^1];
        if (s.Length == 0 || s.Length > 63) return null;
        long value = 0;
        foreach (var c in s)
        {
            var digit = c is >= '0' and <= '9' ? c - '0' : c is >= 'A' and <= 'F' ? c - 'A' + 10 : 99;
            if (digit >= numberBase) return null;
            value = value * numberBase + digit;
        }
        return value;
    }

    private static string BaseName(int b) => b switch { 2 => "binary", 8 => "octal", 16 => "hexadecimal", _ => $"base {b}" };

    private static (bool, bool, string, string?) Text(Question q, QuestionAnswer? a)
    {
        if (q.Base is { } b && q.Answer is { } n)
        {
            var right = Convert.ToString((long)n, b).ToUpperInvariant();
            if (a?.Text is not { } typed || typed.Trim().Length == 0) return (false, false, right, null);
            if (InBase(typed, b) is not { } value)
                return (true, false, right, $"\"{typed.Trim()}\" is not a {BaseName(b)} number. Use only {(b == 16 ? "0-9 and A-F" : b == 2 ? "0 and 1" : $"the digits 0-{b - 1}")}."
                                            + (b == 2 ? " (0x… and 16#… are how hexadecimal is written.)" : ""));
            return (true, value == (long)n, right, null);
        }
        var answers = q.Answers.Select(Squeeze).ToHashSet(StringComparer.Ordinal);
        var shown = q.Answers.Count == 0 ? "" : $"\"{q.Answers[0]}\"";
        if (a?.Text is not { } text || text.Trim().Length == 0) return (false, false, shown, null);
        return (true, answers.Contains(Squeeze(text)), shown, null);
    }

    private static (bool, bool, string, string?) TruthTable(Question q, QuestionAnswer? a)
    {
        var expected = BooleanExpression.Parse(q.Expression ?? "").Rows().Select(r => r.Result).ToList();
        var right = string.Join(", ", expected.Select(r => r ? "TRUE" : "FALSE")) + " (top to bottom)";
        if (a is null || a.Rows.All(r => r is null)) return (false, false, right, null);
        var blank = a.Rows.Select((r, i) => (r, i)).Where(x => x.r is null).Select(x => x.i + 1).ToList();
        if (blank.Count > 0)
            return (true, false, right, $"Row{(blank.Count == 1 ? "" : "s")} {string.Join(" and ", blank)} {(blank.Count == 1 ? "is" : "are")} still ?: choose TRUE or FALSE for every row.");
        return (true, a.Rows.Count == expected.Count && a.Rows.Zip(expected).All(x => x.First == x.Second), right, null);
    }

    private static (bool, bool, string, string?) Match(Question q, QuestionAnswer? a)
    {
        var choices = q.MatchChoices;
        var right = string.Join("; ", q.Pairs.Select(p => $"{p.Left} → {p.Right}"));
        if (a is null || a.Matches.All(m => m < 0)) return (false, false, right, null);
        if (a.Matches.Any(m => m < 0)) return (true, false, right, "Some are still \"Choose…\": choose one for every symbol.");
        var ok = a.Matches.Count == q.Pairs.Count
                 && q.Pairs.Select((p, i) => a.Matches[i] < choices.Count && choices[a.Matches[i]] == p.Right).All(x => x);
        return (true, ok, right, null);
    }
}
