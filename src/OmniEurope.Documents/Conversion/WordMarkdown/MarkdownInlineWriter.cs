// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Conversion.WordLayout;
using OmniEurope.Documents.Markdown;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordMarkdown;

/// <summary>
/// Writes the inline content of one paragraph as Markdown. Strike, bold and italic open as <c>~~</c>, <c>**</c>
/// and <c>*</c> and stay open while the next runs keep them; spaces between runs are written outside the
/// markers that close or open there, since Markdown does not read a marker next to a space as emphasis. Text is
/// escaped, with the rules of a line start after the start of the paragraph and after a hard break. Under a
/// baseline (the formatting a heading takes from its style), only bold and italic beyond it are marked.
/// </summary>
internal sealed class MarkdownInlineWriter(MarkdownContext context, WordParagraphProperties paragraph, string indent, bool singleLine, WordRunProperties? baseline = null)
{
    private static readonly string[] SafeSchemes = ["http", "https", "mailto"];
    private static readonly string[] MarkerOrder = ["~~", "**", "*"];

    private readonly StringBuilder _text = new();
    private readonly List<string> _open = [];
    private string _space = string.Empty;
    private bool _break;
    private bool _lineStart = true;
    private bool _inLink;

    /// <summary>The text boxes met, in order.</summary>
    public List<WordTextBox> TextBoxes { get; } = [];

    /// <summary>The Markdown of the inlines, or an empty string when nothing visible was written.</summary>
    public string Write(IEnumerable<WordInline> inlines)
    {
        Inlines(inlines);
        Close(0);
        return _text.ToString();
    }

    private void Inlines(IEnumerable<WordInline> inlines)
    {
        foreach (var inline in inlines)
        {
            if (!MarkdownContext.Skips(inline.Revision))
            {
                Inline(inline);
            }
        }
    }

    private void Inline(WordInline inline)
    {
        switch (inline)
        {
            case WordText text:
                Text(text.Value, inline.Properties, null);
                break;
            case WordTab:
                context.Gaps.Add("tabs are written as tab characters");
                Text("\t", inline.Properties, null);
                break;
            case WordSymbol symbol:
                Text(symbol.Character.ToString(), inline.Properties, symbol.Font);
                break;
            case WordBreak lineBreak:
                Break(lineBreak);
                break;
            default:
                Complex(inline);
                break;
        }
    }

    private void Complex(WordInline inline)
    {
        switch (inline)
        {
            case WordField field:
                Inlines(field.Result);
                break;
            case WordHyperlink link:
                Link(link);
                break;
            case WordNoteReference note:
                Note(note);
                break;
            case WordPicture picture:
                Picture(picture);
                break;
            case WordTextBox box:
                TextBoxes.Add(box);
                break;
            case WordCommentReference:
                context.Gaps.Add("comments are left out");
                break;
        }
    }

    private void Text(string text, WordRunProperties direct, string? symbolFont)
    {
        var properties = WordResolution.Run(context.Document.Styles, paragraph, direct, null);
        if (properties.Hidden == true || text.Length == 0)
        {
            return;
        }

        var font = symbolFont ?? properties.Font;
        text = Symbols.IsSymbolFont(font) ? Symbols.Map(text, font, context.Gaps) : text;

        NoteLostFormatting(properties);
        if (baseline is not null)
        {
            properties = properties with
            {
                Bold = properties.Bold == true && baseline.Bold != true,
                Italic = properties.Italic == true && baseline.Italic != true,
            };
        }
        var core = text.Trim();
        if (core.Length == 0)
        {
            _space += text;
            return;
        }

        var start = text.IndexOf(core[0], StringComparison.Ordinal);
        _space += text[..start];
        Switch(Markers(properties));
        Append(core);
        _space = text[(start + core.Length)..];
    }

    // Markdown has no underline, superscript or subscript (a link's own underline is the link's look); the other
    // run formatting is not carried either.
    private void NoteLostFormatting(WordRunProperties properties)
    {
        if ((!_inLink && properties.Underline is { } underline and not WordUnderline.None) || properties.VerticalPosition is WordVerticalPosition.Superscript or WordVerticalPosition.Subscript)
        {
            context.Gaps.Add("underline, superscript and subscript are written as plain text");
        }
    }

