using System.Globalization;
using Learning.Core.Reports;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.UniversalAccessibility;
using QRCoder;

namespace Learning.Reports.Pdf;

/// <summary>What the program says about itself on its reports (the renderer is shared by the learning programs).</summary>
/// <param name="HowToCheck">Where an instructor checks a report, in that program's words.</param>
/// <param name="TimeNote">How time on task is counted.</param>
public sealed record ReportWords(string HowToCheck, string TimeNote);

/// <summary>
/// Draws a progress report (plan 8.3, spec 9.2): cover, summary, exercise detail, outcomes, program evidence (optional) and
/// verification, as a tagged PDF (headings, paragraphs, tables and figures a screen reader can follow) written as PDF/A.
/// The file carries the signed snapshot and a placeholder for the seal: <see cref="ReportSeal.Seal"/> signs it after.
/// </summary>
public static class ReportPdf
{
    /// <param name="picture">A picture (PNG, no transparency) of a program that passed, or null to leave it out.</param>
    public static byte[] Render(ProgressSnapshot report, byte[] publicKey, ReportWords words, Func<ProgramEvidence, byte[]?>? picture = null)
    {
        ReportFontResolver.Install();
        var doc = new PdfDocument();
        doc.Info.Title = $"{report.App} progress report: {report.PointTitle}, {report.Learner.Name}";
        doc.Info.Author = report.Learner.Name;
        doc.Info.Subject = $"{report.ContentTitle}, {report.PointTitle}";
        doc.Info.Creator = $"{report.App} {report.AppVersion}";
        doc.Info.Elements.SetString(ReportSeal.ReportEntry, ReportSeal.Encode(report.ToJson()));
        doc.Info.Elements.SetString(ReportSeal.KeyEntry, Base64Url(publicKey));
        doc.Info.Elements.SetString(ReportSeal.SealEntry, ReportSeal.Placeholder);
        doc.Options.CompressContentStreams = true;
        doc.SetPdfA();
        doc.ViewerPreferences.DisplayDocTitle = true;

        var w = new Writer(doc, report);
        Cover(w, report);
        Summary(w, report, words);
        Exercises(w, report);
        Outcomes(w, report);
        Evidence(w, report, picture);
        Verification(w, report, words);
        w.Finish();

        using var stream = new MemoryStream();
        doc.Save(stream, false);
        return stream.ToArray();
    }

