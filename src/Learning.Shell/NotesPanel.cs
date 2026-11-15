using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Learning.Core.Content;
using Learning.Core.Learners;
using Learning.Core.Notes;

namespace Learning.Shell;

/// <summary>
/// My notes (Learning Core plan 3.2): the learner's own notes, kept in their progress file. One control, two places:
/// beside an item (its notes, and a box to write a new one: on the item, or on the paragraph the learner right-clicked),
/// and as the My notes page in the path (every note, with search, each opening its item).
/// </summary>
public sealed class NotesPanel : UserControl
{
    /// <summary>The page in the path: every note, with search, instead of one item's.</summary>
    public bool AllNotes { get; init; }

    public Func<LearnerFile?> Session { get; set; } = () => null;
    public Func<string, LearningItem?> FindItem { get; set; } = _ => null;
    /// <summary>A note was added, changed or deleted: the window saves and redraws the marks.</summary>
    public Action Changed { get; set; } = () => { };
    public Action<string> Status { get; set; } = _ => { };
    /// <summary>Go to a note: open its item and tab, and show the paragraph it is on.</summary>
    public Action<LearnerNote> GoTo { get; set; } = _ => { };

    /// <summary>The tab showing now ("learn", "example", …): a note written in the box without a paragraph goes on it.</summary>
    public string? CurrentTab
    {
        get => _tab;
        set
        {
            _tab = value;
            if (_built && !AllNotes) ShowOn(); // the box says where a new note will go
        }
    }

    private string? _tab;

    private readonly StackPanel _list = new();
    private readonly TextBlock _heading = new();
    private readonly TextBox _box = new();
    private readonly TextBlock _onText = new();
    private readonly Button _wholeItem = new();
    private readonly TextBox _search = new();
    private readonly TextBlock _found = new();

    private LearningItem? _item;
    private NoteAnchor? _pending;
    private IReadOnlyDictionary<string, NoteLocation> _placed = new Dictionary<string, NoteLocation>();

    /// <summary>
    /// The note being edited, what is typed in it, and its box. The edit survives the list being drawn again (a search, a new
    /// note, the marks), and leaving the item or locking keeps what was typed (UX review 2026-11-15).
    /// </summary>
    private string? _editId;
    private string? _editDraft;
    private TextBox? _editBox;

    /// <summary>The page in the path asks the window for an item's page (Learn or Reference), to say whether a note's paragraph is still there.</summary>
    public Func<LearningItem, string, string> PageText { get; set; } = (_, _) => "";

    public NotesPanel()
    {
        Loaded += (_, _) => Build();
    }

    private bool _built;