    private static List<string> Markers(WordRunProperties p)
    {
        var markers = new List<string>(3);
        if (p.Strike == true || p.DoubleStrike == true)
        {
            markers.Add("~~");
        }

        if (p.Bold == true)
        {
            markers.Add("**");
        }

        if (p.Italic == true)
        {
            markers.Add("*");
        }

        return markers;
    }

    // Keeps the markers still wanted from the outside in, closes the rest, writes the pending space, then opens
    // the missing ones in a fixed order.
    private void Switch(List<string> wanted)
    {
        var keep = 0;
        while (keep < _open.Count && wanted.Contains(_open[keep]))
        {
            keep++;
        }

        Close(keep);
        FlushSpace();
        foreach (var marker in MarkerOrder)
        {
            if (wanted.Contains(marker) && !_open.Contains(marker))
            {
                _text.Append(marker);
                _open.Add(marker);
                _lineStart = false;
            }
        }
    }

    private void Close(int keep)
    {
        for (var i = _open.Count - 1; i >= keep; i--)
        {
            _text.Append(_open[i]);
        }

        _open.RemoveRange(keep, _open.Count - keep);
    }

    // A pending hard break is written before the next visible content only: one at the end of the paragraph is
    // dropped, as are spaces at the start of a line.
    private void FlushSpace()
    {
        if (_break)
        {
            _text.Append("\\\n").Append(indent);
            (_break, _lineStart, _space) = (false, true, string.Empty);
        }

        if (!_lineStart)
        {
            _text.Append(MarkdownWriter.EscapeInline(_space));
        }

        _space = string.Empty;
    }

    private void Append(string text)
    {
        _text.Append(_lineStart ? MarkdownWriter.Escape(text) : MarkdownWriter.EscapeInline(text));
        _lineStart = false;
    }

    private void Raw(string markdown)
    {
        FlushSpace();
        _text.Append(markdown);
        _lineStart = false;
    }

    private void Break(WordBreak lineBreak)
    {
        if (lineBreak.Kind == WordBreakKind.Page)
        {
            context.Gaps.Add("page breaks are left out");
            return;
        }

        if (lineBreak.Kind == WordBreakKind.Column)
        {
            context.Gaps.Add("column breaks are written as line breaks");
        }

        if (singleLine)
        {
            _space += " ";
            return;
        }

        _break |= !_lineStart;
    }

    private void Link(WordHyperlink link)
    {
        var href = Href(link);
        _inLink = true;
        if (href is null)
        {
            Inlines(link.Inlines);
            _inLink = false;
            return;
        }

        Close(0);
        Raw("[");
        var label = _text.Length;
        Inlines(link.Inlines);
        _inLink = false;
        Close(0);
        if (_text.Length == label)
        {
            _text.Append(MarkdownWriter.EscapeInline(href));
        }

        _text.Append("](").Append(href.Contains('(', StringComparison.Ordinal) || href.Contains(')', StringComparison.Ordinal) ? "<" + href + ">" : href).Append(')');
    }

    // Only http, https and mailto addresses are kept; any other link, a bookmark included, keeps its text only.
    private string? Href(WordHyperlink link)
    {
        if (link.Target is { } target)
        {
            var compact = new string(target.Where(c => !char.IsControl(c) && !char.IsWhiteSpace(c)).ToArray());
            if (MarkdownUrl.SchemeOf(compact) is { } scheme && SafeSchemes.Contains(scheme, StringComparer.OrdinalIgnoreCase))
            {
                return MarkdownUrl.Normalize(compact);
            }

            context.Gaps.Add("links with an address other than http, https or mailto keep their text only");
            return null;
        }

        if (link.Anchor is { Length: > 0 })
        {
            context.Gaps.Add("links to bookmarks keep their text only");
        }

        return null;
    }

    private void Note(WordNoteReference note)
    {
        // The mark at the start of a note's own text is replaced by the footnote definition.
        if (note.IsMark || context.NoteLabel(note.Kind, note.Id) is not { } label)
        {
            return;
        }

        Raw("[^" + label + "]");
    }

    private void Picture(WordPicture picture)
    {
        if (picture.Floating is not null)
        {
            context.Gaps.Add("floating pictures are written in the text flow");
        }

        var name = context.Images.Reference(picture.Image);
        Raw("![" + MarkdownWriter.EscapeInline(picture.Description ?? string.Empty) + "][" + name + "]");
    }
}
