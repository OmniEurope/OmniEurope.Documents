// SPDX-License-Identifier: EUPL-1.2
using System.Buffers;

namespace OmniEurope.Documents.Markdown;

/// <summary>
/// Second phase of CommonMark parsing: turns the raw text of a paragraph, heading or table cell into
/// inline nodes (code spans, emphasis, links, images, autolinks, raw HTML, entities, breaks).
/// </summary>
internal sealed class InlineParser(MarkdownOptions options, IReadOnlyDictionary<string, LinkReference> references)
{
    private static readonly SearchValues<char> Special = SearchValues.Create("\n\\`*_[]!<&");
    private static readonly SearchValues<char> SpecialWithTilde = SearchValues.Create("\n\\`*_[]!<&~");

    private string _subject = string.Empty;
    private int _position;
    private InlineList _list = new();

    public List<MarkdownInline> Parse(string text)
    {
        _subject = text;
        _position = 0;
        _list = new InlineList();
        while (_position < _subject.Length)
        {
            ParseOne();
        }

        _list.ProcessEmphasis(null);
        var inlines = InlineCleanup.MergeText(_list.ToList());
        return options.ExtendedAutolinks ? ExtendedAutolinks.Apply(inlines) : inlines;
    }

    private void ParseOne()
    {
        var c = _subject[_position];
        switch (c)
        {
            case '\n':
                ParseNewline();
                break;
            case '\\':
                ParseBackslash();
                break;
            case '`':
                ParseBackticks();
                break;
            case '*' or '_':
                ParseDelimiterRun(c);
                break;
            case '~' when options.Strikethrough:
                ParseDelimiterRun(c);
                break;
            case '[':
                _list.PushBracket(AppendText("["), ++_position, image: false);
                break;
            case '!' when Peek(1) == '[':
                _position += 2;
                _list.PushBracket(AppendText("!["), _position, image: true);
                break;
            case ']':
                ParseCloseBracket();
                break;
            case '<':
                ParseAngle();
                break;
            case '&':
                ParseEntity();
                break;
            default:
                ParseText();
                break;
        }
    }

    private char Peek(int ahead = 0) => _position + ahead < _subject.Length ? _subject[_position + ahead] : '\0';

    private InlineList.Item AppendText(string text) => _list.Append(new MarkdownText(text));

    private void ParseText()
    {
        var special = options.Strikethrough ? SpecialWithTilde : Special;
        var found = _subject.AsSpan(_position + 1).IndexOfAny(special);
        var end = found < 0 ? _subject.Length : found + _position + 1;

        AppendText(_subject[_position..end]);
        _position = end;
    }

    private void ParseNewline()
    {
        _position++;
        var hard = false;
        if (_list.Last?.Inline is MarkdownText text && text.Text.EndsWith(' '))
        {
            hard = text.Text.EndsWith("  ", StringComparison.Ordinal);
            text.Text = text.Text.TrimEnd(' ');
        }

        _list.Append(new MarkdownLineBreak(hard));
        SkipLeadingSpaces();
    }

    private void SkipLeadingSpaces()
    {
        while (_position < _subject.Length && _subject[_position] is ' ' or '\t')
        {
            _position++;
        }
    }

    private void ParseBackslash()
    {
        var next = Peek(1);
        if (next == '\n')
        {
            _position += 2;
            _list.Append(new MarkdownLineBreak(hard: true));
            SkipLeadingSpaces();
        }
        else if (LinkSyntax.IsEscapable(next))
        {
            AppendText(next.ToString());
            _position += 2;
        }
        else
        {
            AppendText("\\");
            _position++;
        }
    }

    private void ParseBackticks()
    {
        var start = _position;
        var length = RunLength(start, '`');
        var search = start + length;
        while (true)
        {
            var close = _subject.IndexOf('`', search);
            if (close < 0)
            {
                AppendText(_subject.Substring(start, length));
                _position = start + length;
                return;
            }

            var closeLength = RunLength(close, '`');
            if (closeLength == length)
            {
                _list.Append(new MarkdownCode(CodeContent(_subject[(start + length)..close])));
                _position = close + closeLength;
                return;
            }

            search = close + closeLength;
        }
    }

    private static string CodeContent(string raw)
    {
        var content = raw.Replace('\n', ' ');
        return content.Length >= 2 && content[0] == ' ' && content[^1] == ' ' && !string.IsNullOrWhiteSpace(content)
            ? content[1..^1]
            : content;
    }

    private int RunLength(int start, char c)
    {
        var end = start;
        while (end < _subject.Length && _subject[end] == c)
        {
            end++;
        }

        return end - start;
    }

