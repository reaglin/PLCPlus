using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Learning.Core;
using Learning.Core.Content;
using Learning.Core.Progress;

namespace Learning.Shell;

/// <summary>
/// The program's window in the family layout (Statistle, BMPFast): the learning path as a tree on the
/// left (modules, then their items), and on the right a module's first page or an item with its four
/// tabs: Learn, Example, Exercise and Reference (Ron, 2026-11-04). Code-behind for now; plan 7.1 moves
/// it to MVVM when the learners and reports arrive.
/// </summary>
public partial class MainWindow : Window
{
    public static readonly RoutedUICommand ShowTab = new("Show tab", nameof(ShowTab), typeof(MainWindow));
    public static readonly RoutedUICommand NextItem = new("Next item", nameof(NextItem), typeof(MainWindow));
    public static readonly RoutedUICommand PreviousItem = new("Previous item", nameof(PreviousItem), typeof(MainWindow));

    // PLCPLUS_PROGRESS points a test run at a file of its own, so it does not change the tester's progress.
    private static readonly string DefaultProgressFile = Environment.GetEnvironmentVariable("PLCPLUS_PROGRESS") is { Length: > 0 } test ? test : Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PLCPlus", "progress", "preview.json");

    /// <summary>Where progress is saved: the usual file, or a separate one if the usual file could not be read (so it is never overwritten).</summary>
    private readonly string _progressFile = DefaultProgressFile;

    private readonly ContentPackage _package = null!;
    private readonly PlcContent _plc = null!;
    private readonly LearningProgress _progress = new();
    private readonly ObservableCollection<PathNode> _roots = [];
    private readonly List<LearningItem> _order = [];
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

        string? progressProblem = null;
        try
        {
            _progress = LearningProgress.Load(DefaultProgressFile, out progressProblem);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leave the file alone: this session's progress goes to a file of its own.
            _progressFile = Path.ChangeExtension(DefaultProgressFile, ".after-error.json");
            progressProblem = $"PLC+ could not read your progress ({ex.Message}). It may be open in another copy of PLC+. " +
                              $"Your progress file is left as it is; this session's progress is kept in {_progressFile}. Close other copies of PLC+ and start it again to carry on with your own.";
        }

        foreach (var view in new[] { LearnView, ReferenceView, ModuleView, ExerciseNone })
            view.WidgetFactory = w => LessonWidgets.Create(_plc, w);
        // A workspace speaks only while its tab is showing: a hidden one is loading, not answering the learner.
        foreach (var ws in new[] { ExampleWorkspace, ExerciseWorkspace }) ws.StatusChanged += text => { if (ws.IsVisible) SetStatus(text); };
        ExerciseWorkspace.Checked += Exercise_Checked;
        QuizPanel.Graded += Quiz_Graded;
        QuizPanel.FaultMachine = FaultMachine;
        // A wrong answer to a check names the item to read again: the first lesson that teaches the question's outcome.
        QuizPanel.WhereToLook = q => _package.AllItems.FirstOrDefault(i => i.Kind == ItemKind.Lesson && q.Objective is not null
            && i.Objectives.Contains(q.Objective, StringComparer.OrdinalIgnoreCase))?.ToString();

        _order.AddRange(_package.AllItems);
        BuildTree();
        PathTree.ItemsSource = _roots;

