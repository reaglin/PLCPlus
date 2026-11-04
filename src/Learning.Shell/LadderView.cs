using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Plc.Editors;
using Plc.Engine;

namespace Learning.Shell;

/// <summary>What the ladder view does besides drawing: selection, problems, clicks and forcing.</summary>
internal sealed class LadderViewOptions
{
    public LadderSelection? Selection { get; init; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];
    public Action<int, IReadOnlyList<int>>? ElementClicked { get; init; }
    public Action<int>? RungClicked { get; init; }
    /// <summary>Whether a tag can be forced (it is wired to the machine).</summary>
    public Func<string, bool>? CanForce { get; init; }
    /// <summary>Force a tag on or off, or remove the force (null).</summary>
    public Action<string, bool?>? Force { get; init; }
}

/// <summary>
/// Draws a program's rungs and shows power flow live (spec 5.1 live mode); in the editor it also shows the
/// selection and marks each problem on the element it is about. Drawn with WPF shapes and text (not a
/// bitmap) so a screen reader can read it, and so the same drawing prints (plan 3.6).
/// Power is shown three ways, never by colour alone: green and thick, and the value TRUE.
/// </summary>
internal sealed class LadderView
{
    private const double CellW = 120, CellH = 78, Mid = 34, Rail = 12, Left = 24;

    private readonly Canvas _canvas;
    private readonly List<Action<PlcRuntime>> _updates = [];
    private readonly Brush _on, _off, _ink, _muted;
    private static readonly Brush Problem = new SolidColorBrush(Color.FromRgb(0xB3, 0x26, 0x1E));
    private static readonly Brush Forced = new SolidColorBrush(Color.FromRgb(0xB4, 0x53, 0x1A));
    private static readonly Brush SelectStroke = new SolidColorBrush(Color.FromRgb(0x1F, 0x5F, 0xA8));
    private static readonly Brush SelectFill = new SolidColorBrush(Color.FromArgb(0x26, 0x1F, 0x5F, 0xA8));
    private LadderViewOptions _options = new();
    private int _rung;
    private readonly List<(Rect Area, int Rung, IReadOnlyList<int> Path)> _hits = [];

    /// <summary>The element under a point of the canvas (for drag and drop), or null.</summary>
    public (int Rung, IReadOnlyList<int> Path)? ElementAt(Point p)
    {
        foreach (var (area, rung, path) in _hits)
            if (area.Contains(p)) return (rung, path);
        return null;
    }

    public LadderView(Canvas canvas)
    {
        _canvas = canvas;
        _on = (Brush)Application.Current.Resources["PowerOnBrush"];
        _off = (Brush)Application.Current.Resources["PowerOffBrush"];
        _ink = (Brush)Application.Current.Resources["InkBrush"];
        _muted = (Brush)Application.Current.Resources["MutedTextBrush"];
    }

    public void Show(PlcProject project, LadderViewOptions? options = null)
    {
        _options = options ?? new LadderViewOptions();
        _canvas.Children.Clear();
        _updates.Clear();
        _hits.Clear();
        var rungs = project.Programs.SelectMany(p => p.Rungs).ToList();
        var width = Math.Max(4, rungs.Select(r => Measure(r.Logic).W).DefaultIfEmpty(1).Max()) * CellW + Left + 30;

        double y = 8;
        var top = y;
        for (var i = 0; i < rungs.Count; i++)
        {
            _rung = i;
            var rung = rungs[i];
            var rungSelected = _options.Selection is { IsRung: true } rs && rs.Rung == i;
            var comment = Text($"Rung {i + 1}" + (rung.Comment is null ? "" : $" — {rung.Comment}"), Left, y, rungSelected ? SelectStroke : _muted, 12.5, width - Left - Rail, bold: rungSelected);
            comment.Measure(new Size(comment.Width, double.PositiveInfinity));
            if (_options.RungClicked is { } rungClicked)
            {
                var index = i;
                comment.Cursor = Cursors.Hand;
                comment.ToolTip = "Selects this rung (to move, copy or delete it, or edit its comment)";
                comment.MouseLeftButtonDown += (_, e) => { rungClicked(index); e.Handled = true; };
            }
            y += comment.DesiredSize.Height + 4;
            foreach (var d in _options.Diagnostics.Where(d => d.Rung == i + 1 && d.ElementPath.Length == 0))
            {
                var note = Text($"{(d.Severity == Severity.Error ? "✗" : "⚠")} {d.Message}", Left, y, Problem, 12, width - Left - Rail);
                note.Measure(new Size(note.Width, double.PositiveInfinity));
                y += note.DesiredSize.Height + 2;
            }
            y += 4;

            var size = Measure(rung.Logic);
            Func<PlcRuntime, bool> powerOut;
            if (rung.Logic is SeriesElement { Items.Count: 0 })
            {
                var hint = Text("Empty rung: add a contact (O) or a coil (Q) from the tools above.", Left + 10, y + Mid - 10, _muted, 12.5, width - Left - 40);
                hint.FontStyle = FontStyles.Italic;
                powerOut = _ => true;
            }
            else
            {
                powerOut = Draw(rung.Logic, Left, y, _ => true, []);
            }
            var endX = Left + Math.Max(1, size.W) * CellW;
            Wire(endX, y + Mid, width - Rail, y + Mid, powerOut);
            y += Math.Max(1, size.H) * CellH + 10;
        }
        // The rails: power comes in on the left, returns on the right.
        _canvas.Children.Add(new Line { X1 = Rail, Y1 = top, X2 = Rail, Y2 = y, Stroke = _on, StrokeThickness = 4 });
        _canvas.Children.Add(new Line { X1 = width - Rail, Y1 = top, X2 = width - Rail, Y2 = y, Stroke = _ink, StrokeThickness = 2 });
        _canvas.Width = width;
        _canvas.Height = y + 8;
    }

