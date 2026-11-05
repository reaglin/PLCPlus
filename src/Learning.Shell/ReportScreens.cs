using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Learning.Core.Content;
using Learning.Core.Learners;
using Learning.Core.Progress;
using Learning.Core.Reports;
using Learning.Reports.Pdf;
using Microsoft.Win32;

namespace Learning.Shell;

/// <summary>What PLC+ says about itself on reports and result lines (plan 8.3, 8.4).</summary>
internal static class PlcReports
{
    public static ReportOptions Options => new("PLC+", LearnerPlaces.AppVersion, "PLC1", "PLCPlus");

    public static readonly ReportWords Words = new(
        "To check it, an instructor opens PLC+ and chooses File, then Check a report (for instructors), or Check a report on the start screen.",
        "Counted only while the learner was using PLC+ (a key or the mouse in the last two minutes), so it is a lower bound.");

    public static string Percent(double? value) => value is { } v ? $"{Math.Round(v, MidpointRounding.AwayFromZero).ToString(CultureInfo.CurrentCulture)}%" : "no score";
}

/// <summary>
/// This computer's signing key (spec 10: "a per-installation key pair that is created on first run"), kept in the program's data
/// folder for every learner on the computer, protected with Windows (DPAPI, this computer). Made the first time a report is made.
/// </summary>
internal static class ComputerKeyStore
{
    private static string File => Path.Combine(LearnerPlaces.DataFolder, "computer-key.dat");
    private static readonly byte[] Entropy = "PLCPlus computer key"u8.ToArray();

    public static ComputerKey Load()
    {
        if (System.IO.File.Exists(File))
        {
            try
            {
                return ComputerKey.FromPkcs8(ProtectedData.Unprotect(System.IO.File.ReadAllBytes(File), Entropy, DataProtectionScope.LocalMachine));
            }
            catch (CryptographicException)
            {
                // A key from another computer (the data folder was copied): this computer makes its own.
            }
        }
        var key = ComputerKey.New();
        Directory.CreateDirectory(LearnerPlaces.DataFolder);
        System.IO.File.WriteAllBytes(File, ProtectedData.Protect(key.ExportPkcs8(), Entropy, DataProtectionScope.LocalMachine));
        return key;
    }
}

/// <summary>The cover's details (plan 8.4a): only the name is required, and it is already in the learner's profile.</summary>
internal sealed class ReportDetailsWindow : FormWindow
{
    private readonly TextBox _id, _course, _section, _email, _instructor, _code;
    private readonly CheckBox _programs;
    private readonly ReportDetails _details;

    public ReportDetailsWindow(Window owner, string name, ReportDetails details, bool firstReport = false)
        : base(owner, "PLC+ — My details for reports", firstReport ? "Before your first report: your details" : "My details for reports",
            $"What goes on the cover of your reports, beside your name ({name}). Only your name is required: fill in what your instructor asks for, " +
            "or leave every box empty. A class code is 16 letters and numbers your instructor may give you, so they can tell your reports came from your class. " +
            "These details are kept in your own progress file, for every report you make." +
            (firstReport ? " Press Continue, then choose where to save the report." : ""), 560)
    {
        _details = details;
        _id = AddText("_Student ID (optional):", "Your college or school ID, if your instructor wants it on the report.");
        _course = AddText("_Course (optional):", "The course, such as \"ETD 1100 PLC Fundamentals\".");
        _section = AddText("S_ection (optional):", "Your section or class group, if there is more than one.");
        _email = AddText("Your _e-mail (optional):", "Printed on the cover, so the instructor can reply.");
        _instructor = AddText("Your _instructor's e-mail (optional):", "Send to my instructor starts an e-mail to this address. PLC+ never sends anything itself.");
        _code = AddText("C_lass code (optional, from your instructor):", "16 letters and numbers, such as 7K3M-9QX2-4F2A-BD30. With it, your reports show they came from your class. Leave it empty if your instructor did not give you one.");
        _programs = new CheckBox
        {
            Content = "Put _pictures of my programs in the report",
            ToolTip = "Each exercise you passed is shown as the ladder program you wrote, so your instructor can see how you solved it (spec: program evidence). Untick to leave them out.",
            Margin = new Thickness(0, 8, 0, 4),
        };
        Body.Children.Add(_programs);
        (_id.Text, _course.Text, _section.Text, _email.Text, _instructor.Text, _code.Text, _programs.IsChecked) =
            (details.StudentId ?? "", details.Course ?? "", details.Section ?? "", details.Email ?? "", details.InstructorEmail ?? "", details.ClassCode ?? "", details.IncludePrograms);
        AddButton(firstReport ? "_Continue" : "_Save my details", firstReport ? "Keeps these details (empty boxes are fine) and goes on to save the report" : "Keeps these details for your reports",
            Save_Click, isDefault: true);
        AddButton(firstReport ? "Cancel (no report)" : "Cancel", firstReport ? "Closes this; no report is made" : "Closes this without changing anything",
            (_, _) => DialogResult = false, isCancel: true);
        Loaded += (_, _) => _id.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var code = _code.Text.Trim();
        if (code.Length > 0 && ClassCode.Format(code) is null)
        {
            Say("That class code is not right: a class code has 16 letters and numbers, in four groups of four (dashes and spaces do not matter). Check it with your instructor, or leave the box empty.");
            _code.Focus();
            return;
        }
        foreach (var (box, what) in new[] { (_email, "your e-mail"), (_instructor, "your instructor's e-mail") })
            if (box.Text.Trim() is { Length: > 0 } mail && !mail.Contains('@', StringComparison.Ordinal))
            {
                Say($"That is not an e-mail address ({what}): it needs an @, such as name@college.edu. Correct it, or leave the box empty.");
                box.Focus();
                return;
            }
        _details.StudentId = Blank(_id.Text);
        _details.Course = Blank(_course.Text);
        _details.Section = Blank(_section.Text);
        _details.Email = Blank(_email.Text);
        _details.InstructorEmail = Blank(_instructor.Text);
        _details.ClassCode = code.Length > 0 ? ClassCode.Format(code) : null;
        _details.IncludePrograms = _programs.IsChecked == true;
        _details.Confirmed = true;
        DialogResult = true;
    }

