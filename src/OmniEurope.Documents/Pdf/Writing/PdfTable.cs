// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Pdf.Writing;

/// <summary>A cell of a <see cref="PdfTable"/>.</summary>
public sealed record PdfTableCell(string Text)
{
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
        PaintHeaders();
        foreach (var row in table.Rows)
        {
            var height = Height(row, header: false);
            if (flow.Y + height > flow.Bottom)
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
                page.StrokeRectangle(x, flow.Y, width, height, border, table.BorderWidth);
            }

            x += width;
        }

        flow.Y += height;
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
