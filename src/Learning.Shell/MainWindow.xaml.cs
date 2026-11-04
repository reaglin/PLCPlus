using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Learning.Core;
using Plc.Dialects.Iec;
using Plc.Engine;
using Plc.Grading;
using Plc.Scenes;

namespace Learning.Shell;

/// <summary>
/// The phase 0 preview: one scene, one challenge, two programs. Code-behind on purpose — the real
/// shell (dashboard, lessons, learners) is MVVM with CommunityToolkit.Mvvm and replaces this window.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly string ContentDir = Path.Combine(AppContext.BaseDirectory, "content", "plcplus-foundations");

    private readonly (string Title, string File, string Note)[] _programs =
    [
        ("With seal-in (passes)", "motor-seal-in.json",
            "Rung 1: Start, or the Motor's own contact in parallel with it (the seal-in), passes power through Stop to the Motor coil. Let go of Start and the seal-in keeps the motor running."),
        ("Without seal-in (fails a test)", "motor-no-seal-in.json",
            "Rung 1 has no seal-in contact: the motor runs only while Start is held down. Check this program to see how a failed test is reported."),
    ];

    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(15) };
    private readonly Stopwatch _wall = new();
    private readonly LadderView _ladder;
    private readonly PlcChallenge _challenge;
    private readonly string _challengeJson;
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
        _challengeJson = File.ReadAllText(Path.Combine(ContentDir, "challenges", "motor-start-stop.json"));
        _challenge = PlcChallengeRunner.LoadChallenge(_challengeJson);
        BriefText.Text = "The challenge: " + _challenge.Brief;

        foreach (var p in _programs) ProgramPicker.Items.Add(p.Title);
        ProgramPicker.SelectedIndex = 0;

        _timer.Tick += (_, _) => Tick();
        _wall.Start();
        _timer.Start();
    }

    private void LoadProgram()
    {
        var p = _programs[Math.Max(0, ProgramPicker.SelectedIndex)];
        _programJson = File.ReadAllText(Path.Combine(ContentDir, "programs", p.File));
        ProgramNote.Text = p.Note;
        _scene = Scene.Load(File.ReadAllText(Path.Combine(ContentDir, "scenes", "motor-station.json")));
        _runtime = new PlcRuntime(ProjectJson.Deserialize(_programJson), IecDialect.Instance, _scene);
        _ladder.Show(_runtime.Project);
        _owedMs = 0;
        _failedChecks = 0;
        ResultsList.ItemsSource = null;
        ScoreText.Text = "";
        Refresh();
        SetStatus("Next: hold down Start to run the motor, then press Stop. Or press Check this program to run the challenge's tests.");
    }

    private void Tick()
    {
        var elapsed = _wall.Elapsed.TotalMilliseconds;
        _wall.Restart();
        if (_runtime is null || _paused) return;
        _owedMs = Math.Min(_owedMs + elapsed, 250); // never try to catch up more than a quarter second
        var scan = _runtime.Project.ScanTimeMs;
        while (_owedMs >= scan)
        {
            ReadButtons();
            _runtime.Scan();
            _owedMs -= scan;
        }
        Refresh();
    }

    private void ReadButtons()
    {
        _scene.Operate("start", StartButton.IsPressed);
        _scene.Operate("stop", StopButton.IsPressed);
    }

    private void Refresh()
    {
        if (_runtime is null) return;
        _ladder.Update(_runtime);

        var light = (PilotLight)_scene.Find("running")!;
        LightLamp.Fill = light.Lit ? new SolidColorBrush(Color.FromRgb(0x3C, 0xC8, 0x5A)) : new SolidColorBrush(Color.FromRgb(0x2A, 0x33, 0x2C));
        LightText.Text = $"Running light: {(light.Lit ? "ON" : "off")}";

        var motor = (Motor)_scene.Find("m1")!;
        MotorRotation.Angle = motor.Angle;
        MotorText.Text = motor.Running ? "Motor M1: running"
            : motor.Commanded ? $"Motor M1: starting ({motor.Speed:P0} speed)"
            : motor.Speed > 0 ? "Motor M1: slowing down"
            : "Motor M1: stopped";

        var seconds = (_runtime.NowMs / 1000.0).ToString("0.00", CultureInfo.InvariantCulture);
        ClockText.Text = _runtime.State == RunState.Faulted
            ? $"FAULTED at {seconds} s: {_runtime.Fault!.Message} Press Reset."
            : $"Simulated time {seconds} s · scan {_runtime.ScanCount:N0} · {(_paused ? "paused" : "running")}";
    }

    private void SetStatus(string text) => StatusText.Text = text;

    // ---- Commands ---------------------------------------------------------------------------------

    private void ProgramPicker_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => LoadProgram();

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        _paused = !_paused;
        PauseButton.Content = _paused ? "_Run" : "_Pause";
        PauseButton.ToolTip = _paused ? "Starts the simulated clock again." : "Stops the simulated clock. The program and the machine stay as they are.";
        StepButton.IsEnabled = _paused;
        StepButton.ToolTip = _paused ? "Runs exactly one scan (10 ms)." : "Runs exactly one scan (10 ms). Available while paused: press Pause first.";
        _owedMs = 0;
        Refresh();
        SetStatus(_paused ? "Paused. Step one scan runs 10 ms at a time; hold a button down while you click Step to press it." : "Running.");
    }

    private void Step_Click(object sender, RoutedEventArgs e)
    {
        if (_runtime is null) return;
        ReadButtons();
        _runtime.Scan();
        Refresh();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        LoadProgram();
        SetStatus("Reset to power-up: every tag off, the motor stopped, the clock at 0.");
    }

    private void Check_Click(object sender, RoutedEventArgs e)
    {
        var runner = new PlcChallengeRunner(id => File.ReadAllText(Path.Combine(ContentDir, "scenes", id + ".json")));
        var outcome = runner.Run(_challengeJson, _programJson);
        var lines = new List<string>();
        if (!outcome.Ran)
        {
            lines.Add("✗ The tests could not run, because the program has problems:");
            lines.AddRange(outcome.Problems.Select(p => "    • " + p));
        }
        foreach (var s in outcome.Scenarios)
        {
            var name = s.Hidden ? $"Hidden test {outcome.Scenarios.Where(x => x.Hidden).ToList().IndexOf(s) + 1}" : s.Name;
            lines.Add(s.Passed
                ? $"✓ Passed — {name} ({s.Points:0}/{s.MaxPoints:0})"
                : $"✗ Failed — {name} ({s.Points:0}/{s.MaxPoints:0}): {s.Failure}");
        }

        if (outcome.Passed)
        {
            ScoreText.Text = $"Passed: {outcome.Score:0} of {outcome.MaxScore:0} points.";
            SetStatus("Every test passed. In the finished program this attempt is recorded in your progress.");
        }
        else
        {
            _failedChecks++;
            var hint = _challenge.Hints.Count == 0 ? null : _challenge.Hints[Math.Min(_failedChecks, _challenge.Hints.Count) - 1];
            ScoreText.Text = $"Not passed yet: {outcome.Score:0} of {outcome.MaxScore:0} points.";
            if (hint is not null) lines.Add($"Hint {Math.Min(_failedChecks, _challenge.Hints.Count)} of {_challenge.Hints.Count}: {hint}");

            // Replay the first failed test the learner can see, and show what happened, moment by moment.
            var failed = outcome.Scenarios.Select((s, i) => (s, i)).FirstOrDefault(x => !x.s.Passed && !x.s.Hidden);
            if (failed.s is not null)
            {
                var (_, trace) = runner.Replay(_challengeJson, _programJson, failed.i);
                lines.Add($"What happened in \"{failed.s.Name}\" (each line is a moment something changed):");
                lines.AddRange(trace.ToChangeLog().Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(12).Select(l => "    " + l));
            }
            SetStatus("Read the first failed test: it says what should have happened and when. Each further check unlocks the next hint.");
        }
        lines.Add($"Program fingerprint (SHA-256): {outcome.WorkHash[..16]}… — recorded with each attempt.");
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