        Loaded += (_, _) =>
        {
            if (progressProblem is not null)
                MessageBox.Show(this, progressProblem, "PLC+ — your progress", MessageBoxButton.OK, MessageBoxImage.Warning);

            var next = _progress.Continue(_package);
            if (next is not null && _progress.Items.Count > 0)
            {
                Select(next); // a returning learner goes straight back to work
                // After the item has opened (it sets its own status line first).
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                    () => SetStatus($"Welcome back. Here is where you left off: {next}. {LearnFooterText.Text}"));
                return;
            }
            var start = _roots.FirstOrDefault(r => r.Module == next?.Module) ?? _roots[0];
            start.IsExpanded = true;
            start.IsSelected = true;
            SetStatus(next is null
                ? "Every item in the path is complete."
                : $"Welcome to PLC+. In this preview, modules 3 and 5 are written; the others are outlines. Start here with module {next.Module.Number}: press the button under its title, or choose any module on the left.");
        };
    }

    // ---- The tree -------------------------------------------------------------------------------------

    private void BuildTree()
    {
        foreach (var m in _package.Modules)
        {
            var node = new PathNode { Module = m };
            foreach (var i in m.Items) node.Children.Add(new PathNode { Module = m, Item = i, Parent = node });
            _roots.Add(node);
        }
        RefreshTree();
    }

    private void RefreshTree()
    {
        var on = (Brush)FindResource("PowerOnBrush");
        var started = (Brush)FindResource("AccentBrush");
        var muted = (Brush)FindResource("MutedTextBrush");
        foreach (var node in _roots)
        {
            var m = node.Module!;
            var p = _progress.Of(m);
            node.Header = $"{m.Number}. {m.Title}";
            node.Weight = FontWeights.SemiBold;
            (node.Mark, node.MarkBrush) = p.State switch
            {
                ModuleState.Complete => ("✓", on),
                ModuleState.InProgress => ("◐", started),
                ModuleState.ComingLater => ("–", muted),
                _ => ("○", muted),
            };
            node.Detail = p.State == ModuleState.ComingLater ? "Coming later" : $"{p.Complete} of {p.Total} complete{StatusWords(m)}";
            node.SpokenName = $"Module {m.Number}, {m.Title}: {node.Detail}";
            node.Tip = $"Module {m.Number}: {m.Title}. Choose it to see what it teaches, what to finish first, and its items.";

            foreach (var child in node.Children)
            {
                var item = child.Item!;
                var state = _progress.StateOf(item);
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
        var items = _package.Modules.Where(m => m.Status != ModuleStatus.ComingLater).SelectMany(m => m.Items).ToList();
        PathProgress.Text = $"{items.Count(i => _progress.StateOf(i) == ItemState.Complete)} of {items.Count} items complete";
    }

    private static string StatusWords(LearningModule m) => m.Status switch
    {
        ModuleStatus.Draft => " · first draft",
        ModuleStatus.Outline => " · outline",
        _ => "",
    };

    private string ItemDetail(LearningItem item, ItemState state)
    {
        if (item.Kind == ItemKind.Assess)
        {
            var done = item.Covers.Count(c => _package.FindItem(c) is { } i && _progress.StateOf(i) == ItemState.Complete);
            var questions = item.Quiz is null ? ""
                : _progress.Items.TryGetValue(item.Id, out var own) && own.Completed is not null ? " · questions passed"
                : _progress.Best(item) is { } quizBest ? $" · questions: best {quizBest.Score:0} of {quizBest.MaxScore:0}"
                : " · questions not passed yet";
            return $"Module check · {done} of {item.Covers.Count} items done{questions}";
        }
        var kind = item.Exercise is not null ? "Exercise" : item.Quiz is not null ? "Questions" : "Reading";
        if (state == ItemState.Complete) return $"{kind} · complete";
        if (!item.CompletesByReading && _progress.Best(item) is { } best) return $"{kind} · best {best.Score:0} of {best.MaxScore:0}";
        return state == ItemState.InProgress ? $"{kind} · started" : kind;
    }

    private void PathTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not PathNode node) return;
        if (node.Item is { } item) ShowItem(item);
        else ShowModule(node.Module!);
    }

    private PathNode? NodeFor(LearningItem item) =>
        _roots.SelectMany(r => r.Children).FirstOrDefault(n => n.Item == item);

    private void Select(LearningItem item)
    {
        if (NodeFor(item) is not { } node) return;
        node.Parent!.IsExpanded = true;
        node.IsSelected = true;
    }

    // ---- A module's first page -------------------------------------------------------------------------

    private void ShowModule(LearningModule m)
    {
        _module = m;
        _item = null;
        ShowScreenHelp();
        ItemPage.Visibility = Visibility.Collapsed;
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
                    MasteryState.Mastered => $"- ✓ {op.Outcome.Text} **(mastered)**",
                    MasteryState.NeedsReview => $"- ↺ {op.Outcome.Text} **(review suggested)**: " + string.Join("; ", op.Evidence.Where(e => e.Contains("wrong", StringComparison.Ordinal) || e.Contains("did not pass", StringComparison.Ordinal))) + ".",
                    MasteryState.InProgress => $"- ◐ {op.Outcome.Text} (in progress: " + string.Join("; ", op.Evidence.Where(e => !e.EndsWith(": complete", StringComparison.Ordinal) && !e.EndsWith(" answered", StringComparison.Ordinal))) + ")",
                    _ => $"- ○ {op.Outcome.Text}",
                });
            md.AppendLine().AppendLine("*✓ mastered: its items are complete and its questions were right. ↺ review suggested: the work is done, but an answer in the module check was wrong, or a passed exercise later failed; it says what to do. ◐ in progress. ○ not started.*");
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
                    : i.Quiz is not null ? "Lesson and questions"
                    : i.Example is null ? "Reading" : "Reading, with an example to run";
                md.AppendLine($"| {i.Number} {i.Title} | {kind} | {WhereYouAre(i)} |");
            }
            md.AppendLine();
        }
        if (m.Introduction.Trim().Length > 0) md.AppendLine("## About this module").AppendLine().AppendLine(m.Introduction);
        return md.ToString();
    }

    private string WhereYouAre(LearningItem i)
    {
        var state = _progress.StateOf(i);
        if (i.Kind == ItemKind.Assess) return ItemDetail(i, state).Replace("Module check · ", "", StringComparison.Ordinal);
        if (state == ItemState.Complete) return "Complete ✓";
        if (!i.CompletesByReading && _progress.Best(i) is { } best) return $"Started: best {best.Score:0} of {best.MaxScore:0}";
        return state == ItemState.InProgress ? "Started" : "Not started";
    }

    private static string Hours(int minutes) => minutes < 90 ? $"{minutes} minutes" : $"{minutes / 60.0:0.#} hours";

    private void ModuleStart_Click(object sender, RoutedEventArgs e)
    {
        if (ModuleStartButton.Tag is LearningItem item) Select(item);
    }

    // ---- An item -------------------------------------------------------------------------------------

    private void ShowItem(LearningItem item)
    {
        ExerciseWorkspace.CommitPendingEdit();
        QuizPanel.Remember();
        _item = item;
        _module = item.Module;
        ModulePage.Visibility = Visibility.Collapsed;
        ItemPage.Visibility = Visibility.Visible;

        _progress.Opened(_package, item, DateTimeOffset.Now);
        SaveProgress();

        ItemModuleText.Text = $"Module {item.Module.Number} · {item.Module.Title}";
        ItemTitle.Text = $"{item.Number} {item.Title}";
        var missing = _progress.MissingPrerequisites(_package, item.Module);
        var index = _order.IndexOf(item);
        PreviousButton.IsEnabled = index > 0;
        NextButton.IsEnabled = index < _order.Count - 1;
        PreviousButton.ToolTip = index > 0 ? $"Back to {_order[index - 1]} (Ctrl+Shift+PgUp)" : "This is the first item in the path";
        NextButton.ToolTip = index < _order.Count - 1 ? $"On to {_order[index + 1]} (Ctrl+Shift+PgDn)" : "This is the last item in the path";

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
        if (example is not null) ExampleWorkspace.Load(_plc, example, exercise: false, item.ExampleProgram);
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
            ? state == ItemState.Complete ? (item.Quiz is null ? "Complete ✓: every item it covers is complete" : "Complete ✓: every item it covers is complete, and you passed the questions") : ItemDetail(item, state)
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
                md.AppendLine($"| {covered} | {WhereYouAre(covered)} |");
        md.AppendLine().AppendLine(_progress.StateOf(item) == ItemState.Complete
            ? "Every item is complete: so is this module check. ✓"
            : "When every item above is complete, so is this module check.");
        md.AppendLine().AppendLine("*In a later version of PLC+, finishing the check makes a progress report to hand in to your instructor: your name, your scores and your programs.*");
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
                .Select(c => $"{c} ({WhereYouAre(c).ToLowerInvariant()})")) + ".");
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
            RefreshTree();
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
        if (outcome.Passed && !wasComplete) Completed($"Every test passed: {_item.Number} is complete.");
        else
        {
            SaveProgress();
            RefreshTree();
            ShowItemStatus();
        }
    }

    private void Completed(string what)
    {
        SaveProgress();
        RefreshTree();
        ShowItemStatus();
        var module = _item!.Module;
        var p = _progress.Of(module);
        var next = _progress.NextAfter(_package, _item);
        SetStatus(what +
            (p.State == ModuleState.Complete ? $" That completes module {module.Number}." : $" Module {module.Number}: {p.Complete} of {p.Total} complete.") +
            (next is null ? " Every item in the path is complete." : $" Next: {next}{(next == NextInOrder(_item) ? " (Next ▸, top right)" : " (Go ▸ Continue where I left off)")}.") +
            (_item.Exercise is not null ? " To keep your program or hand it in: File ▸ Save My program as…" : ""));
    }

    private LearningItem? NextInOrder(LearningItem item)
    {
        var index = _order.IndexOf(item);
        return index >= 0 && index + 1 < _order.Count ? _order[index + 1] : null;
    }

    private void SaveProgress()
    {
        try
        {
            _progress.Save(_progressFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus("Your progress could not be saved on this computer: " + ex.Message);
        }
    }

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

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        var index = _item is null ? (_module is null ? -1 : _order.FindIndex(i => i.Module == _module) - 1) : _order.IndexOf(_item);
        if (index + 1 < _order.Count && index + 1 >= 0) Select(_order[index + 1]);
    }

    private void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (_item is null) return;
        var index = _order.IndexOf(_item);
        if (index > 0) Select(_order[index - 1]);
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (_progress.Continue(_package) is { } next) Select(next);
        else SetStatus("Every item in the path is complete.");
    }

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
        (HelpScreenTitle.Text, HelpScreenText.Text) = _item is null
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

    private void SetStatus(string text) => StatusText.Text = text;

    private void Help_Executed(object sender, RoutedEventArgs e) =>
        HelpPanel.Visibility = HelpPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        ExerciseWorkspace.CommitPendingEdit();
        base.OnClosing(e);
    }
}

/// <summary>A module or an item in the learning path's tree.</summary>
public sealed class PathNode : INotifyPropertyChanged
{
    public LearningModule? Module { get; init; }
    public LearningItem? Item { get; init; }
    public PathNode? Parent { get; init; }
    public ObservableCollection<PathNode> Children { get; } = [];

    private string _header = "", _mark = "", _detail = "", _spoken = "", _tip = "";
    private Brush _markBrush = Brushes.Gray;
    private FontWeight _weight = FontWeights.Normal;
    private bool _expanded, _selected;

    public string Header { get => _header; set => Set(ref _header, value); }
    public string Mark { get => _mark; set => Set(ref _mark, value); }
    public Brush MarkBrush { get => _markBrush; set => Set(ref _markBrush, value); }
    public string Detail { get => _detail; set => Set(ref _detail, value); }
    public string SpokenName { get => _spoken; set => Set(ref _spoken, value); }
    public string Tip { get => _tip; set => Set(ref _tip, value); }
    public FontWeight Weight { get => _weight; set => Set(ref _weight, value); }
    public bool IsExpanded { get => _expanded; set => Set(ref _expanded, value); }
    public bool IsSelected { get => _selected; set => Set(ref _selected, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