    public void Update(PlcRuntime runtime)
    {
        foreach (var u in _updates) u(runtime);
    }

    private static (int W, int H) Measure(LadderElement e) => e switch
    {
        SeriesElement s => s.Items.Count == 0 ? (1, 1) : (s.Items.Sum(i => Measure(i).W), s.Items.Max(i => Measure(i).H)),
        ParallelElement p => p.Branches.Count == 0 ? (1, 1) : (p.Branches.Max(b => Measure(b).W), p.Branches.Sum(b => Measure(b).H)),
        BoxElement => (2, 2),
        _ => (1, 1),
    };

    /// <summary>Draws an element with its top-left at (x, y); returns a function that says whether power flows out of it.</summary>
    private Func<PlcRuntime, bool> Draw(LadderElement e, double x, double y, Func<PlcRuntime, bool> powerIn, IReadOnlyList<int> path)
    {
        switch (e)
        {
            case SeriesElement s:
            {
                // path is the series' own path: its items are path + [i].
                var p = powerIn;
                for (var i = 0; i < s.Items.Count; i++)
                {
                    p = Draw(s.Items[i], x, y, p, [.. path, i]);
                    x += Measure(s.Items[i]).W * CellW;
                }
                return p;
            }
            case ParallelElement par:
            {
                // path is the parallel's path: branch b is the series at path + [b].
                var w = Measure(par).W * CellW;
                var h = Measure(par).H * CellH;
                if (_options.Selection?.Contains(_rung, path) == true)
                {
                    var sel = new Rectangle { Width = w + 8, Height = h - 4, Fill = SelectFill, Stroke = SelectStroke, StrokeThickness = 3, RadiusX = 8, RadiusY = 8 };
                    Canvas.SetLeft(sel, x - 4);
                    Canvas.SetTop(sel, y - 4);
                    _canvas.Children.Add(sel);
                }
                var by = y;
                var lastMid = y + Mid;
                for (var b = 0; b < par.Branches.Count; b++)
                {
                    var branch = par.Branches[b];
                    var bw = Measure(branch).W * CellW;
                    var outFn = branch is SeriesElement
                        ? Draw(branch, x, by, powerIn, [.. path, b])
                        : Draw(branch, x, by, powerIn, [.. path, b, 0]);
                    if (bw < w) Wire(x + bw, by + Mid, x + w, by + Mid, outFn);
                    lastMid = by + Mid;
                    by += Measure(branch).H * CellH;
                }
                Wire(x, y + Mid, x, lastMid, powerIn);
                Func<PlcRuntime, bool> result = rt => rt.PowerOut(par);
                Wire(x + w, y + Mid, x + w, lastMid, result);
                if (_options.ElementClicked is { } clicked)
                {
                    // The branch's right-hand side selects the whole branch.
                    var rung = _rung;
                    var strip = new Rectangle
                    {
                        Width = 16, Height = lastMid - (y + Mid) + 16, Fill = Brushes.Transparent, Cursor = Cursors.Hand,
                        ToolTip = "Selects the whole branch: what you add next goes after it; Delete removes it; Branch adds another path",
                    };
                    strip.MouseLeftButtonDown += (_, e) => { clicked(rung, path); e.Handled = true; };
                    Canvas.SetLeft(strip, x + w - 8);
                    Canvas.SetTop(strip, y + Mid - 8);
                    _canvas.Children.Add(strip);
                }
                return result;
            }
            case ContactElement c:
                return Symbol(x, y, c.Tag, ContactGlyph(c.Contact), powerIn, rt => rt.PowerOut(c), DescribeContact(c), path);
            case CoilElement coil:
                return Symbol(x, y, coil.Tag, CoilGlyph(coil.Coil), powerIn, rt => rt.PowerOut(coil), DescribeCoil(coil), path);
            case BoxElement box:
                return Box(box, x, y, powerIn, path);
            default:
                return powerIn;
        }
    }

