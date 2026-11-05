using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Learning.Core.Learners;
using Learning.Core.Progress;

namespace Learning.Shell;

/// <summary>PLC+'s settings for this computer: where learners' files are kept, and when PLC+ locks itself (plan 6.4, 6.8).</summary>
public sealed class ShellSettings
{
    /// <summary>Null: the usual folder, %ProgramData%\PLCPlus\Learners.</summary>
    public string? LearnersFolder { get; set; }
    public int LockMinutes { get; set; } = 15;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static ShellSettings Load()
    {
        try
        {
            return File.Exists(LearnerPlaces.SettingsFile)
                ? JsonSerializer.Deserialize<ShellSettings>(File.ReadAllText(LearnerPlaces.SettingsFile), Json) ?? new ShellSettings()
                : new ShellSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new ShellSettings();
        }
    }

    public void Save()
    {
        LearnerPlaces.EnsureShared(LearnerPlaces.DataFolder);
        File.WriteAllText(LearnerPlaces.SettingsFile, JsonSerializer.Serialize(this, Json));
    }

    public string Folder => string.IsNullOrWhiteSpace(LearnersFolder) ? LearnerPlaces.DefaultLearnersFolder : LearnersFolder;
}

/// <summary>
/// Where PLC+ keeps learners' files (spec 8.2): by default %ProgramData%\PLCPlus\Learners, one folder every Windows account on the
/// computer can use, so lab PCs work whether students share one login or have their own (Ron, question 10).
/// PLCPLUS_DATA points a test run at a folder of its own.
/// </summary>
public static class LearnerPlaces
{
    public const string Extension = ".plcprogress";

    public static string DataFolder { get; } = Environment.GetEnvironmentVariable("PLCPLUS_DATA") is { Length: > 0 } test ? test
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PLCPlus");

    public static string DefaultLearnersFolder => Path.Combine(DataFolder, "Learners");
    public static string SettingsFile => Path.Combine(DataFolder, "settings.json");

    public static string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0";

    public static LearnerFileOptions Options { get; } = new("PLCPlus", Extension, AppVersion, Environment.MachineName);

    /// <summary>
    /// Makes the folder, and lets every account on the computer change the files in it: by default a file under ProgramData
    /// can be changed only by the account that made it, which would lock a lab's second Windows account out.
    /// </summary>
    public static void EnsureShared(string folder)
    {
        if (Directory.Exists(folder)) return;
        var info = Directory.CreateDirectory(folder);
        var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!Path.GetFullPath(folder).StartsWith(Path.GetFullPath(common), StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            var security = info.GetAccessControl();
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                FileSystemRights.Modify, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            info.SetAccessControl(security);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            // The folder works for this account; another account may not be able to change its files. Settings can point elsewhere.
        }
    }

    /// <summary>Removes a file to the Recycle Bin, so a learner removed by mistake can be brought back (plan 6.5).</summary>
    public static void Recycle(string path)
    {
        var op = new ShFileOp
        {
            Func = 3, // FO_DELETE
            From = Path.GetFullPath(path) + "\0\0",
            Flags = 0x0040 | 0x0010 | 0x0004 | 0x0400, // ALLOWUNDO | NOCONFIRMATION | SILENT | NOERRORUI
        };
        var result = SHFileOperationW(ref op);
        if (result != 0 || File.Exists(path)) File.Delete(path); // no Recycle Bin on this drive: delete
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOp
    {
        public IntPtr Hwnd;
        public uint Func;
        [MarshalAs(UnmanagedType.LPWStr)] public string From;
        [MarshalAs(UnmanagedType.LPWStr)] public string? To;
        public ushort Flags;
        [MarshalAs(UnmanagedType.Bool)] public bool AnyOperationsAborted;
        public IntPtr NameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? ProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperationW(ref ShFileOp op);
}

/// <summary>
/// The preview's progress (phases 4–5 kept one learner's progress in %LocalAppData%\PLCPlus, with My program for each
/// exercise beside it). The first learner made on this computer can start with it; it is then renamed, not deleted.
/// </summary>
public static class PreviewProgress
{
    private static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PLCPlus");
    private static string ProgressFile => Path.Combine(Root, "progress", "preview.json");
    private static string WorkFolder => Path.Combine(Root, "work");

    /// <summary>How many items the preview's progress has complete, or null if there is none to bring in.</summary>
    public static int? CompleteItems()
    {
        if (Environment.GetEnvironmentVariable("PLCPLUS_DATA") is { Length: > 0 }) return null; // a test run never takes a tester's preview
        if (!File.Exists(ProgressFile)) return null;
        try
        {
            return LearningProgress.FromJson(File.ReadAllText(ProgressFile)).Items.Values.Count(r => r.Completed is not null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>The preview's progress and programs as a new learner's start. The old files are renamed (.moved), not deleted.</summary>
    public static LearnerData Take(DateTimeOffset now)
    {
        var data = new LearnerData { Progress = LearningProgress.FromJson(File.ReadAllText(ProgressFile)) };
        if (Directory.Exists(WorkFolder))
            foreach (var file in Directory.GetFiles(WorkFolder, "*.plcprogram"))
                data.Work[Path.GetFileNameWithoutExtension(file)] = new SavedWork { Content = File.ReadAllText(file), Saved = File.GetLastWriteTimeUtc(file) };
        data.Progress.LastChanged ??= now;
        return data;
    }

    public static void MarkTaken()
    {
        try
        {
            File.Move(ProgressFile, ProgressFile + ".moved", overwrite: true);
            if (Directory.Exists(WorkFolder)) Directory.Move(WorkFolder, WorkFolder + ".moved-" + DateTime.Now.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Taken already; leaving the old files is harmless (the check box appears again for the next new learner).
        }
    }
}
