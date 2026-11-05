using System.Collections.Concurrent;
using PdfSharp.Fonts;

namespace Learning.Reports.Pdf;

/// <summary>
/// Gives PDFsharp its fonts (as SMADA's resolver does): the core PDFsharp package has none of its own. Reads the font files
/// directly, so the renderer needs no GDI or WPF. The fonts are embedded in the report (PDF/A needs it).
/// </summary>
internal sealed class ReportFontResolver : IFontResolver
{
    private static readonly ConcurrentDictionary<string, byte[]> Cache = new();

    private static readonly (string Family, string Regular, string Bold)[] Candidates =
    [
        ("segoe ui", "segoeui.ttf", "segoeuib.ttf"),
        ("arial", "arial.ttf", "arialbd.ttf"),
        ("dejavu sans", "DejaVuSans.ttf", "DejaVuSans-Bold.ttf"),
    ];

    private static readonly string[] Folders =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts"),
        "/usr/share/fonts/truetype/dejavu",
    ];

    private static readonly object Gate = new();

    public static void Install()
    {
        lock (Gate)
            GlobalFontSettings.FontResolver ??= new ReportFontResolver();
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        foreach (var (family, regular, bold) in Candidates)
            if (Find(isBold ? bold : regular) is not null) return new FontResolverInfo($"{family}|{(isBold ? "b" : "r")}");
        return null;
    }

    public byte[]? GetFont(string faceName) => Cache.GetOrAdd(faceName, name =>
    {
        var parts = name.Split('|');
        var bold = parts.Length > 1 && parts[1] == "b";
        foreach (var (family, regular, boldFile) in Candidates)
            if (family == parts[0] && (Find(bold ? boldFile : regular) ?? Find(regular)) is { } path) return File.ReadAllBytes(path);
        return [];
    });

    private static string? Find(string file) =>
        Folders.Select(f => Path.Combine(f, file)).FirstOrDefault(File.Exists);
}
