// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Markdown;

/// <summary>The seven kinds of CommonMark HTML blocks: how each starts and how kinds 1 to 5 end.</summary>
internal static class HtmlBlockSyntax
{
    private static readonly string[] RawTextTags = ["script", "pre", "style", "textarea"];

    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "base", "basefont", "blockquote", "body", "caption", "center", "col",
        "colgroup", "dd", "details", "dialog", "dir", "div", "dl", "dt", "fieldset", "figcaption", "figure",
        "footer", "form", "frame", "frameset", "h1", "h2", "h3", "h4", "h5", "h6", "head", "header", "hr",
        "html", "iframe", "legend", "li", "link", "main", "menu", "menuitem", "nav", "noframes", "ol",
        "optgroup", "option", "p", "param", "search", "section", "summary", "table", "tbody", "td", "tfoot",
        "th", "thead", "title", "tr", "track", "ul",
    };

    /// <summary>The kind (1-7) of HTML block starting <paramref name="text"/>, or 0.</summary>
    public static int StartKind(ReadOnlySpan<char> text)
    {
        if (text.Length < 2 || text[0] != '<')
        {
            return 0;
        }

        if (StartsWithRawTextTag(text))
        {
            return 1;
        }

        if (text.StartsWith("<!--"))
        {
            return 2;
        }

        if (text.StartsWith("<?"))
        {
            return 3;
        }

        if (text.Length > 2 && text[1] == '!' && char.IsAsciiLetter(text[2]))
        {
            return 4;
        }

        if (text.StartsWith("<![CDATA["))
        {
            return 5;
        }

        if (StartsWithBlockTag(text))
        {
            return 6;
        }

        var end = InlineHtmlSyntax.MatchTag(text, 0);
        return end > 0 && text[end..].Trim(" \t").IsEmpty ? 7 : 0;
    }

    // What closes each kind of HTML block (1 to 5); kinds 6 and 7 end at a blank line instead.
    private static readonly string[][] Terminators =
    [
        [],
        ["</script>", "</pre>", "</style>", "</textarea>"],
        ["-->"],
        ["?>"],
        [">"],
        ["]]>"],
    ];

    /// <summary>True when <paramref name="line"/> holds the end condition of an HTML block of kind 1-5.</summary>
    public static bool Ends(int kind, ReadOnlySpan<char> line)
    {
        if (kind is < 1 or > 5)
        {
            return false;
        }

        foreach (var terminator in Terminators[kind])
        {
            if (line.Contains(terminator, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool StartsWithRawTextTag(ReadOnlySpan<char> text)
    {
        foreach (var tag in RawTextTags)
        {
            if (text.Length > tag.Length && text[1..].StartsWith(tag, StringComparison.OrdinalIgnoreCase)
                && IsTagNameEnd(text, tag.Length + 1, allowSlash: false))
            {
                return true;
            }
        }

        return false;
    }

    private static bool StartsWithBlockTag(ReadOnlySpan<char> text)
    {
        var start = text[1] == '/' ? 2 : 1;
        var end = start;
        while (end < text.Length && char.IsAsciiLetterOrDigit(text[end]))
        {
            end++;
        }

        return end > start && BlockTags.Contains(text[start..end].ToString()) && IsTagNameEnd(text, end, allowSlash: true);
    }

    private static bool IsTagNameEnd(ReadOnlySpan<char> text, int position, bool allowSlash)
    {
        if (position >= text.Length)
        {
            return true;
        }

        var c = text[position];
        return c is ' ' or '\t' or '>' || (allowSlash && c == '/' && position + 1 < text.Length && text[position + 1] == '>');
    }
}
