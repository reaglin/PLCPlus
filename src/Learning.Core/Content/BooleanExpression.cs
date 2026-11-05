namespace Learning.Core.Content;

/// <summary>
/// A logic expression for the truth-table widget, such as <c>(Start OR Motor) AND NOT Stop</c>.
/// Words: AND, OR, XOR, NOT and brackets; names are letters, digits and underscores. NOT binds
/// tightest, then AND, then XOR, then OR, as in most textbooks.
/// </summary>
public sealed class BooleanExpression
{
    private readonly Node _root;

    public IReadOnlyList<string> Variables { get; }
    public string Text { get; }

    private BooleanExpression(string text, Node root, IReadOnlyList<string> variables)
    {
        Text = text;
        _root = root;
        Variables = variables;
    }

    public bool Evaluate(IReadOnlyDictionary<string, bool> values) => _root.Eval(values);

    /// <summary>Every row of the truth table: the inputs in order (all FALSE first) and the result.</summary>
    public IEnumerable<(bool[] Inputs, bool Result)> Rows()
    {
        var n = Variables.Count;
        for (var row = 0; row < 1 << n; row++)
        {
            var inputs = new bool[n];
            var values = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            for (var v = 0; v < n; v++)
            {
                inputs[v] = (row >> (n - 1 - v) & 1) == 1;
                values[Variables[v]] = inputs[v];
            }
            yield return (inputs, Evaluate(values));
        }
    }

    /// <exception cref="ContentException">The expression cannot be read; the message says where.</exception>
    public static BooleanExpression Parse(string text)
    {
        var tokens = Tokenize(text);
        var variables = new List<string>();
        var at = 0;

        string? Peek() => at < tokens.Count ? tokens[at] : null;
        bool Accept(string word)
        {
            if (!string.Equals(Peek(), word, StringComparison.OrdinalIgnoreCase)) return false;
            at++;
            return true;
        }

        Node Or()
        {
            var left = Xor();
            while (Accept("OR")) { var l = left; var r = Xor(); left = new Node(v => l.Eval(v) || r.Eval(v)); }
            return left;
        }
        Node Xor()
        {
            var left = And();
            while (Accept("XOR")) { var l = left; var r = And(); left = new Node(v => l.Eval(v) ^ r.Eval(v)); }
            return left;
        }
        Node And()
        {
            var left = Not();
            while (Accept("AND")) { var l = left; var r = Not(); left = new Node(v => l.Eval(v) && r.Eval(v)); }
            return left;
        }
        Node Not()
        {
            if (Accept("NOT")) { var inner = Not(); return new Node(v => !inner.Eval(v)); }
            if (Accept("("))
            {
                var inner = Or();
                if (!Accept(")")) throw new ContentException($"The truth table \"{text}\" opens a bracket that is never closed.");
                return inner;
            }
            var name = Peek() ?? throw new ContentException($"The truth table \"{text}\" ends where a name was expected.");
            if (name is "(" or ")" || IsWord(name)) throw new ContentException($"The truth table \"{text}\" has \"{name}\" where a name was expected.");
            at++;
            if (!variables.Contains(name, StringComparer.OrdinalIgnoreCase)) variables.Add(name);
            return new Node(v => v[name]);
        }

        var root = Or();
        if (at < tokens.Count) throw new ContentException($"The truth table \"{text}\" has \"{tokens[at]}\" that PLC+ cannot read there.");
        if (variables.Count > 4) throw new ContentException($"The truth table \"{text}\" has {variables.Count} inputs; four is the most that fits on a page (16 rows).");
        return new BooleanExpression(text, root, variables);
    }

    private static bool IsWord(string s) => s.ToUpperInvariant() is "AND" or "OR" or "XOR" or "NOT";

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c is '(' or ')') { tokens.Add(c.ToString()); i++; continue; }
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                var start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                tokens.Add(text[start..i]);
                continue;
            }
            throw new ContentException($"The truth table \"{text}\" has \"{c}\", which PLC+ cannot read. Use AND, OR, XOR, NOT and brackets.");
        }
        return tokens;
    }

    private sealed class Node(Func<IReadOnlyDictionary<string, bool>, bool> eval)
    {
        public bool Eval(IReadOnlyDictionary<string, bool> v) => eval(v);
    }
}
