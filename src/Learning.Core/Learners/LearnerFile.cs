using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Learning.Core.Progress;

namespace Learning.Core.Learners;

/// <summary>Who the learner is. Only the name is required (Ron, D19).</summary>
public sealed record LearnerProfile(string ProfileId, string DisplayName, string? StudentId, DateTimeOffset Created);

/// <summary>A piece of the learner's own work kept between sessions, such as the program they are writing for an exercise.</summary>
public sealed class SavedWork
{
    public string Content { get; set; } = "";
    public DateTimeOffset Saved { get; set; }
}

/// <summary>A content package and version the learner has used (spec 8.4, ContentState).</summary>
public sealed record ContentUsed(string PackageId, string Version, DateTimeOffset FirstUsed);

/// <summary>Everything inside the encrypted part of a learner's file.</summary>
public sealed class LearnerData
{
    public LearnerProfile Profile { get; set; } = null!;
    public LearningProgress Progress { get; set; } = new();
    /// <summary>The learner's own work, by a key the subject module chooses (PLC+: the challenge ID).</summary>
    public Dictionary<string, SavedWork> Work { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ContentUsed> Content { get; init; } = [];
    public DateTimeOffset? LastBackup { get; set; }
    /// <summary>The folder the learner last backed up to, so the next backup starts there.</summary>
    public string? BackupFolder { get; set; }

    /// <summary>The learner has done something since the last backup (plan 6.10).</summary>
    [JsonIgnore]
    public bool NeedsBackup => Progress.LastChanged is { } changed && (LastBackup is null || changed > LastBackup);

    public void UsedContent(string packageId, string version, DateTimeOffset now)
    {
        if (!Content.Any(c => c.PackageId == packageId && c.Version == version)) Content.Add(new ContentUsed(packageId, version, now));
    }

    /// <summary>Two copies of one learner's file, combined: nothing is lost (spec 8.3). The first copy's profile is kept.</summary>
    public static LearnerData Merge(LearnerData mine, LearnerData theirs)
    {
        var merged = new LearnerData
        {
            Profile = mine.Profile,
            Progress = LearningProgress.Merge(mine.Progress, theirs.Progress),
            LastBackup = mine.LastBackup is null ? theirs.LastBackup : theirs.LastBackup is null ? mine.LastBackup
                : mine.LastBackup > theirs.LastBackup ? mine.LastBackup : theirs.LastBackup,
            BackupFolder = mine.BackupFolder ?? theirs.BackupFolder,
        };
        foreach (var (key, work) in mine.Work.Concat(theirs.Work))
            if (!merged.Work.TryGetValue(key, out var have) || work.Saved > have.Saved) merged.Work[key] = work;
        merged.Content.AddRange(mine.Content.Concat(theirs.Content)
            .GroupBy(c => (c.PackageId, c.Version)).Select(g => g.OrderBy(c => c.FirstUsed).First()).OrderBy(c => c.FirstUsed));
        return merged;
    }
}

/// <summary>How the key is kept: wrapped once with the PIN and once with the recovery code (spec, PIN and progress-file protection).</summary>
public sealed class KeyBox
{
    public string Kdf { get; set; } = "PBKDF2-SHA256";
    public int Iterations { get; set; }
    public string PinSalt { get; set; } = "";
    public string PinKey { get; set; } = "";
    public string RecoverySalt { get; set; } = "";
    public string RecoveryKey { get; set; } = "";
}

/// <summary>
/// A learner's file as it is on disk (plan 6.3, D13): a header anyone can read, so the start screen can list learners
/// without unlocking anything, then one encrypted block. The header is a copy for listing; the encrypted block is the record.
/// </summary>
public sealed class LearnerHeader
{
    public const string FormatName = "learning-progress";