    private void Build()
    {
        if (_built) return;
        _built = true;
        var top = new StackPanel();
        _heading.Style = (Style)FindResource(AllNotes ? "SectionTitle" : "PanelTitle");
        _heading.TextWrapping = TextWrapping.Wrap;
        AutomationProperties.SetHeadingLevel(_heading, AllNotes ? AutomationHeadingLevel.Level1 : AutomationHeadingLevel.Level2);
        top.Children.Add(_heading);
        top.Children.Add(Lead(AllNotes
            ? "Every note you have written, newest first. Only you see them: they are kept in your progress file, locked with your PIN, " +
              "backed up with it, and never put in a report. Press Open to go to the note in its lesson."
            : "Your own notes on this item. Only you see them: they are kept in your progress file, locked with your PIN, and never put " +
              "in a report. To put a note on one paragraph, right-click it on the Learn or Reference tab."));

        if (AllNotes)
        {
            var label = new TextBlock { Text = "Find in my notes", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 2) };
            _search.ToolTip = "Type words to find: notes whose words, or the paragraph they are on, have them all. Empty shows every note.";
            _search.MaxWidth = 480;
            _search.HorizontalAlignment = HorizontalAlignment.Left;
            _search.Width = 480;
            _search.Padding = new Thickness(4, 3, 4, 3);
            AutomationProperties.SetName(_search, "Find in my notes");
            AutomationProperties.SetLabeledBy(_search, label);
            _search.TextChanged += (_, _) => ShowAll();
            top.Children.Add(label);
            top.Children.Add(_search);
            _found.Style = (Style)FindResource("Lead");
            _found.Margin = new Thickness(0, 4, 0, 6);
            AutomationProperties.SetLiveSetting(_found, AutomationLiveSetting.Polite);
            top.Children.Add(_found);
        }
        else
        {
            var compose = new StackPanel { Margin = new Thickness(0, 2, 0, 10) };
            _onText.TextWrapping = TextWrapping.Wrap;
            _onText.FontWeight = FontWeights.SemiBold;
            _onText.Margin = new Thickness(0, 0, 0, 3);
            compose.Children.Add(_onText);
            _wholeItem.Content = "Put it on the whole item instead";
            _wholeItem.ToolTip = "The note goes on the item as a whole, not on one paragraph";
            _wholeItem.HorizontalAlignment = HorizontalAlignment.Left;
            _wholeItem.Margin = new Thickness(0, 0, 0, 4);
            _wholeItem.Padding = new Thickness(6, 1, 6, 1);
            _wholeItem.Click += (_, _) => { _pending = null; ShowOn(); _box.Focus(); };
            compose.Children.Add(_wholeItem);
            _box.AcceptsReturn = true;
            _box.TextWrapping = TextWrapping.Wrap;
            _box.MinHeight = 72;
            _box.MaxHeight = 200;
            _box.MaxLength = NoteBook.MaxLength;
            _box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _box.Padding = new Thickness(4, 3, 4, 3);
            _box.ToolTip = "Type your note: a question for class, a reminder, the idea in your own words. Ctrl+Enter adds it.";
            AutomationProperties.SetLabeledBy(_box, _onText);
            _box.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; Add(); }
                else if (e.Key == Key.Escape && _box.Text.Length == 0 && _pending is not null) { e.Handled = true; _pending = null; ShowOn(); }
            };
            compose.Children.Add(_box);
            var buttons = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            var add = new Button { Content = "_Add note", ToolTip = "Keeps the note in your progress file (Ctrl+Enter)" };
            add.Click += (_, _) => Add();
            buttons.Children.Add(add);
            compose.Children.Add(buttons);
            top.Children.Add(compose);
        }

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _list, Focusable = false };
        AutomationProperties.SetName(_list, AllNotes ? "My notes" : "My notes on this item");
        var dock = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        dock.Children.Add(top);
        dock.Children.Add(scroll);
        Content = AllNotes ? dock : new Border
        {
            Child = dock,
            Padding = new Thickness(14, 10, 12, 10),
            Background = (Brush)FindResource("PanelBrush"),
            BorderBrush = (Brush)FindResource("LineBrush"),
            BorderThickness = new Thickness(1, 0, 0, 0),
        };
        if (AllNotes) ShowAll();
        else Redraw();
    }

    private TextBlock Lead(string text) => new() { Text = text, Style = (Style)FindResource("Lead") };

    // ---- Beside an item ---------------------------------------------------------------------------------

    /// <summary>Shows an item's notes. <paramref name="placed"/> says where each note's paragraph is on the page now.</summary>
    public void ShowItem(LearningItem item, IReadOnlyDictionary<string, NoteLocation> placed)
    {
        if (_item != item)
        {
            KeepDraft();
            _pending = null;
            _highlight = null;
        }
        _item = item;
        _placed = placed;
        if (_built) Redraw();
    }

    /// <summary>Starts a note on a paragraph (or on the item, if <paramref name="anchor"/> names no block) and puts the cursor in the box.</summary>
    public void StartNote(NoteAnchor anchor)
    {
        _pending = anchor.Block is null && anchor.Tab is null ? null : anchor;
        if (!_built) Build();
        ShowOn();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () => { _box.Focus(); Keyboard.Focus(_box); });
        Status(_pending is null
            ? $"Write your note on {_item?.Number} in the box under My notes (right), then press Add note or Ctrl+Enter."
            : "Write your note on that paragraph in the box under My notes (right), then press Add note or Ctrl+Enter. The paragraph gets a bar on its left.");
    }

    private void ShowOn()
    {
        if (_item is null) return;
        _onText.Text = _pending?.Quote is { } quote ? $"New note on the paragraph: “{Short(quote, 90)}”"
            : _pending?.Tab is { } page ? $"New note on {_item.Number}'s {TabName(page)} {(page is NoteTabs.Learn or NoteTabs.Reference ? "page" : "tab")}"
            : CurrentTab is NoteTabs.Example or NoteTabs.Exercise ? $"New note on {_item.Number}'s {TabName(CurrentTab)} tab"
            : $"New note on {_item.Number} {_item.Title}";
        _wholeItem.Visibility = _pending is null ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetName(_box, _onText.Text);
    }

    private void Add()
    {
        if (_item is null || Session() is not { } s)
        {
            Status("Choose an item in the path first: a note goes on the item that is open. Your words stay in the box.");
            return;
        }
        if (_box.Text.Trim().Length == 0)
        {
            Status("The note is empty: type something in the box first.");
            _box.Focus();
            return;
        }
        var anchor = _pending ?? NoteAnchor.OnItem(_item.Id, CurrentTab is NoteTabs.Example or NoteTabs.Exercise ? CurrentTab : null);
        s.Data.Notes.Add(anchor, _box.Text, DateTimeOffset.Now);
        _box.Clear();
        _pending = null;
        Changed();
        Status($"Note added to {_item.Number}. It is in your progress file; see all your notes under My notes in the path.");
    }


    /// <summary>
    /// What was typed and not yet added is kept, not lost: leaving the item or locking adds the half-written note, and keeps an
    /// edit (the checklist: no surprise loss of work). Returns what was kept, in words, or null.
    /// </summary>
    public string? KeepDraft()
    {
        var kept = CommitEdit();
        if (_item is not null && _box.Text.Trim().Length > 0 && Session() is { } session)
        {
            var anchor = _pending ?? NoteAnchor.OnItem(_item.Id, CurrentTab is NoteTabs.Example or NoteTabs.Exercise ? CurrentTab : null);
            session.Data.Notes.Add(anchor, _box.Text, DateTimeOffset.Now);
            kept = (kept is null ? "" : kept + " ") + $"The note you were writing on {_item.Number} was added to My notes.";
            _box.Clear();   // only once it is kept: a box with nowhere to go keeps its words
            _pending = null;
        }
        if (kept is not null) Changed();
        return kept;
    }

    /// <summary>Keeps an open edit's words (an emptied box leaves the note as it was: deleting asks first). Returns what was kept.</summary>
    private string? CommitEdit()
    {
        var text = _editBox?.Text ?? _editDraft;
        var id = _editId;
        (_editId, _editDraft, _editBox) = (null, null, null);
        if (id is null || text is null || Session() is not { } s || s.Data.Notes.Find(id) is not { } note) return null;
        if (text.Trim().Length == 0 || text.Trim() == note.Text) return null;
        s.Data.Notes.Edit(id, text, DateTimeOffset.Now);
        return "Your change to a note was kept.";
    }

    /// <summary>Another learner, or nobody: nothing of the last learner's notes stays on screen.</summary>
    public void Forget()
    {
        _box.Clear();
        _search.Clear();
        _pending = null;
        (_editId, _editDraft, _editBox) = (null, null, null);
        _highlight = null;
        _item = null;
        _list.Children.Clear();
        _cards.Clear();
    }

    /// <summary>Before the list is drawn again, an open edit's words are taken from its box, so the new card shows them.</summary>
    private void ClearList()
    {
        if (_editBox is not null) _editDraft = _editBox.Text;
        _editBox = null;
        _list.Children.Clear();
        _cards.Clear();
    }

    private void Redraw()
    {
        if (AllNotes) { ShowAll(); return; }
        ClearList();
        if (_item is null || Session() is not { } s) return;
        var notes = s.Data.Notes.For(_item.Id);
        _heading.Text = notes.Count == 0 ? "My notes" : $"My notes ({notes.Count})";
        ShowOn();
        if (notes.Count == 0)
        {
            _list.Children.Add(Lead("No notes on this item yet. Write one in the box above, or right-click a paragraph of the lesson and choose Add a note to this paragraph."));
            return;
        }
        foreach (var note in notes.Reverse()) _list.Children.Add(Card(note, showItem: false));
    }

    // ---- The My notes page ------------------------------------------------------------------------------

    /// <summary>Every note (or those the search finds), newest first.</summary>
    public void ShowAll()
    {
        if (!_built) return;
        ClearList();
        _heading.Text = "My notes";
        if (Session() is not { } s) return;
        var all = s.Data.Notes.Live.OrderByDescending(n => n.Created).ToList();
        var query = _search.Text.Trim();
        var shown = query.Length == 0 ? all : [.. s.Data.Notes.Search(query).OrderByDescending(n => n.Created)];
        // A note being edited stays on screen, even when the search would leave it out.
        if (_editId is not null && shown.All(n => n.Id != _editId) && s.Data.Notes.Find(_editId) is { } editing) shown.Insert(0, editing);
        _found.Text = all.Count == 0 ? ""
            : query.Length == 0 ? $"{all.Count} note{(all.Count == 1 ? "" : "s")} on {all.Select(n => n.Anchor.ItemId).Distinct(StringComparer.OrdinalIgnoreCase).Count()} item(s)."
            : shown.Count == 0 ? $"No note has all of “{query}”. Try fewer words, or the start of a word."
            : $"{shown.Count} of your {all.Count} notes have “{query}”.";
        if (all.Count == 0)
        {
            _list.Children.Add(Lead("You have no notes yet. On any item, press ✎ My notes (top right) and write one, or right-click a paragraph of a lesson " +
                                    "and choose Add a note to this paragraph. Your notes come together here, so you can read them again before a module check."));
            return;
        }
        foreach (var note in shown) _list.Children.Add(Card(note, showItem: true));
    }

    /// <summary>
    /// On the My notes page: is a note's paragraph still on its page, word for word? Without drawing the page, its words are
    /// looked for in the page's Markdown. False means it changed or is gone (Open says which).
    /// </summary>
    private bool StillThere(LearnerNote note, LearningItem item)
    {
        var quote = BlockText.Words(note.Anchor.Quote);
        if (note.Anchor.Quote?.EndsWith('…') == true && quote.Count > 1) quote = [.. quote.Take(quote.Count - 1)]; // the cut last word
        if (quote.Count == 0) return true;
        var page = " " + string.Join(' ', BlockText.Words(PageText(item, note.Anchor.Tab ?? NoteTabs.Learn))) + " ";
        return page.Contains(" " + string.Join(' ', quote) + " ", StringComparison.Ordinal);
    }

    // ---- One note ---------------------------------------------------------------------------------------

    private Border Card(LearnerNote note, bool showItem)
    {
        var item = FindItem(note.Anchor.ItemId);
        var panel = new StackPanel();
        if (showItem)
            panel.Children.Add(new TextBlock
            {
                Text = item is null ? "An item that is no longer in this version of PLC+" : $"{item.Number} {item.Title}",
                FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap,
            });
        var where = Where(note, showItem, item);
        panel.Children.Add(new TextBlock { Text = where, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("MutedTextBrush"), Margin = new Thickness(0, 0, 0, 3) });

        if (note.Id == _editId)
        {
            EditParts(note, panel);
        }
        else
        {
            panel.Children.Add(new TextBlock { Text = note.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 3) });
            var when = note.Modified > note.Created.AddMinutes(1) ? $"Written {Words.When(note.Created)}, changed {Words.When(note.Modified)}" : $"Written {Words.When(note.Created)}";
            panel.Children.Add(new TextBlock { Text = when, FontSize = 12, Foreground = (Brush)FindResource("MutedTextBrush") });

            var buttons = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            // Show goes to the paragraph (if it is still there) or the tab; a note on the whole item has nowhere else to go.
            var canGo = item is not null && (showItem
                || (note.Anchor.Block is null ? note.Anchor.Tab is not null : _placed.TryGetValue(note.Id, out var at) && at.BlockIndex is not null));
            if (canGo)
            {
                var go = SmallButton(showItem ? "Open" : "Show", showItem
                    ? $"Opens {item!.Number} {item.Title} and shows where this note is"
                    : "Shows the paragraph or tab this note is on");
                go.Click += (_, _) => GoTo(note);
                buttons.Children.Add(go);
            }
            var edit = SmallButton("Edit", "Change the words of this note");
            var delete = SmallButton("Delete", "Deletes this note (it asks first)");
            edit.Click += (_, _) => StartEdit(note);
            delete.Click += (_, _) => Delete(note);
            buttons.Children.Add(edit);
            buttons.Children.Add(delete);
            panel.Children.Add(buttons);
        }

        var card = new Border
        {
            Child = panel,
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 8),
            BorderBrush = (Brush)FindResource("LineBrush"),
            BorderThickness = new Thickness(4, 1, 1, 1),
            Background = (Brush)FindResource("PageBrush"),
        };
        AutomationProperties.SetName(card, $"Note: {note.Text}. {where}");
        _cards[note.Id] = card;
        if (note.Id == _highlight)
        {
            // Marked by a heavier frame and a word, not by colour alone.
            card.BorderBrush = (Brush)FindResource("AccentBrush");
            card.BorderThickness = new Thickness(6, 3, 3, 3);
            panel.Children.Insert(0, new TextBlock { Text = "▶ The note you went to", FontSize = 12, FontWeight = FontWeights.SemiBold });
        }
        return card;
    }

    private readonly Dictionary<string, Border> _cards = [];
    private string? _highlight;

    /// <summary>Marks the note the learner went to (from the My notes page or Show) and scrolls it into view.</summary>
    public void Highlight(string noteId)
    {
        _highlight = noteId;
        Refresh();
        if (_cards.TryGetValue(noteId, out var card)) card.BringIntoView();
    }

    private static Button SmallButton(string content, string tip) => new() { Content = content, ToolTip = tip, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 6, 0) };

    private const string MoveIt = " To put this note on another paragraph: right-click that paragraph, add a note there, copy these words into it, then delete this one.";

    private string Where(LearnerNote note, bool page, LearningItem? item)
    {
        var tab = TabName(note.Anchor.Tab);
        if (note.Anchor.Block is not null)
        {
            var quote = $"“{Short(note.Anchor.Quote ?? "", 120)}”";
            if (page)
                return item is null || StillThere(note, item)
                    ? $"On a paragraph of the {tab} page: {quote}"
                    : $"On a paragraph of the {tab} page that said {quote}. The lesson has been updated since: Open shows where the note is now.";
            if (!_placed.TryGetValue(note.Id, out var at)) return $"On a paragraph of the {tab} page: {quote}";
            return at.Place switch
            {
                NotePlace.Found => $"On the {tab} paragraph: {quote}",
                NotePlace.Changed => $"On the {tab} paragraph that said {quote}. The lesson has been updated since; this is the paragraph most like it.",
                _ => $"The paragraph this note was on is no longer on the {tab} page (the lesson has been updated). It said: {quote}." + MoveIt,
            };
        }
        return note.Anchor.Tab is not null ? $"On the {tab} tab" : "On the item as a whole";
    }

    private static string TabName(string? tab) => tab switch
    {
        NoteTabs.Learn => "Learn",
        NoteTabs.Example => "Example",
        NoteTabs.Exercise => "Exercise",
        NoteTabs.Reference => "Reference",
        _ => "item",
    };

    public static string Short(string text, int length) => text.Length <= length ? text : text[..length].TrimEnd() + "…";

    /// <summary>Edit: the card becomes a box with Save and Cancel. Another note's open edit is kept first.</summary>
    private void StartEdit(LearnerNote note)
    {
        var kept = _editId is not null && _editId != note.Id ? CommitEdit() : null;
        (_editId, _editDraft, _editBox) = (note.Id, null, null);
        if (kept is not null) { Changed(); Status(kept); }
        else Refresh();
        if (_editBox is { } box)
        {
            box.Focus();
            box.CaretIndex = box.Text.Length;
        }
    }

    private void EditParts(LearnerNote note, StackPanel panel)
    {
        var box = new TextBox
        {
            Text = _editDraft ?? note.Text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 60, MaxHeight = 200,
            MaxLength = NoteBook.MaxLength, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(4, 3, 4, 3),
            ToolTip = "Change the words, then press Save (Ctrl+Enter), or Cancel (Esc) to leave the note as it was.",
        };
        AutomationProperties.SetName(box, "The note's words");
        _editBox = box;
        var save = SmallButton("Save", "Keeps the changed words (Ctrl+Enter). An empty note is deleted, after asking.");
        var cancel = SmallButton("Cancel", "Leaves the note as it was (Esc)");
        var row = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        row.Children.Add(save);
        row.Children.Add(cancel);
        panel.Children.Add(box);
        panel.Children.Add(row);

        void Save()
        {
            if (Session() is not { } s) return;
            if (box.Text.Trim().Length == 0)
            {
                // Emptying a note deletes it, so it asks first, as Delete does.
                if (Delete(note)) (_editId, _editDraft, _editBox) = (null, null, null);
                return;
            }
            (_editId, _editDraft, _editBox) = (null, null, null);
            s.Data.Notes.Edit(note.Id, box.Text, DateTimeOffset.Now);
            Changed();
            Highlight(note.Id);   // the card stays marked where the learner can see it
            Status(_cards.ContainsKey(note.Id) ? "Note changed."
                : "Note changed. It no longer has the words you searched for, so it is not listed: clear Find in my notes to see it.");
        }
        void Cancel()
        {
            (_editId, _editDraft, _editBox) = (null, null, null);
            Refresh();
            Status("The note is as it was.");
        }
        save.Click += (_, _) => Save();
        cancel.Click += (_, _) => Cancel();
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; Save(); }
            else if (e.Key == Key.Escape) { e.Handled = true; Cancel(); }
        };
    }

    /// <summary>Deletes a note after asking. True if it was deleted.</summary>
    private bool Delete(LearnerNote note)
    {
        if (Session() is not { } s) return false;
        if (!ChoiceWindow.Ask(Window.GetWindow(this), "PLC+ — Delete a note", "Delete this note?",
                $"“{Short(note.Text, 200)}”{Environment.NewLine}{Environment.NewLine}A deleted note cannot be brought back.",
                "Delete", "Deletes the note from your progress file", "Keep it", "Leaves the note as it is"))
            return false;
        if (_editId == note.Id) (_editId, _editDraft, _editBox) = (null, null, null);
        s.Data.Notes.Delete(note.Id, DateTimeOffset.Now);
        Changed();
        Status("Note deleted.");
        return true;
    }

    /// <summary>The notes changed (added, edited, deleted, or the learner's file was combined with a copy): draw them again.</summary>
    public void Refresh()
    {
        if (_built) Redraw();
    }
}
