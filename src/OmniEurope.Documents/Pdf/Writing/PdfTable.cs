// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Pdf.Writing;

/// <summary>The sides of a table cell that get a border line (see <see cref="PdfTable.Borders"/>). Combine with <c>|</c>.</summary>
[Flags]
public enum PdfBorderSides
{
    /// <summary>No border.</summary>
    None = 0,

    /// <summary>The top edge.</summary>
    Top = 1,

    /// <summary>The right edge.</summary>
    Right = 2,

    /// <summary>The bottom edge: a rule under the row when set on every cell of it.</summary>
    Bottom = 4,

    /// <summary>The left edge.</summary>
    Left = 8,

    /// <summary>The top and bottom edges.</summary>
    Horizontal = Top | Bottom,

    /// <summary>The left and right edges.</summary>
    Vertical = Left | Right,

    /// <summary>The four edges: the full grid.</summary>
    All = Top | Right | Bottom | Left,
}

/// <summary>A cell of a <see cref="PdfTable"/>.</summary>
public sealed record PdfTableCell(string Text)
{
    /// <summary>Border sides of this cell; the table's (<see cref="PdfTable.HeaderBorders"/> in a header row, then
    /// <see cref="PdfTable.Borders"/>) when null. Needs a <see cref="PdfTable.BorderColor"/>.</summary>
    public PdfBorderSides? Borders { get; init; }

    /// <summary>Font; the table's font when null.</summary>
    public PdfFont? Font { get; init; }

    /// <summary>Font size; the table's size when null.</summary>
    public double? Size { get; init; }

    /// <summary>Text colour.</summary>
    public PdfColor? Color { get; init; }

    /// <summary>Background fill.</summary>
    public PdfColor? Fill { get; init; }

    /// <summary>Horizontal alignment.</summary>
    public PdfTextAlignment Alignment { get; init; }

    /// <summary>Columns spanned (1 or more).</summary>
    public int ColumnSpan { get; init; } = 1;

    /// <summary>A cell from text.</summary>
    public static implicit operator PdfTableCell(string text) => new(text);
}

/// <summary>A table for <see cref="PdfFlowLayout.AddTable"/>: column widths, header rows, body rows.</summary>
public sealed class PdfTable
{
    /// <summary>Creates a table with relative column widths (scaled to the content width).</summary>
    public PdfTable(params double[] columnWeights)
    {
        ArgumentNullException.ThrowIfNull(columnWeights);
        if (columnWeights.Length == 0 || columnWeights.Any(w => w <= 0))
        {
            throw new ArgumentException("A table needs at least one column, each with a positive width.", nameof(columnWeights));
        }

        ColumnWeights = columnWeights;
    }

    /// <summary>Relative column widths.</summary>
    public IReadOnlyList<double> ColumnWeights { get; }

    /// <summary>Rows repeated at the top of every page the table crosses.</summary>
    public List<IReadOnlyList<PdfTableCell>> HeaderRows { get; } = [];

    /// <summary>Body rows.</summary>
    public List<IReadOnlyList<PdfTableCell>> Rows { get; } = [];

    /// <summary>Default font.</summary>
    public PdfFont Font { get; init; } = PdfFont.Sans;

    /// <summary>Default size.</summary>
    public double Size { get; init; } = 9;

    /// <summary>Header font; bold of <see cref="Font"/> when null.</summary>
    public PdfFont? HeaderFont { get; init; }

    /// <summary>Header background.</summary>
    public PdfColor? HeaderFill { get; init; } = new PdfColor(240, 240, 240);

    /// <summary>Grid colour; no grid when null.</summary>
    public PdfColor? BorderColor { get; init; } = PdfColor.Gray;

    /// <summary>Grid line width.</summary>
    public double BorderWidth { get; init; } = 0.5;

    /// <summary>Which edges of each cell get a line in <see cref="BorderColor"/>. Default <see cref="PdfBorderSides.All"/>
    /// (the full grid). <see cref="PdfBorderSides.Bottom"/> alone gives a rule under each row and nothing else;
    /// <see cref="PdfBorderSides.Horizontal"/> adds the rule above the first row.</summary>
    public PdfBorderSides Borders { get; init; } = PdfBorderSides.All;

    /// <summary>Edges of the cells of the header rows; <see cref="Borders"/> when null. For example
    /// <see cref="PdfBorderSides.None"/> in a table of row separators keeps the line under the header out.</summary>
    public PdfBorderSides? HeaderBorders { get; init; }

    /// <summary>Inner cell padding.</summary>
    public double Padding { get; init; } = 3;

    /// <summary>Adds a header row.</summary>
    public PdfTable Header(params PdfTableCell[] cells)
    {
        HeaderRows.Add(cells);
        return this;
    }

    /// <summary>Adds a body row.</summary>
    public PdfTable Row(params PdfTableCell[] cells)
    {
        Rows.Add(cells);
        return this;
    }
}

