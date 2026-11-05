using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Learning.Core.Reports;

// How a report is signed (plan 8.4, D12, spec 10 "Report integrity"):
//
// - Every computer has its own key pair, made the first time a report is made (ComputerKey). The program signs the SHA-256 of
//   the saved PDF with it. The verifier finds any change made to the file after it was saved: one changed byte fails.
// - When the learner has entered their instructor's class code, the same hash is also signed with the class code. Only someone
//   who knows the code can make that signature, so a report that passes it came from a learner in that class.
// - Honest limit (spec 10): the computer's key is on the learner's computer, so a determined learner can sign a report they made
//   up. The computer's signature proves the file was not edited after it was made; the class code proves the class. For a
//   high-stakes assessment, use the lab or a proctored challenge.

/// <summary>A computer's own key pair (ECDSA P-256), kept by the program on that computer.</summary>
public sealed class ComputerKey : IDisposable
{
    private readonly ECDsa _key;

    private ComputerKey(ECDsa key) => _key = key;

    public static ComputerKey New() => new(ECDsa.Create(ECCurve.NamedCurves.nistP256));

    public static ComputerKey FromPkcs8(byte[] pkcs8)
    {
        var key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(pkcs8, out _);
        return new ComputerKey(key);
    }

    public byte[] ExportPkcs8() => _key.ExportPkcs8PrivateKey();

    public byte[] PublicKey => _key.ExportSubjectPublicKeyInfo();

    /// <summary>"7K3M-9QX2": a short name for the key, printed on every report it signs, so an instructor sees which computer.</summary>
    public string Fingerprint => FingerprintOf(PublicKey);

    public static string FingerprintOf(byte[] publicKey) => Base32.Group(Base32.Encode(SHA256.HashData(publicKey), 8));

    public byte[] SignHash(byte[] hash) => _key.SignHash(hash);

    public static bool Verify(byte[] publicKey, byte[] hash, byte[] signature)
    {
        try
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(publicKey, out _);
            return key.VerifyHash(hash, signature);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public void Dispose() => _key.Dispose();
}

/// <summary>
/// An instructor's class code (D12): 16 characters, "XXXX-XXXX-XXXX-XXXX". The instructor makes one in the verifier and gives it
/// to the class; a learner types it in their report details. Reports signed with it check out only with that code.
/// </summary>
public static class ClassCode
{
    public const int Length = 16;

    public static string New() => Base32.Group(Base32.Encode(RandomNumberGenerator.GetBytes(10), Length));

    /// <summary>The code as typed, or null if it is not a class code (empty, the wrong length, or letters a code never has).</summary>
    public static string? Normalize(string? typed) => Base32.Normalize(typed) is { Length: Length } code ? code : null;

    public static string? Format(string? typed) => Normalize(typed) is { } code ? Base32.Group(code) : null;

    internal static byte[] Key(string normalized) => SHA256.HashData(Encoding.UTF8.GetBytes("learning-class:" + normalized));
}

public enum ReportCheck
{
    /// <summary>Not a report made by the program, or one that has lost its signature (printed and scanned, saved again as PDF).</summary>
    NotAReport,
    /// <summary>The file was changed after it was made.</summary>
    Altered,
    /// <summary>Signed, and not one byte changed since.</summary>
    Genuine,
}

public enum ClassCheck
{
    /// <summary>No class code was given to the check.</summary>
    NotAsked,
    /// <summary>The learner had not entered a class code when the report was made.</summary>
    NotSigned,
    Matches,
    DoesNotMatch,
}

/// <summary>What the verifier says about one PDF (plan 8.8).</summary>
public sealed record ReportVerification(ReportCheck Outcome, ClassCheck Class, ProgressSnapshot? Snapshot, string? KeyFingerprint,
    string? VerificationCode, string Explanation)
{
    internal byte[]? Signature { get; init; }
}

/// <summary>The result of sealing a PDF: the file to save, its signature (for the result line), and its SHA-256 (for the history).</summary>
public sealed record SealedReport(byte[] Pdf, byte[] Signature, string FileHash);

/// <summary>
/// Signs a report PDF in place, the way PDF digital signatures do: the renderer leaves a fixed-length placeholder in the document's
/// information dictionary; the seal hashes the whole file with the placeholder in it, signs the hash, and writes the signature
/// over the placeholder, keeping its length, so nothing else in the file moves. The verifier puts the placeholder back and hashes again.
/// The signed snapshot and the public key are in the same dictionary, inside what is signed.
/// </summary>
public static class ReportSeal
{
    public const string ReportEntry = "/LearningReport";
    public const string KeyEntry = "/LearningKey";
    public const string SealEntry = "/LearningSeal";
    private const string Marker = "LEARNING-SEAL1:";
    private const int BodyLength = 140;
    private const char Fill = '*';

