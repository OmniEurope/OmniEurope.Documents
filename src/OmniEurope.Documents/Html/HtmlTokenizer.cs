// SPDX-License-Identifier: EUPL-1.2
using System.Net;

namespace OmniEurope.Documents.Html;

/// <summary>Kinds of tokens.</summary>
internal enum HtmlTokenKind
{
    Text,
    StartTag,
    EndTag,
    Comment,
    Doctype,
}

/// <summary>One token: text (decoded), a start or end tag with its attributes, a comment or a doctype.</summary>
internal sealed record HtmlToken(HtmlTokenKind Kind, string Data, IReadOnlyList<KeyValuePair<string, string>> Attributes, bool SelfClosing);

/// <summary>
/// A tolerant HTML tokenizer: never fails, a lone <c>&lt;</c> is text, character references are decoded in
/// text and attribute values, and the content of raw-text elements (<c>script</c>, <c>style</c>...) is read
/// up to the matching end tag without looking for markup.
/// </summary>
internal sealed class HtmlTokenizer(string html)
{
    private static readonly IReadOnlyList<KeyValuePair<string, string>> NoAttributes = [];

    private int _position;
    private string? _rawTextEnd;

    public IEnumerable<HtmlToken> Tokens()
    {
        while (_position < html.Length)
        {
            if (_rawTextEnd is not null)
            {
                var raw = ReadRawText(_rawTextEnd, out var escapable);
                _rawTextEnd = null;
                if (raw.Length > 0)
                {
                    yield return Text(escapable ? Decode(raw) : raw);
                }

                continue;
            }

            if (html[_position] == '<' && TryReadMarkup(out var token))
            {
                if (token.Kind == HtmlTokenKind.StartTag && !token.SelfClosing
                    && (HtmlTags.IsRawText(token.Data) || HtmlTags.IsEscapableRawText(token.Data)))
                {
                    _rawTextEnd = token.Data;
                }

                yield return token;
                continue;
            }

            yield return Text(Decode(ReadText()));
        }
    }

    private static HtmlToken Text(string data) => new(HtmlTokenKind.Text, data, NoAttributes, false);

    private static string Decode(string text) => text.Contains('&') ? WebUtility.HtmlDecode(text) : text;

    private string ReadText()
    {
        var start = _position;
        var next = html.IndexOf('<', _position + 1);
        _position = next < 0 ? html.Length : next;
        return html[start.._position];
    }

    private string ReadRawText(string tag, out bool escapable)
    {
        escapable = HtmlTags.IsEscapableRawText(tag);
        var search = _position;
        while (true)
        {
            var end = html.IndexOf("</", search, StringComparison.Ordinal);
            if (end < 0)
            {
                var all = html[_position..];
                _position = html.Length;
                return all;
            }

            var nameEnd = end + 2 + tag.Length;
            if (nameEnd <= html.Length && html.AsSpan(end + 2, tag.Length).Equals(tag, StringComparison.OrdinalIgnoreCase)
                && (nameEnd == html.Length || html[nameEnd] is ' ' or '\t' or '\n' or '\r' or '\f' or '/' or '>'))
            {
                var text = html[_position..end];
                _position = end;
                return text;
            }

            search = end + 2;
        }
    }

    private bool TryReadMarkup(out HtmlToken token)
    {
        token = null!;
        var next = _position + 1 < html.Length ? html[_position + 1] : '\0';
        if (char.IsAsciiLetter(next))
        {
            token = ReadTag(_position + 1, HtmlTokenKind.StartTag);
            return true;
        }

        if (next == '/' && _position + 2 < html.Length && char.IsAsciiLetter(html[_position + 2]))
        {
            token = ReadTag(_position + 2, HtmlTokenKind.EndTag);
            return true;
        }

        if (next == '/' && _position + 2 < html.Length && html[_position + 2] == '>')
        {
            _position += 3;
            token = Text(string.Empty);
            return true;
        }

        if (html.AsSpan(_position).StartsWith("<!--"))
        {
            token = ReadComment();
            return true;
        }

        if (next is '!' or '?')
        {
            token = ReadBogus(next == '!');
            return true;
        }

        return false;
    }

