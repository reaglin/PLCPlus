namespace Learning.Core.Content;

/// <summary>
/// The block of settings at the top of a lesson file, between two lines of three dashes (spec 6.3).
/// Only the small part of YAML the content uses: <c>key: value</c>, <c>key: [a, b]</c>, and a list
/// written as lines starting with "- " under a <c>key:</c> line. Anything else is a mistake in the
/// file and is reported, so an author learns about it from the validator, not from a learner.
/// </summary>
public sealed class FrontMatter
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _lists = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Keys => [.. _values.Keys, .. _lists.Keys];

    public string? Get(string key) => _values.TryGetValue(key, out var v) && v.Length > 0 ? v : null;

    public IReadOnlyList<string> List(string key) =>
        _lists.TryGetValue(key, out var l) ? l : Get(key) is { } single ? [single] : [];

    public int? Int(string key) => int.TryParse(Get(key), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null;

    /// <summary>Splits a file into its front-matter and the Markdown after it.</summary>
    /// <exception cref="ContentException">The front-matter is not closed or has a line it cannot read.</exception>
    public static (FrontMatter Matter, string Body) Parse(string text, string fileForMessages)
    {
        var matter = new FrontMatter();
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "---") return (matter, text);

        string? listKey = null;
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Trim() == "---")
                return (matter, string.Join('\n', lines.Skip(i + 1)).Trim('\n'));
            if (line.Trim().Length == 0 || line.TrimStart().StartsWith('#')) continue;

            var trimmed = line.Trim();
            if (trimmed.StartsWith("- ", StringComparison.Ordinal) && listKey is not null)
            {
                matter._lists[listKey].Add(Unquote(trimmed[2..]));
                continue;
            }

            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0 || char.IsWhiteSpace(line[0]))
                throw new ContentException($"{fileForMessages}, line {i + 1}: \"{trimmed}\" is not a setting. Write it as name: value.");
            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (value.Length == 0)
            {
                listKey = key;
                matter._lists[key] = [];
            }
            else if (value.StartsWith('[') && value.EndsWith(']'))
            {
                listKey = null;
                matter._lists[key] = [.. value[1..^1].Split(',').Select(s => Unquote(s.Trim())).Where(s => s.Length > 0)];
            }
            else
            {
                listKey = null;
                matter._values[key] = Unquote(value);
            }
        }
        throw new ContentException($"{fileForMessages}: the settings at the top start with --- but never end with ---.");
    }

    private static string Unquote(string s) =>
        s.Length >= 2 && (s[0] == '"' && s[^1] == '"' || s[0] == '\'' && s[^1] == '\'') ? s[1..^1] : s;
}

/// <summary>A content file PLC+ cannot read: the message says which file and what to change.</summary>
public sealed class ContentException(string message) : Exception(message);
