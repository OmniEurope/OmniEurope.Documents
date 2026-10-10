// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Conversion.WordHtml;
using OmniEurope.Documents.Excel;

namespace OmniEurope.Documents.Conversion.ExcelHtml;

/// <summary>
/// Writes the page of <see cref="ExcelToHtml"/>. Every text of the workbook goes through
/// <see cref="HtmlOutput"/>, which encodes it; the style sheet holds only rules built by <see cref="ExcelHtmlCss"/>
/// from checked values.
/// </summary>
internal sealed class ExcelHtmlWriter(ExcelHtmlOptions options)
{
    private const double DefaultColumnWidth = 8.43;
    private const double DigitPixels = 7;
    private const double PaddingPixels = 5;

    private readonly ExcelHtmlCss _css = new(options.Gridlines);
    private readonly HtmlOutput _body = new();

    public string Write(XlsxWorkbook workbook)
    {
        foreach (var sheet in workbook.Worksheets)
        {
            WriteSheet(sheet);
        }

        var page = new HtmlOutput();
        page.Raw("<!DOCTYPE html>\n<html><head><meta charset=\"utf-8\">");
        page.Open("title");
        page.Text(workbook.Title ?? workbook.Worksheets.FirstOrDefault()?.Name ?? "Workbook");
        page.Close("title");
        page.Raw("<style>\n");
        page.Raw(_css.StyleSheet());
        page.Raw("</style></head><body>\n");
        page.Raw(_body.ToString());
        page.Raw("</body></html>\n");
        return page.ToString();
    }

    private void WriteSheet(XlsxWorksheet sheet)
    {
        _body.Open("div", ("class", "omni-sheet-part"));
        if (options.SheetTitles)
        {
            _body.Open("h2");
            _body.Text(sheet.Name);
            _body.Close("h2");
        }

        if (sheet.UsedRange is { } range)
        {
            WriteTable(sheet, range);
        }

        _body.Close("div");
        _body.Line();
    }

    private void WriteTable(XlsxWorksheet sheet, XlsxRange range)
    {
        var widths = new List<double>();
        for (var c = range.FirstColumn; c <= range.LastColumn; c++)
        {
            var characters = sheet.ColumnWidths.TryGetValue(c, out var width) ? width : DefaultColumnWidth;
            widths.Add(Math.Round((characters * DigitPixels) + PaddingPixels));
        }

        _body.Open("table", ("class", ExcelToHtml.TableClass + " " + _css.WidthClass(widths.Sum())));
        _body.Open("colgroup");
        foreach (var width in widths)
        {
            _body.Open("col", ("class", _css.WidthClass(width)));
        }

        _body.Close("colgroup");
        _body.Open("tbody");
        var merges = new MergeMap(sheet.MergedRanges, range);
        for (var r = range.FirstRow; r <= range.LastRow; r++)
        {
            _body.Open("tr");
            for (var c = range.FirstColumn; c <= range.LastColumn; c++)
            {
                if (!merges.IsCovered(r, c))
                {
                    WriteCell(sheet.FindCell(r, c), merges.SpanAt(r, c));
                }
            }

            _body.Close("tr");
            _body.Line();
        }

        _body.Close("tbody");
        _body.Close("table");
    }

    private void WriteCell(XlsxCell? cell, (int Rows, int Columns) span)
    {
        var style = cell?.Style ?? XlsxStyle.Default;
        _body.Open(
            "td",
            ("class", _css.CellClass(style, cell?.ValueType ?? XlsxValueType.Empty)),
            ("colspan", span.Columns > 1 ? span.Columns.ToString(CultureInfo.InvariantCulture) : null),
            ("rowspan", span.Rows > 1 ? span.Rows.ToString(CultureInfo.InvariantCulture) : null));
        _body.Text(cell?.FormatValue(options.Culture) ?? string.Empty);
        _body.Close("td");
    }

    /// <summary>The merged ranges of a sheet clipped to the range written: the first cell of each spans it,
    /// the others are left out.</summary>
    private sealed class MergeMap
    {
        private readonly Dictionary<(int Row, int Column), (int Rows, int Columns)> _spans = [];
        private readonly HashSet<(int Row, int Column)> _covered = [];

        public MergeMap(IEnumerable<XlsxRange> merges, XlsxRange range)
        {
            foreach (var merge in merges)
            {
                var (top, left) = (Math.Max(merge.FirstRow, range.FirstRow), Math.Max(merge.FirstColumn, range.FirstColumn));
                var (bottom, right) = (Math.Min(merge.LastRow, range.LastRow), Math.Min(merge.LastColumn, range.LastColumn));
                var cells = new List<(int Row, int Column)>();
                for (var r = top; r <= bottom; r++)
                {
                    for (var c = left; c <= right; c++)
                    {
                        cells.Add((r, c));
                    }
                }

                // A range overlapping one already placed (Excel never writes one) is ignored.
                if (cells.Count == 0 || cells.Exists(p => _covered.Contains(p) || _spans.ContainsKey(p)))
                {
                    continue;
                }

                _spans[cells[0]] = (bottom - top + 1, right - left + 1);
                _covered.UnionWith(cells.Skip(1));
            }
        }

        public bool IsCovered(int row, int column) => _covered.Contains((row, column));

        public (int Rows, int Columns) SpanAt(int row, int column) => _spans.GetValueOrDefault((row, column), (1, 1));
    }
}
