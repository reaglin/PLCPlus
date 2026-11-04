using System.Text.Json;
using System.Text.RegularExpressions;
using Plc.Engine;

namespace Plc.Editors;

/// <summary>
/// What is selected: an element (or a run of neighbouring elements) in a rung, or the rung itself.
/// <see cref="Path"/> leads from the rung's top-level series to the first selected element:
/// <c>[i]</c> is item i of the rung; <c>[i, b, j]</c> is item j of branch b of the parallel at item i; and so on.
/// An empty path selects the rung.
/// </summary>
public sealed class LadderSelection(int rung, IReadOnlyList<int> path, int length = 1) : IEquatable<LadderSelection>
{
    public int Rung { get; } = rung;
    public IReadOnlyList<int> Path { get; } = path;
    public int Length { get; } = Math.Max(1, length);
    public bool IsRung => Path.Count == 0;

    public static LadderSelection OfRung(int rung) => new(rung, []);

    public bool Contains(int rung, IReadOnlyList<int> path) =>
        rung == Rung && path.Count == Path.Count && path.Count > 0
        && path.Take(path.Count - 1).SequenceEqual(Path.Take(Path.Count - 1))
        && path[^1] >= Path[^1] && path[^1] < Path[^1] + Length;

    public bool Equals(LadderSelection? other) =>
        other is not null && other.Rung == Rung && other.Length == Length && other.Path.SequenceEqual(Path);
    public override bool Equals(object? obj) => Equals(obj as LadderSelection);
    public override int GetHashCode() => HashCode.Combine(Rung, Length, string.Join('.', Path));
    public override string ToString() => $"rung {Rung + 1}" + (IsRung ? "" : $", element {string.Join('.', Path.Select(p => p + 1))}" + (Length > 1 ? $" (+{Length - 1})" : ""));
}

/// <summary>The result of an edit: whether it happened, and one sentence for the status line.</summary>
public sealed record EditResult(bool Done, string Message)
{
    public static EditResult Ok(string message) => new(true, message);
    public static EditResult No(string message) => new(false, message);
}

/// <summary>
/// Every editing action of the ladder editor (spec 5.1), on a copy of a project, with undo and redo.
/// No UI here: the window shows <see cref="Project"/> and <see cref="Selection"/> and calls these methods,
/// and the tests call the same ones. Every editor produces the neutral program tree (spec 4.1), so the
/// engine, the grader and the printouts work on what is edited here without knowing about the editor.
/// </summary>
public sealed class LadderEditor
{
    private const int UndoLimit = 200;
    private static readonly Regex Identifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

    private readonly IDialect _dialect;
    private readonly Stack<(string Json, LadderSelection? Selection, string What)> _undo = new();
    private readonly Stack<(string Json, LadderSelection? Selection, string What)> _redo = new();

    public PlcProject Project { get; private set; }
    public LadderSelection? Selection { get; private set; }

    /// <summary>Raised after every change to the program or the selection.</summary>
    public event EventHandler? Changed;

    public LadderEditor(PlcProject project, IDialect dialect)
    {
        _dialect = dialect;
        Project = Clone(project);
        if (Project.Programs.Count == 0) Project.Programs.Add(new ProgramUnit { Name = "Main" });
        foreach (var r in Rungs) r.Logic = Normalize(r.Logic);
        Selection = Rungs.Count > 0 ? FirstLeafOr(0) : null;
    }

    public List<Rung> Rungs => Project.Programs[0].Rungs;
    public IDialect Dialect => _dialect;

    /// <summary>The problems in the program now (the validator), for the problems list and the marks on the rungs.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics => ProjectValidator.Validate(Project, _dialect);

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    // ---- Undo and redo ------------------------------------------------------------------------------

    private void Checkpoint(string what)
    {
        _undo.Push((ProjectJson.Serialize(Project), Selection, what));
        if (_undo.Count > UndoLimit)
        {
            var keep = _undo.Take(UndoLimit).Reverse().ToList();
            _undo.Clear();
            foreach (var k in keep) _undo.Push(k);
        }
        _redo.Clear();
    }