    public string Format { get; set; } = FormatName;
    public int FormatVersion { get; set; } = 1;
    /// <summary>The program that wrote it, such as "PLCPlus".</summary>
    public string App { get; set; } = "";
    public string ProfileId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTimeOffset LastSaved { get; set; }
    /// <summary>The computer that last saved it (spec 8.3), shown when the file is imported.</summary>
    public string SavedOn { get; set; } = "";
    public string AppVersion { get; set; } = "";
    public DateTimeOffset? LastBackup { get; set; }
    public DateTimeOffset? LastChanged { get; set; }
    /// <summary>Changes with every save, so a session can tell that another one saved the file in the meantime.</summary>
    public string Revision { get; set; } = "";
    public int FailedPins { get; set; }
    public DateTimeOffset? LastFailedPin { get; set; }
    public KeyBox Keys { get; set; } = new();
    public string Body { get; set; } = "";

    [JsonIgnore]
    public bool NeedsBackup => LastChanged is { } changed && (LastBackup is null || changed > LastBackup);
}

/// <summary>What a program tells the learner files about itself.</summary>
public sealed record LearnerFileOptions(string App, string Extension, string AppVersion, string Computer, int Iterations = LearnerFile.DefaultIterations);

public enum UnlockStatus { Unlocked, WrongPin, MustWait }

/// <summary>The answer to a PIN or recovery code: the file, or why not, in words for the learner.</summary>
public sealed record UnlockResult(UnlockStatus Status, LearnerFile? File, DateTimeOffset? WaitUntil, string Message);

/// <summary>A file that is not a learner's file, or is damaged, in words for the learner.</summary>
public sealed class LearnerFileException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// One learner's progress file, unlocked (plan 6.3–6.7). AES-GCM with a random key; that key is kept wrapped with a key made
/// from the PIN by PBKDF2, and again with one made from the recovery code. Every copy of a learner's file, on any computer,
/// has the same key, which is what lets two copies be merged.
///
/// The PIN is a convenience lock for shared computers, to stop classmates opening each other's files. A 4–6 digit PIN cannot
/// stand up to a determined attack on a copied file, and the program's help says so (spec).
/// </summary>
public sealed class LearnerFile : IDisposable
{
    /// <summary>PBKDF2-SHA256 rounds: about a third of a second on a classroom PC, which also slows guessing in the program.</summary>
    public const int DefaultIterations = 600_000;
    public const int TriesBeforeWait = 5;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private byte[] _key;
    private string _revision;

    public string Path { get; private set; }
    public LearnerHeader Header { get; private set; }
    public LearnerData Data { get; private set; }
    public LearnerFileOptions Options { get; }

    private LearnerFile(string path, LearnerHeader header, LearnerData data, byte[] key, LearnerFileOptions options)
    {
        Path = path;
        Header = header;
        Data = data;
        _key = key;
        _revision = header.Revision;
        Options = options;
    }

    // ---- Reading the header ----------------------------------------------------------------------------------

    /// <summary>The readable part of a file. Throws <see cref="LearnerFileException"/> when it is not a learner's file.</summary>
    public static LearnerHeader ReadHeader(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LearnerFileException($"The file could not be read: {ex.Message}", ex);
        }
        try
        {
            var header = JsonSerializer.Deserialize<LearnerHeader>(text, Json);
            if (header is null || header.Format != LearnerHeader.FormatName || header.ProfileId.Length == 0 || header.Body.Length == 0)
                throw new LearnerFileException("This is not a learner's progress file.");
            if (header.FormatVersion > 1)
                throw new LearnerFileException("This progress file was saved by a newer version of the program. Update the program to open it.");
            return header;
        }
        catch (JsonException ex)
        {
            throw new LearnerFileException("This is not a learner's progress file, or it is damaged.", ex);
        }
    }

    // ---- Creating, unlocking, recovering ---------------------------------------------------------------------

