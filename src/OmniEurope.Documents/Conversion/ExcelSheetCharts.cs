// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion;

/// <summary>
/// The charts of a printed sheet as floating shapes in front of its table: each one anchored in the cell holding its
/// top-left corner (the first cell of a merged range holding it), placed from the table's left edge by the widths of
/// the columns before it and from the top of that cell by its offset, rows below the anchor counting 15 points. A
/// table too wide for its page is narrowed by the layout: the charts shrink by the same factor.
/// </summary>
internal sealed class ExcelSheetCharts
{
    // The cell margin above the anchor's line (WordCellMargins of the sheet table).
    private const double CellMarginTop = 1;

    private readonly Dictionary<(int Row, int Column), List<WordChart>> _anchors = [];

    public ExcelSheetCharts(XlsxWorksheet sheet, XlsxRange range, ExcelPdfOptions options, IReadOnlyList<double> pixels)
    {
        var width = pixels.Sum() * 0.75;
        var scale = Math.Min(1, ExcelSheetTable.Page(width).ContentWidth / Math.Max(width, 1));
        foreach (var chart in sheet.Charts)
        {
            var (row, column) = (chart.Cells.FirstRow, chart.Cells.FirstColumn);
            var merge = sheet.MergedRanges.FirstOrDefault(m => row >= m.FirstRow && row <= m.LastRow && column >= m.FirstColumn && column <= m.LastColumn);
            var below = merge == default ? 0 : (row - merge.FirstRow) * XlsxWorksheet.DefaultRowHeight;
            var key = merge == default ? (row, column) : (merge.FirstRow, merge.FirstColumn);
            var left = (pixels.Take(column - range.FirstColumn).Sum() * 0.75) + chart.OffsetX;
            var shape = new WordChart(chart.Chart, chart.Width * scale, chart.Height * scale, chart.Part)
            {
                Culture = options.Culture,
                Description = chart.Chart.Title,
                Floating = new WordFloatingPosition(left * scale, "column", below + chart.OffsetY - CellMarginTop, "line", WordWrap.None),
            };
            if (!_anchors.TryGetValue(key, out var list))
            {
                _anchors[key] = list = [];
            }

            list.Add(shape);
        }
    }

    /// <summary>The charts anchored in the cell starting at <paramref name="column"/> of <paramref name="row"/>.</summary>
    public IReadOnlyList<WordChart> At(int row, int column) => _anchors.TryGetValue((row, column), out var list) ? list : [];
}
