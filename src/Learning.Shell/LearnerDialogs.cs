using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Learning.Core.Learners;

namespace Learning.Shell;

/// <summary>
/// The shape every learner dialog shares: a heading, what the dialog is for, labelled boxes each with a line of help, a line
/// that says what is wrong in words, and buttons named for what they do (checklist).
/// </summary>
internal class FormWindow : Window
{
    protected readonly StackPanel Body = new();
    protected readonly TextBlock Problem = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 10), Visibility = Visibility.Collapsed };
    protected readonly StackPanel Buttons = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

    protected FormWindow(Window? owner, string title, string heading, string lead, double width = 520)
    {
        Title = title;
        Owner = owner;
        Width = width;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = owner is null;
        WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 14;
        SetResourceReference(BackgroundProperty, "PageBrush");
        SetResourceReference(ForegroundProperty, "InkBrush");
        Problem.Foreground = new SolidColorBrush(Color.FromRgb(0xA4, 0x1E, 0x1E));
        AutomationProperties.SetLiveSetting(Problem, AutomationLiveSetting.Assertive);

        var head = new TextBlock { Text = heading, TextWrapping = TextWrapping.Wrap };
        head.SetResourceReference(StyleProperty, "SectionTitle");
        AutomationProperties.SetHeadingLevel(head, AutomationHeadingLevel.Level1);
        Body.Children.Add(head);
        Body.Children.Add(Lead(lead));
        var outer = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
        outer.Children.Add(Body);
        outer.Children.Add(Problem);
        outer.Children.Add(Buttons);
        Content = outer;
    }

    protected static TextBlock Lead(string text, bool ink = true)
    {
        var t = new TextBlock { Text = text };
        t.SetResourceReference(StyleProperty, "Lead");
        if (ink) t.SetResourceReference(ForegroundProperty, "InkBrush");
        return t;
    }

    private T Field<T>(string label, string help, T box) where T : Control
    {
        var l = new Label { Content = label, Target = box, Padding = new Thickness(0, 6, 0, 2), FontWeight = FontWeights.SemiBold };
        AutomationProperties.SetName(box, label.Replace("_", "", StringComparison.Ordinal).TrimEnd(':'));
        AutomationProperties.SetHelpText(box, help);
        box.ToolTip = help;
        box.Padding = new Thickness(4, 3, 4, 3);
        Body.Children.Add(l);
        Body.Children.Add(box);
        var h = new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap, FontSize = 12.5, Margin = new Thickness(0, 2, 0, 4) };
        h.SetResourceReference(ForegroundProperty, "MutedTextBrush");
        Body.Children.Add(h);
        return box;
    }

    protected TextBox AddText(string label, string help) => Field(label, help, new TextBox());

    protected PasswordBox AddPin(string label, string help) => Field(label, help, new PasswordBox { MaxLength = 6, Width = 140, HorizontalAlignment = HorizontalAlignment.Left });

    protected Button AddButton(string text, string tip, RoutedEventHandler click, bool isDefault = false, bool isCancel = false)
    {
        var b = new Button { Content = text, ToolTip = tip, IsDefault = isDefault, IsCancel = isCancel, MinWidth = 90 };
        b.Click += click;
        Buttons.Children.Add(b);
        return b;
    }

    protected void Say(string? problem)
    {
        Problem.Text = problem ?? "";
        Problem.Visibility = string.IsNullOrEmpty(problem) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Runs slow work (a PIN takes a moment to check) with the buttons off and the pointer busy.</summary>
    protected async Task<T> Busy<T>(Func<T> work)
    {
        IsEnabled = false;
        Cursor = System.Windows.Input.Cursors.Wait;
        try
        {
            return await Task.Run(work);
        }
        finally
        {
            IsEnabled = true;
            Cursor = null;
        }
    }
}

/// <summary>A question with two answers named for what they do, such as "Back up now…" and "Not now".</summary>
internal sealed class ChoiceWindow : FormWindow
{
    private bool? _answer;

