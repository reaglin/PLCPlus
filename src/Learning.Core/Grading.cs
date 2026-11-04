using System.Security.Cryptography;
using System.Text;

namespace Learning.Core;

// The only link between the Learning Core and a subject module (spec 3, 12): the core asks the
// module to grade a learner's work and gets results back. Nothing here knows what a PLC is.

/// <summary>Implemented by each subject module (PLC+: Plc.Grading) so the core can grade its challenges.</summary>
public interface IChallengeRunner
{
    /// <summary>The subject this runner grades, such as "plc".</summary>
    string Domain { get; }

    /// <summary>
    /// Runs the learner's work against every scenario of the challenge, from a clean reset each time.
    /// Both arguments are the module's own JSON; the core stores them but never reads inside them.
    /// </summary>
    ChallengeOutcome Run(string challengeJson, string workJson);
}

/// <summary>The result of one grading run (spec 7.2).</summary>
public sealed record ChallengeOutcome(
    string ChallengeId,
    bool Ran,
    IReadOnlyList<string> Problems,
    IReadOnlyList<ScenarioOutcome> Scenarios,
    double Score,
    double MaxScore,
    bool Passed,
    string WorkHash)
{
    /// <summary>Score as a fraction of the maximum, 0 to 1.</summary>
    public double Fraction => MaxScore <= 0 ? 0 : Score / MaxScore;
}

/// <summary>One scenario's result: hidden scenarios report pass or fail but not their details.</summary>
public sealed record ScenarioOutcome(
    string Name,
    bool Hidden,
    bool Passed,
    double Points,
    double MaxPoints,
    string? Failure,
    double? FailedAtSeconds);

public static class WorkHash
{
    /// <summary>SHA-256 of the submitted work, recorded with every attempt (spec 7.2 step 4).</summary>
    public static string Of(string work) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(work)));
}