    private static string? Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>
/// The report verifier (plan 8.8, FR-11) for instructors: checks a PDF report and a result line, and makes a class code. Works
/// on any computer with PLC+, offline; nothing is sent anywhere.
/// </summary>
internal sealed class VerifyWindow : FormWindow
{
    private readonly TextBox _file = new() { Padding = new Thickness(4, 3, 4, 3) };
    private readonly TextBox _line, _code;
    private readonly TextBox _result = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 6), IsReadOnly = true, BorderThickness = new Thickness(0),
        Background = Brushes.Transparent, Visibility = Visibility.Collapsed };
    private readonly TextBox _newCode = new() { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 16, Padding = new Thickness(4, 3, 4, 3),
        Margin = new Thickness(0, 4, 0, 2), Visibility = Visibility.Collapsed };
    private readonly TextBlock _newCodeText = new() { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, FontSize = 12.5 };

    public VerifyWindow(Window? owner)
        : base(owner, "PLC+ — Check a report (for instructors)", "Check a report (for instructors)",
            "Checks that a PLC+ progress report is genuine and was not changed after the learner saved it, and checks a result line. " +
            "Choose the PDF, paste the result line, or both. If your class uses a class code, type it too. Nothing is sent anywhere.", 720)
    {
        var fileLabel = new Label { Content = "The _report (a PDF file from PLC+):", Target = _file, Padding = new Thickness(0, 6, 0, 2), FontWeight = FontWeights.SemiBold };
        var browse = new Button { Content = "_Choose…", Margin = new Thickness(8, 0, 0, 0), ToolTip = "Choose the PDF the learner handed in" };
        browse.Click += Browse_Click;
        var row = new DockPanel();
        DockPanel.SetDock(browse, Dock.Right);
        row.Children.Add(browse);
        row.Children.Add(_file);
        AutomationProperties.SetName(_file, "The report (a PDF file from PLC+)");
        _file.ToolTip = "The file exactly as the learner handed it in: download it from the LMS without opening and saving it again";
        Body.Children.Add(fileLabel);
        Body.Children.Add(row);
        _line = AddText("Result _line (optional):", "The line that starts PLC1| , as the learner pasted it (the whole line).");
        _code = AddText("Your class c_ode (optional):", "The class code you gave your class. Reports and lines signed with it prove they came from your class.");
        var make = new Button { Content = "_Make a new class code", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 4),
            ToolTip = "Makes a new class code to give a class, and copies it. The code in the box above is not changed" };
        make.Click += MakeCode_Click;
        Body.Children.Add(make);
        AutomationProperties.SetName(_newCode, "Your new class code");
        Body.Children.Add(_newCode);
        Body.Children.Add(_newCodeText);
        AutomationProperties.SetName(_result, "What the check found");
        AutomationProperties.SetLiveSetting(_result, AutomationLiveSetting.Polite);
        Body.Children.Add(new ScrollViewer { Content = _result, MaxHeight = 320, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false });
        AddButton("C_heck", "Checks the report and the line", Check_Click, isDefault: true);
        AddButton("Close", "Closes this window", (_, _) => Close(), isCancel: true);
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Choose the report to check", Filter = "PDF report (*.pdf)|*.pdf" };
        if (dialog.ShowDialog(this) == true) _file.Text = dialog.FileName;
    }

    private void MakeCode_Click(object sender, RoutedEventArgs e)
    {
        var code = ClassCode.New();
        var copied = true;
        try { Clipboard.SetText(code); } catch (System.Runtime.InteropServices.ExternalException) { copied = false; }
        _newCode.Text = code;
        _newCode.Visibility = _newCodeText.Visibility = Visibility.Visible;
        _newCodeText.Text = (copied ? "Your new class code (copied). " : "Your new class code: select it and press Ctrl+C to copy it. ") +
                       "Give it to your class, in your course page or assignment instructions; each learner types it in Hand in a report ▸ My details for reports. " +
                       "Keep it somewhere safe: you need it to check their reports. Anyone with the code can sign as your class, so give it only to your class. " +
                       "Make a new one each term. The class code box above is not changed: type this one there when you check reports made with it.";
    }

    private void Check_Click(object sender, RoutedEventArgs e)
    {
        _result.Visibility = Visibility.Visible;
        byte[]? pdf = null;
        var path = _file.Text.Trim().Trim('"');
        if (path.Length > 0)
        {
            try { pdf = File.ReadAllBytes(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                _result.Text = $"PLC+ could not read that file: {ex.Message} Check the name, or press Choose… to find it.";
                return;
            }
        }
        var code = _code.Text.Trim();
        if (code.Length > 0 && ClassCode.Normalize(code) is null)
        {
            _result.Text = "That class code is not right: a class code has 16 letters and numbers, in four groups of four. Check it, or leave the box empty.";
            return;
        }
        if (pdf is null && _line.Text.Trim().Length == 0)
        {
            _result.Text = "Choose a report, paste a result line, or both; then press Check.";
            return;
        }
        var text = new StringBuilder();
        if (pdf is not null)
        {
            var v = ReportSeal.Verify(pdf, code.Length > 0 ? code : null);
            text.AppendLine(v.Outcome switch { ReportCheck.Genuine => "✓ THE REPORT IS GENUINE", ReportCheck.Altered => "✗ THE REPORT WAS CHANGED", _ => "✗ NOT A SIGNED PLC+ REPORT" });
            text.AppendLine(v.Explanation);
            if (v.Snapshot is not null)
                text.AppendLine("(The verification code is printed on the report, to quote it; the computer key names the computer it was made on.)");
            if (v.Snapshot is { } s)
            {
                text.AppendLine();
                text.AppendLine(v.Outcome == ReportCheck.Genuine ? "What the report says:" : "What the report said when it was made:");
                text.AppendLine($"  {s.Learner.Name}{(s.Learner.StudentId is { } id ? $" ({id})" : "")}{(s.Learner.Course is { } c ? $", {c}" : "")}{(s.Learner.Section is { } sec ? $", section {sec}" : "")}");
                text.AppendLine($"  {s.PointTitle}: {(s.Status == ReportStatus.Complete ? "Complete" : "Incomplete")}, {s.Summary.ItemsComplete} of {s.Summary.Items} items, overall score {PlcReports.Percent(s.Summary.OverallScore)}");
                text.AppendLine($"  Made {s.Generated.ToLocalTime():d MMMM yyyy HH:mm}; report {s.ReportId}; verification code {v.VerificationCode}; computer key {v.KeyFingerprint}");
                if (s.Missing.Count > 0) text.AppendLine("  Missing: " + string.Join("; ", s.Missing));
            }
        }
        if (_line.Text.Trim().Length > 0)
        {
            if (pdf is not null) text.AppendLine();
            var (outcome, _, words) = ResultLine.Check(_line.Text, PlcReports.Options.LineMarker, code.Length > 0 ? code : null, pdf);
            text.AppendLine(outcome switch { LineCheck.Matches => "✓ THE RESULT LINE IS GENUINE", LineCheck.DoesNotMatch => "✗ THE RESULT LINE DOES NOT CHECK OUT", LineCheck.CannotCheck => "? THE RESULT LINE CANNOT BE CHECKED YET", _ => "✗ NOT A RESULT LINE" });
            text.AppendLine(words);
        }
        _result.Text = text.ToString().TrimEnd();
    }
}