    private HtmlToken ReadComment()
    {
        var bodyStart = _position + 4;
        var end = html.IndexOf("-->", bodyStart, StringComparison.Ordinal);
        if (html.AsSpan(bodyStart).StartsWith(">") || html.AsSpan(bodyStart).StartsWith("->"))
        {
            _position = html.IndexOf('>', bodyStart) + 1;
            return new HtmlToken(HtmlTokenKind.Comment, string.Empty, NoAttributes, false);
        }

        var data = end < 0 ? html[bodyStart..] : html[bodyStart..end];
        _position = end < 0 ? html.Length : end + 3;
        return new HtmlToken(HtmlTokenKind.Comment, data, NoAttributes, false);
    }

    // "<!...>" (a declaration) or "<?...>": a comment holding what follows "<!", or what follows "<" for "<?"
    // (HTML Standard, unexpected-question-mark-instead-of-tag-name: the question mark is kept).
    private HtmlToken ReadBogus(bool declaration)
    {
        var start = _position + (declaration ? 2 : 1);
        var end = html.IndexOf('>', _position);
        var data = end < 0 ? html[start..] : html[start..end];
        _position = end < 0 ? html.Length : end + 1;
        var doctype = declaration && data.StartsWith("doctype", StringComparison.OrdinalIgnoreCase);
        return new HtmlToken(doctype ? HtmlTokenKind.Doctype : HtmlTokenKind.Comment, data, NoAttributes, false);
    }

    private HtmlToken ReadTag(int nameStart, HtmlTokenKind kind)
    {
        var i = nameStart;
        while (i < html.Length && !IsSpace(html[i]) && html[i] is not ('/' or '>'))
        {
            i++;
        }

        var name = html[nameStart..i].ToLowerInvariant();
        var attributes = new List<KeyValuePair<string, string>>();
        var selfClosing = false;
        while (i < html.Length)
        {
            var c = html[i];
            if (IsSpace(c))
            {
                i++;
            }
            else if (c == '>')
            {
                i++;
                break;
            }
            else if (c == '/')
            {
                i++;
                selfClosing = i < html.Length && html[i] == '>';
            }
            else
            {
                i = ReadAttribute(i, attributes);
            }
        }

        _position = i;
        return new HtmlToken(kind, name, kind == HtmlTokenKind.StartTag ? attributes : NoAttributes, selfClosing);
    }

    private int ReadAttribute(int i, List<KeyValuePair<string, string>> attributes)
    {
        var nameStart = i;
        i++;
        while (i < html.Length && !IsSpace(html[i]) && html[i] is not ('/' or '>' or '='))
        {
            i++;
        }

        var name = html[nameStart..i].ToLowerInvariant();
        var afterName = SkipSpaces(i);
        var value = string.Empty;
        if (afterName < html.Length && html[afterName] == '=')
        {
            (i, value) = ReadAttributeValue(SkipSpaces(afterName + 1));
        }

        if (!attributes.Exists(a => a.Key == name))
        {
            attributes.Add(new KeyValuePair<string, string>(name, value));
        }

        return i;
    }

    private (int End, string Value) ReadAttributeValue(int i)
    {
        if (i >= html.Length)
        {
            return (i, string.Empty);
        }

        var quote = html[i];
        if (quote is '"' or '\'')
        {
            var close = html.IndexOf(quote, i + 1);
            var end = close < 0 ? html.Length : close;
            return (Math.Min(end + 1, html.Length), Decode(html[(i + 1)..end]));
        }

        var start = i;
        while (i < html.Length && !IsSpace(html[i]) && html[i] != '>')
        {
            i++;
        }

        return (i, Decode(html[start..i]));
    }

    private int SkipSpaces(int i)
    {
        while (i < html.Length && IsSpace(html[i]))
        {
            i++;
        }

        return i;
    }

    private static bool IsSpace(char c) => c is ' ' or '\t' or '\n' or '\r' or '\f';
}
