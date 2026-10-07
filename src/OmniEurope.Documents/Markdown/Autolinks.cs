// SPDX-License-Identifier: EUPL-1.2
using System.Buffers;
using System.Text;

namespace OmniEurope.Documents.Markdown;

/// <summary>CommonMark autolinks: <c>&lt;scheme:...&gt;</c> and <c>&lt;user@example.com&gt;</c>.</summary>
internal static class Autolinks
{
    public static bool TryParse(string text, int start, out int end, out MarkdownLink link)
    {
        end = start;
        link = null!;
        var close = text.IndexOf('>', start + 1);
        if (close < 0)
        {
            return false;
        }

        var body = text[(start + 1)..close];
        if (IsUri(body))
        {
            link = Create(body, body);
        }
        else if (IsEmail(body))
        {
            link = Create("mailto:" + body, body);
        }
        else
        {
            return false;
        }

        end = close + 1;
        return true;
    }

    public static MarkdownLink Create(string url, string text)
    {
        var link = new MarkdownLink(url, string.Empty, image: false, autolink: true);
        link.ChildList.Add(new MarkdownText(text));
        return link;
    }

    private static readonly SearchValues<char> SchemeChars =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+.-");

    // Space, ASCII and C1 controls, '<' and '>' end a URI autolink.
    private static readonly SearchValues<char> UriStoppers = SearchValues.Create(
        string.Concat(Enumerable.Range(0, 0x21).Concat(Enumerable.Range(0x7F, 0x21)).Select(c => (char)c)) + "<>");

    private static bool IsUri(string body)
    {
        var colon = body.IndexOf(':');
        return colon is >= 2 and <= 32
            && char.IsAsciiLetter(body[0])
            && !body.AsSpan(1, colon - 1).ContainsAnyExcept(SchemeChars)
            && !body.AsSpan(colon + 1).ContainsAny(UriStoppers);
    }