    private Func<PlcRuntime, bool> Symbol(double x, double y, string tag, string glyph, Func<PlcRuntime, bool> powerIn,
        Func<PlcRuntime, bool> powerOut, string description, IReadOnlyList<int> path)
    {
        var problems = Marks(x, y, CellW, CellH - 6, path);
        var cy = y + Mid;
        Wire(x, cy, x + 40, cy, powerIn);
        Wire(x + 80, cy, x + CellW, cy, powerOut);

        var missing = string.IsNullOrWhiteSpace(tag);
        var label = Text(missing ? "(tag?)" : tag, x, y + 2, missing ? Problem : _ink, 13, CellW, center: true, bold: true);
        var symbol = Text(glyph, x + 34, cy - 14, _off, 20, 52, center: true, bold: true);
        symbol.FontFamily = new FontFamily("Consolas");
        var value = Text("", x, cy + 14, _muted, 12, CellW, center: true);
        AutomationProperties.SetName(symbol, description + (problems.Count > 0 ? ". Problem: " + string.Join(" ", problems) : ""));
        _ = label;

        _updates.Add(rt =>
        {
            var on = powerOut(rt);
            symbol.Foreground = on ? _on : _off;
            var v = SafeRead(rt, tag);
            var forced = !missing && rt.Forces.TryGetValue(tag, out var f);
            value.Text = forced ? $"FORCED {rt.Forces[tag]}" : v;
            value.Foreground = forced ? Forced : _muted;
            value.FontWeight = forced ? FontWeights.Bold : FontWeights.Normal;
            AutomationProperties.SetName(symbol, $"{description}: {value.Text}, {(on ? "passes power" : "no power")}");
        });
        Hit(x, y, CellW, CellH - 6, path, problems, missing ? null : tag);
        return powerOut;
    }

    private Func<PlcRuntime, bool> Box(BoxElement box, double x, double y, Func<PlcRuntime, bool> powerIn, IReadOnlyList<int> path)
    {
        var problems = Marks(x, y, 2 * CellW, 2 * CellH - 6, path);
        var cy = y + Mid;
        Func<PlcRuntime, bool> powerOut = rt => rt.PowerOut(box);
        Wire(x, cy, x + 30, cy, powerIn);
        Wire(x + 2 * CellW - 30, cy, x + 2 * CellW, cy, powerOut);
        var rect = new Rectangle { Width = 2 * CellW - 60, Height = 2 * CellH - 30, Stroke = _off, StrokeThickness = 2 };
        Canvas.SetLeft(rect, x + 30);
        Canvas.SetTop(rect, y + 14);
        _canvas.Children.Add(rect);
        Text(box.Instance ?? "", x + 30, y - 4, _ink, 13, 2 * CellW - 60, center: true, bold: true);
        Text(box.Instruction, x + 30, y + 18, _ink, 14, 2 * CellW - 60, center: true, bold: true);
        var ops = string.Join("\n", box.Operands.Select(o => $"{o.Key}: {o.Value}"));
        Text(ops, x + 38, y + 40, _muted, 12, 2 * CellW - 76);
        var live = Text("", x + 38, y + 2 * CellH - 40, _ink, 12, 2 * CellW - 76);
        _updates.Add(rt =>
        {
            rect.Stroke = powerOut(rt) ? _on : _off;
            rect.StrokeThickness = powerOut(rt) ? 4 : 2;
            var fb = box.Instance is null ? null : rt.InstanceOf(box.Instance);
            live.Text = fb is null ? "" : string.Join("  ", fb.Members.Where(m => m.Key is "Q" or "ET" or "CV").Select(m => $"{m.Key}={m.Value}"));
        });
        Hit(x, y, 2 * CellW, 2 * CellH - 6, path, problems, null);
        return powerOut;
    }