/// <summary>
/// "Hand in a report" (plan 8.2–8.7): the final screen of a submission point, as in the Gamify+ games. Choose what the report
/// covers, see what it will say (grade, progress, name), save the PDF, and hand it in: the result line, Send to my instructor,
/// and every report made so far.
/// </summary>
internal sealed class ReportsView : UserControl
{
    private readonly ListBox _points = new() { MinHeight = 60, Margin = new Thickness(0, 4, 0, 6) };
    private readonly TextBlock _cardTitle = new() { FontSize = 20, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _cardStatus = new() { FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) };
    private readonly TextBlock _cardFacts = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _cardMissing = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly TextBlock _cardCover = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 4) };
    private readonly CheckBox _programs = new() { Content = "Put _pictures of my programs in the report", Margin = new Thickness(0, 4, 0, 8) };
    private readonly Button _save = new() { Content = "_Save the PDF report…", FontWeight = FontWeights.SemiBold, Padding = new Thickness(14, 5, 14, 5) };
    private readonly TextBlock _handInLead = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _line = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(4, 3, 4, 3), Margin = new Thickness(0, 6, 0, 6), FontFamily = new FontFamily("Consolas") };
    private readonly Button _copy = new() { Content = "_Copy the result line" };
    private readonly Button _send = new() { Content = "Send to my _instructor…" };
    private readonly Button _show = new() { Content = "Show the _file in its folder" };
    private readonly ListBox _history = new() { MinHeight = 60, Margin = new Thickness(0, 4, 0, 0) };
    private readonly StackPanel _handIn = new();
    private readonly TextBlock _handInDetail = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), FontWeight = FontWeights.SemiBold };
    private readonly TextBlock _lineHelp = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), FontSize = 12.5 };
    private WrapPanel _handInButtons = null!;

    private SubmissionRecord? _shown;

    public ContentPackage Package { get; set; } = null!;
    public Func<LearnerFile?> Session { get; set; } = () => null;
    public Action Save { get; set; } = () => { };
    public Action<string> Status { get; set; } = _ => { };
    /// <summary>A picture of a program that passed, for program evidence (the subject module draws it).</summary>
    public Func<ProgramEvidence, byte[]?> Picture { get; set; } = _ => null;

    public ReportsView()
    {
        var page = new StackPanel { Margin = new Thickness(0, 0, 12, 12), MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };

        page.Children.Add(Heading("1. Choose what the report covers"));
        page.Children.Add(Lead("Each module ends with a module check: that is where a report is due. You can make one before the check is complete; it is marked Incomplete and lists what is missing. Or make a full report of everything so far."));
        AutomationProperties.SetName(_points, "What the report covers");
        ScrollViewer.SetHorizontalScrollBarVisibility(_points, ScrollBarVisibility.Disabled);
        ScrollViewer.SetHorizontalScrollBarVisibility(_history, ScrollBarVisibility.Disabled);
        _points.SelectionChanged += (_, _) => ShowCard();
        page.Children.Add(_points);

        page.Children.Add(Heading("2. Your report"));
        var card = new StackPanel();
        card.Children.Add(_cardTitle);
        card.Children.Add(_cardStatus);
        card.Children.Add(_cardFacts);
        card.Children.Add(_cardMissing);
        card.Children.Add(_cardCover);
        var details = new Button { Content = "My _details for reports…", HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip = "Your student ID, course, section, e-mail and class code, for the cover. Only your name is required" };
        details.Click += (_, _) => EditDetails();
        card.Children.Add(details);
        _programs.ToolTip = "Each exercise you passed is shown as the ladder program you wrote, so your instructor can see how you solved it";
        _programs.Click += (_, _) =>
        {
            if (Data is not { } d) return;
            Details(d).IncludePrograms = _programs.IsChecked == true;
            Save();
            ShowCard();
        };
        card.Children.Add(_programs);
        _save.ToolTip = "Makes the report as a PDF and asks where to save it (the desktop, a USB drive or a cloud folder)";
        _save.HorizontalAlignment = HorizontalAlignment.Left;
        _save.Click += (_, _) => MakeReport();
        card.Children.Add(_save);
        var border = new Border { Padding = new Thickness(16, 12, 16, 14), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Child = card, Margin = new Thickness(0, 4, 0, 6) };
        border.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
        border.SetResourceReference(Border.BackgroundProperty, "PanelBrush");
        page.Children.Add(border);

        _handIn.Children.Add(Heading("3. Hand it in"));
        _handIn.Children.Add(_handInLead);
        _handIn.Children.Add(_handInDetail);
        _lineHelp.Text = "The result line is a one-line summary of this report: your name, the module check, Complete or Incomplete, your score, " +
                         "the report ID and a check code. Your instructor can paste it into PLC+ to confirm it is genuine. If they ask for it, " +
                         "copy the whole line, unchanged, and paste it where they say (for example an assignment's comment box).";
        _handIn.Children.Add(_lineHelp);
        AutomationProperties.SetName(_line, "The result line");
        _line.ToolTip = "The result line: paste it where your instructor asks (an assignment's comment, a discussion, an e-mail)";
        _handIn.Children.Add(_line);
        var buttons = new WrapPanel();
        _copy.ToolTip = "Copies the result line, to paste where your instructor asks";
        ToolTipService.SetShowOnDisabled(_copy, true);
        _copy.Click += (_, _) => CopyLine();
        _send.ToolTip = "Starts an e-mail to your instructor with the result line in it, and opens the folder with the PDF, to attach it. PLC+ never sends anything itself";
        _send.Click += (_, _) => SendToInstructor();
        _show.ToolTip = "Opens the folder the PDF was saved in, with the file chosen";
        _show.Click += (_, _) => ShowFile();
        foreach (var b in new[] { _copy, _send, _show }) { b.Margin = new Thickness(0, 0, 8, 6); buttons.Children.Add(b); }
        _handIn.Children.Add(buttons);
        _handInButtons = buttons;
        page.Children.Add(_handIn);

        page.Children.Add(Heading("Reports you have made"));
        page.Children.Add(Lead("Every report you make is listed here with its ID, so you can show exactly what you handed in. Choose one: its result line appears above, under 3. Hand it in. To make one again (after more work, or if the file is lost), choose its module check at the top and save a new report."));
        AutomationProperties.SetName(_history, "Reports you have made");
        _history.SelectionChanged += (_, _) =>
        {
            if (_history.SelectedItem is not ListBoxItem { Tag: SubmissionRecord r }) return;
            ShowHandIn(r);
            _handIn.BringIntoView();
        };
        page.Children.Add(_history);

        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = page, Focusable = false };
    }

    private LearnerData? Data => Session()?.Data;

    /// <summary>A list entry that wraps instead of scrolling sideways (and is still read by its words).</summary>
    private static TextBlock Wrapped(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };

    private static ListBoxItem Entry(string text, object? tag, string? tip = null)
    {
        var item = new ListBoxItem { Content = Wrapped(text), Tag = tag, ToolTip = tip };
        AutomationProperties.SetName(item, text);
        return item;
    }

    private static ReportDetails Details(LearnerData d) => d.ReportDetails ??= new ReportDetails();

    private static TextBlock Heading(string text)
    {
        var t = new TextBlock { Text = text, FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 2) };
        AutomationProperties.SetHeadingLevel(t, AutomationHeadingLevel.Level2);
        return t;
    }

    private static TextBlock Lead(string text)
    {
        var t = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 2) };
        t.SetResourceReference(ForegroundProperty, "MutedTextBrush");
        return t;
    }

    /// <summary>Shows the page with a submission point chosen (a module check's ID; null for the first one ready, or the first).</summary>
    public void Show(string? pointId)
    {
        if (Data is not { } d) return;
        var summary = Dashboard.Build(Package, d.Progress, DateTimeOffset.Now);
        _points.Items.Clear();
        foreach (var s in summary.Submissions)
        {
            var checkDone = d.Progress.StateOf(s.Check) == ItemState.Complete;
            var words = s.State switch
            {
                SubmissionState.Generated when s.Latest!.Complete != false => $"✓ report made ({s.Latest.Generated.ToLocalTime():d MMM})",
                SubmissionState.Generated when checkDone => $"◐ your report ({s.Latest!.Generated.ToLocalTime():d MMM}) says Incomplete, but the check is now complete: save a new report to hand in",
                SubmissionState.Generated => $"◐ Incomplete report made ({s.Latest!.Generated.ToLocalTime():d MMM}); finish the check, then save a new one",
                SubmissionState.Ready => "● complete: ready to hand in",
                _ => "○ not complete yet: " + s.Detail,
            };
            _points.Items.Add(Entry($"Module {s.Check.Module.Number} check: {s.Check.Module.Title} — {words}", s.Check,
                $"A report for module {s.Check.Module.Number}, {s.Check.Module.Title}: its items, scores, tests and outcomes"));
        }
        _points.Items.Add(Entry("Everything so far — a full progress report, every module written so far", SubmissionRecord.FullReport,
            "A report of every item in the modules written so far"));
        var chosen = _points.Items.Cast<ListBoxItem>().FirstOrDefault(i => i.Tag is LearningItem c && c.Id.Equals(pointId, StringComparison.OrdinalIgnoreCase))
            ?? (pointId == SubmissionRecord.FullReport ? _points.Items.Cast<ListBoxItem>().Last() : null)
            ?? _points.Items.Cast<ListBoxItem>().FirstOrDefault(i => i.Tag is LearningItem c && d.Progress.StateOf(c) == ItemState.Complete
                && !d.Progress.Submissions.Any(r => r.PointId.Equals(c.Id, StringComparison.OrdinalIgnoreCase) && r.Complete != false))
            ?? _points.Items.Cast<ListBoxItem>().First();
        _points.SelectedItem = chosen;

        _history.Items.Clear();
        foreach (var r in d.Progress.Submissions.OrderByDescending(r => r.Generated))
            _history.Items.Add(Entry($"{r.Generated.ToLocalTime():d MMM yyyy HH:mm} · {r.Title ?? r.PointId} · {(r.Complete == false ? "Incomplete" : "Complete")} · {r.ReportId}" +
                (r.FileName is { } f ? $" · {Path.GetFileName(f)}" : ""), r, $"Report {r.ReportId}. File fingerprint (SHA-256): {r.ReportHash}"));
        if (_history.Items.Count == 0) _history.Items.Add(new ListBoxItem { Content = "None yet.", IsEnabled = false });
        ShowCard();
    }

    private LearningItem? ChosenPoint => (_points.SelectedItem as ListBoxItem)?.Tag as LearningItem;

    /// <summary>The final screen (plan 8.2): what the report will say, before it is made.</summary>
    private void ShowCard()
    {
        if (Data is not { } d || _points.SelectedItem is null) return;
        var r = ReportBuilder.Build(Package, d, ChosenPoint, PlcReports.Options, DateTimeOffset.Now);
        var s = r.Summary;
        _cardTitle.Text = r.PointTitle;
        _cardStatus.Text = r.Status == ReportStatus.Complete
            ? "✓ Complete: ready to hand in"
            : "Incomplete: you can hand it in now, marked Incomplete, or finish first";
        _cardFacts.Text = $"{d.Profile.DisplayName} · overall score {PlcReports.Percent(s.OverallScore)} · {s.ItemsComplete} of {s.Items} items complete · " +
                          $"{s.OutcomesMastered} of {s.Outcomes} outcomes mastered · time {ReportPdf.Duration(s.Time)}" +
                          (r.Evidence.Count > 0 ? $" · {r.Evidence.Count} program{(r.Evidence.Count == 1 ? "" : "s")} shown" : "");
        _cardMissing.Text = r.Missing.Count == 0 ? "" : "Still to do:\n" + string.Join("\n", r.Missing.Select(m => "• " + m));
        _cardMissing.Visibility = r.Missing.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        var det = d.ReportDetails ?? new ReportDetails();
        var cover = new List<string>();
        if (r.Learner.StudentId is { } id) cover.Add($"student ID {id}");
        if (det.Course is { } c) cover.Add(c);
        if (det.Section is { } sec) cover.Add($"section {sec}");
        cover.Add(det.ClassCode is null ? "no class code (only needed if your instructor gave you one)" : "your class code");
        _cardCover.Text = "On the cover: your name, " + string.Join(", ", cover) + ".";
        _programs.IsChecked = det.IncludePrograms;
        _save.Content = r.Status == ReportStatus.Complete ? "_Save the PDF report…" : "_Save the PDF report (marked Incomplete)…";
        // Section 3 shows the latest report for the point chosen here, never another point's (so a line is not copied for the wrong check).
        ShowHandIn(d.Progress.Submissions.Where(x => x.PointId.Equals(r.PointId, StringComparison.OrdinalIgnoreCase)).MaxBy(x => x.Generated));
    }

    private bool EditDetails(bool firstReport = false)
    {
        if (Session() is not { } file) return false;
        var details = file.Data.ReportDetails ?? new ReportDetails();
        var window = new ReportDetailsWindow(Window.GetWindow(this), file.Data.Profile.DisplayName, details, firstReport);
        if (window.ShowDialog() != true)
        {
            if (firstReport) Status("No report was made. Press Save the PDF report again when you are ready: only your name is required, so the other boxes can stay empty.");
            return false;
        }
        file.Data.ReportDetails = details;
        Save();
        ShowCard();
        Status("Your details for reports are saved in your progress file.");
        return true;
    }

    /// <summary>Makes the report (plan 8.3, 8.6, 8.7): build, draw, sign, save where the learner chooses, and record it.</summary>
    private void MakeReport()
    {
        if (Session() is not { } file) return;
        var owner = Window.GetWindow(this);
        var d = file.Data;
        if (d.ReportDetails is not { Confirmed: true } && !EditDetails(firstReport: true)) return;   // the first time: what goes on the cover
        ComputerKey key;
        try { key = ComputerKeyStore.Load(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            MessageBox.Show(owner, $"PLC+ could not get this computer's signing key, so it cannot sign the report. Try again; if it keeps happening, ask whoever looks after this computer.\n\nDetails: {ex.Message}",
                "PLC+ — Save the PDF report", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        using (key)
        {
            var now = DateTimeOffset.Now;
            var r = ReportBuilder.Build(Package, d, ChosenPoint, PlcReports.Options, now, key.Fingerprint);
            if (r.Status == ReportStatus.Incomplete && !ChoiceWindow.Ask(owner, "PLC+ — Save the PDF report", "Make an incomplete report?",
                    $"{r.PointTitle} is not complete yet. The report will say Incomplete on its cover and list what is missing ({r.Missing.Count} " +
                    $"thing{(r.Missing.Count == 1 ? "" : "s")}). Your instructor decides whether to accept it. You can make another report when you have finished.",
                    "_Make it, marked Incomplete", "Makes the report now, marked Incomplete", "_Not now", "Goes back, so you can finish first"))
                return;
            var details = Details(d);
            var dialog = new SaveFileDialog
            {
                Title = "Save your progress report",
                Filter = "PDF report (*.pdf)|*.pdf",
                FileName = r.FileName(PlcReports.Options.FilePrefix),
                InitialDirectory = details.Folder is { } f && Directory.Exists(f) ? f : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };
            // PLCPLUS_SAVE_TO (window captures and tests only, like PLCPLUS_DATA): save there without the Save dialog.
            string path;
            if (Environment.GetEnvironmentVariable("PLCPLUS_SAVE_TO") is { Length: > 0 } testFolder) path = Path.Combine(testFolder, dialog.FileName);
            else if (dialog.ShowDialog(owner) == true) path = dialog.FileName;
            else return;
            SealedReport sealedReport;
            try
            {
                var pdf = ReportPdf.Render(r, key.PublicKey, PlcReports.Words, Picture);
                sealedReport = ReportSeal.Seal(pdf, key, details.ClassCode);
                File.WriteAllBytes(path, sealedReport.Pdf);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(owner, $"PLC+ could not save the report there. The folder may be read-only, or a file with that name is open in another program (close it, or choose another name).\n\nDetails: {ex.Message}",
                    "PLC+ — Save the PDF report", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var line = ResultLine.Issue(r, PlcReports.Options.LineMarker, sealedReport.Signature, details.ClassCode).Text();
            var record = new SubmissionRecord(r.PointId, r.ReportId, now, sealedReport.FileHash, r.Status == ReportStatus.Complete, r.PointTitle, line, path);
            d.Progress.Submissions.Add(record);
            d.Progress.Log(EventKinds.Reported, r.PointId, $"Made report {r.ReportId} ({(r.Status == ReportStatus.Complete ? "complete" : "incomplete")}): {Path.GetFileName(path)}.", now);
            d.Progress.LastChanged = now;
            details.Folder = Path.GetDirectoryName(path);
            Save();
            Show(ChosenPoint?.Id ?? SubmissionRecord.FullReport);
            ShowHandIn(record);
            Status($"Saved your report {r.ReportId} as {path}. Hand in that file exactly as it is (usually: upload it to the assignment); do not open and save it again in another program. Copy the result line too, if your instructor asks for it.");
            _line.Focus();
        }
    }

    private void ShowHandIn(SubmissionRecord? r)
    {
        _shown = r;
        var some = r is not null;
        _handInDetail.Visibility = _lineHelp.Visibility = _line.Visibility = _handInButtons.Visibility = some ? Visibility.Visible : Visibility.Collapsed;
        if (r is null)
        {
            _handInLead.Text = "After you save the report, its result line and the ways to hand it in appear here.";
            return;
        }
        var exists = r.FileName is { } f && File.Exists(f);
        var other = ChosenPoint?.Id ?? SubmissionRecord.FullReport;
        _handInLead.Text = (r.PointId.Equals(other, StringComparison.OrdinalIgnoreCase) ? "" : $"This is your earlier report for {r.Title}, not for the one chosen at the top. ") +
            $"Report {r.ReportId}, {r.Title}, {(r.Complete == false ? "Incomplete" : "Complete")}, made {r.Generated.ToLocalTime():d MMMM yyyy 'at' HH:mm}" +
            (r.FileName is null ? "." : exists ? $", saved as {r.FileName}." : $". It was saved as {r.FileName}, which is not there now (moved, or on a drive that is not plugged in).");
        _handInDetail.Text = "Hand in that PDF exactly as PLC+ saved it, the way your instructor asks (usually by uploading it to the assignment). " +
            "The report is signed: a hidden seal shows it was not changed. You can open it to read it, and renaming it is fine, but do not save it again, " +
            "print it to PDF or edit it: that breaks the seal, and your instructor's check will say it was changed.";
        _line.Text = r.ResultLine ?? "(This report has no result line.)";
        _copy.IsEnabled = r.ResultLine is not null;
        _copy.ToolTip = r.ResultLine is null ? "This report has no result line" : "Copies the result line, to paste where your instructor asks";
        _show.IsEnabled = exists;
        _show.ToolTip = exists ? "Opens the folder the PDF was saved in, with the file chosen" : "The file is not where it was saved";
        ToolTipService.SetShowOnDisabled(_show, true);
    }

    private void CopyLine()
    {
        if (_shown?.ResultLine is not { } line) return;
        try
        {
            Clipboard.SetText(line);
            Status("Copied the result line. Paste it (Ctrl+V) where your instructor asks: an assignment's comment box, a discussion, or an e-mail.");
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            Status("The result line could not be copied just now (another program is using the clipboard). Try again, or select the line and press Ctrl+C.");
        }
    }

    private void ShowFile()
    {
        if (_shown?.FileName is not { } f || !File.Exists(f)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{f}\"") { UseShellExecute = true });
    }

    /// <summary>Send to my instructor (plan 8.5): an e-mail with the result line, and the folder with the PDF to attach. Nothing is sent by PLC+.</summary>
    private void SendToInstructor()
    {
        if (_shown is not { } r || Data is not { } d) return;
        var to = Details(d).InstructorEmail ?? "";
        var subject = $"PLC+ report: {d.Profile.DisplayName}, {r.Title}";
        var body = $"Hello,\n\nMy PLC+ progress report for {r.Title} is attached ({(r.FileName is { } f ? Path.GetFileName(f) : "the PDF")}).\n\nResult line:\n{r.ResultLine}\n\n{d.Profile.DisplayName}";
        var mailto = $"mailto:{Uri.EscapeDataString(to)}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
        try
        {
            Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });
            var exists = r.FileName is { } path && File.Exists(path);
            ShowFile();
            Status((to.Length == 0 ? "Your e-mail program opened: type your instructor's address (or add it in My details for reports). " : $"Your e-mail program opened, to {to}. ") +
                   (exists ? "Attach the PDF: drag it from the folder that opened into the message. Then press Send in your e-mail program."
                           : $"The PDF is no longer at {r.FileName ?? "the place it was saved"}: find it, or save a new report, and attach it. Then press Send in your e-mail program."));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            try { Clipboard.SetText(body); } catch (System.Runtime.InteropServices.ExternalException) { }
            MessageBox.Show(Window.GetWindow(this),
                "This computer has no e-mail program set up to open. The message (with your result line) is copied: paste it into your e-mail in your web browser, and attach the PDF from the folder that opens.",
                "PLC+ — Send to my instructor", MessageBoxButton.OK, MessageBoxImage.Information);
            ShowFile();
        }
    }
}
