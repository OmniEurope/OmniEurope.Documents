// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion.WordLayout;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordHtml;

/// <summary>
/// Writes blocks: a paragraph as a heading (outline levels 1 to 6) or a <c>p</c> element carrying its
/// address, its resolved formatting and its list label, followed by the text boxes it anchors; a table
/// through <see cref="HtmlTableWriter"/>. Space between two paragraphs of the same style is dropped on the
/// side that asks for it (contextual spacing).
/// </summary>
internal sealed class HtmlBlockWriter(HtmlContext context, HtmlOutput output)
{
    private (WordNoteKind Kind, int Id)? _backLink;

    /// <summary>Makes the next paragraph start with a link back to the reference of this note.</summary>
    public void StartWithBackLink(WordNoteKind kind, int id) => _backLink = (kind, id);

    public void Write(IReadOnlyList<WordBlock> blocks, CellStyle? cell = null, bool numbering = true)
    {
        var resolved = blocks.Select(b => b is WordParagraph p ? WordResolution.Paragraph(context.Document, p, cell) : null).ToList();
        for (var i = 0; i < blocks.Count; i++)
        {
            if (blocks[i] is WordTable table)
            {
                new HtmlTableWriter(context, output, this).Write(table);
                continue;
            }

            var spacing = Spacing(resolved[i]!, i > 0 ? resolved[i - 1] : null, i + 1 < blocks.Count ? resolved[i + 1] : null);
            Paragraph((WordParagraph)blocks[i], resolved[i]!, spacing, cell, numbering);
        }
    }

    private void Paragraph(WordParagraph paragraph, WordParagraphProperties p, IEnumerable<string> spacing, CellStyle? cell, bool numbering)
    {
        var tag = p.OutlineLevel is >= 0 and < 6 ? "h" + (p.OutlineLevel.Value + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : "p";
        var address = paragraph.SourceAddress;
        if (address is null)
        {
            context.Gaps.Add("paragraphs without a source address (built in code, or read from alternate-content fallback markup) have no data-address");
        }

        var highlighted = context.IsHighlighted(address);
        var run = WordResolution.Run(context.Styles, p, WordRunProperties.Empty, cell);
        var style = HtmlCss.Join(HtmlCss.Paragraph(p).Concat(spacing).Concat(HtmlCss.ParagraphFont(run)));
        output.Open(
            tag,
            ("id", highlighted ? WordToHtml.HighlightId : null),
            ("class", highlighted ? WordToHtml.HighlightClass : null),
            ("data-address", address),
            ("style", style));
        var inline = new HtmlInlineWriter(context, output, p, cell, run);
        WriteContent(paragraph, inline, numbering);
        output.Close(tag);
        output.Line();
        foreach (var box in inline.TextBoxes)
        {
            TextBox(box);
        }
    }

    private void WriteContent(WordParagraph paragraph, HtmlInlineWriter inline, bool numbering)
    {
        if (_backLink is { } note)
        {
            _backLink = null;
            inline.BackLink(note.Kind, note.Id);
        }

        if (numbering)
        {
            inline.Label();
        }

        inline.Write(paragraph.Inlines);
        inline.End();
        if (!inline.HasContent)
        {
            output.Raw("<br>");
        }
    }

    private static IEnumerable<string> Spacing(WordParagraphProperties p, WordParagraphProperties? previous, WordParagraphProperties? next)
    {
        var contextual = p.ContextualSpacing == true;
        var before = contextual && previous?.StyleId == p.StyleId ? 0 : p.SpacingBefore ?? 0;
        var after = contextual && next?.StyleId == p.StyleId ? 0 : p.SpacingAfter ?? 0;
        if (before > 0)
        {
            yield return "margin-top:" + HtmlCss.Points(before);
        }

        if (after > 0)
        {
            yield return "margin-bottom:" + HtmlCss.Points(after);
        }
    }

    private void TextBox(WordTextBox box)
    {
        var size = "width:" + HtmlCss.Points(box.Width) + ";min-height:" + HtmlCss.Points(box.Height);
        output.Open("div", ("class", "omni-textbox"), ("style", size));
        output.Line();
        Write(box.Blocks);
        output.Close("div");
        output.Line();
    }
}
