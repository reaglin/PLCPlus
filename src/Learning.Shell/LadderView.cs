using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Plc.Engine;

namespace Learning.Shell;

/// <summary>
/// Draws a program's rungs on a canvas and shows power flow live. A first, read-only view: the
/// ladder editor (plan phase 3) replaces it. Drawn with WPF shapes (not a bitmap canvas) so the
/// words stay readable by a screen reader.
/// Power is shown three ways, never by colour alone: green and thick, and the value TRUE.
/// </summary>
internal sealed class LadderView
{
    private const double CellW = 120, CellH = 78, Mid = 34, Rail = 12, Left = 24;

    private readonly Canvas _canvas;
    private readonly List<Action<PlcRuntime>> _updates = [];
    private readonly Brush _on, _off, _ink, _muted;

    public LadderView(Canvas canvas)
    {
        _canvas = canvas;
        _on = (Brush)Application.Current.Resources["PowerOnBrush"];
        _off = (Brush)Application.Current.Resources["PowerOffBrush"];
        _ink = (Brush)Application.Current.Resources["InkBrush"];
        _muted = (Brush)Application.Current.Resources["MutedTextBrush"];
    }

    public void Show(PlcProject project)
    {
        _canvas.Children.Clear();
        _updates.Clear();
        var rungs = project.Programs.SelectMany(p => p.Rungs).ToList();
        var width = rungs.Select(r => Measure(r.Logic).W).DefaultIfEmpty(1).Max() * CellW + Left + 30;

        double y = 8;
        var top = y;
        for (var i = 0; i < rungs.Count; i++)
        {
            var rung = rungs[i];
            var comment = Text($"Rung {i + 1}" + (rung.Comment is null ? "" : $" — {rung.Comment}"), Left, y, _muted, 12.5, width - Left - Rail);
            comment.Measure(new Size(comment.Width, double.PositiveInfinity));
            y += comment.DesiredSize.Height + 8;
            var size = Measure(rung.Logic);
            var powerOut = Draw(rung.Logic, Left, y, _ => true);
            var endX = Left + size.W * CellW;
            Wire(endX, y + Mid, width - Rail, y + Mid, powerOut);
            y += size.H * CellH + 10;
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
    private Func<PlcRuntime, bool> Draw(LadderElement e, double x, double y, Func<PlcRuntime, bool> powerIn)
    {
        switch (e)
        {
            case SeriesElement s:
            {
                var p = powerIn;
                foreach (var item in s.Items)
                {
                    p = Draw(item, x, y, p);
                    x += Measure(item).W * CellW;
                }
                return p;
            }
            case ParallelElement par:
            {
                var w = Measure(par).W * CellW;
                var by = y;
                var lastMid = y + Mid;
                foreach (var b in par.Branches)
                {
                    var bw = Measure(b).W * CellW;
                    var outFn = Draw(b, x, by, powerIn);
                    if (bw < w) Wire(x + bw, by + Mid, x + w, by + Mid, outFn);
                    lastMid = by + Mid;
                    by += Measure(b).H * CellH;
                }
                Wire(x, y + Mid, x, lastMid, powerIn);
                Func<PlcRuntime, bool> result = rt => rt.PowerOut(par);
                Wire(x + w, y + Mid, x + w, lastMid, result);
                return result;
            }
            case ContactElement c:
                return Symbol(x, y, c.Tag, ContactGlyph(c.Contact), powerIn, rt => rt.PowerOut(c), DescribeContact(c));
            case CoilElement coil:
                return Symbol(x, y, coil.Tag, CoilGlyph(coil.Coil), powerIn, rt => rt.PowerOut(coil), DescribeCoil(coil));
            case BoxElement box:
                return Box(box, x, y, powerIn);
            default:
                return powerIn;
        }
    }

    private Func<PlcRuntime, bool> Symbol(double x, double y, string tag, string glyph, Func<PlcRuntime, bool> powerIn,
        Func<PlcRuntime, bool> powerOut, string description)
    {
        var cy = y + Mid;
        Wire(x, cy, x + 40, cy, powerIn);
        Wire(x + 80, cy, x + CellW, cy, powerOut);

        Text(tag, x, y + 2, _ink, 13, CellW, center: true, bold: true);
        var symbol = Text(glyph, x + 34, cy - 14, _off, 20, 52, center: true, bold: true);
        symbol.FontFamily = new FontFamily("Consolas");
        var value = Text("", x, cy + 14, _muted, 12, CellW, center: true);
        AutomationProperties.SetName(symbol, description);

        _updates.Add(rt =>
        {
            var on = powerOut(rt);
            symbol.Foreground = on ? _on : _off;
            var v = SafeRead(rt, tag);
            value.Text = v;
            AutomationProperties.SetName(symbol, $"{description}: {v}, {(on ? "passes power" : "no power")}");
        });
        return powerOut;
    }

    private Func<PlcRuntime, bool> Box(BoxElement box, double x, double y, Func<PlcRuntime, bool> powerIn)
    {
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
            live.Text = fb is null ? "" : string.Join("  ", fb.Members.Where(m => !m.Key.StartsWith('_') && m.Key is "Q" or "ET" or "CV").Select(m => $"{m.Key}={m.Value}"));
        });
        return powerOut;
    }

    private static string SafeRead(PlcRuntime rt, string tag)
    {
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

    private static string DescribeContact(ContactElement c) => c.Contact switch
    {
        ContactKind.NormallyClosed => $"Normally closed contact {c.Tag}",
        ContactKind.PositiveEdge => $"Rising-edge contact {c.Tag}",
        ContactKind.NegativeEdge => $"Falling-edge contact {c.Tag}",
        _ => $"Normally open contact {c.Tag}",
    };

    private static string DescribeCoil(CoilElement c) => c.Coil switch
    {
        CoilKind.Negated => $"Negated coil {c.Tag}",
        CoilKind.Set => $"Set coil {c.Tag}",
        CoilKind.Reset => $"Reset coil {c.Tag}",
        _ => $"Coil {c.Tag}",
    };
}
