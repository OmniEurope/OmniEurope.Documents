// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion.WordLayout;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Pdf.Writing;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordFields;

/// <summary>
/// Where the paragraphs of a document land when <see cref="WordToPdf"/> lays it out: for each paragraph address
/// (<see cref="WordParagraph.SourceAddress"/>) the page it starts on, as that page's number is displayed (the
/// section's number format and restart), and the page counts of the document and of each section.
/// </summary>
internal sealed class WordPageMap
{
    private readonly Dictionary<string, (string Label, int Section)> _paragraphs = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<int> _sectionPages = [];

    private WordPageMap()
    {
    }

    public int PageCount { get; private set; }

    public static WordPageMap Build(WordDocument document, FontLibrary fonts)
    {
        var context = new LayoutContext(document, new PdfDocumentBuilder { Fonts = fonts });
        var pages = WordToPdf.Paginate(context);
        var map = new WordPageMap { PageCount = pages.Count };
        var sections = new List<WordSection>();
        foreach (var page in pages)
        {
            if (sections.Count == 0 || !ReferenceEquals(sections[^1], page.Section))
            {
                sections.Add(page.Section);
                map._sectionPages.Add(0);
            }

            map._sectionPages[^1]++;
            var label = WordNumbering.FormatNumber(page.PageNumber, page.Setup.PageNumberFormat);
            var items = page.Placements.Select(p => p.Item).Concat(page.Footnotes.SelectMany(n => n.Items));
            foreach (var source in items.SelectMany(Sources))
            {
                map._paragraphs.TryAdd(source, (label, sections.Count - 1));
            }
        }

        return map;
    }

    /// <summary>The displayed number of the page a paragraph starts on, or null when it is not laid out in the body or the notes.</summary>
    public string? PageOf(string address) => _paragraphs.TryGetValue(address, out var page) ? page.Label : null;

    /// <summary>The page count of the section a paragraph starts in, or null.</summary>
    public int? SectionPagesOf(string address) => _paragraphs.TryGetValue(address, out var page) ? _sectionPages[page.Section] : null;

    private static IEnumerable<string> Sources(FlowItem item)
    {
        if (item.Source is { } source)
        {
            yield return source;
        }

        if (item is RowItem row)
        {
            foreach (var nested in row.Cells.SelectMany(c => c.Items).SelectMany(Sources))
            {
                yield return nested;
            }
        }
    }
}
