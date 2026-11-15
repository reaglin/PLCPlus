using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Learning.Core.Content;
using Learning.Core.Learners;

namespace Learning.Shell;

/// <summary>
/// Flashcards (Learning Core plan 3.11): an item's key terms, the term on the front and its definition on the back. Turn
/// over, previous, next, shuffle. Nothing is recorded: they are for the learner's own practice.
/// </summary>
public sealed class FlashcardsView : UserControl
{
    private readonly List<GlossaryEntry> _cards;
    private int _at;
    private bool _back;
    private readonly TextBlock _side = new() { FontSize = 12, Margin = new Thickness(0, 0, 0, 6) };
    private readonly TextBlock _face = new() { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _count = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    private readonly Button _card = new();

    public FlashcardsView(IEnumerable<GlossaryEntry> cards)
    {
        _cards = [.. cards];
        _side.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
        var inside = new StackPanel { Margin = new Thickness(16, 12, 16, 14) };
        inside.Children.Add(_side);
        inside.Children.Add(_face);
        _card.Content = inside;
        _card.MinHeight = 120;
        _card.MaxWidth = 560;
        _card.HorizontalAlignment = HorizontalAlignment.Left;
        _card.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _card.SetResourceReference(BackgroundProperty, "PageBrush");
        _card.Click += (_, _) => Turn();
        _card.ToolTip = "Click the card (or press Space) to turn it over";

        var buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        Button B(string text, string tip, Action act)
        {
            var b = new Button { Content = text, ToolTip = tip, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 6, 0) };
            b.Click += (_, _) => act();
            buttons.Children.Add(b);
            return b;
        }
        B("◂ Previous card", "The card before this one", () => Go(-1));
        B("Turn over", "Shows the other side of the card (Space)", Turn);
        B("Next card ▸", "The next card", () => Go(1));
        B("Shuffle", "Puts the cards in a new order and starts again", Shuffle);
        buttons.Children.Add(_count);

        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
        panel.Children.Add(_card);
        panel.Children.Add(buttons);
        Content = panel;
        AutomationProperties.SetLiveSetting(_face, AutomationLiveSetting.Polite);
        Show();
    }

    private void Turn()
    {
        _back = !_back;
        Show();
    }

    private void Go(int by)
    {
        if (_cards.Count == 0) return;
        _at = (_at + by + _cards.Count) % _cards.Count;
        _back = false;
        Show();
    }

    private void Shuffle()
    {
        var random = new Random();
        for (var i = _cards.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }
        _at = 0;
        _back = false;
        Show();
    }

    private void Show()
    {
        if (_cards.Count == 0) { _face.Text = "No key terms on this item yet."; return; }
        var card = _cards[_at];
        _side.Text = _back ? $"Back: what “{card.Term}” means" : "Front: the key term. What does it mean? Turn over to check.";
        _face.Text = _back ? card.Definition : card.Term;
        _face.FontSize = _back ? 15 : 22;
        _face.FontWeight = _back ? FontWeights.Normal : FontWeights.SemiBold;
        _count.Text = $"Card {_at + 1} of {_cards.Count}";
        AutomationProperties.SetName(_card, _back ? $"Card {_at + 1}, back: {card.Definition}" : $"Card {_at + 1}, front: {card.Term}. Press to turn over.");
    }
}

/// <summary>
/// The Reader's voice and speed (Learning Core plan 3.15), kept for the learner. Windows voices are free and work offline;
/// Google's natural voices are listed when a Google voice key is set up in AI settings, and cost a little.
/// </summary>
internal sealed class ReaderSettingsWindow : FormWindow
{
    public const string VoicePreference = "readerVoice";
    public const string RatePreference = "readerRate";

