// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordHtml;

/// <summary>
/// Writes the page: the head with the embedded style sheet, the running header of the first section, each
/// section at its page width and margins, the footnotes then the endnotes in the order of their first
/// reference, and the running footer.
/// </summary>
internal sealed class HtmlPageWriter
{
    private const string StyleSheet =
        "html{background:#ffffff}body{margin:0;color:#000000}" +
        ".omni-document{margin:0 auto;width:max-content;max-width:100%}" +
        ".omni-header,.omni-section,.omni-notes,.omni-footer{box-sizing:border-box;margin:0 auto}" +
        ".omni-header,.omni-section,.omni-notes,.omni-footer,.omni-note,.omni-cell,.omni-textbox{display:flex;flex-direction:column}" +
        ".omni-section.omni-columns{display:block}" +
        "p,h1,h2,h3,h4,h5,h6{margin:0;font-size:inherit;font-weight:inherit;white-space:pre-wrap;overflow-wrap:break-word}" +
        "table.omni-table{border-collapse:collapse}td{vertical-align:top}" +
        ".omni-label{white-space:pre}.omni-page-break{display:block;break-after:page}" +
        ".omni-textbox{box-sizing:border-box;border:0.75pt solid #000000;padding:3.6pt 7.2pt}" +
        ".omni-noteref a{text-decoration:none}.omni-notes hr{width:144pt;margin:6pt 0;border:0;border-top:0.5pt solid #000000}" +
        "tr.omni-row-deleted{text-decoration:line-through}" +
        ".omni-highlight{background-color:#fff2a8 !important;outline:2pt solid #f0b400}" +
        "@media print{.omni-document{width:auto}}";

    private readonly HtmlContext _context;
    private readonly HtmlOutput _output = new();
    private readonly HtmlBlockWriter _blocks;

    public HtmlPageWriter(HtmlContext context)
    {
        _context = context;
        _blocks = new HtmlBlockWriter(context, _output);
    }

    private WordDocument Document => _context.Document;

    public string Write()
    {
        _context.Gaps.Add("line and page breaks are computed by the browser");
        _context.Gaps.Add("line heights are approximated (single spacing taken as 1.15 times the font size)");
        ReportLeftOut();
        Head();
        _output.Raw("<body>\n<div class=\"omni-document\">\n");
        var first = Document.Sections.Count > 0 ? Document.Sections[0] : null;
        Running("header", first, first?.Headers);
        for (var s = 0; s < Document.Sections.Count; s++)
        {
            Section(s);
        }

        Notes(WordNoteKind.Footnote, first);
        Notes(WordNoteKind.Endnote, first);
        Running("footer", first, first?.Footers);
        _output.Raw("</div>\n</body>\n</html>\n");
        return _output.ToString();
    }

