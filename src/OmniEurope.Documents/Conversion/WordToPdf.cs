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
}

/// <summary>A converted document: the PDF, its page count and what the conversion could not render faithfully.</summary>
public sealed record WordPdfResult(byte[] Pdf, int PageCount, IReadOnlyList<string> Gaps);

/// <summary>
/// Lays a Word document out into pages and writes it as PDF: styles and numbering resolved, lines broken
/// and justified, tabs, pagination with keep rules and widow control, sections and columns, tables,
/// headers and footers with page fields, footnotes and endnotes, pictures (EMF included) and text boxes.
/// What it renders approximately (substituted fonts, floating shapes without text wrapping...) is listed
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
        };
        var context = new LayoutContext(document, builder);
        var pages = Paginate(context);
        ReportFloatingShapes(document, context);
        new PagePainter(context, options.Bookmarks).Paint(pages);
        var gaps = document.Gaps.Concat(context.Gaps).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return new WordPdfResult(builder.ToArray(), pages.Count, gaps);
    }

    // Footnotes restarting at each page are numbered after the pages of the previous layout, until the
    // numbers no longer move a footnote to another page.
    private static List<PageFrame> Paginate(LayoutContext context)
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

    private static void ReportFloatingShapes(WordDocument document, LayoutContext context)
    {
        var floating = document.Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordShape>()
            .Any(s => s.Floating is { Wrap: not WordWrap.None, BehindText: false });
        if (floating)
        {
            context.Gaps.Add("text does not flow around floating shapes");
        }

        if (document.Blocks.OfType<WordParagraph>().Any(p => p.Properties.RightToLeft == true))
        {
            context.Gaps.Add("right-to-left paragraphs laid out left to right");
        }

        if (document.Sections.Any(s => s.Page.ColumnWidths is { Count: > 1 } widths && widths.Distinct().Count() > 1))
        {
            context.Gaps.Add("unequal columns laid out at the first column's width");
        }
    }
}
