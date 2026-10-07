// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Excel;

namespace OmniEurope.Documents.Conversion;

/// <summary>The columns one table cell covers (a merged range, or one column), and the sheet column whose cell it shows.</summary>
internal readonly record struct ExcelSegment(int First, int Last, int Owner, XlsxRange Merge);

/// <summary>The columns a one-line text spreads over, its own column included.</summary>
internal readonly record struct ExcelRun(int First, int Last);

/// <summary>
/// How a sheet row maps to table cells, and where text that does not wrap goes, as Excel shows it: text wider
/// than its column runs on into the empty cells beside it (to the right when aligned left or by default, to
/// the left when aligned right, both ways when centred) and is clipped at the first cell holding a value or a
/// formula. An empty cell with a fill or a border does not stop it. Text that would pass the edge of the
/// printed range wraps in its own cell instead, so that nothing is lost. Numbers never run on.
/// </summary>
internal sealed class ExcelRowLayout
{
    private readonly XlsxWorksheet _sheet;
    private readonly XlsxRange _range;
    private readonly int _row;
    private readonly IReadOnlyList<double> _widths;
    private readonly XlsxRange[] _merge;
    private readonly int[] _owner;

    private ExcelRowLayout(XlsxWorksheet sheet, XlsxRange range, int row, IReadOnlyList<double> widths, IEnumerable<XlsxRange> merges)
    {
        (_sheet, _range, _row, _widths) = (sheet, range, row, widths);
        var count = range.LastColumn - range.FirstColumn + 1;
        _merge = new XlsxRange[count];
        _owner = Enumerable.Range(range.FirstColumn, count).ToArray();
        foreach (var merge in merges)
        {
            for (var c = Math.Max(merge.FirstColumn, range.FirstColumn); c <= Math.Min(merge.LastColumn, range.LastColumn); c++)
            {
                _merge[c - range.FirstColumn] = merge;
                _owner[c - range.FirstColumn] = merge.FirstColumn;
            }
        }
    }

    /// <summary>The merged ranges of each row of the sheet, built once per sheet.</summary>
    public static Dictionary<int, List<XlsxRange>> MergesByRow(XlsxWorksheet sheet)
    {
        var rows = new Dictionary<int, List<XlsxRange>>();
        foreach (var merge in sheet.MergedRanges)
        {
            for (var r = merge.FirstRow; r <= merge.LastRow; r++)
            {
                if (!rows.TryGetValue(r, out var list))
                {
                    rows[r] = list = [];
                }

                list.Add(merge);
            }
        }

        return rows;
    }

    /// <summary>The cells of row <paramref name="row"/> left to right, and the runs of its one-line texts by owning column.</summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="range">The columns printed.</param>
    /// <param name="row">The 1-based row.</param>
    /// <param name="widths">Width of each column of the range, in the unit of <paramref name="needed"/>.</param>
    /// <param name="merges">The merged ranges crossing the row.</param>
    /// <param name="needed">Width the text of a cell needs on one line.</param>
    public static (List<ExcelSegment> Segments, Dictionary<int, ExcelRun> Runs) Plan(
        XlsxWorksheet sheet, XlsxRange range, int row, IReadOnlyList<double> widths, IEnumerable<XlsxRange> merges, Func<XlsxCell, double> needed)
    {
        var layout = new ExcelRowLayout(sheet, range, row, widths, merges);
        var runs = new Dictionary<int, ExcelRun>();
        for (var c = range.FirstColumn; c <= range.LastColumn; c++)
        {
            if (layout.Alone(c) && sheet.FindCell(row, c) is { } cell && RunsOn(cell) && layout.Run(c, needed(cell), Directions(cell.Style.HorizontalAlignment)) is { } run)
            {
                runs[c] = run;
            }
        }

        return (layout.Segments(), runs);
    }

    /// <summary>True for text shown on one line (no wrapping, no justification).</summary>
    public static bool RunsOn(XlsxCell cell) =>
        cell.ValueType == XlsxValueType.Text && !cell.Style.WrapText && cell.Style.HorizontalAlignment != XlsxHorizontalAlignment.Justify;

    private static (bool Left, bool Right) Directions(XlsxHorizontalAlignment alignment) => alignment switch
    {
        XlsxHorizontalAlignment.Right => (true, false),
        XlsxHorizontalAlignment.Center => (true, true),
        _ => (false, true),
    };

    // A cell the text can run over: neither a value nor a formula (a fill or a border does not stop it).
    private static bool IsBlank(XlsxCell? cell) => cell is null || (cell.ValueType == XlsxValueType.Empty && cell.Formula is null);

    // The run of the text of column c: null when it fits its own cell, or when it reaches the edge of the range
    // without fitting (it then wraps). A run stopped by a value is kept: the text is clipped there.
    private ExcelRun? Run(int c, double needed, (bool Left, bool Right) directions)
    {
        var (left, right) = (c, c);
        while (Room(left, right) < needed)
        {
            if ((directions.Left && left == _range.FirstColumn) || (directions.Right && right == _range.LastColumn))
            {
                return null;
            }

            var growLeft = directions.Left && Free(left - 1);
            var growRight = directions.Right && Free(right + 1);
            if (directions.Left && directions.Right ? !(growLeft && growRight) : !(growLeft || growRight))
            {
                break;
            }

            left -= growLeft ? 1 : 0;
            right += growRight ? 1 : 0;
        }

        for (var k = left; k <= right; k++)
        {
            _owner[k - _range.FirstColumn] = c;
        }

        return left == right && Room(c, c) >= needed ? null : new ExcelRun(left, right);
    }

    // Each column is its own cell, except a merged range: one cell over its columns.
    private List<ExcelSegment> Segments()
    {
        var segments = new List<ExcelSegment>();
        for (var c = _range.FirstColumn; c <= _range.LastColumn; c++)
        {
            var merge = _merge[c - _range.FirstColumn];
            if (merge != default && segments.Count > 0 && segments[^1].Merge == merge)
            {
                segments[^1] = segments[^1] with { Last = c };
            }
            else
            {
                segments.Add(new ExcelSegment(c, c, merge != default ? merge.FirstColumn : c, merge));
            }
        }

        return segments;
    }

    private bool Alone(int c) => _merge[c - _range.FirstColumn] == default && _owner[c - _range.FirstColumn] == c;

    private double Room(int left, int right)
    {
        double room = 0;
        for (var k = left; k <= right; k++)
        {
            room += _widths[k - _range.FirstColumn];
        }

        return room;
    }

    private bool Free(int c) => c >= _range.FirstColumn && c <= _range.LastColumn && Alone(c) && IsBlank(_sheet.FindCell(_row, c));
}
