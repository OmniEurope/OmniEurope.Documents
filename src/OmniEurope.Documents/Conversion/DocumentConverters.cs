// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion.HtmlLayout;
using OmniEurope.Documents.Html;
using OmniEurope.Documents.Markdown;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion;

/// <summary>
/// HTML to Word: headings, paragraphs, lists, quotes, preformatted text, tables with spans, rules, links,
/// inline formatting and the inline <c>style</c> attribute. Images are read from <c>data:</c> URIs only
/// (nothing is fetched); anything left out is listed in <see cref="WordDocument.Gaps"/>.
/// </summary>
public static class HtmlToWord
{
    /// <summary>Converts an HTML document or fragment into a new Word document (its title becomes the document title).</summary>
    public static WordDocument Convert(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var parsed = HtmlParser.ParseDocument(html);
        var document = new WordDocument();
        if (parsed.Title.Length > 0)
        {
            document.Information = document.Information with { Title = parsed.Title };
        }

        Append(parsed.Body, document);
        return document;
    }

    /// <summary>Appends the content of an HTML tree to the last section of <paramref name="document"/>.</summary>
    public static void Append(HtmlNode root, WordDocument document)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(document);
        new HtmlWordWriter(document).Write(root);
    }
}

/// <summary>HTML to PDF, through <see cref="HtmlToWord"/> and <see cref="WordToPdf"/>.</summary>
public static class HtmlToPdf
{
    /// <summary>Converts HTML into PDF.</summary>
    public static WordPdfResult Convert(string html, WordPdfOptions? options = null) => WordToPdf.Convert(HtmlToWord.Convert(html), options);
}

/// <summary>
/// Markdown to Word, through the HTML renderer (raw HTML in the Markdown stays escaped unless the options
/// allow it). GitHub extensions (tables, strikethrough, task lists, autolinks) are on by default.
/// </summary>
public static class MarkdownToWord
{
    /// <summary>Converts Markdown into a Word document.</summary>
    public static WordDocument Convert(string markdown, MarkdownOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        return HtmlToWord.Convert(MarkdownRenderer.ToHtml(markdown, options ?? MarkdownOptions.GitHub));
    }
}

/// <summary>Markdown to PDF, through <see cref="MarkdownToWord"/> and <see cref="WordToPdf"/>.</summary>
public static class MarkdownToPdf
{
    /// <summary>Converts Markdown into PDF.</summary>
    public static WordPdfResult Convert(string markdown, MarkdownOptions? markdownOptions = null, WordPdfOptions? options = null) =>
        WordToPdf.Convert(MarkdownToWord.Convert(markdown, markdownOptions), options);
}
