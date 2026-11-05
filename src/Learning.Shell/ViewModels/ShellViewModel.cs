using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Learning.Core.Content;
using Learning.Core.Learners;
using Learning.Core.Progress;

namespace Learning.Shell.ViewModels;

/// <summary>
/// The program window's state (plan 7.1, MVVM step by step; Ron, 2026-11-05): who is signed in, the learning path with its
/// marks, the totals and time on task, the status line, the title, the backup button, and the Go commands. The window binds
/// to it. The item's tabs, the machines and the dialogs stay in the window for now; they move in later steps.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private const string ProgramTitle = "PLC+ (preview)";
    private static readonly LearningProgress NoOne = new();

    private readonly List<LearningItem> _order;
    private StudySession? _study;
    private double _unsavedSeconds;

    public ShellViewModel(ContentPackage package)
    {
        Package = package;
        _order = [.. package.AllItems];
        Dashboard = new PathNode { IsDashboard = true, Weight = FontWeights.SemiBold };
        Roots.Add(Dashboard);
        Reports = new PathNode { IsReports = true, Weight = FontWeights.SemiBold };
        Roots.Add(Reports);
        foreach (var m in package.Modules)
        {
            var node = new PathNode { Module = m };
            foreach (var i in m.Items) node.Children.Add(new PathNode { Module = m, Item = i, Parent = node });
            Roots.Add(node);
        }
    }

    public ContentPackage Package { get; }

    /// <summary>The learner's unlocked file; null while the start screen shows.</summary>
    public LearnerFile? Session { get; private set; }

    /// <summary>The signed-in learner's progress (nobody's while the start screen shows).</summary>
    public LearningProgress Progress => Session?.Data.Progress ?? NoOne;

    public ObservableCollection<PathNode> Roots { get; } = [];
    public PathNode Dashboard { get; }
    public PathNode Reports { get; }

    /// <summary>The item on screen (the window sets it), so time on task goes to the right item.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand), nameof(PreviousCommand))]
    private LearningItem? _currentItem;

    /// <summary>The module on screen (its first page, or one of its items): Next from a module's page opens its first item.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private LearningModule? _currentModule;

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _title = ProgramTitle;
    [ObservableProperty] private string _learnerName = "";
    [ObservableProperty] private string _backupText = "Back up…";
    [ObservableProperty] private string _backupTip = "";
    [ObservableProperty] private string _totalsText = "";
    [ObservableProperty] private string _timeText = "";
    /// <summary>The text size for this learner, 1 = normal (plan 7.7). Kept in their own file, so it follows them.</summary>
    [ObservableProperty] private double _textScale = 1.0;

    // ---- Signing in and out --------------------------------------------------------------------------

    /// <summary>Who was signed in before an idle lock, so the same learner carries on where they were.</summary>
    public string? LockedProfileId { get; private set; }

    /// <summary>A learner's file is open. True if it is the learner who was here before the idle lock (nothing on screen changes).</summary>
    public bool SignIn(LearnerFile file, DateTimeOffset now)
    {
        var same = LockedProfileId == file.Header.ProfileId;
        Session = file;
        LockedProfileId = null;
        file.Data.UsedContent(Package.Id, Package.Version, now);
        file.Data.Progress.Log(EventKinds.SignedIn, file.Header.ProfileId, $"Signed in on {Environment.MachineName}.", now);
        _study = file.Data.Progress.StartSession(now, Environment.MachineName);
        TextScale = file.Data.Preferences.TryGetValue("textScale", out var s) && double.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var v) ? Math.Clamp(v, 0.8, 2.0) : 1.0;
        Title = $"{ProgramTitle} — {file.Data.Profile.DisplayName}";
        LearnerName = file.Data.Profile.DisplayName;
        OnPropertyChanged(nameof(Progress));
        Save();
        Refresh();
        NotifyCommands();
        if (!same) (CurrentItem, CurrentModule) = (null, null);
        return same;
    }

    /// <summary>Saves and forgets the key: Switch learner, the idle lock, closing. Returns the learner to choose on the start screen, if any.</summary>
    public string? Lock(bool switching, DateTimeOffset now)
    {
        if (Session is not { } file) return null;
        var minutes = (int)Math.Round((_study?.ActiveSeconds ?? 0) / 60);
        file.Data.Progress.Log(switching ? EventKinds.SignedOut : EventKinds.Locked, file.Header.ProfileId,
            $"{(switching ? "Signed out" : "Locked after no use")}, {minutes} minute{(minutes == 1 ? "" : "s")} active.", now);
        Save();
        // After Switch learner nobody is chosen, so the next student cannot type their PIN against the last one's name.
        LockedProfileId = switching ? null : file.Header.ProfileId;
        file.Dispose();
        Session = null;
        _study = null;
        Title = ProgramTitle;
        LearnerName = "";
        OnPropertyChanged(nameof(Progress));
        NotifyCommands();
        return LockedProfileId;
    }

    private void NotifyCommands()
    {
        NextCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
        ContinueWhereILeftOffCommand.NotifyCanExecuteChanged();
        ShowDashboardCommand.NotifyCanExecuteChanged();
        ShowReportsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Saves the learner's file. A problem goes on the status line, in words.</summary>
    public void Save()
    {
        if (Session is not { IsOpen: true } s) return;
        try
        {
            s.Save(DateTimeOffset.Now);
            _unsavedSeconds = 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = "Your progress could not be saved on this computer. Keep PLC+ open and try Learner ▸ Back up my progress, so your work is not lost. Details for support: " + ex.Message;
        }
        ShowBackup();
    }

    /// <summary>
    /// Every few seconds: time on task (plan 7.5) counts while the learner is active (a key or the mouse used in the last two
    /// minutes), to the item on screen. Saved every five minutes of activity, and with everything else that saves.
    /// </summary>
    public void Tick(double seconds, bool active, DateTimeOffset now)
    {
        if (Session is null || _study is null || !active) return;
        LearningProgress.AddActiveTime(_study, CurrentItem?.Id, seconds, now);
        _unsavedSeconds += seconds;
        ShowTime(now);
        if (_unsavedSeconds >= 300) Save();
    }

    public void SetTextScale(double scale)
    {
        TextScale = Math.Clamp(Math.Round(scale, 2), 0.8, 2.0);
        if (Session is null) return;
        Session.Data.Preferences["textScale"] = TextScale.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Save();
    }

    private void ShowBackup()
    {
        if (Session is not { } s) return;
        (BackupText, BackupTip) = s.Data.LastBackup is null
            ? (s.Data.NeedsBackup ? "⚠ Not backed up: Back up…" : "Back up…", "You have not backed up your progress yet. Saves a copy on a USB drive or in a cloud folder.")
            : s.Data.NeedsBackup
                ? ("⚠ Backup out of date: Back up…", $"Last backed up {Words.When(s.Data.LastBackup.Value)}, and you have done more since. Saves a new copy.")
                : ($"✓ Backed up {Words.When(s.Data.LastBackup.Value)}", "Your backup has everything so far. Press to save another copy.");
    }

    // ---- The path: marks, details, totals ----------------------------------------------------------------

    /// <summary>Every row's mark and words, the totals and the time (after anything that changes progress).</summary>
    public void Refresh()
    {
        var on = Brush("PowerOnBrush");
        var started = Brush("AccentBrush");
        var muted = Brush("MutedTextBrush");
        var now = DateTimeOffset.Now;
        var summary = Core.Progress.Dashboard.Build(Package, Progress, now);
        var review = summary.ReviewSuggested.Count;
        Dashboard.Header = "My progress";
        Dashboard.Mark = "▤";
        Dashboard.MarkBrush = started;
        Dashboard.Detail = $"{summary.Totals.ItemsComplete} of {summary.Totals.Items} items · {Core.Progress.Dashboard.Duration(summary.Totals.Time)}" +
                           (review > 0 ? $" · {review} to review" : "");
        Dashboard.SpokenName = "My progress: " + Dashboard.Detail;
        Dashboard.Tip = "Your progress through the whole path: each module, the module checks, outcomes to review, and your time in PLC+";

        var ready = summary.Submissions.Count(s => s.State == SubmissionState.Ready);
        var made = Progress.Submissions.Count;
        Reports.Header = "Hand in a report";
        Reports.Mark = "✉";
        Reports.MarkBrush = ready > 0 ? started : muted;
        Reports.Detail = ready > 0 ? $"{ready} module check{(ready == 1 ? "" : "s")} ready to hand in"
            : made > 0 ? $"{made} report{(made == 1 ? "" : "s")} made"
            : "A PDF report for your instructor";
        Reports.SpokenName = "Hand in a report: " + Reports.Detail;
        Reports.Tip = "Make a progress report (a PDF) for a module check, or of everything so far, and hand it in to your instructor";

        foreach (var node in Roots.Where(r => r.Module is not null))
        {
            var entry = summary.Modules.First(e => e.Module == node.Module);
            var m = node.Module!;
            node.Header = $"{m.Number}. {m.Title}";
            node.Weight = FontWeights.SemiBold;
            node.Mark = Core.Progress.Dashboard.Mark(entry.State);
            node.MarkBrush = entry.State switch
            {
                PathState.Complete or PathState.Mastered => on,
                PathState.InProgress or PathState.NeedsReview => started,
                _ => muted,
            };
            node.Detail = entry.State switch
            {
                PathState.ComingLater => "Coming later",
                PathState.Outline => $"{entry.Complete} of {entry.Total} complete · outline",
                PathState.Mastered => $"{entry.Complete} of {entry.Total} complete · mastered{StatusWords(m)}",
                PathState.NeedsReview => $"{entry.Complete} of {entry.Total} complete · review suggested{StatusWords(m)}",
                PathState.StartAfter => $"{entry.Complete} of {entry.Total} complete · after module {string.Join(" and ", entry.RecommendedFirst.Select(x => x.Number))}{StatusWords(m)}",
                _ => $"{entry.Complete} of {entry.Total} complete{StatusWords(m)}",
            };
            node.SpokenName = $"Module {m.Number}, {m.Title}: {Core.Progress.Dashboard.Words(entry)}. {node.Detail}";
            node.Tip = $"Module {m.Number}: {m.Title}. {Core.Progress.Dashboard.Words(entry)}. Choose it to see what it teaches, what to finish first, and its items.";

            foreach (var child in node.Children)
            {
                var item = child.Item!;
                var state = Progress.StateOf(item);
                child.Header = $"{item.Number} {item.Title}";
                (child.Mark, child.MarkBrush) = state switch
                {
                    ItemState.Complete => ("✓", on),
                    ItemState.InProgress => ("◐", started),
                    _ => ("○", muted),
                };
                child.Detail = ItemDetail(item, state);
                child.SpokenName = $"{item.Number} {item.Title}: {child.Detail}";
                child.Tip = item.Kind == ItemKind.Assess
                    ? item.Quiz is null ? "The module check: complete when every item it covers is" : "The module check: complete when every item it covers is complete and you pass its questions"
                    : item.Exercise is null ? "A reading: complete when you press I have read this" : "An exercise: complete when your program passes every test";
            }
        }
        TotalsText = $"{summary.Totals.ItemsComplete} of {summary.Totals.Items} items · {summary.Totals.ModulesComplete} of {summary.Totals.Modules} modules complete (the {summary.Totals.Modules} written so far)";
        ShowTime(now);
    }

    private void ShowTime(DateTimeOffset now)
    {
        var all = Progress.TimeOnTask();
        var today = Progress.TimeOnTask(s => s.Start.ToLocalTime().Date == now.ToLocalTime().Date);
        TimeText = all.TotalMinutes < 1 ? "Time in PLC+: none yet"
            : $"Time in PLC+: {Core.Progress.Dashboard.Duration(all)}" + (today.TotalMinutes >= 1 ? $" (today {Core.Progress.Dashboard.Duration(today)})" : "");
    }

    private static Brush Brush(string key) => Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;

    public static string StatusWords(LearningModule m) => m.Status switch
    {
        ModuleStatus.Draft => " · first draft",
        ModuleStatus.Outline => " · outline",
        _ => "",
    };

    public string ItemDetail(LearningItem item, ItemState state)
    {
        if (item.Kind == ItemKind.Assess)
        {
            var done = item.Covers.Count(c => Package.FindItem(c) is { } i && Progress.StateOf(i) == ItemState.Complete);
            var questions = item.Quiz is null ? ""
                : Progress.Items.TryGetValue(item.Id, out var own) && own.Completed is not null ? " · questions passed"
                : Progress.Best(item) is { } quizBest ? $" · questions: best {quizBest.Score:0} of {quizBest.MaxScore:0}"
                : " · questions not passed yet";
            return $"Module check · {done} of {item.Covers.Count} items done{questions}";
        }
        var kind = item.Exercise is not null ? "Exercise" : item.Quiz is not null ? "Questions" : "Reading";
        if (state == ItemState.Complete) return $"{kind} · complete";
        if (!item.CompletesByReading && Progress.Best(item) is { } best) return $"{kind} · best {best.Score:0} of {best.MaxScore:0}";
        return state == ItemState.InProgress ? $"{kind} · started" : kind;
    }

    public string WhereYouAre(LearningItem i)
    {
        var state = Progress.StateOf(i);
        if (i.Kind == ItemKind.Assess) return ItemDetail(i, state).Replace("Module check · ", "", StringComparison.Ordinal);
        if (state == ItemState.Complete) return "Complete ✓";
        if (!i.CompletesByReading && Progress.Best(i) is { } best) return $"Started: best {best.Score:0} of {best.MaxScore:0}";
        return state == ItemState.InProgress ? "Started" : "Not started";
    }

    // ---- Going through the path ------------------------------------------------------------------------

    public LearningItem? NextInOrder(LearningItem item)
    {
        var index = _order.IndexOf(item);
        return index >= 0 && index + 1 < _order.Count ? _order[index + 1] : null;
    }

    public LearningItem? PreviousInOrder(LearningItem item)
    {
        var index = _order.IndexOf(item);
        return index > 0 ? _order[index - 1] : null;
    }

    /// <summary>Chooses an item in the tree (the window shows what is chosen).</summary>
    public void Select(LearningItem item)
    {
        var node = Roots.SelectMany(r => r.Children).FirstOrDefault(n => n.Item == item);
        if (node is null) return;
        node.Parent!.IsExpanded = true;
        node.IsSelected = true;
    }

    public void SelectModule(LearningModule module)
    {
        if (Roots.FirstOrDefault(r => r.Module == module) is { } node) { node.IsExpanded = true; node.IsSelected = true; }
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next() { if (NextTarget() is { } n) Select(n); }

    private bool CanGoNext() => Session is not null && NextTarget() is not null;

    private LearningItem? NextTarget() =>
        CurrentItem is { } i ? NextInOrder(i) : CurrentModule?.Items.FirstOrDefault();

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void Previous() { if (CurrentItem is { } i && PreviousInOrder(i) is { } p) Select(p); }

    private bool CanGoPrevious() => Session is not null && CurrentItem is { } i && PreviousInOrder(i) is not null;

    private bool SignedIn() => Session is not null;

    [RelayCommand(CanExecute = nameof(SignedIn))]
    private void ContinueWhereILeftOff()
    {
        if (Progress.Continue(Package) is { } next) Select(next);
        else Status = "Every item in the path is complete.";
    }

    [RelayCommand(CanExecute = nameof(SignedIn))]
    private void ShowDashboard()
    {
        Dashboard.IsSelected = false; // so choosing it again shows it again
        Dashboard.IsSelected = true;
        PageFocusRequested?.Invoke(); // from the menu or Ctrl+Shift+Home: the reader moves to the page
    }

    [RelayCommand(CanExecute = nameof(SignedIn))]
    private void ShowReports()
    {
        Reports.IsSelected = false;
        Reports.IsSelected = true;
        PageFocusRequested?.Invoke();
    }

    /// <summary>A command opened a page: the window puts the keyboard (and a screen reader) on it.</summary>
    public event Action? PageFocusRequested;

    // ---- My progress (the dashboard, plan 7.2–7.5) -----------------------------------------------------------

    /// <summary>The dashboard page, in the same Markdown as a module's first page.</summary>
    public string DashboardMarkdown()
    {
        var d = Core.Progress.Dashboard.Build(Package, Progress, DateTimeOffset.Now);
        var t = d.Totals;
        var md = new StringBuilder();
        md.AppendLine("## So far").AppendLine();
        md.AppendLine($"- **{t.ItemsComplete} of {t.Items} items** complete, in the modules written so far");
        md.AppendLine($"- **{t.ModulesComplete} of {t.Modules} modules** complete");
        md.AppendLine($"- **{t.OutcomesMastered} of {t.Outcomes} outcomes** mastered");
        md.AppendLine("- **Time in PLC+:** " + (t.Time.TotalMinutes < 1 ? "none yet" :
            $"{Core.Progress.Dashboard.Duration(t.Time)} in all, {Core.Progress.Dashboard.Duration(t.Today)} today, over {t.Sessions} sitting{(t.Sessions == 1 ? "" : "s")}") +
            ". Time counts while you are using PLC+ (a key or the mouse in the last two minutes), not while it sits idle.");
        md.AppendLine();

        md.AppendLine("## Review suggested").AppendLine();
        if (d.ReviewSuggested.Count == 0)
            md.AppendLine("Nothing to review. If an answer in a module check is wrong, or an exercise you passed later fails, it is listed here with what to read again.").AppendLine();
        foreach (var r in d.ReviewSuggested)
        {
            var why = string.Join("; ", r.Outcome.Evidence.Where(e => e.Contains("wrong", StringComparison.Ordinal) || e.Contains("did not pass", StringComparison.Ordinal)));
            md.AppendLine($"- ↺ **Module {r.Module.Number}:** {r.Outcome.Outcome.Text} *({why}.)*");
        }
        if (d.ReviewSuggested.Count > 0) md.AppendLine();

        md.AppendLine("## Module checks (where reports are made)").AppendLine();
        md.AppendLine("Each module ends with a module check. When it is complete, make a progress report there and hand it in to your instructor: **Hand in a report**, the second row on the left (Go ▸ Hand in a report, Ctrl+Shift+R).").AppendLine();
        md.AppendLine("| Module check | Where you are |").AppendLine("|---|---|");
        foreach (var s in d.Submissions)
            md.AppendLine($"| {s.Check} | {s.State switch { SubmissionState.Generated => "✓ ", SubmissionState.Ready => "● ", _ => "○ " }}{(s.State == SubmissionState.Ready ? "Complete: ready to hand in" : s.Detail)} |");
        md.AppendLine();
        md.AppendLine("*○ not reached yet. ● module check complete: ready to hand in. ✓ report made.*").AppendLine();

        md.AppendLine("## The path").AppendLine();
        md.AppendLine("| Module | Where you are | Items | Time |").AppendLine("|---|---|---|---|");
        foreach (var e in d.Modules)
            md.AppendLine($"| {e.Module.Number}. {e.Module.Title} | {Core.Progress.Dashboard.Mark(e.State)} {Core.Progress.Dashboard.Words(e)} | " +
                          (e.State == PathState.ComingLater ? "–" : $"{e.Complete} of {e.Total}") + $" | {(e.Time > TimeSpan.Zero ? Core.Progress.Dashboard.Duration(e.Time) : "–")} |");
        md.AppendLine();
        md.AppendLine("*★ mastered: complete, and every outcome mastered. ✓ complete, not every outcome mastered yet. ↺ complete, review suggested. ◐ in progress. ○ not started (\"after module N\": recommended first, not required). – coming later.*").AppendLine();

        md.AppendLine("## Outcomes").AppendLine();
        foreach (var e in d.Modules.Where(e => LearningProgress.Learnable(e.Module) && e.Outcomes.Count > 0))
        {
            md.AppendLine($"**Module {e.Module.Number}, {e.Module.Title}**").AppendLine();
            foreach (var o in e.Outcomes)
                md.AppendLine(o.State switch
                {
                    MasteryState.Mastered => $"- ★ {o.Outcome.Text} (mastered)",
                    MasteryState.NeedsReview => $"- ↺ {o.Outcome.Text} (review suggested)",
                    MasteryState.InProgress => $"- ◐ {o.Outcome.Text} (in progress)",
                    _ => $"- ○ {o.Outcome.Text}",
                });
            md.AppendLine();
        }
        return md.ToString();
    }
}
