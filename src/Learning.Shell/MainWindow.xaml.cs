using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Learning.Core;
using Learning.Core.Content;
using Learning.Core.Learners;
using Learning.Core.Progress;
using System.Windows.Threading;
using Learning.Shell.ViewModels;

namespace Learning.Shell;

/// <summary>
/// The program's window in the family layout (Statistle, BMPFast): the learning path as a tree on the
/// left (modules, then their items), and on the right a module's first page or an item with its four
/// tabs: Learn, Example, Exercise and Reference (Ron, 2026-11-04). Its state is in <see cref="ShellViewModel"/> (plan 7.1, MVVM
/// step by step): the learner, the path, the totals, the status line and the Go commands. What is still here draws the pages
/// and the item's tabs, and opens the dialogs; those move in later steps.
/// </summary>
public partial class MainWindow : Window
{
    public static readonly RoutedUICommand ShowTab = new("Show tab", nameof(ShowTab), typeof(MainWindow));
    public static readonly RoutedUICommand SwitchLearner = new("Switch learner", nameof(SwitchLearner), typeof(MainWindow));
    public static readonly RoutedUICommand TextLarger = new("Larger text", nameof(TextLarger), typeof(MainWindow));
    public static readonly RoutedUICommand TextSmaller = new("Smaller text", nameof(TextSmaller), typeof(MainWindow));
    public static readonly RoutedUICommand TextNormal = new("Normal size", nameof(TextNormal), typeof(MainWindow));

    private const double TickSeconds = 20;

    private readonly ContentPackage _package = null!;
    private readonly PlcContent _plc = null!;
    private readonly ShellViewModel _vm = null!;

    private DateTime _lastInput = DateTime.Now;
    private readonly HashSet<string> _openedThisSession = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _idleTimer = new() { Interval = TimeSpan.FromSeconds(TickSeconds) };

    /// <summary>The learner signed in (plan 6.1): their unlocked progress file. Null while the start screen shows.</summary>
    private LearnerFile? _session => _vm.Session;
    /// <summary>The signed-in learner's progress (nobody's while the start screen shows).</summary>
    private LearningProgress _progress => _vm.Progress;
    private LearningItem? _item;
    private LearningModule? _module;
    private bool _helpHiddenForMachine;

