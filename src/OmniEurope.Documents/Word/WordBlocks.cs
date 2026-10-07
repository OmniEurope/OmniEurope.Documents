// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Word;

/// <summary>A block of a document flow: a <see cref="WordParagraph"/> or a <see cref="WordTable"/>.</summary>
public abstract class WordBlock
{
    private protected WordBlock()
    {
    }

    /// <summary>The visible text (deleted revisions excluded).</summary>
    public abstract string Text { get; }

    internal static string TextOf(IEnumerable<WordBlock> blocks) => string.Join('\n', blocks.Select(b => b.Text));
}

/// <summary>A paragraph: formatting and inline content.</summary>
public sealed class WordParagraph : WordBlock
{
    /// <summary>An empty paragraph.</summary>
    public WordParagraph()
    {
    }

    /// <summary>A paragraph holding <paramref name="text"/> (tabs and line feeds become tabs and breaks).</summary>
    public WordParagraph(string text, string? styleId = null)
    {
        Properties = new WordParagraphProperties { StyleId = styleId };
        AddText(text);
    }

    /// <summary>Paragraph formatting set on the paragraph itself.</summary>
    public WordParagraphProperties Properties { get; set; } = WordParagraphProperties.Empty;

    /// <summary>The paragraph style id.</summary>
    public string? StyleId
    {
        get => Properties.StyleId;
        set => Properties = Properties with { StyleId = value };
    }

    /// <summary>Inline content in order.</summary>
    public List<WordInline> Inlines { get; } = [];

    /// <summary>
    /// Where the paragraph was read from: the <see cref="Editing.WordEditableParagraph.Address"/> of the same
    /// paragraph in the loaded package (<c>part#index</c>). Null for a paragraph built in code or read from
    /// alternate-content fallback markup. It is not updated when blocks are added or removed.
    /// </summary>
    public string? SourceAddress { get; internal set; }

    /// <inheritdoc />
    public override string Text => WordInline.TextOf(Inlines);

    /// <summary>Appends text; tabs and line feeds become <see cref="WordTab"/> and <see cref="WordBreak"/>.</summary>
    public WordParagraph AddText(string text, WordRunProperties? properties = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        Inlines.AddRange(WordText.Split(text, properties ?? WordRunProperties.Empty));
        return this;
    }

    /// <summary>Appends an inline.</summary>
    public WordParagraph Add(WordInline inline)
    {
        ArgumentNullException.ThrowIfNull(inline);
        Inlines.Add(inline);
        return this;
    }

    /// <summary>Makes the paragraph an item of list <paramref name="numberingId"/> at <paramref name="level"/>.</summary>
    public WordParagraph AsListItem(int numberingId, int level = 0)
    {
        Properties = Properties with { NumberingId = numberingId, NumberingLevel = level };
        return this;
    }
}

/// <summary>A table: grid column widths and rows.</summary>
public sealed class WordTable : WordBlock
{
    /// <summary>An empty table.</summary>
    public WordTable()
    {
    }

    /// <summary>A table with grid columns of the given widths in points.</summary>
    public WordTable(params IEnumerable<double> columnWidths)
    {
        Columns.AddRange(columnWidths);
    }

    /// <summary>Table formatting.</summary>
    public WordTableProperties Properties { get; set; } = WordTableProperties.Empty;

    /// <summary>Widths of the grid columns, in points.</summary>
    public List<double> Columns { get; } = [];

    /// <summary>Rows.</summary>
    public List<WordTableRow> Rows { get; } = [];

    /// <summary>Cells separated by tabs, rows by line feeds.</summary>
    public override string Text => string.Join('\n', Rows.Select(r => string.Join('\t', r.Cells.Select(c => c.Text))));

    /// <summary>Appends a row with one single-paragraph cell per text.</summary>
    public WordTableRow AddRow(params IEnumerable<string> cells)
    {
        var row = new WordTableRow();
        foreach (var text in cells)
        {
            row.Cells.Add(new WordTableCell(text));
        }

        Rows.Add(row);
        return row;
    }
}

/// <summary>A table row.</summary>
public sealed class WordTableRow
{
    /// <summary>Row formatting.</summary>
    public WordTableRowProperties Properties { get; set; } = WordTableRowProperties.Empty;

    /// <summary>Cells.</summary>
    public List<WordTableCell> Cells { get; } = [];

    /// <summary>Set when the row itself was inserted or deleted with change tracking on.</summary>
    public WordRevision? Revision { get; set; }
}

/// <summary>A table cell holding blocks.</summary>
public sealed class WordTableCell
{
    /// <summary>An empty cell (one empty paragraph is written when it stays empty).</summary>
    public WordTableCell()
    {
    }

    /// <summary>A cell with one paragraph of text.</summary>
    public WordTableCell(string text)
    {
        Blocks.Add(new WordParagraph(text));
    }

    /// <summary>Cell formatting.</summary>
    public WordTableCellProperties Properties { get; set; } = WordTableCellProperties.Empty;

    /// <summary>Content.</summary>
    public List<WordBlock> Blocks { get; } = [];

    /// <summary>The text of its blocks, one per line.</summary>
    public string Text => WordBlock.TextOf(Blocks);
}

/// <summary>Kind of a tracked change.</summary>
public enum WordRevisionKind
{
    /// <summary>Inserted (or moved to).</summary>
    Inserted,

    /// <summary>Deleted (or moved from).</summary>
    Deleted,
}

/// <summary>A tracked change: kind, author and date.</summary>
public sealed record WordRevision(WordRevisionKind Kind, string? Author = null, DateTimeOffset? Date = null);

/// <summary>Plain-text helpers shared by blocks and inlines.</summary>
internal static class WordTextBuilder
{
    public static void Append(StringBuilder builder, IEnumerable<WordInline> inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline.Revision?.Kind != WordRevisionKind.Deleted)
            {
                inline.AppendText(builder);
            }
        }
    }
}
