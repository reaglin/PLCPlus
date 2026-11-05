using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Plc.Dialects.Iec;
using Plc.Editors;
using Plc.Engine;
using Plc.Scenes;

namespace Learning.Shell;

// The ladder editor in the window (plan phase 3): "My program" for each challenge is editable. The
// editing itself is Plc.Editors.LadderEditor (tested without a window); this part shows it and passes
// the learner's clicks and keys to it. Every change runs straight away: the PLC and the machine restart
// with the new program, so a learner sees what their change does.
public partial class MachineWorkspace
{
    private LadderEditor? _editor;
    private string _editedJson = "";
    private Action? _pendingCommit;   // the property box being typed in: saved before another element is selected
    private bool _forcesRemoved;

    private bool Editing => _editor is not null;

    private static string WorkFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PLCPlus", "work");
    private string WorkFile => Path.Combine(WorkFolder, (_current?.Challenge.Id ?? "program") + ".plcprogram");

    // ---- Entering and leaving the editor ------------------------------------------------------------

    private void EnterEditor()
    {
        PlcProject project;
        var resumed = false;
        try
        {
            project = File.Exists(WorkFile) ? ProjectJson.Deserialize(File.ReadAllText(WorkFile)) : BlankForScene();
            resumed = File.Exists(WorkFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            project = BlankForScene();
        }
        StartEditing(project);
        SetStatus((resumed
            ? "My program, as you left it. Select an element (click it, or Tab to the rungs and use the arrow keys), then use the tools above the rungs."
            : "My program starts with the machine's tags and one rung: a contact and a coil. Click the contact: its Tag box is ready. Type the tag, press Enter, and build from there."));
    }

    private void StartEditing(PlcProject project)
    {
        if (_editor is not null) _editor.Changed -= Editor_Changed;
        _editor = new LadderEditor(project, IecDialect.Instance);
        _editor.Changed += Editor_Changed;
        _editedJson = ProjectJson.Serialize(_editor.Project);
        _programJson = _editedJson;
        EditorTools.Visibility = Visibility.Visible;
        ProblemsExpander.Visibility = Visibility.Visible;
        LadderScroller.Focusable = true;
        LadderScroller.IsTabStop = true;
        AutomationPropertiesHelper.SetName(LadderScroller, "Ladder editor: arrow keys select, letters add elements (O contact, C closed contact, Q coil, B branch, I box)");
        // The note is short here: the rungs need the room. The rest is in the ladder's tooltip.
        ProgramNote.Text = "My program: every change runs straight away.";
        LadderScroller.ToolTip = "Your own program. Every change runs straight away: the PLC and the machine restart with it. It is kept on this computer for this exercise.";
        PowerUp();
        UpdateEditorPanels();
    }

    private void LeaveEditor()
    {
        if (_editor is null) return;
        _editor.Changed -= Editor_Changed;
        _editor = null;
        EditorTools.Visibility = Visibility.Collapsed;
        ProblemsExpander.Visibility = Visibility.Collapsed;
        LadderScroller.ToolTip = null;
        LadderScroller.Focusable = false;
        LadderScroller.IsTabStop = false;
    }

    private PlcProject BlankForScene()
    {
        var scene = Scene.Load(File.ReadAllText(_content!.ScenePath(_current!.Challenge.Scene)));
        return LadderEditor.Blank("My program", scene.IoList.Select(p => (p.Tag, p.Kind == IoKind.Input, p.Meaning)));
    }

    private void Editor_Changed(object? sender, EventArgs e)
    {
        if (_editor is null) return;
        var json = ProjectJson.Serialize(_editor.Project);
        if (json != _editedJson)
        {
            // The program changed: keep it, and run the new one from power-up.
            _editedJson = json;
            _programJson = json;
            Save();
            _forcesRemoved = _runtime?.Forces.Count > 0;
            PowerUp();
            if (ResultsList.ItemsSource is not null)
            {
                ResultsList.ItemsSource = new[] { "The program has changed since this check. Press Check my program again to test the new one." };
                ScoreText.Text = "";
                ScoreText.ToolTip = null;
            }
        }
        else
        {
            ShowLadder(_editor.Project); // only the selection moved
            if (_runtime is not null) _ladder.Update(_runtime);
        }
        UpdateEditorPanels();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(WorkFolder);
            File.WriteAllText(WorkFile, _editedJson);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus("My program could not be kept on this computer: " + ex.Message + " Use File ▸ Save My program as… to keep a copy.");
        }
    }