    public MainWindow()
    {
        InitializeComponent();
        // As much of the screen as is useful: the Exercise tab needs height for the rungs.
        var area = SystemParameters.WorkArea;
        Width = Math.Min(1560, area.Width - 16);
        Height = Math.Min(1040, area.Height - 16);
        try
        {
            _package = ContentPackage.Load(PlcContent.DefaultFolder);
            _plc = new PlcContent(PlcContent.DefaultFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or ContentException)
        {
            MessageBox.Show(
                "PLC+ could not open its lesson files (the content folder beside PLCPlus.exe). " +
                "Reinstall PLC+, or if you copied the program, copy the content folder with it.\n\nDetails: " + ex.Message,
                "PLC+ — cannot start", MessageBoxButton.OK, MessageBoxImage.Error);
            Loaded += (_, _) => Close();
            return;
        }

        _vm = new ShellViewModel(_package);
        DataContext = _vm;
        // The status line is a live region: a screen reader hears each new message (WPF announces it only when told to).
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.Status))
                System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(StatusText)
                    ?.RaiseAutomationEvent(System.Windows.Automation.Peers.AutomationEvents.LiveRegionChanged);
            else if (e.PropertyName == nameof(ShellViewModel.TextScale)) FitToTextSize();
        };
        _vm.PageFocusRequested += () => Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (ReportsPage.IsVisible) ReportsPanel.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            else DashboardView.Focus();
        });

        // Hand in a report (phase 8): the page draws PLC+ programs as ladder pictures for the report's program evidence.
        ReportsPanel.Package = _package;
        ReportsPanel.Session = () => _session;
        ReportsPanel.Save = () => { SaveProgress(); _vm.Refresh(); };
        ReportsPanel.Status = SetStatus;
        ReportsPanel.Picture = evidence =>
        {
            try { return LadderPrinter.Png(Plc.Engine.ProjectJson.Deserialize(evidence.Program)); }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException) { return null; }
        };

        // My program on the Exercise tab is kept in the learner's own file, not in a folder classmates share.
        foreach (var ws in new[] { ExampleWorkspace, ExerciseWorkspace })
        {
            ws.LoadWork = key => _session?.Data.Work.TryGetValue(key, out var w) == true ? w.Content : null;
            ws.SaveWork = (key, json) =>
            {
                if (_session is null) return;
                _session.Data.Work[key] = new SavedWork { Content = json, Saved = DateTimeOffset.Now };
                SaveProgress();
            };
        }
        SignInScreen.SignedIn += SignIn;
        SignInScreen.Done += text => Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => SetStatus(text));
        // Auto-lock (plan 6.4): any key or mouse use counts as activity.
        InputManager.Current.PreProcessInput += (_, e) => { if (e.StagingItem.Input is KeyboardEventArgs or MouseButtonEventArgs or MouseWheelEventArgs) _lastInput = DateTime.Now; };
        PreviewMouseMove += (_, _) => _lastInput = DateTime.Now;
        _idleTimer.Tick += (_, _) =>
        {
            // Not while a dialog is open (a save window, a question): it may be using the learner's file.
            if (System.Windows.Interop.ComponentDispatcher.IsThreadModal) return;
            // Time on task (plan 7.5): counted while a key or the mouse was used in the last two minutes.
            _vm.Tick(TickSeconds, DateTime.Now - _lastInput < TimeSpan.FromMinutes(2), DateTimeOffset.Now);
            if (_session is not null && DateTime.Now - _lastInput > TimeSpan.FromMinutes(Math.Max(1, SignInScreen.Settings.LockMinutes)))
                Lock($"PLC+ locked itself after {SignInScreen.Settings.LockMinutes} minutes without use, and saved your work. " +
                     $"{_session.Data.Profile.DisplayName}: type your PIN to carry on where you were. Someone else? Choose your own name.");
        };
        _idleTimer.Start();

        foreach (var view in new[] { LearnView, ReferenceView, ModuleView, ExerciseNone, DashboardView })
            view.WidgetFactory = w => LessonWidgets.Create(_plc, w);
        // A workspace speaks only while its tab is showing: a hidden one is loading, not answering the learner.
        foreach (var ws in new[] { ExampleWorkspace, ExerciseWorkspace }) ws.StatusChanged += text => { if (ws.IsVisible) SetStatus(text); };
        ExerciseWorkspace.Checked += Exercise_Checked;
        QuizPanel.Graded += Quiz_Graded;
        QuizPanel.FaultMachine = FaultMachine;
        // A wrong answer to a check names the item to read again: the first lesson that teaches the question's outcome.
        QuizPanel.WhereToLook = q => _package.AllItems.FirstOrDefault(i => i.Kind == ItemKind.Lesson && q.Objective is not null
            && i.Objectives.Contains(q.Objective, StringComparer.OrdinalIgnoreCase))?.ToString();

        Loaded += (_, _) => SignInScreen.Show();
    }

    // ---- Learners (plan 6.1–6.10) ---------------------------------------------------------------------

    /// <summary>A learner's file is open: show their progress, where they left off.</summary>
    private void SignIn(LearnerFile file)
    {
        _lastInput = DateTime.Now;
        var same = _vm.SignIn(file, DateTimeOffset.Now);
        SignInScreen.Visibility = Visibility.Collapsed;
        AppContent.Visibility = Visibility.Visible;
        if (same && (_item is not null || _module is not null || DashboardPage.Visibility == Visibility.Visible))
        {
            // Back after a lock: everything is as it was.
            _vm.CurrentItem = _item;
            SetStatus($"Welcome back, {file.Data.Profile.DisplayName}. Everything is as you left it.");
            return;
        }
        // Someone new: nothing of the last learner's work may show.
        QuizView.ForgetAll();
        _openedThisSession.Clear();
        _item = null;
        _module = null;
        foreach (var r in _vm.Roots) { r.IsSelected = false; r.IsExpanded = false; foreach (var c in r.Children) c.IsSelected = false; }
        Welcome();
    }

    /// <summary>Saves, forgets the key, and shows the start screen: Switch learner, the idle lock, and closing.</summary>
    private void Lock(string banner, bool switching = false)
    {
        if (_session is null) return;
        ExerciseWorkspace.CommitPendingEdit();
        QuizPanel.Remember();
        var id = _vm.Lock(switching, DateTimeOffset.Now);
        AppContent.Visibility = Visibility.Hidden;
        SignInScreen.Show(banner, id);
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        ExerciseWorkspace.CommitPendingEdit();
        SaveProgress();
        var where = Backups.Run(this, _session, out var combined);
        _backupMade = where is not null;
        if (_backupMade) SetStatus($"Backed up: a copy of your progress is now at {where}. {combined}{(combined is null ? "" : " ")}On another computer, Add learner from file brings it in.");
        else if (_session is not null) SetStatus("No backup was made.");
        if (combined is not null) _vm.Refresh();
        _vm.Save();
    }

    /// <summary>
    /// Asks for a backup when there is work since the last one (plan 6.10, Ron: backups are required): when the learner
    /// leaves, and at a submission point (a module check complete). True to carry on; false if they cancelled leaving.
    /// </summary>
    private bool OfferBackup(string when, bool leaving)
    {
        if (_session is not { Data.NeedsBackup: true } s) return true;
        var last = s.Data.LastBackup is { } b ? $"Your last backup was {Words.When(b)}, and you have done more since." : "You have not backed up your progress yet.";
        var text = $"{last} A backup is a copy of your progress file on a USB drive or in a cloud folder (OneDrive, Google Drive …). " +
                   "If this computer's copy is lost, Add learner from file brings you back to where you were.";
        if (!leaving)
        {
            if (ChoiceWindow.Ask(this, "PLC+ — Back up your progress", $"{when}: back up your progress?", text,
                    "Back up now…", "Choose where to save a copy of your progress file.", "Not now", "Carries on; the backup button (top right) is there when you want it."))
                Backup_Click(this, new RoutedEventArgs());
            return true;
        }
        var choice = ChoiceWindow.Choose(this, "PLC+ — Back up your progress", $"{when}: back up your progress first?", text,
            "Back up now…", "Choose where to save a copy, then carry on leaving.", "Not now", "Leaves without a backup; your work is saved on this computer.");
        if (choice is null) return false;
        if (choice == true)
        {
            Backup_Click(this, new RoutedEventArgs());
            if (!_backupMade)
            {
                SetStatus("No backup was made, so PLC+ is still open. Back up (top right), or leave again and choose Not now.");
                return false;
            }
        }
        return true;
    }

    private bool _backupMade;

    private void SwitchLearner_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null || !OfferBackup("Before you go", leaving: true)) return;
        Lock($"{_session.Data.Profile.DisplayName}'s progress is saved and locked. Next learner: choose your name in the list (not in the list? press New learner).", switching: true);
    }

    private void SignedIn_CanExecute(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = _session is not null;

    private void ChangePin_Click(object sender, RoutedEventArgs e)
    {
        if (_session is not { } s) return;
        var w = new NewPinWindow(this, recovering: false, s.Data.Profile.DisplayName, (now, pin) =>
        {
            if (!s.IsOpen) return "PLC+ locked your progress while this was open. Close this, sign in again, and change the PIN then.";
            if (!s.IsPin(now)) return "That is not the PIN you use now. Type the PIN you opened PLC+ with today.";
            try
            {
                s.ChangePin(pin, DateTimeOffset.Now);
                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return "PLC+ could not save the new PIN: " + ex.Message;
            }
        });
        if (w.ShowDialog() == true)
            SetStatus("Your PIN is changed: use the new one from now on. Backups made before now still open with the old PIN.");
    }

    /// <summary>Where a learner starts after signing in: where they left off, or the first module.</summary>
    private void Welcome()
    {
        var name = _session?.Data.Profile.DisplayName ?? "";
        var next = _progress.Continue(_package);
        if (next is not null && _progress.Items.Count > 0)
        {
            _vm.Select(next); // a returning learner goes straight back to work
            // After the item has opened (it sets its own status line first).
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                () => SetStatus($"Welcome back, {name}. Here is where you left off: {next}. {LearnFooterText.Text}"));
            return;
        }
        if (next is not null) _vm.SelectModule(next.Module);
        else _vm.Dashboard.IsSelected = true;
        SetStatus(next is null
            ? "Every item in the path is complete."
            : $"Welcome to PLC+, {name}. In this preview, modules 3 and 5 are written; the others are outlines. Start here with module {next.Module.Number}: press the button under its title, or choose any module on the left.");
    }

    private void PathTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not PathNode node) return;
        if (node.IsDashboard) ShowDashboard();
        else if (node.IsReports) ShowReports();
        else if (node.Item is { } item) ShowItem(item);
        else ShowModule(node.Module!);
    }

    // ---- My progress (plan 7.2–7.5) ---------------------------------------------------------------------

    private void ShowDashboard()
    {
        ExerciseWorkspace.CommitPendingEdit();
        QuizPanel.Remember();
        _item = null;
        _module = null;
        _vm.CurrentItem = null;
        _vm.CurrentModule = null;
        ItemPage.Visibility = ModulePage.Visibility = ReportsPage.Visibility = Visibility.Collapsed;
        DashboardPage.Visibility = Visibility.Visible;
        _vm.Refresh();
        DashboardTitle.Text = $"My progress · {_session?.Data.Profile.DisplayName}";
        var next = _progress.Continue(_package);
        DashboardContinueButton.Content = next is null ? "Every item is complete" : $"_Continue with {next}";
        DashboardContinueButton.IsEnabled = next is not null;
        DashboardView.Markdown = _vm.DashboardMarkdown();
        ShowScreenHelp();
        SetStatus("My progress: each module, the module checks, outcomes to review, and your time. Choose a module or an item on the left to work on it.");
    }

    // ---- Hand in a report (plan 8.2–8.7) ------------------------------------------------------------------

    /// <summary>The submission point to choose when the page opens from a module check's button.</summary>
    private string? _reportPoint;

    private void ShowReports()
    {
        ExerciseWorkspace.CommitPendingEdit();
        QuizPanel.Remember();
        _item = null;
        _module = null;
        _vm.CurrentItem = null;
        _vm.CurrentModule = null;
        ItemPage.Visibility = ModulePage.Visibility = DashboardPage.Visibility = Visibility.Collapsed;
        ReportsPage.Visibility = Visibility.Visible;
        _vm.Refresh();
        ReportsPanel.Show(_reportPoint);
        _reportPoint = null;
        ShowScreenHelp();
        SetStatus("Hand in a report: choose a module check (or everything so far), check what the report will say, then press Save the PDF report. Hand the PDF in the way your instructor asks.");
    }

    private void HandIn_Click(object sender, RoutedEventArgs e)
    {
        if (_item is not { Kind: ItemKind.Assess } check) return;
        _reportPoint = check.Id;
        _vm.ShowReportsCommand.Execute(null);
    }

    private void CheckReport_Click(object sender, RoutedEventArgs e) => new VerifyWindow(this).ShowDialog();

    // ---- A module's first page -------------------------------------------------------------------------

    private void ShowModule(LearningModule m)
    {
        ExerciseWorkspace.CommitPendingEdit();
        QuizPanel.Remember();
        _module = m;
        _item = null;
        _vm.CurrentItem = null;
        _vm.CurrentModule = m;
        ShowScreenHelp();
        ItemPage.Visibility = DashboardPage.Visibility = ReportsPage.Visibility = Visibility.Collapsed;
        ModulePage.Visibility = Visibility.Visible;

        var p = _progress.Of(m);
        ModuleTitle.Text = $"Module {m.Number} · {m.Title}";
        ModuleStatusText.Text = m.Status == ModuleStatus.ComingLater
            ? "Coming in a later phase of PLC+. It is in the path so you can see where it fits."
            : $"{p.Complete} of {p.Total} items complete" + (m.Minutes > 0 ? $" · about {Hours(m.Minutes)}" : "") + m.Status switch
            {
                ModuleStatus.Draft => " · a first draft for the instructors' review",
                ModuleStatus.Outline => " · an outline: the outcomes and items are set, most pages are still to be written",
                _ => "",
            };

        var missing = _progress.MissingPrerequisites(_package, m);
        ModuleEarlyText.Visibility = missing.Count > 0 && m.Status != ModuleStatus.ComingLater ? Visibility.Visible : Visibility.Collapsed;
        ModuleEarlyText.Text = $"PLC+ recommends finishing {string.Join(" and ", missing.Select(x => $"module {x.Number} ({x.Title})"))} first. You can still start this module now; PLC+ notes that you started it early.";

        var first = m.Items.FirstOrDefault(i => _progress.StateOf(i) != ItemState.Complete) ?? m.Items.FirstOrDefault();
        ModuleStartButton.Visibility = first is null ? Visibility.Collapsed : Visibility.Visible;
        ModuleStartButton.Tag = first;
        ModuleStartButton.Content = first is null ? "" : p.State == ModuleState.NotStarted ? $"_Start with {first}" : p.State == ModuleState.Complete ? $"_Open {first}" : $"_Continue with {first}";
        ModuleStartButton.ToolTip = "Opens the item, on its Learn tab";

        ModuleView.Markdown = ModuleMarkdown(m);
    }

    private string ModuleMarkdown(LearningModule m)
    {
        var md = new StringBuilder();
        md.AppendLine("## Recommended first").AppendLine();
        if (m.Prerequisites.Count == 0) md.AppendLine("Nothing: this module can be the first you take.").AppendLine();
        foreach (var id in m.Prerequisites)
            if (_package.FindModule(id) is { } pre)
            {
                var pp = _progress.Of(pre);
                md.AppendLine($"- **Module {pre.Number}, {pre.Title}**: {(pp.State == ModuleState.Complete ? "complete ✓" : $"{pp.Complete} of {pp.Total} items complete")}");
            }
        md.AppendLine().AppendLine("## What you will be able to do").AppendLine();
        if (m.Status == ModuleStatus.ComingLater)
            foreach (var o in m.Outcomes) md.AppendLine("- " + o.Text);
        else
        {
            // Mastery per outcome (plan 5.6): a mark and words, never colour alone.
            foreach (var op in Mastery.Of(_package, _progress, m))
                md.AppendLine(op.State switch
                {
                    MasteryState.Mastered => $"- ★ {op.Outcome.Text} **(mastered)**",
                    MasteryState.NeedsReview => $"- ↺ {op.Outcome.Text} **(review suggested)**: " + string.Join("; ", op.Evidence.Where(e => e.Contains("wrong", StringComparison.Ordinal) || e.Contains("did not pass", StringComparison.Ordinal))) + ".",
                    MasteryState.InProgress => $"- ◐ {op.Outcome.Text} (in progress: " + string.Join("; ", op.Evidence.Where(e => !e.EndsWith(": complete", StringComparison.Ordinal) && !e.EndsWith(" answered", StringComparison.Ordinal))) + ")",
                    _ => $"- ○ {op.Outcome.Text}",
                });
            md.AppendLine().AppendLine("*★ mastered: its items are complete and its questions were right. ↺ review suggested: the work is done, but an answer in the module check was wrong, or a passed exercise later failed; it says what to do. ◐ in progress. ○ not started.*");
        }
        md.AppendLine().AppendLine("## Topics").AppendLine();
        md.AppendLine(string.Join(" · ", m.Topics)).AppendLine();
        if (m.Items.Count > 0)
        {
            md.AppendLine("## The items in this module").AppendLine();
            md.AppendLine("Each item builds on the one before it. Choose one on the left to open it.").AppendLine();
            md.AppendLine("| Item | What it is | Where you are |").AppendLine("|---|---|---|");
            foreach (var i in m.Items)
            {
                var kind = i.Kind == ItemKind.Assess ? (i.Quiz is null ? "Module check" : $"Module check, with {i.Quiz.Questions.Count} questions")
                    : i.Exercise is not null ? "Lesson, example and exercise"
                    : i.Quiz is not null ? (i.Example is null ? "Lesson and questions" : "Lesson, example and questions")
                    : i.Example is null ? "Reading" : "Reading, with an example to run";
                md.AppendLine($"| {i.Number} {i.Title} | {kind} | {_vm.WhereYouAre(i)} |");
            }
            md.AppendLine();
        }
        if (m.Introduction.Trim().Length > 0) md.AppendLine("## About this module").AppendLine().AppendLine(m.Introduction);
        return md.ToString();
    }

    private static string Hours(int minutes) => minutes < 90 ? $"{minutes} minutes" : $"{minutes / 60.0:0.#} hours";

    private void ModuleStart_Click(object sender, RoutedEventArgs e)
    {
        if (ModuleStartButton.Tag is LearningItem item) _vm.Select(item);
    }

    // ---- An item -------------------------------------------------------------------------------------

    private void ShowItem(LearningItem item)
    {
        ExerciseWorkspace.CommitPendingEdit();
        QuizPanel.Remember();
        _item = item;
        _module = item.Module;
        _vm.CurrentItem = item;
        _vm.CurrentModule = item.Module;
        ModulePage.Visibility = DashboardPage.Visibility = ReportsPage.Visibility = Visibility.Collapsed;
        ItemPage.Visibility = Visibility.Visible;
        HandInButton.Visibility = item.Kind == ItemKind.Assess ? Visibility.Visible : Visibility.Collapsed;

        _progress.Opened(_package, item, DateTimeOffset.Now);
        if (_session is not null && _openedThisSession.Add(item.Id)) _progress.Log(EventKinds.Opened, item.Id, $"Opened {item}.", DateTimeOffset.Now);
        SaveProgress();

        ItemModuleText.Text = $"Module {item.Module.Number} · {item.Module.Title}";
        ItemTitle.Text = $"{item.Number} {item.Title}";
        var missing = _progress.MissingPrerequisites(_package, item.Module);
        PreviousButton.ToolTip = _vm.PreviousInOrder(item) is { } before ? $"Back to {before} (Ctrl+Shift+PgUp)" : "This is the first item in the path";
        NextButton.ToolTip = _vm.NextInOrder(item) is { } after ? $"On to {after} (Ctrl+Shift+PgDn)" : "This is the last item in the path";

        // Learn
        LearnView.Markdown = item.Learn.Trim().Length > 0
            ? item.Learn
            : $"# {item.Title}\n\n*This page is still to be written: module {item.Module.Number} is an outline in this preview. Its learning outcomes are on the module's first page.*";
        ReadButton.Visibility = item.CompletesByReading ? Visibility.Visible : Visibility.Collapsed;
        ExerciseWorkspace.SaveName = $"{item.Number} {item.Title} — My program";

        // Example
        var example = _plc.Find(item.Example);
        ExampleScroll.Visibility = example is null ? Visibility.Collapsed : Visibility.Visible;
        ExampleWorkspace.Visibility = ExampleScroll.Visibility;
        ExampleNone.Visibility = example is null ? Visibility.Visible : Visibility.Collapsed;
        ExampleNone.Text = item.Kind == ItemKind.Assess
            ? "A module check has no example of its own: each item it covers has its own."
            : "This item has no machine to run. Its Learn tab has the pictures and tables that go with it.";
        if (example is not null)
        {
            ExampleWorkspace.Load(_plc, example, exercise: false, item.ExampleProgram);
            // D21: the example is a machine like the exercise's, not the same one. Say so where the learner looks.
            if (_plc.Find(item.Exercise) is { } own && own.Challenge.Id != example.Challenge.Id)
            {
                ExampleWorkspace.TitleText.Text = "An example: " + example.Challenge.Title;
                ExampleWorkspace.BriefText.Text = example.Challenge.Brief + Environment.NewLine + Environment.NewLine
                    + $"This is an example machine, like your exercise's but not the same one. Watch how it works here, then write your own program for “{own.Challenge.Title}” on the Exercise tab.";
            }
        }
        ExampleTab.Header = example is null ? "Exa_mple (none)" : "Exa_mple";
        ExampleTab.ToolTip = example is null ? "This item has no machine to run (Ctrl+2)" : "Run the working program on its machine (Ctrl+2)";

        // Exercise
        var exercise = _plc.Find(item.Exercise);
        ExerciseScroll.Visibility = exercise is null ? Visibility.Collapsed : Visibility.Visible;
        ExerciseWorkspace.Visibility = ExerciseScroll.Visibility;
        QuizScroll.Visibility = exercise is null && item.Quiz is not null ? Visibility.Visible : Visibility.Collapsed;
        ExerciseNone.Visibility = exercise is null && item.Quiz is null ? Visibility.Visible : Visibility.Collapsed;
        if (exercise is not null) ExerciseWorkspace.Load(_plc, exercise, exercise: true);
        else if (item.Quiz is { } quiz) LoadQuiz(item, quiz);
        else ExerciseNone.Markdown = NoExerciseMarkdown(item);
        ExerciseTab.Header = exercise is not null || item.Quiz is not null || item.Kind == ItemKind.Assess ? "Exerc_ise" : "Exerc_ise (none)";
        ExerciseTab.ToolTip = exercise is not null ? "Write your own program and check it (Ctrl+3)"
            : item.Quiz is not null ? $"{item.Quiz.Questions.Count} questions to answer (Ctrl+3)"
            : item.Kind == ItemKind.Assess ? "What this module check covers, and where you are (Ctrl+3)"
            : "This item has no exercise: it is complete when you have read it (Ctrl+3)";

        // Reference
        ReferenceView.Markdown = ReferenceMarkdown(item);

        ShowItemStatus();
        ItemTabs.SelectedItem = LearnTab;
        ShowScreenHelp();
        SetStatus($"{item}: {LearnFooterText.Text}" + (missing.Count > 0
            ? $" (Recommended first: {string.Join(" and ", missing.Select(m => $"module {m.Number}"))}. You can work on this now; PLC+ notes that you started early.)"
            : ""));
    }

    private void ShowItemStatus()
    {
        if (_item is not { } item) return;
        var state = _progress.StateOf(item);
        var parts = new List<string>();
        if (item.Minutes > 0) parts.Add($"About {item.Minutes} minutes");
        if (item.BuildsOn is { } b && _package.FindItem(b) is { } before) parts.Add($"Builds on {before}");
        parts.Add(item.Kind == ItemKind.Assess
            ? state == ItemState.Complete ? (item.Quiz is null ? "Complete ✓: every item it covers is complete" : "Complete ✓: every item it covers is complete, and you passed the questions") : _vm.ItemDetail(item, state)
            : state == ItemState.Complete ? "Complete ✓"
            : item.Exercise is not null ? (_progress.Best(item) is { } best ? $"Not complete yet: best so far {best.Score:0} of {best.MaxScore:0}" : "Not complete yet: complete when your program passes on the Exercise tab")
            : item.Quiz is not null ? (_progress.Best(item) is { } qbest ? $"Not complete yet: best so far {qbest.Score:0} of {qbest.MaxScore:0}" : "Not complete yet: complete when you pass the questions on the Exercise tab")
            : "Not complete yet: complete when you have read it");
        ItemStatusText.Text = string.Join(" · ", parts);

        LearnFooterText.Text = item.Kind == ItemKind.Assess
            ? item.Quiz is null ? "The Exercise tab lists what this check covers and where you are." : "The questions are on the Exercise tab (Ctrl+3), under what this check covers."
            : item.Quiz is not null && item.Exercise is null
                ? state == ItemState.Complete ? "Complete. Next ▸ (top right) goes on to the next item." : "When you have read the page, answer the questions on the Exercise tab (Ctrl+3)."
            : item.Exercise is not null
                ? "Next: run the working program on the Example tab (Ctrl+2), then write your own on the Exercise tab (Ctrl+3)."
                : state == ItemState.Complete
                    ? "Complete. Next ▸ (top right) goes on to the next item."
                    : item.Example is not null ? "Try the Example tab (Ctrl+2) too. When you have read the page, press I have read this." : "When you have read the page, press I have read this.";
        var unwritten = item.Learn.Trim().Length == 0;
        ReadButton.IsEnabled = state != ItemState.Complete && !unwritten;
        ReadButton.ToolTip = state == ItemState.Complete ? "This item is already complete"
            : unwritten ? "This page is still to be written, so it cannot be marked as read yet"
            : "Marks this item complete. Items without an exercise are complete when you have read them.";
        if (unwritten && item.Kind == ItemKind.Lesson) LearnFooterText.Text = "This page is still to be written. Modules 3 and 5 are written in this preview.";
    }

    private string NoExerciseMarkdown(LearningItem item)
    {
        if (item.Kind != ItemKind.Assess)
            return item.Module.Status is ModuleStatus.Outline or ModuleStatus.ComingLater
                ? "## No exercise yet\n\nThis item is still to be written: its pages and any exercise come in a later version of PLC+."
                : "## No exercise for this item\n\nThis item is complete when you have read it: press **I have read this** on the Learn tab." +
                  (item.Example is not null ? " Its Example tab has a machine to run." : "");

        var md = new StringBuilder("## What this module check covers\n\n");
        md.AppendLine("| Item | Where you are |").AppendLine("|---|---|");
        foreach (var c in item.Covers)
            if (_package.FindItem(c) is { } covered)
                md.AppendLine($"| {covered} | {_vm.WhereYouAre(covered)} |");
        md.AppendLine().AppendLine(_progress.StateOf(item) == ItemState.Complete
            ? "Every item is complete: so is this module check. ✓"
            : "When every item above is complete, so is this module check.");
        md.AppendLine().AppendLine("**To hand in your work:** press **Hand in a report…** (top right) to make this check's progress report: your name, your scores, the tests and your programs, as a PDF to hand in to your instructor.");
        return md.ToString();
    }

    private string ReferenceMarkdown(LearningItem item)
    {
        if (item.Kind == ItemKind.Assess)
        {
            var md = new StringBuilder($"# Module {item.Module.Number}: what you should be able to do\n\n");
            foreach (var o in item.Module.Outcomes) md.AppendLine("- " + o.Text);
            return md.ToString();
        }
        var pages = item.References.Select(r => _package.References.TryGetValue(r, out var p) ? p : null).OfType<ReferencePage>().ToList();
        if (pages.Count == 0) return "# Reference\n\n*This item has no reference pages yet.*";
        return string.Join("\n\n", pages.Select(p => p.Body));
    }

    private void Read_Click(object sender, RoutedEventArgs e)
    {
        if (_item is null || !_progress.MarkRead(_item, DateTimeOffset.Now)) return;
        Completed($"{_item.Number} is complete.");
    }

    // ---- Questions (plan 5.5) ------------------------------------------------------------------------

    private void LoadQuiz(LearningItem item, Quiz quiz)
    {
        var intro = new List<string>();
        if (item.Kind == ItemKind.Assess)
        {
            intro.Add("This check covers: " + string.Join("; ", item.Covers.Select(_package.FindItem).OfType<LearningItem>()
                .Select(c => $"{c} ({_vm.WhereYouAre(c).ToLowerInvariant()})")) + ".");
            intro.Add("The module is complete when those items are complete and you have passed these questions.");
        }
        QuizPanel.Load(quiz, item.Kind == ItemKind.Assess ? $"Module {item.Module.Number} check: the questions" : "Questions", intro);
    }

    /// <summary>A "find the fault" question's machine: the working program, with the question's wiring fault.</summary>
    private UIElement? FaultMachine(Question q)
    {
        if (_plc.Find(q.Machine) is not { } challenge || Plc.Scenes.WiringFault.Parse(q.Fault) is not { } fault) return null;
        var ws = new MachineWorkspace { Height = 640 };
        ws.StatusChanged += text => { if (ws.IsVisible) SetStatus(text); };
        ws.Load(_plc, challenge, exercise: false, q.Program, fault);
        return ws;
    }

    private void Quiz_Graded(QuizResult result)
    {
        if (_item is null) return;
        var wasComplete = _progress.StateOf(_item) == ItemState.Complete;
        _progress.Record(_item, result.ToOutcome(), DateTimeOffset.Now, machine: Environment.MachineName);
        var right = result.Questions.Count(q => q.Correct);
        if (_progress.StateOf(_item) == ItemState.Complete && !wasComplete)
            Completed($"Passed: {right} of {result.Questions.Count} right. {_item.Number} is complete.");
        else
        {
            SaveProgress();
            _vm.Refresh();
            ShowItemStatus();
            SetStatus(result.Passed
                ? _item.Kind == ItemKind.Assess && _progress.StateOf(_item) != ItemState.Complete
                    ? $"Passed: {right} of {result.Questions.Count} right. The check is complete when the items it covers are complete too (listed at the top)."
                    : $"Passed: {right} of {result.Questions.Count} right."
                : $"Not passed yet: {right} of {result.Questions.Count} right. Each ✗ says where to look again; change those answers and press Check my answers again. Your answers stay while PLC+ is open.");
        }
    }

    private void Exercise_Checked(ChallengeOutcome outcome, int hintsShown)
    {
        if (_item is null || !string.Equals(_item.Exercise, outcome.ChallengeId, StringComparison.OrdinalIgnoreCase)) return;
        var wasComplete = _progress.StateOf(_item) == ItemState.Complete;
        _progress.Record(_item, outcome, DateTimeOffset.Now, hintsShown, Environment.MachineName);
        // The program that passed is kept, so a report can show it even after My program changes (plan 8.3, program evidence).
        if (outcome.Passed && _session is not null && WorkHash.Of(ExerciseWorkspace.ProgramJson) == outcome.WorkHash)
            _session.Data.Work[Learning.Core.Reports.ReportBuilder.PassedWorkKey(outcome.ChallengeId)] = new SavedWork { Content = ExerciseWorkspace.ProgramJson, Saved = DateTimeOffset.Now };
        if (outcome.Passed && !wasComplete) Completed($"Every test passed: {_item.Number} is complete.");
        else
        {
            SaveProgress();
            _vm.Refresh();
            ShowItemStatus();
        }
    }

    private void Completed(string what)
    {
        SaveProgress();
        _vm.Refresh();
        ShowItemStatus();
        var module = _item!.Module;
        var p = _progress.Of(module);
        var next = _progress.NextAfter(_package, _item);
        SetStatus(what +
            (p.State == ModuleState.Complete ? $" That completes module {module.Number}." : $" Module {module.Number}: {p.Complete} of {p.Total} complete.") +
            (next is null ? " Every item in the path is complete." : $" Next: {next}{(next == _vm.NextInOrder(_item) ? " (Next ▸, top right)" : " (Go ▸ Continue where I left off)")}.") +
            (_item.Exercise is not null ? " To keep your program or hand it in: File ▸ Save My program as…" : ""));
        // A module check complete is a submission point: the time to make sure there is a backup (plan 6.10).
        var check = module.Items.FirstOrDefault(i => i.Kind == ItemKind.Assess);
        if (check is not null && _progress.StateOf(check) == ItemState.Complete && (_item == check || check.Covers.Contains(_item.Id, StringComparer.OrdinalIgnoreCase)))
        {
            SetStatus(_vm.Status + $" The module {module.Number} check is complete: your report is ready to hand in (Go ▸ Hand in a report, Ctrl+Shift+R).");
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () => OfferBackup($"Module {module.Number} is complete", leaving: false));
        }
    }

    private void SaveProgress() => _vm.Save();

    private void ItemTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, ItemTabs)) return; // a ComboBox inside a tab
        ExerciseWorkspace.CommitPendingEdit();
        var machine = (ItemTabs.SelectedItem == ExampleTab && ExampleWorkspace.Visibility == Visibility.Visible)
                      || (ItemTabs.SelectedItem == ExerciseTab && ExerciseWorkspace.Visibility == Visibility.Visible)
                      || (ItemTabs.SelectedItem == ExerciseTab && QuizScroll.Visibility == Visibility.Visible
                          && _item?.Quiz?.Questions.Any(q => q.Kind == QuestionKind.Fault) == true);
        ShowScreenHelp();
        var hid = "";
        if (machine && HelpPanel.Visibility == Visibility.Visible && !_helpHiddenForMachine)
        {
            _helpHiddenForMachine = true;
            HelpPanel.Visibility = Visibility.Collapsed;
            hid = " (The help panel is hidden to make room for the machine; F1 shows it.)";
        }
        if (ItemTabs.SelectedItem == ExerciseTab && ExerciseWorkspace.Visibility == Visibility.Visible)
            SetStatus("My program: click an element and type its tag in the Tag box, then use the tools above the rungs. Check my program tests it." + hid);
        else if (ItemTabs.SelectedItem == ExerciseTab && QuizScroll.Visibility == Visibility.Visible)
            SetStatus("Answer the questions, then press Check my answers at the bottom." + hid);
        else if (ItemTabs.SelectedItem == ExampleTab && ExampleWorkspace.Visibility == Visibility.Visible)
            SetStatus("The working program, running: work the machine and watch the rungs. Program switches to the common mistake. Examples do not count toward your progress." + hid);
    }

    /// <summary>The machine screen needs about this much height to show the rungs; a shorter window scrolls the tab.</summary>
    private const double MachineMinHeight = 760;

    private void MachineScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is ScrollViewer { Content: MachineWorkspace ws } scroll)
            ws.Height = Math.Max(MachineMinHeight, scroll.ActualHeight);
    }

    // ---- Moving around ---------------------------------------------------------------------------------

    private void ShowTab_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is string s && int.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var i) && i < ItemTabs.Items.Count)
            ItemTabs.SelectedIndex = i;
    }

    private void ItemShown_CanExecute(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = _item is not null;

    // ---- File menu: the program on the Example or Exercise tab ----------------------------------------

    private MachineWorkspace? ShownWorkspace =>
        _item is null ? null
        : ItemTabs.SelectedItem == ExerciseTab && ExerciseWorkspace.Visibility == Visibility.Visible ? ExerciseWorkspace
        : ItemTabs.SelectedItem == ExampleTab && ExampleWorkspace.Visibility == Visibility.Visible ? ExampleWorkspace
        : null;

    /// <summary>My program, whichever tab is showing, when the item has an exercise.</summary>
    private MachineWorkspace? MyProgram => _item?.Exercise is not null && ExerciseWorkspace.Visibility == Visibility.Visible ? ExerciseWorkspace : null;

    private void FileMenu_Opened(object sender, RoutedEventArgs e)
    {
        var ws = ShownWorkspace ?? MyProgram;
        var exercise = MyProgram is not null;
        OpenProgramItem.IsEnabled = SaveProgramItem.IsEnabled = exercise;
        SavePictureItem.IsEnabled = ws is not null;
        const string why = " Available on an item that has an exercise.";
        OpenProgramItem.ToolTip = "Opens a program saved from PLC+ (.plcprogram) as My program on the Exercise tab." + (exercise ? "" : why);
        SaveProgramItem.ToolTip = "Saves My program to a file you choose (.plcprogram), to keep, hand in or open on another computer." + (exercise ? "" : why);
        SavePictureItem.ToolTip = "Saves the rungs as a picture (PNG), for a report or a handout: the Example's program on the Example tab, My program otherwise." + (ws is null ? " Available on an item with a machine." : "");
        foreach (var item in new[] { OpenProgramItem, SaveProgramItem, SavePictureItem }) ToolTipService.SetShowOnDisabled(item, true);
    }

    private void OpenProgram_Click(object sender, RoutedEventArgs e)
    {
        if (MyProgram is not { } ws) return;
        ItemTabs.SelectedItem = ExerciseTab; // so the learner sees what was opened
        ws.OpenProgram();
    }

    private void SaveProgram_Click(object sender, RoutedEventArgs e) => MyProgram?.SaveProgram();

    private void SavePicture_Click(object sender, RoutedEventArgs e) => (ShownWorkspace ?? MyProgram)?.SavePicture();

    /// <summary>The help panel's first part says what the screen showing now is for (checklist: local help).</summary>
    private void ShowScreenHelp()
    {
        (HelpScreenTitle.Text, HelpScreenText.Text) = ReportsPage.Visibility == Visibility.Visible
            ? ("This screen: Hand in a report", "1. Choose a module check, or everything so far. 2. Check what the report will say; My details for reports adds your student ID, course and class code to the cover. 3. Save the PDF report, and hand in that file as it is, the way your instructor asks; do not save it again in another program. The result line is a one-line summary of the report your instructor can check: copy it if they ask for it. A report made before the check is complete is marked Incomplete. Every report you make is listed at the bottom.")
            : DashboardPage.Visibility == Visibility.Visible
            ? ("This screen: My progress", "Your progress through the whole path. ★ mastered, ✓ complete, ↺ review suggested, ◐ in progress, ○ not started. Review suggested lists what to read again. At a module check, make a report to hand in: Hand in a report, the next row on the left. Time counts while you use PLC+.")
            : _item is null
            ? ("This screen: a module", "A module's first page: what to finish first, what you will be able to do, its topics and its items. Press the button under the title to start, or choose an item on the left.")
            : ItemTabs.SelectedItem == ExampleTab && ExampleWorkspace.Visibility == Visibility.Visible
                ? ("This screen: Example", "The working program runs on the machine. Work the machine and watch power flow along the rungs. Program switches to a common mistake; Check this program runs the task's tests on it. Examples do not count toward your progress.")
            : ItemTabs.SelectedItem == ExerciseTab && ExerciseWorkspace.Visibility == Visibility.Visible
                ? ("This screen: Exercise", "Write your own program. Click a part and type its tag in the Tag box, then press Enter. The tools above the rungs add contacts (key O), coils (Q), branches (B) and boxes (I). Check my program runs the tests: when they all pass, the item is complete. File ▸ Save My program as… keeps a copy to hand in.")
            : ItemTabs.SelectedItem == ReferenceTab
                ? ("This screen: Reference", "The symbols and rules this item uses, with the Allen-Bradley names, to look up while you work.")
            : ItemTabs.SelectedItem == ExerciseTab
                ? QuizScroll.Visibility == Visibility.Visible
                    ? ("This screen: Questions", "Answer every question, then press Check my answers at the bottom. Each answer is marked ✓ or ✗: a right one says why, a wrong one says where to look again. Change the wrong ones and check again; your answers stay while PLC+ is open. For a machine with a fault, work its buttons and watch which contact never changes.")
                    : ("This screen: Exercise", "What this item asks you to do. A reading has no exercise; a module check lists the items it covers.")
                : ("This screen: Learn", "The lesson. Read it, then try the Example and Exercise tabs if the item has them. A reading is complete when you press I have read this.");
    }

    // ---- The rest --------------------------------------------------------------------------------------

    private void SetStatus(string text) => _vm.Status = text;

    private void Help_Executed(object sender, RoutedEventArgs e) =>
        HelpPanel.Visibility = HelpPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();

    private const string WebHelp = "https://softwareplus.ai/plcplus/help/";

    private void WebHelp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(WebHelp) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(this, $"Windows could not open your web browser. The help is at:\n\n{WebHelp}", "PLC+ — Help on the web",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void CheckForUpdate_Click(object sender, RoutedEventArgs e) => await StoreUpdates.CheckAsync(this);

    /// <summary>
    /// Larger text needs room (UX review 2026-11-05): the columns' smallest widths shrink with the scale, and above 120% the help
    /// panel steps aside (F1 brings it back), so Next ▸ and the right of each page stay on the screen.
    /// </summary>
    private void FitToTextSize()
    {
        var s = _vm.TextScale;
        PathColumn.MinWidth = 220 / s;
        PageColumn.MinWidth = 600 / s;
        if (s > 1.2 && HelpPanel.Visibility == Visibility.Visible)
        {
            HelpPanel.Visibility = Visibility.Collapsed;
            SetStatus($"Text size {s:P0}. The help panel is hidden to make room; F1 shows it.");
        }
    }

    /// <summary>Text size (plan 7.7): Ctrl++ and Ctrl+−, 80% to 200%, kept in the learner's own file.</summary>
    private void TextSize_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var scale = e.Command == TextLarger ? _vm.TextScale + 0.1 : e.Command == TextSmaller ? _vm.TextScale - 0.1 : 1.0;
        _vm.SetTextScale(scale);
        if (HelpPanel.Visibility == Visibility.Visible || _vm.TextScale <= 1.2)
            SetStatus($"Text size {_vm.TextScale:P0}. Ctrl++ larger, Ctrl+− smaller, Ctrl+0 normal; kept for you on any computer.");
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        ExerciseWorkspace.CommitPendingEdit();
        if (_session is not null)
        {
            if (!OfferBackup("Before you close PLC+", leaving: true))
            {
                e.Cancel = true;
                return;
            }
            Lock("", switching: true);
        }
        base.OnClosing(e);
    }
}
