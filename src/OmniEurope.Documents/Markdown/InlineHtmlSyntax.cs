// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Markdown;

/// <summary>
/// Recognises raw inline HTML as CommonMark defines it: open tags with attributes, closing tags,
/// comments, processing instructions, declarations and CDATA sections. Every method returns the index just
/// past the construct, or 0 when there is none at <c>start</c>.
/// </summary>
internal static class InlineHtmlSyntax
{
    private const string AsciiLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    private static readonly System.Buffers.SearchValues<char> AttributeNameStart = System.Buffers.SearchValues.Create(AsciiLetters + "_:");

    private static readonly System.Buffers.SearchValues<char> AttributeNameChars = System.Buffers.SearchValues.Create(AsciiLetters + "0123456789_.:-");

    private static readonly System.Buffers.SearchValues<char> UnquotedValueStops = System.Buffers.SearchValues.Create(" \t\n\"'=<>`");

    public static int Match(ReadOnlySpan<char> text, int start)
    {
        if (start + 1 >= text.Length || text[start] != '<')
        {
            return 0;
        }

        var tag = MatchTag(text, start);
        if (tag > 0)
        {
            return tag;
        }

        var rest = text[start..];
        if (rest.StartsWith("<!--"))
        {
            return MatchComment(text, start);
        }

        if (rest.StartsWith("<?"))
        {
            return End(text, start + 2, "?>");
        }

        if (rest.StartsWith("<![CDATA["))
        {
            return End(text, start + 9, "]]>");
        }

        if (rest.Length > 2 && rest[1] == '!' && char.IsAsciiLetter(rest[2]))
        {
            return End(text, start + 2, ">");
        }

        return 0;
    }

    /// <summary>An open or closing tag at <paramref name="start"/>.</summary>
    public static int MatchTag(ReadOnlySpan<char> text, int start)
    {
        var i = start + 1;
        var closing = i < text.Length && text[i] == '/';
        if (closing)
        {
            i++;
        }

        var nameEnd = TagName(text, i);
        if (nameEnd == i)
        {
            return 0;
        }

        i = nameEnd;
        if (closing)
        {
            i = SkipWhitespace(text, i);
            return i < text.Length && text[i] == '>' ? i + 1 : 0;
        }

        while (true)
        {
            var afterSpace = SkipWhitespace(text, i);
            var attributeEnd = afterSpace > i ? Attribute(text, afterSpace) : 0;
            if (attributeEnd == 0)
            {
                i = afterSpace;
                break;
            }

            i = attributeEnd;
        }

        if (i < text.Length && text[i] == '>')
        {
            return i + 1;
        }

        return i + 1 < text.Length && text[i] == '/' && text[i + 1] == '>' ? i + 2 : 0;
    }

    private static int TagName(ReadOnlySpan<char> text, int i)
    {
        if (i >= text.Length || !char.IsAsciiLetter(text[i]))
        {
            return i;
        }

        i++;
        while (i < text.Length && (char.IsAsciiLetterOrDigit(text[i]) || text[i] == '-'))
        {
            i++;
        }

        return i;
    }

    private static int Attribute(ReadOnlySpan<char> text, int i)
    {
        if (i >= text.Length || !AttributeNameStart.Contains(text[i]))
        {
            return 0;
        }

        var nameLength = text[(i + 1)..].IndexOfAnyExcept(AttributeNameChars);
        i = nameLength < 0 ? text.Length : i + 1 + nameLength;
        var beforeValue = SkipWhitespace(text, i);
        if (beforeValue >= text.Length || text[beforeValue] != '=')
        {
            return i;
        }

        var valueStart = SkipWhitespace(text, beforeValue + 1);
        var valueEnd = AttributeValue(text, valueStart);
        return valueEnd > 0 ? valueEnd : 0;
    }

    private static int AttributeValue(ReadOnlySpan<char> text, int i)
    {
        if (i >= text.Length)
        {
            return 0;
        }

        if (text[i] is '"' or '\'')
        {
            var close = text[(i + 1)..].IndexOf(text[i]);
            return close < 0 ? 0 : i + close + 2;
        }

        // An unquoted value: anything but white space, quotes, '=', '<', '>' and '`'.
        var length = text[i..].IndexOfAny(UnquotedValueStops);
        return length switch
        {
            0 => 0,
            < 0 => text.Length,
            _ => i + length,
        };
    }

    private static int MatchComment(ReadOnlySpan<char> text, int start)
    {
        var body = start + 4;
        if (text[body..].StartsWith(">"))
        {
            return body + 1;
        }

        if (text[body..].StartsWith("->"))
        {
            return body + 2;
        }

        return End(text, body, "-->");
    }

    private static int End(ReadOnlySpan<char> text, int from, string terminator)
    {
        var at = text[from..].IndexOf(terminator, StringComparison.Ordinal);
        return at < 0 ? 0 : from + at + terminator.Length;
    }

    private static int SkipWhitespace(ReadOnlySpan<char> text, int i)
    {
        while (i < text.Length && text[i] is ' ' or '\t' or '\n' or '\r')
        {
            i++;
        }

        return i;
    }
}
