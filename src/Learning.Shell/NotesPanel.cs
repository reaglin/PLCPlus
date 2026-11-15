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
/// My notes (Learning Core plan 3.2, 3.8): the learner's own notes, kept in their progress file. One control, two places:
/// the My notes tab of an item (Ron, 2026-11-15: a tab, so the lesson keeps its width) with every note on the item, a box
/// to write a new one, and Ask the AI; and the My notes page in the path (every note, with search, each opening its item).
/// A note on one paragraph is written from the lesson's right-click menu (<see cref="NoteWindow"/>).
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

    /// <summary>Ask the AI (plan 3.8): what it would send and cost, or what it needs; asking; and the AI's settings.</summary>
    public Func<bool> AiReady { get; set; } = () => false;
    public Func<string> AiStatus { get; set; } = () => "";
    public Func<LearningItem, string, CancellationToken, Task<AiReply>>? AskAi { get; set; }
    public Action AiSettings { get; set; } = () => { };

    private readonly StackPanel _list = new();
    private readonly TextBlock _heading = new();
    private readonly TextBlock _listHeading = new();
    private readonly TextBox _box = new();
    private readonly TextBlock _onText = new();
    private readonly TextBox _search = new();
    private readonly TextBlock _found = new();

    // Ask the AI.
    private readonly TextBox _question = new();
    private readonly TextBlock _aiLead = new();
    private readonly Button _ask = new();
    private readonly Button _aiSettings = new();
    private readonly StackPanel _answerPanel = new();
    private CancellationTokenSource? _asking;
    /// <summary>
    /// Answers not yet kept or discarded, by learner and item: moving to another item, or the idle lock, does not throw away
    /// an answer that was paid for. Only in memory, and only the learner who asked sees theirs.
    /// </summary>
    private readonly Dictionary<(string Learner, string Item), (string Question, AiReply Reply)> _answers = [];

    private LearningItem? _item;
    private IReadOnlyDictionary<string, NoteLocation> _placed = new Dictionary<string, NoteLocation>();

    /// <summary>
    /// The note being edited, what is typed in it, and its box. The edit survives the list being drawn again (a search, a new
    /// note, the marks), and leaving the item or locking keeps what was typed (UX review 2026-11-15).
    /// </summary>
    private string? _editId;
    private string? _editDraft;
    private TextBox? _editBox;

    /// <summary>The page in the path asks the window for an item's page (Learn, Laboratory or Reference), to say whether a note's paragraph is still there.</summary>
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
            : "Your own notes on this item, and the AI answers you kept. Only you see them: they are kept in your progress file, " +
              "locked with your PIN, and never put in a report. A ✎ after a paragraph of the lesson shows a note on it."));

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
            top.Children.Add(TwoColumns(ComposePart(), AiPart()));
            _listHeading.Style = (Style)FindResource("PanelTitle");
            _listHeading.Margin = new Thickness(0, 14, 0, 6);
            AutomationProperties.SetHeadingLevel(_listHeading, AutomationHeadingLevel.Level2);
            top.Children.Add(_listHeading);
        }

        AutomationProperties.SetName(_list, AllNotes ? "My notes" : "My notes on this item");
        if (AllNotes)
        {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _list, Focusable = false };
            var dock = new DockPanel();
            DockPanel.SetDock(top, Dock.Top);
            dock.Children.Add(top);
            dock.Children.Add(scroll);
            Content = dock;
            ShowAll();
        }
        else
        {
            // On the tab, the boxes and the list scroll together, so a long answer never hides the notes.
            top.Children.Add(_list);
            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = top, Padding = new Thickness(10, 8, 10, 8), Focusable = false,
            };
            Redraw();
        }
    }

    /// <summary>Two parts side by side when there is room, one above the other when the window is narrow.</summary>
    private static Grid TwoColumns(FrameworkElement left, FrameworkElement right)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(left);
        grid.Children.Add(right);
        void Lay(double width)
        {
            var wide = width >= 640;
            Grid.SetColumn(right, wide ? 1 : 0);
            Grid.SetRow(right, wide ? 0 : 1);
            Grid.SetColumnSpan(left, wide ? 1 : 2);
            Grid.SetColumnSpan(right, wide ? 1 : 2);
            left.Margin = new Thickness(0, 0, wide ? 8 : 0, wide ? 0 : 12);
            right.Margin = new Thickness(wide ? 8 : 0, 0, 0, 0);
        }
        grid.SizeChanged += (_, e) => { if (e.WidthChanged) Lay(e.NewSize.Width); };
        Lay(1000);
        return grid;
    }

    private Border Part(string title, string tip, UIElement body)
    {
        var heading = new TextBlock { Text = title, Style = (Style)FindResource("PanelTitle"), TextWrapping = TextWrapping.Wrap, ToolTip = tip };
        AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level2);
        var panel = new StackPanel();
        panel.Children.Add(heading);
        panel.Children.Add(body);
        return new Border
        {
            Child = panel, Padding = new Thickness(12, 8, 12, 10),
            Background = (Brush)FindResource("PanelBrush"), BorderBrush = (Brush)FindResource("LineBrush"), BorderThickness = new Thickness(1),
        };
    }

    private Border ComposePart()
    {
        var compose = new StackPanel();
        _onText.TextWrapping = TextWrapping.Wrap;
        _onText.Margin = new Thickness(0, 0, 0, 4);
        _onText.FontSize = 12.5;
        _onText.Foreground = (Brush)FindResource("MutedTextBrush");
        compose.Children.Add(_onText);
        _box.AcceptsReturn = true;
        _box.TextWrapping = TextWrapping.Wrap;
        _box.MinHeight = 72;
        _box.MaxHeight = 200;
        _box.MaxLength = NoteBook.MaxLength;
        _box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _box.Padding = new Thickness(4, 3, 4, 3);
        _box.ToolTip = "Type your note: a question for class, a reminder, the idea in your own words. Ctrl+Enter adds it.";
        AutomationProperties.SetName(_box, "Write a note on this item");
        AutomationProperties.SetHelpText(_box, "Type your note, then press Add note or Ctrl+Enter");
        _box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; Add(); }
        };
        compose.Children.Add(_box);
        var add = new Button { Content = "A_dd note", ToolTip = "Keeps the note in your progress file (Ctrl+Enter)", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0) };
        add.Click += (_, _) => Add();
        compose.Children.Add(add);
        return Part("Write a note", "A note on this item as a whole. For one paragraph, right-click it in the lesson.", compose);
    }

    private Border AiPart()
    {
        var ai = new StackPanel();
        _aiLead.Style = (Style)FindResource("Lead");
        _aiLead.Margin = new Thickness(0, 0, 0, 4);
        _aiLead.FontSize = 12.5;
        ai.Children.Add(_aiLead);
        _question.TextWrapping = TextWrapping.Wrap;
        _question.MinHeight = 48;
        _question.MaxLength = 600;
        _question.Padding = new Thickness(4, 3, 4, 3);
        _question.ToolTip = "Type a question about this item, such as \u201cWhy is the Stop button wired normally closed?\u201d, then press Ask the AI (Enter)";
        AutomationProperties.SetName(_question, "Your question for the AI");
        _question.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) { e.Handled = true; _ = Ask(); }
        };
        ai.Children.Add(_question);
        var row = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        _ask.Margin = new Thickness(0, 0, 8, 0);
        _ask.Click += (_, _) => { if (_asking is null) _ = Ask(); else _asking.Cancel(); };
        ToolTipService.SetShowOnDisabled(_ask, true);
        _aiSettings.Content = "AI settings…";
        _aiSettings.ToolTip = "Set up the AI: choose the AI company and paste its key (kept on this computer only)";
        _aiSettings.Click += (_, _) => AiSettings();
        row.Children.Add(_ask);
        row.Children.Add(_aiSettings);
        ai.Children.Add(row);
        _answerPanel.Margin = new Thickness(0, 8, 0, 0);
        AutomationProperties.SetLiveSetting(_answerPanel, AutomationLiveSetting.Polite);
        ai.Children.Add(_answerPanel);
        return Part("Ask the AI about this topic (optional)", "Ask a question; the AI answers from this item's lesson. Keep the answer in My notes, or discard it.", ai);
    }

    /// <summary>Puts the cursor in the question box (AI ▸ Ask the AI about this item).</summary>
    public void FocusQuestion()
    {
        if (!_built) Build();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () => { _question.BringIntoView(); _question.Focus(); Keyboard.Focus(_question); });
    }

    private string LearnerKey => Session()?.Data.Profile.ProfileId ?? "";

    /// <summary>The item being asked about while the AI answers (null when not asking).</summary>
    private LearningItem? _askingItem;
    /// <summary>Why the last question on an item got no answer; shown until the next question or AI settings. Never replaces an answer.</summary>
    private readonly Dictionary<(string Learner, string Item), string> _failures = [];
    /// <summary>A question typed on an item and not yet asked stays with that item (UX review 2026-11-15).</summary>
    private readonly Dictionary<(string Learner, string Item), string> _questions = [];

    /// <summary>The AI's settings closed: an old "no key" message no longer applies.</summary>
    public void AiSettingsChanged()
    {
        _failures.Clear();
        if (_built && !AllNotes) ShowAi();
    }

    /// <summary>Draws the Ask the AI part: ready or not, asking or not, and an answer waiting to be kept or discarded.</summary>
    private void ShowAi()
    {
        var ready = AiReady();
        _aiLead.Text = AiStatus();
        _answerPanel.Children.Clear();
        var item = _item;
        var key = (LearnerKey, item?.Id ?? "");
        var waiting = item is not null && _answers.TryGetValue(key, out var w) ? w : ((string, AiReply)?)null;
        var askingHere = _asking is not null && _askingItem == item;
        _question.IsEnabled = ready && _asking is null && waiting is null;
        _ask.IsEnabled = ready && (askingHere || (_asking is null && waiting is null));
        _ask.Content = askingHere ? "_Stop" : "As_k the AI";
        _ask.ToolTip = !ready ? "Set up the AI first: AI settings… (the button beside this one)"
            : askingHere ? "Stops waiting for the answer; nothing is kept and your question goes back in the box"
            : _asking is not null ? $"The AI is still answering your question on {_askingItem?.Number}; ask again when it has answered"
            : waiting is not null ? "Save or discard the answer below first"
            : "Sends your question and this item's lesson to the AI (Enter)";
        if (askingHere)
        {
            _answerPanel.Children.Add(Lead("Asking the AI… The answer usually takes a few seconds."));
            return;
        }
        if (_asking is not null)
            _answerPanel.Children.Add(Lead($"The AI is still answering your question on {_askingItem?.Number}. Its answer will wait on {_askingItem?.Number}'s My notes tab."));
        if (item is not null && _failures.TryGetValue(key, out var problemText))
        {
            var problem = Lead(problemText);
            problem.Foreground = new SolidColorBrush(Color.FromRgb(0xA4, 0x1E, 0x1E));
            _answerPanel.Children.Add(problem);
        }
        if (item is null || waiting is not { } shown) return;
        var (question, reply) = shown;
        var answer = new StackPanel();
        answer.Children.Add(new TextBlock { Text = $"You asked: \u201c{question}\u201d", FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });
        var text = new TextBox
        {
            Text = reply.Text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
        };
        text.SetResourceReference(ForegroundProperty, "InkBrush");
        AutomationProperties.SetName(text, "The AI's answer");
        answer.Children.Add(text);
        answer.Children.Add(new TextBlock
        {
            Text = $"Answer by {reply.Source}. These are the AI's words, not the book's: check them against the lesson.",
            FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("MutedTextBrush"), Margin = new Thickness(0, 4, 0, 0),
        });
        var buttons = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        var keep = SmallButton("Save to my notes", "Keeps the question and the answer in My notes, marked as the AI's answer");
        var discard = SmallButton("Discard", "Throws the answer away; nothing is kept");
        keep.Click += (_, _) => KeepAnswer(question, reply);
        discard.Click += (_, _) =>
        {
            _answers.Remove(key);
            ShowAi();
            Status("The AI's answer was discarded. Ask another question when you like.");
        };
        buttons.Children.Add(keep);
        buttons.Children.Add(discard);
        answer.Children.Add(buttons);
        _answerPanel.Children.Add(new Border
        {
            Child = answer, Padding = new Thickness(10, 8, 10, 8), Background = (Brush)FindResource("PageBrush"),
            BorderBrush = (Brush)FindResource("LineBrush"), BorderThickness = new Thickness(1),
        });
    }

    private async Task Ask()
    {
        if (_item is not { } item || AskAi is null || _asking is not null) return;
        var key = (LearnerKey, item.Id);
        if (_answers.ContainsKey(key))
        {
            Status("Save or discard the AI's answer first: a new question would replace it.");
            return;
        }
        var question = _question.Text.Trim();
        if (question.Length == 0)
        {
            Status("Type your question in the box first, then press Ask the AI.");
            _question.Focus();
            return;
        }
        if (!AiReady())
        {
            ShowAi();
            Status("The AI is not set up on this computer: press AI settings… to set it up. Everything else works without it.");
            return;
        }
        _failures.Remove(key);
        using var asking = new CancellationTokenSource();
        (_asking, _askingItem) = (asking, item);
        ShowAi();
        Status($"Asking the AI about {item.Number}…");
        AiReply reply;
        try
        {
            reply = await AskAi(item, question, asking.Token);
        }
        catch (Exception ex)
        {
            reply = AiReply.Failed("Something went wrong asking the AI. Try again. Details for support: " + ex.Message);
        }
        finally
        {
            (_asking, _askingItem) = (null, null);
        }
        var here = _item == item;
        if (asking.IsCancellationRequested)
        {
            ShowAi();
            Status("Stopped: nothing was kept. Your question is still in the box; press Ask the AI to try again.");
            return;
        }
        if (reply.Ok)
        {
            _answers[key] = (question, reply);
            if (here) _question.Clear(); else _questions.Remove(key);
        }
        else _failures[key] = reply.Problem;
        ShowAi();
        Status(!reply.Ok ? $"The AI did not answer your question on {item.Number}: " + reply.Problem
            : here ? $"The AI answered your question on {item.Number}. Save to my notes keeps it; Discard throws it away."
            : $"The AI answered your question on {item.Number}: open {item.Number}'s My notes tab to save it or discard it.");
    }

    private void KeepAnswer(string question, AiReply reply)
    {
        if (_item is not { } item || Session() is not { } s) return;
        var note = s.Data.Notes.AddAiAnswer(NoteAnchor.OnItem(item.Id), question, reply.Text, reply.Source, DateTimeOffset.Now);
        _answers.Remove((LearnerKey, item.Id));
        Changed();
        ShowAi();
        if (note is not null) Highlight(note.Id);
        Status($"The AI's answer is kept in My notes on {item.Number}, marked as the AI's.");
    }

    private TextBlock Lead(string text) => new() { Text = text, Style = (Style)FindResource("Lead") };

    // ---- Beside an item ---------------------------------------------------------------------------------

    /// <summary>Shows an item's notes. <paramref name="placed"/> says where each note's paragraph is on the page now.</summary>
    public void ShowItem(LearningItem item, IReadOnlyDictionary<string, NoteLocation> placed)
    {
        if (_item != item)
        {
            KeepDraft();
            _highlight = null;
            // The question typed on the last item stays with it; this item's comes back.
            if (_item is not null && _asking is null) _questions[(LearnerKey, _item.Id)] = _question.Text;
            _question.Text = _questions.GetValueOrDefault((LearnerKey, item.Id)) ?? "";
        }
        _item = item;
        _placed = placed;
        if (_built) Redraw();
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
        var note = s.Data.Notes.Add(NoteAnchor.OnItem(_item.Id), _box.Text, DateTimeOffset.Now);
        _box.Clear();
        Changed();
        if (note is not null) Highlight(note.Id);
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
            session.Data.Notes.Add(NoteAnchor.OnItem(_item.Id), _box.Text, DateTimeOffset.Now);
            kept = (kept is null ? "" : kept + " ") + $"The note you were writing on {_item.Number} was added to My notes.";
            _box.Clear();   // only once it is kept: a box with nowhere to go keeps its words
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
        // A question typed and not asked waits for the same learner (kept by learner and item, only in memory).
        if (_item is not null && _question.Text.Trim().Length > 0) _questions[(LearnerKey, _item.Id)] = _question.Text;
        _question.Clear();
        _asking?.Cancel();
        _answerPanel.Children.Clear();
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
        _heading.Text = $"My notes on {_item.Number} {_item.Title}";
        _onText.Text = $"A note on {_item.Number} as a whole. To put one on a paragraph, right-click the paragraph in the lesson.";
        _listHeading.Text = notes.Count == 0 ? "Notes on this item" : $"Notes on this item ({notes.Count})";
        ShowAi();
        if (notes.Count == 0)
        {
            _list.Children.Add(Lead("No notes on this item yet. Write one above, or right-click a paragraph of the lesson and choose Add a note to this paragraph."));
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
            _list.Children.Add(Lead("You have no notes yet. On any item, open its My notes tab and write one, or right-click a paragraph of a lesson " +
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
            if (note.Kind == NoteKind.AiAnswer)
                panel.Children.Add(new TextBlock { Text = $"You asked the AI: \u201c{note.Question}\u201d", FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 3) });
            panel.Children.Add(new TextBlock { Text = note.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 3) });
            if (note.Kind == NoteKind.AiAnswer)
                panel.Children.Add(new TextBlock { Text = $"The AI's answer ({note.Source}), not the book's words: check it against the lesson.", FontSize = 12, FontStyle = FontStyles.Italic, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("MutedTextBrush") });
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
        AutomationProperties.SetName(card, note.Kind == NoteKind.AiAnswer ? $"The AI's answer to {note.Question}: {note.Text}. {where}" : $"Note: {note.Text}. {where}");
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
        return note.Kind == NoteKind.AiAnswer ? "Asked about this item" : note.Anchor.Tab is not null ? $"On the {tab} tab" : "On the item as a whole";
    }

    private static string TabName(string? tab) => tab switch
    {
        NoteTabs.Learn => "Learn",
        NoteTabs.Example => "Example",
        NoteTabs.Exercise => "Exercise",
        NoteTabs.Reference => "Reference",
        NoteTabs.Laboratory => "Laboratory",
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
