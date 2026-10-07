// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion;

/// <summary>
/// What a table style and the table give one cell, shared by the conversions: the conditional formats of the
/// regions the cell belongs to (header row, total row, first and last column, banding, corners), its
/// margins and its borders.
/// </summary>
internal static class WordTableFormats
{
    /// <summary>The regions a table without <c>w:tblLook</c> formats.</summary>
    public const WordTableLook DefaultLook = WordTableLook.FirstRow | WordTableLook.FirstColumn | WordTableLook.NoVerticalBanding;

    // Region formats applied in Word's order of precedence: whole table, bands, columns, rows, corners.
    public static List<WordTableRegion> Regions(WordTable table, int r, int c, WordTableLook look)
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

    public static (WordParagraphProperties? Paragraph, WordRunProperties? Run, WordTableCellProperties? Cell) Format(WordStyle? style, List<WordTableRegion> regions)
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

    public static (double Top, double Left, double Bottom, double Right) Margins(WordCellMargins? table, WordCellMargins? cell) => (
        cell?.Top ?? table?.Top ?? 0,
        cell?.Left ?? table?.Left ?? 5.4,
        cell?.Bottom ?? table?.Bottom ?? 0,
        cell?.Right ?? table?.Right ?? 5.4);

    // A cell's own border wins; otherwise the table's outer border on the edge of the table, its inside border elsewhere.
    public static WordTableBorders Borders(WordTableBorders? table, WordTableBorders? cell, bool top, bool bottom, bool first, bool last)
    {
        var (t, c) = (table ?? new WordTableBorders(), cell ?? new WordTableBorders());
        return new WordTableBorders(
            Edge(c.Top, top, t.Top, t.InsideHorizontal),
            Edge(c.Left, first, t.Left, t.InsideVertical),
            Edge(c.Bottom, bottom, t.Bottom, t.InsideHorizontal),
            Edge(c.Right, last, t.Right, t.InsideVertical));
    }

    private static WordBorder? Edge(WordBorder? own, bool outer, WordBorder? outside, WordBorder? inside) => own ?? (outer ? outside : inside);
}
