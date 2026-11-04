using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Plc.Dialects.Iec;
using Plc.Engine;
using Plc.Grading;
using Plc.Scenes;

namespace Learning.Shell;

/// <summary>
/// The preview for review: every challenge in the content package, its machine, its programs and its
/// tests. Code-behind on purpose — the real shell (dashboard, lessons, learners) is MVVM with
/// CommunityToolkit.Mvvm and replaces this window (plan 7.1).
/// </summary>
public partial class MainWindow : Window
{
    private const string MyProgramTitle = "My program (edit it)";

    private static readonly string ContentDir = Path.Combine(AppContext.BaseDirectory, "content", "plcplus-foundations");

    private sealed record ProgramEntry(string Title, string File, string Note);
    private sealed record ChallengeEntry(string File, PlcChallenge Challenge, string Json, List<ProgramEntry> Programs);

    private readonly List<ChallengeEntry> _challenges = [];
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(15) };
    private readonly Stopwatch _wall = new();
    private readonly LadderView _ladder;
    private readonly SceneView _sceneView;
    private readonly PlcChallengeRunner _runner = new(id => File.ReadAllText(Path.Combine(ContentDir, "scenes", id + ".json")));
    private ChallengeEntry? _current;
    private string _programJson = "";
    private Scene _scene = new();
    private PlcRuntime? _runtime;
    private double _owedMs;
    private bool _paused;
    private int _failedChecks;

    public MainWindow()
    {
        InitializeComponent();
        _ladder = new LadderView(LadderCanvas);
        _sceneView = new SceneView(SceneCanvas, StateList);
        try
        {
            LoadManifest();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException)
        {
            MessageBox.Show(
                "PLC+ could not open its lesson files (the content folder beside PLCPlus.exe). " +
                "Reinstall PLC+, or if you copied the program, copy the content folder with it.\n\nDetails: " + ex.Message,
                "PLC+ cannot start", MessageBoxButton.OK, MessageBoxImage.Error);
            Loaded += (_, _) => Close();
            return;
        }

        foreach (var c in _challenges) ChallengePicker.Items.Add(c.Challenge.Title);
        ChallengePicker.SelectedIndex = 0;

        _timer.Tick += (_, _) => Tick();
        _wall.Start();
        _timer.Start();
    }

    private void LoadManifest()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(ContentDir, "manifest.json")));
        foreach (var c in doc.RootElement.GetProperty("challenges").EnumerateArray())
        {
            var file = c.GetProperty("file").GetString()!;
            var json = File.ReadAllText(Path.Combine(ContentDir, file));
            var programs = c.GetProperty("programs").EnumerateArray()
                .Select(p => new ProgramEntry(p.GetProperty("title").GetString()!, p.GetProperty("file").GetString()!, p.GetProperty("note").GetString() ?? ""))
                .ToList();
            _challenges.Add(new ChallengeEntry(file, PlcChallengeRunner.LoadChallenge(json), json, programs));
        }
    }

    // ---- Loading ----------------------------------------------------------------------------------------

    private void ChallengePicker_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ChallengePicker.SelectedIndex < 0) return;
        _current = _challenges[ChallengePicker.SelectedIndex];
        TitleText.Text = _current.Challenge.Title;
        BriefText.Text = "The task: " + _current.Challenge.Brief;
        ProgramPicker.Items.Clear();
        foreach (var p in _current.Programs) ProgramPicker.Items.Add(p.Title);
        ProgramPicker.Items.Add(MyProgramTitle);
        ProgramPicker.SelectedIndex = 0; // loads the program
    }

    private void ProgramPicker_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_current is null || ProgramPicker.SelectedIndex < 0) return;
        _failedChecks = 0;
        if (ProgramPicker.SelectedIndex >= _current.Programs.Count)
        {
            ResultsList.ItemsSource = null;
            ScoreText.Text = "";
            ScoreText.ToolTip = null;
            if (_paused) Pause_Click(this, new RoutedEventArgs());
            EnterEditor();
            return;
        }
        LeaveEditor();
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
        _scene = Scene.Load(File.ReadAllText(Path.Combine(ContentDir, "scenes", _current.Challenge.Scene + ".json")));
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

    private void SetStatus(string text) => StatusText.Text = text;

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        _paused = !_paused;
        PauseButton.Content = _paused ? "Ru_n" : "P_ause";
        PauseButton.ToolTip = _paused ? "Starts the simulated clock again." : "Stops the simulated clock. The program and the machine stay as they are.";
        StepButton.IsEnabled = _paused;
        StepButton.ToolTip = _paused ? "Runs exactly one scan (10 ms)." : "Runs exactly one scan (10 ms). Available while paused: press Pause first.";
        _owedMs = 0;
        Refresh();
        SetStatus(_paused ? "Paused. Step one scan runs 10 ms at a time. To press a button while stepping, tick its keep pressed box." : "Running.");
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
        if (_current is null) return;
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
            SetStatus(_current.Programs.Count > 1
                ? "Every test passed. Choose \"A common mistake\" in Program to see how a failed test is reported."
                : "Every test passed.");
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
            SetStatus("Read the first failed test: it says what should have happened and when, with a hint for it.");
        }
        ScoreText.ToolTip = $"Program fingerprint (SHA-256): {outcome.WorkHash}";
        ResultsList.ItemsSource = lines;
    }

    private void Help_Executed(object sender, RoutedEventArgs e) =>
        HelpPanel.Visibility = HelpPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }
}
