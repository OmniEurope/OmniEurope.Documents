// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Conversion.WordLayout;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordHtml;

/// <summary>
/// Writes a table: grid column widths, alignment and indent, rows with their height, cells with their span
/// (<c>colspan</c>), vertical merges (<c>rowspan</c>), shading, borders, margins and vertical alignment, and
/// the conditional formats of the table style. Grid columns skipped before or after a row's cells become
/// empty cells. A deleted row is left out in the accepted view and struck through in the marked view.
/// </summary>
internal sealed class HtmlTableWriter(HtmlContext context, HtmlOutput output, HtmlBlockWriter blocks)
{
    public void Write(WordTable table)
    {
        var properties = context.Styles.ResolveTable(table.Properties);
        var style = context.Styles.Get(properties.StyleId);
        var rows = table.Rows.Where(r => !context.Skips(r.Revision)).ToList();
        var plan = Plan(rows);
        var grid = table.Columns.Count > 0 ? table.Columns.Count : plan.Select((cells, r) => cells.Select(c => c.Column + c.Span).DefaultIfEmpty(0).Max() + (rows[r].Properties.GridAfter ?? 0)).DefaultIfEmpty(0).Max();
        var state = new TableState(table, rows, properties, style, grid);
        output.Open("table", ("class", "omni-table"), ("style", HtmlCss.Join(TableStyle(table, properties))));
        output.Line();
        Columns(table);
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            output.Open("tr", ("class", row.Revision is null ? null : "omni-row-" + (row.Revision.Kind == WordRevisionKind.Inserted ? "inserted" : "deleted")), ("style", row.Properties.Height is { } height ? "height:" + HtmlCss.Points(height) : null));
            Skipped(row.Properties.GridBefore);
            foreach (var cell in plan[r])
            {
                Cell(state, r, cell);
            }

            Skipped(row.Properties.GridAfter);
            output.Close("tr");
            output.Line();
        }