    /// <summary>What the renderer writes as the <see cref="SealEntry"/> value.</summary>
    public static string Placeholder => Marker + new string(Fill, BodyLength);

    /// <summary>Text for an information entry, in letters a PDF writes as they are (base64url).</summary>
    public static string Encode(string text) => Base64Url(Encoding.UTF8.GetBytes(text));

    public static SealedReport Seal(byte[] pdf, ComputerKey key, string? classCode)
    {
        var at = FindMarker(pdf) ?? throw new InvalidOperationException("The report has no place for its signature: it must be drawn with ReportSeal.Placeholder.");
        var hash = SHA256.HashData(pdf);
        var signature = key.SignHash(hash);
        var body = Base64Url(signature) + "." + (ClassCode.Normalize(classCode) is { } code ? Base64Url(HMACSHA256.HashData(ClassCode.Key(code), hash)) : "0");
        if (body.Length > BodyLength) throw new InvalidOperationException("The signature is longer than its place in the report.");
        var sealedPdf = (byte[])pdf.Clone();
        Encoding.ASCII.GetBytes(body.PadRight(BodyLength, Fill)).CopyTo(sealedPdf, at + Marker.Length);
        return new SealedReport(sealedPdf, signature, Convert.ToHexStringLower(SHA256.HashData(sealedPdf)));
    }

    /// <summary>Checks a PDF (plan 8.8): made by the program, not changed since, and (with a class code) from that class.</summary>
    public static ReportVerification Verify(byte[] pdf, string? classCode = null)
    {
        var at = FindMarker(pdf);
        var reportText = ReadEntry(pdf, ReportEntry);
        var keyText = ReadEntry(pdf, KeyEntry);
        if (at is null || reportText is null || keyText is null)
            return new ReportVerification(ReportCheck.NotAReport, ClassCheck.NotAsked, null, null, null,
                "This file is not a signed report. It may be another kind of PDF, or a report that was printed and scanned, or saved again " +
                "from another program (\"Print to PDF\" or an editor), which drops the signature. Ask the learner for the file exactly as PLC+ saved it.");

        var body = Encoding.ASCII.GetString(pdf, at.Value + Marker.Length, BodyLength).TrimEnd(Fill).Split('.');
        string? json = null;
        byte[]? publicKey = null, signature = null, mac = null;
        try
        {
            json = Encoding.UTF8.GetString(FromBase64Url(reportText));
            publicKey = FromBase64Url(keyText);
            signature = FromBase64Url(body[0]);
            mac = body.Length > 1 && body[1] != "0" ? FromBase64Url(body[1]) : null;
        }
        catch (FormatException) { }
        var snapshot = json is null ? null : ProgressSnapshot.FromJson(json);
        if (snapshot is null || publicKey is null || signature is null)
            return new ReportVerification(ReportCheck.Altered, ClassCheck.NotAsked, null, null, null,
                "This report's signature is damaged: the file was changed after it was made.");

        var original = (byte[])pdf.Clone();
        Encoding.ASCII.GetBytes(new string(Fill, BodyLength)).CopyTo(original, at.Value + Marker.Length);
        var hash = SHA256.HashData(original);
        var fingerprint = ComputerKey.FingerprintOf(publicKey);
        var code = ProgressSnapshot.VerificationCodeOf(json!);
        if (!ComputerKey.Verify(publicKey, hash, signature))
            return new ReportVerification(ReportCheck.Altered, ClassCheck.NotAsked, snapshot, fingerprint, code,
                "The file was changed after it was made: what it shows may not be what the learner did. " +
                "Below is what the report said when it was made. Ask the learner for the file exactly as PLC+ saved it.");

        var normalized = ClassCode.Normalize(classCode);
        var classCheck = normalized is null ? ClassCheck.NotAsked
            : mac is null ? ClassCheck.NotSigned
            : CryptographicOperations.FixedTimeEquals(mac, HMACSHA256.HashData(ClassCode.Key(normalized), hash)) ? ClassCheck.Matches : ClassCheck.DoesNotMatch;
        var made = snapshot.Generated.ToLocalTime().ToString("d MMMM yyyy 'at' HH:mm", CultureInfo.CurrentCulture);
        var explanation = $"Genuine: made by {snapshot.App} {snapshot.AppVersion} on {made}, on the computer with key {fingerprint}, and not changed since. " +
            classCheck switch
            {
                ClassCheck.Matches => "It is signed with your class code, so it came from a learner who has the code.",
                ClassCheck.NotSigned => "It is not signed with a class code: the learner had not entered one in their report details.",
                ClassCheck.DoesNotMatch => "It is signed with a different class code from the one you typed: check the code, or ask the learner which code they entered.",
                _ => mac is null ? "It is not signed with a class code." : "It is signed with a class code; type yours to check it.",
            };
        return new ReportVerification(ReportCheck.Genuine, classCheck, snapshot, fingerprint, code, explanation) { Signature = signature };
    }

