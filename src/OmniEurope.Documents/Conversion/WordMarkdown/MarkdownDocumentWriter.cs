// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using OmniEurope.Documents.Markdown;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordMarkdown;

/// <summary>
/// Writes the blocks of the document body, then the footnote definitions and the picture references. Blocks
/// are separated by a blank line, list items by a line break only (tight lists); a list item is indented under
/// the content of the items it nests in, as Markdown reads nesting.
/// </summary>
internal sealed class MarkdownDocumentWriter(MarkdownContext context)
{
    private readonly StringBuilder _markdown = new();
    private readonly List<int> _listWidths = [];
    private bool _previousIsItem;

    public string Write()
    {
        NoteLeftOut();
        Blocks(context.Document.Blocks.ToList());
        Notes();
        Images();
        return _markdown.Length == 0 ? string.Empty : _markdown.Append('\n').ToString();
    }

    private void NoteLeftOut()
    {
        if (context.Document.Sections.Any(s => s.Headers.Values.Concat(s.Footers.Values).Any(h => h.Blocks.Count > 0)))
        {
            context.Gaps.Add("headers and footers are left out");
        }

        if (context.Document.Comments.Count > 0)
        {
            context.Gaps.Add("comments are left out");
        }
    }

    private void Blocks(IReadOnlyList<WordBlock> blocks)
    {
        foreach (var block in blocks)
        {
            if (block is WordTable table)
            {
                Table(table);
            }
            else
            {
                Paragraph((WordParagraph)block);
            }
        }
    }

    private void Paragraph(WordParagraph paragraph)
    {
        var resolved = WordResolution.Paragraph(context.Document, paragraph, null);
        var heading = resolved.OutlineLevel is >= 0 and < 6 ? resolved.OutlineLevel.Value + 1 : 0;
        var item = heading == 0 && resolved.NumberingId is > 0 ? context.Lists.Next(resolved.NumberingId.Value, resolved.NumberingLevel ?? 0) : null;
        string text;
        List<WordTextBox> boxes;
        if (item is { } list)
        {
            var (marker, indent) = Item(list.Level, list.Number, resolved.NumberingLevel ?? 0);
            (text, boxes) = Content(paragraph, resolved, indent + new string(' ', marker.Length), singleLine: false);
            Add(text.Length == 0 ? string.Empty : indent + marker + text, isItem: true);
        }
        else
        {
            var label = heading > 0 ? HeadingLabel(resolved) : string.Empty;
            (text, boxes) = Content(paragraph, resolved, string.Empty, singleLine: heading > 0);
            text = label.Length > 0 && text.Length > 0 ? MarkdownWriter.Escape(label) + " " + text : text;
            Add(heading > 0 && text.Length > 0 ? new string('#', heading) + " " + text : text, isItem: false);
        }

        foreach (var box in boxes)
        {
            context.Gaps.Add("text boxes are written as paragraphs after the paragraph that anchors them");
            Blocks(box.Blocks);
        }
    }

    private (string Text, List<WordTextBox> Boxes) Content(WordParagraph paragraph, WordParagraphProperties resolved, string indent, bool singleLine)
    {
        var baseline = singleLine ? WordResolution.Run(context.Document.Styles, resolved, WordRunProperties.Empty, null) : null;
        var writer = new MarkdownInlineWriter(context, resolved, indent, singleLine, baseline);
        var text = writer.Write(paragraph.Inlines);
        if (text.Length == 0)
        {
            context.Gaps.Add("empty paragraphs are left out");
        }

        return (text, writer.TextBoxes);
    }

    // The label of a numbered heading ("2.1"), counted with the document's own rules.
    private string HeadingLabel(WordParagraphProperties resolved) =>
        resolved.NumberingId is > 0 && context.HeadingLabels.Next(resolved.NumberingId.Value, resolved.NumberingLevel ?? 0) is { } next
            ? next.Label.Trim()
            : string.Empty;

    // A level deeper than the open lists allow nests one level down only, as Markdown cannot skip a level.
    private (string Marker, string Indent) Item(WordNumberingLevel level, int number, int depth)
    {
        if (!_previousIsItem)
        {
            _listWidths.Clear();
        }

        depth = Math.Min(depth, _listWidths.Count);
        _listWidths.RemoveRange(depth, _listWidths.Count - depth);
        var bullet = level.Format is WordNumberFormat.Bullet or WordNumberFormat.None;
        if (!bullet && level.Format is not (WordNumberFormat.Decimal or WordNumberFormat.DecimalZero))
        {
            context.Gaps.Add("list numbers in letters, roman numerals or words are written as numbers");
        }

        var marker = bullet ? "- " : number.ToString(CultureInfo.InvariantCulture) + ". ";
        var indent = new string(' ', _listWidths.Sum());
        _listWidths.Add(marker.Length);
        return (marker, indent);
    }