    private readonly ComboBox _voice = new() { MinWidth = 420 };
    private readonly ComboBox _rate = new() { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly LearnerData _data;

    private ReaderSettingsWindow(Window owner, LearnerData data, IReadOnlyList<ReaderVoice> voices)
        : base(owner, "PLC+ — Reader voice and speed", "Reader voice and speed",
            "The Reader reads the Learn, Laboratory, Review and Reference pages aloud: press 🔊 Read aloud above the tabs, or right-click a " +
            "paragraph ▸ Read aloud from here. Windows voices are free and work without the internet. Google's natural voices sound more " +
            "like a person and cost a little (a page is usually well under a cent); they need a Google voice key in AI ▸ AI settings….", 620)
    {
        _data = data;
        Body.Children.Add(new Label { Content = "_Voice:", Target = _voice, Padding = new Thickness(0, 6, 0, 2), FontWeight = FontWeights.SemiBold });
        foreach (var v in voices) _voice.Items.Add(new ComboBoxItem { Content = v.Name, Tag = v });
        var now = data.Preferences.GetValueOrDefault(VoicePreference);
        // A Google voice chosen before stays chosen when Google cannot be reached now, so saving the speed does not change it.
        if (now?.StartsWith("google:", StringComparison.Ordinal) == true && voices.All(v => v.Key != now))
            _voice.Items.Add(new ComboBoxItem { Content = now[7..] + " (Google; can't reach Google now)", Tag = new ReaderVoice(now, now[7..] + " (Google)", true) });
        _voice.SelectedItem = _voice.Items.OfType<ComboBoxItem>().FirstOrDefault(i => ((ReaderVoice)i.Tag).Key == now) ?? _voice.Items.OfType<ComboBoxItem>().FirstOrDefault();
        AutomationProperties.SetName(_voice, "Voice");
        _voice.ToolTip = "The voice that reads the pages aloud";
        Body.Children.Add(_voice);
        var row = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        var hear = new Button { Content = "_Hear it", ToolTip = "Says a sentence in the chosen voice and speed", Margin = new Thickness(0, 0, 8, 0) };
        hear.Click += async (_, _) => await Hear();
        row.Children.Add(hear);
        var google = new Button
        {
            Content = PlcAi.Shared.CanSpeak ? "Choose a _Google voice…" : "Set up _Google voices…",
            ToolTip = PlcAi.Shared.CanSpeak
                ? "AI Manager's voice chooser: every Google voice, by language and kind, with its price, and Hear it"
                : "Google's natural voices need a Google voice key: AI settings, the part \"Voices — Google text-to-speech\"",
        };
        google.Click += (_, _) => ChooseGoogle();
        row.Children.Add(google);
        Body.Children.Add(row);
        if (!voices.Any(v => v.Google) && PlcAi.Shared.CanSpeak)
            Body.Children.Add(Lead("Google's voices could not be listed just now (" + (PlcAi.Shared.GoogleVoicesProblem ?? "no answer") + "). Is this computer online? The Windows voices work without the internet.", ink: false));
        else if (!voices.Any(v => v.Google))
            Body.Children.Add(Lead("No Google voices are listed yet: they need a Google voice key, in AI settings (the part \"Voices — Google text-to-speech\"). Set up Google voices… opens it.", ink: false));

        Body.Children.Add(new Label { Content = "Spee_d:", Target = _rate, Padding = new Thickness(0, 10, 0, 2), FontWeight = FontWeights.SemiBold });
        foreach (var (rate, name) in new[] { (0.75, "Slower (0.75×)"), (1.0, "Normal (1×)"), (1.25, "A little faster (1.25×)"), (1.5, "Faster (1.5×)") })
            _rate.Items.Add(new ComboBoxItem { Content = name, Tag = rate });
        var r = double.TryParse(data.Preferences.GetValueOrDefault(RatePreference), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) ? x : 1.0;
        _rate.SelectedItem = _rate.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (double)i.Tag == r) ?? _rate.Items[1];
        AutomationProperties.SetName(_rate, "Speed");
        _rate.ToolTip = "How fast the Reader reads";
        Body.Children.Add(_rate);

        AddButton("_Save", "Keeps this voice and speed for you", (_, _) => Save(), isDefault: true);
        AddButton("Cancel", "Closes this; nothing changes (Esc)", (_, _) => DialogResult = false, isCancel: true);
        Closed += (_, _) => _sample.Dispose();
    }

    private ReaderVoice? Voice => (_voice.SelectedItem as ComboBoxItem)?.Tag as ReaderVoice;
    private double Rate => (_rate.SelectedItem as ComboBoxItem)?.Tag is double r ? r : 1.0;
    private readonly Reader _sample = new();

    private async Task Hear()
    {
        if (Voice is not { } v) return;
        Say(null);
        var problem = await _sample.ReadAsync(["A seal-in keeps the motor running after the Start button is released."], 0, v, Rate);
        if (problem is not null) Say(problem);
    }

