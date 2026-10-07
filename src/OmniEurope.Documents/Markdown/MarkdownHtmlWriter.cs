// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;

namespace OmniEurope.Documents.Markdown;

/// <summary>Writes the HTML of a Markdown tree, following the CommonMark reference output.</summary>
internal sealed class MarkdownHtmlWriter(MarkdownOptions options)
{
    private readonly StringBuilder _html = new();

    public string Write(MarkdownDocument document)
    {
        WriteBlocks(document.Children, tight: false);
        return _html.ToString();
    }

    private void WriteBlocks(IReadOnlyList<MarkdownBlock> blocks, bool tight)
    {
        foreach (var block in blocks)
        {
            WriteBlock(block, tight);
        }
    }

    private void WriteBlock(MarkdownBlock block, bool tight)
    {
        switch (block)
        {
            case MarkdownParagraph paragraph when tight:
                WriteInlines(paragraph.Inlines);
                break;
            case MarkdownParagraph paragraph:
                Line().Append("<p>");
                WriteInlines(paragraph.Inlines);
                _html.Append("</p>\n");
                break;
            case MarkdownHeading heading:
                Line().Append("<h").Append(heading.Level).Append('>');
                WriteInlines(heading.Inlines);
                _html.Append("</h").Append(heading.Level).Append(">\n");
                break;
            case MarkdownThematicBreak:
                Line().Append("<hr />\n");
                break;
            case MarkdownCodeBlock code:
                WriteCode(code);
                break;
            case MarkdownHtmlBlock html:
                Line().Append(html.Html).Append('\n');
                break;
            case MarkdownBlockQuote quote:
                Line().Append("<blockquote>\n");
                WriteBlocks(quote.Children, tight: false);
                Line().Append("</blockquote>\n");
                break;
            case MarkdownList list:
                WriteList(list);
                break;
            case MarkdownTable table:
                WriteTable(table);
                break;
        }
    }

    private StringBuilder Line()
    {
        if (_html.Length > 0 && _html[^1] != '\n')
        {
            _html.Append('\n');
        }

        return _html;
    }

    private void WriteCode(MarkdownCodeBlock code)
    {
        Line().Append("<pre><code");
        var language = code.Info.Split([' ', '\t'], 2)[0];
        if (language.Length > 0)
        {
            _html.Append(" class=\"language-");
            Escape(language);
            _html.Append('"');
        }

        _html.Append('>');
        Escape(code.Code);
        _html.Append("</code></pre>\n");
    }

    private void WriteList(MarkdownList list)
    {
        var tag = list.IsOrdered ? "ol" : "ul";
        Line().Append('<').Append(tag);
        if (list.IsOrdered && list.Start != 1)
        {
            _html.Append(" start=\"").Append(list.Start.ToString(CultureInfo.InvariantCulture)).Append('"');
        }

        _html.Append(">\n");
        foreach (var block in list.Children)
        {
            var item = (MarkdownListItem)block;
            _html.Append("<li>");
            if (item.TaskChecked is { } done)
            {
                _html.Append(done ? "<input type=\"checkbox\" checked=\"\" disabled=\"\" /> " : "<input type=\"checkbox\" disabled=\"\" /> ");
            }

            WriteBlocks(item.Children, list.IsTight);
            if (!(list.IsTight && item.Children is [.., MarkdownParagraph]) && item.Children.Count > 0)
            {
                Line();
            }

            _html.Append("</li>\n");
        }

        _html.Append("</").Append(tag).Append(">\n");
    }

    private void WriteTable(MarkdownTable table)
    {
        Line().Append("<table>\n<thead>\n");
        WriteRow(table.Header, table.Alignments, "th");
        _html.Append("</thead>\n");
        if (table.Rows.Count > 0)
        {
            _html.Append("<tbody>\n");
            foreach (var row in table.Rows)
            {
                WriteRow(row, table.Alignments, "td");
            }

            _html.Append("</tbody>\n");
        }

        _html.Append("</table>\n");
    }

