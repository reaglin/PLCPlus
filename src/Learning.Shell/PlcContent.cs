using System.IO;
using System.Text.Json;
using Plc.Grading;

namespace Learning.Shell;

/// <summary>A program that ships with a challenge: the one that works, or one with a common mistake.</summary>
public sealed record ProgramEntry(string Title, string File, string Note);

/// <summary>A challenge in the package, with its machine's programs.</summary>
public sealed record ChallengeEntry(string File, PlcChallenge Challenge, string Json, IReadOnlyList<ProgramEntry> Programs);

/// <summary>
/// The PLC half of a content package: the challenges listed in manifest.json, with their programs.
/// The learning path itself (modules and items) is Learning.Core's <c>ContentPackage</c>.
/// </summary>
public sealed class PlcContent
{
    public static readonly string DefaultFolder = Path.Combine(AppContext.BaseDirectory, "content", "plcplus-foundations");

    private readonly Dictionary<string, ChallengeEntry> _byId = new(StringComparer.OrdinalIgnoreCase);

    public string Folder { get; }

    public IReadOnlyCollection<ChallengeEntry> Challenges => _byId.Values;

    public PlcContent(string folder)
    {
        Folder = folder;
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "manifest.json")));
        foreach (var c in doc.RootElement.GetProperty("challenges").EnumerateArray())
        {
            var file = c.GetProperty("file").GetString()!;
            var json = File.ReadAllText(Path.Combine(folder, file));
            var programs = c.GetProperty("programs").EnumerateArray()
                .Select(p => new ProgramEntry(p.GetProperty("title").GetString()!, p.GetProperty("file").GetString()!, p.GetProperty("note").GetString() ?? ""))
                .ToList();
            var challenge = PlcChallengeRunner.LoadChallenge(json);
            _byId[challenge.Id] = new ChallengeEntry(file, challenge, json, programs);
        }
    }

    public ChallengeEntry? Find(string? id) => id is not null && _byId.TryGetValue(id, out var c) ? c : null;

    public string Read(string relative) => File.ReadAllText(Path.Combine(Folder, relative));

    public string ScenePath(string sceneId) => Path.Combine(Folder, "scenes", sceneId + ".json");
}
