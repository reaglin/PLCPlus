using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Learning.Core.Learners;
using Microsoft.Win32;

namespace Learning.Shell;

/// <summary>
/// The start screen (plan 6.1–6.6, 6.8): every learner on this computer, by name and when they last used PLC+. Choose your
/// name and type your PIN; or make a new learner, add one from a file, set a new PIN with the recovery code, or remove one.
/// It covers the whole window whenever nobody is signed in, so nothing of the last learner's work shows.
/// </summary>
public partial class LearnerScreen : UserControl
{
    private readonly DispatcherTimer _waitTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTimeOffset? _waitUntil;

    public LearnerScreen()
    {
        InitializeComponent();
        Settings = ShellSettings.Load();
        Store = new LearnerStore(Settings.Folder, LearnerPlaces.Options);
        _waitTimer.Tick += (_, _) => ShowWait();
        IsVisibleChanged += (_, _) => { if (IsVisible) Dispatcher.BeginInvoke(DispatcherPriority.Input, FocusFirst); };
    }

    /// <summary>A learner's file is open: the window takes it from here.</summary>
    public event Action<LearnerFile>? SignedIn;

    public LearnerStore Store { get; private set; }
    public ShellSettings Settings { get; private set; }

    private Window? Owner => Window.GetWindow(this);

    private LearnerRow? Selected => LearnersList.SelectedItem as LearnerRow;

    /// <summary>Shows the screen: after Switch learner or a lock (with why, and that learner chosen), or at start.</summary>
    public void Show(string? banner = null, string? selectProfileId = null)
    {
        LockedBanner.Visibility = banner is null ? Visibility.Collapsed : Visibility.Visible;
        LockedText.Text = banner ?? "";
        Say(null);
        PinBox.Clear();
        Refresh(selectProfileId);
        Visibility = Visibility.Visible;
    }

    private void Refresh(string? selectProfileId = null)
    {
        IReadOnlyList<LearnerListing> learners;
        try
        {
            learners = Store.List();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            learners = [];
            Say($"PLC+ could not read the learners' folder ({Store.Folder}). Settings can point PLC+ at another folder. Details for support: {ex.Message}");
        }
        var rows = learners.Select(l => new LearnerRow(l)).ToList();
        LearnersList.ItemsSource = rows;
        var empty = rows.Count == 0;
        EmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        LearnersList.Visibility = PinRow.Visibility = ForgotButton.Visibility = RemoveButton.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        var usual = string.Equals(Store.Folder, LearnerPlaces.DefaultLearnersFolder, StringComparison.OrdinalIgnoreCase);
        LeadText.Text = empty ? "Start by making yourself a learner." : "Choose your name, type your PIN and press Open. Not in the list? Press New learner.";
        EmptyText.Text = "No learners on this computer yet. Press New learner to start. If you have a progress file from another computer or a backup, press Add learner from file." +
                         (usual ? "" : $" (PLC+ is looking in {Store.Folder}, chosen in Settings. If learners are missing, Settings ▸ Use the usual folder.)");
        LearnersList.SelectedItem = rows.FirstOrDefault(r => r.Listing.Header?.ProfileId == selectProfileId)
                                    ?? (rows.Count == 1 ? rows[0] : null);
        FolderText.Text = $"Learners' files are kept in {Store.Folder}" +
                          (string.Equals(Store.Folder, LearnerPlaces.DefaultLearnersFolder, StringComparison.OrdinalIgnoreCase)
                              ? ", which everyone who uses this computer shares." : " (chosen in Settings).");
        UpdateButtons();
    }

    private void FocusFirst()
    {
        if (Selected is not null) PinBox.Focus();
        else if (LearnersList.Items.Count > 0) LearnersList.Focus();
    }

    private void UpdateButtons()
    {
        var row = Selected;
        var ok = row?.Listing.Header is not null;
        PinLabel.Content = ok ? $"_PIN for {row!.Name}:" : "_PIN:";
        var waiting = _waitUntil is { } until && until > DateTimeOffset.Now;
        OpenButton.IsEnabled = ok && PinBox.Password.Length > 0 && !waiting;
        OpenButton.ToolTip = !ok ? "Choose your name in the list first"
            : waiting ? "Wait until PLC+ lets you try again (after 5 wrong PINs)"
            : PinBox.Password.Length == 0 ? "Type your PIN first" : $"Opens {row!.Name}'s progress";
        ForgotButton.IsEnabled = ok;
        RemoveButton.IsEnabled = row is not null;
        ForgotButton.ToolTip = ok ? $"Set a new PIN for {row!.Name} with the recovery code given when the learner was made" : "Choose your name in the list first";
        RemoveButton.ToolTip = row is null ? "Choose a learner in the list first"
            : !ok ? "Removes this damaged file from the list (to the Recycle Bin)"
            : $"Takes {row.Name} off this computer (needs the PIN; offers a backup first). The file goes to the Recycle Bin. " +
              $"An instructor without the PIN can delete {Path.GetFileName(row.Listing.Path)} from the learners' folder instead, with PLC+ closed";
    }

