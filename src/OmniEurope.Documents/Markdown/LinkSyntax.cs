// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Net;
using System.Text;

namespace OmniEurope.Documents.Markdown;

/// <summary>A link reference definition (<c>[label]: url "title"</c>).</summary>
internal readonly record struct LinkReference(string Url, string Title);

/// <summary>Link labels, destinations, titles, backslash escapes and character references.</summary>
internal static class LinkSyntax
{
    private const int MaxLabelLength = 999;
    private static readonly System.Buffers.SearchValues<char> AsciiAlphanumerics =
        System.Buffers.SearchValues.Create("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");

    public static bool IsEscapable(char c) => c is >= '!' and <= '~' && !char.IsAsciiLetterOrDigit(c);

    /// <summary>True when a backslash at <paramref name="i"/> escapes the next character.</summary>
    public static bool IsEscapeAt(string text, int i) => text[i] == '\\' && i + 1 < text.Length && IsEscapable(text[i + 1]);

    /// <summary>Decodes backslash escapes and character references.</summary>
    public static string Unescape(string text)
    {
        if (text.IndexOfAny(['\\', '&']) < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (IsEscapeAt(text, i))
            {
                builder.Append(text[++i]);
            }
            else if (c == '&' && TryDecodeEntity(text, i, out var decoded, out var end))
            {
                builder.Append(decoded);
                i = end - 1;
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>Decodes a character reference (<c>&amp;amp;</c>, <c>&amp;#233;</c>, <c>&amp;#xE9;</c>) at <paramref name="start"/>.</summary>
    public static bool TryDecodeEntity(string text, int start, out string decoded, out int end)
    {
        decoded = string.Empty;
        end = start;
        var semicolon = text.IndexOf(';', start + 1);
        if (semicolon < 0 || semicolon - start > 34)
        {
            return false;
        }

        var body = text.AsSpan(start + 1, semicolon - start - 1);
        if (body.Length > 1 && body[0] == '#')
        {
            if (!TryNumeric(body[1..], out decoded))
            {
                return false;
            }
        }
        else
        {
            if (body.IsEmpty || !char.IsAsciiLetter(body[0]) || body.ContainsAnyExcept(AsciiAlphanumerics))
            {
                return false;
            }

            var raw = text.Substring(start, semicolon - start + 1);
            decoded = WebUtility.HtmlDecode(raw);
            if (decoded == raw)
            {
                return false;
            }
        }

        end = semicolon + 1;
        return true;
    }

    private static bool TryNumeric(ReadOnlySpan<char> digits, out string decoded)
    {
        decoded = string.Empty;
        var hex = digits[0] is 'x' or 'X';
        var number = hex ? digits[1..] : digits;
        var style = hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None;
        if (number.IsEmpty || number.Length > (hex ? 6 : 7) || !int.TryParse(number, style, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        decoded = IsScalar(value) ? char.ConvertFromUtf32(value) : "�";
        return true;
    }

    // A Unicode scalar value other than U+0000; anything else decodes to the replacement character.
    private static bool IsScalar(int value) => value is > 0 and <= 0x10FFFF && (value > 0xFFFF || !char.IsSurrogate((char)value));

    /// <summary>Normalises a label for matching: trimmed, inner white space collapsed, case-folded.</summary>
    public static string NormalizeLabel(ReadOnlySpan<char> label)
    {
        var builder = new StringBuilder(label.Length);
        var pendingSpace = false;
        foreach (var c in label.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = true;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        return builder.ToString().ToUpperInvariant().ToLowerInvariant();
    }

    /// <summary>A label <c>[...]</c> at <paramref name="start"/>; returns the index after <c>]</c> or 0.</summary>
    public static int ParseLabel(string text, int start)
    {
        if (start >= text.Length || text[start] != '[')
        {
            return 0;
        }

        for (var i = start + 1; i < text.Length && i - start <= MaxLabelLength + 1; i++)
        {
            switch (text[i])
            {
                case '\\' when IsEscapeAt(text, i):
                    i++;
                    break;
                case '[':
                    return 0;
                case ']':
                    return i - start > 1 ? i + 1 : 0;
            }
        }

        return 0;
    }

    /// <summary>A destination at <paramref name="start"/>: <c>&lt;...&gt;</c> or a raw run with balanced
    /// parentheses. Returns false when there is none.</summary>
    public static bool TryParseDestination(string text, int start, out int end, out string destination)
    {
        end = start;
        destination = string.Empty;
        if (start >= text.Length)
        {
            return false;
        }

        var bracketed = text[start] == '<';
        var close = bracketed ? BracketedEnd(text, start) : RawEnd(text, start);
        if (close < 0)
        {
            return false;
        }

        end = bracketed ? close + 1 : close;
        destination = Unescape(bracketed ? text[(start + 1)..close] : text[start..close]);
        return true;
    }

    // The index of the '>' closing a <...> destination, or -1.
    private static int BracketedEnd(string text, int start)
    {
        for (var i = start + 1; i < text.Length; i++)
        {
            if (IsEscapeAt(text, i))
            {
                i++;
            }
            else if (text[i] is '\n' or '<')
            {
                return -1;
            }
            else if (text[i] == '>')
            {
                return i;
            }
        }

        return -1;
    }

    // The end of a raw destination (no space or control character, parentheses balanced and at most 32 deep), or -1.
    private static int RawEnd(string text, int start)
    {
        var depth = 0;
        var position = start;
        while (position < text.Length && depth <= 32)
        {
            var c = text[position];
            if (IsEscapeAt(text, position))
            {
                position += 2;
                continue;
            }

            if (c <= ' ' || char.IsControl(c) || (c == ')' && depth == 0))
            {
                break;
            }

            depth += c == '(' ? 1 : c == ')' ? -1 : 0;
            position++;
        }

        return position == start || depth != 0 ? -1 : position;
    }

    /// <summary>A title <c>"..."</c>, <c>'...'</c> or <c>(...)</c> at <paramref name="start"/>.</summary>
    public static bool TryParseTitle(string text, int start, out int end, out string title)
    {
        end = start;
        title = string.Empty;
        if (start >= text.Length || text[start] is not ('"' or '\'' or '('))
        {
            return false;
        }

        var close = text[start] == '(' ? ')' : text[start];
        for (var i = start + 1; i < text.Length; i++)
        {
            if (IsEscapeAt(text, i))
            {
                i++;
            }
            else if (text[i] == close)
            {
                end = i + 1;
                title = Unescape(text[(start + 1)..i]);
                return true;
            }
            else if (BreaksTitle(text, i, close))
            {
                return false;
            }
        }

        return false;
    }

    // An unescaped '(' inside a (...) title, or a blank line, ends the attempt.
    private static bool BreaksTitle(string text, int i, char close) =>
        (close == ')' && text[i] == '(') || (text[i] == '\n' && IsBlankLineAhead(text, i + 1));

    /// <summary>A link reference definition starting at <paramref name="start"/> of a paragraph's text.</summary>
    public static bool TryParseReferenceDefinition(string text, int start, out int end, out string label, out LinkReference reference)
    {
        end = start;
        label = string.Empty;
        reference = default;
        var labelEnd = ParseLabel(text, start);
        if (labelEnd == 0 || labelEnd >= text.Length || text[labelEnd] != ':')
        {
            return false;
        }

        label = NormalizeLabel(text.AsSpan(start + 1, labelEnd - start - 2));
        if (label.Length == 0)
        {
            return false;
        }

        var destinationStart = SkipSpacesAndOneNewline(text, labelEnd + 1);
        if (!TryParseDestination(text, destinationStart, out var destinationEnd, out var url))
        {
            return false;
        }

        var titleStart = SkipSpacesAndOneNewline(text, destinationEnd);
        if (titleStart > destinationEnd && TryParseTitle(text, titleStart, out var titleEnd, out var title)
            && TryLineEnd(text, titleEnd, out end))
        {
            reference = new LinkReference(url, title);
            return true;
        }

        if (!TryLineEnd(text, destinationEnd, out end))
        {
            return false;
        }

        reference = new LinkReference(url, string.Empty);
        return true;
    }

    public static int SkipSpaces(string text, int i)
    {
        while (i < text.Length && text[i] is ' ' or '\t')
        {
            i++;
        }

        return i;
    }

    public static int SkipSpacesAndOneNewline(string text, int i)
    {
        i = SkipSpaces(text, i);
        if (i < text.Length && text[i] == '\n')
        {
            i = SkipSpaces(text, i + 1);
        }

        return i;
    }

    private static bool TryLineEnd(string text, int i, out int end)
    {
        i = SkipSpaces(text, i);
        end = i < text.Length ? i + 1 : i;
        return i >= text.Length || text[i] == '\n';
    }

    private static bool IsBlankLineAhead(string text, int i)
    {
        i = SkipSpaces(text, i);
        return i >= text.Length || text[i] == '\n';
    }
}