    /// <summary>Draws the rungs: in the editor with the selection and the problems; always with forcing.</summary>
    private void ShowLadder(PlcProject project)
    {
        var wired = project.Tags.Where(t => t.Io != IoDirection.None).Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _ladder.Show(project, new LadderViewOptions
        {
            Selection = _editor?.Selection,
            Diagnostics = _editor?.Diagnostics ?? [],
            ElementClicked = _editor is null ? null : (rung, path) =>
            {
                CommitPending();
                _editor?.Select(new LadderSelection(rung, path));
                FocusFirstProperty(selectText: true); // click, then type: the tag goes in the Tag box
            },
            RungClicked = _editor is null ? null : rung =>
            {
                CommitPending();
                _editor?.Select(LadderSelection.OfRung(rung));
                LadderScroller.Focus();
            },
            CanForce = wired.Contains,
            Force = (tag, value) =>
            {
                if (_runtime is null) return;
                if (value is { } v) _runtime.Force(tag, PlcValue.FromBool(v)); else _runtime.Unforce(tag);
                Refresh();
                SetStatus(value is null
                    ? $"{tag} follows the machine again."
                    : $"{tag} is forced {(value.Value ? "ON" : "OFF")}: the PLC uses that value whatever the machine does. Right-click it again to remove the force. Reset removes every force.");
            },
        });
    }

    // ---- The panels: the selected element's properties, and the problems ---------------------------

    private sealed record ProblemItem(string Text, Diagnostic Diagnostic);

    private void UpdateEditorPanels()
    {
        if (_editor is null) return;
        UndoButton.IsEnabled = _editor.CanUndo;
        RedoButton.IsEnabled = _editor.CanRedo;
        UndoButton.ToolTip = _editor.CanUndo ? "Undoes the last change. Key: Ctrl+Z" : "Nothing to undo yet. Key: Ctrl+Z";
        RedoButton.ToolTip = _editor.CanRedo ? "Puts back what Undo took away. Key: Ctrl+Y" : "Nothing to redo: Redo works after Undo. Key: Ctrl+Y";

        var problems = _editor.Diagnostics;
        var errors = problems.Count(d => d.Severity == Severity.Error);
        ProblemsExpander.Header = problems.Count == 0 ? "Problems: none" : $"Problems: {errors} to fix, {problems.Count - errors} to look at";
        ProblemsList.ItemsSource = problems.Select(d => new ProblemItem(
            $"{(d.Severity == Severity.Error ? "✗" : "⚠")} {(d.Rung > 0 ? $"Rung {d.Rung}" : d.Program)}: {d.Message}", d)).ToList();

        BuildProperties();
    }

