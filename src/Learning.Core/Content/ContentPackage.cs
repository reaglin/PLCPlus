using System.Text.Json;

namespace Learning.Core.Content;

// The learning path (spec 6.1, Ron 2026-11-04): a package holds modules; a module holds items that
// build on each other. Every item has four tabs: Learn, Example, Exercise and Reference. The core
// knows nothing about what an example or exercise is; it names a challenge, and the subject module
// (PLC+: Plc.Grading and the machine screen) runs it.

/// <summary>How far a module's text has got, so a reviewer knows what they are reading.</summary>
public enum ModuleStatus
{
    /// <summary>Written and checked.</summary>
    Ready,
    /// <summary>A first draft, for the instructors' review.</summary>
    Draft,
    /// <summary>Outcomes, topics and item titles only; the pages are still to be written.</summary>
    Outline,
    /// <summary>In the path so its place is clear; its items come in a later phase.</summary>
    ComingLater,
}

public enum ItemKind
{
    /// <summary>An item to learn from: Learn, Example, Exercise and Reference.</summary>
    Lesson,
    /// <summary>The module's check: a submission point (spec 6.1) covering the module's items.</summary>
    Assess,
}

/// <summary>A learning outcome with a stable ID, so it can be matched to a course's or a grant's outcomes.</summary>
public sealed record Outcome(string Id, string Text);

/// <summary>A page of the Reference tab, shared by the items that list it.</summary>
public sealed record ReferencePage(string Name, string Title, string Body);

public sealed class LearningModule
{
    public required string Id { get; init; }
    public required int Number { get; init; }
    public required string Title { get; init; }
    public string Introduction { get; init; } = "";
    public IReadOnlyList<string> Prerequisites { get; init; } = [];
    public IReadOnlyList<Outcome> Outcomes { get; init; } = [];
    public IReadOnlyList<string> Topics { get; init; } = [];
    public int Minutes { get; init; }
    public ModuleStatus Status { get; init; }
    public List<LearningItem> Items { get; } = [];
    public string File { get; init; } = "";

    public override string ToString() => $"Module {Number} · {Title}";
}

public sealed class LearningItem
{
    public required string Id { get; init; }
    /// <summary>"5.2": the module's number and the item's place in it.</summary>
    public required string Number { get; init; }
    public required string Title { get; init; }
    public ItemKind Kind { get; init; }
    public int Minutes { get; init; }
    public IReadOnlyList<string> Objectives { get; init; } = [];
    /// <summary>The item this one builds on (usually the one before it).</summary>
    public string? BuildsOn { get; init; }
    /// <summary>The challenge whose working program the Example tab runs.</summary>
    public string? Example { get; init; }
    /// <summary>Which of that challenge's programs the Example tab starts with (its title; the first if empty).</summary>
    public string? ExampleProgram { get; init; }
    /// <summary>The challenge the learner's own work is graded against in the Exercise tab.</summary>
    public string? Exercise { get; init; }
    /// <summary>For the module check: the items it covers. It is complete when they all are.</summary>
    public IReadOnlyList<string> Covers { get; init; } = [];
    public IReadOnlyList<string> References { get; init; } = [];
    /// <summary>The Learn tab's Markdown.</summary>
    public string Learn { get; init; } = "";
    public string File { get; init; } = "";
    public required LearningModule Module { get; init; }

    /// <summary>A lesson with no exercise is complete when the learner says they have read it.</summary>
    public bool CompletesByReading => Kind == ItemKind.Lesson && Exercise is null;

    public override string ToString() => $"{Number} {Title}";
}

/// <summary>A content package (spec 6.3): a folder with manifest.json, one folder per module, and reference pages.</summary>
public sealed class ContentPackage
{
    public required string Id { get; init; }
    public required string Version { get; init; }
    public required string Title { get; init; }
    public required string Folder { get; init; }
    public IReadOnlyList<LearningModule> Modules { get; init; } = [];
    public IReadOnlyDictionary<string, ReferencePage> References { get; init; } = new Dictionary<string, ReferencePage>();

    public IEnumerable<LearningItem> AllItems => Modules.SelectMany(m => m.Items);

