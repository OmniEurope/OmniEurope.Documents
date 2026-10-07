// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion.WordLayout;
using OmniEurope.Documents.Markdown;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordHtml;

/// <summary>
/// Writes the inline content of one paragraph: the list label, then runs grouped while their resolved
/// formatting stays the same (bold, italic, underline, strike, superscript and subscript as elements, the
/// rest as a style), breaks, field results, links, note references, pictures. Text boxes are collected for
/// the paragraph writer to place after the paragraph, since a block may not sit inside it.
/// </summary>
internal sealed class HtmlInlineWriter(HtmlContext context, HtmlOutput output, WordParagraphProperties paragraph, CellStyle? cell, WordRunProperties paragraphRun)
{
    private static readonly string[] SafeSchemes = ["http", "https", "mailto"];
    private readonly List<string> _tags = [];
    private RunKey? _open;

    /// <summary>The text boxes met, in order.</summary>
    public List<WordTextBox> TextBoxes { get; } = [];

    /// <summary>True once something visible was written.</summary>
    public bool HasContent { get; private set; }

    /// <summary>Writes the list label of a numbered paragraph and what follows it.</summary>
    public void Label()
    {
        if (paragraph.NumberingId is not > 0 || context.Lists.Next(paragraph.NumberingId.Value, paragraph.NumberingLevel ?? 0) is not { } next)
        {
            return;
        }

        var properties = Resolve((paragraph.MarkProperties ?? WordRunProperties.Empty).Overlay(next.Level.RunProperties));
        var hanging = next.Level.Suffix == WordLabelSuffix.Tab && paragraph.FirstLineIndent < 0 ? -paragraph.FirstLineIndent : null;
        output.Open("span", ("class", "omni-label"), ("style", hanging is { } width ? "display:inline-block;min-width:" + HtmlCss.Points(width) : null));
        if (next.Label.Length > 0)
        {
            Switch(new RunKey(properties, null));
            output.Text(Symbols.Map(next.Label, properties.Font, context.Gaps));
        }

        CloseRun();
        output.Close("span");
        output.Raw(next.Level.Suffix switch
        {
            WordLabelSuffix.Tab when hanging is null => "\t",
            WordLabelSuffix.Space => " ",
            _ => string.Empty,
        });
        HasContent = true;
    }

    /// <summary>A link back to the reference of a note whose text has no mark of its own.</summary>
    public void BackLink(WordNoteKind kind, int id) => NoteLink(kind, id, isMark: true);

    public void Write(IEnumerable<WordInline> inlines)
    {
        foreach (var inline in inlines)
        {
            if (!context.Skips(inline.Revision))
            {
                Inline(inline);
            }
        }
    }

    /// <summary>Closes the run still open.</summary>
    public void End() => CloseRun();

    private void Inline(WordInline inline)
    {
        switch (inline)
        {
            case WordText text:
                Text(text.Value, inline, null);
                break;
            case WordTab:
                context.Gaps.Add("tab stops shown as tab characters of fixed width");
                Text("\t", inline, null);
                break;
            case WordSymbol symbol:
                Text(symbol.Character.ToString(), inline, symbol.Font);
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
                Field(field);
                break;
            case WordHyperlink link:
                Link(link);
                break;
            case WordNoteReference note:
                NoteLink(note.Kind, note.Id, note.IsMark);
                break;
            case WordPicture picture:
                Picture(picture);
                break;
            case WordTextBox box:
                Floating(box);
                TextBoxes.Add(box);
                break;
        }
    }

    private void Text(string text, WordInline inline, string? symbolFont)
    {
        var properties = Resolve(inline.Properties);
        if (properties.Hidden == true || text.Length == 0)
        {
            return;
        }

        var font = symbolFont ?? properties.Font;
        Switch(new RunKey(properties, context.Marked ? inline.Revision?.Kind : null));
        output.Text(Symbols.IsSymbolFont(font) ? Symbols.Map(text, font, context.Gaps) : text);
        HasContent = true;
    }

    private void Break(WordBreak lineBreak)
    {
        if (lineBreak.Kind == WordBreakKind.Page)
        {
            CloseRun();
            output.Raw("<span class=\"omni-page-break\"></span>");
            return;
        }

        if (lineBreak.Kind == WordBreakKind.Column)
        {
            context.Gaps.Add("column breaks shown as line breaks");
        }

        output.Raw("<br>");
        HasContent = true;
    }

