using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Plc.Scenes;

namespace Learning.Shell;

/// <summary>
/// Draws any scene from its file and animates it (plan 2.9). Buttons and switches are real WPF controls,
/// so they work from the keyboard and a screen reader; every part also says what it is doing in words
/// (its StateText), shown beside it and in the list under the drawing — never colour alone.
/// </summary>
internal sealed class SceneView
{
    private readonly Canvas _canvas;
    private readonly ItemsControl _stateList;
    private readonly List<Action> _updates = [];
    private readonly List<(PushButton Model, ButtonBase Button, CheckBox Hold)> _momentary = [];
    private readonly ObservableCollection<string> _states = [];
    private Scene _scene = new();

    private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(0x1B, 0x24, 0x30));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x4A, 0x55, 0x63));
    private static readonly Brush Steel = new SolidColorBrush(Color.FromRgb(0x9A, 0xA5, 0xB1));
    private static readonly Brush Water = new SolidColorBrush(Color.FromRgb(0x3A, 0x7B, 0xD5));
    private static readonly Brush Made = new SolidColorBrush(Color.FromRgb(0x1E, 0x7B, 0x34));
    private static readonly Brush NotMade = new SolidColorBrush(Color.FromRgb(0xC9, 0xD0, 0xD9));

    public SceneView(Canvas canvas, ItemsControl stateList)
    {
        _canvas = canvas;
        _stateList = stateList;
        _stateList.ItemsSource = _states;
    }

    public void Show(Scene scene)
    {
        _scene = scene;
        _canvas.Children.Clear();
        _updates.Clear();
        _momentary.Clear();
        _canvas.Width = scene.Width;
        _canvas.Height = scene.Height;
        AutomationProperties.SetName(_canvas, $"{scene.Title}, animated");

        // Process elements first, so buttons and labels sit on top of them.
        foreach (var c in scene.Components.OrderBy(c => c is IOperatorInput ? 1 : 0))
        {
            switch (c)
            {
                case PushButton b: DrawPushButton(b); break;
                case SelectorSwitch s: DrawToggle(s, () => $"{s.Label}: {Word(s, s.On)}", () => $"Turns the switch to {Word(s, !s.On)}", 130,
                    $"{s.Label} switch; pressed means {Word(s, true)}, not pressed means {Word(s, false)}"); break;
                case EmergencyStop e: DrawToggle(e, () => e.Pressed ? "E-STOP pressed" : "E-STOP", () => e.Pressed ? "Pulls the emergency stop out (releases it)" : "Presses the emergency stop; it stays pressed until you click again", 110,
                    $"{e.Label} emergency stop"); break;
                case SafetyEye eye: DrawSafetyEye(eye); break;
                case PilotLight l: DrawLight(l); break;
                case Horn h: DrawText(h, h.X, h.Y); break;
                case Motor m: DrawMotor(m); break;
                case Tank t: DrawTank(t); break;
                case SolenoidValve v: DrawValve(v); break;
                case FloatSwitch f: DrawFloat(f); break;
                case GarageDoor d: DrawDoor(d); break;
                case LimitSwitch ls: DrawSensorBox(ls, ls.X, ls.Y); break;
                case Conveyor belt: DrawConveyor(belt); break;
                case Diverter dv: DrawDiverter(dv); break;
                case PhotoEye pe: DrawBeltSensor(pe); break;
            }
        }
        Update();
    }

    /// <summary>Reads the momentary buttons (held down, or Keep pressed ticked) into the scene.</summary>
    public void ReadOperatorInputs()
    {
        foreach (var (model, button, hold) in _momentary)
            model.Operate(button.IsPressed || hold.IsChecked == true);
    }

    public void Update()
    {
        foreach (var u in _updates) u();
        // Change only the lines whose words changed, so a screen reader keeps its place in the list.
        var now = _scene.Components.Select(c => c.StateText).ToList();
        while (_states.Count > now.Count) _states.RemoveAt(_states.Count - 1);
        for (var i = 0; i < now.Count; i++)
        {
            if (i >= _states.Count) _states.Add(now[i]);
            else if (_states[i] != now[i]) _states[i] = now[i];
        }
    }

    // ---- Operator inputs ------------------------------------------------------------------------------

    private void DrawPushButton(PushButton b)
    {
        var button = new Button
        {
            Content = b.Label, Width = 92, Height = 46, FontSize = 15, FontWeight = FontWeights.SemiBold,
            Background = b.Color == "red" ? new SolidColorBrush(Color.FromRgb(0xB3, 0x26, 0x1E)) : new SolidColorBrush(Color.FromRgb(0x2E, 0x8B, 0x47)),
            Foreground = Brushes.White, Margin = new Thickness(0),
            ToolTip = b.NormallyClosed
                ? $"{b.Label} push button (normally closed): its input is TRUE until you press it. Hold it down with the mouse, or Tab to it and hold the Space bar."
                : $"{b.Label} push button (normally open): its input is TRUE while you hold it down. Hold it down with the mouse, or Tab to it and hold the Space bar.",
        };
        AutomationProperties.SetHelpText(button, "Hold it down to press it: hold the Space bar, or tick Keep pressed below it.");
        Place(button, b.X, b.Y);
        var hold = new CheckBox
        {
            Content = "keep pressed", FontSize = 12,
            ToolTip = $"While ticked, {b.Label} stays pressed — useful with Pause and Step one scan",
        };
        AutomationProperties.SetName(hold, $"Keep {b.Label} pressed");
        Place(hold, b.X, b.Y + 50);
        _momentary.Add((b, button, hold));
    }

    private void DrawToggle(SceneComponent c, Func<string> text, Func<string> tip, double width, string automationName)
    {
        var op = (IOperatorInput)c;
        var toggle = new ToggleButton { Width = width, Height = 46, FontSize = 14, FontWeight = FontWeights.SemiBold, IsChecked = op.Active };
        // A fixed name; the pressed state says on or off (the visible words change, the name does not).
        AutomationProperties.SetName(toggle, automationName);
        toggle.Checked += (_, _) => op.Operate(true);
        toggle.Unchecked += (_, _) => op.Operate(false);
        Place(toggle, c.X, c.Y);
        _updates.Add(() =>
        {
            toggle.Content = text();
            toggle.ToolTip = tip();
            if (toggle.IsChecked != op.Active) toggle.IsChecked = op.Active;
        });
    }

    private void DrawSafetyEye(SafetyEye eye)
    {
        DrawToggle(eye, () => eye.Blocked ? "Step out of the doorway" : "Step into the doorway",
            () => eye.Blocked ? "Moves the person out of the doorway: the beam is clear again" : "Puts a person in the doorway, breaking the safety eye's beam", 190,
            "Person in the doorway");
        var state = DrawText(eye, eye.X, eye.Y + 50);
        _ = state;
    }

    private static string Word(SelectorSwitch s, bool on) => s.Positions.Count == 2 ? s.Positions[on ? 1 : 0] : on ? "On" : "Off";

    // ---- Indicators and actuators ---------------------------------------------------------------------

    private void DrawLight(PilotLight l)
    {
        var (on, off) = l.Color switch
        {
            "red" => (Color.FromRgb(0xE5, 0x37, 0x2B), Color.FromRgb(0x4A, 0x22, 0x20)),
            "amber" => (Color.FromRgb(0xF5, 0xB3, 0x01), Color.FromRgb(0x4A, 0x3C, 0x14)),
            _ => (Color.FromRgb(0x3C, 0xC8, 0x5A), Color.FromRgb(0x22, 0x3A, 0x28)),
        };
        var lamp = new Ellipse { Width = 46, Height = 46, Stroke = Ink, StrokeThickness = 2 };
        Place(lamp, l.X, l.Y);
        var text = Label("", l.X + 56, l.Y + 12, 120);
        _updates.Add(() =>
        {
            lamp.Fill = new SolidColorBrush(l.Lit ? on : off);
            lamp.StrokeThickness = l.Lit ? 4 : 2;
            text.Text = $"{l.Label}: {(l.Lit ? "ON" : "off")}";
        });
    }

    private void DrawMotor(Motor m)
    {
        var body = new Ellipse { Width = 56, Height = 56, Fill = new SolidColorBrush(Color.FromRgb(0xDD, 0xE3, 0xEA)), Stroke = Ink, StrokeThickness = 1.5 };
        Place(body, m.X, m.Y);
        var rotate = new RotateTransform();
        var shaft = new Rectangle { Width = 6, Height = 44, Fill = Ink, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = rotate };
        Place(shaft, m.X + 25, m.Y + 6);
        DrawText(m, m.X - 30, m.Y + 62, 160);
        _updates.Add(() => rotate.Angle = m.Angle);
    }

    private void DrawTank(Tank t)
    {
        var wall = new Rectangle { Width = t.Width, Height = t.Height, Stroke = Ink, StrokeThickness = 3, Fill = Brushes.White };
        Place(wall, t.X, t.Y);
        var liquid = new Rectangle { Width = t.Width - 6, Fill = Water };
        Canvas.SetLeft(liquid, t.X + 3);
        _canvas.Children.Add(liquid);
        var level = Label("", t.X, t.Y + t.Height / 2 - 10, t.Width, center: true, bold: true);
        _updates.Add(() =>
        {
            var h = Math.Max(0, (t.Height - 6) * t.LevelPercent / 100);
            liquid.Height = h;
            Canvas.SetTop(liquid, t.Y + t.Height - 3 - h);
            level.Text = $"{t.LevelPercent:0} %" + (t.Overflowed ? Environment.NewLine + "OVERFLOWED" : "");
            level.Foreground = t.LevelPercent > 50 ? Brushes.White : Ink;
        });
    }

    private void DrawValve(SolenoidValve v)
    {
        var left = new Polygon { Points = [new(0, 0), new(16, 10), new(0, 20)], Stroke = Ink, StrokeThickness = 1.5 };
        var right = new Polygon { Points = [new(32, 0), new(16, 10), new(32, 20)], Stroke = Ink, StrokeThickness = 1.5 };
        Place(left, v.X, v.Y);
        Place(right, v.X, v.Y);
        var text = Label("", v.X + 40, v.Y + 1, 170);
        _updates.Add(() =>
        {
            left.Fill = right.Fill = v.Open ? Water : Brushes.White;
            text.Text = $"{v.Label}: {(v.Open ? "OPEN" : "closed")}";
        });
    }

    private void DrawFloat(FloatSwitch f)
    {
        var tank = _scene.Find<Tank>(f.Tank);
        double x = f.X, y = f.Y;
        if (tank is not null)
        {
            x = tank.X + tank.Width + 6;
            y = tank.Y + tank.Height * (1 - f.LevelPercent / 100) - 8;
            _canvas.Children.Add(new Line
            {
                X1 = tank.X, X2 = tank.X + tank.Width, Y1 = y + 8, Y2 = y + 8,
                Stroke = Muted, StrokeThickness = 1, StrokeDashArray = [4, 3],
            });
        }
        var marker = new Ellipse { Width = 16, Height = 16, Stroke = Ink, StrokeThickness = 1.5 };
        Place(marker, x, y);
        var text = Label("", x + 22, y - 1, 130);
        _updates.Add(() =>
        {
            marker.Fill = f.Made ? Made : NotMade;
            text.Text = $"{f.Label}: {(f.Made ? "liquid" : "dry")}";
        });
    }

    private void DrawDoor(GarageDoor d)
    {
        var frame = new Rectangle { Width = d.Width, Height = d.Height, Stroke = Ink, StrokeThickness = 3, Fill = new SolidColorBrush(Color.FromRgb(0xF1, 0xF4, 0xF7)) };
        Place(frame, d.X, d.Y);
        var panel = new Rectangle { Width = d.Width - 6, Fill = new SolidColorBrush(Color.FromRgb(0x8A, 0x6D, 0x4B)) };
        Canvas.SetLeft(panel, d.X + 3);
        Canvas.SetTop(panel, d.Y + 3);
        _canvas.Children.Add(panel);
        var person = new Grid { Width = 26, Height = d.Height * GarageDoor.ObstructionHeight, Visibility = Visibility.Collapsed };
        person.Children.Add(new Rectangle { Fill = new SolidColorBrush(Color.FromRgb(0xB4, 0x53, 0x1A)), RadiusX = 6, RadiusY = 6 });
        Place(person, d.X + d.Width / 2 - 13, d.Y + d.Height * (1 - GarageDoor.ObstructionHeight));
        var beam = new Line { X1 = d.X + 3, X2 = d.X + d.Width - 3, Y1 = d.Y + d.Height - 10, Y2 = d.Y + d.Height - 10, StrokeThickness = 2 };
        _canvas.Children.Add(beam);
        var eye = _scene.Find<SafetyEye>(d.SafetyEye);
        DrawText(d, d.X - 10, d.Y + d.Height + 6, d.Width + 60);
        _updates.Add(() =>
        {
            panel.Height = Math.Max(0, (d.Height - 6) * (1 - d.Position));
            var blocked = eye?.Blocked == true;
            person.Visibility = blocked ? Visibility.Visible : Visibility.Collapsed;
            beam.Visibility = eye is null ? Visibility.Collapsed : Visibility.Visible;
            beam.Stroke = blocked ? Brushes.Firebrick : Made;
            beam.StrokeDashArray = blocked ? [2, 3] : null;
        });
    }

    private void DrawSensorBox(SensorBase s, double x, double y)
    {
        var box = new Rectangle { Width = 16, Height = 16, Stroke = Ink, StrokeThickness = 1.5 };
        Place(box, x, y);
        var text = Label("", x + 22, y - 1, 90);
        text.FontSize = 11.5;
        _updates.Add(() =>
        {
            box.Fill = s.Made ? Made : NotMade;
            text.Text = $"{s.Label}: {(s.Made ? "ON" : "off")}";
        });
    }

    private void DrawConveyor(Conveyor belt)
    {
        var x = belt.X;
        var y = belt.Y;
        _canvas.Children.Add(Shape(new Rectangle { Width = belt.Length, Height = 22, Fill = new SolidColorBrush(Color.FromRgb(0x3C, 0x45, 0x50)), RadiusX = 11, RadiusY = 11 }, x, y));
        for (var i = 0; i <= 8; i++)
            _canvas.Children.Add(Shape(new Ellipse { Width = 10, Height = 10, Fill = Steel }, x + 6 + i * (belt.Length - 22) / 8, y + 6));
        // Bin B at the end.
        _canvas.Children.Add(Shape(new Rectangle { Width = 56, Height = 46, Stroke = Ink, StrokeThickness = 2 }, x + belt.Length - 28, y + 46));
        var binB = Label("", x + belt.Length - 70, y + 96, 140, center: true);
        Label(belt.EndBinLabel, x + belt.Length - 28, y + 58, 56, center: true, bold: true);
        var parts = new Canvas();
        _canvas.Children.Add(parts);
        var state = DrawText(belt, x, y + 120, belt.Length);
        _ = state;
        _updates.Add(() =>
        {
            parts.Children.Clear();
            foreach (var p in belt.Parts)
            {
                var metal = p.Kind.Equals("metal", StringComparison.OrdinalIgnoreCase);
                var g = new Grid { Width = 22, Height = 22 };
                g.Children.Add(new Rectangle { Fill = metal ? Steel : new SolidColorBrush(Color.FromRgb(0x5B, 0x9B, 0xE6)), Stroke = Ink, StrokeThickness = 1 });
                g.Children.Add(new TextBlock { Text = metal ? "M" : "P", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold, Foreground = Ink });
                Canvas.SetLeft(g, x + belt.PositionOf(p) * belt.Length - 11);
                Canvas.SetTop(g, y - 22);
                parts.Children.Add(g);
            }
            binB.Text = Diverter_Describe(belt.EndBin);
        });
    }

    private void DrawBeltSensor(PhotoEye s)
    {
        var belt = _scene.Find<Conveyor>(s.Conveyor);
        if (belt is null) { DrawSensorBox(s, s.X, s.Y); return; }
        var cx = belt.X + s.At * belt.Length;
        var y = belt.Y - 62;
        var box = new Rectangle { Width = 14, Height = 14, Stroke = Ink, StrokeThickness = 1.5 };
        Place(box, cx - 7, y + 18);
        var beam = new Line { X1 = cx, X2 = cx, Y1 = y + 32, Y2 = belt.Y - 2, StrokeThickness = 2, StrokeDashArray = [2, 2] };
        _canvas.Children.Add(beam);
        var text = Label("", cx - 55, y, 110, center: true);
        text.FontSize = 11.5;
        _updates.Add(() =>
        {
            box.Fill = s.Made ? Made : NotMade;
            beam.Stroke = s.Made ? Made : Steel;
            text.Text = $"{s.Label}: {(s.Made ? "sees a part" : "clear")}";
        });
    }

    private void DrawDiverter(Diverter dv)
    {
        var belt = _scene.Find<Conveyor>(dv.Conveyor);
        if (belt is null) return;
        var cx = belt.X + dv.At * belt.Length;
        // The pusher sits below the belt and pushes the part across, into Bin A beside it.
        var body = new Rectangle { Width = 30, Height = 26, Fill = Steel, Stroke = Ink, StrokeThickness = 1.5 };
        Place(body, cx - 15, belt.Y + 52);
        var rod = new Rectangle { Width = 8, Fill = Ink };
        _canvas.Children.Add(rod);
        _canvas.Children.Add(Shape(new Rectangle { Width = 56, Height = 46, Stroke = Ink, StrokeThickness = 2 }, cx - 100, belt.Y + 40));
        Label(dv.BinLabel, cx - 100, belt.Y + 52, 56, center: true, bold: true);
        var bin = Label("", cx - 130, belt.Y + 90, 116, center: true);
        var text = Label("", cx + 20, belt.Y + 54, 110);
        _updates.Add(() =>
        {
            var len = 26 * dv.Extension;
            rod.Height = len;
            Canvas.SetLeft(rod, cx - 4);
            Canvas.SetTop(rod, belt.Y + 52 - len);
            text.Text = $"{dv.Label}: {(dv.Extension >= 1 ? "OUT" : dv.Extension > 0 ? "moving" : "in")}";
            bin.Text = Diverter_Describe(dv.Bin);
        });
    }

    private static string Diverter_Describe(Dictionary<string, int> bin) =>
        bin.Count == 0 ? "empty" : string.Join(", ", bin.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Value} {k.Key}"));

    // ---- Helpers ------------------------------------------------------------------------------------

    private TextBlock DrawText(SceneComponent c, double x, double y, double width = 200)
    {
        var t = Label("", x, y, width);
        _updates.Add(() => t.Text = c.StateText);
        return t;
    }

    private TextBlock Label(string text, double x, double y, double width, bool center = false, bool bold = false)
    {
        var t = new TextBlock
        {
            Text = text, Width = width, Foreground = Ink, FontSize = 12.5, TextWrapping = TextWrapping.Wrap,
            TextAlignment = center ? TextAlignment.Center : TextAlignment.Left,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        };
        Place(t, x, y);
        return t;
    }

    private void Place(UIElement e, double x, double y)
    {
        Canvas.SetLeft(e, x);
        Canvas.SetTop(e, y);
        _canvas.Children.Add(e);
    }

    private static UIElement Shape(UIElement e, double x, double y)
    {
        Canvas.SetLeft(e, x);
        Canvas.SetTop(e, y);
        return e;
    }
}