    private void WriteRow(IReadOnlyList<IReadOnlyList<MarkdownInline>> cells, IReadOnlyList<MarkdownTableAlignment> alignments, string tag)
    {
        _html.Append("<tr>\n");
        for (var i = 0; i < cells.Count; i++)
        {
            _html.Append('<').Append(tag);
            var align = alignments[i] switch
            {
                MarkdownTableAlignment.Left => "left",
                MarkdownTableAlignment.Center => "center",
                MarkdownTableAlignment.Right => "right",
                _ => null,
            };
            if (align is not null)
            {
                _html.Append(" align=\"").Append(align).Append('"');
            }

            _html.Append('>');
            WriteInlines(cells[i]);
            _html.Append("</").Append(tag).Append(">\n");
        }

        _html.Append("</tr>\n");
    }

    private void WriteInlines(IReadOnlyList<MarkdownInline> inlines)
    {
        foreach (var inline in inlines)
        {
            WriteInline(inline);
        }
    }

    private void WriteInline(MarkdownInline inline)
    {
        switch (inline)
        {
            case MarkdownText text:
                Escape(text.Text);
                break;
            case MarkdownCode code:
                _html.Append("<code>");
                Escape(code.Code);
                _html.Append("</code>");
                break;
            case MarkdownLineBreak lineBreak:
                _html.Append(lineBreak.IsHard ? "<br />\n" : "\n");
                break;
            case MarkdownRawHtml raw:
                _html.Append(raw.Html);
                break;
            case MarkdownEmphasis emphasis:
                Wrap(emphasis.IsStrong ? "strong" : "em", emphasis.Children);
                break;
            case MarkdownStrikethrough strike:
                Wrap("del", strike.Children);
                break;
            case MarkdownLink { IsImage: true } image:
                WriteImage(image);
                break;
            case MarkdownLink link:
                WriteLink(link);
                break;
        }
    }

    private void Wrap(string tag, IReadOnlyList<MarkdownInline> children)
    {
        _html.Append('<').Append(tag).Append('>');
        WriteInlines(children);
        _html.Append("</").Append(tag).Append('>');
    }

    private void WriteLink(MarkdownLink link)
    {
        if (!options.UrlFilter(link.Url, false))
        {
            WriteInlines(link.Children);
            return;
        }

        _html.Append("<a href=\"");
        Escape(MarkdownUrl.Normalize(link.Url));
        _html.Append('"');
        if (link.Title.Length > 0)
        {
            _html.Append(" title=\"");
            Escape(link.Title);
            _html.Append('"');
        }

        _html.Append('>');
        WriteInlines(link.Children);
        _html.Append("</a>");
    }

    private void WriteImage(MarkdownLink image)
    {
        var alt = new StringBuilder();
        PlainText(image.Children, alt);
        if (!options.UrlFilter(image.Url, true))
        {
            Escape(alt.ToString());
            return;
        }

        _html.Append("<img src=\"");
        Escape(MarkdownUrl.Normalize(image.Url));
        _html.Append("\" alt=\"");
        Escape(alt.ToString());
        _html.Append('"');
        if (image.Title.Length > 0)
        {
            _html.Append(" title=\"");
            Escape(image.Title);
            _html.Append('"');
        }

        _html.Append(" />");
    }

    internal static void PlainText(IReadOnlyList<MarkdownInline> inlines, StringBuilder output)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case MarkdownText text:
                    output.Append(text.Text);
                    break;
                case MarkdownCode code:
                    output.Append(code.Code);
                    break;
                case MarkdownLineBreak:
                    output.Append('\n');
                    break;
                case MarkdownContainerInline container:
                    PlainText(container.Children, output);
                    break;
            }
        }
    }

    private void Escape(string text)
    {
        foreach (var c in text)
        {
            _ = c switch
            {
                '&' => _html.Append("&amp;"),
                '<' => _html.Append("&lt;"),
                '>' => _html.Append("&gt;"),
                '"' => _html.Append("&quot;"),
                _ => _html.Append(c),
            };
        }
    }
}