    private void Head()
    {
        var language = new string((Document.Styles.DefaultRunProperties.Language ?? string.Empty).Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').ToArray());
        _output.Raw("<!DOCTYPE html>\n");
        _output.Open("html", ("lang", language.Length > 0 ? language : null));
        _output.Raw("\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n<meta name=\"generator\" content=\"OmniEurope.Documents\">\n<title>");
        _output.Text(Document.Information.Title ?? string.Empty);
        _output.Raw("</title>\n<style>");
        _output.Raw(StyleSheet);
        _output.Raw("</style>\n</head>\n");
    }

    private void ReportLeftOut()
    {
        if (Document.Comments.Count > 0)
        {
            _context.Gaps.Add("comments are left out");
        }

        if (Enumerable.Range(0, Document.Sections.Count).Any(s => _context.Notes.Rule(s).Restart == WordNoteRestart.EachPage))
        {
            _context.Gaps.Add("footnote numbering restarting at each page continues, the page has no pages");
        }

        if (Document.Sections.Any(s => s.Page.ColumnWidths is { Count: > 1 } widths && widths.Distinct().Count() > 1))
        {
            _context.Gaps.Add("unequal columns shown as equal columns");
        }

        var first = Document.Sections.Count > 0 ? Document.Sections[0] : null;
        var others = Document.Sections.SelectMany(s => s.Headers.Concat(s.Footers))
            .Any(pair => pair.Key != WordHeaderFooterKind.Default || (!ReferenceEquals(first?.Headers.GetValueOrDefault(pair.Key), pair.Value) && !ReferenceEquals(first?.Footers.GetValueOrDefault(pair.Key), pair.Value)));
        if (others)
        {
            _context.Gaps.Add("headers and footers other than the first section's default ones are left out");
        }
    }

    private void Running(string tag, WordSection? section, Dictionary<WordHeaderFooterKind, WordHeaderFooter>? parts)
    {
        if (section is null || parts?.GetValueOrDefault(WordHeaderFooterKind.Default) is not { } content)
        {
            return;
        }

        var page = section.Page;
        var top = tag == "header" ? page.HeaderDistance : 0;
        var bottom = tag == "footer" ? page.FooterDistance : 0;
        _output.Open(tag, ("class", "omni-" + tag), ("style", Box(page, top, bottom)));
        _output.Line();
        _blocks.Write(content.Blocks, numbering: false);
        _output.Close(tag);
        _output.Line();
    }

    private void Section(int index)
    {
        var section = Document.Sections[index];
        var page = section.Page;
        _context.Notes.Section = index;
        var hasHeader = section.Headers.ContainsKey(WordHeaderFooterKind.Default) && index == 0;
        var hasFooter = section.Footers.ContainsKey(WordHeaderFooterKind.Default) && index == Document.Sections.Count - 1;
        var top = hasHeader ? Math.Max(0, page.MarginTop - page.HeaderDistance) : page.MarginTop;
        var bottom = hasFooter ? Math.Max(0, page.MarginBottom - page.FooterDistance) : page.MarginBottom;
        var style = Box(page, top, bottom);
        if (page.Columns > 1)
        {
            style += ";column-count:" + page.Columns.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";column-gap:" + HtmlCss.Points(page.ColumnSpacing)
                + (page.ColumnSeparator ? ";column-rule:0.5pt solid #000000" : string.Empty);
        }

        if (index > 0 && page.Start != WordSectionStart.Continuous)
        {
            style += ";break-before:page";
        }

        _output.Open("div", ("class", page.Columns > 1 ? "omni-section omni-columns" : "omni-section"), ("style", style));
        _output.Line();
        _blocks.Write(section.Blocks);
        _output.Close("div");
        _output.Line();
    }

    // The page width, with the margins as padding.
    private static string Box(WordPageSetup page, double top, double bottom) =>
        "width:" + HtmlCss.Points(page.Width) + ";padding:" + HtmlCss.Points(top) + " " + HtmlCss.Points(page.MarginRight) + " "
        + HtmlCss.Points(bottom) + " " + HtmlCss.Points(page.MarginLeft + page.Gutter);

    private void Notes(WordNoteKind kind, WordSection? first)
    {
        var (order, notes) = kind == WordNoteKind.Footnote ? (_context.Notes.FootnoteOrder, Document.Footnotes) : (_context.Notes.EndnoteOrder, Document.Endnotes);
        if (first is null || !order.Any(notes.ContainsKey))
        {
            return;
        }

        if (kind == WordNoteKind.Footnote)
        {
            _context.Gaps.Add("footnotes are listed at the end of the page");
        }

        _output.Open("section", ("class", "omni-notes omni-" + (kind == WordNoteKind.Footnote ? "footnotes" : "endnotes")), ("style", Box(first.Page, 0, 0)));
        _output.Raw("\n<hr>\n");

        // A note may reference another one, which is then added to the order: the list grows while it is read.
        for (var i = 0; i < order.Count; i++)
        {
            if (notes.TryGetValue(order[i], out var note))
            {
                Note(kind, note);
            }
        }

        _output.Close("section");
        _output.Line();
    }

    private void Note(WordNoteKind kind, WordNote note)
    {
        _output.Open("div", ("class", "omni-note"), ("id", HtmlContext.NoteId(kind, note.Id, reference: false)));
        _output.Line();
        var marked = note.Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).Any(i => i is WordNoteReference { IsMark: true });
        if (!marked)
        {
            _blocks.StartWithBackLink(kind, note.Id);
        }

        _blocks.Write(note.Blocks, numbering: false);
        _output.Close("div");
        _output.Line();
    }
}
