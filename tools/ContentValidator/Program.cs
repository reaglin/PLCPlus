using Plc.Grading;

// Usage: dotnet run --project tools/ContentValidator [package folder]
// Checks IDs, prerequisites, outcomes, challenges, programs, reference pages and widgets.
// Exit code 0 when the package is clean, 1 when it has problems, 2 when the folder is missing.

var folder = args.Length > 0 ? args[0] : Path.Combine("content", "plcplus-foundations");
if (!Directory.Exists(folder))
{
    Console.Error.WriteLine($"There is no folder {Path.GetFullPath(folder)}. Give the package folder, e.g. content/plcplus-foundations.");
    return 2;
}

var problems = PackageChecks.Validate(folder);
if (problems.Count == 0)
{
    Console.WriteLine($"{Path.GetFullPath(folder)}: no problems.");
    return 0;
}

Console.WriteLine($"{Path.GetFullPath(folder)}: {problems.Count} problem{(problems.Count == 1 ? "" : "s")}.");
foreach (var p in problems) Console.WriteLine("  - " + p);
return 1;
