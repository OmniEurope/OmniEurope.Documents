// SPDX-License-Identifier: EUPL-1.2
using static OmniEurope.Documents.Word.WordInherit;

namespace OmniEurope.Documents.Word;

/// <summary>Unit of a <see cref="WordWidth"/>.</summary>
public enum WordWidthType
{
    /// <summary>Sized by content.</summary>
    Auto,

    /// <summary>Points.</summary>
    Points,

    /// <summary>Percent of the available width (100 is full width).</summary>
    Percent,

    /// <summary>Zero width.</summary>
    Nil,
}

/// <summary>A table or cell width.</summary>
public readonly record struct WordWidth(double Value, WordWidthType Type)
{
    /// <summary>A width in points.</summary>
    public static WordWidth Points(double value) => new(value, WordWidthType.Points);

    /// <summary>A width in percent of the available width.</summary>
    public static WordWidth Percent(double value) => new(value, WordWidthType.Percent);

    /// <summary>Automatic width.</summary>
    public static WordWidth Auto => new(0, WordWidthType.Auto);
}

/// <summary>How a cell takes part in a vertical merge.</summary>
public enum WordVerticalMerge
{
    /// <summary>Not merged.</summary>
    None,

    /// <summary>First cell of a vertical merge.</summary>
    Restart,

    /// <summary>Continues the merge of the cell above.</summary>
    Continue,
}

/// <summary>Vertical alignment of cell content.</summary>
public enum WordCellAlignment
{
    /// <summary>Top.</summary>
    Top,

    /// <summary>Centre.</summary>
    Center,

    /// <summary>Bottom.</summary>
    Bottom,
}

/// <summary>How a row height applies.</summary>
public enum WordRowHeightRule
{
    /// <summary>At least this height.</summary>
    AtLeast,

    /// <summary>Exactly this height.</summary>
    Exact,
}

/// <summary>Parts of a table that receive the conditional formats of its style.</summary>
[Flags]
public enum WordTableLook
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary>The header row format.</summary>
    FirstRow = 1,

    /// <summary>The total row format.</summary>
    LastRow = 2,

    /// <summary>The first column format.</summary>
    FirstColumn = 4,

    /// <summary>The last column format.</summary>
    LastColumn = 8,

    /// <summary>No banded rows.</summary>
    NoHorizontalBanding = 16,

    /// <summary>No banded columns.</summary>
    NoVerticalBanding = 32,
}

/// <summary>Borders of a table or cell; the inside borders separate rows and columns.</summary>
public sealed record WordTableBorders(WordBorder? Top = null, WordBorder? Left = null, WordBorder? Bottom = null, WordBorder? Right = null, WordBorder? InsideHorizontal = null, WordBorder? InsideVertical = null)
{
    /// <summary>Single half-point lines everywhere.</summary>
    public static WordTableBorders Grid(double width = 0.5, string color = "auto")
    {
        var line = new WordBorder("single", width, 0, color);
        return new WordTableBorders(line, line, line, line, line, line);
    }

    /// <summary>The borders of <paramref name="top"/> that are set replace these.</summary>
    public WordTableBorders Overlay(WordTableBorders? top) => top is null ? this : new WordTableBorders(
        Pick(top.Top, Top), Pick(top.Left, Left), Pick(top.Bottom, Bottom), Pick(top.Right, Right),
        Pick(top.InsideHorizontal, InsideHorizontal), Pick(top.InsideVertical, InsideVertical));
}

/// <summary>Cell margins in points.</summary>
public sealed record WordCellMargins(double? Top = null, double? Left = null, double? Bottom = null, double? Right = null);

/// <summary>Table formatting; null members are inherited from the table style.</summary>
public sealed record WordTableProperties
{
    /// <summary>No formatting.</summary>
    public static WordTableProperties Empty { get; } = new();

    /// <summary>Table style id.</summary>
    public string? StyleId { get; init; }

    /// <summary>Preferred width.</summary>
    public WordWidth? Width { get; init; }

    /// <summary>Horizontal alignment of the table.</summary>
    public WordAlignment? Alignment { get; init; }

    /// <summary>Indent from the start margin, in points.</summary>
    public double? Indent { get; init; }

    /// <summary>Column widths are fixed instead of fitted to content.</summary>
    public bool? FixedLayout { get; init; }

    /// <summary>Borders.</summary>
    public WordTableBorders? Borders { get; init; }

    /// <summary>Default cell margins.</summary>
    public WordCellMargins? CellMargins { get; init; }

    /// <summary>Background fill as <c>RRGGBB</c>.</summary>
    public string? Shading { get; init; }

    /// <summary>Which conditional formats of the style apply.</summary>
    public WordTableLook? Look { get; init; }