    public EditResult Undo()
    {
        if (_undo.Count == 0) return EditResult.No("There is nothing to undo.");
        var (json, sel, what) = _undo.Pop();
        _redo.Push((ProjectJson.Serialize(Project), Selection, what));
        Project = ProjectJson.Deserialize(json);
        Selection = sel;
        Raise();
        return EditResult.Ok($"Undone: {what}.");
    }

    public EditResult Redo()
    {
        if (_redo.Count == 0) return EditResult.No("There is nothing to redo.");
        var (json, sel, what) = _redo.Pop();
        _undo.Push((ProjectJson.Serialize(Project), Selection, what));
        Project = ProjectJson.Deserialize(json);
        Selection = sel;
        Raise();
        return EditResult.Ok($"Redone: {what}.");
    }

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);

    // ---- Selection ------------------------------------------------------------------------------------

    public void Select(LadderSelection? selection)
    {
        if (selection is not null && (selection.Rung < 0 || selection.Rung >= Rungs.Count)) return;
        Selection = selection;
        Raise();
    }

    /// <summary>The selected element (the first of a run), or null when a rung or nothing is selected.</summary>
    public LadderElement? SelectedElement =>
        Selection is { IsRung: false } s && TryLocate(s.Rung, s.Path, out var parent, out var index) ? parent.Items[index] : null;

    /// <summary>
    /// Every contact, coil and box of a rung, left to right, top branch first, and each branch as a whole
    /// after its paths: the order the arrow keys move through.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<int>> Leaves(int rung)
    {
        var list = new List<IReadOnlyList<int>>();
        void Walk(SeriesElement s, List<int> prefix)
        {
            for (var i = 0; i < s.Items.Count; i++)
            {
                if (s.Items[i] is ParallelElement p)
                {
                    for (var b = 0; b < p.Branches.Count; b++)
                        Walk((SeriesElement)p.Branches[b], [.. prefix, i, b]);
                    list.Add([.. prefix, i]); // then the whole branch, so the next thing can go after it
                }
                else
                    list.Add([.. prefix, i]);
            }
        }
        if (rung >= 0 && rung < Rungs.Count) Walk((SeriesElement)Rungs[rung].Logic, []);
        return list;
    }

    /// <summary>Moves the selection to the next (+1) or previous (−1) element, across rungs.</summary>
    public void MoveSelection(int step)
    {
        if (Rungs.Count == 0) return;
        var sel = Selection ?? LadderSelection.OfRung(0);
        var leaves = Leaves(sel.Rung);
        var at = sel.IsRung ? (step > 0 ? -1 : leaves.Count) : IndexOf(leaves, sel.Path);
        var next = at + step;
        if (next >= 0 && next < leaves.Count) { Select(new LadderSelection(sel.Rung, leaves[next])); return; }
        var rung = sel.Rung + step;
        if (rung < 0 || rung >= Rungs.Count) return;
        var other = Leaves(rung);
        Select(other.Count == 0 ? LadderSelection.OfRung(rung) : new LadderSelection(rung, step > 0 ? other[0] : other[^1]));
    }

    /// <summary>Moves to the rung above (−1) or below (+1), to its first element.</summary>
    public void MoveRungSelection(int step)
    {
        if (Rungs.Count == 0) return;
        var rung = Math.Clamp((Selection?.Rung ?? 0) + step, 0, Rungs.Count - 1);
        Select(FirstLeafOr(rung));
    }

    /// <summary>Grows (+1) or shrinks (−1) the selection over its neighbours in the same series (for a branch around several).</summary>
    public void ExtendSelection(int step)
    {
        if (Selection is not { IsRung: false } s || !TryLocate(s.Rung, s.Path, out var parent, out var index)) return;
        var length = Math.Clamp(s.Length + step, 1, parent.Items.Count - index);
        Select(new LadderSelection(s.Rung, s.Path, length));
    }

    private LadderSelection FirstLeafOr(int rung)
    {
        var leaves = Leaves(rung);
        return leaves.Count > 0 ? new LadderSelection(rung, leaves[0]) : LadderSelection.OfRung(rung);
    }

    private static int IndexOf(IReadOnlyList<IReadOnlyList<int>> leaves, IReadOnlyList<int> path)
    {
        for (var i = 0; i < leaves.Count; i++) if (leaves[i].SequenceEqual(path)) return i;
        return -1;
    }

    /// <summary>Replaces the whole program (Start again, Open) as one step that Undo can take back.</summary>
    public EditResult ReplaceProgram(PlcProject project, string what)
    {
        Checkpoint(what);
        Project = Clone(project);
        if (Project.Programs.Count == 0) Project.Programs.Add(new ProgramUnit { Name = "Main" });
        foreach (var r in Rungs) r.Logic = Normalize(r.Logic);
        Selection = Rungs.Count > 0 ? FirstLeafOr(0) : null;
        Raise();
        return EditResult.Ok($"{what}. Undo (Ctrl+Z) brings the old program back.");
    }

    // ---- Rungs --------------------------------------------------------------------------------------

    /// <summary>Adds a rung below the selected one (or at the end): a contact and a coil, both waiting for a tag.</summary>
    public EditResult AddRung()
    {
        Checkpoint("added a rung");
        var at = Selection is null ? Rungs.Count : Selection.Rung + 1;
        Rungs.Insert(at, new Rung { Logic = new SeriesElement { Items = [new ContactElement(), new CoilElement()] } });
        Selection = new LadderSelection(at, [0]);
        Raise();
        return EditResult.Ok($"Added rung {at + 1}: a contact and a coil. Type a tag for each.");
    }

    public EditResult DeleteRung()
    {
        if (Selection is null || Rungs.Count == 0) return EditResult.No("Select a rung first.");
        Checkpoint("deleted a rung");
        var at = Selection.Rung;
        Rungs.RemoveAt(at);
        Selection = Rungs.Count == 0 ? null : FirstLeafOr(Math.Min(at, Rungs.Count - 1));
        Raise();
        return EditResult.Ok($"Deleted rung {at + 1}. Undo (Ctrl+Z) brings it back.");
    }

    /// <summary>Moves the selected rung up (−1) or down (+1). The order matters: rungs run top to bottom.</summary>
    public EditResult MoveRung(int step)
    {
        if (Selection is null) return EditResult.No("Select a rung first.");
        var from = Selection.Rung;
        var to = from + step;
        if (to < 0 || to >= Rungs.Count) return EditResult.No(step < 0 ? "This is already the first rung." : "This is already the last rung.");
        Checkpoint("moved a rung");
        (Rungs[from], Rungs[to]) = (Rungs[to], Rungs[from]);
        Selection = new LadderSelection(to, Selection.Path, Selection.Length);
        Raise();
        return EditResult.Ok($"Moved the rung to position {to + 1}.");
    }

    public EditResult SetRungComment(int rung, string? comment)
    {
        if (rung < 0 || rung >= Rungs.Count) return EditResult.No("There is no such rung.");
        var text = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (text == Rungs[rung].Comment) return EditResult.No("");
        Checkpoint("changed a rung comment");
        Rungs[rung].Comment = text;
        Raise();
        return EditResult.Ok("Rung comment saved.");
    }

    /// <summary>The selected rung as text, for the clipboard.</summary>
    public string? CopyRung() =>
        Selection is null ? null : JsonSerializer.Serialize(Rungs[Selection.Rung], ProjectJson.Options);

    /// <summary>Pastes a copied rung below the selected one.</summary>
    public EditResult PasteRung(string? text)
    {
        Rung? rung;
        try { rung = string.IsNullOrWhiteSpace(text) ? null : JsonSerializer.Deserialize<Rung>(text, ProjectJson.Options); }
        catch (JsonException) { rung = null; }
        if (rung is null) return EditResult.No("The clipboard does not hold a copied rung. Copy a rung first (Ctrl+C).");
        Checkpoint("pasted a rung");
        rung.Logic = Normalize(rung.Logic);
        var at = Selection is null ? Rungs.Count : Selection.Rung + 1;
        Rungs.Insert(at, rung);
        Selection = FirstLeafOr(at);
        Raise();
        return EditResult.Ok($"Pasted as rung {at + 1}.");
    }

    // ---- Elements -----------------------------------------------------------------------------------

    public EditResult InsertContact(ContactKind kind, bool before = false) =>
        Insert(new ContactElement { Contact = kind }, before, $"Added a {Words(kind)} contact. Type its tag.");

    public EditResult InsertCoil(CoilKind kind, bool before = false) =>
        Insert(new CoilElement { Coil = kind }, before, $"Added a {Words(kind)} coil. Type its tag.");

    /// <summary>Adds a box (TON, CTU, GT, ADD, MOVE, …). A timer or counter gets its own new instance tag (T1, C1, …).</summary>
    public EditResult InsertBox(string instructionName, bool before = false)
    {
        var instruction = _dialect.Find(instructionName);
        if (instruction is null) return EditResult.No($"'{instructionName}' is not an instruction this PLC knows.");
        var box = new BoxElement { Instruction = instruction.Name };
        foreach (var op in instruction.Operands.Where(o => o.Required))
            box.Operands[op.Name] = DefaultOperand(instruction.Name, op);
        string? declared = null;
        if (instruction.FunctionBlockType is { } fb)
        {
            var prefix = fb.StartsWith('C') ? "C" : "T";
            var n = 1;
            while (Find(prefix + n) is not null) n++;
            box.Instance = prefix + n;
            declared = box.Instance;
        }
        var result = Insert(box, before, "");
        if (!result.Done) return result;
        if (declared is not null)
            Project.Tags.Add(new TagDeclaration { Name = declared, Type = instruction.FunctionBlockType!, Comment = "declared when the box was added" });
        Raise();
        return EditResult.Ok($"Added {instruction.Name}{(declared is null ? "" : $" ({declared})")}: {instruction.Summary} Fill in its values.");
    }

    private static string DefaultOperand(string instruction, OperandSpec op) => op.Kind switch
    {
        OperandKind.Time => "T#1s",
        OperandKind.Integer when op.Name == "PV" => "10",
        _ when op.Direction == OperandDirection.In && op.Name == "IN2" => instruction is "DIV" or "MOD" ? "1" : "0",
        _ when instruction == "MOVE" && op.Name == "IN" => "0",
        _ => "",
    };

    private EditResult Insert(LadderElement element, bool before, string message)
    {
        if (Rungs.Count == 0)
        {
            Checkpoint("added an element");
            Rungs.Add(new Rung { Logic = new SeriesElement { Items = [element] } });
            Selection = new LadderSelection(0, [0]);
            Raise();
            return EditResult.Ok(message);
        }
        var sel = Selection ?? LadderSelection.OfRung(Rungs.Count - 1);
        Checkpoint("added an element");
        if (sel.IsRung)
        {
            var root = (SeriesElement)Rungs[sel.Rung].Logic;
            // A contact goes before the rung's outputs; anything else at the end.
            var at = element is ContactElement ? FirstOutputIndex(root) : root.Items.Count;
            root.Items.Insert(at, element);
            Selection = new LadderSelection(sel.Rung, [at]);
        }
        else
        {
            if (!TryLocate(sel.Rung, sel.Path, out var parent, out var index)) return EditResult.No("Select an element first.");
            // A contact added after a coil or box goes before it: power has to pass a contact before reaching an output.
            var selected = parent.Items[index];
            if (element is ContactElement && !before && selected is CoilElement or BoxElement && index == parent.Items.Count - 1 - (sel.Length - 1))
                before = true;
            var at = before ? index : index + sel.Length;
            parent.Items.Insert(at, element);
            Selection = new LadderSelection(sel.Rung, [.. sel.Path.Take(sel.Path.Count - 1), at]);
        }
        Raise();
        return EditResult.Ok(message);
    }

    private static int FirstOutputIndex(SeriesElement s)
    {
        for (var i = 0; i < s.Items.Count; i++)
            if (s.Items[i] is CoilElement or BoxElement) return i;
        return s.Items.Count;
    }

    /// <summary>Deletes the selected element(s). A branch left with one path stops being a branch.</summary>
    public EditResult DeleteSelected()
    {
        if (Selection is null) return EditResult.No("Select an element first.");
        if (Selection.IsRung) return DeleteRung();
        if (!TryLocate(Selection.Rung, Selection.Path, out var parent, out var index)) return EditResult.No("Select an element first.");
        Checkpoint("deleted");
        var count = Math.Min(Selection.Length, parent.Items.Count - index);
        parent.Items.RemoveRange(index, count);
        var rung = Selection.Rung;
        Rungs[rung].Logic = Normalize(Rungs[rung].Logic);
        var leaves = Leaves(rung);
        // The selection moves to the element before the deleted one (or the first, if it was first).
        Selection = leaves.Count == 0 ? LadderSelection.OfRung(rung)
            : new LadderSelection(rung, leaves.LastOrDefault(l => Compare(l, Selection.Path) < 0) ?? leaves[0]);
        Raise();
        return EditResult.Ok(count == 1 ? "Deleted. Undo (Ctrl+Z) brings it back." : $"Deleted {count} elements. Undo (Ctrl+Z) brings them back.");
    }

    /// <summary>
    /// Puts a parallel branch around the selected element(s), with a new contact on the second path: the way
    /// an OR (a seal-in) is drawn. If the selection already fills one path of a branch, adds another path to it.
    /// </summary>
    public EditResult AddBranch()
    {
        if (Selection is not { IsRung: false } sel || !TryLocate(sel.Rung, sel.Path, out var parent, out var index))
            return EditResult.No("Select the element (or elements, with Shift+arrow) to put a branch around.");
        Checkpoint("added a branch");
        var count = Math.Min(sel.Length, parent.Items.Count - index);

        // A whole branch is selected: add a path to it.
        if (count == 1 && parent.Items[index] is ParallelElement selectedBranch)
        {
            selectedBranch.Branches.Add(new SeriesElement { Items = [new ContactElement()] });
            Selection = new LadderSelection(sel.Rung, [.. sel.Path, selectedBranch.Branches.Count - 1, 0]);
            Raise();
            return EditResult.Ok("Added another path to the branch, with a contact on it. Type its tag.");
        }

        // The selection fills a whole path of an existing branch: add a path to that branch.
        if (sel.Path.Count >= 3 && index == 0 && count == parent.Items.Count
            && TryLocate(sel.Rung, sel.Path.Take(sel.Path.Count - 2).ToList(), out var outer, out var parIndex)
            && outer.Items[parIndex] is ParallelElement existing)
        {
            existing.Branches.Add(new SeriesElement { Items = [new ContactElement()] });
            Selection = new LadderSelection(sel.Rung, [.. sel.Path.Take(sel.Path.Count - 2), parIndex, existing.Branches.Count - 1, 0]);
            Raise();
            return EditResult.Ok("Added another path to the branch, with a contact on it. Type its tag.");
        }

        var wrapped = parent.Items.GetRange(index, count);
        parent.Items.RemoveRange(index, count);
        var branch = new ParallelElement
        {
            Branches = [new SeriesElement { Items = wrapped }, new SeriesElement { Items = [new ContactElement()] }],
        };
        parent.Items.Insert(index, branch);
        Selection = new LadderSelection(sel.Rung, [.. sel.Path.Take(sel.Path.Count - 1), index, 1, 0]);
        Raise();
        return EditResult.Ok("Added a branch: power can now pass either path (OR). Type the tag of the new contact. To add something after the branch, select the whole branch (click its right-hand side, or press →) or the coil.");
    }

    // ---- What an element is ---------------------------------------------------------------------------

    public EditResult SetContactKind(ContactKind kind)
    {
        if (SelectedElement is not ContactElement c) return EditResult.No("Select a contact first.");
        if (c.Contact == kind) return EditResult.No("");
        Checkpoint("changed a contact");
        c.Contact = kind;
        Raise();
        return EditResult.Ok($"Now a {Words(kind)} contact.");
    }

    public EditResult SetCoilKind(CoilKind kind)
    {
        if (SelectedElement is not CoilElement c) return EditResult.No("Select a coil first.");
        if (c.Coil == kind) return EditResult.No("");
        Checkpoint("changed a coil");
        c.Coil = kind;
        Raise();
        return EditResult.Ok($"Now a {Words(kind)} coil.");
    }

    /// <summary>
    /// Sets the tag of the selected contact or coil. A new name is declared on first use as BOOL
    /// (spec 5.1), so a learner can type as they think.
    /// </summary>
    public EditResult SetTag(string? text)
    {
        var name = (text ?? "").Trim();
        if (SelectedElement is not (ContactElement or CoilElement)) return EditResult.No("Select a contact or coil first.");
        var current = SelectedElement is ContactElement ce ? ce.Tag : ((CoilElement)SelectedElement).Tag;
        if (name == current) return EditResult.No("");
        if (name.Length > 0 && PlcValue.TryParseLiteral(name, out _))
            return EditResult.No($"'{name}' is a value, not a tag. A contact or coil needs the name of a BOOL tag, such as Start or Motor.");
        if (name.Length > 0 && !LooksLikeTag(name))
            return EditResult.No($"'{name}' is not a tag name. A name starts with a letter and has only letters, digits and _ (for example Start or Motor_1).");
        Checkpoint("changed a tag");
        if (SelectedElement is ContactElement c) c.Tag = name; else ((CoilElement)SelectedElement!).Tag = name;
        var declared = DeclareOnFirstUse(name, "BOOL");
        Raise();
        if (!declared) return EditResult.Ok(name.Length == 0 ? "Tag cleared." : $"Uses {name}.");
        var near = ClosestTag(name);
        return EditResult.Ok(near is null
            ? $"'{name}' is a new tag: declared as BOOL (on/off)."
            : $"'{name}' is a new tag (declared as BOOL). Did you mean {near}? If so, Undo (Ctrl+Z) and type {near}.");
    }

    /// <summary>Sets an operand of the selected box: a value (T#2s, 10) or a tag, declared on first use with a fitting type.</summary>
    public EditResult SetOperand(string operand, string? text)
    {
        if (SelectedElement is not BoxElement box) return EditResult.No("Select a box first.");
        var spec = _dialect.Find(box.Instruction)?.Operands.FirstOrDefault(o => o.Name.Equals(operand, StringComparison.OrdinalIgnoreCase));
        if (spec is null) return EditResult.No($"{box.Instruction} has no {operand}.");
        var value = (text ?? "").Trim();
        box.Operands.TryGetValue(spec.Name, out var current);
        if (value == (current ?? "")) return EditResult.No("");
        var literal = PlcValue.TryParseLiteral(value, out _);
        if (value.Length > 0 && !literal && !LooksLikeTag(value))
            return EditResult.No($"'{value}' is neither a value (such as 10, 2.5 or T#2s) nor a tag name.");
        Checkpoint("changed a value in a box");
        if (value.Length == 0) box.Operands.Remove(spec.Name); else box.Operands[spec.Name] = value;
        var declared = !literal && DeclareOnFirstUse(value, spec.Kind switch
        {
            OperandKind.Bool => "BOOL",
            OperandKind.Time => "TIME",
            OperandKind.Number when spec.Direction == OperandDirection.Out => "INT",
            _ => "INT",
        });
        Raise();
        return EditResult.Ok(declared ? $"'{value}' is a new tag: declared as {Find(value)!.Type}." : $"{spec.Name} is {(value.Length == 0 ? "empty" : value)}.");
    }

    // ---- Tags ---------------------------------------------------------------------------------------

    /// <summary>An existing tag spelled almost the same (a likely typo), or null.</summary>
    public string? ClosestTag(string name) =>
        Project.Tags.Where(t => !t.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .Select(t => (t.Name, Distance: Distance(t.Name.ToUpperInvariant(), name.ToUpperInvariant())))
            .Where(x => x.Distance <= 2 && x.Distance < Math.Max(2, name.Length / 2))
            .OrderBy(x => x.Distance).Select(x => x.Name).FirstOrDefault();

    private static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
            {
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        return d[a.Length, b.Length];
    }

    public TagDeclaration? Find(string name) =>
        Project.Tags.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The names a learner can pick for a contact or coil: BOOL tags and timer/counter outputs (T1.Q).</summary>
    public IReadOnlyList<string> BoolTagNames()
    {
        var names = new List<string>();
        foreach (var t in Project.Tags)
        {
            if (t.Type.Equals("BOOL", StringComparison.OrdinalIgnoreCase)) names.Add(t.Name);
            else if (_dialect.Find(t.Type) is { FunctionBlockType: not null } fb)
                names.AddRange(fb.Members.Where(m => m.Type == DataType.Bool && m.Name is "Q" or "QU" or "QD").Select(m => $"{t.Name}.{m.Name}"));
        }
        return names;
    }

    /// <summary>A plain name (Start), a member (T1.Q) or an element (Values[2]) — the shapes a tag reference can take.</summary>
    private static bool LooksLikeTag(string s) => TypeSystem.ParsePath(s) is not null;

    private bool DeclareOnFirstUse(string name, string type)
    {
        if (!Identifier.IsMatch(name) || Find(name) is not null) return false;
        if (PlcValue.TryParseType(name, out _) || _dialect.Find(name) is not null) return false;
        Project.Tags.Add(new TagDeclaration { Name = name, Type = type, Comment = "declared on first use" });
        return true;
    }

    /// <summary>Renames a tag everywhere it is used.</summary>
    public EditResult RenameTag(string from, string to)
    {
        var tag = Find(from);
        if (tag is null) return EditResult.No($"There is no tag called '{from}'.");
        to = to.Trim();
        if (!Identifier.IsMatch(to)) return EditResult.No($"'{to}' is not a tag name.");
        if (Find(to) is not null && !to.Equals(from, StringComparison.OrdinalIgnoreCase)) return EditResult.No($"There is already a tag called '{to}'.");
        Checkpoint("renamed a tag");
        tag.Name = to;
        foreach (var r in Rungs) Rename(r.Logic, from, to);
        Raise();
        return EditResult.Ok($"Renamed {from} to {to} everywhere it is used.");
    }

    private static void Rename(LadderElement e, string from, string to)
    {
        string Fix(string s) =>
            s.Equals(from, StringComparison.OrdinalIgnoreCase) ? to
            : s.StartsWith(from + ".", StringComparison.OrdinalIgnoreCase) || s.StartsWith(from + "[", StringComparison.OrdinalIgnoreCase) ? to + s[from.Length..]
            : s;
        switch (e)
        {
            case SeriesElement s: foreach (var i in s.Items) Rename(i, from, to); break;
            case ParallelElement p: foreach (var b in p.Branches) Rename(b, from, to); break;
            case ContactElement c: c.Tag = Fix(c.Tag); break;
            case CoilElement c: c.Tag = Fix(c.Tag); break;
            case BoxElement b:
                if (b.Instance is not null) b.Instance = Fix(b.Instance);
                foreach (var k in b.Operands.Keys.ToList()) b.Operands[k] = Fix(b.Operands[k]);
                break;
        }
    }

    // ---- Tree helpers -----------------------------------------------------------------------------------

    private bool TryLocate(int rung, IReadOnlyList<int> path, out SeriesElement parent, out int index)
    {
        parent = null!;
        index = -1;
        if (rung < 0 || rung >= Rungs.Count || path.Count == 0 || path.Count % 2 == 0) return false;
        var series = (SeriesElement)Rungs[rung].Logic;
        for (var k = 0; ; k += 2)
        {
            var i = path[k];
            if (i < 0 || i >= series.Items.Count) return false;
            if (k == path.Count - 1) { parent = series; index = i; return true; }
            if (series.Items[i] is not ParallelElement p) return false;
            var b = path[k + 1];
            if (b < 0 || b >= p.Branches.Count || p.Branches[b] is not SeriesElement next) return false;
            series = next;
        }
    }

    /// <summary>
    /// The editor's shape of a rung: the top is a series; every branch path is a series; a series never sits
    /// directly inside a series; a branch with one path is just that path. The program means exactly the same.
    /// </summary>
    public static LadderElement Normalize(LadderElement e)
    {
        var root = e as SeriesElement ?? new SeriesElement { Items = [e] };
        return NormalizeSeries(root);
    }

    private static SeriesElement NormalizeSeries(SeriesElement s)
    {
        var items = new List<LadderElement>();
        foreach (var item in s.Items)
        {
            switch (item)
            {
                case SeriesElement inner:
                    items.AddRange(NormalizeSeries(inner).Items);
                    break;
                case ParallelElement p:
                    var branches = p.Branches
                        .Select(b => NormalizeSeries(b as SeriesElement ?? new SeriesElement { Items = [b] }))
                        .Where(b => b.Items.Count > 0).Cast<LadderElement>().ToList();
                    if (branches.Count == 1) items.AddRange(((SeriesElement)branches[0]).Items);
                    else if (branches.Count > 1) items.Add(new ParallelElement { Branches = branches });
                    break;
                default:
                    items.Add(item);
                    break;
            }
        }
        return new SeriesElement { Items = items };
    }

    private static int Compare(IReadOnlyList<int> a, IReadOnlyList<int> b)
    {
        for (var i = 0; i < Math.Min(a.Count, b.Count); i++)
            if (a[i] != b[i]) return a[i].CompareTo(b[i]);
        return a.Count.CompareTo(b.Count);
    }

    private static PlcProject Clone(PlcProject p) => ProjectJson.Deserialize(ProjectJson.Serialize(p));

    public static string Words(ContactKind k) => k switch
    {
        ContactKind.NormallyClosed => "normally closed",
        ContactKind.PositiveEdge => "rising-edge",
        ContactKind.NegativeEdge => "falling-edge",
        _ => "normally open",
    };

    public static string Words(CoilKind k) => k switch
    {
        CoilKind.Negated => "negated",
        CoilKind.Set => "set (latch)",
        CoilKind.Reset => "reset (unlatch)",
        _ => "normal",
    };

    /// <summary>A program for a machine with nothing written yet: its inputs and outputs declared from the wiring, one empty rung.</summary>
    public static PlcProject Blank(string name, IEnumerable<(string Tag, bool Input, string Meaning)> wiring) => new()
    {
        Name = name,
        Tags = [.. wiring.Select(w => new TagDeclaration { Name = w.Tag, Type = "BOOL", Io = w.Input ? IoDirection.Input : IoDirection.Output, Comment = w.Meaning })],
        Programs = [new ProgramUnit { Name = "Main", Rungs = [new Rung { Logic = new SeriesElement { Items = [new ContactElement(), new CoilElement()] } }] }],
    };
}
