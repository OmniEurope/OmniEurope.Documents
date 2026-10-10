// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion.WordLayout;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Pdf.Writing;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion;

/// <summary>Options of <see cref="WordToPdf"/>.</summary>
public sealed record WordPdfOptions
{
    /// <summary>Fonts to draw with; the bundled fonts by default (Arial, Calibri, Times New Roman... map to
    /// metric-compatible faces). Register the document's own fonts here for an exact rendering.</summary>
    public FontLibrary? Fonts { get; init; }

    /// <summary>Adds an outline entry for each heading (paragraph with an outline level).</summary>
    public bool Bookmarks { get; init; } = true;

    /// <summary>PDF/A-2b or PDF/A-2u output (<see cref="PdfDocumentBuilder.Conformance"/>); a plain PDF by default.</summary>
    public PdfConformance Conformance { get; init; }
}

/// <summary>A converted document: the PDF, its page count and what the conversion could not render faithfully.</summary>
public sealed record WordPdfResult(byte[] Pdf, int PageCount, IReadOnlyList<string> Gaps);

/// <summary>
/// Lays a Word document out into pages and writes it as PDF: styles and numbering resolved, lines broken
/// and justified, tabs, pagination with keep rules and widow control, sections and columns, tables,
/// headers and footers with page fields, footnotes and endnotes, pictures (EMF included) and text boxes.
/// Text flows around floating shapes, right-to-left paragraphs and mixed text follow the Unicode Bidirectional
/// Algorithm, columns may have unequal widths. What it renders approximately (substituted fonts, Arabic without joining
/// forms...) is listed
/// in <see cref="WordPdfResult.Gaps"/>.
/// </summary>
public static class WordToPdf
{
    private const int MaxPasses = 4;

    /// <summary>Converts a loaded document.</summary>
    public static WordPdfResult Convert(WordDocument document, WordPdfOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= new WordPdfOptions();
        var information = document.Information;
        var builder = new PdfDocumentBuilder
        {
            Fonts = options.Fonts ?? FontLibrary.Default,
            Title = information.Title,
            Author = information.Author,
            Subject = information.Subject,
            Keywords = information.Keywords,
            Creator = "OmniEurope.Documents",
            Conformance = options.Conformance,
        };
        var context = new LayoutContext(document, builder);
        var pages = Paginate(context);
        ReportFloatingShapes(document, context);
        new PagePainter(context, options.Bookmarks).Paint(pages);
        if (builder.OmittedCharacters > 0)
        {
            context.Gaps.Add("characters without a glyph in any font left out (PDF/A forbids the .notdef glyph)");
        }

        var gaps = document.Gaps.Concat(context.Gaps).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return new WordPdfResult(builder.ToArray(), pages.Count, gaps);
    }

    // Footnotes restarting at each page are numbered after the pages of the previous layout, until the
    // numbers no longer move a footnote to another page.
    internal static List<PageFrame> Paginate(LayoutContext context)
    {
        var pages = new Paginator(context, new BlockLayout(context)).Run();
        for (var pass = 1; context.Restart(pages); pass++)
        {
            if (pass == MaxPasses)
            {
                context.Gaps.Add("footnote numbers restarting at each page may not match their final page");
                break;
            }

            pages = new Paginator(context, new BlockLayout(context)).Run();
        }

        return pages;
    }

    /// <summary>Loads a .docx package and converts it.</summary>
    public static WordPdfResult Convert(byte[] docx, WordPdfOptions? options = null) => Convert(WordDocument.Load(docx), options);

    // What the layout of floating shapes, right-to-left text and columns leaves approximate.
    private static void ReportFloatingShapes(WordDocument document, LayoutContext context)
    {
        var paragraphs = document.Blocks.OfType<WordParagraph>().ToList();
        var wrapping = paragraphs.SelectMany(p => p.Inlines).OfType<WordShape>().Where(s => s.Floating is { Wrap: not WordWrap.None }).ToList();
        if (wrapping.Exists(s => s.Floating!.Wrap is WordWrap.Tight or WordWrap.Through))
        {
            context.Gaps.Add("tight and through wrapping follows the bounding box of floating shapes");
        }

        var elsewhere = document.Blocks.OfType<WordTable>().SelectMany(Shapes)
            .Concat(document.Sections.SelectMany(s => s.Headers.Values.Concat(s.Footers.Values)).SelectMany(h => h.Blocks.OfType<WordParagraph>()).SelectMany(p => p.Inlines).OfType<WordShape>());
        if (elsewhere.Any(s => s.Floating is { Wrap: not WordWrap.None }))
        {
            context.Gaps.Add("text does not flow around floating shapes in tables, headers and footers");
        }

        if (paragraphs.Exists(p => p.Text.Any(c => c is >= '؀' and <= 'ۿ' or >= 'ݐ' and <= 'ࣿ')))
        {
            context.Gaps.Add("Arabic letters drawn without joining forms");
        }
    }

    private static IEnumerable<WordShape> Shapes(WordTable table) =>
        table.Rows.SelectMany(r => r.Cells).SelectMany(c => c.Blocks).SelectMany(b => b switch
        {
            WordParagraph paragraph => paragraph.Inlines.OfType<WordShape>(),
            WordTable inner => Shapes(inner),
            _ => [],
        });
}
