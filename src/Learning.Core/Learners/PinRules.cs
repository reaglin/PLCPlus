using System.Security.Cryptography;
using System.Text;

namespace Learning.Core.Learners;

/// <summary>A PIN is 4 to 6 digits (spec 8.1).</summary>
public static class PinRules
{
    /// <summary>What is wrong with a new PIN and its second typing, in words for the learner; null when it is fine.</summary>
    public static string? Problem(string pin, string again)
    {
        pin = pin.Trim();
        if (pin.Length == 0) return "Type a PIN: 4 to 6 digits.";
        if (!pin.All(char.IsAsciiDigit)) return "A PIN is digits only (0–9), no letters or spaces.";
        if (pin.Length is < 4 or > 6) return $"A PIN is 4 to 6 digits; this one has {pin.Length}.";
        if (pin != again.Trim()) return "The two PINs are not the same. Type the same PIN in both boxes.";
        return null;
    }
}

/// <summary>
/// The one-time recovery code (spec 8.1): 20 characters from Crockford's alphabet (no I, L, O or U, so nothing is misread),
/// shown as five groups of four. About 100 bits, so unlike the PIN it cannot be guessed.
/// </summary>
public static class RecoveryCode
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string New()
    {
        var chars = RandomNumberGenerator.GetItems<char>(Alphabet, 20);
        return string.Join("-", Enumerable.Range(0, 5).Select(i => new string(chars, i * 4, 4)));
    }

    /// <summary>The code as typed, made exact: dashes and spaces dropped, lower case raised, O read as 0 and I or L as 1. Null if it cannot be a code.</summary>
    public static string? Normalize(string typed)
    {
        var sb = new StringBuilder();
        foreach (var c in typed.ToUpperInvariant())
        {
            if (c is '-' or ' ' or '\t') continue;
            var d = c switch { 'O' => '0', 'I' or 'L' => '1', _ => c };
            if (!Alphabet.Contains(d)) return null;
            sb.Append(d);
        }
        return sb.Length == 20 ? sb.ToString() : null;
    }
}

/// <summary>Wrong PINs (plan 6.4): five tries, then a wait of 30 seconds that doubles with each further wrong PIN, up to 30 minutes.</summary>
public static class PinLockout
{
    public static TimeSpan WaitAfter(int failed) =>
        failed < LearnerFile.TriesBeforeWait ? TimeSpan.Zero
        : TimeSpan.FromSeconds(Math.Min(30 * Math.Pow(2, failed - LearnerFile.TriesBeforeWait), 30 * 60));

    /// <summary>When the learner may try again, or null if they may try now.</summary>
    public static DateTimeOffset? WaitUntil(LearnerHeader header) =>
        header.LastFailedPin is { } last && WaitAfter(header.FailedPins) is { } wait && wait > TimeSpan.Zero ? last + wait : null;

    public static string WaitMessage(DateTimeOffset until, DateTimeOffset now)
    {
        var left = until - now;
        var words = left.TotalSeconds < 60 ? $"{Math.Ceiling(left.TotalSeconds):0} seconds" : $"{Math.Ceiling(left.TotalMinutes):0} minute{(Math.Ceiling(left.TotalMinutes) == 1 ? "" : "s")}";
        return $"After {LearnerFile.TriesBeforeWait} wrong PINs in a row there is a wait before the next try: try again in {words}. Forgot your PIN? Press Forgot my PIN and type your recovery code.";
    }
}