    private void Table(WordTable table)
    {
        var rows = table.Rows.Where(r => !MarkdownContext.Skips(r.Revision)).Select(Row).ToList();
        var width = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
        if (width == 0)
        {
            return;
        }

        var markdown = new StringBuilder();
        for (var r = 0; r < rows.Count; r++)
        {
            markdown.Append('|');
            for (var c = 0; c < width; c++)
            {
                markdown.Append(' ').Append(c < rows[r].Count ? rows[r][c] : string.Empty).Append(" |");
            }

            if (r == 0)
            {
                markdown.Append('\n').Append('|').Append(string.Concat(Enumerable.Repeat(" --- |", width)));
            }

            markdown.Append(r + 1 < rows.Count ? "\n" : string.Empty);
        }

        Add(markdown.ToString(), isItem: false);
    }

    // A merged cell keeps its text in its first grid column; the columns it spans and the cells continuing a
    // vertical merge stay empty.
    private List<string> Row(WordTableRow row)
    {
        var cells = new List<string>();
        foreach (var cell in row.Cells)
        {
            var span = Math.Max(cell.Properties.GridSpan ?? 1, 1);
            var continued = cell.Properties.VerticalMerge == WordVerticalMerge.Continue;
            if (span > 1 || continued)
            {
                context.Gaps.Add("merged table cells keep their text in the first cell, the cells they cover are empty");
            }

            cells.Add(continued ? string.Empty : Cell(cell.Blocks));
            cells.AddRange(Enumerable.Repeat(string.Empty, span - 1));
        }

        return cells;
    }

    // A cell is one line of a pipe table: its paragraphs are joined with spaces, a nested table gives its text.
    private string Cell(IReadOnlyList<WordBlock> blocks)
    {
        var parts = new List<string>();
        foreach (var block in blocks)
        {
            if (block is WordTable nested)
            {
                context.Gaps.Add("tables inside table cells are written as their text");
                parts.Add(MarkdownWriter.Escape(nested.Text.Replace('\t', ' ')));
                continue;
            }

            parts.Add(Line((WordParagraph)block));
        }

        if (parts.Count(p => p.Length > 0) > 1)
        {
            context.Gaps.Add("several paragraphs in a table cell or a note are joined on one line");
        }

        return string.Join(' ', parts.Where(p => p.Length > 0));
    }

    private string Line(WordParagraph paragraph)
    {
        var resolved = WordResolution.Paragraph(context.Document, paragraph, null);
        if (resolved.NumberingId is > 0)
        {
            context.Gaps.Add("list items inside table cells and notes are written as plain paragraphs");
        }

        var writer = new MarkdownInlineWriter(context, resolved, string.Empty, singleLine: true);
        var text = writer.Write(paragraph.Inlines);
        if (writer.TextBoxes.Count > 0)
        {
            context.Gaps.Add("text boxes inside table cells and notes are left out");
        }

        return text;
    }

    // Notes referred to inside other notes are numbered as they are met, so the loop runs to the end of a
    // list that may grow.
    private void Notes()
    {
        for (var i = 0; i < context.Notes.Count; i++)
        {
            var (kind, id, label) = context.Notes[i];
            var note = kind == WordNoteKind.Footnote ? context.Document.Footnotes[id] : context.Document.Endnotes[id];
            Add("[^" + label + "]: " + Cell(note.Blocks), isItem: false);
        }
    }

    private void Images()
    {
        var images = context.Images.All;
        if (images.Count == 0)
        {
            return;
        }

        Add(string.Join('\n', images.Select(i => "[" + MarkdownImages.Name(i) + "]: " + i.Path)), isItem: false);
    }

    private void Add(string block, bool isItem)
    {
        if (block.Length == 0)
        {
            return;
        }

        if (_markdown.Length > 0)
        {
            _markdown.Append(isItem && _previousIsItem ? "\n" : "\n\n");
        }

        _markdown.Append(block);
        _previousIsItem = isItem;
    }
}
