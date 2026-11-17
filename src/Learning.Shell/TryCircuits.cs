using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Plc.Grading;

namespace Learning.Shell;

/// <summary>
/// <c>[[try-circuit: …]]</c> (Ron, 2026-11-17: "a small circuit with a voltage (battery) and resistor, and a measurable current. Change
/// values on battery or resistor and show results on current … also show power … the same for series and parallel circuits … show with
/// diagram of actual circuits"). Each draws the circuit and redraws it as the sliders move: the battery with its voltage, each resistor with
/// its ohms, the voltage across it and its current, the current's arrow, and a switch to break the circuit. A wire carrying current is green
/// and thick (the program's rule for power, never colour alone: the current is written beside it). A resistor warms to orange as its power
/// goes up. The arguments (<see cref="CircuitSpec"/>) let an Example start at the Learn page's worked example.
/// </summary>
internal static partial class TryWidgets
{
    /// <summary>A PLC output point can usually give about this much; the current bars are full here.</summary>
    private const double OutputLimit = 0.5;

    public static UIElement? Circuit(string argument) => CircuitSpec.Parse(argument, out _) is not { } spec ? null : spec.Kind switch
    {
        "ohms-law" => OhmsLaw(spec),
        "series" => Series(spec),
        "parallel" => Parallel(spec),
        "ac-dc" => AcDc(),
        _ => null,
    };

    internal static string Amps(double a) => Math.Abs(a) >= 1 ? $"{a:0.##} A" : $"{a * 1000:0.#} mA";
    internal static string Ohms(double r) => r >= 1000 ? $"{r / 1000:0.##} kΩ" : $"{r:0.#} Ω";
    internal static string Watts(double w) => w >= 1 ? $"{w:0.##} W" : $"{w * 1000:0.#} mW";
    internal static string Volts(double v) => $"{v:0.##} V";

    private static string Over(double amps) => amps > OutputLimit ? $"  (more than the {OutputLimit} A a PLC output usually gives)" : "";

    /// <summary>A slider with its name and value in words; returns the row and the slider.</summary>
    internal static (Grid Row, Slider Slider) Knob(string name, double min, double max, double start, Func<double, string> show, string tip, double step = 1)
    {
        var slider = new Slider { Minimum = min, Maximum = max, Value = start, Width = 240, SmallChange = step, LargeChange = step * 10, IsSnapToTickEnabled = true, TickFrequency = step, VerticalAlignment = VerticalAlignment.Center, ToolTip = tip };
        AutomationProperties.SetName(slider, name);
        var label = Text(name, 14, bold: true);
        label.Width = 170;
        var value = Text(show(start), 14);
        value.Margin = new Thickness(10, 0, 0, 0);
        value.MinWidth = 70;
        slider.ValueChanged += (_, _) => value.Text = show(slider.Value);
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        for (var c = 0; c < 4; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(slider, 1);
        Grid.SetColumn(value, 2);
        grid.Children.Add(label);
        grid.Children.Add(slider);
        grid.Children.Add(value);
        return (grid, slider);
    }

    /// <summary>A tick box in a knob's fourth column: "Open" breaks that part's path.</summary>
    private static CheckBox OpenBox(Grid knob, string what, string tip)
    {
        var box = new CheckBox { Content = "Open", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), ToolTip = tip };
        AutomationProperties.SetName(box, $"Open {what}");
        Grid.SetColumn(box, 3);
        knob.Children.Add(box);
        return box;
    }

    /// <summary>The one switch of a loop, on a line of its own under the sliders.</summary>
    private static CheckBox SwitchBox(string text, string tip)
    {
        var box = new CheckBox { Content = text, Margin = new Thickness(0, 6, 0, 2), ToolTip = tip, FontSize = 14 };
        AutomationProperties.SetName(box, text);
        return box;
    }

