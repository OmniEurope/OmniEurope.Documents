// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>
/// Lays a table out into row items: grid widths (scaled to a percentage width or down to the available
/// width), cell spans, vertical merges (the merge's last row grows to fit the merged content), cell margins,
/// resolved borders and shading, and the conditional formats of the table style (header row, total row,
/// first and last column, banding, corners).
/// </summary>
internal sealed class TableLayout(LayoutContext context, BlockLayout blocks)
{
    private const WordTableLook DefaultLook = WordTableLook.FirstRow | WordTableLook.FirstColumn | WordTableLook.NoVerticalBanding;

    public List<FlowItem> Layout(WordTable table, double width)
    {
        var properties = context.Styles.ResolveTable(table.Properties);
        var style = context.Styles.Get(properties.StyleId);
        var grid = Grid(table, properties, width);
        var left = Left(properties, grid.Sum(), width);
        var shared = new TableContext();
        var rows = new List<RowItem>();
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = Row(table, r, grid, left, properties, style);
            row.Table = shared;
            row.IsHeader = table.Rows[r].Properties.IsHeader == true && rows.All(x => x.IsHeader);
            if (row.IsHeader)
            {
                shared.HeaderRows.Add(row);
            }

            rows.Add(row);
        }

        MergeVertically(rows);
        return [.. rows];
    }

    private static List<double> Grid(WordTable table, WordTableProperties properties, double width)
    {
        var columns = table.Columns.Count > 0 ? new List<double>(table.Columns) : [];
        if (columns.Count == 0)
        {
            var count = Math.Max(1, table.Rows.Select(r => (r.Properties.GridBefore ?? 0) + r.Cells.Sum(c => c.Properties.GridSpan ?? 1)).DefaultIfEmpty(1).Max());
            columns.AddRange(Enumerable.Repeat(width / count, count));
        }

        var total = columns.Sum();
        var target = properties.Width is { Type: WordWidthType.Percent } percent ? width * percent.Value / 100 : Math.Min(total, width);
        if (total > 0 && Math.Abs(target - total) > 0.5)
        {
            var scale = target / total;
            columns = columns.Select(c => c * scale).ToList();
        }

        return columns;
    }

    private static double Left(WordTableProperties properties, double tableWidth, double width) => properties.Alignment switch
    {
        WordAlignment.Center => (width - tableWidth) / 2,
        WordAlignment.Right => width - tableWidth,
        _ => properties.Indent ?? 0,
    };

    private RowItem Row(WordTable table, int r, List<double> grid, double left, WordTableProperties properties, WordStyle? style)
    {
        var source = table.Rows[r];
        var row = new RowItem { CantSplit = source.Properties.CantSplit == true };
        var column = Math.Min(source.Properties.GridBefore ?? 0, grid.Count);
        for (var c = 0; c < source.Cells.Count && column < grid.Count; c++)
        {
            var cell = source.Cells[c];
            var span = Math.Clamp(cell.Properties.GridSpan ?? 1, 1, grid.Count - column);
            var regions = Regions(table, r, c, properties.Look ?? DefaultLook);
            var format = Format(style, regions);
            var cellProperties = (style?.CellProperties ?? WordTableCellProperties.Empty).Overlay(format.Cell).Overlay(cell.Properties);
            var x = left + grid.Take(column).Sum();
            var cellWidth = grid.Skip(column).Take(span).Sum();
            var margins = Margins(properties.CellMargins, cellProperties.Margins);
            var box = new CellBox(column, x, cellWidth, margins, cellProperties)
            {
                Borders = Borders(properties.Borders, cellProperties.Borders, r == 0, r == table.Rows.Count - 1, c == 0, column + span >= grid.Count),
            };
            if (cellProperties.VerticalMerge != WordVerticalMerge.Continue)
            {
                var cellStyle = new CellStyle(properties.StyleId, format.Paragraph, format.Run);
                var inner = Math.Max(1, cellWidth - margins.Left - margins.Right);
                box.Items.AddRange(blocks.Layout(cell.Blocks, Math.Max(inner, cellProperties.Overflow?.Width ?? 0), cellStyle));
            }

            row.Cells.Add(box);
            column += span;
        }

        row.MinimumHeight = source.Properties.Height ?? 0;
        row.ExactHeight = source.Properties.HeightRule == WordRowHeightRule.Exact;
        row.Measure();
        return row;
    }

    // Region formats applied in Word's order of precedence: whole table, bands, columns, rows, corners.
    private static List<WordTableRegion> Regions(WordTable table, int r, int c, WordTableLook look)
    {
        var (headerRow, totalRow) = (look.HasFlag(WordTableLook.FirstRow) && r == 0, look.HasFlag(WordTableLook.LastRow) && r == table.Rows.Count - 1);
        var (firstColumn, lastColumn) = (c == 0, c == table.Rows[r].Cells.Count - 1);
        var regions = new List<WordTableRegion> { WordTableRegion.WholeTable };
        regions.AddRange(Bands(look, r, c));
        AddIf(regions, look.HasFlag(WordTableLook.FirstColumn) && firstColumn, WordTableRegion.FirstColumn);
        AddIf(regions, look.HasFlag(WordTableLook.LastColumn) && lastColumn, WordTableRegion.LastColumn);
        AddIf(regions, headerRow, WordTableRegion.FirstRow);
        AddIf(regions, totalRow, WordTableRegion.LastRow);
        regions.AddRange(Corners(headerRow, totalRow, firstColumn, lastColumn));
        return regions;
    }

    private static IEnumerable<WordTableRegion> Bands(WordTableLook look, int r, int c)
    {
        if (!look.HasFlag(WordTableLook.NoHorizontalBanding))
        {
            yield return (r - (look.HasFlag(WordTableLook.FirstRow) ? 1 : 0)) % 2 == 0 ? WordTableRegion.OddRows : WordTableRegion.EvenRows;
        }

        if (!look.HasFlag(WordTableLook.NoVerticalBanding))
        {
            yield return (c - (look.HasFlag(WordTableLook.FirstColumn) ? 1 : 0)) % 2 == 0 ? WordTableRegion.OddColumns : WordTableRegion.EvenColumns;
        }
    }

    private static IEnumerable<WordTableRegion> Corners(bool headerRow, bool totalRow, bool firstColumn, bool lastColumn)
    {
        var row = headerRow ? 0 : totalRow ? 1 : -1;
        var column = firstColumn ? 0 : lastColumn ? 1 : -1;
        if (row >= 0 && column >= 0)
        {
            yield return (row, column) switch
            {
                (0, 0) => WordTableRegion.TopLeftCell,
                (0, _) => WordTableRegion.TopRightCell,
                (_, 0) => WordTableRegion.BottomLeftCell,
                _ => WordTableRegion.BottomRightCell,
            };
        }
    }

    private static void AddIf(List<WordTableRegion> regions, bool condition, WordTableRegion region)
    {
        if (condition)
        {
            regions.Add(region);
        }
    }

    private static (WordParagraphProperties? Paragraph, WordRunProperties? Run, WordTableCellProperties? Cell) Format(WordStyle? style, List<WordTableRegion> regions)
    {
        WordParagraphProperties? paragraph = null;
        WordRunProperties? run = null;
        WordTableCellProperties? cell = null;
        foreach (var region in regions)
        {
            var format = style?.ConditionalFormats.FirstOrDefault(f => f.Region == region);
            if (format is null)
            {
                continue;
            }

            paragraph = paragraph is null ? format.ParagraphProperties : paragraph.Overlay(format.ParagraphProperties);
            run = run is null ? format.RunProperties : run.Overlay(format.RunProperties);
            cell = cell is null ? format.CellProperties : cell.Overlay(format.CellProperties);
        }

        return (paragraph, run, cell);
    }

    private static (double Top, double Left, double Bottom, double Right) Margins(WordCellMargins? table, WordCellMargins? cell) => (
        cell?.Top ?? table?.Top ?? 0,
        cell?.Left ?? table?.Left ?? 5.4,
        cell?.Bottom ?? table?.Bottom ?? 0,
        cell?.Right ?? table?.Right ?? 5.4);

    // A cell's own border wins; otherwise the table's outer border on the edge of the table, its inside border elsewhere.
    private static WordTableBorders Borders(WordTableBorders? table, WordTableBorders? cell, bool top, bool bottom, bool first, bool last)
    {
        var (t, c) = (table ?? new WordTableBorders(), cell ?? new WordTableBorders());
        return new WordTableBorders(
            Edge(c.Top, top, t.Top, t.InsideHorizontal),
            Edge(c.Left, first, t.Left, t.InsideVertical),
            Edge(c.Bottom, bottom, t.Bottom, t.InsideHorizontal),
            Edge(c.Right, last, t.Right, t.InsideVertical));
    }

    private static WordBorder? Edge(WordBorder? own, bool outer, WordBorder? outside, WordBorder? inside) => own ?? (outer ? outside : inside);

    // A vertically merged cell's content may need more room than its rows: the last row of the merge grows.
    private static void MergeVertically(List<RowItem> rows)
    {
        for (var r = 0; r < rows.Count; r++)
        {
            foreach (var cell in rows[r].Cells.Where(c => c.Properties.VerticalMerge == WordVerticalMerge.Restart))
            {
                var group = new List<RowItem> { rows[r] };
                for (var next = r + 1; next < rows.Count && rows[next].Cells.Find(c => c.Column == cell.Column) is { Properties.VerticalMerge: WordVerticalMerge.Continue } below; next++)
                {
                    below.MergedAbove = true;
                    group.Add(rows[next]);
                }

                if (group.Count == 1)
                {
                    continue;
                }

                cell.MergedBelow = true;
                var needed = cell.ContentHeight + cell.Margins.Top + cell.Margins.Bottom;
                var available = group.Sum(g => g.Height);
                if (needed > available && !group[^1].ExactHeight)
                {
                    group[^1].Height += needed - available;
                }

                cell.SpanHeight = group.Sum(g => g.Height);
            }
        }
    }
}
