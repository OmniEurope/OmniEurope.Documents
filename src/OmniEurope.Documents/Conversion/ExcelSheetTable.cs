// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion;

/// <summary>
/// The table one sheet is printed from. Widths follow Excel's unit: a column of width <c>w</c> is <c>7w + 5</c>
/// pixels, 7 pixels being the widest digit of the default font and 5 the cell padding; content fits when its
/// width, scaled so that the default digit measures 7 pixels, fits the columns it may use less that padding.
/// Numbers follow that rule; text that runs on is measured as drawn, so that none of its letters is clipped
/// where the run says it fits.
/// </summary>
internal sealed class ExcelSheetTable
{
    private const double DefaultColumnWidth = 8.43;
    private const double DigitPixels = 7;
    private const double PaddingPixels = 5;

    // The cell margin on the side text starts from, in points: text drawn must fit the cell box past it.
    private const double MarginPoints = 2.5;
    private static readonly WordBorder Grid = new("single", 0.25, 0, "BFBFBF");
    private static readonly WordBorder NoLine = new("nil", 0);

    private readonly XlsxWorksheet _sheet;
    private readonly XlsxRange _range;
    private readonly ExcelPdfOptions _options;
    private readonly FontLibrary _fonts;
    private readonly double _digitPoints;
    private readonly List<double> _pixels = [];

    public ExcelSheetTable(XlsxWorksheet sheet, XlsxRange range, ExcelPdfOptions options)
    {
        (_sheet, _range, _options) = (sheet, range, options);
        _fonts = options.Pdf?.Fonts ?? FontLibrary.Default;
        _digitPoints = _fonts.Resolve(XlsxStyle.Default.FontName).MeasureText("0", XlsxStyle.Default.FontSize);
        for (var c = range.FirstColumn; c <= range.LastColumn; c++)
        {
            var characters = sheet.ColumnWidths.TryGetValue(c, out var width) ? width : DefaultColumnWidth;
            _pixels.Add((characters * DigitPixels) + PaddingPixels);
        }
    }

    public WordTable Build()
    {
        var table = new WordTable
        {
            Properties = new WordTableProperties
            {
                Borders = _options.Gridlines ? new WordTableBorders(Grid, Grid, Grid, Grid, Grid, Grid) : null,
                CellMargins = new WordCellMargins(1, 2.5, 1, 2.5),
                FixedLayout = true,
            },
        };
        table.Columns.AddRange(_pixels.Select(p => p * 0.75));
        var merges = ExcelRowLayout.MergesByRow(_sheet);
        for (var r = _range.FirstRow; r <= _range.LastRow; r++)
        {
            var row = new WordTableRow { Properties = new WordTableRowProperties { IsHeader = r < _range.FirstRow + _sheet.FrozenRows ? true : null, CantSplit = true } };
            var rowMerges = merges.TryGetValue(r, out var list) ? list : [];
            // Text runs on as far as it is drawn (points), so no letter is clipped where the run says it fits.
            var (segments, runs) = ExcelRowLayout.Plan(_sheet, _range, r, table.Columns, rowMerges, c => Points(c, c.FormatValue(_options.Culture)) + MarginPoints);
            foreach (var segment in segments)
            {
                row.Cells.Add(Cell(segment, r, runs));
            }

            table.Rows.Add(row);
        }

        return table;
    }

    private WordTableCell Cell(ExcelSegment segment, int r, Dictionary<int, ExcelRun> runs)
    {
        var cell = _sheet.FindCell(r, segment.Owner);
        var style = cell?.Style ?? XlsxStyle.Default;
        var properties = CellProperties(style, segment.Merge, r);
        var result = new WordTableCell { Properties = Gridless(properties, segment, runs.Values, style) };
        if (properties.VerticalMerge == WordVerticalMerge.Continue)
        {
            return result;
        }

        var alignment = Alignment(style, cell);
        var text = cell?.FormatValue(_options.Culture) ?? string.Empty;
        var room = Span(segment.First, segment.Last) - PaddingPixels;
        if (cell?.ValueType is XlsxValueType.Number or XlsxValueType.DateTime)
        {
            // Never wrapped: shortened or filled with '#' to fit, then kept on one line inside the cell.
            text = Fit(cell, text, room);
            result.Properties = result.Properties with { Overflow = new WordCellOverflow(Points(cell, text) + 1, alignment) };
        }
        else if (cell is not null && ExcelRowLayout.RunsOn(cell))
        {
            result.Properties = result.Properties with { Overflow = Overflow(cell, text, alignment, segment, runs) };
        }

        var paragraph = new WordParagraph { Properties = new WordParagraphProperties { Alignment = alignment } };
        if (text.Length > 0)
        {
            paragraph.AddText(text, Run(style));
        }

        result.Blocks.Add(paragraph);
        return result;
    }

    // One line, clipped to the cell, the merged range, or the run of empty cells the text spreads over; null
    // (wrapped in its cell) when the text reached the edge of the printed range without fitting.
    private WordCellOverflow? Overflow(XlsxCell cell, string text, WordAlignment alignment, ExcelSegment segment, Dictionary<int, ExcelRun> runs)
    {
        var width = Points(cell, text) + 1;
        if (segment.Merge != default)
        {
            return new WordCellOverflow(width, alignment);
        }

        if (!runs.TryGetValue(segment.Owner, out var run))
        {
            return Points(cell, text) + MarginPoints > Span(segment.First, segment.Last) * 0.75 ? null : new WordCellOverflow(width, alignment);
        }

        var before = Span(run.First, segment.Owner - 1) * 0.75;
        var after = Span(segment.Owner + 1, run.Last) * 0.75;
        return new WordCellOverflow(width, alignment, before, after);
    }