    private static int? FindMarker(byte[] pdf)
    {
        var marker = Encoding.ASCII.GetBytes(Marker);
        var first = pdf.AsSpan().IndexOf(marker);
        if (first < 0) return null;
        // A second marker means a second information dictionary (a later edit appended to the file): not the file as made.
        return pdf.AsSpan(first + marker.Length).IndexOf(marker) >= 0 ? null : first;
    }

    /// <summary>The text of an information entry, written as a literal string (…) or a hex string &lt;…&gt;.</summary>
    private static string? ReadEntry(byte[] pdf, string name)
    {
        var key = Encoding.ASCII.GetBytes(name);
        var span = pdf.AsSpan();
        var at = span.IndexOf(key);
        if (at < 0) return null;
        var i = at + key.Length;
        while (i < pdf.Length && pdf[i] is (byte)' ' or (byte)'\r' or (byte)'\n') i++;
        if (i >= pdf.Length) return null;
        if (pdf[i] == '(')
        {
            var end = span[(i + 1)..].IndexOf((byte)')');
            return end < 0 ? null : Encoding.ASCII.GetString(pdf, i + 1, end);
        }
        if (pdf[i] == '<')
        {
            var end = span[(i + 1)..].IndexOf((byte)'>');
            if (end < 0) return null;
            try
            {
                var bytes = Convert.FromHexString(Encoding.ASCII.GetString(pdf, i + 1, end));
                return bytes is [0xFE, 0xFF, ..] ? Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2) : Encoding.ASCII.GetString(bytes);
            }
            catch (FormatException) { return null; }
        }
        return null;
    }

    internal static string Base64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static byte[] FromBase64Url(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}

public enum LineCheck
{
    /// <summary>Not a result line at all.</summary>
    NotALine,
    /// <summary>A result line, but there is nothing to check it with: no class code, and no report.</summary>
    CannotCheck,
    Matches,
    DoesNotMatch,
}

/// <summary>
/// The signed result line (plan 8.4, D6), in the shape of Gamify+'s, to paste into an LMS, a discussion or an e-mail:
/// <c>PLC1|Maria Gomez|Module 3 check: Motors and starters|Complete, score 92%|2026-11-05|R-7K3M-9QX2|4F2A-91C7-BD30</c>.
/// The code is signed with the class code when the learner entered one (it checks with the code alone), and otherwise with the
/// report's own signature (it checks together with the PDF it came with).
/// </summary>
public sealed record ResultLine(string Marker, string Name, string Point, string Result, DateOnly Date, string ReportId, string Code)
{
    public string Text() =>
        string.Join('|', Marker, Name, Point, Result, Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ReportId, Code);

    public override string ToString() => Text();

