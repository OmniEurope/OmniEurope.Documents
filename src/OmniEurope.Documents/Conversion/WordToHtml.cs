// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion.WordHtml;
using OmniEurope.Documents.Internal;
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

    /// <summary>
    /// When true, each content control (<c>w:sdt</c>) is wrapped: an inline control in a <c>span</c>, a block
    /// control in a <c>div</c> around its paragraphs and tables. The wrapper carries <c>data-oe-field</c> (the tag,
    /// or <c>data-oe-field-id</c> with the control id when there is no tag), <c>data-oe-field-kind</c> (the lowercase
    /// <see cref="Word.Editing.WordContentControlKind"/> name), <c>data-oe-field-alias</c> when the control has a
    /// title and <c>data-oe-placeholder="true"</c> while it shows its placeholder. Row and cell level controls are
    /// not wrapped, nor is a control without content. False by default: the output is then unchanged.
    /// </summary>
    public bool MarkContentControls { get; init; }

    /// <summary>
    /// The address of the style sheet when the consumer serves it as a file (its text is
    /// <see cref="WordHtmlResult.StyleSheet"/>): the page then links it with <c>&lt;link rel="stylesheet"&gt;</c>
    /// and embeds no <c>style</c> element, which a Content-Security-Policy <c>style-src 'self'</c> allows. Null by
    /// default: the sheet is embedded.
    /// </summary>
    public string? StyleSheetHref { get; init; }

    /// <summary>
    /// The nonce written on the <c>style</c> element (or on the <c>link</c> element of <see cref="StyleSheetHref"/>),
    /// for a Content-Security-Policy <c>style-src 'nonce-…'</c>. Only a CSP <c>base64-value</c> is accepted: ASCII
    /// letters, digits, <c>+ / - _</c>, then at most two <c>=</c>; anything else throws
    /// <see cref="ArgumentException"/>. Null by default: no nonce.
    /// </summary>
    public string? StyleSheetNonce { get; init => field = HtmlPageStyle.CheckNonce(value); }

    /// <summary>
    /// When set, pictures are not embedded as <c>data:</c> URIs: each one becomes a separate resource of
    /// <see cref="WordHtmlResult.Images"/>, named <c>image1.png</c>, <c>image2.jpg</c>... in order of first
    /// appearance, and its <c>img</c> element takes this prefix followed by the name as <c>src</c>
    /// (<c>"/documents/42/images/"</c> gives <c>/documents/42/images/image1.png</c>), which a
    /// Content-Security-Policy <c>img-src 'self'</c> allows when the consumer serves them. Null by default: the
    /// page embeds its pictures and stays standalone.
    /// </summary>
    public string? ImageUrlPrefix { get; init; }
}

/// <summary>A picture of the page served apart from it (<see cref="WordHtmlOptions.ImageUrlPrefix"/>): its file name,
/// the one its <c>src</c> ends with, its media type (<c>image/png</c>...) and its bytes.</summary>
public sealed record WordHtmlImage(string Name, string MediaType, byte[] Data);

/// <summary>A converted document: a standalone HTML page and what the conversion only approximates.</summary>
public sealed record WordHtmlResult(string Html, IReadOnlyList<string> Gaps)
{
    /// <summary>The style sheet of the page, the one it embeds or links (<see cref="WordHtmlOptions.StyleSheetHref"/>).</summary>
    public string StyleSheet { get; init; } = string.Empty;

    /// <summary>The pictures to serve at <see cref="WordHtmlOptions.ImageUrlPrefix"/>; empty when the page embeds
    /// them.</summary>
    public IReadOnlyList<WordHtmlImage> Images { get; init; } = [];

    /// <summary>The <see cref="Gaps"/> in their order, each with its code, to describe them in another language
    /// (<see cref="DocumentGap.Describe"/>).</summary>
    public IReadOnlyList<DocumentGap> GapDetails { get; init; } = Gaps.Select(DocumentGap.FromText).ToList();
}

/// <summary>
/// Converts a Word document into one HTML page (UTF-8, nothing fetched unless asked): each
/// section at its page width with its margins, the default header and footer of the first section as
/// running blocks, headings, paragraphs and runs with their resolved formatting, lists with their computed
/// labels, tables with spans and vertical merges, footnotes and endnotes listed at the end with links both
/// ways, pictures as <c>data:</c> URIs (or separate resources, <see cref="WordHtmlOptions.ImageUrlPrefix"/>),
/// text boxes as blocks and hyperlinks with safe addresses only. The page has no <c>style</c> attribute: the
/// formatting of each element is a class of one style sheet (identical formatting shares a class), embedded or
/// linked (<see cref="WordHtmlOptions.StyleSheetHref"/>), so it shows under a Content-Security-Policy without
/// <c>'unsafe-inline'</c>. Every
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
        var (html, css) = new HtmlPageWriter(context).Write();
        var gaps = new GapSet(document.GapDetails);
        gaps.UnionWith(context.Gaps);
        return new WordHtmlResult(html, gaps.Messages) { StyleSheet = css, Images = context.Images.Resources, GapDetails = gaps.ToList() };
    }

    /// <summary>Loads a .docx package and converts it; every paragraph is addressed.</summary>
    public static WordHtmlResult Convert(byte[] docx, WordHtmlOptions? options = null) => Convert(WordDocument.Load(docx), options);
}
