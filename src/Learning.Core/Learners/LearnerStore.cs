namespace Learning.Core.Learners;

/// <summary>A learner on the start screen: their file's readable header, or why the file could not be read.</summary>
public sealed record LearnerListing(string Path, LearnerHeader? Header, string? Problem)
{
    public string Name => Header?.DisplayName ?? System.IO.Path.GetFileName(Path);
}

/// <summary>What importing a file would do (plan 6.6): add a new learner, or combine with the copy already here.</summary>
public sealed record ImportPlan(string Source, LearnerHeader Incoming, LearnerListing? Existing, bool SameFile);

/// <summary>
/// The learners on this computer: one file each in one folder (plan 6.1, 6.8). The program chooses the folder (PLC+:
/// %ProgramData%\PLCPlus\Learners, shared by every Windows account) and the file extension.
/// </summary>
public sealed class LearnerStore(string folder, LearnerFileOptions options)
{
    public string Folder { get; } = folder;
    public LearnerFileOptions Options { get; } = options;

    /// <summary>Every learner's file in the folder, most recently used first. Damaged files are listed with their problem.</summary>
    public IReadOnlyList<LearnerListing> List()
    {
        if (!Directory.Exists(Folder)) return [];
        var list = new List<LearnerListing>();
        foreach (var path in Directory.GetFiles(Folder, "*" + Options.Extension))
        {
            try
            {
                list.Add(new LearnerListing(path, LearnerFile.ReadHeader(path), null));
            }
            catch (LearnerFileException ex)
            {
                list.Add(new LearnerListing(path, null, ex.Message));
            }
        }
        return [.. list.OrderByDescending(l => l.Header?.LastSaved ?? DateTimeOffset.MinValue)];
    }

    public string PathFor(string profileId) => Path.Combine(Folder, profileId + Options.Extension);

    /// <summary>What is wrong with a new learner's name, or null. Names are unique on a computer so nobody opens the wrong one.</summary>
    public string? NameProblem(string name)
    {
        name = name.Trim();
        if (name.Length == 0) return "Type your name: it is how you find your progress on this computer, and it goes on your reports.";
        if (name.Length > 60) return "That name is long: use 60 letters or fewer.";
        if (List().Any(l => string.Equals(l.Header?.DisplayName.Trim(), name, StringComparison.OrdinalIgnoreCase)))
            return $"There is already a learner called {name} on this computer. Add your last name or an initial, so each of you finds your own.";
        return null;
    }

    public (LearnerFile File, string RecoveryCode) Create(string displayName, string? studentId, string pin, DateTimeOffset now, LearnerData? start = null)
    {
        if (NameProblem(displayName) is { } problem) throw new ArgumentException(problem, nameof(displayName));
        var id = Guid.NewGuid().ToString("D");
        var profile = new LearnerProfile(id, displayName.Trim(), string.IsNullOrWhiteSpace(studentId) ? null : studentId.Trim(), now);
        return LearnerFile.Create(PathFor(id), profile, pin, Options, now, start);
    }

    public UnlockResult Unlock(LearnerListing learner, string pin, DateTimeOffset now) => LearnerFile.Unlock(learner.Path, pin, Options, now);

    /// <summary>Reads a file chosen to import and says what importing it would do. Throws <see cref="LearnerFileException"/> if it is not a learner's file.</summary>
    public ImportPlan PlanImport(string source)
    {
        var incoming = LearnerFile.ReadHeader(source);
        var target = PathFor(incoming.ProfileId);
        var existing = File.Exists(target) ? List().FirstOrDefault(l => string.Equals(l.Path, target, StringComparison.OrdinalIgnoreCase)) : null;
        var same = string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase);
        return new ImportPlan(source, incoming, existing, same);
    }

    /// <summary>A learner new to this computer: their file is copied in as it is. The same PIN opens it.</summary>
    public LearnerListing ImportNew(ImportPlan plan)
    {
        if (plan.Existing is not null) throw new InvalidOperationException("This learner is already here: merge instead.");
        var target = PathFor(plan.Incoming.ProfileId);
        Directory.CreateDirectory(Folder);
        File.Copy(plan.Source, target, overwrite: false);
        return new LearnerListing(target, LearnerFile.ReadHeader(target), null);
    }

    /// <summary>
    /// A learner already here: the two copies are combined (plan 6.7), and the copy on this computer keeps its own PIN. The PIN
    /// may be the one for either copy. Wrong PINs count against the copy on this computer.
    /// </summary>
    public UnlockResult ImportMerge(ImportPlan plan, string pin, DateTimeOffset now)
    {
        if (plan.Existing is null) throw new InvalidOperationException("This learner is not here yet: import it as new.");
        var local = LearnerFile.Unlock(plan.Existing.Path, pin, Options, now);
        if (local.File is null)
        {
            // Perhaps the PIN was changed on one of the computers: the incoming copy's PIN opens the same key.
            var copy = CopyOf(plan.Source);
            UnlockResult incoming;
            try
            {
                incoming = LearnerFile.Unlock(copy, pin, Options, now);
            }
            finally
            {
                File.Delete(copy);
            }
            if (incoming.File is null) return local;
            using (incoming.File)
            {
                var opened = LearnerFile.OpenWithKeyOf(plan.Existing.Path, incoming.File, Options);
                if (opened is null) return new UnlockResult(UnlockStatus.WrongPin, null, null, "The two files have the same learner ID but do not belong together, so they cannot be combined.");
                opened.MergeFrom(plan.Incoming, Path.GetFileName(plan.Source), now);
                return new UnlockResult(UnlockStatus.Unlocked, opened, null, "");
            }
        }
        if (!local.File.MergeFrom(plan.Incoming, Path.GetFileName(plan.Source), now))
        {
            local.File.Dispose();
            return new UnlockResult(UnlockStatus.WrongPin, null, null, "The two files have the same learner ID but do not belong together, so they cannot be combined.");
        }
        return local;
    }

    /// <summary>A temporary copy, so trying a PIN on a file being imported never writes to the learner's USB drive or cloud folder.</summary>
    private static string CopyOf(string source)
    {
        var temp = Path.Combine(Path.GetTempPath(), "learner-import-" + Guid.NewGuid().ToString("N") + Path.GetExtension(source));
        File.Copy(source, temp);
        return temp;
    }

    /// <summary>Removes a learner from this computer (plan 6.5). The program checks the PIN and offers a backup first; <paramref name="delete"/> is how (PLC+: to the Recycle Bin).</summary>
    public static void Remove(LearnerListing learner, Action<string> delete) => delete(learner.Path);
}