    private void Say(string? text)
    {
        MessageText.Text = text ?? "";
        MessageText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void LearnersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PinBox.Clear();
        _waitUntil = null;
        _waitTimer.Stop();
        Say(Selected?.Listing.Problem is { } problem ? problem + " If you have a backup, use Add learner from file." : null);
        if (Selected?.Listing.Header is { } h && PinLockout.WaitUntil(h) is { } until && until > DateTimeOffset.Now) StartWait(until);
        UpdateButtons();
    }

    private void LearnersList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => PinBox.Focus();

    private void PinBox_PasswordChanged(object sender, RoutedEventArgs e) => UpdateButtons();

    // ---- Open ---------------------------------------------------------------------------------------------------

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        if (Selected?.Listing is not { Header: not null } learner || PinBox.Password.Length == 0) return;
        var pin = PinBox.Password;
        Say(null);
        IsEnabled = false;
        Cursor = Cursors.Wait;
        UnlockResult result;
        try
        {
            result = await Task.Run(() => Store.Unlock(learner, pin, DateTimeOffset.Now));
        }
        catch (Exception ex) when (ex is LearnerFileException or IOException or UnauthorizedAccessException)
        {
            result = new UnlockResult(UnlockStatus.WrongPin, null, null, ex is LearnerFileException ? ex.Message
                : $"PLC+ could not open the file: {ex.Message} It may be open in another program; close that and try again.");
        }
        finally
        {
            IsEnabled = true;
            Cursor = null;
        }
        PinBox.Clear();
        if (result.File is { } file)
        {
            SignedIn?.Invoke(file);
            return;
        }
        if (result.Status == UnlockStatus.MustWait && result.WaitUntil is { } until) StartWait(until, result.Message);
        else Say(result.Message);
        PinBox.Focus();
    }

    private string _waitLead = "";

    private void StartWait(DateTimeOffset until, string? first = null)
    {
        _waitUntil = until;
        _waitLead = first is not null && first.StartsWith("That PIN is not right.", StringComparison.Ordinal) ? "That PIN is not right. " : "";
        _waitTimer.Start();
        ShowWait();
    }

    private void ShowWait()
    {
        if (_waitUntil is not { } until) return;
        var now = DateTimeOffset.Now;
        if (until <= now)
        {
            _waitUntil = null;
            _waitTimer.Stop();
            Say(null);
            LockedBanner.Visibility = Visibility.Visible;
            LockedText.Text = "You can try your PIN again now.";
        }
        else Say(_waitLead + PinLockout.WaitMessage(until, now));
        UpdateButtons();
    }

    // ---- New, import, forgot, remove ----------------------------------------------------------------------------------

    private void New_Click(object sender, RoutedEventArgs e)
    {
        var w = new NewLearnerWindow(Owner, Store);
        w.ShowDialog();
        if (w.Created is { } file) SignedIn?.Invoke(file);
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add learner from file: choose a progress file (an instructor can choose several)",
            Multiselect = true,
            Filter = "PLC+ progress files (*.plcprogress)|*.plcprogress|All files (*.*)|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog(Owner) != true) return;
        if (dialog.FileNames.Length > 1)
        {
            ImportMany(dialog.FileNames);
            return;
        }

        ImportPlan plan;
        try
        {
            plan = Store.PlanImport(dialog.FileName);
        }
        catch (LearnerFileException ex)
        {
            Say($"{Path.GetFileName(dialog.FileName)}: {ex.Message} Choose a file that ends in .plcprogress, made by Back up my progress.");
            return;
        }
        var them = plan.Incoming;
        if (plan.SameFile)
        {
            Say($"That is {them.DisplayName}'s file in this computer's learners' folder already. Choose {them.DisplayName} in the list and type the PIN.");
            Refresh(them.ProfileId);
            return;
        }
        var saved = $"last saved on {them.SavedOn}, {Words.When(them.LastSaved).Replace("on ", "", StringComparison.Ordinal)}";
        if (plan.Existing is null)
        {
            if (!ChoiceWindow.Ask(Owner, "PLC+ — Add learner from file", $"Add {them.DisplayName}?",
                    $"This is {them.DisplayName}'s progress, {saved}. If that is the copy you want, add it: the same PIN opens it here.",
                    "Add this learner", $"Copies the file into this computer's learners' folder; {them.DisplayName} then opens it with the PIN.",
                    "Cancel", "Closes this without adding anyone."))
                return;
            try
            {
                LearnerPlaces.EnsureShared(Store.Folder);
                Store.ImportNew(plan);
                Refresh(them.ProfileId);
                Say(null);
                LockedBanner.Visibility = Visibility.Visible;
                LockedText.Text = $"{them.DisplayName} is now on this computer. Type the PIN and press Open.";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Say($"PLC+ could not copy the file into {Store.Folder}: {ex.Message}");
            }
            return;
        }

        var here = plan.Existing.Header;
        if (!ChoiceWindow.Ask(Owner, "PLC+ — Add learner from file", $"Combine {them.DisplayName}'s two copies?",
                $"This computer already has {them.DisplayName}'s progress" + (here is null ? "" : $", last saved here {Words.When(here.LastSaved)}") +
                $". The file you chose was {saved}. PLC+ combines the two: every check and answer from both is kept, so nothing is lost. " +
                "The PIN on this computer stays as it is. Type your PIN next.",
                "Combine them…", "Asks for the PIN, then combines the two copies and opens the progress.", "Cancel", "Closes this; nothing changes."))
            return;
        UnlockResult? merged = null;
        var confirm = new ConfirmPinWindow(Owner, "PLC+ — Combine two copies", $"PIN for {them.DisplayName}",
            "The PIN for either copy works, if it was changed on one computer.", "Combine and open", "Combines the two copies and opens the progress.",
            pin =>
            {
                try
                {
                    var r = Store.ImportMerge(plan, pin, DateTimeOffset.Now);
                    if (r.File is null) return r.Message;
                    merged = r;
                    return null;
                }
                catch (Exception ex) when (ex is LearnerFileException or IOException or UnauthorizedAccessException)
                {
                    return ex.Message;
                }
            });
        if (confirm.ShowDialog() == true && merged?.File is { } file)
        {
            SignedIn?.Invoke(file);
            Done?.Invoke($"Combined: the work from {them.SavedOn} ({Words.When(them.LastSaved)}) is added to your progress here. Nothing was lost.");
        }
    }

    /// <summary>Several files at once (an instructor bringing in a class): new learners are added; one already here is left to combine on its own, with its PIN.</summary>
    private void ImportMany(string[] files)
    {
        int added = 0;
        var skipped = new List<string>();
        LearnerPlaces.EnsureShared(Store.Folder);
        foreach (var f in files)
        {
            try
            {
                var plan = Store.PlanImport(f);
                if (plan.Existing is not null || plan.SameFile) { skipped.Add($"{plan.Incoming.DisplayName} (already here: add that file by itself to combine the two, with the PIN)"); continue; }
                Store.ImportNew(plan);
                added++;
            }
            catch (Exception ex) when (ex is LearnerFileException or IOException or UnauthorizedAccessException)
            {
                skipped.Add($"{Path.GetFileName(f)} ({ex.Message})");
            }
        }
        Refresh();
        LockedBanner.Visibility = Visibility.Visible;
        LockedText.Text = $"Added {added} learner{(added == 1 ? "" : "s")}." + (skipped.Count == 0 ? "" : " Not added: " + string.Join("; ", skipped) + ".");
    }

    /// <summary>Something to tell the learner once they are in (a new PIN, two copies combined): the window shows it on its status line.</summary>
    public event Action<string>? Done;

    private void Forgot_Click(object sender, RoutedEventArgs e)
    {
        if (Selected?.Listing is not { Header: not null } learner) return;
        LearnerFile? opened = null;
        var w = new NewPinWindow(Owner, recovering: true, learner.Name, (code, pin) =>
        {
            try
            {
                var r = LearnerFile.Recover(learner.Path, code, pin, Store.Options, DateTimeOffset.Now);
                opened = r.File;
                return r.File is null ? r.Message : null;
            }
            catch (Exception ex) when (ex is LearnerFileException or IOException or UnauthorizedAccessException)
            {
                return ex.Message;
            }
        });
        if (w.ShowDialog() == true && opened is not null)
        {
            SignedIn?.Invoke(opened);
            Done?.Invoke("Your new PIN is set: use it from now on. Backups made before now still open with your old PIN.");
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (Selected?.Listing is { Header: null } damaged)
        {
            if (!ChoiceWindow.Ask(Owner, "PLC+ — Remove a damaged file", $"Remove {damaged.Name}?",
                    $"PLC+ cannot read this file ({damaged.Problem}). Removing it moves it to the Recycle Bin. If the learner has a backup, Add learner from file brings their progress back.",
                    "Move it to the Recycle Bin", "Takes the damaged file off the list.", "Cancel", "Leaves it as it is."))
                return;
            try { LearnerStore.Remove(damaged, LearnerPlaces.Recycle); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Say($"PLC+ could not remove the file. Details for support: {ex.Message}"); return; }
            Refresh();
            return;
        }
        if (Selected?.Listing is not { Header: not null } learner) return;
        LearnerFile? file = null;
        var confirm = new ConfirmPinWindow(Owner, "PLC+ — Remove a learner", $"Remove {learner.Name} from this computer?",
            $"Type {learner.Name}'s PIN first. Next, PLC+ offers to back up the progress, then moves the file to the Recycle Bin. " +
            "Other copies (backups, other computers) are not touched.",
            "Next", "Checks the PIN; nothing is removed yet.",
            pin =>
            {
                try
                {
                    var r = Store.Unlock(learner, pin, DateTimeOffset.Now);
                    file = r.File;
                    return r.File is null ? r.Message : null;
                }
                catch (Exception ex) when (ex is LearnerFileException or IOException or UnauthorizedAccessException)
                {
                    return ex.Message;
                }
            });
        if (confirm.ShowDialog() != true || file is null) return;
        using (file)
        {
            var backup = ChoiceWindow.Choose(Owner, "PLC+ — Remove a learner", $"Back up {learner.Name}'s progress first?",
                (file.Data.LastBackup is { } last ? $"The last backup was {Words.When(last)}" + (file.Data.NeedsBackup ? ", and there is work since." : ", and it is up to date.")
                    : "There is no backup yet.") +
                " Once removed from this computer, the progress is in the Recycle Bin until it is emptied, and in any backup.",
                "Back up, then remove…", "Saves a copy where you choose (a USB drive or a cloud folder), then removes the learner from this computer.",
                "Remove without a backup", "Removes the learner from this computer now.");
            if (backup is null) return;
            if (backup == true && Backups.Run(Owner, file) is null) return; // the backup was cancelled or failed: remove nothing
            try
            {
                LearnerStore.Remove(learner, LearnerPlaces.Recycle);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Say($"PLC+ could not remove the file. It may be open in PLC+ on another Windows account. Details for support: {ex.Message}");
                return;
            }
        }
        Refresh();
        LockedBanner.Visibility = Visibility.Visible;
        LockedText.Text = $"{learner.Name} is no longer on this computer. The file is in the Recycle Bin, if it was removed by mistake.";
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (new SettingsWindow(Owner, Settings).ShowDialog() != true) return;
        Store = new LearnerStore(Settings.Folder, LearnerPlaces.Options);
        Refresh();
    }

    private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow { Owner = Owner }.ShowDialog();

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LearnerPlaces.EnsureShared(Store.Folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{Store.Folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Say($"PLC+ could not open {Store.Folder}. Details for support: {ex.Message}");
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Owner?.Close();
}

/// <summary>A learner in the start screen's list.</summary>
public sealed class LearnerRow(LearnerListing listing)
{
    private static readonly Brush Warn = new SolidColorBrush(Color.FromRgb(0x9A, 0x4A, 0x00));

    public LearnerListing Listing { get; } = listing;
    public string Name => Listing.Name;

    public string Detail => Listing.Header is { } h
        ? $"Last used {Words.When(h.LastSaved)}" + (h.SavedOn.Length > 0 && !string.Equals(h.SavedOn, Environment.MachineName, StringComparison.OrdinalIgnoreCase) ? $" on {h.SavedOn}" : "")
        : Listing.Problem ?? "";

    public string Backup => Listing.Header is not { } h ? ""
        : h.LastBackup is null ? (h.LastChanged is null ? "No backup yet (nothing to back up so far)" : "⚠ Not backed up yet")
        : h.NeedsBackup ? $"⚠ Backup out of date: last backed up {Words.When(h.LastBackup.Value)}, and there is work since"
        : $"✓ Backed up {Words.When(h.LastBackup.Value)}";

    public Brush BackupBrush => Listing.Header is { } h && (h.NeedsBackup || (h.LastBackup is null && h.LastChanged is not null)) ? Warn : Brushes.DimGray;

    public string Spoken => $"{Name}. {Detail}. {Backup.TrimStart('⚠', '✓', ' ')}";

    public override string ToString() => Spoken;
}

/// <summary>Dates and times in words.</summary>
public static class Words
{
    public static string When(DateTimeOffset at)
    {
        var local = at.ToLocalTime();
        var today = DateTime.Today;
        var time = local.ToString("HH:mm", CultureInfo.CurrentCulture);
        return local.Date == today ? $"today at {time}"
            : local.Date == today.AddDays(-1) ? $"yesterday at {time}"
            : $"on {local.ToString("ddd d MMM yyyy", CultureInfo.CurrentCulture)} at {time}";
    }
}

/// <summary>Back up my progress (plan 6.10): a copy of the learner's file where they choose, such as a USB drive or a cloud folder.</summary>
public static class Backups
{
    /// <summary>
    /// Asks where, then writes the copy. Returns where it went, or null if the learner cancelled or it failed (and they were told).
    /// If an older backup of this learner is already there, with work from another computer, that work is combined in first, so a
    /// backup never overwrites anything (UX review: the USB copy from home).
    /// </summary>
    public static string? Run(Window? owner, LearnerFile file) => Run(owner, file, out _);

    public static string? Run(Window? owner, LearnerFile file, out string? combined)
    {
        combined = null;
        var folder = file.Data.BackupFolder is { } f && Directory.Exists(f) ? f : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var dialog = new SaveFileDialog
        {
            Title = "Back up my progress: choose a USB drive or a cloud folder (OneDrive, Google Drive …)",
            Filter = "PLC+ progress files (*.plcprogress)|*.plcprogress",
            FileName = SafeName(file.Data.Profile.DisplayName) + " - PLC+ progress.plcprogress",
            InitialDirectory = folder,
        };
        while (true)
        {
            if (dialog.ShowDialog(owner) != true) return null;
            var chosen = Path.GetFullPath(dialog.FileName);
            if (string.Equals(Path.GetDirectoryName(chosen), Path.GetDirectoryName(Path.GetFullPath(file.Path)), StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(owner, "That is the folder PLC+ keeps everyone's progress in, so it is not a backup. Choose a USB drive or a cloud folder, somewhere that is yours.",
                    "PLC+ — Back up my progress", MessageBoxButton.OK, MessageBoxImage.Information);
                continue;
            }
            if (!file.IsOpen)
            {
                MessageBox.Show(owner, "PLC+ locked your progress while this was open, so nothing was backed up. Sign in again and back up.",
                    "PLC+ — Back up my progress", MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }
            var (lastBackup, lastFolder) = (file.Data.LastBackup, file.Data.BackupFolder);
            try
            {
                var now = DateTimeOffset.Now;
                if (File.Exists(chosen))
                {
                    try
                    {
                        var there = LearnerFile.ReadHeader(chosen);
                        if (there.ProfileId == file.Header.ProfileId && there.Revision != file.Header.Revision && file.MergeFrom(there, Path.GetFileName(chosen), now))
                            combined = $"The copy already there (saved on {there.SavedOn}, {Words.When(there.LastSaved).Replace("on ", "", StringComparison.Ordinal)}) was combined in first, so nothing in it was lost.";
                    }
                    catch (LearnerFileException)
                    {
                        // Not a learner's file: the save dialog has already asked whether to replace it.
                    }
                }
                file.MarkBackedUp(Path.GetDirectoryName(chosen)!, now); // saved first, so the copy says it is a backup too
                file.CopyTo(chosen);
                return chosen;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Not backed up after all: put back when the last real backup was.
                (file.Data.LastBackup, file.Data.BackupFolder) = (lastBackup, lastFolder);
                try { file.Save(DateTimeOffset.Now); } catch (Exception again) when (again is IOException or UnauthorizedAccessException) { }
                MessageBox.Show(owner, $"PLC+ could not write the backup there. Choose another place, such as a different USB drive or your OneDrive folder.\n\nDetails for support: {ex.Message}",
                    "PLC+ — Back up my progress", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private static string SafeName(string name) => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
}