    private void ChooseGoogle()
    {
        if (!PlcAi.Shared.CanSpeak)
        {
            PlcAi.Shared.ShowSettings(this);
            Say(PlcAi.Shared.CanSpeak ? "Google voices are set up: press Choose a Google voice…." : "Google voices are not set up yet; the Windows voices work without them.");
            return;
        }
        if (PlcAi.Shared.ChooseGoogleVoice(this, Voice is { Google: true } g ? g.Id : null) is not { } name) return;
        var item = _voice.Items.OfType<ComboBoxItem>().FirstOrDefault(i => ((ReaderVoice)i.Tag).Key == "google:" + name);
        if (item is null)
        {
            item = new ComboBoxItem { Content = name + " (Google)", Tag = new ReaderVoice("google:" + name, name + " (Google)", true) };
            _voice.Items.Add(item);
        }
        _voice.SelectedItem = item;
    }

    private void Save()
    {
        if (_voice.SelectedItem is ComboBoxItem { Tag: ReaderVoice v }) _data.Preferences[VoicePreference] = v.Key;
        if (_rate.SelectedItem is ComboBoxItem { Tag: double r }) _data.Preferences[RatePreference] = r.ToString(System.Globalization.CultureInfo.InvariantCulture);
        DialogResult = true;
    }

    public static async Task<bool> EditAsync(Window owner, LearnerData data)
    {
        var voices = ReaderVoice.WindowsVoices().Concat(await PlcAi.Shared.GoogleVoicesAsync()).ToList();
        return new ReaderSettingsWindow(owner, data, voices).ShowDialog() == true;
    }

    /// <summary>The learner's voice (Windows' default if none chosen, or if the chosen one is gone) and speed.</summary>
    public static (ReaderVoice Voice, double Rate) Chosen(LearnerData? data)
    {
        var key = data?.Preferences.GetValueOrDefault(VoicePreference);
        var rate = double.TryParse(data?.Preferences.GetValueOrDefault(RatePreference), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 1.0;
        if (key is null) return (ReaderVoice.WindowsDefault, rate);
        if (key.StartsWith("google:", StringComparison.Ordinal)) return (new ReaderVoice(key, key[7..] + " (Google)", true), rate);
        return (ReaderVoice.WindowsVoices().FirstOrDefault(v => v.Key == key) ?? ReaderVoice.WindowsDefault, rate);
    }
}

/// <summary>
/// The Glossary page in the path (Learning Core plan 3.13): every key term, A to Z, its definition, and the items that use
/// it, each opening the item. A search box finds a term by its words or its definition's.
/// </summary>
public sealed class GlossaryPanel : UserControl
{
    private readonly TextBox _search = new() { Width = 480, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(4, 3, 4, 3) };
    private readonly TextBlock _found = new();
    private readonly StackPanel _list = new();
    private readonly Dictionary<GlossaryEntry, FrameworkElement> _rows = [];
    private ContentPackage? _package;
    private IReadOnlyDictionary<GlossaryEntry, IReadOnlyList<LearningItem>> _used = new Dictionary<GlossaryEntry, IReadOnlyList<LearningItem>>();

    private readonly Button _back = new();

    /// <summary>Go back to the item and tab the learner came from.</summary>
    public Action Back { get; set; } = () => { };