    private ChoiceWindow(Window? owner, string title, string heading, string text, string yes, string yesTip, string no, string noTip, bool withCancel)
        : base(owner, title, heading, text, 560)
    {
        AddButton(yes, yesTip, (_, _) => { _answer = true; DialogResult = true; }, isDefault: true);
        AddButton(no, noTip, (_, _) => { _answer = false; DialogResult = false; }, isCancel: !withCancel);
        if (withCancel) AddButton("Cancel", "Closes this without doing either.", (_, _) => DialogResult = false, isCancel: true);
    }

    /// <summary>Yes or no; closing the window counts as no.</summary>
    public static bool Ask(Window? owner, string title, string heading, string text, string yes, string yesTip, string no, string noTip)
    {
        var w = new ChoiceWindow(owner, title, heading, text, yes, yesTip, no, noTip, withCancel: false);
        w.ShowDialog();
        return w._answer == true;
    }

    /// <summary>Two things to do, and Cancel: null when the learner cancels or closes the window.</summary>
    public static bool? Choose(Window? owner, string title, string heading, string text, string yes, string yesTip, string no, string noTip)
    {
        var w = new ChoiceWindow(owner, title, heading, text, yes, yesTip, no, noTip, withCancel: true);
        w.ShowDialog();
        return w._answer;
    }
}

/// <summary>Asks for the learner's PIN before something that cannot be undone easily, such as removing them from this computer.</summary>
internal sealed class ConfirmPinWindow : FormWindow
{
    private readonly PasswordBox _pin;
    private readonly Func<string, string?> _check;

    public ConfirmPinWindow(Window? owner, string title, string heading, string lead, string ok, string okTip, Func<string, string?> check)
        : base(owner, title, heading, lead)
    {
        _check = check;
        _pin = AddPin("_PIN:", "The 4 to 6 digit PIN that opens this learner's progress.");
        AddButton(ok, okTip, async (_, _) =>
        {
            var pin = _pin.Password;
            var problem = await Busy(() => _check(pin));
            if (problem is null) DialogResult = true;
            else
            {
                Say(problem);
                _pin.Clear();
                _pin.Focus();
            }
        }, isDefault: true);
        AddButton("Cancel", "Closes this without doing anything.", (_, _) => DialogResult = false, isCancel: true);
        Loaded += (_, _) => _pin.Focus();
    }
}

/// <summary>A new PIN: with the recovery code when the PIN is forgotten, or with the PIN now when the learner wants a different one.</summary>
internal sealed class NewPinWindow : FormWindow
{
    private readonly Control _first;
    private readonly PasswordBox _pin, _again;

    /// <param name="apply">Given what was typed in the first box and the new PIN, does the change and returns a problem, or null.</param>
    public NewPinWindow(Window? owner, bool recovering, string name, Func<string, string, string?> apply)
        : base(owner, recovering ? "PLC+ — Forgot my PIN" : "PLC+ — Change my PIN",
            recovering ? $"A new PIN for {name}" : "Change my PIN",
            recovering
                ? "Type the recovery code you were given when you started, then choose a new PIN. The recovery code is the only way back in: PLC+ has no server, and nobody else can reset a PIN."
                : "Type the PIN you use now, then the new one twice. Your progress stays as it is.")
    {
        _first = recovering
            ? AddText("_Recovery code:", "20 letters and numbers in five groups, such as 7K2M-QX9P-4TRE-WH4C-8NDA. Capitals or not, with or without the dashes.")
            : AddPin("_PIN now:", "The PIN you type to open your progress today.");
        _pin = AddPin("_New PIN:", "4 to 6 digits. Choose one a classmate would not guess, and that you will remember.");
        _again = AddPin("New PIN _again:", "The same new PIN again, to be sure it was typed as you meant.");
        AddButton(recovering ? "Set the new PIN" : "Change the PIN", "Checks what you typed and sets the new PIN.", async (_, _) =>
        {
            var first = _first is TextBox t ? t.Text : ((PasswordBox)_first).Password;
            if (PinRules.Problem(_pin.Password, _again.Password) is { } p) { Say(p); _pin.Focus(); return; }
            var newPin = _pin.Password;
            var problem = await Busy(() => apply(first, newPin));
            if (problem is null) DialogResult = true;
            else { Say(problem); _first.Focus(); }
        }, isDefault: true);
        AddButton("Cancel", "Closes this; the PIN stays as it was.", (_, _) => DialogResult = false, isCancel: true);
        Loaded += (_, _) => _first.Focus();
    }
}

