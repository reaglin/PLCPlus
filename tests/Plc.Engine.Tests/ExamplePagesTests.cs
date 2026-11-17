using Learning.Core.Content;
using Plc.Grading;

namespace Plc.Engine.Tests;

/// <summary>
/// Plan 13.15 (Ron, 2026-11-17): the Try it examples moved from the Learn page to the Example tab, each with a figure, numbered
/// Example 1, Example 2 … when an item has more than one; try-circuit starts at the worked example's numbers.
/// </summary>
public class ExamplePagesTests
{
    private static readonly string Folder = Path.Combine(K.RepoRoot(), "content", "plcplus-foundations");

    [Fact]
    public void An_item_splits_into_its_Learn_page_and_its_examples()
    {
        var (learn, pages) = ExamplePages.Split("# Title\n\nText.\n\n[[see-example: 1]]\n\nMore.\n\n[[example: First]]\n\nTry this.\n\n[[try-circuit: series]]\n\n[[example:   Second  ]]\n\n[[try-number: bcd]]\n");
        Assert.Contains("More.", learn);
        Assert.DoesNotContain("Try this", learn);
        Assert.Equal(["First", "Second"], pages.Select(p => p.Title));
        Assert.Contains("[[try-circuit: series]]", pages[0].Markdown);
        Assert.DoesNotContain("try-number", pages[0].Markdown);
    }

    [Fact]
    public void An_item_without_examples_is_all_Learn_page()
    {
        var (learn, pages) = ExamplePages.Split("# Title\r\n\r\nText.\r\n");
        Assert.Empty(pages);
        Assert.Equal("# Title\n\nText.\n", learn);
    }

    [Fact]
    public void Every_try_widget_is_on_the_Example_tab_with_a_button_to_it_on_the_Learn_page()
    {
        var package = ContentPackage.Load(Folder);
        var withExamples = 0;
        foreach (var item in package.AllItems)
        {
            var (learn, pages) = ExamplePages.Split(item.Learn);
            Assert.DoesNotContain(Widgets.In(learn), w => w.Name.StartsWith("try-", StringComparison.Ordinal));
            if (pages.Count == 0) continue;
            withExamples++;
            var buttons = Widgets.In(learn).Where(w => w.Name == "see-example").Select(w => int.Parse(w.Argument.Trim())).ToList();
            Assert.Equal(Enumerable.Range(1, pages.Count), buttons.Order());
            Assert.All(pages, p => Assert.Contains(Widgets.In(p.Markdown), w => w.Name.StartsWith("try-", StringComparison.Ordinal)));
        }
        Assert.True(withExamples >= 39, $"only {withExamples} items have examples");
    }

    [Fact]
    public void An_example_without_a_figure_or_a_button_to_nowhere_is_a_problem()
    {
        var problems = PackageChecksFor("# T\n\n[[see-example: 3]]\n\n[[example: Words only]]\n\nNothing to try.\n");
        Assert.Contains(problems, p => p.Contains("has no figure", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("names no example", StringComparison.Ordinal));
    }

    private static List<string> PackageChecksFor(string learn)
    {
        var folder = Path.Combine(Path.GetTempPath(), "plcplus-examples-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(Folder, folder);
        try
        {
            var file = Directory.GetFiles(Path.Combine(folder, "modules", "01-electrical-foundations"), "01-*.md").Single();
            var text = File.ReadAllText(file);
            var end = text.IndexOf("\n---", 4, StringComparison.Ordinal) + 4;
            File.WriteAllText(file, text[..end] + "\n" + learn);
            return [.. PackageChecks.Validate(folder)];
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static void CopyDirectory(string from, string to)
    {
        foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(dir.Replace(from, to, StringComparison.Ordinal));
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories)) File.Copy(file, file.Replace(from, to, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("ohms-law", 24, "R=480")]
    [InlineData("ohms-law 12V Coil=240", 12, "Coil=240")]
    [InlineData("series 24V 200 400", 24, "R1=200 R2=400")]
    [InlineData("parallel 24V Coil=480 Light1=960 Light2=960 Light3=960", 24, "Coil=480 Light1=960 Light2=960 Light3=960")]
    public void A_circuit_starts_at_the_values_it_is_given(string argument, double volts, string parts)
    {
        var spec = CircuitSpec.Parse(argument, out var problem);
        Assert.Null(problem);
        Assert.NotNull(spec);
        Assert.Equal(volts, spec.Volts);
        Assert.Equal(parts, string.Join(" ", spec.Parts.Select(p => $"{p.Name}={p.Ohms}")));
    }

    [Theory]
    [InlineData("resistor")]
    [InlineData("ohms-law 24V 480 960")]
    [InlineData("series 24V 200")]
    [InlineData("parallel 24V 1 2 3")]
    [InlineData("parallel 99V")]
    [InlineData("ac-dc 24V")]
    public void A_circuit_that_cannot_be_drawn_says_why(string argument)
    {
        Assert.Null(CircuitSpec.Parse(argument, out var problem));
        Assert.False(string.IsNullOrWhiteSpace(problem));
    }
}