    /// <summary>Shows "◂ Back to 5.2 (Learn)" when the learner came from an item, or hides it.</summary>
    public void SetBack(string? label)
    {
        _back.Content = label;
        _back.ToolTip = label is null ? null : "Goes back to the item and tab you were reading";
        _back.Visibility = label is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Open an item where a term is used.</summary>
    public Action<LearningItem, GlossaryEntry> Open { get; set; } = (_, _) => { };

    public GlossaryPanel()
    {
        var top = new StackPanel();
        var title = new TextBlock { Text = "Glossary", TextWrapping = TextWrapping.Wrap };
        title.SetResourceReference(StyleProperty, "SectionTitle");
        AutomationProperties.SetHeadingLevel(title, AutomationHeadingLevel.Level1);
        top.Children.Add(title);
        var lead = new TextBlock { Text = "The book's key terms, A to Z, with what each means and the items that use it. In a lesson, the item's key terms " +
            "have a dotted underline: point at one to read its meaning, click it to come here. Press an item number under Used in to open that item." };
        lead.SetResourceReference(StyleProperty, "Lead");
        top.Children.Add(lead);
        _back.Visibility = Visibility.Collapsed;
        _back.HorizontalAlignment = HorizontalAlignment.Left;
        _back.Margin = new Thickness(0, 0, 0, 8);
        _back.Click += (_, _) => Back();
        top.Children.Add(_back);
        var label = new TextBlock { Text = "Find a term", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 2) };
        top.Children.Add(label);
        _search.ToolTip = "Type part of a term or of its meaning. Empty shows every term.";
        AutomationProperties.SetName(_search, "Find a term");
        _search.TextChanged += (_, _) => Draw();
        top.Children.Add(_search);
        _found.SetResourceReference(StyleProperty, "Lead");
        _found.Margin = new Thickness(0, 4, 0, 6);
        AutomationProperties.SetLiveSetting(_found, AutomationLiveSetting.Polite);
        top.Children.Add(_found);
        var dock = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        dock.Children.Add(top);
        dock.Children.Add(new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false });
        Content = dock;
        AutomationProperties.SetName(_list, "Key terms");
    }

    public void Load(ContentPackage package)
    {
        _package = package;
        _used = package.Glossary.UsedIn(package);
        Draw();
    }

    /// <summary>Shows one term: the search is cleared, and the term is marked and scrolled to.</summary>
    public void ShowTerm(GlossaryEntry entry)
    {
        _search.Text = "";
        Draw(entry);
        if (_rows.TryGetValue(entry, out var row)) Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => row.BringIntoView());
    }

    public void FocusSearch() => _search.Focus();

    private void Draw(GlossaryEntry? marked = null)
    {
        _list.Children.Clear();
        _rows.Clear();
        if (_package is null) return;
        var all = _package.Glossary.Entries;
        var query = _search.Text.Trim();
        var shown = query.Length == 0 ? all : [.. all.Where(e => (e.Term + " " + string.Join(" ", e.Also) + " " + e.Definition).Contains(query, StringComparison.CurrentCultureIgnoreCase))];
        _found.Text = all.Count == 0 ? "This book has no glossary yet."
            : query.Length == 0 ? $"{all.Count} key terms."
            : shown.Count == 0 ? $"No term has “{query}”. Try fewer letters." : $"{shown.Count} of {all.Count} terms have “{query}”.";
        foreach (var e in shown)
        {
            var panel = new StackPanel();
            if (e == marked) panel.Children.Add(new TextBlock { Text = "▶ The term you chose", FontSize = 12, FontWeight = FontWeights.SemiBold });
            panel.Children.Add(new TextBlock { Text = e.Term, FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            if (e.Also.Count > 0)
            {
                var also = new TextBlock { Text = "Also: " + string.Join(", ", e.Also), FontSize = 12, TextWrapping = TextWrapping.Wrap };
                also.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
                panel.Children.Add(also);
            }
            panel.Children.Add(new TextBlock { Text = e.Definition, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 4) });
            var items = _used.GetValueOrDefault(e) ?? [];
            if (items.Count > 0)
            {
                var row = new WrapPanel();
                var usedIn = new TextBlock { Text = "Used in:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0), FontSize = 12 };
                usedIn.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
                row.Children.Add(usedIn);
                foreach (var item in items)
                {
                    var b = new Button { Content = item.Number, ToolTip = $"Opens {item.Number} {item.Title}", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 4, 2) };
                    AutomationProperties.SetName(b, $"Open {item.Number} {item.Title}");
                    var entry = e;
                    b.Click += (_, _) => Open(item, entry);
                    row.Children.Add(b);
                }
                panel.Children.Add(row);
            }
            var card = new Border
            {
                Child = panel, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 8),
                BorderThickness = e == marked ? new Thickness(6, 3, 3, 3) : new Thickness(1),
            };
            card.SetResourceReference(Border.BorderBrushProperty, e == marked ? "AccentBrush" : "LineBrush");
            card.SetResourceReference(Border.BackgroundProperty, "PageBrush");
            AutomationProperties.SetName(card, $"{e.Term}: {e.Definition}");
            _rows[e] = card;
            _list.Children.Add(card);
        }
    }
}