    /// <summary>A new learner's file, and the recovery code to show them once.</summary>
    public static (LearnerFile File, string RecoveryCode) Create(string path, LearnerProfile profile, string pin, LearnerFileOptions options,
        DateTimeOffset now, LearnerData? start = null)
    {
        if (PinRules.Problem(pin, pin) is { } problem) throw new ArgumentException(problem, nameof(pin));
        var key = RandomNumberGenerator.GetBytes(32);
        var recovery = RecoveryCode.New();
        var header = new LearnerHeader { App = options.App, ProfileId = profile.ProfileId, DisplayName = profile.DisplayName };
        header.Keys = NewKeys(profile.ProfileId, key, pin, RecoveryCode.Normalize(recovery)!, options.Iterations);
        var data = start ?? new LearnerData();
        data.Profile = profile;
        data.Progress.Log(EventKinds.Created, profile.ProfileId, $"Learner created on {options.Computer}.", now);
        var file = new LearnerFile(path, header, data, key, options);
        file.Write(now, merge: false);
        return (file, recovery);
    }

    /// <summary>Opens a file with its PIN. Five wrong PINs in a row make the learner wait, longer after each (plan 6.4).</summary>
    public static UnlockResult Unlock(string path, string pin, LearnerFileOptions options, DateTimeOffset now)
    {
        var header = ReadHeader(path);
        if (PinLockout.WaitUntil(header) is { } until && until > now)
            return new UnlockResult(UnlockStatus.MustWait, null, until, PinLockout.WaitMessage(until, now));

        var key = TryUnwrap(header.Keys.PinKey, header.Keys.PinSalt, "pin:" + header.ProfileId, pin.Trim(), header.Keys.Iterations);
        if (key is null)
        {
            header.FailedPins++;
            header.LastFailedPin = now;
            WriteHeaderOnly(path, header);
            var wait = PinLockout.WaitUntil(header);
            var left = TriesBeforeWait - header.FailedPins;
            return new UnlockResult(wait is not null ? UnlockStatus.MustWait : UnlockStatus.WrongPin, null, wait,
                wait is not null ? "That PIN is not right. " + PinLockout.WaitMessage(wait.Value, now)
                : left == 1 ? "That PIN is not right. One more try, then there is a wait before the next. Forgot it? Press Forgot my PIN and type your recovery code."
                : $"That PIN is not right. Try again ({left} tries before a wait). Forgot it? Press Forgot my PIN and type your recovery code.");
        }
        var file = Open(path, header, key, options);
        if (header.FailedPins > 0)
        {
            file.Header.FailedPins = 0;
            file.Header.LastFailedPin = null;
            WriteHeaderOnly(path, file.Header);
        }
        return new UnlockResult(UnlockStatus.Unlocked, file, null, "");
    }

    /// <summary>A forgotten PIN: the recovery code sets a new one (spec 8.1). The recovery code stays the same.</summary>
    public static UnlockResult Recover(string path, string recoveryCode, string newPin, LearnerFileOptions options, DateTimeOffset now)
    {
        if (PinRules.Problem(newPin, newPin) is { } problem) throw new ArgumentException(problem, nameof(newPin));
        var header = ReadHeader(path);
        var code = RecoveryCode.Normalize(recoveryCode);
        var key = code is null ? null : TryUnwrap(header.Keys.RecoveryKey, header.Keys.RecoverySalt, "recovery:" + header.ProfileId, code, header.Keys.Iterations);
        if (key is null)
            return new UnlockResult(UnlockStatus.WrongPin, null, null, code is null
                ? "A recovery code is 20 letters and numbers, in five groups of four, such as 7K2M-QX9P-4TRE-WH4C-8NDA. Check what you typed."
                : "That recovery code does not open this learner's file. Check each group against the code you wrote down or printed.");
        var file = Open(path, header, key, options);
        file.Header.FailedPins = 0;
        file.Header.LastFailedPin = null;
        file.SetPin(newPin, now, EventKinds.PinReset, "PIN reset with the recovery code.");
        return new UnlockResult(UnlockStatus.Unlocked, file, null, "");
    }

    /// <summary>
    /// Opens a file with a key already in hand: another copy of the same learner's file, whose key is the same.
    /// Null if the key does not open it.
    /// </summary>
    public LearnerData? ReadOtherCopy(LearnerHeader other) => other.ProfileId == Header.ProfileId ? TryDecrypt(other, _key) : null;