    public static ResultLine Issue(ProgressSnapshot report, string marker, byte[] signature, string? classCode)
    {
        var result = (report.Status == ReportStatus.Complete ? "Complete" : "Incomplete") +
                     (report.Summary.OverallScore is { } s ? $", score {Math.Round(s, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}%" : "");
        var line = new ResultLine(marker, Clean(report.Learner.Name), Clean(report.PointTitle), result,
            DateOnly.FromDateTime(report.Generated.ToLocalTime().DateTime), report.ReportId, "");
        return line with { Code = line.CodeWith(KeyFor(classCode, signature)) };
    }

    private static byte[] KeyFor(string? classCode, byte[] signature) =>
        ClassCode.Normalize(classCode) is { } code ? ClassCode.Key(code) : SHA256.HashData(signature);

    private string CodeWith(byte[] key)
    {
        var canonical = string.Join('|', Marker.ToUpperInvariant(), NormalizeName(Name), Point.Trim().ToLowerInvariant(), Result.Trim().ToLowerInvariant(),
            Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ReportId.Trim().ToUpperInvariant());
        return Base32.Group(Base32.Encode(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(canonical)), 12));
    }

    private static string Clean(string s) =>
        string.Join(' ', s.Replace('|', '/').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Trimmed, spaces collapsed, lower case: "Maria  Gomez" checks out the same as "maria gomez" (as Gamify+).</summary>
    public static string NormalizeName(string name) =>
        string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    /// <summary>Reads a pasted line, with whatever came around it. Null if there is no result line in the text.</summary>
    public static ResultLine? Parse(string? text, string marker)
    {
        var t = text ?? "";
        var start = t.IndexOf(marker + "|", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        var parts = t[start..].Split('\n', '\r')[0].Split('|');
        if (parts.Length < 7) return null;
        if (!DateOnly.TryParseExact(parts[4].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return null;
        return new ResultLine(parts[0].Trim(), parts[1].Trim(), parts[2].Trim(), parts[3].Trim(), date, parts[5].Trim(), parts[6].Trim());
    }

    /// <summary>
    /// Checks a pasted line (plan 8.8), with the class code, the report it came with, or both. Says in words what it found.
    /// </summary>
    public static (LineCheck Outcome, ResultLine? Line, string Explanation) Check(string? text, string marker, string? classCode, byte[]? pdf)
    {
        if (Parse(text, marker) is not { } line)
            return (LineCheck.NotALine, null, $"This is not a result line. A result line starts with {marker}| and has seven parts separated by |. Ask the learner to paste the whole line (the Copy button copies it).");
        var given = Base32.Normalize(line.Code);
        if (ClassCode.Normalize(classCode) is { } code && Base32.Normalize(line.CodeWith(ClassCode.Key(code))) == given)
            return (LineCheck.Matches, line, $"Genuine: signed with your class code. {line.Name}, {line.Point}: {line.Result}, on {line.Date:d MMMM yyyy}.");
        if (pdf is not null)
        {
            var report = ReportSeal.Verify(pdf, classCode);
            if (report.Outcome == ReportCheck.Genuine && report.Signature is { } sig && report.Snapshot is { } s)
            {
                if (!s.ReportId.Equals(line.ReportId, StringComparison.OrdinalIgnoreCase))
                    return (LineCheck.DoesNotMatch, line, $"This line is for report {line.ReportId}, but the PDF is report {s.ReportId}. Ask the learner for the line that came with this report.");
                if (Base32.Normalize(line.CodeWith(SHA256.HashData(sig))) == given)
                    return (LineCheck.Matches, line, $"Genuine: the line matches report {s.ReportId}, and the report is not changed. {line.Name}, {line.Point}: {line.Result}.");
                if (s.ClassSigned && ClassCode.Normalize(classCode) is null)
                    return (LineCheck.CannotCheck, line, $"Report {s.ReportId} is genuine. Its line is signed with a class code: type your class code to check the line too.");
            }
            else if (report.Outcome != ReportCheck.Genuine)
                return (LineCheck.CannotCheck, line, "The PDF did not pass its own check, so the line cannot be checked against it. " + report.Explanation);
        }
        if (classCode is null && pdf is null)
            return (LineCheck.CannotCheck, line, "To check this line, type your class code (if your class uses one), or choose the PDF report it came with.");
        return (LineCheck.DoesNotMatch, line,
            "The code does not match the rest of the line: something in it was changed, or it was made with a different class code or for a different report.");
    }
}