    /// <summary>A bar that fills with a value from 0 to its full scale, with the value in words beside it.</summary>
    internal static (StackPanel Row, Action<double, string> Set) Meter(string name, double full)
    {
        var bar = new Rectangle { Height = 16, Fill = On, HorizontalAlignment = HorizontalAlignment.Left };
        var track = new Border { Width = 240, Height = 18, BorderBrush = Line, BorderThickness = new Thickness(1), Background = Brushes.White, Child = bar, Margin = new Thickness(0, 0, 10, 0) };
        var label = Text(name, 14, bold: true);
        label.Width = 170;
        var words = Text("", 14, bold: true);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
        row.Children.Add(label);
        row.Children.Add(track);
        row.Children.Add(words);
        AutomationProperties.SetLiveSetting(words, AutomationLiveSetting.Polite);
        return (row, (v, text) => { bar.Width = Math.Clamp(v / full, 0, 1) * 238; words.Text = text; AutomationProperties.SetName(row, $"{name}: {text}"); });
    }

    /// <summary>The figure beside the controls: the circuit drawing, then the sliders and the readings.</summary>
    private static UIElement Layout(Schematic figure, string caption, IEnumerable<UIElement> controls, IEnumerable<UIElement> readings, TextBlock sum)
    {
        var body = new StackPanel();
        var picture = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left };
        picture.Children.Add(new Border { Child = figure.Canvas, BorderBrush = Line, BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left });
        picture.Children.Add(Text(caption, 12, brush: Muted));
        ((TextBlock)picture.Children[^1]).MaxWidth = figure.Canvas.Width;
        ((TextBlock)picture.Children[^1]).Margin = new Thickness(0, 3, 0, 8);
        body.Children.Add(picture);
        var set = new StackPanel();
        set.Children.Add(Text("Change the circuit", 12.5, bold: true, brush: Muted));
        foreach (var c in controls) set.Children.Add(c);
        set.Children.Add(Text("What it does", 12.5, bold: true, brush: Muted));
        ((TextBlock)set.Children[^1]).Margin = new Thickness(0, 8, 0, 0);
        foreach (var r in readings) set.Children.Add(r);
        body.Children.Add(set);
        sum.Margin = new Thickness(0, 6, 0, 0);
        body.Children.Add(sum);
        return body;
    }

    // ---- Ohm's law: a battery and one resistor -------------------------------------------------------------

    private static UIElement OhmsLaw(CircuitSpec spec)
    {
        var (name, start) = spec.Parts[0];
        var (vRow, v) = Knob("Battery voltage (V)", 0, 48, spec.Volts, x => $"{x:0} V", "The battery's voltage: 0 to 48 volts (Tab to the slider and use the arrow keys)");
        var (rRow, r) = Knob($"Resistance of {name}", CircuitSpec.MinOhms, CircuitSpec.MaxOhms, start, Ohms, "The resistor's resistance: 10 Ω to 2 kΩ", 10);
        var open = SwitchBox("Open the switch (break the circuit)", "Breaks the loop, as a switch that opens or a cut wire would: no current flows");
        var (iRow, i) = Meter("Current (I = V ÷ R)", OutputLimit);
        var (pRow, p) = Meter("Power (P = V × I)", 5);
        var sum = Text("", 13, brush: Muted);
        var fig = new Schematic(440, 220, "The circuit: a battery and a resistor in one loop, with a switch");
        void Show()
        {
            double V = v.Value, R = r.Value, I = open.IsChecked == true ? 0 : V / R, P = V * I;
            i(I, Amps(I) + Over(I));
            p(P, Watts(P));
            sum.Text = open.IsChecked == true
                ? "The switch is open: there is no complete path, so no current flows and the resistor gets no power, whatever the voltage. This is an open circuit."
                : $"I = V ÷ R = {V:0} V ÷ {Ohms(R)} = {Amps(I)}.  P = V × I = {V:0} V × {Amps(I)} = {Watts(P)}. Double the voltage and the current doubles; double the resistance and it halves.";

            fig.Clear();
            bool live = I > 0;
            const double L = 80, R0 = 300, T = 50, B = 180;
            fig.Battery(L, T, B, V, live);
            fig.Wire(L, T, R0, T, live);
            fig.Wire(R0, T, R0, 85, live);
            fig.Resistor(new Point(R0, 85), new Point(R0, 145), P / 5, live);
            fig.Wire(R0, 145, R0, B, live);
            fig.Wire(R0, B, 220, B, live);
            fig.Switch(new Point(220, B), new Point(160, B), open.IsChecked != true, live, "Switch");
            fig.Wire(160, B, L, B, live);
            if (live) fig.Arrow(new Point(190, T), 1, 0, I);
            fig.Label(live ? $"I = {Amps(I)} →" : "I = 0: no current", 190, T - 26, 13, bold: true, centre: true, brush: live ? On : Muted);
            fig.Label(name, R0 + 16, 80, 13, bold: true);
            fig.Label(Ohms(R), R0 + 16, 97);
            fig.Label($"{Volts(open.IsChecked == true ? 0 : V)} across it", R0 + 16, 114);
            fig.Label($"P = {Watts(P)}", R0 + 16, 131);
        }
        v.ValueChanged += (_, _) => Show();
        r.ValueChanged += (_, _) => Show();
        open.Checked += (_, _) => Show();
        open.Unchecked += (_, _) => Show();
        Show();
        AutomationProperties.SetLiveSetting(sum, AutomationLiveSetting.Polite);
        return Frame("Ohm's law, V = I × R",
            "Move the battery's voltage and the resistance (or Tab to a slider and use the arrow keys). The current, its arrow and the power follow; the resistor warms as its power goes up. Tick Open to break the circuit.",
            Layout(fig, "The battery pushes current out of its + side, round the loop through the resistor, and back to −. A wire carrying current is green and thick.",
                [vRow, rRow, open], [iRow, pRow], sum), "ohms-law");
    }

    // ---- Series: one path ---------------------------------------------------------------------------------------

    private static UIElement Series(CircuitSpec spec)
    {
        var (vRow, v) = Knob("Supply voltage", 0, 48, spec.Volts, x => $"{x:0} V", "The supply's voltage: 0 to 48 volts");
        var knobs = spec.Parts.Select(part => Knob(part.Name, CircuitSpec.MinOhms, CircuitSpec.MaxOhms, part.Ohms, Ohms, $"The resistance of {part.Name}", 10)).ToList();
        var open = SwitchBox("Open the switch (break the loop)", "Opens the switch in the loop: one break anywhere stops the current everywhere");
        var (iRow, i) = Meter("Current (one, everywhere)", OutputLimit);
        var drops = spec.Parts.Select(part => Meter($"Voltage across {part.Name}", 48)).ToList();
        var (pRow, p) = Meter("Power, all together", 5);
        var sum = Text("", 13, brush: Muted);
        var n = spec.Parts.Count;
        var fig = new Schematic(Math.Max(460, 140 + n * 110), 230, $"The circuit: a supply and {n} resistors in series, in one loop with a switch");
        void Show()
        {
            var V = v.Value;
            var Rs = knobs.Select(k => k.Slider.Value).ToList();
            var total = Rs.Sum();
            var I = open.IsChecked == true ? 0 : V / total;
            i(I, Amps(I) + Over(I));
            for (var k = 0; k < n; k++) drops[k].Set(I * Rs[k], Volts(I * Rs[k]));
            p(V * I, Watts(V * I));
            sum.Text = open.IsChecked == true
                ? "The switch is open. In series there is only one path, so one break stops the current through every part, and every voltage across a resistor drops to 0."
                : $"Total resistance {string.Join(" + ", Rs.Select(Ohms))} = {Ohms(total)}. Current {V:0} V ÷ {Ohms(total)} = {Amps(I)}, the same through every part. "
                  + $"The voltages add up to the supply: {string.Join(" + ", Rs.Select(R => Volts(I * R)))} = {Volts(V)}. The bigger resistor takes the bigger share.";

            fig.Clear();
            bool live = I > 0;
            double L = 70, R0 = fig.Canvas.Width - 40, T = 60, B = 190, mid = (T + B) / 2;
            fig.Battery(L, T, B, V, live);
            var slot = (R0 - L - 30) / n;
            var x = L;
            for (var k = 0; k < n; k++)
            {
                var c = L + 30 + slot * (k + 0.5);
                fig.Wire(x, T, c - 30, T, live);
                fig.Resistor(new Point(c - 30, T), new Point(c + 30, T), I * I * Rs[k] / 5, live);
                fig.Label($"{spec.Parts[k].Name} = {Ohms(Rs[k])}", c, T - 40, 13, bold: true, centre: true);
                fig.Label($"{Volts(I * Rs[k])} across it", c, T + 14, 12.5, centre: true);
                x = c + 30;
            }
            fig.Wire(x, T, R0, T, live);
            fig.Wire(R0, T, R0, B, live);
            if (live) fig.Arrow(new Point(R0, mid), 0, 1, I);
            fig.Label(live ? $"I = {Amps(I)}" : "I = 0", R0 - 10, mid - 10, 13, bold: true, right: true, brush: live ? On : Muted);
            var sx = (L + R0) / 2;
            fig.Wire(R0, B, sx + 30, B, live);
            fig.Switch(new Point(sx + 30, B), new Point(sx - 30, B), open.IsChecked != true, live, "Switch");
            fig.Wire(sx - 30, B, L, B, live);
        }
        v.ValueChanged += (_, _) => Show();
        foreach (var k in knobs) k.Slider.ValueChanged += (_, _) => Show();
        open.Checked += (_, _) => Show();
        open.Unchecked += (_, _) => Show();
        Show();
        AutomationProperties.SetLiveSetting(sum, AutomationLiveSetting.Polite);
        return Frame(n == 2 ? "Two resistors in series" : $"{n} resistors in series",
            "Move the sliders. In series the same current flows through every part, and the supply's voltage is shared between them. Tick Open to break the loop.",
            Layout(fig, "One path: the current leaves the supply's +, passes through every resistor in turn, and returns to −.",
                [vRow, .. knobs.Select(k => (UIElement)k.Row), open], [iRow, .. drops.Select(d => (UIElement)d.Row), pRow], sum), "series");
    }

    // ---- Parallel: a path for each branch ------------------------------------------------------------------------

    private static UIElement Parallel(CircuitSpec spec)
    {
        var n = spec.Parts.Count;
        var (vRow, v) = Knob("Supply voltage", 0, 48, spec.Volts, x => $"{x:0} V", "The supply's voltage: 0 to 48 volts");
        var knobs = spec.Parts.Select(part => Knob(part.Name, CircuitSpec.MinOhms, CircuitSpec.MaxOhms, part.Ohms, Ohms, $"The resistance of {part.Name}", 10)).ToList();
        var opens = spec.Parts.Select((part, k) => OpenBox(knobs[k].Row, part.Name + "'s branch", $"Opens the switch in {part.Name}'s branch: only that branch stops")).ToList();
        var currents = spec.Parts.Select(part => Meter($"Current in {part.Name}", OutputLimit)).ToList();
        var (tRow, t) = Meter("Total current", OutputLimit);
        var (pRow, p) = Meter("Power, all together", 5);
        var sum = Text("", 13, brush: Muted);
        var fig = new Schematic(Math.Max(460, 235 + n * 95), 250, $"The circuit: a supply and {n} branches in parallel, each with its own switch");
        void Show()
        {
            var V = v.Value;
            var Rs = knobs.Select(k => k.Slider.Value).ToList();
            var Is = Enumerable.Range(0, n).Select(k => opens[k].IsChecked == true ? 0 : V / Rs[k]).ToList();
            var I = Is.Sum();
            for (var k = 0; k < n; k++) currents[k].Set(Is[k], Amps(Is[k]) + (opens[k].IsChecked == true ? "  (open)" : ""));
            t(I, Amps(I) + Over(I));
            p(V * I, Watts(V * I));
            var closed = Enumerable.Range(0, n).Where(k => opens[k].IsChecked != true).ToList();
            var rt = closed.Count == 0 ? double.PositiveInfinity : 1 / closed.Sum(k => 1 / Rs[k]);
            sum.Text = $"Each closed branch has the full {V:0} V across it, so its current is {V:0} V ÷ its resistance. The currents add: "
                + string.Join(" + ", Is.Select(Amps)) + $" = {Amps(I)}. "
                + (closed.Count == n ? $"Total resistance {Ohms(rt)}, less than the smallest branch." : closed.Count == 0 ? "Every branch is open: no current." : "An open branch stops only itself; the others carry on as before.");

            fig.Clear();
            double L = 95, T = 55, B = 215;
            var xs = Enumerable.Range(0, n).Select(k => 205.0 + k * 95).ToList();
            fig.Battery(L, T, B, V, I > 0);
            // A piece of rail carries the current of every branch beyond it.
            var before = L;
            for (var k = 0; k < n; k++)
            {
                var beyond = Is.Skip(k).Sum() > 0;
                fig.Wire(before, T, xs[k], T, beyond);
                fig.Wire(before, B, xs[k], B, beyond);
                before = xs[k];
            }
            if (I > 0) fig.Arrow(new Point((L + xs[0]) / 2, T), 1, 0, I);
            fig.Label(I > 0 ? $"Total {Amps(I)} →" : "Total 0", (L + xs[0]) / 2, T - 28, 13, bold: true, centre: true, brush: I > 0 ? On : Muted);
            for (var k = 0; k < n; k++)
            {
                double x = xs[k];
                var live = Is[k] > 0;
                fig.Switch(new Point(x, T), new Point(x, T + 40), opens[k].IsChecked != true, live, null);
                fig.Wire(x, T + 40, x, T + 60, live);
                fig.Resistor(new Point(x, T + 60), new Point(x, T + 120), Is[k] * Is[k] * Rs[k] / 5, live);
                fig.Wire(x, T + 120, x, B, live);
                if (live) fig.Arrow(new Point(x, T + 140), 0, 1, Is[k]);
                fig.Label(spec.Parts[k].Name, x + 14, T + 58, 13, bold: true);
                fig.Label(Ohms(Rs[k]), x + 14, T + 75, 12.5);
                fig.Label(opens[k].IsChecked == true ? "open" : Amps(Is[k]), x + 14, T + 92, 12.5, bold: live, brush: live ? On : Muted);
            }
        }
        v.ValueChanged += (_, _) => Show();
        foreach (var k in knobs) k.Slider.ValueChanged += (_, _) => Show();
        foreach (var o in opens) { o.Checked += (_, _) => Show(); o.Unchecked += (_, _) => Show(); }
        Show();
        AutomationProperties.SetLiveSetting(sum, AutomationLiveSetting.Polite);
        return Frame(n == 2 ? "Two branches in parallel" : $"{n} branches in parallel",
            "Move the sliders. In parallel every branch gets the full supply voltage, and the currents add up. Tick Open on a branch to break only that one.",
            Layout(fig, "Several paths: each branch joins the + rail to the − rail on its own. The rail near the supply carries the total.",
                [vRow, .. knobs.Select(k => (UIElement)k.Row)], [.. currents.Select(c => (UIElement)c.Row), tRow, pRow], sum), "parallel");
    }

    // ---- AC and DC ---------------------------------------------------------------------------------------------

    private static UIElement AcDc()
    {
        var body = new StackPanel();
        var (pRow, peak) = Knob("AC peak voltage", 10, 340, 170, x => $"{x:0} V", "The highest voltage the AC reaches each cycle", 10);
        var (fRow, freq) = Knob("Frequency", 1, 60, 60, x => $"{x:0} Hz", "Cycles each second (60 Hz in North America, 50 Hz in Europe)");
        var canvas = new Canvas { Width = 420, Height = 182, Background = Brushes.White, Margin = new Thickness(0, 6, 0, 6), ClipToBounds = true };
        var info = Text("", 13, brush: Muted);
        void Show()
        {
            canvas.Children.Clear();
            const double mid = 80, scale = 70 / 340.0;
            canvas.Children.Add(new System.Windows.Shapes.Line { X1 = 0, X2 = 420, Y1 = mid, Y2 = mid, Stroke = Line, StrokeThickness = 1 });
            var rms = peak.Value / Math.Sqrt(2);
            var dc = new System.Windows.Shapes.Line { X1 = 0, X2 = 420, Y1 = mid - rms * scale, Y2 = mid - rms * scale, Stroke = Brushes.SteelBlue, StrokeThickness = 2, StrokeDashArray = [4, 3] };
            canvas.Children.Add(dc);
            var wave = new Polyline { Stroke = On, StrokeThickness = 2.5 };
            // 0.1 s across the picture.
            for (var x = 0; x <= 420; x += 2) wave.Points.Add(new Point(x, mid - peak.Value * scale * Math.Sin(2 * Math.PI * freq.Value * (x / 420.0 * 0.1))));
            canvas.Children.Add(wave);
            var key = Text("green: AC   blue dashes: the DC voltage that heats a lamp the same (the RMS)   across: 0.1 s", 11, brush: Muted);
            Canvas.SetLeft(key, 4);
            Canvas.SetTop(key, 162);
            canvas.Children.Add(key);
            info.Text = $"AC swings from +{peak.Value:0} V to −{peak.Value:0} V, {freq.Value:0} times a second (one cycle every {1000 / freq.Value:0.#} ms). It does the same work as {rms:0} V of DC (peak ÷ √2). DC stays at one voltage: a 24 V DC PLC input sees a steady 24 V.";
        }
        peak.ValueChanged += (_, _) => Show();
        freq.ValueChanged += (_, _) => Show();
        AutomationProperties.SetName(canvas, "A picture of the AC wave and the DC line");
        foreach (var x in new UIElement[] { pRow, fRow, canvas, info }) body.Children.Add(x);
        Show();
        return Frame("AC and DC", "Move the sliders: the green wave is AC; the blue dashed line is DC of the same effect. 170 V peak is the 120 V of a wall socket.", body, "ac-dc");
    }

    /// <summary>Draws a circuit diagram with the usual symbols on a canvas.</summary>
    private sealed class Schematic
    {
        private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(0x22, 0x26, 0x2B));
        public Canvas Canvas { get; }

        public Schematic(double width, double height, string name)
        {
            Canvas = new Canvas { Width = width, Height = height, Background = Brushes.White, ClipToBounds = true };
            AutomationProperties.SetName(Canvas, name);
        }

        public void Clear() => Canvas.Children.Clear();

        /// <summary>A wire: green and thick when it carries current, thin and dark when it does not.</summary>
        public void Wire(double x1, double y1, double x2, double y2, bool live) =>
            Canvas.Children.Add(new System.Windows.Shapes.Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = live ? On : Ink, StrokeThickness = live ? 3.5 : 1.8, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });

        /// <summary>A battery on the left side, upright between the top and bottom wires: the long plate is +.</summary>
        public void Battery(double x, double top, double bottom, double volts, bool live)
        {
            var mid = (top + bottom) / 2;
            Wire(x, top, x, mid - 7, live);
            Wire(x, mid + 7, x, bottom, live);
            Canvas.Children.Add(new System.Windows.Shapes.Line { X1 = x - 18, X2 = x + 18, Y1 = mid - 7, Y2 = mid - 7, Stroke = Ink, StrokeThickness = 2.5 });
            Canvas.Children.Add(new System.Windows.Shapes.Line { X1 = x - 9, X2 = x + 9, Y1 = mid + 7, Y2 = mid + 7, Stroke = Ink, StrokeThickness = 5 });
            Label("+", x + 14, mid - 30, 15, bold: true);
            Label("−", x + 14, mid + 8, 15, bold: true);
            Label("Battery", x - 24, mid - 26, 12.5, bold: true, right: true);
            Label($"{volts:0} V", x - 24, mid - 8, 14, bold: true, right: true);
        }

        /// <summary>A resistor (a zigzag) from a to b, on an orange glow that grows with its power (warmth, 0 to 1).</summary>
        public void Resistor(Point a, Point b, double warmth, bool live)
        {
            var dx = b.X - a.X; var dy = b.Y - a.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            double ux = dx / length, uy = dy / length, nx = -uy, ny = ux;
            var glow = Math.Clamp(warmth, 0, 1);
            if (glow > 0.01)
            {
                var halo = new Rectangle
                {
                    Width = Math.Abs(dx) + 22, Height = Math.Abs(dy) + 22, RadiusX = 9, RadiusY = 9,
                    Fill = new SolidColorBrush(Color.FromArgb((byte)(40 + 170 * glow), 0xF5, 0x8A, 0x1F)),
                };
                Canvas.SetLeft(halo, Math.Min(a.X, b.X) - 11);
                Canvas.SetTop(halo, Math.Min(a.Y, b.Y) - 11);
                Canvas.Children.Add(halo);
            }
            var zig = new Polyline { Stroke = live ? On : Ink, StrokeThickness = live ? 3 : 2, StrokeLineJoin = PenLineJoin.Round };
            zig.Points.Add(a);
            const int peaks = 6;
            for (var k = 0; k < peaks; k++)
            {
                var along = (k + 0.5) / peaks * length;
                var side = k % 2 == 0 ? 8 : -8;
                zig.Points.Add(new Point(a.X + ux * along + nx * side, a.Y + uy * along + ny * side));
            }
            zig.Points.Add(b);
            Canvas.Children.Add(zig);
        }

        /// <summary>A switch from a to b: its blade lies across when closed, and lifts away from b when open.</summary>
        public void Switch(Point a, Point b, bool closed, bool live, string? name)
        {
            foreach (var p in new[] { a, b })
            {
                var dot = new Ellipse { Width = 7, Height = 7, Fill = Brushes.White, Stroke = Ink, StrokeThickness = 1.5 };
                Canvas.SetLeft(dot, p.X - 3.5);
                Canvas.SetTop(dot, p.Y - 3.5);
                Canvas.Children.Add(dot);
            }
            var dx = b.X - a.X; var dy = b.Y - a.Y;
            var end = closed ? b : new Point(a.X + dx * 0.87 - dy * 0.5, a.Y + dy * 0.87 + dx * 0.5);
            // An open blade lifts upward on a level wire and to the right on an upright one.
            if (!closed && Math.Abs(dy) < 1) end = new Point(a.X + dx * 0.87, a.Y - Math.Abs(dx) * 0.5);
            if (!closed && Math.Abs(dx) < 1) end = new Point(a.X + Math.Abs(dy) * 0.5, a.Y + dy * 0.87);
            Canvas.Children.Add(new System.Windows.Shapes.Line { X1 = a.X, Y1 = a.Y, X2 = end.X, Y2 = end.Y, Stroke = live && closed ? On : Ink, StrokeThickness = live && closed ? 3.5 : 2, StrokeEndLineCap = PenLineCap.Round });
            if (name is not null) Label($"{name}: {(closed ? "closed" : "OPEN")}", (a.X + b.X) / 2, a.Y + 8, 12, bold: !closed, centre: true, brush: closed ? Muted : new SolidColorBrush(Color.FromRgb(0xB3, 0x26, 0x1E)));
            else if (!closed) Label("OPEN", a.X + 26, (a.Y + b.Y) / 2 - 8, 12, bold: true, brush: new SolidColorBrush(Color.FromRgb(0xB3, 0x26, 0x1E)));
        }

        /// <summary>An arrow on a wire in the direction (dx, dy) of the current; larger for more current.</summary>
        public void Arrow(Point at, double dx, double dy, double amps)
        {
            var size = 7 + 7 * Math.Clamp(amps / OutputLimit, 0, 1);
            double nx = -dy, ny = dx;
            var tip = new Point(at.X + dx * size, at.Y + dy * size);
            var head = new Polygon { Fill = On, Stroke = On, StrokeThickness = 1 };
            head.Points.Add(tip);
            head.Points.Add(new Point(at.X - dx * size * 0.6 + nx * size * 0.75, at.Y - dy * size * 0.6 + ny * size * 0.75));
            head.Points.Add(new Point(at.X - dx * size * 0.6 - nx * size * 0.75, at.Y - dy * size * 0.6 - ny * size * 0.75));
            Canvas.Children.Add(head);
        }

        /// <summary>Words on the drawing at (x, y): from x, centred on x, or ending at x.</summary>
        public void Label(string text, double x, double y, double size = 12.5, bool bold = false, bool centre = false, bool right = false, Brush? brush = null)
        {
            var t = new TextBlock { Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Foreground = brush ?? Ink };
            t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(t, centre ? x - t.DesiredSize.Width / 2 : right ? x - t.DesiredSize.Width : x);
            Canvas.SetTop(t, y);
            Canvas.Children.Add(t);
        }
    }
}
