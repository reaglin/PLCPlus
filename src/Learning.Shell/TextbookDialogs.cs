using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Learning.Core.Learners;
using Learning.Core.Notes;

namespace Learning.Shell;

/// <summary>
/// A note on one paragraph (or the page), written without leaving the lesson (Learning Core plan 3.2; Ron, 2026-11-15: the
/// notes moved to their own tab, so the right-click note is a small window over the lesson). Shows the words it goes on.
/// </summary>
internal sealed class NoteWindow : FormWindow
{
    private readonly TextBox _box;

    public string Text => _box.Text;

    private NoteWindow(Window owner, string heading, string lead) : base(owner, "PLC+ — My note", heading, lead, 560)
    {
        _box = AddText("_Your note:", "Only you see it: it is kept in your progress file, locked with your PIN, and never put in a report. Ctrl+Enter adds it.");
        _box.AcceptsReturn = true;
        _box.TextWrapping = TextWrapping.Wrap;
        _box.MinHeight = 96;
        _box.MaxHeight = 260;
        _box.MaxLength = NoteBook.MaxLength;
        _box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; Done(); }
        };
        AddButton("_Add note", "Keeps the note; a ✎ appears after the paragraph (Ctrl+Enter)", (_, _) => Done(), isDefault: false);
        AddButton("Cancel", "Closes this; nothing is kept (Esc)", (_, _) => DialogResult = false, isCancel: true);
        Loaded += (_, _) => { _box.Focus(); Keyboard.Focus(_box); };
        // Esc, Cancel or the window's X with words typed asks first: no surprise loss of work.
        Closing += (_, e) =>
        {
            if (DialogResult == true || _box.Text.Trim().Length == 0) return;
            if (!ChoiceWindow.Ask(this, "PLC+ — My note", "Throw away this note?", "You have typed a note that is not kept yet.",
                    "Throw it away", "Closes this; the note is not kept", "Keep writing", "Back to the note"))
                e.Cancel = true;
        };
    }

    private void Done()
    {
        if (_box.Text.Trim().Length == 0)
        {
            Say("The note is empty: type something first, or press Cancel.");
            _box.Focus();
            return;
        }
        DialogResult = true;
    }

    /// <summary>Asks for a note on <paramref name="anchor"/>; null if the learner cancels.</summary>
    public static string? Ask(Window owner, NoteAnchor anchor, string where)
    {
        var heading = anchor.Quote is null ? $"A note on {where}" : "A note on this paragraph";
        var lead = anchor.Quote is { } quote ? $"“{NotesPanel.Short(quote, 220)}” ({where})" : "The note goes on the page as a whole.";
        var w = new NoteWindow(owner, heading, lead);
        return w.ShowDialog() == true ? w.Text : null;
    }
}

/// <summary>
/// My course's links (Learning Core plan 3.10; Ron, 2026-11-15): the course's text (the website made with CourseBuilder+) and
/// the course in the learning management system. The learner types them; later the instructor's course pack provides them.
/// </summary>
internal sealed class CourseLinksWindow : FormWindow
{
    private readonly TextBox _name;
    private readonly TextBox _text;
    private readonly TextBox _lms;
    private CourseLinks? _result;

    private CourseLinksWindow(Window owner, CourseLinks? now)
        : base(owner, "PLC+ — My course's links", "My course's links",
            "If your class uses PLC+, your instructor can give you two web addresses: the course website and the course " +
            "in your learning management system (Canvas, Brightspace, Blackboard, Moodle …). Paste them here and PLC+ keeps them with your " +
            "progress, with buttons at the top of the learning path that open them in your web browser. Leave a box empty if you have no such address.",
            600)
    {
        _name = AddText("Course _name (optional):", "How you know the course, such as “ETI 1420 PLCs, spring 2027”. It is shown on the buttons' tips.");
        _text = AddText("The course _website:", "The address of the course website (the course's text), often made with CourseBuilder+. Copy it from your browser's address bar.");
        _lms = AddText("The course in the _LMS:", "The address of your course's page in Canvas, Brightspace, Blackboard or Moodle. Copy it from your browser's address bar.");
        (_name.Text, _text.Text, _lms.Text) = (now?.Name ?? "", now?.TextUrl ?? "", now?.LmsUrl ?? "");
        AddButton("_Save", "Keeps these links with your progress", (_, _) => Save(), isDefault: true);
        AddButton("Cancel", "Closes this; nothing changes (Esc)", (_, _) => DialogResult = false, isCancel: true);
        Loaded += (_, _) => _text.Focus();
    }

    private void Save()
    {
        var (text, textProblem) = CourseLinks.Check(_text.Text, "course website's address");
        var (lms, lmsProblem) = CourseLinks.Check(_lms.Text, "LMS address");
        if ((textProblem ?? lmsProblem) is { } problem)
        {
            Say(problem);
            (textProblem is null ? _lms : _text).Focus();
            return;
        }
        _result = new CourseLinks
        {
            Name = _name.Text.Trim().Length == 0 ? null : _name.Text.Trim(), TextUrl = text, LmsUrl = lms, Modified = DateTimeOffset.Now,
        };
        DialogResult = true;
    }

    /// <summary>The links as the learner left them (empty ones are allowed: that clears them); null if they cancelled.</summary>
    public static CourseLinks? Edit(Window owner, CourseLinks? now)
    {
        var w = new CourseLinksWindow(owner, now);
        return w.ShowDialog() == true ? w._result : null;
    }
}