    private static string Base64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string When(DateTimeOffset t) => t.ToLocalTime().ToString("d MMMM yyyy, HH:mm", CultureInfo.CurrentCulture);
    private static string Day(DateTimeOffset t) => t.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture);

    public static string Duration(TimeSpan t) =>
        t.TotalMinutes < 1 ? (t.TotalSeconds < 1 ? "none recorded" : "under a minute")
        : t.TotalHours < 1 ? $"{(int)t.TotalMinutes} min"
        : $"{(int)t.TotalHours} h {t.Minutes:00} min";

    private static string Score(double? score, double? max) =>
        score is { } s && max is { } m ? $"{s.ToString("0.#", CultureInfo.CurrentCulture)} of {m.ToString("0.#", CultureInfo.CurrentCulture)}" : "–";

    // ---- The sections ------------------------------------------------------------------------------------------

    private static void Cover(Writer w, ProgressSnapshot r)
    {
        w.Heading(1, $"{r.App} progress report");
        w.Paragraph(r.PointTitle, w.Fonts.Large);
        w.Space(4);
        w.StatusBox(r.Status == ReportStatus.Complete
            ? ("Complete", "Every item this report covers is complete.", true)
            : ("Incomplete", "Not every item is complete yet: what is missing is listed below. The instructor decides whether to accept it.", false));
        var rows = new List<(string, string)> { ("Learner", r.Learner.Name) };
        if (r.Learner.StudentId is { } id) rows.Add(("Student ID", id));
        if (r.Learner.Course is { } c) rows.Add(("Course", c));
        if (r.Learner.Section is { } s) rows.Add(("Section", s));
        if (r.Learner.Email is { } e) rows.Add(("E-mail", e));
        rows.Add(("Submission point", r.PointTitle));
        rows.Add(("Status", r.Status == ReportStatus.Complete ? "Complete" : "Incomplete"));
        rows.Add(("Made", When(r.Generated)));
        rows.Add(("Report ID", r.ReportId));
        rows.Add(("Course content", $"{r.ContentTitle}, version {r.ContentVersion}"));
        w.KeyValues(rows);
        if (r.Missing.Count > 0)
        {
            w.Heading(2, "What is missing");
            w.List(r.Missing);
        }
    }

    private static void Summary(Writer w, ProgressSnapshot r, ReportWords words)
    {
        var s = r.Summary;
        w.Heading(2, "Summary");
        w.KeyValues(
        [
            ("Complete", $"{Math.Round(s.PercentComplete)}%: {s.ItemsComplete} of {s.Items} items"),
            ("Outcomes mastered", s.Outcomes == 0 ? "none listed" : $"{s.OutcomesMastered} of {s.Outcomes}"),
            ("Time on task", $"{Duration(s.Time)}. {words.TimeNote}"),
            ("Overall score", s.OverallScore is { } o
                ? $"{Math.Round(o)}%: the average of the best score on each of the {s.ScoredItems} exercises and sets of questions; one not tried counts as 0"
                : "no exercises or questions in this report"),
        ]);
    }

    private static void Exercises(Writer w, ProgressSnapshot r)
    {
        w.Heading(2, "Exercise detail");
        w.Paragraph("Each item the report covers: its status, the best score, how many times it was checked (attempts), the time spent on it, " +
                    "the most hints shown in one attempt, and the date it was completed. The tests of each exercise follow the table; " +
                    "a hidden test is named only by its number.", w.Fonts.Small);
        w.Table(["Item", "Kind", "Status", "Best score", "Attempts", "Time", "Hints", "Completed"],
            [130, 62, 58, 52, 50, 50, 32, 70],
            r.Exercises.Select(e => (IReadOnlyList<string>)
            [
                $"{e.Number} {e.Title}", e.Kind, e.Status, Score(e.BestScore, e.MaxScore),
                e.Kind == ExerciseKinds.Reading ? "–" : e.Attempts.ToString(CultureInfo.CurrentCulture),
                e.Time > TimeSpan.Zero ? Duration(e.Time) : "–",
                e.Kind == ExerciseKinds.Exercise ? e.HintsUsed.ToString(CultureInfo.CurrentCulture) : "–",
                e.Completed is { } d ? Day(d) : "–",
            ]).ToList());
        var withTests = r.Exercises.Where(e => e.Tests.Count > 0).ToList();
        if (withTests.Count == 0) return;
        w.Heading(3, "Tests, from the best attempt");
        foreach (var e in withTests)
            w.Paragraph($"{e.Number} {e.Title}: " + string.Join("; ", e.Tests.Select(t => $"{(t.Passed ? "pass" : "FAIL")}, {t.Name}")) + ".", w.Fonts.Body);
    }

    private static void Outcomes(Writer w, ProgressSnapshot r)
    {
        if (r.Outcomes.Count == 0) return;
        w.Heading(2, "Learning outcomes");
        w.Paragraph("Mastered: every item that teaches the outcome is complete, and the latest answer to every question on it was right. " +
                    "Review suggested: the work was done, but a later answer or check went wrong.", w.Fonts.Small);
        w.Table(["Module", "Outcome", "State"], [62, 342, 100],
            r.Outcomes.Select(o => (IReadOnlyList<string>)[o.Module, o.Text, o.State]).ToList());
    }

    private static void Evidence(Writer w, ProgressSnapshot r, Func<ProgramEvidence, byte[]?>? picture)
    {
        if (r.Evidence.Count == 0) return;
        w.Heading(2, "Program evidence");
        w.Paragraph("The programs that passed, as the learner wrote them. The fingerprint is the SHA-256 of the program that was graded; " +
                    "the program itself is inside this file, so it can be run through the same grader again.", w.Fonts.Small);
        foreach (var e in r.Evidence)
        {
            var png = picture?.Invoke(e);
            // The heading, its line and the picture stay together on one page.
            w.Heading(3, e.Title, keepWith: 40 + (png is null ? 0 : w.FigureHeight(png, 440)));
            w.Paragraph($"Passed {When(e.PassedAt)}. Program fingerprint (SHA-256): {e.WorkHash}", w.Fonts.Small);
            if (png is not null)
                w.Figure(png, $"The ladder program for {e.Title}, as the learner wrote it", 440);
        }
    }

    private static void Verification(Writer w, ProgressSnapshot r, ReportWords words)
    {
        w.Heading(2, "Verification", keepWith: 260);
        var code = r.VerificationCode();
        w.KeyValues(
        [
            ("Report ID", r.ReportId),
            ("Verification code", code),
            ("Computer key", r.ComputerKey ?? "–"),
            ("Class code", r.ClassSigned ? "Signed with the class code the learner entered" : "No class code entered"),
            ("Made with", $"{r.App} {r.AppVersion}"),
            ("Content", $"{r.ContentTitle} ({r.ContentId}), version {r.ContentVersion}"),
        ]);
        using var qr = new QRCodeGenerator().CreateQrCode($"{r.App} report {r.ReportId} verification {code}", QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(qr).GetGraphic(6, [0, 0, 0], [255, 255, 255], drawQuietZones: true);
        w.Figure(png, $"QR code holding the report ID {r.ReportId} and the verification code {code}", 96);
        w.Paragraph("This report is signed. " + words.HowToCheck + " The check shows any change made to this file after it was saved, " +
                    "and whether it was signed with the instructor's class code. The verification code identifies this report, to quote it; " +
                    "the computer key names the computer it was made on.", w.Fonts.Body);
        w.Paragraph("Its limits: the program runs on the learner's own computer, so the signature proves the file was not changed after it was " +
                    "made, not that nobody tampered with that computer. For a high-stakes assessment, use the lab or a proctored challenge.", w.Fonts.Small);
    }

    // ---- Drawing, with tags ------------------------------------------------------------------------------------

    internal sealed class FontSet
    {
        public XFont H1 { get; } = new("Segoe UI", 20, XFontStyleEx.Bold);
        public XFont H2 { get; } = new("Segoe UI", 14, XFontStyleEx.Bold);
        public XFont H3 { get; } = new("Segoe UI", 11, XFontStyleEx.Bold);
        public XFont Large { get; } = new("Segoe UI", 13);
        public XFont Body { get; } = new("Segoe UI", 10);
        public XFont Bold { get; } = new("Segoe UI", 10, XFontStyleEx.Bold);
        public XFont Small { get; } = new("Segoe UI", 8.5);
        public XFont SmallBold { get; } = new("Segoe UI", 8.5, XFontStyleEx.Bold);
    }

    private sealed class Writer
    {
        private const double Margin = 54, Top = 66, Bottom = 60;
        private readonly PdfDocument _doc;
        private readonly ProgressSnapshot _report;
        private readonly StructureBuilder _sb;
        private PdfPage _page = null!;
        private XGraphics _gfx = null!;
        private double _y;
        private int _pages;

        public FontSet Fonts { get; } = new();
        private static readonly XColor Ink = XColor.FromArgb(0x1F, 0x23, 0x28);
        private static readonly XColor Muted = XColor.FromArgb(0x55, 0x5D, 0x66);
        private static readonly XColor Line = XColor.FromArgb(0xC8, 0xCE, 0xD4);

        public Writer(PdfDocument doc, ProgressSnapshot report)
        {
            _doc = doc;
            _report = report;
            var ua = UAManager.ForDocument(doc);
            ua.SetDocumentLanguage("en-US");
            _sb = ua.StructureBuilder;
            NewPage();
            _sb.BeginElement(PdfGroupingElementTag.Document);
        }

        private double Width => _page.Width.Point - 2 * Margin;
        private double Limit => _page.Height.Point - Bottom;

        private void NewPage()
        {
            _gfx?.Dispose();
            _page = _doc.AddPage();
            _page.Size = PageSize.Letter;
            _gfx = XGraphics.FromPdfPage(_page);
            _pages++;
            _y = Top;
            // The running header and footer are artifacts: a screen reader skips them.
            _sb.BeginArtifact();
            _gfx.DrawString($"{_report.App} progress report · {_report.Learner.Name} · {_report.PointTitle}", Fonts.Small, new XSolidBrush(Muted), new XPoint(Margin, 34), XStringFormats.TopLeft);
            _gfx.DrawString($"Report {_report.ReportId} · page {_pages}", Fonts.Small, new XSolidBrush(Muted),
                new XPoint(_page.Width.Point - Margin, _page.Height.Point - 36), XStringFormats.TopRight);
            _gfx.DrawLine(new XPen(Line, 0.6), Margin, 48, _page.Width.Point - Margin, 48);
            _sb.End();
        }

        private void Ensure(double height)
        {
            if (_y + height > Limit && _y > Top) NewPage();
        }

        public void Space(double points) => _y += points;

        public void Finish()
        {
            _sb.End();
            _gfx.Dispose();
        }

        public void Heading(int level, string text, double keepWith = 40)
        {
            var font = level == 1 ? Fonts.H1 : level == 2 ? Fonts.H2 : Fonts.H3;
            _y += level == 1 ? 0 : level == 2 ? 14 : 8;
            var lines = Wrap(text, font, Width);
            Ensure(lines.Count * font.Height * 1.15 + keepWith);
            _sb.BeginBlockLevelElement("H" + level);
            foreach (var line in lines)
            {
                _gfx.DrawString(line, font, new XSolidBrush(Ink), new XPoint(Margin, _y), XStringFormats.TopLeft);
                _y += font.Height * 1.15;
            }
            _sb.End();
            _y += 3;
        }

        public void Paragraph(string text, XFont font)
        {
            var lines = Wrap(text, font, Width);
            var lineHeight = font.Height * 1.2;
            var start = 0;
            while (start < lines.Count)
            {
                Ensure(lineHeight * Math.Min(2, lines.Count - start));
                // As many lines as fit on this page, as one paragraph; the rest carry on as another on the next page.
                var fit = Math.Max(1, (int)((Limit - _y) / lineHeight));
                var take = Math.Min(fit, lines.Count - start);
                _sb.BeginElement(PdfBlockLevelElementTag.Paragraph);
                for (var i = start; i < start + take; i++)
                {
                    _gfx.DrawString(lines[i], font, new XSolidBrush(font == Fonts.Small ? Muted : Ink), new XPoint(Margin, _y), XStringFormats.TopLeft);
                    _y += lineHeight;
                }
                _sb.End();
                start += take;
            }
            _y += 4;
        }

        public void List(IReadOnlyList<string> entries)
        {
            _sb.BeginElement(PdfBlockLevelElementTag.List);
            foreach (var entry in entries)
            {
                var lines = Wrap(entry, Fonts.Body, Width - 14);
                var lh = Fonts.Body.Height * 1.2;
                Ensure(lines.Count * lh);
                _sb.BeginElement(PdfBlockLevelElementTag.ListItem);
                _sb.BeginElement(PdfBlockLevelElementTag.Label);
                _gfx.DrawString("•", Fonts.Body, new XSolidBrush(Ink), new XPoint(Margin + 2, _y), XStringFormats.TopLeft);
                _sb.End();
                _sb.BeginElement(PdfBlockLevelElementTag.ListBody);
                foreach (var line in lines)
                {
                    _gfx.DrawString(line, Fonts.Body, new XSolidBrush(Ink), new XPoint(Margin + 14, _y), XStringFormats.TopLeft);
                    _y += lh;
                }
                _sb.End();
                _sb.End();
            }
            _sb.End();
            _y += 4;
        }

        public void StatusBox((string Title, string Text, bool Good) status)
        {
            var lines = Wrap(status.Text, Fonts.Body, Width - 24);
            var height = 14 + Fonts.H2.Height * 1.2 + lines.Count * Fonts.Body.Height * 1.2 + 8;
            Ensure(height);
            _sb.BeginArtifact();
            _gfx.DrawRectangle(new XPen(status.Good ? XColor.FromArgb(0x2E, 0x7D, 0x32) : XColor.FromArgb(0xB2, 0x6A, 0x00), 1.2),
                new XSolidBrush(status.Good ? XColor.FromArgb(0xE8, 0xF5, 0xE9) : XColor.FromArgb(0xFF, 0xF4, 0xE0)), Margin, _y, Width, height);
            _sb.End();
            var y = _y + 8;
            _sb.BeginElement(PdfBlockLevelElementTag.Paragraph);
            _gfx.DrawString(status.Title, Fonts.H2, new XSolidBrush(Ink), new XPoint(Margin + 12, y), XStringFormats.TopLeft);
            y += Fonts.H2.Height * 1.2;
            foreach (var line in lines)
            {
                _gfx.DrawString(line, Fonts.Body, new XSolidBrush(Ink), new XPoint(Margin + 12, y), XStringFormats.TopLeft);
                y += Fonts.Body.Height * 1.2;
            }
            _sb.End();
            _y += height + 10;
        }

        /// <summary>Two columns, the names as row headers: a table to a screen reader.</summary>
        public void KeyValues(IReadOnlyList<(string Key, string Value)> rows)
        {
            const double keyWidth = 130;
            var lh = Fonts.Body.Height * 1.25;
            var open = false;
            foreach (var (key, value) in rows)
            {
                var lines = Wrap(value, Fonts.Body, Width - keyWidth - 8);
                var height = Math.Max(1, lines.Count) * lh + 4;
                if (_y + height > Limit && _y > Top)
                {
                    if (open) _sb.End();
                    NewPage();
                    open = false;
                }
                if (!open) { _sb.BeginElement(PdfBlockLevelElementTag.Table); open = true; }
                _sb.BeginElement(PdfBlockLevelElementTag.TableRow);
                _sb.BeginElement(PdfBlockLevelElementTag.TableHeaderCell);
                _gfx.DrawString(key, Fonts.Bold, new XSolidBrush(Ink), new XPoint(Margin, _y + 2), XStringFormats.TopLeft);
                _sb.End();
                _sb.BeginElement(PdfBlockLevelElementTag.TableDataCell);
                var y = _y + 2;
                foreach (var line in lines)
                {
                    _gfx.DrawString(line, Fonts.Body, new XSolidBrush(Ink), new XPoint(Margin + keyWidth, y), XStringFormats.TopLeft);
                    y += lh;
                }
                _sb.End();
                _sb.End();
                _sb.BeginArtifact();
                _gfx.DrawLine(new XPen(Line, 0.5), Margin, _y + height, Margin + Width, _y + height);
                _sb.End();
                _y += height;
            }
            if (open) _sb.End();
            _y += 6;
        }

        /// <summary>A table with a header row, repeated at the top of each page it runs onto.</summary>
        public void Table(IReadOnlyList<string> headers, IReadOnlyList<double> widths, IReadOnlyList<IReadOnlyList<string>> rows)
        {
            var scale = Width / widths.Sum();
            var cols = widths.Select(x => x * scale).ToArray();
            var lh = Fonts.Small.Height * 1.2;
            var open = false;

            void HeaderRow()
            {
                var cells = headers.Select((h, i) => Wrap(h, Fonts.SmallBold, cols[i] - 6)).ToList();
                var height = cells.Max(c => c.Count) * lh + 6;
                _sb.BeginElement(PdfBlockLevelElementTag.TableRow);
                var x = Margin;
                for (var i = 0; i < headers.Count; i++)
                {
                    _sb.BeginElement(PdfBlockLevelElementTag.TableHeaderCell);
                    var y = _y + 3;
                    foreach (var line in cells[i]) { _gfx.DrawString(line, Fonts.SmallBold, new XSolidBrush(Ink), new XPoint(x + 3, y), XStringFormats.TopLeft); y += lh; }
                    _sb.End();
                    x += cols[i];
                }
                _sb.End();
                _sb.BeginArtifact();
                _gfx.DrawLine(new XPen(Ink, 0.8), Margin, _y + height, Margin + Width, _y + height);
                _sb.End();
                _y += height;
            }

            foreach (var row in rows)
            {
                var cells = row.Select((c, i) => Wrap(c, Fonts.Small, cols[i] - 6)).ToList();
                var height = cells.Max(c => c.Count) * lh + 6;
                if (!open || (_y + height > Limit && _y > Top))
                {
                    if (open) { _sb.End(); NewPage(); }
                    else Ensure(height + 3 * lh + 6);
                    _sb.BeginElement(PdfBlockLevelElementTag.Table);
                    open = true;
                    HeaderRow();
                }
                _sb.BeginElement(PdfBlockLevelElementTag.TableRow);
                var x = Margin;
                for (var i = 0; i < row.Count; i++)
                {
                    _sb.BeginElement(PdfBlockLevelElementTag.TableDataCell);
                    var y = _y + 3;
                    foreach (var line in cells[i]) { _gfx.DrawString(line, Fonts.Small, new XSolidBrush(Ink), new XPoint(x + 3, y), XStringFormats.TopLeft); y += lh; }
                    _sb.End();
                    x += cols[i];
                }
                _sb.End();
                _sb.BeginArtifact();
                _gfx.DrawLine(new XPen(Line, 0.5), Margin, _y + height, Margin + Width, _y + height);
                _sb.End();
                _y += height;
            }
            if (open) _sb.End();
            _y += 8;
        }

        /// <summary>How tall a picture will be on the page (at most a page, less a heading).</summary>
        public double FigureHeight(byte[] png, double maxWidth)
        {
            using var image = XImage.FromStream(new MemoryStream(png));
            return Size(image, maxWidth).H + 6;
        }

        private (double W, double H) Size(XImage image, double maxWidth)
        {
            var w = Math.Min(maxWidth, Width);
            var h = w * image.PixelHeight / image.PixelWidth;
            var room = Limit - Top - 80;
            if (h > room) { w *= room / h; h = room; }
            return (w, h);
        }

        /// <summary>A picture with its words for a screen reader, scaled to fit the page.</summary>
        public void Figure(byte[] png, string alt, double maxWidth)
        {
            using var image = XImage.FromStream(new MemoryStream(png));
            var (w, h) = Size(image, maxWidth);
            Ensure(h + 6);
            var rect = new XRect(Margin, _y, w, h);
            _sb.BeginElement(PdfIllustrationElementTag.Figure, alt, rect);
            _gfx.DrawImage(image, rect);
            _sb.End();
            _y += h + 10;
        }

        private List<string> Wrap(string text, XFont font, double width)
        {
            var lines = new List<string>();
            foreach (var para in text.Replace("\r", "").Split('\n'))
            {
                var line = "";
                foreach (var word in para.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var tryLine = line.Length == 0 ? word : line + " " + word;
                    if (_gfx.MeasureString(tryLine, font).Width <= width) { line = tryLine; continue; }
                    if (line.Length > 0) lines.Add(line);
                    // A word wider than the column (a long fingerprint): break it where it must.
                    var rest = word;
                    while (_gfx.MeasureString(rest, font).Width > width && rest.Length > 1)
                    {
                        var n = rest.Length - 1;
                        while (n > 1 && _gfx.MeasureString(rest[..n], font).Width > width) n--;
                        lines.Add(rest[..n]);
                        rest = rest[n..];
                    }
                    line = rest;
                }
                lines.Add(line);
            }
            return lines;
        }
    }
}