/// <summary>
/// A new learner (plan 6.2): name, optional student ID, PIN twice; then the one-time recovery code to write down or print.
/// </summary>
internal sealed class NewLearnerWindow : FormWindow
{
    private readonly LearnerStore _store;
    private readonly TextBox _name, _id;
    private readonly PasswordBox _pin, _again;
    private readonly CheckBox? _takePreview;
    private string? _code;
    private CheckBox? _kept;

    public LearnerFile? Created { get; private set; }

    public NewLearnerWindow(Window? owner, LearnerStore store)
        : base(owner, "PLC+ — New learner", "New learner",
            "Your progress is kept in a file of its own on this computer, locked with a PIN so classmates cannot open it. Only your name is needed.")
    {
        _store = store;
        _name = AddText("Your _name:", "As your instructor knows you: it is how you find your progress here, and it goes on your progress reports.");
        _id = AddText("_Student ID (optional):", "Only if your instructor asks for one. It is kept inside your locked file and printed on your reports.");
        _pin = AddPin("_PIN:", "4 to 6 digits that you will type each time you open PLC+. Choose one a classmate would not guess.");
        _again = AddPin("PIN _again:", "The same PIN again, to be sure it was typed as you meant.");
        if (PreviewProgress.CompleteItems() is { } done)
        {
            _takePreview = new CheckBox
            {
                Content = new TextBlock
                {
                    Text = $"Start with the progress from the earlier review copy of PLC+ on this computer ({done} item{(done == 1 ? "" : "s")} complete, and My program for each exercise)",
                    TextWrapping = TextWrapping.Wrap,
                },
                IsChecked = false, Margin = new Thickness(0, 8, 0, 4),
            };
            _takePreview.ToolTip = "The earlier review copy kept one person's progress on this computer. Tick this only if it is yours: it then belongs to this learner, and no one else can take it.";
            Body.Children.Add(_takePreview);
        }
        AddButton("Create", "Makes your progress file and shows your recovery code.", Create_Click, isDefault: true);
        AddButton("Cancel", "Closes this without making a learner.", (_, _) => DialogResult = false, isCancel: true);
        Loaded += (_, _) => _name.Focus();
        Closing += (_, e) =>
        {
            if (Created is null || DialogResult == true || _kept?.IsChecked == true) return;
            if (!ChoiceWindow.Ask(this, "PLC+ — Recovery code", "Have you kept your recovery code?",
                    "It will not be shown again. Without it, a forgotten PIN means starting again from the beginning.",
                    "Yes, I have kept it", "Closes this and opens your progress.", "Go back", "Shows the code again, to write down or print."))
                e.Cancel = true;
            else DialogResult = true;
        };
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        if (_store.NameProblem(_name.Text) is { } n) { Say(n); _name.Focus(); return; }
        if (PinRules.Problem(_pin.Password, _again.Password) is { } p) { Say(p); _pin.Focus(); return; }
        var (name, id, pin) = (_name.Text, _id.Text, _pin.Password);
        var take = _takePreview?.IsChecked == true;
        try
        {
            LearnerPlaces.EnsureShared(_store.Folder);
            var now = DateTimeOffset.Now;
            var start = take ? PreviewProgress.Take(now) : null;
            var (file, code) = await Busy(() => _store.Create(name, id, pin, now, start));
            if (take) PreviewProgress.MarkTaken();
            Created = file;
            _code = code;
            ShowCode();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Say($"PLC+ could not make your progress file in {_store.Folder}. An instructor can choose another folder in Settings on the start screen. Details for support: {ex.Message}");
        }
    }