    public static bool IsEmail(string body)
    {
        var at = body.IndexOf('@');
        if (at < 1 || at == body.Length - 1)
        {
            return false;
        }

        for (var i = 0; i < at; i++)
        {
            if (!(char.IsAsciiLetterOrDigit(body[i]) || ".!#$%&'*+/=?^_`{|}~-".Contains(body[i])))
            {
                return false;
            }
        }

        foreach (var label in body[(at + 1)..].Split('.'))
        {
            if (label.Length is 0 or > 63 || label[0] == '-' || label[^1] == '-'
                || !label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// GFM extended autolinks, applied to text outside links and code: <c>https://</c>, <c>http://</c> and
/// <c>www.</c> URLs and bare e-mail addresses. Trailing punctuation and an unbalanced closing parenthesis
/// are left out of the link.
/// </summary>
internal static class ExtendedAutolinks
{
    private static readonly SearchValues<char> TrailingPunctuation = SearchValues.Create("?!.,:*_~'\"");

    public static List<MarkdownInline> Apply(List<MarkdownInline> inlines)
    {
        var result = new List<MarkdownInline>(inlines.Count);
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case MarkdownText text:
                    Split(text.Text, result);
                    break;
                case MarkdownLink:
                    result.Add(inline);
                    break;
                case MarkdownContainerInline container:
                    var children = Apply(container.ChildList);
                    container.ChildList.Clear();
                    container.ChildList.AddRange(children);
                    result.Add(container);
                    break;
                default:
                    result.Add(inline);
                    break;
            }
        }

        return result;
    }

    private static void Split(string text, List<MarkdownInline> output)
    {
        var plain = new StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            if (AtWordStart(text, i) && TryMatchUrl(text, i, out var end, out var url))
            {
                Flush(plain, output);
                output.Add(Autolinks.Create(url, text[i..end]));
                i = end;
                continue;
            }

            if (text[i] == '@' && TryMatchEmail(text, i, plain, out var emailEnd, out var email))
            {
                Flush(plain, output);
                output.Add(Autolinks.Create("mailto:" + email, email));
                i = emailEnd;
                continue;
            }

            plain.Append(text[i++]);
        }

        Flush(plain, output);
    }

    private static void Flush(StringBuilder plain, List<MarkdownInline> output)
    {
        if (plain.Length > 0)
        {
            output.Add(new MarkdownText(plain.ToString()));
            plain.Clear();
        }
    }

    private static bool AtWordStart(string text, int i) =>
        i == 0 || char.IsWhiteSpace(text[i - 1]) || text[i - 1] is '*' or '_' or '~' or '(';

    private static bool TryMatchUrl(string text, int start, out int end, out string url)
    {
        end = start;
        url = string.Empty;
        var rest = text.AsSpan(start);
        string prefix;
        if (rest.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || rest.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            prefix = string.Empty;
        }
        else if (rest.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            prefix = "http://";
        }
        else
        {
            return false;
        }

        var domainStart = start + (prefix.Length == 0 ? rest.IndexOf("//", StringComparison.Ordinal) + 2 : 0);
        var domainEnd = domainStart;
        while (domainEnd < text.Length && (char.IsLetterOrDigit(text[domainEnd]) || text[domainEnd] is '.' or '-' or '_'))
        {
            domainEnd++;
        }

        if (!ValidDomain(text.AsSpan(domainStart, domainEnd - domainStart)))
        {
            return false;
        }

        var stop = domainEnd;
        while (stop < text.Length && !char.IsWhiteSpace(text[stop]) && text[stop] != '<')
        {
            stop++;
        }

        end = TrimTrailing(text, domainEnd, stop);
        url = prefix + text[start..end];
        return true;
    }

    private static bool ValidDomain(ReadOnlySpan<char> domain)
    {
        if (domain.IsEmpty || !domain.Contains('.'))
        {
            return false;
        }

        var parts = domain.ToString().Split('.');
        return parts.All(p => p.Length > 0) && !parts[^1].Contains('_') && !parts[^2].Contains('_');
    }

    private static int TrimTrailing(string text, int minimum, int end)
    {
        for (var trimmed = TrimOne(text, minimum, end); trimmed < end; trimmed = TrimOne(text, minimum, end))
        {
            end = trimmed;
        }

        return end;
    }

    // Trailing punctuation, an unbalanced ')' or an entity-like '&name;' leave the link; returns the new end.
    private static int TrimOne(string text, int minimum, int end)
    {
        if (end <= minimum)
        {
            return end;
        }

        var c = text[end - 1];
        var span = text.AsSpan(minimum, end - minimum);
        if (TrailingPunctuation.Contains(c) || (c == ')' && span.Count(')') > span.Count('(')))
        {
            return end - 1;
        }

        return c == ';' && EntityStart(text, minimum, end) is >= 0 and var amp ? amp : end;
    }

    private static int EntityStart(string text, int minimum, int end)
    {
        var i = end - 2;
        while (i >= minimum && char.IsAsciiLetterOrDigit(text[i]))
        {
            i--;
        }

        return i >= minimum && text[i] == '&' && i < end - 2 ? i : -1;
    }

    private static bool TryMatchEmail(string text, int at, StringBuilder plain, out int end, out string email)
    {
        end = at;
        email = string.Empty;
        var localLength = 0;
        while (localLength < plain.Length && IsLocalChar(plain[plain.Length - 1 - localLength]))
        {
            localLength++;
        }

        var domainEnd = DomainEnd(text, at + 1);
        var domain = text.AsSpan(at + 1, domainEnd - at - 1);
        if (localLength == 0 || !domain.Contains('.') || domain[^1] is '-' or '_')
        {
            return false;
        }

        var local = plain.ToString(plain.Length - localLength, localLength);
        plain.Length -= localLength;
        email = local + "@" + domain.ToString();
        end = domainEnd;
        return true;
    }

    private static bool IsLocalChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '.' or '+' or '-' or '_';

    // The end of an e-mail domain: letters, digits, '.', '-' and '_', without trailing dots.
    private static int DomainEnd(string text, int start)
    {
        var end = start;
        while (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || text[end] is '.' or '-' or '_'))
        {
            end++;
        }

        while (end > start && text[end - 1] == '.')
        {
            end--;
        }

        return end;
    }
}