    private void Field(WordField field)
    {
        if (field.Kind is "PAGE" or "NUMPAGES" or "SECTIONPAGES")
        {
            context.Gaps.Add("page number fields show their last computed value");
        }

        Write(field.Result);
    }

    private void Link(WordHyperlink link)
    {
        var href = Href(link);
        CloseRun();
        if (href is not null)
        {
            output.Open("a", ("href", href));
        }

        Write(link.Inlines);
        CloseRun();
        if (href is not null)
        {
            output.Close("a");
        }
    }

    // Only http, https and mailto addresses are kept; a link to a bookmark becomes a fragment.
    private string? Href(WordHyperlink link)
    {
        if (link.Target is { } target)
        {
            var compact = new string(target.Where(c => !char.IsControl(c) && !char.IsWhiteSpace(c)).ToArray());
            if (MarkdownUrl.SchemeOf(compact) is { } scheme && SafeSchemes.Contains(scheme, StringComparer.OrdinalIgnoreCase))
            {
                return MarkdownUrl.Normalize(compact);
            }

            context.Gaps.Add("links with an address other than http, https or mailto are left without their address");
            return null;
        }

        if (link.Anchor is { Length: > 0 } anchor)
        {
            context.Gaps.Add("bookmarks are not kept: links to them point to anchors the page does not define");
            return "#" + MarkdownUrl.Normalize(anchor);
        }

        return null;
    }

    private void NoteLink(WordNoteKind kind, int id, bool isMark)
    {
        CloseRun();
        var label = context.Notes.Label(kind, id);
        var href = "#" + HtmlContext.NoteId(kind, id, reference: isMark);
        var anchor = !isMark && context.FirstReference(kind, id) ? HtmlContext.NoteId(kind, id, reference: true) : null;
        output.Raw("<sup class=\"omni-noteref\">");
        output.Open("a", ("href", href), ("id", anchor), ("class", isMark ? "omni-backlink" : null));
        output.Text(label);
        output.Close("a");
        output.Raw("</sup>");
        HasContent = true;
    }

    private void Picture(WordPicture picture)
    {
        CloseRun();
        Floating(picture);
        if (context.Images.Source(picture) is not { } source)
        {
            return;
        }

        var size = "width:" + HtmlCss.Points(picture.Width) + ";height:" + HtmlCss.Points(picture.Height);
        output.Open("img", ("src", source), ("alt", picture.Description ?? string.Empty), ("style", size));
        HasContent = true;
    }

    private void Floating(WordShape shape)
    {
        if (shape.Floating is not null)
        {
            context.Gaps.Add("floating shapes are shown in the text flow");
        }
    }

    private WordRunProperties Resolve(WordRunProperties direct) => WordResolution.Run(context.Styles, paragraph, direct, cell);

    private void Switch(RunKey key)
    {
        if (_open == key)
        {
            return;
        }

        CloseRun();
        _open = key;
        OpenRevision(key.Revision);
        if (HtmlCss.Join(HtmlCss.Run(key.Properties, paragraphRun)) is { } style)
        {
            Open("span", ("style", style));
        }

        OpenEffects(key.Properties);
    }

    private void OpenRevision(WordRevisionKind? revision)
    {
        if (revision is { } kind)
        {
            Open(kind == WordRevisionKind.Inserted ? "ins" : "del");
        }
    }

    private void OpenEffects(WordRunProperties p)
    {
        OpenIf(p.Bold == true, "strong");
        OpenIf(p.Italic == true, "em");
        if (p.Underline is { } underline and not WordUnderline.None)
        {
            Open("u", ("style", HtmlCss.UnderlineStyle(underline)));
        }

        if (p.Strike == true || p.DoubleStrike == true)
        {
            Open("s", ("style", p.DoubleStrike == true ? "text-decoration-style:double" : null));
        }

        OpenIf(p.VerticalPosition == WordVerticalPosition.Superscript, "sup");
        OpenIf(p.VerticalPosition == WordVerticalPosition.Subscript, "sub");
    }

    private void OpenIf(bool condition, string tag)
    {
        if (condition)
        {
            Open(tag);
        }
    }

    private void Open(string tag, params ReadOnlySpan<(string Name, string? Value)> attributes)
    {
        output.Open(tag, attributes);
        _tags.Add(tag);
    }

    private void CloseRun()
    {
        for (var i = _tags.Count - 1; i >= 0; i--)
        {
            output.Close(_tags[i]);
        }

        _tags.Clear();
        _open = null;
    }

    private sealed record RunKey(WordRunProperties Properties, WordRevisionKind? Revision);
}