    /// <summary>The second page: the recovery code, once.</summary>
    private void ShowCode()
    {
        Say(null);
        Title = "PLC+ — Your recovery code";
        Body.Children.Clear();
        Buttons.Children.Clear();
        var head = new TextBlock { Text = "Your recovery code", TextWrapping = TextWrapping.Wrap };
        head.SetResourceReference(StyleProperty, "SectionTitle");
        AutomationProperties.SetHeadingLevel(head, AutomationHeadingLevel.Level1);
        Body.Children.Add(head);
        Body.Children.Add(Lead("If you forget your PIN, this code is the only way back into your progress: PLC+ has no server, and nobody else can reset it. " +
                               "Write it down or print it now, and keep it somewhere safe, away from this computer. It is shown only this once."));
        var code = new TextBox
        {
            Text = _code, IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 24, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 4, 0, 12), BorderThickness = new Thickness(1),
        };
        AutomationProperties.SetName(code, "Recovery code");
        Body.Children.Add(code);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var print = new Button { Content = "_Print…", ToolTip = "Prints the code with your name, to keep somewhere safe." };
        print.Click += (_, _) => Print();
        var copy = new Button { Content = "_Copy", ToolTip = "Copies the code, to paste into a password manager or a note you keep." };
        copy.ToolTip = "Copies the code, to paste straight away into a password manager or a note you keep. PLC+ clears it from the clipboard when this window closes.";
        copy.Click += (_, _) => { Clipboard.SetText(_code!); Say(null); copy.Content = "Copied ✓"; };
        Closed += (_, _) =>
        {
            try { if (Clipboard.ContainsText() && Clipboard.GetText() == _code) Clipboard.Clear(); }
            catch (System.Runtime.InteropServices.COMException) { }
        };
        row.Children.Add(print);
        row.Children.Add(copy);
        Body.Children.Add(row);
        _kept = new CheckBox { Content = "I have written down or printed my recovery code", Margin = new Thickness(0, 4, 0, 6) };
        Body.Children.Add(_kept);
        var start = AddButton("Start learning", "Opens your progress. Available once you have ticked the box above.", (_, _) => DialogResult = true, isDefault: true);
        start.IsEnabled = false;
        ToolTipService.SetShowOnDisabled(start, true);
        _kept.Checked += (_, _) => start.IsEnabled = true;
        _kept.Unchecked += (_, _) => start.IsEnabled = false;
        code.Focus();
    }

    private void Print()
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;
        var doc = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 14, PagePadding = new Thickness(72), ColumnWidth = 9999 };
        doc.Blocks.Add(new Paragraph(new Run("PLC+ recovery code")) { FontSize = 22, FontWeight = FontWeights.SemiBold });
        doc.Blocks.Add(new Paragraph(new Run($"Learner: {Created!.Data.Profile.DisplayName}\nMade: {DateTime.Now.ToString("d MMMM yyyy", CultureInfo.CurrentCulture)}")));
        doc.Blocks.Add(new Paragraph(new Run(_code)) { FontFamily = new FontFamily("Consolas"), FontSize = 28, FontWeight = FontWeights.SemiBold });
        doc.Blocks.Add(new Paragraph(new Run("If you forget your PIN: on PLC+'s start screen, choose your name and press Forgot my PIN, then type this code and a new PIN. " +
                                             "Keep this page somewhere safe and away from the computer: with it, anyone can open your progress.")));
        dialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "PLC+ recovery code");
    }
}

/// <summary>Settings on the start screen: where learners' files are kept, and when PLC+ locks itself (plan 6.4, 6.8).</summary>
internal sealed class SettingsWindow : FormWindow
{
    private readonly ShellSettings _settings;
    private readonly TextBox _folder;
    private readonly ComboBox _lock;

