// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion.WordHtml;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion;

/// <summary>How tracked changes appear in the HTML of <see cref="WordToHtml"/>.</summary>
public enum WordHtmlRevisions
{
    /// <summary>As if every change were accepted: insertions read as plain text, deletions are left out.</summary>
    Accepted,

    /// <summary>Insertions in <c>ins</c> elements, deletions in <c>del</c> elements.</summary>
    Marked,
}

/// <summary>Options of <see cref="WordToHtml"/>.</summary>
public sealed record WordHtmlOptions
{
    /// <summary>How tracked changes appear; accepted by default.</summary>
    public WordHtmlRevisions Revisions { get; init; } = WordHtmlRevisions.Accepted;

    /// <summary>The address of a paragraph to highlight (a <see cref="Word.Editing.WordEditableParagraph.Address"/>,
    /// compared ignoring case): its element receives the class <see cref="WordToHtml.HighlightClass"/> and the id
    /// <see cref="WordToHtml.HighlightId"/>.</summary>
    public string? HighlightAddress { get; init; }
}

/// <summary>A converted document: a standalone HTML page and what the conversion only approximates.</summary>
public sealed record WordHtmlResult(string Html, IReadOnlyList<string> Gaps);

/// <summary>
/// Converts a Word document into one standalone HTML page (UTF-8, styles embedded, nothing fetched): each
/// section at its page width with its margins, the default header and footer of the first section as
/// running blocks, headings, paragraphs and runs with their resolved formatting, lists with their computed
/// labels, tables with spans and vertical merges, footnotes and endnotes listed at the end with links both
/// ways, pictures as <c>data:</c> URIs, text boxes as blocks and hyperlinks with safe addresses only. Every
/// paragraph read from a package (alternate-content fallback markup aside) carries <c>data-address</c>, the
/// address <see cref="Word.Editing.WordEditor"/> gives the same paragraph. All document text is encoded:
/// nothing in the document can add markup or script. What the page only approximates is listed in
/// <see cref="WordHtmlResult.Gaps"/>.
/// </summary>
public static class WordToHtml
{
    /// <summary>The class of the paragraph named by <see cref="WordHtmlOptions.HighlightAddress"/>.</summary>
    public const string HighlightClass = "omni-highlight";

    /// <summary>The id of the paragraph named by <see cref="WordHtmlOptions.HighlightAddress"/>.</summary>
    public const string HighlightId = "omni-highlight";

    /// <summary>
    /// Converts a loaded document. Paragraphs carry the address they were read from
    /// (<see cref="WordParagraph.SourceAddress"/>); a paragraph built in code has none, which is reported in
    /// the gaps. Convert the saved bytes (<see cref="WordDocument.ToArray"/>) to address every paragraph.
    /// </summary>
    public static WordHtmlResult Convert(WordDocument document, WordHtmlOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var context = new HtmlContext(document, options ?? new WordHtmlOptions());
        var html = new HtmlPageWriter(context).Write();
        var gaps = document.Gaps.Concat(context.Gaps).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return new WordHtmlResult(html, gaps);
    }

    /// <summary>Loads a .docx package and converts it; every paragraph is addressed.</summary>
    public static WordHtmlResult Convert(byte[] docx, WordHtmlOptions? options = null) => Convert(WordDocument.Load(docx), options);
}
