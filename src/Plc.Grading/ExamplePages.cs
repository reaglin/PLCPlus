using System.Text.RegularExpressions;

namespace Plc.Grading;

/// <summary>
/// The Example tab's written examples (Ron, 2026-11-17: "the Try it goes into the Example section and always contains some type of
/// figure … if there are multiple larger examples, we can add tabs for Example 1, Example 2"). An item's file holds them after its
/// Learn page, each starting with a line of its own:
/// <code>
/// [[example: Ohm's law on a relay coil]]
/// What to try, then the interactive example …
/// </code>
/// A section runs to the next <c>[[example: …]]</c> line or the end of the file. Everything before the first is the Learn page. On the
/// Learn page, <c>[[see-example: 1]]</c> is a button that opens Example 1.
/// </summary>
public static partial class ExamplePages
{
    /// <summary>One written example: its title and its Markdown (the widgets in it included).</summary>
    public sealed record Page(string Title, string Markdown);

    [GeneratedRegex(@"^\s*\[\[\s*example\s*:\s*(?<title>.*?)\s*\]\]\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Start();

    /// <summary>The Learn page and the written examples of an item's Markdown.</summary>
    public static (string Learn, IReadOnlyList<Page> Examples) Split(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var learn = new List<string>();
        var pages = new List<Page>();
        string? title = null;
        var body = new List<string>();
        void Close()
        {
            if (title is not null) pages.Add(new Page(title, string.Join("\n", body).Trim() + "\n"));
            body.Clear();
        }
        foreach (var line in lines)
        {
            var m = Start().Match(line);
            if (m.Success)
            {
                Close();
                title = m.Groups["title"].Value;
                continue;
            }
            (title is null ? learn : body).Add(line);
        }
        Close();
        return (string.Join("\n", learn).TrimEnd() + "\n", pages);
    }
}