    public SettingsWindow(Window? owner, ShellSettings settings)
        : base(owner, "PLC+ — Settings", "Settings",
            "These are for everyone who uses PLC+ on this computer, so on a lab PC an instructor usually sets them once.", 600)
    {
        _settings = settings;
        _folder = AddText("Where learners' files are _kept:",
            "Usually " + LearnerPlaces.DefaultLearnersFolder + ", which every Windows account on this computer can use. " +
            "Changing it does not move anyone's files: bring them with Add learner from file.");
        _folder.Text = settings.Folder;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 6) };
        var choose = new Button { Content = "C_hoose a folder…", ToolTip = "Choose another folder for learners' files, such as a shared drive on a lab network." };
        choose.Click += (_, _) =>
        {
            var d = new Microsoft.Win32.OpenFolderDialog { Title = "Where should PLC+ keep learners' files?", InitialDirectory = Directory.Exists(_folder.Text) ? _folder.Text : null };
            if (d.ShowDialog(this) == true) _folder.Text = d.FolderName;
        };
        var usual = new Button { Content = "Use the _usual folder", ToolTip = LearnerPlaces.DefaultLearnersFolder };
        usual.Click += (_, _) => _folder.Text = LearnerPlaces.DefaultLearnersFolder;
        row.Children.Add(choose);
        row.Children.Add(usual);
        Body.Children.Add(row);

        _lock = new ComboBox
        {
            Width = 160, HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip = "After this long with no key or mouse use, PLC+ saves and locks the learner's progress; their PIN opens it again.",
        };
        foreach (var m in new[] { 5, 10, 15, 30, 60 }) _lock.Items.Add(new ComboBoxItem { Content = $"{m} minutes", Tag = m });
        _lock.SelectedItem = _lock.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (int)i.Tag == settings.LockMinutes) ?? _lock.Items[2];
        var l = new Label { Content = "_Lock PLC+ after this long without use:", Target = _lock, Padding = new Thickness(0, 6, 0, 2), FontWeight = FontWeights.SemiBold };
        AutomationProperties.SetName(_lock, "Lock PLC+ after this long without use");
        Body.Children.Add(l);
        Body.Children.Add(_lock);
        Body.Children.Add(Lead("Your work is saved first; the learner types their PIN to carry on. 15 minutes unless you change it.", ink: false));

        AddButton("Save", "Keeps these settings for everyone on this computer.", (_, _) => Save(), isDefault: true);
        AddButton("Cancel", "Closes this without changing anything.", (_, _) => DialogResult = false, isCancel: true);
    }

    private void Save()
    {
        var folder = _folder.Text.Trim();
        if (folder.Length == 0 || !Path.IsPathFullyQualified(folder)) { Say("Type a full folder path, such as D:\\PLCPlus\\Learners, or press Choose a folder."); return; }
        if (!string.Equals(Path.GetFullPath(folder).TrimEnd('\\'), Path.GetFullPath(_settings.Folder).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            var here = Directory.Exists(_settings.Folder) ? Directory.GetFiles(_settings.Folder, "*" + LearnerPlaces.Extension).Length : 0;
            if (here > 0 && !ChoiceWindow.Ask(this, "PLC+ — Settings", "Change where learners' files are kept?",
                    $"{here} learner{(here == 1 ? " is" : "s are")} in {_settings.Folder}. They will not be in the list until you bring them with Add learner from file, " +
                    "or change back to this folder. Their files are not moved or deleted.",
                    "Change the folder", "Uses the new folder from now on.", "Keep the folder as it is", "Goes back to Settings."))
                return;
        }
        try
        {
            LearnerPlaces.EnsureShared(folder);
            _settings.LearnersFolder = string.Equals(Path.GetFullPath(folder), Path.GetFullPath(LearnerPlaces.DefaultLearnersFolder), StringComparison.OrdinalIgnoreCase) ? null : folder;
            _settings.LockMinutes = (int)((ComboBoxItem)_lock.SelectedItem).Tag;
            _settings.Save();
            DialogResult = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Say($"PLC+ could not use that folder or save the settings. Choose a folder you can write to. Details for support: {ex.Message}");
        }
    }
}