    /// <summary>Another copy of the same learner's file, opened with this one's key (their keys are the same). Null if it does not open.</summary>
    public static LearnerFile? OpenWithKeyOf(string path, LearnerFile open, LearnerFileOptions options)
    {
        var header = ReadHeader(path);
        if (header.ProfileId != open.Header.ProfileId || TryDecrypt(header, open._key) is not { } data) return null;
        return new LearnerFile(path, header, data, (byte[])open._key.Clone(), options);
    }

    /// <summary>The learner's PIN, changed while the file is open.</summary>
    public void ChangePin(string newPin, DateTimeOffset now)
    {
        if (PinRules.Problem(newPin, newPin) is { } problem) throw new ArgumentException(problem, nameof(newPin));
        SetPin(newPin, now, EventKinds.PinChanged, "PIN changed.");
    }

    /// <summary>Checks a PIN against the open file without changing anything (to confirm before removing or changing it).</summary>
    public bool IsPin(string pin) =>
        TryUnwrap(Header.Keys.PinKey, Header.Keys.PinSalt, "pin:" + Header.ProfileId, pin.Trim(), Header.Keys.Iterations) is { } k
        && CryptographicOperations.FixedTimeEquals(k, _key);

    private void SetPin(string pin, DateTimeOffset now, string kind, string detail)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        Header.Keys.PinSalt = Convert.ToBase64String(salt);
        // The same count as the recovery key's: both are read with Keys.Iterations.
        Header.Keys.PinKey = Wrap(_key, Derive(pin.Trim(), salt, Header.Keys.Iterations), "pin:" + Header.ProfileId);
        Data.Progress.Log(kind, Header.ProfileId, detail, now);
        Save(now);
    }

    // ---- Saving ------------------------------------------------------------------------------------------------

    /// <summary>
    /// Saves the file. If another session saved it since (the same learner open in two copies of the program, or under two
    /// Windows accounts), the two are merged first, so neither loses work.
    /// </summary>
    public void Save(DateTimeOffset now) => Write(now, merge: true);

    /// <summary>The learner backed up: records it (shown on the start screen and in the reminder), then saves.</summary>
    public void MarkBackedUp(string folder, DateTimeOffset now)
    {
        Data.LastBackup = now;
        Data.BackupFolder = folder;
        Data.Progress.Log(EventKinds.BackedUp, Header.ProfileId, "Backed up the progress file.", now);
        Save(now);
    }

    /// <summary>Another copy of this learner's file, folded in (import, plan 6.7). False if it is not a copy of this learner.</summary>
    public bool MergeFrom(LearnerHeader other, string from, DateTimeOffset now)
    {
        if (ReadOtherCopy(other) is not { } theirs) return false;
        Data = LearnerData.Merge(Data, theirs);
        Data.Progress.Log(EventKinds.Merged, Header.ProfileId,
            $"Combined with a copy saved on {other.SavedOn} at {other.LastSaved.ToLocalTime():yyyy-MM-dd HH:mm} ({from}).", now);
        Save(now);
        return true;
    }

    /// <summary>Writes a copy of the file somewhere else, as it is now (export and backup, plan 6.6).</summary>
    public void CopyTo(string destination)
    {
        var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(destination));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var temp = destination + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Header, Json));
        File.Move(temp, destination, overwrite: true);
    }

    private void Write(DateTimeOffset now, bool merge)
    {
        if (!IsOpen) throw new InvalidOperationException("The learner's file is locked; unlock it again to save.");
        if (merge && File.Exists(Path))
        {
            try
            {
                var disk = ReadHeader(Path);
                if (disk.Revision != _revision && disk.ProfileId == Header.ProfileId)
                {
                    if (TryDecrypt(disk, _key) is { } theirs) Data = LearnerData.Merge(Data, theirs);
                    // The newer PIN (changed in the other session) and its wrong-PIN count win.
                    Header.Keys = disk.Keys;
                    Header.FailedPins = disk.FailedPins;
                    Header.LastFailedPin = disk.LastFailedPin;
                }
            }
            catch (LearnerFileException)
            {
                // The file on disk is damaged or not ours any more: this session's copy replaces it.
            }
        }

        Header.App = Options.App;
        Header.DisplayName = Data.Profile.DisplayName;
        Header.LastSaved = now;
        Header.SavedOn = Options.Computer;
        Header.AppVersion = Options.AppVersion;
        Header.LastBackup = Data.LastBackup;
        Header.LastChanged = Data.Progress.LastChanged;
        Header.Revision = Guid.NewGuid().ToString("N");
        Header.Body = Encrypt(Data, _key, Header.ProfileId);
        WriteHeaderOnly(Path, Header);
        _revision = Header.Revision;
    }

    private static void WriteHeaderOnly(string path, LearnerHeader header)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(header, Json));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>False once the file is locked again (the key forgotten): it can no longer be saved.</summary>
    public bool IsOpen => _key.Length > 0;

    /// <summary>Forgets the key (Switch learner, auto-lock, closing): the file must be unlocked again.</summary>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_key);
        _key = [];
    }

    // ---- Cryptography ------------------------------------------------------------------------------------------

    private static LearnerFile Open(string path, LearnerHeader header, byte[] key, LearnerFileOptions options)
    {
        var data = TryDecrypt(header, key)
                   ?? throw new LearnerFileException("The PIN is right, but the rest of the file is damaged. Use a backup copy of your progress file (Add learner from file).");
        return new LearnerFile(path, header, data, key, options);
    }

    private static KeyBox NewKeys(string profileId, byte[] key, string pin, string recovery, int iterations)
    {
        var pinSalt = RandomNumberGenerator.GetBytes(16);
        var recoverySalt = RandomNumberGenerator.GetBytes(16);
        return new KeyBox
        {
            Iterations = iterations,
            PinSalt = Convert.ToBase64String(pinSalt),
            PinKey = Wrap(key, Derive(pin.Trim(), pinSalt, iterations), "pin:" + profileId),
            RecoverySalt = Convert.ToBase64String(recoverySalt),
            RecoveryKey = Wrap(key, Derive(recovery, recoverySalt, iterations), "recovery:" + profileId),
        };
    }

    private static byte[] Derive(string secret, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(secret), salt, iterations, HashAlgorithmName.SHA256, 32);

    private static byte[]? TryUnwrap(string wrapped, string salt, string label, string secret, int iterations)
    {
        try
        {
            return Open(Convert.FromBase64String(wrapped), Derive(secret, Convert.FromBase64String(salt), iterations), label);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }

    private static string Wrap(byte[] plain, byte[] key, string label) => Convert.ToBase64String(Seal(plain, key, label));

    private static string Encrypt(LearnerData data, byte[] key, string profileId) =>
        Convert.ToBase64String(Seal(JsonSerializer.SerializeToUtf8Bytes(data, Json), key, "body:" + profileId));

    private static LearnerData? TryDecrypt(LearnerHeader header, byte[] key)
    {
        try
        {
            var plain = Open(Convert.FromBase64String(header.Body), key, "body:" + header.ProfileId);
            return JsonSerializer.Deserialize<LearnerData>(plain, Json);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException)
        {
            return null;
        }
    }

    /// <summary>AES-256-GCM: nonce (12) | tag (16) | ciphertext. The label ties each block to its use and to this learner.</summary>
    private static byte[] Seal(byte[] plain, byte[] key, string label)
    {
        var output = new byte[12 + 16 + plain.Length];
        var nonce = output.AsSpan(0, 12);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plain, output.AsSpan(28), output.AsSpan(12, 16), Encoding.UTF8.GetBytes(label));
        return output;
    }

    private static byte[] Open(byte[] sealedBytes, byte[] key, string label)
    {
        if (sealedBytes.Length < 28) throw new CryptographicException("too short");
        var plain = new byte[sealedBytes.Length - 28];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(sealedBytes.AsSpan(0, 12), sealedBytes.AsSpan(28), sealedBytes.AsSpan(12, 16), plain, Encoding.UTF8.GetBytes(label));
        return plain;
    }
}
