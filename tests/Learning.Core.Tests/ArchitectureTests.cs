using System.Xml.Linq;
using Learning.Core;

namespace Learning.Core.Tests;

/// <summary>Spec 12 / NFR-5: the Learning Core is subject-neutral and never references a Plc.* assembly.</summary>
public class ArchitectureTests
{
    [Fact]
    public void LearningCoreAssemblyReferencesNoPlcAssembly()
    {
        var references = typeof(IChallengeRunner).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");
        Assert.DoesNotContain(references, n => n.StartsWith("Plc.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LearningCoreProjectHasNoPlcProjectOrPackageReference()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "PLCPlus.slnx"))) dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        var csproj = XDocument.Load(Path.Combine(dir, "src", "Learning.Core", "Learning.Core.csproj"));
        var includes = csproj.Descendants()
            .Where(e => e.Name.LocalName is "ProjectReference" or "PackageReference" or "Reference")
            .Select(e => (string?)e.Attribute("Include") ?? "");
        Assert.DoesNotContain(includes, i => i.Contains("Plc.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WorkHashIsStableSha256()
    {
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", WorkHash.Of("abc"));
    }
}
