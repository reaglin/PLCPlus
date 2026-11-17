using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Learning.Shell;

/// <summary>
/// The figure of a <c>[[try-logic: …]]</c> example (Ron, 2026-11-17: an Example "always contains some type of figure"): the expression
/// drawn as a ladder rung that follows the switches. AND is contacts in series, OR is branches side by side, NOT of a name is a normally
/// closed contact, XOR is its two paths (A AND NOT B, or NOT A AND B), and a NOT of a bracket is turned inward (De Morgan) so that every
/// contact reads one input. A part that passes power is green and thick, and says TRUE.
/// </summary>
internal static class LogicRung
{
    private abstract record Node;
    private sealed record Contact(string Name, bool Closed) : Node;   // Closed = normally closed
    private sealed record Series(IReadOnlyList<Node> Parts) : Node;
    private sealed record Branches(IReadOnlyList<Node> Paths) : Node;

    private const double CW = 104, CH = 74, LineAt = 36;                 // one contact's cell, and its rung line from the cell's top
    private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(0x22, 0x26, 0x2B));

    /// <summary>The rung for an expression, or null if it cannot be read (the truth table still shows).</summary>
    public static (Canvas Canvas, Action<IReadOnlyDictionary<string, bool>, bool> Show)? Create(string expression)
    {
        Node root;
        try
        {
            var tokens = Tokens(expression);
            var at = 0;
            root = Or(tokens, ref at);
            if (at != tokens.Count) return null;
        }
        catch (FormatException)
        {
            return null;
        }
        var (w, h) = Size(root);
        var canvas = new Canvas { Width = w + 150, Height = h + 18, Background = Brushes.White };
        AutomationProperties.SetName(canvas, $"The same logic as a ladder rung: {expression}");
        void Show(IReadOnlyDictionary<string, bool> values, bool result)
        {
            canvas.Children.Clear();
            var line = 14 + LineAt;
            // Left power rail: always powered. Right rail: the return.
            Wire(canvas, 10, 4, 10, h + 12, true, 4);
            Wire(canvas, w + 140, 4, w + 140, h + 12, false, 4);
            Wire(canvas, 10, line, 20, line, true);
            var output = Draw(canvas, root, 20, 14, true, values);
            Wire(canvas, 20 + w, line, w + 62, line, output);
            // The coil.
            var arc = output ? TryWidgets.On : Ink;
            foreach (var (x1, sweep) in new[] { (w + 62.0, SweepDirection.Counterclockwise), (w + 98.0, SweepDirection.Clockwise) })
            {
                var fig = new PathFigure { StartPoint = new Point(x1 + (x1 < w + 80 ? 8 : -8), line - 12) };
                fig.Segments.Add(new ArcSegment(new Point(x1 + (x1 < w + 80 ? 8 : -8), line + 12), new Size(14, 14), 0, false, sweep, true));
                canvas.Children.Add(new Path { Data = new PathGeometry([fig]), Stroke = arc, StrokeThickness = output ? 3.5 : 2 });
            }
            Wire(canvas, w + 62, line, w + 70, line, output);
            Wire(canvas, w + 90, line, w + 140, line, false);
            Label(canvas, "Result", w + 80, line - 34, 13, true, centre: true);
            Label(canvas, output ? "TRUE" : "FALSE", w + 80, line + 14, 12, output, centre: true, brush: output ? TryWidgets.On : TryWidgets.Muted);
        }
        return (canvas, Show);
    }

    // ---- reading the expression --------------------------------------------------------------------------

    private static List<string> Tokens(string text)
    {
        var list = new List<string>();
        for (var i = 0; i < text.Length;)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c is '(' or ')') { list.Add(c.ToString()); i++; continue; }
            if (!char.IsLetterOrDigit(c) && c != '_') throw new FormatException();
            var start = i;
            while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
            list.Add(text[start..i]);
        }
        return list;
    }

    private static bool Take(List<string> t, ref int at, string word)
    {
        if (at < t.Count && string.Equals(t[at], word, StringComparison.OrdinalIgnoreCase)) { at++; return true; }
        return false;
    }

    private static Node Or(List<string> t, ref int at)
    {
        var paths = new List<Node> { Xor(t, ref at) };
        while (Take(t, ref at, "OR")) paths.Add(Xor(t, ref at));
        return paths.Count == 1 ? paths[0] : new Branches(paths);
    }

    private static Node Xor(List<string> t, ref int at)
    {
        var left = And(t, ref at);
        while (Take(t, ref at, "XOR"))
        {
            var right = And(t, ref at);
            left = new Branches([Join(left, Negate(right)), Join(Negate(left), right)]);
        }
        return left;
    }

    private static Node And(List<string> t, ref int at)
    {
        var parts = new List<Node> { Not(t, ref at) };
        while (Take(t, ref at, "AND")) parts.Add(Not(t, ref at));
        return parts.Count == 1 ? parts[0] : new Series(parts);
    }

    private static Node Not(List<string> t, ref int at)
    {
        if (Take(t, ref at, "NOT")) return Negate(Not(t, ref at));
        if (Take(t, ref at, "("))
        {
            var inner = Or(t, ref at);
            if (!Take(t, ref at, ")")) throw new FormatException();
            return inner;
        }
        if (at >= t.Count || t[at] is "(" or ")") throw new FormatException();
        return new Contact(t[at++], false);
    }

    private static Node Join(Node a, Node b) => new Series([.. a is Series sa ? sa.Parts : [a], .. b is Series sb ? sb.Parts : [b]]);

    /// <summary>NOT, pushed down to the contacts: NOT (A AND B) = NOT A OR NOT B, and the other way round.</summary>
    private static Node Negate(Node n) => n switch
    {
        Contact c => c with { Closed = !c.Closed },
        Series s => new Branches([.. s.Parts.Select(Negate)]),
        Branches b => new Series([.. b.Paths.Select(Negate)]),
        _ => n,
    };

    // ---- drawing -----------------------------------------------------------------------------------------

    private static (double W, double H) Size(Node n) => n switch
    {
        Contact => (CW, CH),
        Series s => (s.Parts.Sum(p => Size(p).W), s.Parts.Max(p => Size(p).H)),
        Branches b => (b.Paths.Max(p => Size(p).W) + 16, b.Paths.Sum(p => Size(p).H)),
        _ => (0, 0),
    };

    /// <summary>Draws a node with its top-left at (x, y); its rung line is y + LineAt. Returns whether power comes out of it.</summary>
    private static bool Draw(Canvas c, Node n, double x, double y, bool power, IReadOnlyDictionary<string, bool> values)
    {
        var line = y + LineAt;
        switch (n)
        {
            case Contact k:
            {
                var on = values.TryGetValue(k.Name, out var v) && v;
                var passes = k.Closed ? !on : on;
                var lit = power && passes;
                Wire(c, x, line, x + 39, line, power);
                var plate = lit ? TryWidgets.On : Ink;
                Wire(c, x + 39, line - 13, x + 39, line + 13, lit, lit ? 3.5 : 2, plate);
                Wire(c, x + 65, line - 13, x + 65, line + 13, lit, lit ? 3.5 : 2, plate);
                if (k.Closed) Wire(c, x + 35, line + 12, x + 69, line - 12, lit, lit ? 3 : 1.8, plate);
                Wire(c, x + 65, line, x + CW, line, lit);
                Label(c, $"{k.Name} = {(on ? "TRUE" : "FALSE")}", x + CW / 2, y + 2, 12.5, true, centre: true);
                Label(c, (k.Closed ? "NC, " : "") + (passes ? "closed" : "open") + (lit ? ": power" : ""), x + CW / 2, line + 13, 11.5, lit, centre: true, brush: lit ? TryWidgets.On : TryWidgets.Muted);
                return lit;
            }
            case Series s:
            {
                var p = power;
                var at = x;
                foreach (var part in s.Parts)
                {
                    p = Draw(c, part, at, y, p, values);
                    at += Size(part).W;
                }
                return p;
            }
            case Branches b:
            {
                var (w, _) = Size(b);
                var top = y;
                var lines = new List<double>();
                var any = false;
                foreach (var path in b.Paths)
                {
                    var (pw, ph) = Size(path);
                    var pl = top + LineAt;
                    lines.Add(pl);
                    Wire(c, x, pl, x + 8, pl, power);
                    var outp = Draw(c, path, x + 8, top, power, values);
                    Wire(c, x + 8 + pw, pl, x + w, pl, outp);
                    any |= outp;
                    top += ph;
                }
                Wire(c, x, lines[0], x, lines[^1], power);
                Wire(c, x + w, lines[0], x + w, lines[^1], any);
                return any;
            }
        }
        return false;
    }

    private static void Wire(Canvas c, double x1, double y1, double x2, double y2, bool live, double? thick = null, Brush? brush = null) =>
        c.Children.Add(new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush ?? (live ? TryWidgets.On : Ink),
            StrokeThickness = thick ?? (live ? 3.5 : 1.8), StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
        });

    private static void Label(Canvas c, string text, double x, double y, double size, bool bold, bool centre = false, Brush? brush = null)
    {
        var t = new TextBlock { Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Foreground = brush ?? Ink };
        t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(t, centre ? x - t.DesiredSize.Width / 2 : x);
        Canvas.SetTop(t, y);
        c.Children.Add(t);
    }
}