    // Excel draws no grid line under text that runs over empty cells: the sides inside a run lose theirs.
    private WordTableCellProperties Gridless(WordTableCellProperties properties, ExcelSegment segment, IEnumerable<ExcelRun> runs, XlsxStyle style)
    {
        if (!_options.Gridlines || style.Border || segment.Merge != default)
        {
            return properties;
        }

        var column = segment.First;
        foreach (var run in runs)
        {
            if (column >= run.First && column <= run.Last && run.Last > run.First)
            {
                return properties with
                {
                    Borders = new WordTableBorders(Left: column > run.First ? NoLine : null, Right: column < run.Last ? NoLine : null),
                };
            }
        }

        return properties;
    }

    // Excel never wraps a number: under the General format it drops decimal digits until the value fits,
    // otherwise (or when even one digit does not fit) the cell fills with '#'.
    private string Fit(XlsxCell cell, string text, double room)
    {
        bool Fits(string candidate) => Pixels(cell, candidate) <= room;
        if (Fits(text))
        {
            return text;
        }

        if (cell.ValueType == XlsxValueType.Number && cell.Style.NumberFormat.Equals("General", StringComparison.OrdinalIgnoreCase))
        {
            var value = (double)cell.Value!;
            for (var digits = 9; digits >= 1; digits--)
            {
                var shorter = value.ToString("G" + digits.ToString(CultureInfo.InvariantCulture), _options.Culture);
                if (Fits(shorter))
                {
                    return shorter;
                }
            }
        }

        return new string('#', Math.Max(1, (int)(room / Pixels(cell, "#"))));
    }

    // Total width, in Excel pixels, of the sheet columns first to last (0 when the span is empty).
    private double Span(int first, int last)
    {
        double sum = 0;
        for (var c = first; c <= last; c++)
        {
            sum += _pixels[c - _range.FirstColumn];
        }

        return sum;
    }

    // The width of the text on one line in the cell's font: in points as drawn, in Excel pixels for fitting.
    private double Points(XlsxCell cell, string text) =>
        _fonts.Resolve(cell.Style.FontName, cell.Style.Bold, cell.Style.Italic).MeasureText(text, cell.Style.FontSize);

    private double Pixels(XlsxCell cell, string text) => Points(cell, text) * DigitPixels / _digitPoints;

    private static WordTableCellProperties CellProperties(XlsxStyle style, XlsxRange merge, int r)
    {
        var merged = merge != default;
        return new WordTableCellProperties
        {
            GridSpan = merged && merge.LastColumn > merge.FirstColumn ? merge.LastColumn - merge.FirstColumn + 1 : null,
            VerticalMerge = VerticalMerge(merged, merge, r),
            Shading = style.FillColor is { } fill ? XlsxStyle.NormalizeColor(fill) : null,
            VerticalAlignment = style.VerticalAlignment switch
            {
                XlsxVerticalAlignment.Top => WordCellAlignment.Top,
                XlsxVerticalAlignment.Center => WordCellAlignment.Center,
                _ => WordCellAlignment.Bottom,
            },
            Borders = style.Border ? Borders(style.BorderColor) : null,
        };
    }

    private static WordVerticalMerge? VerticalMerge(bool merged, XlsxRange merge, int r)
    {
        if (!merged || merge.LastRow == merge.FirstRow)
        {
            return null;
        }

        return r == merge.FirstRow ? WordVerticalMerge.Restart : WordVerticalMerge.Continue;
    }

    private static WordTableBorders Borders(string? color)
    {
        var line = new WordBorder("single", 0.5, 0, color is null ? "000000" : XlsxStyle.NormalizeColor(color));
        return new WordTableBorders(line, line, line, line);
    }

    // "General" alignment puts numbers and dates on the right, text on the left, booleans and errors in the centre.
    private static WordAlignment Alignment(XlsxStyle style, XlsxCell? cell) => style.HorizontalAlignment switch
    {
        XlsxHorizontalAlignment.Left => WordAlignment.Left,
        XlsxHorizontalAlignment.Center => WordAlignment.Center,
        XlsxHorizontalAlignment.Right => WordAlignment.Right,
        XlsxHorizontalAlignment.Justify => WordAlignment.Justify,
        _ => cell?.ValueType switch
        {
            XlsxValueType.Number or XlsxValueType.DateTime => WordAlignment.Right,
            XlsxValueType.Boolean or XlsxValueType.Error => WordAlignment.Center,
            _ => WordAlignment.Left,
        },
    };

    private static WordRunProperties Run(XlsxStyle style) => new()
    {
        Font = style.FontName,
        FontSize = style.FontSize,
        Bold = style.Bold ? true : null,
        Italic = style.Italic ? true : null,
        Underline = style.Underline ? WordUnderline.Single : null,
        Strike = style.Strike ? true : null,
        Color = style.FontColor is { } color ? XlsxStyle.NormalizeColor(color) : null,
    };
}
