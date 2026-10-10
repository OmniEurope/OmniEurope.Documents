// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion.WordMarkdown;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion;

/// <summary>Options of <see cref="WordToMarkdown"/>.</summary>
public sealed record WordMarkdownOptions
{
    /// <summary>The folder the picture paths of the Markdown start with (<c>images/image1.png</c>); empty for
    /// none.</summary>
    public string ImageFolder { get; init; } = "images";
}

/// <summary>A picture the Markdown refers to: its relative path and its bytes, as stored in the document.</summary>
public sealed record WordMarkdownImage(string Path, string ContentType, byte[] Data);

/// <summary>A converted document: the Markdown, the pictures it refers to and what the conversion leaves out
/// or only approximates.</summary>
public sealed record WordMarkdownResult(string Markdown, IReadOnlyList<WordMarkdownImage> Images, IReadOnlyList<string> Gaps);

/// <summary>
/// Converts a Word document into GitHub-flavoured Markdown: paragraphs with outline levels 1 to 6 as headings
/// (a numbered heading keeps its label, the bold or italic of its style is not marked), other paragraphs as
/// paragraphs, line breaks as hard breaks, bold, italic and strike as <c>**</c>, <c>*</c> and <c>~~</c>
/// (spaces kept outside the markers; a marker between punctuation and a letter may not read as emphasis), numbered and
/// bulleted paragraphs as nested lists (ordered lists from their counted number), tables as pipe tables whose
/// first row is the header, <c>http</c>, <c>https</c> and <c>mailto</c> links, footnotes and endnotes as
/// footnotes (<c>[^1]</c>, <c>[^e1]</c>) defined at the end, pictures as reference images
/// (<c>![alt][image1]</c>) whose files are returned in <see cref="WordMarkdownResult.Images"/>, text boxes as
/// paragraphs after the one that anchors them. Tracked changes are shown accepted. Text is escaped, so it reads
/// back as the same literal text. Left out or approximated, and listed in <see cref="WordMarkdownResult.Gaps"/>
/// when met: underline, superscript, subscript, fonts, colours and paragraph formatting; headers, footers and
/// comments; merged table cells (their text in the first cell, the others empty), several paragraphs or a nested
/// table in a cell (joined on one line), lists inside cells and notes, page and column breaks, empty paragraphs,
/// bookmarks and links to other schemes, list formats other than numbers and bullets (written as numbers).
/// </summary>
public static class WordToMarkdown
{
    /// <summary>Converts a loaded document.</summary>
    public static WordMarkdownResult Convert(WordDocument document, WordMarkdownOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var context = new MarkdownContext(document, options ?? new WordMarkdownOptions());
        var markdown = new MarkdownDocumentWriter(context).Write();
        var gaps = document.Gaps.Concat(context.Gaps).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return new WordMarkdownResult(markdown, context.Images.All, gaps);
    }

    /// <summary>Loads a .docx package and converts it.</summary>
    public static WordMarkdownResult Convert(byte[] docx, WordMarkdownOptions? options = null) => Convert(WordDocument.Load(docx), options);
}
