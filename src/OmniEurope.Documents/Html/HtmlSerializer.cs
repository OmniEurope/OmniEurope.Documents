// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Html;

/// <summary>HTML serialization: text and attribute values escaped, raw-text elements left as is, void
/// elements without an end tag.</summary>
internal static class HtmlSerializer
{
    public static void Write(HtmlNode node, StringBuilder output)
    {
        switch (node)
        {
            case HtmlText text:
                if (text.Parent is HtmlElement parent && HtmlTags.IsRawText(parent.TagName))
                {
                    output.Append(text.Data);
                }
                else
                {
                    EscapeText(text.Data, output);
                }

                break;
            case HtmlComment comment:
                output.Append("<!--").Append(comment.Data).Append("-->");
                break;
            case HtmlElement element:
                WriteElement(element, output);
                break;
            default:
                foreach (var child in node.ChildNodes)
                {
                    Write(child, output);
                }

                break;
        }
    }

    private static void WriteElement(HtmlElement element, StringBuilder output)
    {
        output.Append('<').Append(element.TagName);
        foreach (var attribute in element.Attributes)
        {
            output.Append(' ').Append(attribute.Name).Append("=\"");
            EscapeAttribute(attribute.Value, output);
            output.Append('"');
        }

        output.Append('>');
        if (element.IsVoid)
        {
            return;
        }

        foreach (var child in element.ChildNodes)
        {
            Write(child, output);
        }

        output.Append("</").Append(element.TagName).Append('>');
    }

    public static void EscapeText(string text, StringBuilder output)
    {
        foreach (var c in text)
        {
            _ = c switch
            {
                '&' => output.Append("&amp;"),
                '<' => output.Append("&lt;"),
                '>' => output.Append("&gt;"),
                ' ' => output.Append("&nbsp;"),
                _ => output.Append(c),
            };
        }
    }

    private static void EscapeAttribute(string text, StringBuilder output)
    {
        foreach (var c in text)
        {
            _ = c switch
            {
                '&' => output.Append("&amp;"),
                '"' => output.Append("&quot;"),
                '<' => output.Append("&lt;"),
                '>' => output.Append("&gt;"),
                ' ' => output.Append("&nbsp;"),
                _ => output.Append(c),
            };
        }
    }
}
