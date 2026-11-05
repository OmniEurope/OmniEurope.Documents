// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion.HtmlLayout;
using OmniEurope.Documents.Html;
using OmniEurope.Documents.Markdown;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion;

/// <summary>
/// HTML to Word: headings, paragraphs, lists, quotes, preformatted text, tables with spans, rules, links,
/// inline formatting, the inline <c>style</c> attribute and, for a whole page (<see cref="Convert"/>), the rules of
/// its own <c>style</c> elements whose selector is a single class (<c>.name</c>, as Word to HTML writes them;
/// the <c>style</c> attribute wins over them). Linked style sheets are not read and images are read from
/// <c>data:</c> URIs only (nothing is fetched); anything left out is listed in <see cref="WordDocument.Gaps"/>.
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

        if (parsed.QuerySelectorAll("link").Any(l => l.GetAttribute("rel")?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("stylesheet", StringComparer.OrdinalIgnoreCase) == true))
        {
            document.Gaps.Add(LinkedStyleSheetsGap);
        }

        new HtmlWordWriter(document, CssClassRules.From(parsed)).Write(parsed.Body);
        return document;
    }

    /// <summary>The gap of a page that links a style sheet: it is never fetched, so its rules are not read.</summary>
    internal const string LinkedStyleSheetsGap = "linked style sheets are not read (nothing is fetched)";

    /// <summary>Appends the content of an HTML tree to the last section of <paramref name="document"/>; the
    /// <c>style</c> attributes count, not the rules of the page's style elements (the tree may not be a page).</summary>
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