    /// <summary>The selection highlight and the problem marks behind an element; returns its problems.</summary>
    private List<string> Marks(double x, double y, double w, double h, IReadOnlyList<int> path)
    {
        if (_options.Selection?.Contains(_rung, path) == true)
        {
            var sel = new Rectangle { Width = w - 4, Height = h, Fill = SelectFill, Stroke = SelectStroke, StrokeThickness = 3, RadiusX = 6, RadiusY = 6 };
            Canvas.SetLeft(sel, x + 2);
            Canvas.SetTop(sel, y - 2);
            _canvas.Children.Add(sel);
        }
        var key = "1." + string.Join('.', path.Select(p => p + 1));
        var problems = _options.Diagnostics.Where(d => d.Rung == _rung + 1 && d.ElementPath == key).Select(d => d.Message).ToList();
        if (problems.Count > 0)
        {
            var box = new Rectangle { Width = w - 10, Height = h - 6, Stroke = Problem, StrokeThickness = 2, StrokeDashArray = [3, 2], RadiusX = 4, RadiusY = 4 };
            Canvas.SetLeft(box, x + 5);
            Canvas.SetTop(box, y + 1);
            _canvas.Children.Add(box);
            var badge = Text("!", x + w - 22, y - 2, Brushes.White, 13, 16, center: true, bold: true);
            badge.Background = Problem;
        }
        return problems;
    }

    /// <summary>The clickable area over an element: selects it; right-click forces a wired tag.</summary>
    private void Hit(double x, double y, double w, double h, IReadOnlyList<int> path, List<string> problems, string? tag)
    {
        _hits.Add((new Rect(x, y - 2, w, h), _rung, path));
        if (_options.ElementClicked is null && _options.Force is null) return;
        var hit = new Rectangle { Width = w, Height = h, Fill = Brushes.Transparent };
        if (problems.Count > 0) hit.ToolTip = string.Join("\n", problems);
        if (_options.ElementClicked is { } clicked)
        {
            var rung = _rung;
            hit.Cursor = Cursors.Hand;
            hit.MouseLeftButtonDown += (_, e) => { clicked(rung, path); e.Handled = true; };
        }
        if (tag is not null && _options.Force is { } force && _options.CanForce?.Invoke(tag) == true)
        {
            var menu = new ContextMenu();
            void Add(string header, bool? value, string tip)
            {
                var item = new MenuItem { Header = header, ToolTip = tip };
                item.Click += (_, _) => force(tag, value);
                menu.Items.Add(item);
            }
            Add($"Force {tag} _ON", true, "The PLC uses TRUE for this tag whatever the machine does, until the force is removed");
            Add($"Force {tag} _OFF", false, "The PLC uses FALSE for this tag whatever the machine does, until the force is removed");
            Add("_Remove the force", null, "The tag follows the machine again");
            hit.ContextMenu = menu;
            hit.ToolTip ??= $"Right-click to force {tag} on or off";
        }
        Canvas.SetLeft(hit, x);
        Canvas.SetTop(hit, y - 2);
        _canvas.Children.Add(hit);
    }

    private static string SafeRead(PlcRuntime rt, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return "";
        try { return rt.Read(tag).ToString(); }
        catch (PlcFaultException) { return "?"; }
    }

    private void Wire(double x1, double y1, double x2, double y2, Func<PlcRuntime, bool> powered)
    {
        var line = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = _off, StrokeThickness = 2 };
        _canvas.Children.Add(line);
        _updates.Add(rt =>
        {
            var on = powered(rt);
            line.Stroke = on ? _on : _off;
            line.StrokeThickness = on ? 4 : 2;
        });
    }

    private TextBlock Text(string text, double x, double y, Brush brush, double size, double width, bool center = false, bool bold = false)
    {
        var t = new TextBlock
        {
            Text = text, Foreground = brush, FontSize = size, Width = width,
            TextAlignment = center ? TextAlignment.Center : TextAlignment.Left,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
        };
        Canvas.SetLeft(t, x);
        Canvas.SetTop(t, y);
        _canvas.Children.Add(t);
        return t;
    }

    private static string ContactGlyph(ContactKind k) => k switch
    {
        ContactKind.NormallyClosed => "|/|",
        ContactKind.PositiveEdge => "|P|",
        ContactKind.NegativeEdge => "|N|",
        _ => "| |",
    };

    private static string CoilGlyph(CoilKind k) => k switch
    {
        CoilKind.Negated => "(/)",
        CoilKind.Set => "(S)",
        CoilKind.Reset => "(R)",
        _ => "( )",
    };

    private static string DescribeContact(ContactElement c) => $"{LadderEditor.Words(c.Contact)} contact {c.Tag}";
    private static string DescribeCoil(CoilElement c) => $"{LadderEditor.Words(c.Coil)} coil {c.Tag}";
}