        output.Close("table");
        output.Line();
    }

    private static IEnumerable<string> TableStyle(WordTable table, WordTableProperties properties)
    {
        if (properties.Width is { Type: WordWidthType.Percent } percent)
        {
            yield return "width:" + HtmlCss.Number(percent.Value) + "%";
        }
        else if (table.Columns.Count > 0)
        {
            yield return "width:" + HtmlCss.Points(table.Columns.Sum());
        }

        if (properties.FixedLayout == true)
        {
            yield return "table-layout:fixed";
        }

        foreach (var declaration in Placement(properties))
        {
            yield return declaration;
        }

        if (HtmlCss.Color(properties.Shading) is { } shading)
        {
            yield return "background-color:" + shading;
        }
    }

    private static IEnumerable<string> Placement(WordTableProperties properties) => properties.Alignment switch
    {
        WordAlignment.Center => ["margin-left:auto", "margin-right:auto"],
        WordAlignment.Right => ["margin-left:auto"],
        _ => properties.Indent is { } indent && indent != 0 ? ["margin-left:" + HtmlCss.Points(indent)] : [],
    };

    private void Columns(WordTable table)
    {
        if (table.Columns.Count == 0)
        {
            return;
        }

        output.Raw("<colgroup>");
        foreach (var width in table.Columns)
        {
            output.Open("col", ("style", "width:" + HtmlCss.Points(width)));
        }

        output.Raw("</colgroup>");
        output.Line();
    }

    // Each row's cells with their grid column, and the rows a vertical merge spans; continued cells are dropped.
    private static List<List<PlannedCell>> Plan(List<WordTableRow> rows)
    {
        var plan = new List<List<PlannedCell>>();
        var open = new Dictionary<int, PlannedCell>();
        foreach (var row in rows)
        {
            var cells = new List<PlannedCell>();
            var next = new Dictionary<int, PlannedCell>();
            var column = row.Properties.GridBefore ?? 0;
            for (var c = 0; c < row.Cells.Count; c++)
            {
                var cell = row.Cells[c];
                var span = Math.Max(1, cell.Properties.GridSpan ?? 1);
                var merge = cell.Properties.VerticalMerge;
                if (merge == WordVerticalMerge.Continue && open.TryGetValue(column, out var above))
                {
                    above.RowSpan++;
                    next[column] = above;
                }
                else
                {
                    var planned = new PlannedCell(cell, c, column, span);
                    cells.Add(planned);
                    if (merge is not null and not WordVerticalMerge.None)
                    {
                        next[column] = planned;
                    }
                }

                column += span;
            }

            plan.Add(cells);
            open = next;
        }

        return plan;
    }

    private void Skipped(int? columns)
    {
        if (columns is > 0)
        {
            output.Open("td", ("class", "omni-grid-skip"), ("colspan", columns.Value.ToString(CultureInfo.InvariantCulture)));
            output.Close("td");
        }
    }

    private void Cell(TableState state, int r, PlannedCell planned)
    {
        var (table, rows, properties, style, grid) = state;
        var regions = WordTableFormats.Regions(table, table.Rows.IndexOf(rows[r]), planned.Index, properties.Look ?? WordTableFormats.DefaultLook);
        var format = WordTableFormats.Format(style, regions);
        var cell = (style?.CellProperties ?? WordTableCellProperties.Empty).Overlay(format.Cell).Overlay(planned.Cell.Properties);
        var last = rows.Count - 1;
        var lastColumn = planned.Column + planned.Span >= grid;
        var borders = WordTableFormats.Borders(properties.Borders, cell.Borders, r == 0, r + planned.RowSpan - 1 >= last, planned.Column == 0, lastColumn);
        var margins = WordTableFormats.Margins(properties.CellMargins, cell.Margins);
        output.Open(
            "td",
            ("colspan", planned.Span > 1 ? planned.Span.ToString(CultureInfo.InvariantCulture) : null),
            ("rowspan", planned.RowSpan > 1 ? planned.RowSpan.ToString(CultureInfo.InvariantCulture) : null),
            ("style", HtmlCss.Join(CellStyle(table, planned, cell, borders, margins))));
        output.Raw("<div class=\"omni-cell\">");
        output.Line();
        blocks.Write(planned.Cell.Blocks, new CellStyle(properties.StyleId, format.Paragraph, format.Run));
        output.Raw("</div>");
        output.Close("td");
    }

    private static IEnumerable<string> CellStyle(WordTable table, PlannedCell planned, WordTableCellProperties cell, WordTableBorders borders, (double Top, double Left, double Bottom, double Right) margins)
    {
        if (table.Columns.Count >= planned.Column + planned.Span)
        {
            yield return "width:" + HtmlCss.Points(table.Columns.Skip(planned.Column).Take(planned.Span).Sum());
        }

        yield return "padding:" + string.Join(' ', new[] { margins.Top, margins.Right, margins.Bottom, margins.Left }.Select(HtmlCss.Points));
        foreach (var (side, border) in new[] { ("top", borders.Top), ("left", borders.Left), ("bottom", borders.Bottom), ("right", borders.Right) })
        {
            if (HtmlCss.Border(border) is { } line)
            {
                yield return "border-" + side + ":" + line;
            }
        }

        foreach (var declaration in CellEffects(cell))
        {
            yield return declaration;
        }
    }

    private static IEnumerable<string> CellEffects(WordTableCellProperties cell)
    {
        if (HtmlCss.Color(cell.Shading) is { } shading)
        {
            yield return "background-color:" + shading;
        }

        if (cell.VerticalAlignment is WordCellAlignment.Center or WordCellAlignment.Bottom)
        {
            yield return "vertical-align:" + (cell.VerticalAlignment == WordCellAlignment.Center ? "middle" : "bottom");
        }

        if (cell.NoWrap == true)
        {
            yield return "white-space:nowrap";
        }
    }

    private sealed record TableState(WordTable Table, List<WordTableRow> Rows, WordTableProperties Properties, WordStyle? Style, int Grid);

    private sealed class PlannedCell(WordTableCell cell, int index, int column, int span)
    {
        public WordTableCell Cell { get; } = cell;

        /// <summary>Position of the cell in its row (for the conditional formats).</summary>
        public int Index { get; } = index;

        public int Column { get; } = column;

        public int Span { get; } = span;

        public int RowSpan { get; set; } = 1;
    }
}
