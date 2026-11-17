using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Learning.Core.Content;
using Plc.Dialects.Iec;
using Plc.Engine;

namespace Learning.Shell;

/// <summary>
/// The interactive examples a Learn page can hold (Ron, 2026-11-17: "wherever possible add interactive examples of the concepts in
/// the Learn section"). Each is written on a line of its own:
/// <list type="bullet">
/// <item><c>[[try-ladder: programs/learn/and.json]]</c>: a small program running live, its inputs as buttons, its outputs as lamps,
/// power flow on the rungs, timers and counters and the tags inside the PLC, with Pause, Step one scan and Reset. A BOOL input whose
/// starting value is TRUE is a normally closed device: pressing it makes it FALSE.</item>
/// <item><c>[[try-logic: (A AND B) OR NOT C]]</c>: switch each input and see the result, the truth table's row marked.</item>
/// <item><c>[[try-number: 8 bits]]</c>, <c>16 bits</c> or <c>bcd</c>: click bits on and off, or type a number; decimal, hex, binary and
/// BCD side by side.</item>
/// <item><c>[[try-circuit: ohms-law]]</c>, <c>series</c>, <c>parallel</c> or <c>ac-dc</c>: move the sliders and see the current, voltages
/// and power.</item>
/// </list>
/// They run on the page, count for nothing, and say what to try. The live ladder runs only while it is on screen.
/// </summary>
internal static partial class TryWidgets
{
    public static readonly IReadOnlyList<string> Numbers = ["8 bits", "16 bits", "bcd"];

    internal static Brush Line => (Brush)Application.Current.FindResource("LineBrush");
    internal static Brush Muted => (Brush)Application.Current.FindResource("MutedTextBrush");
    internal static Brush On => (Brush)Application.Current.FindResource("PowerOnBrush");
    internal static Brush Panel => (Brush)Application.Current.FindResource("PanelBrush");