    private void ParseDelimiterRun(char c)
    {
        var start = _position;
        var count = RunLength(start, c);
        _position += count;
        var before = start == 0 ? '\n' : PreviousCodePoint(start);
        var after = _position >= _subject.Length ? '\n' : NextCodePoint(_position);
        var beforeSpace = IsWhitespace(before);
        var afterSpace = IsWhitespace(after);
        var beforePunct = InlineList.IsPunctuation(before);
        var afterPunct = InlineList.IsPunctuation(after);
        var leftFlanking = !afterSpace && (!afterPunct || beforeSpace || beforePunct);
        var rightFlanking = !beforeSpace && (!beforePunct || afterSpace || afterPunct);
        bool canOpen;
        bool canClose;
        if (c == '_')
        {
            canOpen = leftFlanking && (!rightFlanking || beforePunct);
            canClose = rightFlanking && (!leftFlanking || afterPunct);
        }
        else
        {
            canOpen = leftFlanking;
            canClose = rightFlanking;
        }

        var item = AppendText(_subject.Substring(start, count));
        if ((canOpen || canClose) && (c != '~' || count <= 2))
        {
            _list.PushDelimiter(item, c, count, canOpen, canClose);
        }
    }

    private int NextCodePoint(int index) =>
        index + 1 < _subject.Length && char.IsHighSurrogate(_subject[index]) && char.IsLowSurrogate(_subject[index + 1])
            ? char.ConvertToUtf32(_subject[index], _subject[index + 1])
            : _subject[index];

    private int PreviousCodePoint(int index) =>
        index >= 2 && char.IsLowSurrogate(_subject[index - 1]) && char.IsHighSurrogate(_subject[index - 2])
            ? char.ConvertToUtf32(_subject[index - 2], _subject[index - 1])
            : _subject[index - 1];

    private static bool IsWhitespace(int codePoint) =>
        codePoint is '\n' or ' ' or '\t' or '\r' or '\f' || (codePoint > 127 && char.IsWhiteSpace((char)codePoint));

    private void ParseCloseBracket()
    {
        _position++;
        var afterBracket = _position;
        var opener = _list.TopBracket;
        if (opener is null)
        {
            AppendText("]");
            return;
        }

        if (!opener.Active)
        {
            _list.PopBracket();
            AppendText("]");
            return;
        }

        if (!TryLinkTail(opener, afterBracket, out var url, out var title))
        {
            _list.PopBracket();
            _position = afterBracket;
            AppendText("]");
            return;
        }

        // Emphasis inside the brackets is resolved first, among the delimiters opened after "[".
        _list.ProcessEmphasis(opener.PreviousDelimiter);
        var link = new MarkdownLink(url, title, opener.Image, autolink: false);
        _list.Wrap(opener.Item, link);
        _list.PopBracket();
        if (!opener.Image)
        {
            for (var bracket = _list.TopBracket; bracket is not null; bracket = bracket.Previous)
            {
                if (!bracket.Image)
                {
                    bracket.Active = false;
                }
            }
        }
    }

    // Parses what follows "]": an inline destination, or a full, collapsed or shortcut reference.
    private bool TryLinkTail(InlineList.Bracket opener, int afterBracket, out string url, out string title)
    {
        if (Peek() == '(' && TryInlineDestination(out url, out title))
        {
            return true;
        }

        _position = afterBracket;
        url = string.Empty;
        title = string.Empty;
        string label;
        var labelEnd = LinkSyntax.ParseLabel(_subject, _position);
        if (labelEnd > _position + 2)
        {
            label = _subject[(_position + 1)..(labelEnd - 1)];
        }
        else
        {
            label = _subject[opener.Index..(afterBracket - 1)];
        }

        if (label.Length > 999 || !references.TryGetValue(LinkSyntax.NormalizeLabel(label), out var reference))
        {
            return false;
        }

        if (labelEnd > 0)
        {
            _position = labelEnd;
        }
        else if (_subject.AsSpan(_position).StartsWith("[]"))
        {
            _position += 2;
        }

        url = reference.Url;
        title = reference.Title;
        return true;
    }

    private bool TryInlineDestination(out string url, out string title)
    {
        url = string.Empty;
        title = string.Empty;
        var position = LinkSyntax.SkipSpacesAndOneNewline(_subject, _position + 1);
        if (position < _subject.Length && _subject[position] != ')')
        {
            if (!LinkSyntax.TryParseDestination(_subject, position, out var destinationEnd, out url))
            {
                return false;
            }

            var titleStart = LinkSyntax.SkipSpacesAndOneNewline(_subject, destinationEnd);
            position = titleStart;
            if (titleStart > destinationEnd && LinkSyntax.TryParseTitle(_subject, titleStart, out var titleEnd, out title))
            {
                position = LinkSyntax.SkipSpacesAndOneNewline(_subject, titleEnd);
            }
        }

        if (position >= _subject.Length || _subject[position] != ')')
        {
            title = string.Empty;
            return false;
        }

        _position = position + 1;
        return true;
    }

    private void ParseAngle()
    {
        if (Autolinks.TryParse(_subject, _position, out var end, out var link))
        {
            _list.Append(link);
            _position = end;
            return;
        }

        if (options.AllowRawHtml)
        {
            var htmlEnd = InlineHtmlSyntax.Match(_subject, _position);
            if (htmlEnd > 0)
            {
                _list.Append(new MarkdownRawHtml(_subject[_position..htmlEnd]));
                _position = htmlEnd;
                return;
            }
        }

        AppendText("<");
        _position++;
    }

    private void ParseEntity()
    {
        if (LinkSyntax.TryDecodeEntity(_subject, _position, out var decoded, out var end))
        {
            AppendText(decoded);
            _position = end;
            return;
        }

        AppendText("&");
        _position++;
    }
}