    private void ProblemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_editor is null || ProblemsList.SelectedItem is not ProblemItem item || item.Diagnostic.Rung <= 0) return;
        var d = item.Diagnostic;
        var parts = d.ElementPath.Split('.', StringSplitOptions.RemoveEmptyEntries).Skip(1)
            .Select(s => int.Parse(s, System.Globalization.CultureInfo.InvariantCulture) - 1).ToList();
        _editor.Select(parts.Count == 0 ? LadderSelection.OfRung(d.Rung - 1) : new LadderSelection(d.Rung - 1, parts));
        LadderScroller.Focus();
        Dispatcher.BeginInvoke(() => ProblemsList.SelectedIndex = -1); // so the same problem can be chosen again
    }

    private void BuildProperties()
    {
        PropertiesHost.Children.Clear();
        if (_editor is null) return;
        var sel = _editor.Selection;
        if (sel is null)
        {
            PropertiesHost.Children.Add(Note("No rungs yet. Press New rung to start."));
            return;
        }

        switch (_editor.SelectedElement)
        {
            case ContactElement c:
                PropertiesHost.Children.Add(Caption("Contact — tag:"));
                PropertiesHost.Children.Add(TagBox(c.Tag));
                PropertiesHost.Children.Add(Caption("kind:"));
                PropertiesHost.Children.Add(KindBox(
                    [(ContactKind.NormallyOpen, "Normally open -| |- (TRUE passes)"), (ContactKind.NormallyClosed, "Normally closed -|/|- (FALSE passes)"),
                     (ContactKind.PositiveEdge, "Rising edge -|P|- (one scan on TRUE)"), (ContactKind.NegativeEdge, "Falling edge -|N|- (one scan on FALSE)")],
                    c.Contact, k => _editor.SetContactKind(k)));
                break;
            case CoilElement coil:
                PropertiesHost.Children.Add(Caption("Coil — tag:"));
                PropertiesHost.Children.Add(TagBox(coil.Tag));
                PropertiesHost.Children.Add(Caption("kind:"));
                PropertiesHost.Children.Add(KindBox(
                    [(CoilKind.Normal, "Normal -( )- (follows the power)"), (CoilKind.Negated, "Negated -(/)- (the opposite)"),
                     (CoilKind.Set, "Set -(S)- (latch on)"), (CoilKind.Reset, "Reset -(R)- (unlatch)")],
                    coil.Coil, k => _editor.SetCoilKind(k)));
                break;
            case BoxElement box:
                var instruction = _editor.Dialect.Find(box.Instruction);
                PropertiesHost.Children.Add(Caption($"{box.Instruction}{(box.Instance is null ? "" : $" ({box.Instance})")}:"));
                foreach (var op in instruction?.Operands ?? [])
                {
                    PropertiesHost.Children.Add(Caption(op.Name + ":"));
                    box.Operands.TryGetValue(op.Name, out var value);
                    PropertiesHost.Children.Add(OperandBox(op, value ?? ""));
                }
                PropertiesHost.Children.Add(Note(instruction?.Summary ?? ""));
                break;
            case ParallelElement branch:
                PropertiesHost.Children.Add(Caption($"Branch (OR) with {branch.Branches.Count} paths."));
                PropertiesHost.Children.Add(Note("What you add next goes after the branch. Delete removes the whole branch; Branch adds another path."));
                break;
            default:
                PropertiesHost.Children.Add(Caption($"Rung {sel.Rung + 1} — comment:"));
                PropertiesHost.Children.Add(CommentBox(sel.Rung));
                PropertiesHost.Children.Add(Note("The rung is selected: Delete removes it, Rung ▲/▼ moves it, Copy rung copies it."));
                return;
        }
        PropertiesHost.Children.Add(Caption($"rung {sel.Rung + 1} comment:"));
        PropertiesHost.Children.Add(CommentBox(sel.Rung));
    }

    private static TextBlock Caption(string text) =>
        new() { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 6, 2), FontWeight = FontWeights.SemiBold };

    private static TextBlock Note(string text) =>
        new() { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 6, 2), TextWrapping = TextWrapping.Wrap, MaxWidth = 420, Foreground = Brushes.DimGray };

    private ComboBox TagBox(string tag)
    {
        var box = new ComboBox
        {
            IsEditable = true, Width = 150, Margin = new Thickness(0, 2, 10, 2), Text = tag,
            ItemsSource = _editor!.BoolTagNames(),
            ToolTip = "The tag this element uses. Choose one, or type a new name and press Enter: a new name is declared as a BOOL tag.",
        };
        System.Windows.Automation.AutomationProperties.SetName(box, "Tag");
        var target = _editor.Selection;
        void Commit()
        {
            if (_editor is null || !Equals(_editor.Selection, target)) return; // saved already, for its own element
            Report(_editor.SetTag(box.Text));
        }
        box.GotKeyboardFocus += (_, _) => _pendingCommit = Commit;
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { Commit(); LadderScroller.Focus(); e.Handled = true; }
            else if (e.Key == Key.Escape) { LadderScroller.Focus(); e.Handled = true; }
        };
        box.LostKeyboardFocus += (_, e) => { if (!box.IsKeyboardFocusWithin && box.IsLoaded) Commit(); };
        box.SelectionChanged += (_, _) => { if (box.SelectedItem is string s && s != tag) { box.Text = s; Commit(); } };
        return box;
    }

    private static ComboBox KindBox<T>(IReadOnlyList<(T Kind, string Words)> kinds, T current, Func<T, EditResult> set) where T : struct
    {
        var box = new ComboBox { Width = 250, Margin = new Thickness(0, 2, 10, 2), ToolTip = "What kind of element this is" };
        System.Windows.Automation.AutomationProperties.SetName(box, "Kind");
        foreach (var k in kinds) box.Items.Add(k.Words);
        box.SelectedIndex = kinds.Select((k, i) => (k, i)).First(x => EqualityComparer<T>.Default.Equals(x.k.Kind, current)).i;
        box.SelectionChanged += (_, _) => { if (box.SelectedIndex >= 0) set(kinds[box.SelectedIndex].Kind); };
        return box;
    }

    private TextBox OperandBox(OperandSpec op, string value)
    {
        var box = new TextBox
        {
            Text = value, Width = 90, Margin = new Thickness(0, 2, 10, 2),
            ToolTip = $"{op.Name}: {op.Help} Type a value or a tag, then press Enter. A new tag name is declared for you.",
        };
        System.Windows.Automation.AutomationProperties.SetName(box, op.Name);
        var target = _editor!.Selection;
        void Commit()
        {
            if (_editor is null || !Equals(_editor.Selection, target)) return;
            Report(_editor.SetOperand(op.Name, box.Text));
        }
        box.GotKeyboardFocus += (_, _) => _pendingCommit = Commit;
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Commit(); LadderScroller.Focus(); e.Handled = true; } };
        box.LostKeyboardFocus += (_, _) => { if (box.IsLoaded) Commit(); };
        return box;
    }

    private TextBox CommentBox(int rung)
    {
        var box = new TextBox
        {
            Text = _editor!.Rungs[rung].Comment ?? "", Width = 260, Margin = new Thickness(0, 2, 10, 2),
            ToolTip = "What this rung does, in your words. Press Enter to keep it.",
        };
        System.Windows.Automation.AutomationProperties.SetName(box, "Rung comment");
        void Commit()
        {
            if (_editor is not null && rung < _editor.Rungs.Count) Report(_editor.SetRungComment(rung, box.Text));
        }
        box.GotKeyboardFocus += (_, _) => _pendingCommit = Commit;
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Commit(); LadderScroller.Focus(); e.Handled = true; } };
        box.LostKeyboardFocus += (_, _) => { if (box.IsLoaded) Commit(); };
        return box;
    }

    /// <summary>Saves what is being typed in a property box, before the window moves to another item or tab.</summary>
    public void CommitPendingEdit() => CommitPending();

    private void CommitPending()
    {
        var commit = _pendingCommit;
        _pendingCommit = null;
        commit?.Invoke();
    }

    /// <summary>Shows an edit's message, and says so when the restart took the forces away.</summary>
    private void Report(EditResult r)
    {
        if (r.Message.Length == 0 && !_forcesRemoved) return;
        SetStatus(r.Message + (_forcesRemoved ? " The forces were removed: the PLC restarted with the changed program." : ""));
        _forcesRemoved = false;
    }

    private void Ladder_GotFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        LadderScroller.BorderBrush = (Brush)FindResource("AccentBrush");

    private void Ladder_LostFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        LadderScroller.BorderBrush = Brushes.Transparent;

    // ---- Tools and keys -----------------------------------------------------------------------------

    private void Tool_Click(object sender, RoutedEventArgs e)
    {
        CommitPending();
        if (sender is FrameworkElement { Tag: string tool }) Do(tool);
        FocusAfterAdding();
    }

    /// <summary>
    /// After a tool button or a drop adds a part that still needs its tag, the Tag box is ready for typing, so
    /// "press Branch, type Motor" does what the lessons say. Otherwise the rungs keep the focus (and the letter keys).
    /// </summary>
    private void FocusAfterAdding()
    {
        var needsTag = _editor?.SelectedElement switch
        {
            ContactElement c => string.IsNullOrEmpty(c.Tag),
            CoilElement c => string.IsNullOrEmpty(c.Tag),
            BoxElement => true,
            _ => false,
        };
        if (needsTag) FocusFirstProperty(selectText: true);
        else LadderScroller.Focus();
    }

    /// <summary>One editor action, from a tool button, a key or a drop.</summary>
    private void Do(string tool, bool before = false)
    {
        if (_editor is null) return;
        EditResult? r = tool switch
        {
            "O" => _editor.InsertContact(ContactKind.NormallyOpen, before),
            "C" => _editor.InsertContact(ContactKind.NormallyClosed, before),
            "P" => _editor.InsertContact(ContactKind.PositiveEdge, before),
            "N" => _editor.InsertContact(ContactKind.NegativeEdge, before),
            "Q" => _editor.InsertCoil(CoilKind.Normal, before),
            "S" => _editor.InsertCoil(CoilKind.Set, before),
            "R" => _editor.InsertCoil(CoilKind.Reset, before),
            "B" => _editor.AddBranch(),
            "NewRung" => _editor.AddRung(),
            "Delete" => _editor.DeleteSelected(),
            "Up" => _editor.MoveRung(-1),
            "Down" => _editor.MoveRung(+1),
            "Undo" => _editor.Undo(),
            "Redo" => _editor.Redo(),
            "Copy" => CopyRung(),
            "Paste" => _editor.PasteRung(Clipboard.ContainsText() ? Clipboard.GetText() : null),
            _ when tool.StartsWith("Box:", StringComparison.Ordinal) => _editor.InsertBox(tool[4..], before),
            _ => null,
        };
        if (r is not null) Report(r);
    }

    private EditResult CopyRung()
    {
        var text = _editor!.CopyRung();
        if (text is null) return EditResult.No("Select a rung first.");
        try { Clipboard.SetText(text); }
        catch (System.Runtime.InteropServices.ExternalException) { return EditResult.No("The clipboard is busy; try again."); }
        return EditResult.Ok($"Copied rung {_editor.Selection!.Rung + 1}. Paste rung (Ctrl+V) puts a copy below the selected rung.");
    }

    private void BoxButton_Click(object sender, RoutedEventArgs e) => OpenBoxMenu(BoxButton);

    private void OpenBoxMenu(UIElement target)
    {
        if (_editor is null) return;
        var menu = new ContextMenu { PlacementTarget = target };
        (string Group, string[] Names)[] groups =
        [
            ("Timers", ["TON", "TOF", "TP"]), ("Counters", ["CTU", "CTD", "CTUD"]),
            ("Compare", ["EQ", "NE", "GT", "GE", "LT", "LE"]), ("Math", ["ADD", "SUB", "MUL", "DIV", "MOD"]),
            ("Move and bit logic", ["MOVE", "AND", "OR", "XOR", "NOT"]),
        ];
        foreach (var (group, names) in groups)
        {
            var parent = new MenuItem { Header = group };
            foreach (var name in names)
            {
                var instruction = _editor.Dialect.Find(name);
                if (instruction is null) continue;
                var item = new MenuItem { Header = $"{name} — {instruction.Summary}" };
                item.Click += (_, _) => { Do("Box:" + name); LadderScroller.Focus(); };
                parent.Items.Add(item);
            }
            menu.Items.Add(parent);
        }
        menu.IsOpen = true;
    }

    private void StartAgain_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) return;
        var menu = new ContextMenu { PlacementTarget = StartAgainButton };
        void Add(string header, string tip, Func<PlcProject> make)
        {
            var item = new MenuItem { Header = header, ToolTip = tip };
            item.Click += (_, _) =>
            {
                if (_editor is null) return;
                Report(_editor.ReplaceProgram(make(), "Started again"));
                LadderScroller.Focus();
            };
            menu.Items.Add(item);
        }
        Add("_Empty: just the machine's tags", "One rung with a contact and a coil, and every input and output of the machine declared", BlankForScene);
        // Not the working program: a copy of the answer would pass the exercise without the learner's own work.
        foreach (var p in _current.Programs.Skip(1))
        {
            var file = p.File;
            Add($"A copy of \"{p.Title}\"", p.Note, () => ProjectJson.Deserialize(File.ReadAllText(Path.Combine(ContentDir, file))));
        }
        menu.IsOpen = true;
    }

    private void Ladder_KeyDown(object sender, KeyEventArgs e)
    {
        if (_editor is null || e.OriginalSource is TextBox or ComboBox) return;
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var alt = e.Key == Key.System;
        var handled = true;
        switch (key)
        {
            case Key.Left when shift: _editor.ExtendSelection(-1); break;
            case Key.Right when shift: _editor.ExtendSelection(+1); break;
            case Key.Left: _editor.MoveSelection(-1); break;
            case Key.Right: _editor.MoveSelection(+1); break;
            case Key.Up when alt: Do("Up"); break;
            case Key.Down when alt: Do("Down"); break;
            case Key.Up: _editor.MoveRungSelection(-1); break;
            case Key.Down: _editor.MoveRungSelection(+1); break;
            case Key.Z when ctrl: Do("Undo"); break;
            case Key.Y when ctrl: Do("Redo"); break;
            case Key.C when ctrl: Do("Copy"); break;
            case Key.V when ctrl: Do("Paste"); break;
            case Key.Enter when ctrl: Do("NewRung"); break;
            case Key.Enter or Key.F2: FocusFirstProperty(); break;
            case Key.Delete: Do("Delete"); break;
            case Key.O or Key.C or Key.P or Key.N or Key.Q or Key.S or Key.R or Key.B when !ctrl && !alt:
                Do(key.ToString(), before: shift); break;
            case Key.I when !ctrl: OpenBoxMenu(BoxButton); break;
            default: handled = false; break;
        }
        e.Handled = handled;
    }

    private void FocusFirstProperty(bool selectText = false)
    {
        var first = PropertiesHost.Children.OfType<Control>().FirstOrDefault();
        if (first is null)
        {
            LadderScroller.Focus();
            return;
        }
        first.Focus();
        if (!selectText) return;
        // After layout, so an editable ComboBox has its text box: select what is there, so typing replaces it.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            first.Focus();
            if (first is TextBox text) text.SelectAll();
            else if (first is ComboBox combo && combo.Template?.FindName("PART_EditableTextBox", combo) is TextBox inner) inner.SelectAll();
        });
    }

    // ---- Drag a tool onto an element ------------------------------------------------------------------

    private Point _dragStart;

    private void Tool_DragStart(object sender, MouseButtonEventArgs e) => _dragStart = e.GetPosition(this);

    private void Tool_DragMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement { Tag: string tool } source) return;
        var d = e.GetPosition(this) - _dragStart;
        if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        DragDrop.DoDragDrop(source, new DataObject("plcplus-tool", tool), DragDropEffects.Copy);
    }

    private void Ladder_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = Editing && e.Data.GetDataPresent("plcplus-tool") ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Ladder_Drop(object sender, DragEventArgs e)
    {
        if (_editor is null || e.Data.GetData("plcplus-tool") is not string tool) return;
        if (_ladder.ElementAt(e.GetPosition(LadderCanvas)) is { } at)
            _editor.Select(new LadderSelection(at.Rung, at.Path));
        Do(tool);
        FocusAfterAdding();
    }

    // ---- Files ----------------------------------------------------------------------------------------

    /// <summary>What a saved file is called by default, e.g. "5.2 Start and stop a motor — My program".</summary>
    public string? SaveName { get; set; }

    private static string SafeName(string name) => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));

    private static string DocumentsFolder()
    {
        var docs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PLCPlus");
        return Directory.Exists(docs) ? docs : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    /// <summary>File ▸ Open a program: replaces My program (Undo can bring the old one back).</summary>
    public void OpenProgram()
    {
        if (_editor is null) return;
        var dialog = new OpenFileDialog
        {
            Title = "Open a program", Filter = "PLC+ programs (*.plcprogram)|*.plcprogram|All files (*.*)|*.*",
            InitialDirectory = DocumentsFolder(),
        };
        if (dialog.ShowDialog(HostWindow) != true) return;
        PlcProject project;
        try
        {
            project = ProjectJson.Deserialize(File.ReadAllText(dialog.FileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            MessageBox.Show(HostWindow, $"PLC+ could not open that file as a program. It may not be a PLC+ program (.plcprogram), or it may be damaged or open in another program. Choose another file.\n\nDetails: {ex.Message}", "PLC+ — Open a program", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (MessageBox.Show(HostWindow, $"Open {Path.GetFileName(dialog.FileName)} as My program for this exercise? It replaces My program; Undo (Ctrl+Z) can bring it back while PLC+ is open.",
                "PLC+ — Open a program", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Report(_editor.ReplaceProgram(project, $"Opened {Path.GetFileName(dialog.FileName)}"));
    }

    /// <summary>File ▸ Save My program as.</summary>
    public void SaveProgram()
    {
        CommitPending();
        if (_editor is null) return;
        var dialog = new SaveFileDialog
        {
            Title = "Save My program", Filter = "PLC+ programs (*.plcprogram)|*.plcprogram",
            FileName = SafeName(SaveName ?? _current?.Challenge.Title ?? "My program") + ".plcprogram", InitialDirectory = DocumentsFolder(),
        };
        if (dialog.ShowDialog(HostWindow) != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, _editedJson);
            SetStatus($"Saved My program to {dialog.FileName}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(HostWindow, $"PLC+ could not save there. The folder may be read-only, or the file open in another program. Choose another folder or name.\n\nDetails: {ex.Message}", "PLC+ — Save My program", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Saves the rungs on the left as a picture (plan 3.6: the same drawing, so it can go in a report).</summary>
    public void SavePicture()
    {
        CommitPending();
        var dialog = new SaveFileDialog
        {
            Title = "Save the program as a picture", Filter = "PNG picture (*.png)|*.png",
            FileName = (_current?.Challenge.Title ?? "Program") + ".png", InitialDirectory = DocumentsFolder(),
        };
        if (dialog.ShowDialog(HostWindow) != true) return;
        try
        {
            LadderPrinter.SavePng(ProjectJson.Deserialize(_programJson), $"{_current?.Challenge.Title} — {(IsExercise ? "My program" : ProgramPicker.SelectedItem)}", dialog.FileName);
            SetStatus($"Saved the picture to {dialog.FileName}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(HostWindow, $"PLC+ could not save the picture there. The folder may be read-only, or the file open in another program. Choose another folder or name.\n\nDetails: {ex.Message}", "PLC+ — Save the program as a picture", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

/// <summary>Draws a program off screen with the ladder view, and saves it as a PNG (plan 3.6).</summary>
internal static class LadderPrinter
{
    public static void SavePng(PlcProject project, string title, string path)
    {
        var canvas = new Canvas { Background = Brushes.White };
        new LadderView(canvas).Show(project);
        var heading = new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(16, 12, 16, 4) };
        var panel = new StackPanel { Background = Brushes.White };
        panel.Children.Add(heading);
        panel.Children.Add(canvas);
        panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        panel.Arrange(new Rect(panel.DesiredSize));
        panel.UpdateLayout();
        const double dpi = 192;
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(panel.ActualWidth * dpi / 96), (int)Math.Ceiling(panel.ActualHeight * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(panel);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}

internal static class AutomationPropertiesHelper
{
    public static void SetName(DependencyObject element, string name) => System.Windows.Automation.AutomationProperties.SetName(element, name);
}