/// <summary>Measures and paints a table row by row in a flow.</summary>
internal sealed class PdfTablePainter(PdfFlowLayout flow, PdfTable table)
{
    private readonly double[] _widths = table.ColumnWeights.Select(w => w / table.ColumnWeights.Sum() * flow.ContentWidth).ToArray();

    public void Paint()
    {
        var headerHeight = table.HeaderRows.Sum(r => Height(r, header: true));
        // The headers never stand alone at the bottom of a page: they move with the first body row (on a fresh page
        // this changes nothing, so a block taller than the page still ends).
        flow.EnsureSpace(headerHeight + (table.Rows.Count > 0 ? Height(table.Rows[0], header: false) : 0));
        PaintHeaders();
        for (var index = 0; index < table.Rows.Count; index++)
        {
            var row = table.Rows[index];
            var height = Height(row, header: false);
            // The first row already has its place: where it does not fit, the page is a fresh one and another page
            // would only leave this one blank (or with the headers alone).
            if (index > 0 && flow.Y + height > flow.Bottom)
            {
                flow.NewPage();
                if (flow.Y + headerHeight + height <= flow.Bottom)
                {
                    PaintHeaders();
                }
            }

            PaintRow(row, height, header: false);
        }
    }

    private void PaintHeaders()
    {
        foreach (var row in table.HeaderRows)
        {
            PaintRow(row, Height(row, header: true), header: true);
        }
    }

    private double Height(IReadOnlyList<PdfTableCell> row, bool header)
    {
        var tallest = 0.0;
        foreach (var (cell, width) in Layout(row))
        {
            var (font, size) = Style(cell, header);
            var lines = TextWrapper.Wrap(flow.Document, cell.Text, font, size, width - (2 * table.Padding));
            tallest = Math.Max(tallest, lines.Count * flow.Document.Metrics(font, size).LineHeight);
        }

        return tallest + (2 * table.Padding);
    }

    private void PaintRow(IReadOnlyList<PdfTableCell> row, double height, bool header)
    {
        var page = flow.Page;
        var x = flow.Left;
        foreach (var (cell, width) in Layout(row))
        {
            var fill = cell.Fill ?? (header ? table.HeaderFill : null);
            if (fill is { } background)
            {
                page.FillRectangle(x, flow.Y, width, height, background);
            }

            var (font, size) = Style(cell, header);
            var metrics = flow.Document.Metrics(font, size);
            var baseline = flow.Y + table.Padding + metrics.Ascent;
            foreach (var line in TextWrapper.Wrap(flow.Document, cell.Text, font, size, width - (2 * table.Padding)))
            {
                PdfParagraphPainter.DrawLine(page, line, x + table.Padding, baseline, width - (2 * table.Padding), font, size, cell.Color, cell.Alignment);
                baseline += metrics.LineHeight;
            }

            if (table.BorderColor is { } border)
            {
                StrokeSides(page, cell.Borders ?? (header ? table.HeaderBorders : null) ?? table.Borders, x, width, height, border);
            }

            x += width;
        }

        flow.Y += height;
    }

    private void StrokeSides(PdfCanvas page, PdfBorderSides sides, double x, double width, double height, PdfColor color)
    {
        var (top, bottom) = (flow.Y, flow.Y + height);
        if (sides == PdfBorderSides.All)
        {
            page.StrokeRectangle(x, top, width, height, color, table.BorderWidth);
            return;
        }

        if (sides.HasFlag(PdfBorderSides.Top))
        {
            page.DrawLine(x, top, x + width, top, color, table.BorderWidth);
        }

        if (sides.HasFlag(PdfBorderSides.Bottom))
        {
            page.DrawLine(x, bottom, x + width, bottom, color, table.BorderWidth);
        }

        if (sides.HasFlag(PdfBorderSides.Left))
        {
            page.DrawLine(x, top, x, bottom, color, table.BorderWidth);
        }

        if (sides.HasFlag(PdfBorderSides.Right))
        {
            page.DrawLine(x + width, top, x + width, bottom, color, table.BorderWidth);
        }
    }

    private (PdfFont Font, double Size) Style(PdfTableCell cell, bool header) =>
        (cell.Font ?? (header ? table.HeaderFont ?? table.Font.AsBold() : table.Font), cell.Size ?? table.Size);

    private IEnumerable<(PdfTableCell Cell, double Width)> Layout(IReadOnlyList<PdfTableCell> row)
    {
        var column = 0;
        foreach (var cell in row)
        {
            if (column >= _widths.Length)
            {
                yield break;
            }

            var span = Math.Clamp(cell.ColumnSpan, 1, _widths.Length - column);
            yield return (cell, _widths.Skip(column).Take(span).Sum());
            column += span;
        }
    }
}
