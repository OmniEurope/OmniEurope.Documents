// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Markdown;

/// <summary>Parses Markdown (CommonMark, optionally with the GitHub extensions) into a syntax tree.</summary>
public static class MarkdownParser
{
    /// <summary>Parses <paramref name="markdown"/>.</summary>
    /// <param name="markdown">The Markdown text.</param>
    /// <param name="options">Parser options; <see cref="MarkdownOptions.CommonMark"/> when null.</param>
    public static MarkdownDocument Parse(string markdown, MarkdownOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        options ??= MarkdownOptions.CommonMark;
        var blocks = new BlockParser(options);
        var document = blocks.Parse(markdown);
        var inlines = new InlineParser(options, blocks.References);
        ParseInlines(document, inlines, options);
        return document;
    }

    private static void ParseInlines(MarkdownContainerBlock container, InlineParser inlines, MarkdownOptions options)
    {
        foreach (var block in container.ChildList)
        {
            switch (block)
            {
                case MarkdownParagraph paragraph:
                    var text = paragraph.Content!.ToString().Trim();
                    paragraph.Content = null;
                    if (options.TaskLists && container is MarkdownListItem item && item.ChildList[0] == paragraph)
                    {
                        text = ExtractTask(item, text);
                    }

                    paragraph.Inlines = inlines.Parse(text);
                    break;
                case MarkdownHeading heading:
                    heading.Inlines = inlines.Parse(heading.RawText);
                    break;
                case MarkdownTable table:
                    ParseTable(table, inlines);
                    break;
                case MarkdownContainerBlock child:
                    ParseInlines(child, inlines, options);
                    break;
            }
        }
    }

    private static string ExtractTask(MarkdownListItem item, string text)
    {
        if (text.Length < 3 || text[0] != '[' || text[2] != ']' || text[1] is not (' ' or 'x' or 'X')
            || (text.Length > 3 && text[3] is not (' ' or '\t' or '\n')))
        {
            return text;
        }

        item.TaskChecked = text[1] != ' ';
        return text[3..].TrimStart(' ', '\t');
    }

    private static void ParseTable(MarkdownTable table, InlineParser inlines)
    {
        var columns = table.Alignments.Count;
        table.Header = table.RawHeader.Select(inlines.Parse).ToList();
        table.Rows = table.RawRows
            .Select(row => (IReadOnlyList<IReadOnlyList<MarkdownInline>>)Enumerable.Range(0, columns)
                .Select(i => (IReadOnlyList<MarkdownInline>)(i < row.Count ? inlines.Parse(row[i]) : []))
                .ToList())
            .ToList();
    }
}

/// <summary>Renders Markdown to HTML.</summary>
public static class MarkdownRenderer
{
    /// <summary>
    /// Converts <paramref name="markdown"/> to HTML. With the default options raw HTML is escaped and unsafe
    /// link schemes are dropped, so the result can be injected into a page as is.
    /// </summary>
    public static string ToHtml(string markdown, MarkdownOptions? options = null)
    {
        options ??= MarkdownOptions.CommonMark;
        return ToHtml(MarkdownParser.Parse(markdown, options), options);
    }

    /// <summary>Renders an already parsed document.</summary>
    public static string ToHtml(MarkdownDocument document, MarkdownOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new MarkdownHtmlWriter(options ?? MarkdownOptions.CommonMark).Write(document);
    }
}