    /// <summary>The frame every interactive example has: "Try it" and what to do, then the example.</summary>
    internal static Border Frame(string title, string howTo, UIElement body, string spoken)
    {
        var stack = new StackPanel();
        var head = new TextBlock { FontWeight = FontWeights.SemiBold, FontSize = 15, TextWrapping = TextWrapping.Wrap };
        head.Inlines.Add(new System.Windows.Documents.Run("▶ Try it: ") { Foreground = On });
        head.Inlines.Add(new System.Windows.Documents.Run(title));
        stack.Children.Add(head);
        stack.Children.Add(new TextBlock { Text = howTo, Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 8), FontSize = 13 });
        stack.Children.Add(body);
        var frame = new Border
        {
            Child = stack, Background = Panel, BorderBrush = On, BorderThickness = new Thickness(3, 1, 1, 1),
            Padding = new Thickness(12, 10, 12, 12), HorizontalAlignment = HorizontalAlignment.Stretch,
            Tag = new ReaderTitle($"An example to try: {title}. {howTo}"),
        };
        AutomationProperties.SetName(frame, $"Try it: {title}");
        AutomationProperties.SetHelpText(frame, howTo);
        return frame;
    }

    internal static TextBlock Text(string text, double size = 14, bool bold = false, Brush? brush = null) => new()
    {
        Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap,
        Foreground = brush ?? Brushes.Black, VerticalAlignment = VerticalAlignment.Center,
    };

    internal static Border Group(string heading, UIElement content) => new()
    {
        Child = new StackPanel { Children = { Text(heading, 12.5, bold: true, brush: Muted), content } },
        Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1), Padding = new Thickness(10, 6, 10, 8),
        Margin = new Thickness(0, 0, 10, 10), VerticalAlignment = VerticalAlignment.Top,
    };

    /// <summary>A lamp: a circle, green and lit when on, with its name and ON or OFF in words (never colour alone).</summary>
    internal static (StackPanel Panel, Action<bool> Set) Lamp(string name, bool truth = false)
    {
        var dot = new Ellipse { Width = 22, Height = 22, Stroke = Brushes.Black, StrokeThickness = 1, Margin = new Thickness(0, 0, 8, 0) };
        var words = Text(name);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
        row.Children.Add(dot);
        row.Children.Add(words);
        void Set(bool on)
        {
            dot.Fill = on ? On : new SolidColorBrush(Color.FromRgb(0x2B, 0x31, 0x38));
            words.Text = $"{name}: {(truth ? (on ? "TRUE" : "FALSE") : on ? "ON" : "off")}";
            words.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
            AutomationProperties.SetName(row, words.Text);
        }
        Set(false);
        return (row, Set);
    }

    // ---- try-logic ---------------------------------------------------------------------------------------

    public static UIElement Logic(BooleanExpression e)
    {
        var values = e.Variables.ToDictionary(v => v, _ => false);
        var body = new StackPanel();
        var switches = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        var (lamp, setLamp) = Lamp("Result", truth: true);
        var rows = new List<(bool[] Inputs, Border[] Cells)>();
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
        var columns = e.Variables.Count + 1;
        for (var c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Border Cell(int row, int col, string text, bool header = false)
        {
            while (grid.RowDefinitions.Count <= row) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var b = new Border
            {
                Child = new TextBlock { Text = text, HorizontalAlignment = HorizontalAlignment.Center, FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal },
                Padding = new Thickness(12, 3, 12, 3), BorderBrush = Line, BorderThickness = new Thickness(0, 0, 1, 1),
                Background = header ? new SolidColorBrush(Color.FromRgb(0xEE, 0xF1, 0xF4)) : Brushes.White,
            };
            Grid.SetRow(b, row);
            Grid.SetColumn(b, col);
            grid.Children.Add(b);
            return b;
        }
        for (var v = 0; v < e.Variables.Count; v++) Cell(0, v, e.Variables[v], header: true);
        Cell(0, columns - 1, e.Text, header: true);
        var r = 1;
        var results = new List<bool>();
        foreach (var (inputs, result) in e.Rows())
        {
            results.Add(result);
            var cells = new Border[columns];
            for (var v = 0; v < inputs.Length; v++) cells[v] = Cell(r, v, inputs[v] ? "TRUE" : "FALSE");
            cells[columns - 1] = Cell(r, columns - 1, result ? "TRUE ✓" : "FALSE");
            rows.Add((inputs, cells));
            r++;
        }
        var marked = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xC1, 0x07));
        var said = Text("", 13, brush: Muted);
        var rung = LogicRung.Create(e.Text);
        void Show()
        {
            var result = e.Evaluate(values);
            setLamp(result);
            rung?.Show(values, result);
            for (var k = 0; k < rows.Count; k++)
            {
                var (inputs, cells) = rows[k];
                var here = e.Variables.Select((v, i) => values[v] == inputs[i]).All(x => x);
                foreach (var c in cells) c.Background = here ? marked : Brushes.White;
                ((TextBlock)cells[^1].Child).Text = (results[k] ? "TRUE ✓" : "FALSE") + (here ? "  ◀ you are here" : "");
            }
            said.Text = $"{string.Join(", ", e.Variables.Select(v => $"{v} is {(values[v] ? "TRUE" : "FALSE")}"))}: {e.Text} is {(result ? "TRUE" : "FALSE")}. Its row in the table is marked.";
        }
        foreach (var v in e.Variables)
        {
            var name = v;
            var toggle = new ToggleButton { Content = $"{name}: FALSE", MinWidth = 110, Margin = new Thickness(0, 0, 8, 0), FontWeight = FontWeights.SemiBold, ToolTip = $"Switches {name} between FALSE and TRUE" };
            AutomationProperties.SetName(toggle, $"Input {name}");
            toggle.Checked += (_, _) => { values[name] = true; toggle.Content = $"{name}: TRUE"; Show(); };
            toggle.Unchecked += (_, _) => { values[name] = false; toggle.Content = $"{name}: FALSE"; Show(); };
            switches.Children.Add(toggle);
        }
        switches.Children.Add(lamp);
        if (rung is { } figure)
        {
            body.Children.Add(new ScrollViewer
            {
                Content = new Border { Child = figure.Canvas, BorderBrush = Line, BorderThickness = new Thickness(1), Padding = new Thickness(4), Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left },
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false,
            });
            body.Children.Add(new TextBlock
            {
                Text = "The same logic as a ladder rung: AND is contacts in series, OR is paths side by side, NOT is a normally closed contact (NC). Above each contact is its input; under it, whether it is closed (lets power through) or open. A normally closed contact is closed when its input is FALSE. Where power reaches, the wire is green and thick and says power.",
                FontSize = 12, Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 10),
            });
        }
        body.Children.Add(switches);
        body.Children.Add(said);
        body.Children.Add(new Border { Child = grid, BorderBrush = Line, BorderThickness = new Thickness(1, 1, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0) });
        Show();
        AutomationProperties.SetLiveSetting(said, AutomationLiveSetting.Polite);
        return Frame(e.Text, "Click an input to switch it between FALSE and TRUE. The rung shows where power passes, the result lamp shows the answer, and the row of the truth table you are on is marked.", body, e.Text);
    }

    // ---- try-number ----------------------------------------------------------------------------------------

    public static UIElement? Number(string argument)
    {
        var mode = argument.Trim().ToLowerInvariant();
        if (!Numbers.Contains(mode)) return null;
        var bcd = mode == "bcd";
        var bits = mode == "8 bits" ? 8 : 16;
        var value = 0;
        var body = new StackPanel();
        var row = new WrapPanel();
        var toggles = new ToggleButton[bits];
        var lines = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        var dec = Text("", 16, bold: true);
        var hex = Text("", 15);
        var bin = Text("", 15);
        var bcdLine = Text("", 15);
        bin.FontFamily = hex.FontFamily = new FontFamily("Consolas");
        lines.Children.Add(dec);
        lines.Children.Add(bin);
        lines.Children.Add(hex);
        if (bcd) lines.Children.Add(bcdLine);
        AutomationProperties.SetLiveSetting(dec, AutomationLiveSetting.Polite);
        var typed = new TextBox { Width = 90, FontSize = 15, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = $"Type a number from 0 to {(1 << bits) - 1} and press Enter: its bits switch on" };
        AutomationProperties.SetName(typed, "Type a decimal number");
        var busy = false;
        void Show()
        {
            busy = true;
            for (var i = 0; i < bits; i++)
            {
                var weight = bits - 1 - i;
                var on = (value >> weight & 1) == 1;
                toggles[i].IsChecked = on;
                toggles[i].Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = on ? "1" : "0", FontSize = 18, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, FontFamily = new FontFamily("Consolas") },
                        new TextBlock { Text = (1 << weight).ToString(CultureInfo.InvariantCulture), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center },
                    },
                };
            }
            busy = false;
            var weights = Enumerable.Range(0, bits).Where(b => (value >> b & 1) == 1).Reverse().Select(b => (1 << b).ToString(CultureInfo.InvariantCulture)).ToList();
            dec.Text = $"Decimal: {value}" + (weights.Count > 1 ? $"  ( = {string.Join(" + ", weights)} )" : "");
            var binary = Convert.ToString(value, 2).PadLeft(bits, '0');
            bin.Text = "Binary:  " + string.Join(" ", Enumerable.Range(0, bits / 4).Select(g => binary.Substring(g * 4, 4)));
            hex.Text = $"Hex:     {value.ToString(bits == 8 ? "X2" : "X4", CultureInfo.InvariantCulture)}   (each group of four bits is one hex digit)";
            if (bcd)
            {
                var digits = Enumerable.Range(0, 4).Select(g => value >> (12 - 4 * g) & 0xF).ToList();
                bcdLine.Text = digits.All(d => d <= 9)
                    ? $"BCD:     {string.Join("  ", digits)}  → reads as {string.Concat(digits).TrimStart('0').PadLeft(1, '0')} on a BCD display"
                    : $"BCD:     not a BCD number: a group of four bits is {string.Join(", ", digits.Where(d => d > 9))}, and a BCD digit is only 0 to 9";
                bcdLine.Foreground = digits.All(d => d <= 9) ? Brushes.Black : new SolidColorBrush(Color.FromRgb(0xB3, 0x26, 0x1E));
            }
            typed.Text = value.ToString(CultureInfo.InvariantCulture);
        }
        for (var i = 0; i < bits; i++)
        {
            var weight = bits - 1 - i;
            var t = new ToggleButton { Width = 44, Height = 52, Margin = new Thickness(0, 0, (i % 4 == 3) ? 10 : 3, 6), ToolTip = $"Bit {weight}, worth {1 << weight}: click to switch it between 0 and 1" };
            AutomationProperties.SetName(t, $"Bit {weight}, worth {1 << weight}");
            t.Checked += (_, _) => { if (!busy) { value |= 1 << weight; Show(); } };
            t.Unchecked += (_, _) => { if (!busy) { value &= ~(1 << weight); Show(); } };
            toggles[i] = t;
            // Each group of four bits stays together when the row wraps.
            if (i % 4 == 0) row.Children.Add(new StackPanel { Orientation = Orientation.Horizontal });
            ((StackPanel)row.Children[^1]).Children.Add(t);
        }
        var wrong = Text("", 12.5, brush: new SolidColorBrush(Color.FromRgb(0xB3, 0x26, 0x1E)));
        void Commit()
        {
            var t = typed.Text.Trim();
            if (t == value.ToString(CultureInfo.InvariantCulture)) { wrong.Text = ""; return; }
            if (int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n < 1 << bits) { value = n; wrong.Text = ""; }
            else wrong.Text = int.TryParse(t, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var big) && big >= 0
                ? $"{big} does not fit in {bits} bits: the largest is {(1 << bits) - 1}."
                : $"\"{t}\" is not a whole number from 0 to {(1 << bits) - 1}.";
            Show();
        }
        typed.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Commit(); };
        typed.LostKeyboardFocus += (_, _) => Commit();
        var typeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        typeRow.Children.Add(Text("Or type a number and press Enter: ", 13, brush: Muted));
        typeRow.Children.Add(typed);
        var clear = new Button { Content = "All off", Margin = new Thickness(8, 0, 0, 0), ToolTip = "Switches every bit off (0)" };
        clear.Click += (_, _) => { value = 0; Show(); };
        typeRow.Children.Add(clear);
        body.Children.Add(row);
        body.Children.Add(typeRow);
        body.Children.Add(wrong);
        body.Children.Add(lines);
        Show();
        return Frame(bcd ? "BCD: four bits for each decimal digit" : $"{bits} bits, their weights and the number they make",
            bcd ? "Click the bits on and off. Each group of four bits is one decimal digit (0 to 9); a group above 9 is not BCD. Try 0001 0010 0011 0100, then 1010 in any group."
                : "Click a bit to switch it between 0 and 1: the number is the sum of the weights of the bits that are 1. Watch how each group of four bits makes one hex digit.",
            body, mode);
    }
}

