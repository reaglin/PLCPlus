using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Learning.Core.Content;
using Plc.Engine;

namespace Learning.Shell;

/// <summary>
/// The live parts a lesson page can hold (spec 6.3, plan 4.5), each written on a line of its own:
/// <c>[[ladder: programs/motor-seal-in.json]]</c> draws a program's rungs, and
/// <c>[[truth-table: (Start OR Motor) AND Stop]]</c> lists every row of a logic expression.
/// Plc.Grading's PackageChecks checks them before the content is published.
/// </summary>
internal static class LessonWidgets
{
    public static UIElement? Create(PlcContent content, WidgetTag w)
    {
        try
        {
            return w.Name switch
            {
                // A wide ladder scrolls sideways rather than losing its coils (larger text, a narrow window).
                "ladder" => new ScrollViewer { Content = Ladder(content, w.Argument), HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false },
                "truth-table" => TruthTable(BooleanExpression.Parse(w.Argument)),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ContentException)
        {
            return null; // shown as "this page asks for … that PLC+ cannot show"
        }
    }

    private static Brush Line => (Brush)Application.Current.FindResource("LineBrush");
    private static Brush Muted => (Brush)Application.Current.FindResource("MutedTextBrush");
    private static Brush On => (Brush)Application.Current.FindResource("PowerOnBrush");

    private static UIElement Ladder(PlcContent content, string file)
    {
        var project = ProjectJson.Deserialize(content.Read(file));
        var canvas = new Canvas { Background = Brushes.White };
        new LadderView(canvas).Show(project);
        AutomationProperties.SetName(canvas, "A ladder diagram: " + string.Join(" ", project.Programs.SelectMany(p => p.Rungs).Select((r, i) => $"Rung {i + 1}: {r.Comment}")));
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(new Border { Child = canvas, BorderBrush = Line, BorderThickness = new Thickness(1), Padding = new Thickness(6), Background = Brushes.White });
        panel.Children.Add(new TextBlock
        {
            Text = "Drawn as on the Example tab, but not running: every part shows as off.",
            FontSize = 12, Foreground = Muted, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap,
        });
        return panel;
    }

    private static UIElement TruthTable(BooleanExpression e)
    {
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
        var columns = e.Variables.Count + 1;
        for (var c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        void Cell(int row, int col, string text, bool header = false, bool result = false, bool on = false)
        {
            while (grid.RowDefinitions.Count <= row) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var block = new TextBlock
            {
                Text = text,
                FontWeight = header || (result && on) ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = result ? (on ? On : Muted) : Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            var border = new Border
            {
                Child = block, Padding = new Thickness(12, 4, 12, 4), BorderBrush = Line,
                BorderThickness = new Thickness(0, 0, col == columns - 2 ? 2 : 1, 1),
                Background = header ? new SolidColorBrush(Color.FromRgb(0xEE, 0xF1, 0xF4)) : Brushes.White,
            };
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            grid.Children.Add(border);
        }

        for (var v = 0; v < e.Variables.Count; v++) Cell(0, v, e.Variables[v], header: true);
        Cell(0, columns - 1, e.Text, header: true);
        var r = 1;
        var spoken = new List<string>();
        foreach (var (inputs, result) in e.Rows())
        {
            for (var v = 0; v < inputs.Length; v++) Cell(r, v, inputs[v] ? "TRUE" : "FALSE");
            Cell(r, columns - 1, result ? "TRUE ✓" : "FALSE", result: true, on: result);
            spoken.Add($"{string.Join(", ", e.Variables.Select((n, i) => $"{n} {(inputs[i] ? "true" : "false")}"))}: {(result ? "true" : "false")}");
            r++;
        }

        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(new TextBlock { Text = "Truth table: " + e.Text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        panel.Children.Add(new Border { Child = grid, BorderBrush = Line, BorderThickness = new Thickness(1, 1, 0, 0) });
        AutomationProperties.SetName(panel, $"Truth table for {e.Text}. " + string.Join(". ", spoken));
        return panel;
    }
}
