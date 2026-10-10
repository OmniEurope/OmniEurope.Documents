// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>Resolves run formatting inside one paragraph (and table cell, where the table style and its
/// conditional formats apply).</summary>
internal sealed class RunResolver(LayoutContext context, WordParagraphProperties paragraph, CellStyle? cell)
{
    public LayoutContext Context { get; } = context;

    public WordParagraphProperties Paragraph { get; } = paragraph;

    public WordRunProperties Resolve(WordRunProperties direct) => WordResolution.Run(Context.Styles, Paragraph, direct, cell);

    public TextStyle Style(WordRunProperties direct, string? link = null) => TextStyle.From(Resolve(direct), link);
}

/// <summary>Formatting a table cell adds to its content: the table style and its conditional formats.</summary>
internal sealed record CellStyle(string? TableStyleId, WordParagraphProperties? ParagraphOverlay, WordRunProperties? RunOverlay);

/// <summary>
/// Turns the inline content of a paragraph into tokens: the list label first, then words and spaces in
/// their resolved style, tabs, breaks, page fields, note marks and pictures. Deleted revisions and hidden
/// text are left out; other fields show their last result.
/// </summary>
internal sealed class InlineBuilder(RunResolver resolver, ShapeFactory shapes, bool numbering)
{
    private readonly List<Token> _tokens = [];
    private bool _breakNext;

    private LayoutContext Context => resolver.Context;

    public List<Token> Build(WordParagraph paragraph)
    {
        if (numbering)
        {
            AddLabel(resolver.Paragraph);
        }

        AddInlines(paragraph.Inlines, null);
        return _tokens;
    }

    private void AddLabel(WordParagraphProperties paragraph)
    {
        if (paragraph.NumberingId is not > 0 || Context.Lists.Next(paragraph.NumberingId.Value, paragraph.NumberingLevel ?? 0) is not { } next)
        {
            return;
        }

        var properties = (paragraph.MarkProperties ?? WordRunProperties.Empty).Overlay(next.Level.RunProperties);
        var font = resolver.Resolve(properties).Font;
        var style = resolver.Style(properties);
        style = Symbols.Style(font, style);
        if (next.Label.Length > 0)
        {
            AddText(Symbols.Map(next.Label, font, Context.Gaps), style, null);
        }

        switch (next.Level.Suffix)
        {
            case WordLabelSuffix.Tab:
                Add(new TabToken(style, null));
                break;
            case WordLabelSuffix.Space:
                AddText(" ", style, null);
                break;
        }
    }

    private void AddInlines(IEnumerable<WordInline> inlines, string? link)
    {
        foreach (var inline in inlines)
        {
            if (inline.Revision?.Kind != WordRevisionKind.Deleted)
            {
                AddInline(inline, link);
            }
        }
    }

    private void AddInline(WordInline inline, string? link)
    {
        var style = resolver.Style(inline.Properties, link);
        if (style.Hidden)
        {
            return;
        }

        switch (inline)
        {
            case WordText text when Symbols.IsSymbolFont(style.Font.Family):
                AddText(Symbols.Map(text.Value, style.Font.Family, Context.Gaps), Symbols.Style(style.Font.Family, style), null);
                break;
            case WordText text:
                AddText(text.Value, style, null);
                break;
            case WordTab tab:
                Add(new TabToken(style, tab.Alignment is null ? null : tab));
                break;
            case WordBreak lineBreak:
                Add(new BreakToken(style, lineBreak.Kind));
                _breakNext = true;
                break;
            case WordSymbol symbol:
                AddText(Symbols.Map(symbol.Character.ToString(), symbol.Font, Context.Gaps), Symbols.Style(symbol.Font, style), null);
                break;
            default:
                AddComplex(inline, style, link);
                break;
        }
    }

    private void AddComplex(WordInline inline, TextStyle style, string? link)
    {
        switch (inline)
        {
            case WordField field when field.Kind is "PAGE" or "NUMPAGES" or "SECTIONPAGES":
                var sample = field.Result.Count > 0 ? WordInline.TextOf(field.Result) : "1";
                Add(new FieldToken(style, field.Kind) { Width = Context.Measure(sample.Length > 0 ? sample : "1", style) });
                break;
            case WordField field:
                AddInlines(field.Result, link);
                break;
            case WordHyperlink hyperlink:
                AddInlines(hyperlink.Inlines, hyperlink.Target ?? link);
                break;
            case WordNoteReference note:
                var label = Context.Notes.Label(note.Kind, note.Id);
                AddText(label, style, note.IsMark ? null : (note.Kind, note.Id));
                break;
            case WordShape shape:
                AddShape(shape, style);
                break;
        }
    }

    private void AddShape(WordShape shape, TextStyle style)
    {
        var paint = shapes.Painter(shape);
        if (shape.Floating is not null)
        {
            Add(new AnchorToken(style, shape, paint));
            return;
        }

        Add(new BoxToken(style, shape.Width, shape.Height, paint) { Width = shape.Width });
    }

    // Words and runs of spaces become separate tokens; a line may break after a space or a hyphen.
    private void AddText(string text, TextStyle style, (WordNoteKind, int)? note)
    {
        var start = 0;
        while (start < text.Length)
        {
            var space = IsBreakingSpace(text[start]);
            var end = start + 1;
            while (end < text.Length && IsBreakingSpace(text[end]) == space && !(text[end - 1] is '-' or '\u00AD' && !space))
            {
                end++;
            }

            AddChunk(text[start..end], style, note, space);
            start = end;
        }
    }

    private void AddChunk(string chunk, TextStyle style, (WordNoteKind, int)? note, bool space)
    {
        var breakBefore = _breakNext;
        _breakNext = space || chunk[^1] is '-' or '\u00AD';
        chunk = chunk.Replace("\u00AD", string.Empty, StringComparison.Ordinal);
        if (chunk.Length == 0)
        {
            return;
        }

        foreach (var (text, pieceStyle) in Capitalise(chunk, style))
        {
            var token = new TextToken(text, pieceStyle) { Note = note, BreakBefore = breakBefore };
            token.Width = Context.Measure(text, pieceStyle);
            Add(token);
            breakBefore = false;
        }
    }

    // Small capitals draw lower-case letters as smaller capitals.
    private static IEnumerable<(string Text, TextStyle Style)> Capitalise(string text, TextStyle style)
    {
        if (style.Caps)
        {
            yield return (text.ToUpperInvariant(), style);
            yield break;
        }

        if (!style.SmallCaps)
        {
            yield return (text, style);
            yield break;
        }

        var small = style with { Size = style.Size * 0.8 };
        var start = 0;
        for (var i = 1; i <= text.Length; i++)
        {
            if (i == text.Length || char.IsLower(text[i]) != char.IsLower(text[start]))
            {
                var piece = text[start..i];
                yield return char.IsLower(piece[0]) ? (piece.ToUpperInvariant(), small) : (piece, style);
                start = i;
            }
        }
    }

    private void Add(Token token)
    {
        if (token is not TextToken)
        {
            token.BreakBefore = _breakNext || token is TabToken;
            _breakNext = token is TabToken or BoxToken;
        }

        _tokens.Add(token);
    }

    // No-break spaces (U+00A0, U+202F) and figure spaces keep words together.
    private static bool IsBreakingSpace(char c) => c is ' ' or '\u2002' or '\u2003' or '\u2004' or '\u2005' or '\u2006' or '\u2008' or '\u2009' or '\u200A' or '\u3000';
}
