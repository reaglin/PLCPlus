using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Plc.Dialects.Iec;
using Plc.Engine;
using Plc.Grading;
using Plc.Scenes;

namespace Learning.Shell;

/// <summary>
/// The machine screen: a program on the left, the machine it runs on the right, the grader below.
/// An item's Example tab shows it with the challenge's own programs (the one that works, and the
/// common mistake); its Exercise tab shows it with My program, which the learner writes and edits.
/// The clock runs only while the workspace is on screen.
/// </summary>
public partial class MachineWorkspace : UserControl
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(15) };
    private readonly Stopwatch _wall = new();
    private readonly LadderView _ladder;
    private readonly SceneView _sceneView;
    private PlcContent? _content;
    private PlcChallengeRunner? _runner;
    private ChallengeEntry? _current;
    private string _programJson = "";
    private Scene _scene = new();
    private PlcRuntime? _runtime;
    private double _owedMs;
    private bool _paused;
    private int _failedChecks;

    /// <summary>Something to tell the learner on the status line.</summary>
    public event Action<string>? StatusChanged;

    /// <summary>A grading run finished: the outcome, for the learner's progress.</summary>
    public event Action<Learning.Core.ChallengeOutcome>? Checked;

    /// <summary>True on the Exercise tab: My program, editable.</summary>
    public bool IsExercise { get; private set; }

    public ChallengeEntry? Challenge => _current;

    public MachineWorkspace()
    {
        InitializeComponent();
        _ladder = new LadderView(LadderCanvas);
        _sceneView = new SceneView(SceneCanvas, StateList);
        _timer.Tick += (_, _) => Tick();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) { _wall.Restart(); _timer.Start(); }
            else _timer.Stop();
        };
    }

    private Window HostWindow => Window.GetWindow(this) ?? Application.Current.MainWindow;

    private string ContentDir => _content!.Folder;

    /// <summary>Shows a challenge: its working program (Example), or My program (Exercise).</summary>
    public void Load(PlcContent content, ChallengeEntry challenge, bool exercise, string? exampleProgram = null)
    {
        _content = content;
        _runner ??= new PlcChallengeRunner(id => File.ReadAllText(content.ScenePath(id)));
        CommitPending();
        LeaveEditor();
        _current = challenge;
        IsExercise = exercise;
        TitleText.Text = "The task: " + challenge.Challenge.Title;
        BriefText.Text = challenge.Challenge.Brief;
        ResultsList.ItemsSource = null;
        ScoreText.Text = "";
        ScoreText.ToolTip = null;
        _failedChecks = 0;
        if (_paused) Pause_Click(this, new RoutedEventArgs());

        ProgramPicker.SelectionChanged -= ProgramPicker_SelectionChanged;
        ProgramPicker.Items.Clear();
        ProgramRow.Visibility = exercise ? Visibility.Collapsed : Visibility.Visible;
        CheckButton.Content = exercise ? "_Check my program" : "_Check this program";
        CheckTopButton.Visibility = exercise ? Visibility.Visible : Visibility.Collapsed;
        if (exercise)
        {
            ProgramPicker.SelectionChanged += ProgramPicker_SelectionChanged;
            EnterEditor();
            return;
        }
        foreach (var p in challenge.Programs) ProgramPicker.Items.Add(p.Title);
        var start = Math.Max(0, challenge.Programs.ToList().FindIndex(p => p.Title.Equals(exampleProgram ?? "", StringComparison.OrdinalIgnoreCase)));
        ProgramPicker.SelectionChanged += ProgramPicker_SelectionChanged;
        ProgramPicker.SelectedIndex = start; // loads the program
    }

    private void ProgramPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_current is null || ProgramPicker.SelectedIndex < 0 || IsExercise) return;
        _failedChecks = 0;
        var p = _current.Programs[ProgramPicker.SelectedIndex];
        _programJson = File.ReadAllText(Path.Combine(ContentDir, p.File));
        ProgramNote.Text = p.Note;
        ResultsList.ItemsSource = null;
        ScoreText.Text = "";
        ScoreText.ToolTip = null;
        if (_paused) Pause_Click(this, new RoutedEventArgs());
        PowerUp();
        SetStatus("Next: work the machine with its buttons and switches, and watch the rungs. Or press Check this program to run the challenge's tests.");
    }

    /// <summary>The PLC and the machine back to power-up. The test results stay until the program is changed.</summary>
    private void PowerUp()
    {
        if (_current is null) return;
        _scene = Scene.Load(File.ReadAllText(_content!.ScenePath(_current.Challenge.Scene)));
        var project = ProjectJson.Deserialize(_programJson);
        try
        {
            _runtime = new PlcRuntime(project, IecDialect.Instance, _scene);
        }
        catch (ProjectInvalidException)
        {
            _runtime = null; // the program has problems: the PLC stays stopped until they are fixed
        }
        SceneTitle.Text = _scene.Title;
        _sceneView.Show(_scene);
        WiringList.ItemsSource = _scene.IoList.Select(p => $"{p.Tag} ({(p.Kind == IoKind.Input ? "input" : "output")}): {p.Meaning}").ToList();
        ShowLadder(project);
        _owedMs = 0;
        Refresh();
    }

    // ---- Running ----------------------------------------------------------------------------------------

    private void Tick()
    {
        var elapsed = _wall.Elapsed.TotalMilliseconds;
        _wall.Restart();
        if (_runtime is null || _paused) return;
        _owedMs = Math.Min(_owedMs + elapsed, 250); // never try to catch up more than a quarter second
        var scan = _runtime.Project.ScanTimeMs;
        while (_owedMs >= scan)
        {
            _sceneView.ReadOperatorInputs();
            _runtime.Scan();
            _owedMs -= scan;
        }
        Refresh();
    }

    private void Refresh()
    {
        if (_runtime is null)
        {
            _sceneView.Update();
            ClockText.Text = "The PLC is stopped: the program has problems (listed under the rungs). Fix them and it starts again.";
            return;
        }
        _ladder.Update(_runtime);
        _sceneView.Update();
        var seconds = (_runtime.NowMs / 1000.0).ToString("0.00", CultureInfo.InvariantCulture);
        ClockText.Text = _runtime.State == RunState.Faulted
            ? $"FAULTED at {seconds} s: {_runtime.Fault!.Message} Press Reset."
            : $"Simulated time {seconds} s · scan {_runtime.ScanCount:N0} · {(_paused ? "paused" : "running")}";
    }

    private void SetStatus(string text) => StatusChanged?.Invoke(text);

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        _paused = !_paused;
        PauseButton.Content = _paused ? "Ru_n" : "P_ause";
        PauseButton.ToolTip = _paused ? "Starts the simulated clock again." : "Stops the simulated clock. The program and the machine stay as they are.";
        StepButton.IsEnabled = _paused;
        StepButton.ToolTip = _paused ? "Runs exactly one scan (10 ms)." : "Runs exactly one scan (10 ms). Available while paused: press Pause first.";
        _owedMs = 0;
        Refresh();
        if (sender != this) SetStatus(_paused ? "Paused. Step one scan runs 10 ms at a time. To press a button while stepping, tick its keep pressed box." : "Running.");
    }

    private void Step_Click(object sender, RoutedEventArgs e)
    {
        if (_runtime is null) return;
        _sceneView.ReadOperatorInputs();
        _runtime.Scan();
        Refresh();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        PowerUp();
        SetStatus("Reset to power-up: every tag off, the machine as it began, the clock at 0. The test results are kept.");
    }

    // ---- Checking -----------------------------------------------------------------------------------------

    private void Check_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null || _runner is null) return;
        CommitPending();
        var outcome = _runner.Run(_current.Json, _programJson);
        var lines = new List<string>();
        if (!outcome.Ran)
        {
            lines.Add("✗ The tests could not run, because the program has problems:");
            lines.AddRange(outcome.Problems.Select(p => "    • " + p));
        }
        var hiddenNumber = 0;
        foreach (var s in outcome.Scenarios)
        {
            var name = s.Hidden ? $"Hidden test {++hiddenNumber}" : s.Name;
            lines.Add(s.Passed
                ? $"✓ Passed — {name} ({s.Points:0}/{s.MaxPoints:0})"
                : $"✗ Failed — {name} ({s.Points:0}/{s.MaxPoints:0}): {s.Failure}");
        }

        if (outcome.Passed)
        {
            ScoreText.Text = $"Passed: {outcome.Score:0} of {outcome.MaxScore:0} points.";
            SetStatus(IsExercise
                ? $"Every test passed ({outcome.Score:0} of {outcome.MaxScore:0} points): your program does what the task asks. To keep it or hand it in: File ▸ Save My program as…"
                : "Every test passed. Choose \"A common mistake\" in Program to see how a failed test is reported.");
        }
        else
        {
            _failedChecks++;
            var hints = _current.Challenge.Hints;
            ScoreText.Text = $"Not passed yet: {outcome.Score:0} of {outcome.MaxScore:0} points.";

            // The first failed test the learner can see: its own hint if it has one, then the replay.
            var failed = outcome.Scenarios.Select((s, i) => (s, i)).FirstOrDefault(x => !x.s.Passed && !x.s.Hidden);
            var ownHint = failed.s is null ? null : _current.Challenge.Scenarios[failed.i].Hint;
            if (ownHint is not null)
                lines.Add($"Hint for \"{failed.s!.Name}\": {ownHint}");
            else if (hints.Count > 0)
            {
                var n = Math.Min(_failedChecks, hints.Count);
                lines.Add($"Hint {n} of {hints.Count} (each check shows the next): {hints[n - 1]}");
            }

            if (failed.s is not null)
            {
                lines.Add($"What happened in the test \"{failed.s.Name}\":");
                lines.AddRange(_runner.Narrate(_current.Json, _programJson, failed.i).Select(l => "    " + l));
            }
            SetStatus($"Not passed yet: {outcome.Score:0} of {outcome.MaxScore:0} points. Read the first failed test under the machine: it says what should have happened and when, with a hint for it.");
        }
        ScoreText.ToolTip = $"Program fingerprint (SHA-256): {outcome.WorkHash}";
        ResultsList.ItemsSource = lines;
        Checked?.Invoke(outcome);
    }
}
