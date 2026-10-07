// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Html;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.HtmlLayout;

/// <summary>
/// Builds a Word table from an HTML table: rows of the head, bodies and foot (in that order), header cells
/// in bold, column spans and row spans (as vertical merges). Columns share the text width equally; head rows
/// repeat on each page.
/// </summary>
internal static class HtmlTableBuilder
{
    public static void Build(HtmlElement table, HtmlWordWriter writer)
    {
        var rows = Rows(table).ToList();
        if (rows.Count == 0)
        {
            return;
        }

        var grid = new List<List<WordTableCell?>>();
        var result = new WordTable { Properties = new WordTableProperties { StyleId = writer.Document.Styles.Get("TableGrid") is null ? null : "TableGrid", Width = WordWidth.Percent(100) } };
        for (var r = 0; r < rows.Count; r++)
        {
            Row(rows[r].Element, r, grid, writer);
        }

        var columns = Math.Max(1, grid.Max(g => g.Count));
        var width = writer.Document.Sections[^1].Page.ContentWidth;
        result.Columns.AddRange(Enumerable.Repeat(width / columns, columns));
        for (var r = 0; r < rows.Count; r++)
        {
            var row = new WordTableRow { Properties = new WordTableRowProperties { IsHeader = rows[r].Header ? true : null } };
            row.Cells.AddRange(grid[r].OfType<WordTableCell>());
            if (row.Cells.Count > 0)
            {
                result.Rows.Add(row);
            }
        }

        writer.Container.Add(result);
    }

    private static IEnumerable<(HtmlElement Element, bool Header)> Rows(HtmlElement table)
    {
        var groups = table.Children.ToList();
        foreach (var head in groups.Where(g => g.TagName.Equals("thead", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var tr in head.Children.Where(IsRow))
            {
                yield return (tr, true);
            }
        }

        foreach (var group in groups.Where(g => !g.TagName.Equals("thead", StringComparison.OrdinalIgnoreCase) && !g.TagName.Equals("tfoot", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var tr in IsRow(group) ? [group] : group.Children.Where(IsRow))
            {
                yield return (tr, false);
            }
        }

        foreach (var tr in groups.Where(g => g.TagName.Equals("tfoot", StringComparison.OrdinalIgnoreCase)).SelectMany(f => f.Children.Where(IsRow)))
        {
            yield return (tr, false);
        }
    }

    private static bool IsRow(HtmlElement element) => element.TagName.Equals("tr", StringComparison.OrdinalIgnoreCase);

    // Places the row's cells in the grid, skipping slots taken by row spans from above.
    private static void Row(HtmlElement tr, int r, List<List<WordTableCell?>> grid, HtmlWordWriter writer)
    {
        while (grid.Count <= r)
        {
            grid.Add([]);
        }

        var column = 0;
        foreach (var td in tr.Children.Where(c => c.TagName.Equals("td", StringComparison.OrdinalIgnoreCase) || c.TagName.Equals("th", StringComparison.OrdinalIgnoreCase)))
        {
            while (column < grid[r].Count && grid[r][column] is not null || Covered(grid, r, column))
            {
                column++;
            }

            var span = Math.Clamp(Number(td, "colspan"), 1, 1000);
            var down = Math.Clamp(Number(td, "rowspan"), 1, 1000);
            var cell = new WordTableCell { Properties = new WordTableCellProperties { GridSpan = span > 1 ? span : null, VerticalMerge = down > 1 ? WordVerticalMerge.Restart : null } };
            var header = td.TagName.Equals("th", StringComparison.OrdinalIgnoreCase);
            writer.WriteInto(cell.Blocks, td, header ? new WordRunProperties { Bold = true } : null);
            Put(grid, r, column, cell, span);
            for (var below = 1; below < down; below++)
            {
                while (grid.Count <= r + below)
                {
                    grid.Add([]);
                }

                Put(grid, r + below, column, new WordTableCell { Properties = new WordTableCellProperties { GridSpan = span > 1 ? span : null, VerticalMerge = WordVerticalMerge.Continue } }, span);
            }

            column += span;
        }
    }

    private static bool Covered(List<List<WordTableCell?>> grid, int r, int column) => column < grid[r].Count && grid[r][column] is null && Spanned(grid[r], column);

    private static bool Spanned(List<WordTableCell?> row, int column)
    {
        for (var c = column - 1; c >= 0; c--)
        {
            if (row[c] is { } cell)
            {
                return c + (cell.Properties.GridSpan ?? 1) > column;
            }
        }

        return false;
    }

    // A cell takes its first slot; the slots it spans stay empty.
    private static void Put(List<List<WordTableCell?>> grid, int r, int column, WordTableCell cell, int span)
    {
        var row = grid[r];
        while (row.Count < column + span)
        {
            row.Add(null);
        }

        row[column] = cell;
    }

    private static int Number(HtmlElement element, string attribute) => int.TryParse(element.GetAttribute(attribute), out var value) ? value : 1;
}