    public LearningItem? FindItem(string id) => AllItems.FirstOrDefault(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public LearningModule? FindModule(string id) => Modules.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Reads a package folder. The manifest lists the module folders in path order; each has a
    /// <c>module.md</c> and one Markdown file per item, in file-name order. Reference pages are the
    /// Markdown files in <c>reference/</c>.
    /// </summary>
    /// <exception cref="ContentException">A file is missing or cannot be read; the message says which.</exception>
    public static ContentPackage Load(string folder)
    {
        var manifestPath = Path.Combine(folder, "manifest.json");
        if (!System.IO.File.Exists(manifestPath)) throw new ContentException($"There is no manifest.json in {folder}.");
        using var doc = Parse(manifestPath);
        var root = doc.RootElement;
        string Text(string name) => root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString()! : "";

        var modules = new List<LearningModule>();
        if (root.TryGetProperty("modules", out var list))
            foreach (var entry in list.EnumerateArray())
                modules.Add(LoadModule(folder, entry.GetString() ?? ""));

        var references = new Dictionary<string, ReferencePage>(StringComparer.OrdinalIgnoreCase);
        var refFolder = Path.Combine(folder, "reference");
        if (Directory.Exists(refFolder))
            foreach (var file in Directory.GetFiles(refFolder, "*.md").Order(StringComparer.Ordinal))
            {
                var (matter, body) = FrontMatter.Parse(System.IO.File.ReadAllText(file), Relative(folder, file));
                var name = Path.GetFileNameWithoutExtension(file);
                references[name] = new ReferencePage(name, matter.Get("title") ?? name, body);
            }

        return new ContentPackage
        {
            Id = Text("id"), Version = Text("version"), Title = Text("title"), Folder = folder,
            Modules = modules, References = references,
        };
    }

    private static LearningModule LoadModule(string package, string relative)
    {
        var dir = Path.Combine(package, relative);
        var file = Path.Combine(dir, "module.md");
        if (!System.IO.File.Exists(file)) throw new ContentException($"The manifest lists the module {relative}, but {relative}/module.md is missing.");
        var name = Relative(package, file);
        var (m, body) = FrontMatter.Parse(System.IO.File.ReadAllText(file), name);
        var module = new LearningModule
        {
            Id = m.Get("id") ?? throw new ContentException($"{name} has no id."),
            Number = m.Int("number") ?? throw new ContentException($"{name} has no number."),
            Title = m.Get("title") ?? throw new ContentException($"{name} has no title."),
            Introduction = body,
            Prerequisites = m.List("prerequisites"),
            Outcomes = [.. m.List("outcomes").Select(o => ParseOutcome(o, name))],
            Topics = m.List("topics"),
            Minutes = m.Int("minutes") ?? 0,
            Status = ParseStatus(m.Get("status"), name),
            File = name,
        };

        var files = Directory.GetFiles(dir, "*.md")
            .Where(f => !Path.GetFileName(f).Equals("module.md", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal);
        var n = 0;
        foreach (var itemFile in files)
        {
            var itemName = Relative(package, itemFile);
            var (i, learn) = FrontMatter.Parse(System.IO.File.ReadAllText(itemFile), itemName);
            n++;
            module.Items.Add(new LearningItem
            {
                Id = i.Get("id") ?? throw new ContentException($"{itemName} has no id."),
                Number = $"{module.Number}.{n}",
                Title = i.Get("title") ?? throw new ContentException($"{itemName} has no title."),
                Kind = i.Get("kind") switch
                {
                    null or "lesson" => ItemKind.Lesson,
                    "assess" => ItemKind.Assess,
                    var other => throw new ContentException($"{itemName}: kind \"{other}\" is not one PLC+ knows. Use lesson or assess."),
                },
                Minutes = i.Int("minutes") ?? 0,
                Objectives = i.List("objectives"),
                BuildsOn = i.Get("builds-on"),
                Example = i.Get("example"),
                ExampleProgram = i.Get("example-program"),
                Exercise = i.Get("exercise"),
                Covers = i.List("covers"),
                References = i.List("reference"),
                Learn = learn,
                File = itemName,
                Module = module,
            });
        }
        return module;
    }

    private static Outcome ParseOutcome(string text, string file)
    {
        var colon = text.IndexOf(": ", StringComparison.Ordinal);
        if (colon <= 0) throw new ContentException($"{file}: the outcome \"{text}\" needs an ID first, like plc.m5.o1: Write a rung …");
        return new Outcome(text[..colon].Trim(), text[(colon + 2)..].Trim());
    }

    private static ModuleStatus ParseStatus(string? status, string file) => status switch
    {
        null or "ready" => ModuleStatus.Ready,
        "draft" => ModuleStatus.Draft,
        "outline" => ModuleStatus.Outline,
        "coming-later" => ModuleStatus.ComingLater,
        _ => throw new ContentException($"{file}: status \"{status}\" is not one PLC+ knows. Use ready, draft, outline or coming-later."),
    };

    private static JsonDocument Parse(string path)
    {
        try
        {
            return JsonDocument.Parse(System.IO.File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new ContentException($"{path} is not valid JSON: {ex.Message}");
        }
    }

    private static string Relative(string package, string file) => Path.GetRelativePath(package, file).Replace('\\', '/');
}