    /// <summary>The members of <paramref name="top"/> that are set replace these.</summary>
    public WordTableProperties Overlay(WordTableProperties? top) => top is null ? this : new WordTableProperties
    {
        StyleId = Pick(top.StyleId, StyleId),
        Width = Pick(top.Width, Width),
        Alignment = Pick(top.Alignment, Alignment),
        Indent = Pick(top.Indent, Indent),
        FixedLayout = Pick(top.FixedLayout, FixedLayout),
        Borders = Borders is null ? top.Borders : Borders.Overlay(top.Borders),
        CellMargins = Pick(top.CellMargins, CellMargins),
        Shading = Pick(top.Shading, Shading),
        Look = Pick(top.Look, Look),
    };
}

/// <summary>Row formatting.</summary>
public sealed record WordTableRowProperties
{
    /// <summary>No formatting.</summary>
    public static WordTableRowProperties Empty { get; } = new();

    /// <summary>Row height in points.</summary>
    public double? Height { get; init; }

    /// <summary>How <see cref="Height"/> applies.</summary>
    public WordRowHeightRule? HeightRule { get; init; }

    /// <summary>Repeated at the top of each page.</summary>
    public bool? IsHeader { get; init; }

    /// <summary>Never split across pages.</summary>
    public bool? CantSplit { get; init; }

    /// <summary>Grid columns skipped before the first cell.</summary>
    public int? GridBefore { get; init; }

    /// <summary>Grid columns skipped after the last cell.</summary>
    public int? GridAfter { get; init; }
}

/// <summary>Cell formatting.</summary>
public sealed record WordTableCellProperties
{
    /// <summary>No formatting.</summary>
    public static WordTableCellProperties Empty { get; } = new();

    /// <summary>Preferred width.</summary>
    public WordWidth? Width { get; init; }

    /// <summary>Number of grid columns the cell spans.</summary>
    public int? GridSpan { get; init; }

    /// <summary>Vertical merge with the cells above and below.</summary>
    public WordVerticalMerge? VerticalMerge { get; init; }

    /// <summary>Vertical alignment of the content.</summary>
    public WordCellAlignment? VerticalAlignment { get; init; }

    /// <summary>Background fill as <c>RRGGBB</c>.</summary>
    public string? Shading { get; init; }

    /// <summary>Borders (override the table's).</summary>
    public WordTableBorders? Borders { get; init; }

    /// <summary>Margins (override the table's).</summary>
    public WordCellMargins? Margins { get; init; }

    /// <summary>Text is not wrapped.</summary>
    public bool? NoWrap { get; init; }

    /// <summary>Spreadsheet content on one line: laid out at its own width, placed by its alignment and clipped
    /// to the cell widened by the empty neighbours it runs over. Used by the conversions, never read from or
    /// written to a package.</summary>
    internal WordCellOverflow? Overflow { get; init; }

    /// <summary>The members of <paramref name="top"/> that are set replace these.</summary>
    public WordTableCellProperties Overlay(WordTableCellProperties? top) => top is null ? this : new WordTableCellProperties
    {
        Width = Pick(top.Width, Width),
        GridSpan = Pick(top.GridSpan, GridSpan),
        VerticalMerge = Pick(top.VerticalMerge, VerticalMerge),
        VerticalAlignment = Pick(top.VerticalAlignment, VerticalAlignment),
        Shading = Pick(top.Shading, Shading),
        Borders = Borders is null ? top.Borders : Borders.Overlay(top.Borders),
        Margins = Pick(top.Margins, Margins),
        NoWrap = Pick(top.NoWrap, NoWrap),
        Overflow = top.Overflow ?? Overflow,
    };
}

/// <summary>One line of cell content: its width (points), the side it is anchored to, and how far (points) it
/// may run past the left and right edges of its cell.</summary>
internal sealed record WordCellOverflow(double Width, WordAlignment Alignment, double ExtendLeft = 0, double ExtendRight = 0);

/// <summary>Part of a table that a conditional format of a table style targets.</summary>
public enum WordTableRegion
{
    /// <summary>The whole table.</summary>
    WholeTable,

    /// <summary>Header row.</summary>
    FirstRow,

    /// <summary>Total row.</summary>
    LastRow,

    /// <summary>First column.</summary>
    FirstColumn,

    /// <summary>Last column.</summary>
    LastColumn,

    /// <summary>Odd banded rows.</summary>
    OddRows,

    /// <summary>Even banded rows.</summary>
    EvenRows,

    /// <summary>Odd banded columns.</summary>
    OddColumns,

    /// <summary>Even banded columns.</summary>
    EvenColumns,

    /// <summary>Top left corner cell.</summary>
    TopLeftCell,

    /// <summary>Top right corner cell.</summary>
    TopRightCell,

    /// <summary>Bottom left corner cell.</summary>
    BottomLeftCell,

    /// <summary>Bottom right corner cell.</summary>
    BottomRightCell,
}

/// <summary>Formatting a table style applies to one region of the table.</summary>
public sealed record WordTableConditionalFormat(
    WordTableRegion Region,
    WordParagraphProperties? ParagraphProperties = null,
    WordRunProperties? RunProperties = null,
    WordTableProperties? TableProperties = null,
    WordTableCellProperties? CellProperties = null);