/// <summary>
/// <c>[[try-ladder: programs/learn/x.json]]</c>: a small program running live on the page. Inputs are buttons (hold to press, or tick
/// keep pressed) or number boxes, outputs are lamps or values, and the rungs show power flow as on the Example tab. It runs on its
/// own simulated clock, only while it can be seen.
/// </summary>
internal sealed class TryLadder
{
    private sealed class PanelIo : IVirtualIo
    {
        public readonly Dictionary<string, PlcValue> Inputs = new(StringComparer.OrdinalIgnoreCase);
        public PlcValue? ReadInput(string tag) => Inputs.TryGetValue(tag, out var v) ? v : null;
        public void WriteOutput(string tag, PlcValue value) { }
        public void Advance(long ms) { }
    }

    private const int TickMs = 50;
    private readonly PlcProject _project;
    private readonly PanelIo _io = new();
    private PlcRuntime _runtime;
    private readonly LadderView _ladder;
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(TickMs) };
    private readonly List<Action> _readInputs = [];
    private readonly List<Action> _show = [];
    private readonly TextBlock _clock = TryWidgets.Text("", 12.5, brush: TryWidgets.Muted);
    private readonly TextBlock _fault = TryWidgets.Text("", 13, brush: new SolidColorBrush(Color.FromRgb(0xB3, 0x26, 0x1E)));
    private readonly ToggleButton _pause = new() { Content = "⏸ Pause", Margin = new Thickness(0, 0, 8, 0), ToolTip = "Stops the PLC's clock, so you can look, or go one scan at a time" };
    private readonly Button _step = new() { Content = "Step one scan", Margin = new Thickness(0, 0, 8, 0), IsEnabled = false, ToolTip = "While paused: one scan — read the inputs, solve every rung, write the outputs. To step with a button pressed, tick its keep pressed first" };

    public UIElement View { get; }

    public static UIElement? Create(PlcContent content, string file)
    {
        var project = ProjectJson.Deserialize(content.Read(file));
        return new TryLadder(project).View;
    }

    private TryLadder(PlcProject project)
    {
        _project = project;
        _runtime = new PlcRuntime(project, IecDialect.Instance, _io);
        var canvas = new Canvas { Background = Brushes.White };
        _ladder = new LadderView(canvas);
        _ladder.Show(project);
        AutomationProperties.SetName(canvas, "The program, live: " + string.Join(" ", project.Programs.SelectMany(p => p.Rungs).Select((r, i) => $"Rung {i + 1}: {r.Comment}")));

        var groups = new WrapPanel();
        var inputs = new StackPanel();
        foreach (var tag in project.Tags.Where(t => t.Io == IoDirection.Input)) inputs.Children.Add(Input(tag));
        if (inputs.Children.Count > 0) groups.Children.Add(TryWidgets.Group("Inputs (you work these)", inputs));
        var outputs = new StackPanel();
        foreach (var tag in project.Tags.Where(t => t.Io == IoDirection.Output)) outputs.Children.Add(Output(tag));
        if (outputs.Children.Count > 0) groups.Children.Add(TryWidgets.Group("Outputs (the PLC drives these)", outputs));
        var inside = new StackPanel();
        foreach (var tag in project.Tags.Where(t => t.Io == IoDirection.None)) inside.Children.Add(Inside(tag));
        if (inside.Children.Count > 0) groups.Children.Add(TryWidgets.Group("Inside the PLC", inside));

        var reset = new Button { Content = "Reset", Margin = new Thickness(0, 0, 12, 0), ToolTip = "Starts the PLC again: every tag back to its starting value, timers and counters at zero" };
        reset.Click += (_, _) => { _runtime = new PlcRuntime(_project, IecDialect.Instance, _io); Tick(run: false); };
        _pause.Checked += (_, _) => { _pause.Content = "▶ Run"; _pause.ToolTip = "Starts the PLC's clock again"; _step.IsEnabled = true; };
        _pause.Unchecked += (_, _) => { _pause.Content = "⏸ Pause"; _pause.ToolTip = "Stops the PLC's clock, so you can look, or go one scan at a time"; _step.IsEnabled = false; };
        _step.Click += (_, _) => { ReadInputs(); Guard(_runtime.Scan); Show(); };
        var controls = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        controls.Children.Add(_pause);
        controls.Children.Add(_step);
        controls.Children.Add(reset);
        controls.Children.Add(_clock);

        var body = new StackPanel();
        body.Children.Add(groups);
        body.Children.Add(new ScrollViewer
        {
            Content = new Border { Child = canvas, BorderBrush = TryWidgets.Line, BorderThickness = new Thickness(1), Padding = new Thickness(6), Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left },
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false,
        });
        body.Children.Add(controls);
        body.Children.Add(_fault);
        View = TryWidgets.Frame(project.Name,
            "It is running. Hold a button down (or tick keep pressed) and watch power flow, green and thick, along the rungs and the outputs change. Pause and Step one scan go one scan at a time; to step with a button pressed, tick its keep pressed first.",
            body, project.Name);

        _timer.Tick += (_, _) => Tick(run: _pause.IsChecked != true);
        // Runs only while it can be seen: another tab, another page or a closed window stop it.
        ((FrameworkElement)View).IsVisibleChanged += (_, e) => { if ((bool)e.NewValue) _timer.Start(); else _timer.Stop(); };
        ((FrameworkElement)View).Unloaded += (_, _) => _timer.Stop();
        Tick(run: false);
    }

    private void Guard(Action a)
    {
        try { a(); }
        catch (PlcFaultException) { }
    }

    private void ReadInputs()
    {
        foreach (var r in _readInputs) r();
    }

    private void Tick(bool run)
    {
        ReadInputs();
        if (run && _runtime.State == RunState.Running) Guard(() => _runtime.RunFor(TickMs));
        Show();
    }

    private void Show()
    {
        _ladder.Update(_runtime);
        foreach (var s in _show) s();
        _clock.Text = $"Simulated time {_runtime.NowMs / 1000.0:0.00} s · scan {_runtime.ScanCount}{(_pause.IsChecked == true ? " · paused" : "")}";
        _fault.Text = _runtime.State == RunState.Faulted && _runtime.Fault is { } f ? $"The PLC stopped with a fault: {f.Message} Press Reset to start again." : "";
    }

    private PlcValue Value(string name)
    {
        try { return _runtime.Read(name); }
        catch (PlcFaultException) { return PlcValue.False; }
    }

    private static PlcValue? Initial(TagDeclaration tag) =>
        tag.Initial is not null && PlcValue.TryParseLiteral(tag.Initial, out var v) ? v : null;

    private UIElement Input(TagDeclaration tag)
    {
        var note = tag.Comment is null ? "" : $" ({tag.Comment})";
        if (tag.Type.Equals("BOOL", StringComparison.OrdinalIgnoreCase))
        {
            // A normally closed device starts TRUE: pressing it makes it FALSE.
            var closed = Initial(tag) is { } i && i.AsBool;
            var button = new Button
            {
                Content = closed ? $"{tag.Name} (NC)" : tag.Name, MinWidth = 96, Height = 40, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
                Background = closed ? new SolidColorBrush(Color.FromRgb(0xB3, 0x26, 0x1E)) : new SolidColorBrush(Color.FromRgb(0x2E, 0x8B, 0x47)),
                ToolTip = closed
                    ? $"{tag.Name}{note}: normally closed, its input is TRUE until you press it. Hold it down with the mouse, or Tab to it and hold the Space bar."
                    : $"{tag.Name}{note}: its input is TRUE while you hold it down. Hold it down with the mouse, or Tab to it and hold the Space bar.",
            };
            AutomationProperties.SetHelpText(button, "Hold it down to press it: hold the Space bar, or tick keep pressed beside it.");
            var hold = new CheckBox { Content = "keep pressed", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0), ToolTip = $"While ticked, {tag.Name} stays pressed" };
            AutomationProperties.SetName(hold, $"Keep {tag.Name} pressed");
            var state = TryWidgets.Text("", 13);
            _readInputs.Add(() =>
            {
                var pressed = button.IsPressed || hold.IsChecked == true;
                var v = closed ? !pressed : pressed;
                _io.Inputs[tag.Name] = PlcValue.FromBool(v);
                state.Text = $"{tag.Name} = {(v ? "TRUE" : "FALSE")}";
                state.FontWeight = v ? FontWeights.SemiBold : FontWeights.Normal;
            });
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            row.Children.Add(button);
            row.Children.Add(hold);
            row.Children.Add(state);
            if (tag.Comment is null && !closed) return row;
            var described = new StackPanel { Margin = new Thickness(0, 3, 0, 3) };
            row.Margin = new Thickness(0);
            described.Children.Add(row);
            described.Children.Add(TryWidgets.Text((closed ? "Normally closed: TRUE until pressed. " : "") + (tag.Comment ?? ""), 12, brush: TryWidgets.Muted));
            return described;
        }
        // A number: a box to type in, with − and +.
        var start = Initial(tag) ?? PlcValue.Default(TypeOf(tag));
        var current = start;
        var example = current.Type switch { DataType.Time => "T#2s or T#500ms", DataType.Real => "2.5", _ => "42" };
        var box = new TextBox { Width = 80, Text = current.ToString(), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = $"{tag.Name}{note}: type a value (for example {example}) and press Enter, or use − and +" };
        var problem = TryWidgets.Text("", 12, brush: new SolidColorBrush(Color.FromRgb(0xB3, 0x26, 0x1E)));
        void Commit()
        {
            var typed = box.Text.Trim();
            if (typed == current.ToString()) { problem.Text = ""; return; }
            try
            {
                if (PlcValue.TryParseLiteral(typed, out var v)) { Set(v.ConvertTo(current.Type)); problem.Text = ""; return; }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException or PlcFaultException) { }
            problem.Text = $"\"{typed}\" is not a value {tag.Name} can take: type one like {example}.";
            box.Text = current.ToString();
        }
        AutomationProperties.SetName(box, tag.Name);
        var step = current.Type switch { DataType.Real => 0.5, DataType.Time => 100, _ => 1 };
        void Set(PlcValue v) { current = v; box.Text = v.ToString(); }
        box.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Commit(); };
        box.LostKeyboardFocus += (_, _) => Commit();
        Button Nudge(string text, double by)
        {
            var b = new Button { Content = text, Width = 30, Margin = new Thickness(4, 0, 0, 0), ToolTip = $"{(by < 0 ? "Less" : "More")}: {tag.Name} {(by < 0 ? "−" : "+")} {Math.Abs(by)}" };
            AutomationProperties.SetName(b, $"{tag.Name} {(by < 0 ? "minus" : "plus")} {Math.Abs(by)}");
            b.Click += (_, _) => Set(current.Type switch
            {
                DataType.Real => PlcValue.FromReal(current.R + by),
                DataType.Time => PlcValue.FromTime(Math.Max(0, current.I + (long)by)),
                DataType.DInt => PlcValue.FromDInt(current.I + (long)by),
                _ => PlcValue.FromInt(current.I + (long)by),
            });
            return b;
        }
        _readInputs.Add(() => _io.Inputs[tag.Name] = current);
        var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
        var label = TryWidgets.Text(tag.Name + " ", 14, bold: true);
        label.MinWidth = 90;
        line.Children.Add(label);
        line.Children.Add(box);
        line.Children.Add(Nudge("−", -step));
        line.Children.Add(Nudge("+", step));
        var whole = new StackPanel();
        whole.Children.Add(line);
        whole.Children.Add(problem);
        return whole;
    }

    private static DataType TypeOf(TagDeclaration tag) => tag.Type.ToUpperInvariant() switch
    {
        "INT" => DataType.Int,
        "DINT" => DataType.DInt,
        "REAL" => DataType.Real,
        "TIME" => DataType.Time,
        _ => DataType.Bool,
    };

    private UIElement Output(TagDeclaration tag)
    {
        if (tag.Type.Equals("BOOL", StringComparison.OrdinalIgnoreCase))
        {
            var (row, set) = TryWidgets.Lamp(tag.Name);
            if (tag.Comment is { } c) row.ToolTip = c;
            _show.Add(() => set(Value(tag.Name).AsBool));
            return row;
        }
        var text = TryWidgets.Text("", 14);
        _show.Add(() => text.Text = $"{tag.Name} = {Value(tag.Name)}");
        return text;
    }

    /// <summary>A tag inside the PLC: a memory bit or number, an array or structure (each element), or a timer or counter (its members).</summary>
    private UIElement Inside(TagDeclaration tag)
    {
        var text = TryWidgets.Text("", 13.5);
        text.FontFamily = new FontFamily("Consolas");
        if (tag.Comment is { } c) text.ToolTip = c;
        var spec = IecDialect.Instance.Find(tag.Type);
        _show.Add(() =>
        {
            if (spec is not null)
            {
                var members = spec.Members.Select(m => $"{m.Name} {Value($"{tag.Name}.{m.Name}")}");
                text.Text = $"{tag.Name} ({tag.Type}): {string.Join(" · ", members)}";
                return;
            }
            var elements = _runtime.Tags.Where(kv => kv.Key.Equals(tag.Name, StringComparison.OrdinalIgnoreCase)
                    || kv.Key.StartsWith(tag.Name + "[", StringComparison.OrdinalIgnoreCase) || kv.Key.StartsWith(tag.Name + ".", StringComparison.OrdinalIgnoreCase))
                .Take(12).Select(kv => kv.Key.Equals(tag.Name, StringComparison.OrdinalIgnoreCase) ? $"{kv.Key} = {kv.Value}" : $"{kv.Key}={kv.Value}");
            text.Text = string.Join("  ", elements);
        });
        return text;
    }
}
