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
using Learning.Core.Notes;
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
                "Reinstall PLC+, or if you copied the program, copy the content folder with it.\n\nDetails for support: " + ex.Message,
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
            else if (NotesPage.IsVisible) AllNotesPanel.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            else if (GlossaryPage.IsVisible) GlossaryView.FocusSearch();
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
        foreach (var ws in new[] { ExampleWorkspace, ExerciseWorkspace })
        {
            ws.StatusChanged += text => { if (ws.IsVisible) SetStatus(text); };
            ws.PopOutRequested = PopOut;
        }
        ExerciseWorkspace.Checked += Exercise_Checked;
        QuizPanel.Graded += Quiz_Graded;
        QuizPanel.FaultMachine = FaultMachine;
        // A wrong answer to a check names the item to read again: the first lesson that teaches the question's outcome.
        QuizPanel.WhereToLook = q => _package.AllItems.FirstOrDefault(i => i.Kind == ItemKind.Lesson && q.Objective is not null
            && i.Objectives.Contains(q.Objective, StringComparer.OrdinalIgnoreCase))?.ToString();

        // My notes (Learning Core plan 3.2): beside an item, and as a page in the path.
        foreach (var panel in new[] { ItemNotes, AllNotesPanel })
        {
            panel.Session = () => _session;
            panel.FindItem = _package.FindItem;
            panel.Changed = NotesChanged;
            panel.Status = SetStatus;
            panel.GoTo = GoToNote;
            panel.PageText = (item, tab) => tab switch
            {
                NoteTabs.Reference => ReferenceMarkdown(item),
                NoteTabs.Laboratory => LabMarkdown(item),
                ReviewTabName => ReviewMarkdown(item),
                _ => item.Learn,
            };
        }
        // Ask the AI (Learning Core plan 3.8): optional, through the shared AI Manager.
        ItemNotes.AiReady = () => PlcAi.Shared.IsReady;
        ItemNotes.AiStatus = () => PlcAi.Shared.Status;
        ItemNotes.AiSettings = AiSettingsDone;
        ItemNotes.AskAi = (item, question, ct) => PlcAi.Shared.AskAsync(PlcAi.TutorInstructions("PLC+"), PlcAi.Prompt(item, item.Learn, question), ct);
        foreach (var (view, tab) in NotedPages)
        {
            view.EnableNotes();
            view.NoteRequested += i => RequestNote(view, tab, i);
            view.NoteMarkClicked += i => ShowNotesOn(view, tab, i);
            view.ReadAloudRequested += i => _ = ReadPage(i);
        }
        LabView.WidgetFactory = w => LessonWidgets.Create(_plc, w);
        ReviewView.WidgetFactory = ReviewWidget;

        // Key terms (Learning Core plan 3.13): marked in every page of the book; a click opens the Glossary.
        foreach (var view in new[] { LearnView, LabView, ReferenceView, ModuleView })
        {
            view.Glossary = _package.Glossary;
            view.TermClicked += ShowGlossaryPage;
        }
        GlossaryView.Load(_package);
        GlossaryView.Back = BackFromGlossary;
        GlossaryView.Open = (item, entry) =>
        {
            _vm.Select(item);
            SetStatus($"{item}: the key term “{entry.Term}” is marked with a dotted underline where it is first used.");
        };

        // The Reader (Learning Core plan 3.15).
        _reader.Reading += i =>
        {
            _readView?.ShowReading(i);
            ShowReaderButtons();
        };
        Closed += (_, _) => _reader.Dispose();

        Loaded += (_, _) => SignInScreen.Show();
    }

    // ---- My notes (Learning Core plan 3.2) --------------------------------------------------------------

    /// <summary>A note was added, changed or deleted: saved in the learner's file, and the path, marks and lists drawn again.</summary>
    private void NotesChanged()
    {
        if (_session is null) return;
        _session.Data.NotesChanged(DateTimeOffset.Now);
        SaveProgress();
        _vm.Refresh();
        RefreshNotes();
        if (NotesPage.Visibility == Visibility.Visible) AllNotesPanel.ShowAll();
    }

    /// <summary>The pages a learner can note, with the tab each is on.</summary>
    private IEnumerable<(MarkdownView View, string Tab)> NotedPages =>
        [(LearnView, NoteTabs.Learn), (LabView, NoteTabs.Laboratory), (ReviewView, ReviewTabName), (ReferenceView, NoteTabs.Reference)];

    /// <summary>The Review tab, as a note's anchor names it (Learning Core has no constant for it yet).</summary>
    private const string ReviewTabName = "review";

    /// <summary>The item's notes: a ✎ after each paragraph that has one, the list on the My notes tab, and the count on the tab.</summary>
    private void RefreshNotes()
    {
        if (_item is not { } item || _session is null) return;
        var book = _session.Data.Notes;
        var placed = new List<NoteLocation>();
        foreach (var (view, tab) in NotedPages)
        {
            var here = book.Place(item.Id, tab, view.BlockSources).Where(p => p.Note.Anchor.Block is not null).ToList();
            view.MarkNotes(here.Where(p => p.BlockIndex is not null).Select(p => (p.BlockIndex!.Value, NotesPanel.Short(p.Note.Text, 200))));
            placed.AddRange(here);
        }
        ItemNotes.ShowItem(item, placed.GroupBy(p => p.Note.Id).ToDictionary(g => g.Key, g => g.First()));
        var count = book.For(item.Id).Count;
        NotesTab.Header = count == 0 ? "My no_tes" : $"My no_tes ({count})";
        System.Windows.Automation.AutomationProperties.SetName(NotesTab, count == 0 ? "My notes" : $"My notes, {count} on this item");
    }

    /// <summary>Add a note to this paragraph (or page), from the lesson's right-click menu: a small window over the lesson.</summary>
    private void RequestNote(MarkdownView view, string tab, int? block)
    {
        if (_item is not { } item || _session is null) return;
        var sources = view.BlockSources;
        var anchor = block is int i && i < sources.Count ? NoteAnchor.OnBlock(item.Id, tab, sources[i]) : NoteAnchor.OnItem(item.Id, tab);
        var page = $"{item.Number}'s {TabWord(tab)} page";
        if (NoteWindow.Ask(this, anchor, page) is not { } text) { SetStatus("No note was added."); return; }
        if (_session.Data.Notes.Add(anchor, text, DateTimeOffset.Now) is null) return;
        NotesChanged();
        SetStatus(anchor.Block is null
            ? $"Note added to {page}. It is on the My notes tab with your other notes on {item.Number}."
            : $"Note added: a ✎ after the paragraph shows it (point at it to read it, click it to open My notes).");
    }

    private static string TabWord(string tab) => tab switch
    {
        NoteTabs.Laboratory => "Laboratory",
        ReviewTabName => "Review",
        NoteTabs.Reference => "Reference",
        NoteTabs.Example => "Example",
        NoteTabs.Exercise => "Exercise",
        _ => "Learn",
    };

    /// <summary>The ✎ after a paragraph was clicked: the My notes tab, with that paragraph's newest note marked.</summary>
    private void ShowNotesOn(MarkdownView view, string tab, int block)
    {
        if (_item is not { } item || _session is null) return;
        var note = _session.Data.Notes.Place(item.Id, tab, view.BlockSources).Where(p => p.BlockIndex == block).Select(p => p.Note)
            .OrderByDescending(n => n.Created).FirstOrDefault();
        ItemTabs.SelectedItem = NotesTab;
        if (note is not null) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => ItemNotes.Highlight(note.Id));
        SetStatus($"Your notes on {item.Number}: the one on that paragraph is marked ▶. Its Show button takes you back to the paragraph.");
    }

    /// <summary>Open a note where it is: its item, its tab, and the paragraph it is on.</summary>
    private void GoToNote(LearnerNote note)
    {
        if (_package.FindItem(note.Anchor.ItemId) is not { } item) return;
        if (_item != item) _vm.Select(item);
        if (_item != item) return;
        var (tab, view) = note.Anchor.Tab switch
        {
            NoteTabs.Learn => (LearnTab, LearnView),
            NoteTabs.Laboratory => (LabTab, LabView),
            ReviewTabName => (ReviewTab, ReviewView),
            NoteTabs.Reference => (ReferenceTab, ReferenceView),
            NoteTabs.Example => (ExampleTab, (MarkdownView?)null),
            NoteTabs.Exercise => (ExerciseTab, null),
            _ => ((TabItem?)null, null),
        };
        // A note on a paragraph goes to the paragraph; any other goes to the My notes tab, marked.
        var at = view is null || _session is null || note.Anchor.Block is null ? null
            : _session.Data.Notes.Place(item.Id, note.Anchor.Tab!, view.BlockSources).FirstOrDefault(p => p.Note.Id == note.Id);
        ItemTabs.SelectedItem = at?.BlockIndex is not null ? tab : note.Anchor.Block is null && tab is not null ? tab : NotesTab;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            ItemNotes.Highlight(note.Id);
            if (view is not null && at?.BlockIndex is int i && ItemTabs.SelectedItem == tab) view.ShowBlock(i);
            SetStatus(at?.Place switch
            {
                NotePlace.Found => $"The paragraph your note is on is selected, with a ✎ after it. The note is on the My notes tab.",
                NotePlace.Changed => $"The lesson has been updated since you wrote this note: the selected paragraph is the one most like the one it was on.",
                NotePlace.Gone => $"The paragraph this note was on is no longer in the lesson; the note stays on {item.Number}'s My notes tab, with the words it was on.",
                _ => ItemTabs.SelectedItem == NotesTab ? $"Your note is marked ▶ on {item.Number}'s My notes tab." : $"Your note is on {item.Number}'s {TabWord(note.Anchor.Tab ?? "")} tab; the My notes tab lists it.",
            });
        });
    }

    /// <summary>Leaving an item (or locking): a note half written, or an edit not saved, is kept rather than lost.</summary>
    private string? KeepNoteDraft()
    {
        if (_session is null) return null;
        var item = ItemNotes.KeepDraft();
        var page = AllNotesPanel.KeepDraft();
        return item is null ? page : page is null ? item : item + " " + page;
    }

    private void ShowNotesPage()
    {
        StopReading();
        ExerciseWorkspace.CommitPendingEdit();
        QuizPanel.Remember();
        _selfTest?.Remember();
        var kept = KeepNoteDraft();
        _item = null;
        _module = null;
        _vm.CurrentItem = null;
        _vm.CurrentModule = null;
        ItemPage.Visibility = ModulePage.Visibility = DashboardPage.Visibility = ReportsPage.Visibility = GlossaryPage.Visibility = Visibility.Collapsed;
        NotesPage.Visibility = Visibility.Visible;
        _vm.Refresh();
        AllNotesPanel.ShowAll();
        ShowScreenHelp();
        SetStatus((kept is null ? "" : kept + " ") + "My notes: every note you have written, newest first. Type in Find in my notes to search; Open goes to a note in its lesson.");
    }

    // ---- Learners (plan 6.1–6.10) ---------------------------------------------------------------------

    /// <summary>A learner's file is open: show their progress, where they left off.</summary>
    private void SignIn(LearnerFile file)
    {
        _lastInput = DateTime.Now;
        var same = _vm.SignIn(file, DateTimeOffset.Now);
        ShowCourseLinks();
        ShowVoice();
        SignInScreen.Visibility = Visibility.Collapsed;
        AppContent.Visibility = Visibility.Visible;
        if (same && (_item is not null || _module is not null || DashboardPage.Visibility == Visibility.Visible))
        {
            // Back after a lock: everything is as it was.
            _vm.CurrentItem = _item;
            // The notes were cleared from the screen when it locked: the same learner gets them back (UX review 2026-11-15).
            if (_item is not null) RefreshNotes();
            if (NotesPage.Visibility == Visibility.Visible) AllNotesPanel.ShowAll();
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
        StopReading();
        _popOut?.Close();   // nothing of the learner's stays on screen
        ExerciseWorkspace.CommitPendingEdit();
        QuizPanel.Remember();
        _selfTest?.Remember();
        KeepNoteDraft();
        ItemNotes.Forget();      // before the lock: it keeps an unasked question for this learner
        AllNotesPanel.Forget();
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
        if (combined is not null) { _vm.Refresh(); RefreshNotes(); ShowCourseLinks(); }
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
            : $"Welcome to PLC+, {name}. {WrittenSoFar()} Start here with module {next.Module.Number}: press the button under its title, or choose any module on the left.");
    }

    /// <summary>Which modules can be worked through now, and which come later, in words ("Modules 1 to 8 and 11 are written; 9, 10 and 12 come later.").</summary>
    private string WrittenSoFar()
    {
        var written = _package.Modules.Where(LearningProgress.Learnable).Select(m => m.Number).ToList();
        var later = _package.Modules.Where(m => !LearningProgress.Learnable(m)).Select(m => m.Number).ToList();
        string Words(List<int> n)
        {
            // Runs of three or more become "1 to 8": 1, 2, 3, …, 8, 11 → "1 to 8 and 11".
            var parts = new List<string>();
            for (var i = 0; i < n.Count;)
            {
                var j = i;
                while (j + 1 < n.Count && n[j + 1] == n[j] + 1) j++;
                if (j - i >= 2) parts.Add($"{n[i]} to {n[j]}");
                else for (var k = i; k <= j; k++) parts.Add($"{n[k]}");
                i = j + 1;
            }
            return parts.Count == 1 ? parts[0] : $"{string.Join(", ", parts.Take(parts.Count - 1))} and {parts[^1]}";
        }
        return later.Count == 0 ? "Every module is written." : $"Modules {Words(written)} are written; {Words(later)} come later.";
    }

    private void PathTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not PathNode node) return;
        if (node.IsDashboard) ShowDashboard();
        else if (node.IsReports) ShowReports();
        else if (node.IsNotes) ShowNotesPage();
        else if (node.IsGlossary) ShowGlossaryPage(null);
        else if (node.Item is { } item) ShowItem(item);
        else ShowModule(node.Module!);
    }

    // ---- My progress (plan 7.2–7.5) ---------------------------------------------------------------------

    private void ShowDashboard()
    {
        StopReading();
        ExerciseWorkspace.CommitPendingEdit();
        QuizPanel.Remember();
        _selfTest?.Remember();
        KeepNoteDraft();
        _item = null;
        _module = null;
        _vm.CurrentItem = null;
        _vm.CurrentModule = null;
        ItemPage.Visibility = ModulePage.Visibility = ReportsPage.Visibility = NotesPage.Visibility = GlossaryPage.Visibility = Visibility.Collapsed;
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
        StopReading();
        ExerciseWorkspace.CommitPendingEdit();
        QuizPanel.Remember();
        _selfTest?.Remember();
        KeepNoteDraft();
        _item = null;
        _module = null;
        _vm.CurrentItem = null;
        _vm.CurrentModule = null;
        ItemPage.Visibility = ModulePage.Visibility = DashboardPage.Visibility = NotesPage.Visibility = GlossaryPage.Visibility = Visibility.Collapsed;
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
        StopReading();
        ExerciseWorkspace.CommitPendingEdit();
        QuizPanel.Remember();
        _selfTest?.Remember();
        KeepNoteDraft();
        _module = m;
        _item = null;
        _vm.CurrentItem = null;
        _vm.CurrentModule = m;
        ShowScreenHelp();
        ItemPage.Visibility = DashboardPage.Visibility = ReportsPage.Visibility = NotesPage.Visibility = GlossaryPage.Visibility = Visibility.Collapsed;
        ModulePage.Visibility = Visibility.Visible;

        var p = _progress.Of(m);
        ModuleTitle.Text = $"Module {m.Number} · {m.Title}";
        ModuleStatusText.Text = m.Status == ModuleStatus.ComingLater
            ? "Coming in a later version of PLC+. It is in the path so you can see where it fits."
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
        StopReading();
        // The item's own key terms are marked on its pages; an item that lists none marks every term in the glossary.
        var ownTerms = item.Terms.Select(_package.Glossary.Find).OfType<GlossaryEntry>().ToHashSet();
        foreach (var view in new[] { LearnView, LabView, ReferenceView }) view.MarkOnly = ownTerms.Count == 0 ? null : ownTerms.Contains;
        ExerciseWorkspace.CommitPendingEdit();
        QuizPanel.Remember();
        _selfTest?.Remember();
        var keptNote = _item != item ? KeepNoteDraft() : null;
        var tabBefore = _item is not null && _item != item ? ItemTabs.SelectedItem : null;
        _item = item;
        _module = item.Module;
        _vm.CurrentItem = item;
        _vm.CurrentModule = item.Module;
        ModulePage.Visibility = DashboardPage.Visibility = ReportsPage.Visibility = NotesPage.Visibility = GlossaryPage.Visibility = Visibility.Collapsed;
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
            : $"# {item.Title}\n\n*This page is still to be written. Module {item.Module.Number}'s learning outcomes are on the module's first page.*";
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
        ExerciseTab.Header = exercise is null && item.Quiz is not null ? "Exerc_ise (questions)"
            : exercise is not null || item.Kind == ItemKind.Assess ? "Exerc_ise" : "Exerc_ise (none)";
        ExerciseTab.ToolTip = exercise is not null ? "Write your own program and check it (Ctrl+3)"
            : item.Quiz is not null ? $"{item.Quiz.Questions.Count} questions to answer (Ctrl+3)"
            : item.Kind == ItemKind.Assess ? "What this module check covers, and where you are (Ctrl+3)"
            : "This item has no exercise: it is complete when you have read it (Ctrl+3)";

        // Reference
        ReferenceView.Markdown = ReferenceMarkdown(item);

        // Laboratory
        LabView.Markdown = LabMarkdown(item);
        LabTab.Header = item.Labs.Count == 0 ? "Lab_oratory (none)" : "Lab_oratory";
        LabTab.ToolTip = item.Labs.Count == 0 ? "No lab exercise for this item yet; the tab says how one can be added (Ctrl+4)" : "The lab exercise you do live, on the real equipment in the lab (Ctrl+4)";

        // Review, and In this item at the top of Learn (Learning Core plan 3.11–3.12)
        ReviewView.Markdown = ReviewMarkdown(item);
        ReviewTab.Header = item.Takeaways.Count == 0 && item.Terms.Count == 0 && item.SelfTest is null ? "Revie_w (none)" : "Revie_w";
        ShowInThisItem(item);

        // My notes: the ✎ after noted paragraphs, the My notes tab.
        RefreshNotes();

        ShowItemStatus();
        // Moving on from the My notes tab stays on it (reading notes item by item); from anywhere else, the lesson first.
        ItemTabs.SelectedItem = tabBefore == NotesTab ? NotesTab : LearnTab;
        ShowScreenHelp();
        SetStatus((keptNote is null ? "" : keptNote + " ") + $"{item}: {LearnFooterText.Text}" + (missing.Count > 0
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
        if (unwritten && item.Kind == ItemKind.Lesson) LearnFooterText.Text = $"This page is still to be written. {WrittenSoFar()}";
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

    /// <summary>The Laboratory tab (Learning Core plan 3.9): the item's lab exercises, or what the tab is for.</summary>
    private string LabMarkdown(LearningItem item)
    {
        var labs = item.Labs.Select(l => _package.Labs.TryGetValue(l, out var lab) ? lab : null).OfType<LabPage>().ToList();
        if (labs.Count == 0)
            return "# Laboratory\n\n*No lab exercise here yet.*\n\nLaboratory exercises can be added here: by the book's author, as a lab " +
                   "page in the book, or by your instructor for your class, in the course materials they send (a course pack, made in PLC+ or " +
                   "in CourseBuilder+). A lab exercise is one you do live, on the real PLC and equipment in the lab: what to bring, what to do " +
                   "and what to show your lab instructor. Items 5.2 and 6.1 have an example.";
        var md = new StringBuilder();
        foreach (var lab in labs)
        {
            if (md.Length > 0) md.Append("\n\n---\n\n");
            md.Append("# Lab: ").Append(lab.Title).Append("\n\n");
            var facts = new List<string>();
            if (lab.Minutes > 0) facts.Add($"**Time in the lab:** about {lab.Minutes} minutes");
            if (lab.Equipment is { Count: > 0 } kit) facts.Add("**Equipment:** " + string.Join(", ", kit));
            if (facts.Count > 0) md.Append(string.Join(" · ", facts)).Append("\n\n");
            md.Append(lab.Body.Trim());
        }
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
        QuizPanel.ShuffleSeed = _session?.Data.Profile.ProfileId ?? "";
        QuizPanel.Load(quiz, item.Kind == ItemKind.Assess ? $"Module {item.Module.Number} check: the questions" : "Questions", intro);
    }

    /// <summary>A "find the fault" question's machine: the working program, with the question's wiring fault.</summary>
    private UIElement? FaultMachine(Question q)
    {
        if (_plc.Find(q.Machine) is not { } challenge || Plc.Scenes.WiringFault.Parse(q.Fault) is not { } fault) return null;
        var ws = new MachineWorkspace { Height = 640 };
        ws.StatusChanged += text => { if (ws.IsVisible) SetStatus(text); };
        ws.PopOutRequested = PopOut;
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
        StopReading("Reading stopped because you changed tabs. Press 🔊 Read aloud to read this page.");
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
            SetStatus((ExerciseWorkspace.OpeningMessage ?? "My program: click an element and type its tag in the Tag box, then use the tools above the rungs. Check my program tests it.") + hid);
        else if (ItemTabs.SelectedItem == ExerciseTab && QuizScroll.Visibility == Visibility.Visible)
            SetStatus("Answer the questions, then press Check my answers at the bottom." + hid);
        else if (ItemTabs.SelectedItem == ExampleTab && ExampleWorkspace.Visibility == Visibility.Visible)
            SetStatus("The working program, running: work the machine and watch the rungs. Choose A common mistake in the Program list to see it go wrong. Examples do not count toward your progress." + hid);
    }

    /// <summary>The machine screen needs about this much height to show the rungs; a shorter window scrolls the tab.</summary>
    private const double MachineMinHeight = 760;

    private void MachineScroll_SizeChanged(object sender, SizeChangedEventArgs? e)
    {
        if (sender is ScrollViewer { Content: MachineWorkspace ws } scroll)
            ws.Height = Math.Max(MachineMinHeight, scroll.ActualHeight - ws.Margin.Top - ws.Margin.Bottom);
    }

    private Window? _popOut;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EnableWindow(IntPtr hWnd, bool enable);

    /// <summary>
    /// Pop out (Ron, plan 10.7): the same workspace (its program, its machine, its tests) moves into a window of its own, which
    /// the learner can make as big as they like. It is modal: the main window is disabled, as ShowDialog would do, so it cannot
    /// be used or focused until the pop-out closes. It is not ShowDialog itself, because a dialog stops the auto-lock and the
    /// time on task (the idle timer waits while a dialog is open); the pop-out is the learner working, so both carry on.
    /// Closing it (or Put it back, or Esc) puts the workspace back as it is.
    /// </summary>
    private void PopOut(MachineWorkspace ws)
    {
        if (_popOut is not null) return;
        var home = ws.Parent;
        double height = ws.Height;
        switch (home)
        {
            case ScrollViewer s: s.Content = null; break;
            case Border b: b.Child = null; break;
            default: return;
        }

        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 6, 16, 6) };
        System.Windows.Automation.AutomationProperties.SetLiveSetting(status, System.Windows.Automation.AutomationLiveSetting.Polite);
        void Show(string text) => status.Text = text;
        ws.StatusChanged += Show;
        status.Text = "The program and the machine, in a window of their own. Make it as big as you like. The main window waits until you come back: " +
                      "press Put it back (or close this window, or press Esc) to return to the lesson, the menus and Next.";

        ws.Margin = new Thickness(16, 4, 16, 8);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, Content = ws };
        // The learner sizes this window, so the workspace fills it down to a smaller floor than in the main window.
        void Fit(object? s, SizeChangedEventArgs? e) => ws.Height = Math.Max(520, scroll.ActualHeight - ws.Margin.Top - ws.Margin.Bottom);
        scroll.SizeChanged += Fit;
        var bar = new Border { Child = status, BorderBrush = (Brush)FindResource("LineBrush"), BorderThickness = new Thickness(0, 1, 0, 0), Background = (Brush)FindResource("PanelBrush") };
        DockPanel.SetDock(bar, Dock.Bottom);
        var dock = new DockPanel { LastChildFill = true, Background = (Brush)FindResource("PageBrush") };
        dock.Children.Add(bar);
        dock.Children.Add(scroll);
        dock.LayoutTransform = new ScaleTransform(_vm.TextScale, _vm.TextScale);

        var item = _item;
        var win = new Window
        {
            Title = $"{(item is null ? "" : $"{item.Number} {item.Title} — ")}{ws.TitleText.Text} — PLC+",
            Owner = this, Content = dock, ShowInTaskbar = false, ResizeMode = ResizeMode.CanResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = Math.Max(900, ActualWidth * 0.96), Height = Math.Max(640, ActualHeight * 0.96),
            MinWidth = 640, MinHeight = 480, Icon = Icon,
        };
        if (WindowState == WindowState.Maximized) win.WindowState = WindowState.Maximized;
        win.PreviewMouseMove += (_, _) => _lastInput = DateTime.Now;   // moving the mouse here is using PLC+ too (auto-lock)
        // Esc closes it, unless a box or a menu used the key first.
        win.KeyDown += (_, e) => { if (e.Key == Key.Escape && !e.Handled) { e.Handled = true; win.Close(); } };
        win.Loaded += (_, _) => ws.PopOutButton.Focus();

        var me = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        win.Closing += (_, _) =>
        {
            ws.CommitPendingEdit();      // a half-typed tag or PT is kept, as leaving the box would keep it
            EnableWindow(me, true);      // before the pop-out goes, so Windows hands the focus back to the main window
        };
        win.Closed += (_, _) =>
        {
            _popOut = null;
            ws.StatusChanged -= Show;
            ws.PoppedOut = false;
            scroll.SizeChanged -= Fit;
            scroll.Content = null;
            ws.Margin = new Thickness(0);
            ws.Height = height;
            switch (home)
            {
                case ScrollViewer s: s.Content = ws; MachineScroll_SizeChanged(s, null); break;
                case Border b: b.Child = ws; break;
            }
            Activate();
            ws.PopOutButton.Focus();
        };

        ws.PoppedOut = true;
        _popOut = win;
        EnableWindow(me, false);         // modal: the main window cannot be used or focused while the pop-out is open
        win.Show();
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
        (HelpScreenTitle.Text, HelpScreenText.Text) = GlossaryPage.Visibility == Visibility.Visible
            ? ("This screen: Glossary", "The book's key terms, A to Z: what each means, its other names, and the items that use it. Type in Find a term to look one up; press an item number under Used in to open that item. In a lesson, the item's key terms have a dotted underline the first time they are used on a page: point at it to read its meaning, click it to come here.")
            : NotesPage.Visibility == Visibility.Visible
            ? ("This screen: My notes", "Every note you have written on the lessons, newest first. Type words in Find in my notes to search your notes and the paragraphs they are on. Open goes to the note in its lesson; Edit changes it; Delete asks first. Only you see your notes: they are kept in your progress file, locked with your PIN and backed up with it, and never go in a report.")
            : ReportsPage.Visibility == Visibility.Visible
            ? ("This screen: Hand in a report", "1. Choose a module check, or everything so far. 2. Check what the report will say; My details for reports adds your student ID, course and class code to the cover. 3. Save the PDF report, and hand in that file as it is, the way your instructor asks; do not save it again in another program. The result line is a one-line summary of the report your instructor can check: copy it if they ask for it. A report made before the check is complete is marked Incomplete. Every report you make is listed at the bottom.")
            : DashboardPage.Visibility == Visibility.Visible
            ? ("This screen: My progress", "Your progress through the whole path. ★ mastered, ✓ complete, ↺ review suggested, ◐ in progress, ○ not started. Review suggested lists what to read again. At a module check, make a report to hand in: Hand in a report, the next row on the left. Time counts while you use PLC+.")
            : _item is null
            ? ("This screen: a module", "A module's first page: what to finish first, what you will be able to do, its topics and its items. Press the button under the title to start, or choose an item on the left.")
            : ItemTabs.SelectedItem == ExampleTab && ExampleWorkspace.Visibility == Visibility.Visible
                ? ("This screen: Example", "A working program, on a machine like the exercise's (not the same one). Work the machine and watch power flow along the rungs. Choose A common mistake in the Program list to see it go wrong; Check this program runs the task's tests on it. Pop out gives it a window of its own. Examples do not count toward your progress.")
            : ItemTabs.SelectedItem == ExerciseTab && ExerciseWorkspace.Visibility == Visibility.Visible
                ? ("This screen: Exercise", "Write your own program. Click a part and type its tag in the Tag box, then press Enter. The tools above the rungs add contacts (key O), coils (Q), branches (B) and boxes (I). Check my program runs the tests: when they all pass, the item is complete. In a find-and-fix exercise My program starts with a fault: work the machine, find it, fix it; Start again ▾ gives the program back. Pop out gives it a window of its own.")
            : ItemTabs.SelectedItem == ReferenceTab
                ? ("This screen: Reference", "The symbols and rules this item uses, with the Allen-Bradley names, to look up while you work. Right-click a paragraph to keep a note on it.")
            : ItemTabs.SelectedItem == ReviewTab
                ? ("This screen: Review", "What to remember from this item, like the end of a textbook chapter: the key takeaways, the key terms and what they mean, flashcards to practise the terms (click a card to turn it over), and a self-test. The self-test counts for nothing: each answer shows whether it is right and why. Read it before a module check. 🔊 Read aloud reads it to you.")
            : ItemTabs.SelectedItem == LabTab
                ? ("This screen: Laboratory", "The lab exercise for this item: what you do live, on the real PLC and equipment in the lab, what to bring and what to show your lab instructor. Read it before the lab session. Not every item has one; your instructor may add more for your class. Right-click a paragraph to keep a note on it.")
            : ItemTabs.SelectedItem == NotesTab
                ? ("This screen: My notes", "Your own notes on this item. Write a note on the item as a whole in Write a note. To note one paragraph, right-click it in the lesson: a ✎ after the paragraph shows it, and clicking the ✎ brings you here. Ask the AI (optional) answers a question about this item from its lesson; Save to my notes keeps the answer, marked as the AI's, and Discard throws it away. Only you see your notes; they never go in a report. Go ▸ My notes lists every note, with a search.")
            : ItemTabs.SelectedItem == ExerciseTab
                ? QuizScroll.Visibility == Visibility.Visible
                    ? ("This screen: Questions", "Answer every question, then press Check my answers at the bottom. Each answer is marked ✓ or ✗. In practice questions every answer says why, and a wrong one shows the right answer. In a module check, a wrong one says where to read again. Change the wrong ones and check again; your answers stay while PLC+ is open. For a machine with a fault, work its buttons and watch which contact or device never changes; right-click a contact or coil to force it.")
                    : ("This screen: Exercise", "What this item asks you to do. A reading has no exercise; a module check lists the items it covers.")
                : ("This screen: Learn", "The lesson. Read it, then try the Example and Exercise tabs if the item has them. A reading is complete when you press I have read this. To keep a note on a paragraph, right-click it and choose Add a note to this paragraph: a ✎ after it shows the note. The My notes tab has your notes on this item, and Ask the AI. In this item, at the top, says what you will be able to do and lists the key terms; a key term in the lesson has a dotted underline (point at it for its meaning). 🔊 Read aloud reads the page to you, or right-click a paragraph ▸ Read aloud from here.");
    }

    // ---- The rest --------------------------------------------------------------------------------------

    private void SetStatus(string text) => _vm.Status = text;

    private void Help_Executed(object sender, RoutedEventArgs e) =>
        HelpPanel.Visibility = HelpPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();

    private void WebHelp_Click(object sender, RoutedEventArgs e) => WebLinks.Open(this, WebLinks.Help, "Help on the web");

    // ---- Review, In this item, Glossary (Learning Core plan 3.11–3.13) ------------------------------------

    /// <summary>The Review tab's page: takeaways, key terms, then the flashcards and the self-test as widgets.</summary>
    private string ReviewMarkdown(LearningItem item)
    {
        var md = new StringBuilder($"# Review: {item.Title}\n\n");
        md.Append("## Key takeaways\n\n");
        if (item.Takeaways.Count == 0) md.Append("*The key takeaways for this item are still to be written.*\n\n");
        foreach (var t in item.Takeaways) md.Append("- ").Append(t).Append('\n');
        var terms = item.Terms.Select(_package.Glossary.Find).OfType<GlossaryEntry>().ToList();
        md.Append("\n## Key terms\n\n");
        if (terms.Count == 0) md.Append("*No key terms are listed for this item yet. Go ▸ Glossary has every term in the book.*\n\n");
        foreach (var t in terms) md.Append("**").Append(t.Term).Append("**: ").Append(t.Definition).Append("\n\n");
        if (terms.Count > 0) md.Append("## Flashcards\n\nPractise the key terms: say what each means, then turn the card over to check.\n\n[[flashcards: ").Append(item.Id).Append("]]\n\n");
        md.Append("## Self-test\n\n");
        if (item.SelfTest is null) md.Append("*There is no self-test for this item yet.*");
        else md.Append("Questions to check yourself before you move on. They count for nothing: each answer shows whether it is right, and why.\n\n[[self-test: ").Append(item.Id).Append("]]");
        return md.ToString();
    }

    /// <summary>The self-test on the Review tab, so answers not yet checked are kept like the Exercise's.</summary>
    private QuizView? _selfTest;

    private UIElement? ReviewWidget(WidgetTag w)
    {
        if (_package.FindItem(w.Argument) is not { } item) return null;
        if (w.Name == "flashcards") return new FlashcardsView(item.Terms.Select(_package.Glossary.Find).OfType<GlossaryEntry>());
        if (w.Name == "self-test" && item.SelfTest is { } quiz)
        {
            _selfTest?.Remember();
            var view = new QuizView { ShuffleSeed = _session?.Data.Profile.ProfileId ?? "", SelfTest = true };
            _selfTest = view;
            view.Load(quiz, quiz.Title, []);
            view.Graded += r => SetStatus(r.Passed
                ? $"Self-test: {r.Score:0.#} of {r.MaxScore:0.#}. Well done; it counts for nothing, so move on when you are ready."
                : $"Self-test: {r.Score:0.#} of {r.MaxScore:0.#}. Each answer says why; read those parts of the lesson again, then try again.");
            return view;
        }
        return null;
    }

    /// <summary>In this item (Learning Core plan 3.12): the objectives, the key terms and the time, at the top of the lesson.</summary>
    private void ShowInThisItem(LearningItem item)
    {
        InThisItemPanel.Children.Clear();
        var objectives = item.Objectives.Select(o => item.Module.Outcomes.FirstOrDefault(x => x.Id.Equals(o, StringComparison.OrdinalIgnoreCase))?.Text).OfType<string>().ToList();
        var terms = item.Terms.Select(_package.Glossary.Find).OfType<GlossaryEntry>().ToList();
        if (objectives.Count == 0 && terms.Count == 0 && item.Minutes == 0) { InThisItemBand.Visibility = Visibility.Collapsed; return; }
        InThisItemBand.Visibility = Visibility.Visible;
        var head = new TextBlock { Text = item.Minutes > 0 ? $"In this item · about {item.Minutes} minutes" : "In this item", FontWeight = FontWeights.SemiBold };
        System.Windows.Automation.AutomationProperties.SetHeadingLevel(head, System.Windows.Automation.AutomationHeadingLevel.Level2);
        InThisItemPanel.Children.Add(head);
        foreach (var o in objectives)
            InThisItemPanel.Children.Add(new TextBlock { Text = "You will be able to: " + o, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
        if (terms.Count == 0) return;
        var row = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        row.Children.Add(new TextBlock { Text = "Key terms:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        foreach (var t in terms)
        {
            var b = new Button
            {
                Content = t.Term, Padding = new Thickness(8, 0, 8, 1), Margin = new Thickness(0, 0, 4, 2),
                ToolTip = $"{t.Term}: {t.Definition}{Environment.NewLine}Click to see it in the Glossary.",
            };
            System.Windows.Automation.AutomationProperties.SetName(b, $"Key term {t.Term}: {t.Definition}");
            var entry = t;
            b.Click += (_, _) => ShowGlossaryPage(entry);
            row.Children.Add(b);
        }
        InThisItemPanel.Children.Add(row);
    }

    /// <summary>The Glossary page, at one term (from a key term in a lesson) or at the top.</summary>
    private void ShowGlossaryPage(GlossaryEntry? entry)
    {
        if (entry is not null && !_vm.Glossary.IsSelected)
        {
            _glossaryTerm = entry;
            _glossaryFrom = _item is { } from ? (from, ItemTabs.SelectedItem as TabItem) : null;
            _vm.Glossary.IsSelected = true;   // the path's selection opens the page (and remembers where the learner was)
            return;
        }
        StopReading();
        ExerciseWorkspace.CommitPendingEdit();
        QuizPanel.Remember();
        _selfTest?.Remember();
        var kept = KeepNoteDraft();
        _item = null;
        _module = null;
        _vm.CurrentItem = null;
        _vm.CurrentModule = null;
        ItemPage.Visibility = ModulePage.Visibility = DashboardPage.Visibility = ReportsPage.Visibility = NotesPage.Visibility = Visibility.Collapsed;
        GlossaryPage.Visibility = Visibility.Visible;
        _vm.Refresh();
        var term = entry ?? _glossaryTerm;
        _glossaryTerm = null;
        GlossaryView.SetBack(_glossaryFrom is var (fromItem, fromTab) ? $"◂ Back to {fromItem.Number} ({(fromTab?.Header as string ?? "Learn").Replace("_", "", StringComparison.Ordinal)})" : null);
        if (term is not null) GlossaryView.ShowTerm(term);
        ShowScreenHelp();
        SetStatus((kept is null ? "" : kept + " ") + (term is not null
            ? $"Glossary: “{term.Term}” is marked. Press an item number under Used in to open that item" + (_glossaryFrom is null ? "." : ", or ◂ Back to go back where you were.")
            : "Glossary: every key term, A to Z. Type in Find a term to look one up."));
    }

    private GlossaryEntry? _glossaryTerm;
    /// <summary>The item and tab the learner left for the Glossary, to go back to.</summary>
    private (LearningItem Item, TabItem? Tab)? _glossaryFrom;

    private void BackFromGlossary()
    {
        if (_glossaryFrom is not var (item, tab)) return;
        _glossaryFrom = null;
        _vm.Select(item);
        if (tab is not null && _item == item) ItemTabs.SelectedItem = tab;
        SetStatus($"Back on {item}.");
    }

    // ---- The Reader (Learning Core plan 3.15) -----------------------------------------------------------

    public static readonly RoutedUICommand ReadAloud = new("Read aloud", nameof(ReadAloud), typeof(MainWindow));

    private readonly Reader _reader = new();
    private int _readRun;
    /// <summary>Why reading stopped, for the status line (a tab change, another page).</summary>
    private string? _stopReason;
    private MarkdownView? _readView;

    /// <summary>The page on the tab showing, if it is one the Reader reads.</summary>
    private MarkdownView? ReadablePage =>
        ItemTabs.SelectedItem == LearnTab ? LearnView : ItemTabs.SelectedItem == LabTab ? LabView
        : ItemTabs.SelectedItem == ReviewTab ? ReviewView : ItemTabs.SelectedItem == ReferenceTab ? ReferenceView : null;

    private void ReadAloud_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_reader.IsReading && !_reader.IsPaused) { _reader.Pause(); ShowReaderButtons(); SetStatus("Paused. Press ▶ Go on to carry on, or ■ Stop."); }
        else if (_reader.IsReading) { _reader.Resume(); ShowReaderButtons(); SetStatus("Reading aloud."); }
        else _ = ReadPage(null);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _reader.IsReading && Keyboard.FocusedElement is not TextBox)
        {
            StopReading("Stopped reading.");
            e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }

    private void StopReading_Click(object sender, RoutedEventArgs e)
    {
        StopReading("Stopped reading.");
    }

    private void StopReading(string? why = null)
    {
        if (!_reader.IsReading) return;
        _stopReason = why ?? "Reading stopped because you left the page. Press 🔊 Read aloud to read again.";
        _reader.Stop();
        _readView?.ShowReading(null);
        _readView = null;
        ShowReaderButtons();
    }

    /// <summary>The voice line under the reading buttons: which voice reads, and a link to change it.</summary>
    private void ShowVoice()
    {
        var (voice, rate) = ReaderSettingsWindow.Chosen(_session?.Data);
        VoiceName.Text = voice.Name + (Math.Abs(rate - 1.0) > 0.01 ? $", {rate:0.##}× speed" : "");
    }

    private void ShowReaderButtons()
    {
        ReadAloudButton.Content = !_reader.IsReading ? "🔊 Read aloud" : _reader.IsPaused ? "▶ Go on" : "⏸ Pause";
        ReadAloudButton.ToolTip = !_reader.IsReading ? "Reads this page aloud, the paragraph being read marked in amber (Ctrl+Shift+A)"
            : _reader.IsPaused ? "Goes on reading from where it paused (Ctrl+Shift+A)" : "Pauses reading (Ctrl+Shift+A); ■ Stop or Esc stops";
        StopReadingButton.Visibility = _reader.IsReading ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Reads the page showing aloud, from a block or the start.</summary>
    private async Task ReadPage(int? from)
    {
        if (_item is null || _session is null) return;
        if (ReadablePage is not { } view)
        {
            SetStatus("The Reader reads the Learn, Laboratory, Review and Reference tabs. Open one of them, then press 🔊 Read aloud.");
            return;
        }
        var blocks = view.ReadableBlocks;
        var (voice, rate) = ReaderSettingsWindow.Chosen(_session.Data);
        if (voice.Google)
        {
            var characters = blocks.Skip(from ?? 0).Sum(b => b.Length);
            var cost = PlcAi.Shared.EstimateSpeech(voice.Id, characters);
            if (!ChoiceWindow.Ask(this, "PLC+ — Read aloud", "Read this page with a Google voice?",
                    $"{characters:N0} characters with {voice.Name}" + (cost is { } c ? $": about ${c:0.000}, charged to the Google account in AI settings." : ".") +
                    " A part already heard in this session costs nothing again. A Windows voice is free: View ▸ Reader voice and speed….",
                    "Read it", "Reads the page aloud", "Not now", "Nothing is read or charged"))
            {
                SetStatus("Nothing was read or charged.");
                return;
            }
        }
        StopReading();
        _readView = view;
        ShowReaderButtons();
        SetStatus($"Reading aloud with {voice.Name}. The paragraph being read has an amber background. ⏸ Pause or ■ Stop above the tabs (Ctrl+Shift+A pauses).");
        var run = ++_readRun;
        string? problem;
        try
        {
            problem = await _reader.ReadAsync(blocks, from ?? 0, voice, rate);
        }
        catch (Exception ex)
        {
            problem = "The page could not be read aloud: " + ex.Message + " Choose another voice in View ▸ Reader voice and speed….";
        }
        if (run != _readRun) return; // a newer reading took over
        view.ShowReading(null);
        var stoppedByLearner = _readView is null;
        _readView = null;
        ShowReaderButtons();
        SetStatus(problem ?? (stoppedByLearner ? _stopReason ?? "Stopped reading." : "Finished reading the page."));
        _stopReason = null;
    }

    private async void ReaderSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        SetStatus("Finding the voices…");
        bool saved;
        try
        {
            saved = await ReaderSettingsWindow.EditAsync(this, _session.Data);
        }
        catch (Exception ex)
        {
            SetStatus("The voices could not be listed: " + ex.Message + " The Windows voice keeps reading.");
            return;
        }
        if (saved)
        {
            SaveProgress();
            StopReading();
            var (voice, rate) = ReaderSettingsWindow.Chosen(_session.Data);
            ShowVoice();
            SetStatus($"The Reader will use {voice.Name} at {rate:0.##}× speed. Press 🔊 Read aloud on a page.");
        }
        else SetStatus("The Reader's voice and speed are as they were.");
    }

    // ---- My course's links (Learning Core plan 3.10) and the AI (plan 3.8) --------------------------------

    /// <summary>The buttons at the top of the path: the course's text and the LMS when set, and a way to set them.</summary>
    private void ShowCourseLinks()
    {
        var links = _session?.Data.Course;
        var named = string.IsNullOrWhiteSpace(links?.Name) ? "your course" : links!.Name!;
        CourseTextButton.Visibility = links?.TextUrl is null ? Visibility.Collapsed : Visibility.Visible;
        CourseLmsButton.Visibility = links?.LmsUrl is null ? Visibility.Collapsed : Visibility.Visible;
        CourseTextButton.ToolTip = $"Opens the website of {named} in your web browser: {links?.TextUrl}";
        CourseLmsButton.ToolTip = $"Opens {named} in the learning management system, in your web browser: {links?.LmsUrl}";
        var any = links is not null && (links.TextUrl is not null || links.LmsUrl is not null);
        CourseLinksButton.Content = any ? "Change links…" : "My course's links…";
        System.Windows.Automation.AutomationProperties.SetName(CourseLinksButton, any ? "Change my course's links" : "Add my course's links");
    }

    private void CourseLinks_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        if (CourseLinksWindow.Edit(this, _session.Data.Course) is not { } links) { SetStatus("Your course's links are as they were."); return; }
        _session.Data.Course = links.IsEmpty ? null : links;
        _session.Data.NotesChanged(DateTimeOffset.Now);
        SaveProgress();
        ShowCourseLinks();
        SetStatus(links.IsEmpty ? "Your course's links are cleared."
            : links.TextUrl is null && links.LmsUrl is null ? "Your course's name is kept. Add an address to get a button that opens it."
            : "Your course's links are kept: the buttons at the top of the learning path open them.");
    }

    private void CourseText_Click(object sender, RoutedEventArgs e)
    {
        if (_session?.Data.Course?.TextUrl is { } url) WebLinks.Open(this, url, "Your course's website");
        else CourseLinks_Click(sender, e);
    }

    private void CourseLms_Click(object sender, RoutedEventArgs e)
    {
        if (_session?.Data.Course?.LmsUrl is { } url) WebLinks.Open(this, url, "Your course in the LMS");
        else CourseLinks_Click(sender, e);
    }

    private void AiMenu_Opened(object sender, RoutedEventArgs e)
    {
        AskAiItem.IsEnabled = _item is not null;
        AskAiItem.ToolTip = _item is null
            ? "Open an item in the learning path first: the AI answers questions about the item you are reading"
            : "Opens this item's My notes tab with the cursor in Ask the AI: ask a question about the topic, then keep the answer in My notes or discard it";
    }

    private void AskAi_Click(object sender, RoutedEventArgs e)
    {
        if (_item is null)
        {
            SetStatus("Choose an item in the learning path first: the AI answers questions about the item you are reading.");
            return;
        }
        ItemTabs.SelectedItem = NotesTab;
        ItemNotes.FocusQuestion();
        SetStatus(PlcAi.Shared.IsReady ? "Type your question in Ask the AI and press Enter." : "The AI is not set up yet: press AI settings… in Ask the AI. Everything else works without it.");
    }

    private void AiSettings_Click(object sender, RoutedEventArgs e) => AiSettingsDone();

    /// <summary>AI settings, then a word on what changed.</summary>
    private void AiSettingsDone()
    {
        var saved = PlcAi.Shared.ShowSettings(this);
        ItemNotes.AiSettingsChanged();
        SetStatus(!saved ? "AI settings were not changed."
            : PlcAi.Shared.IsReady ? "The AI is set up. Ask it on any item's My notes tab (AI ▸ Ask the AI about this item)."
            : "AI settings were saved, but the AI is not ready yet: it needs a key. AI ▸ AI settings… again to add one.");
    }

    private void AiUsage_Click(object sender, RoutedEventArgs e) => PlcAi.Shared.ShowUsage(this);

    private void TestingGuide_Click(object sender, RoutedEventArgs e) => WebLinks.Open(this, WebLinks.Testing, "The testing guide");

    private void Link_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        WebLinks.Open(this, e.Uri.AbsoluteUri, "The page");
        e.Handled = true;
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
