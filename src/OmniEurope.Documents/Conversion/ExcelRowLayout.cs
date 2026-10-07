// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Excel;

namespace OmniEurope.Documents.Conversion;

/// <summary>The columns one table cell covers, and the sheet column whose cell it shows.</summary>
internal readonly record struct ExcelSegment(int First, int Last, int Owner);

/// <summary>
/// How a sheet row maps to table cells. A merged range is one cell. Text that does not wrap and is wider than
/// its column runs on into the empty cells beside it, as Excel shows it: to the right when aligned left (or by
/// default), to the left when aligned right, both ways when centred; it stops at the first cell that holds a
/// value, a fill or a border, and what still does not fit is clipped. Numbers never run on.
/// </summary>
internal static class ExcelRowLayout
{
    /// <summary>The segments of row <paramref name="row"/>, left to right.</summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="range">The columns printed.</param>
    /// <param name="row">The 1-based row.</param>
    /// <param name="widths">Width of each column of the range, in points.</param>
    /// <param name="needed">Width the text of a cell needs on one line, margins included.</param>
    public static List<ExcelSegment> Segments(XlsxWorksheet sheet, XlsxRange range, int row, IReadOnlyList<double> widths, Func<XlsxCell, double> needed)
    {
        var plan = new RowPlan(sheet, range, row, widths);
        for (var c = range.FirstColumn; c <= range.LastColumn; c++)
        {
            if (plan.Owns(c) && sheet.FindCell(row, c) is { } cell && RunsOn(cell))
            {
                plan.RunOn(c, needed(cell), Directions(cell.Style.HorizontalAlignment));
            }
        }

        return plan.Join();
    }

    /// <summary>True for text shown on one line (no wrapping, no justification).</summary>
    public static bool RunsOn(XlsxCell cell) =>
        cell.ValueType == XlsxValueType.Text && !cell.Style.WrapText && cell.Style.HorizontalAlignment != XlsxHorizontalAlignment.Justify;

    // Empty, without a fill or a border to cover.
    public static bool IsBlank(XlsxCell? cell) =>
        cell is null || (cell.ValueType == XlsxValueType.Empty && cell.Formula is null && cell.Style.FillColor is null && !cell.Style.Border);

    private static (bool Left, bool Right) Directions(XlsxHorizontalAlignment alignment) => alignment switch
    {
        XlsxHorizontalAlignment.Right => (true, false),
        XlsxHorizontalAlignment.Center => (true, true),
        _ => (false, true),
    };
}

/// <summary>Which sheet column each printed column of one row shows, built merge by merge and run by run.</summary>
internal sealed class RowPlan
{
    private readonly XlsxWorksheet _sheet;
    private readonly XlsxRange _range;
    private readonly int _row;
    private readonly IReadOnlyList<double> _widths;
    private readonly int[] _owner;
    private readonly bool[] _locked;

    public RowPlan(XlsxWorksheet sheet, XlsxRange range, int row, IReadOnlyList<double> widths)
    {
        (_sheet, _range, _row, _widths) = (sheet, range, row, widths);
        _owner = Enumerable.Range(range.FirstColumn, range.LastColumn - range.FirstColumn + 1).ToArray();
        _locked = new bool[_owner.Length];
        foreach (var merge in sheet.MergedRanges.Where(m => row >= m.FirstRow && row <= m.LastRow))
        {
            Take(Math.Max(merge.FirstColumn, range.FirstColumn), Math.Min(merge.LastColumn, range.LastColumn), merge.FirstColumn);
        }
    }

    /// <summary>True when column <paramref name="c"/> still shows its own cell alone.</summary>
    public bool Owns(int c) => !_locked[c - _range.FirstColumn];

    /// <summary>Lets the text of column <paramref name="c"/> take free neighbours until it fits.</summary>
    public void RunOn(int c, double needed, (bool Left, bool Right) directions)
    {
        var (left, right) = (c, c);
        while (Room(left, right) < needed)
        {
            var growLeft = directions.Left && Free(left - 1);
            var growRight = directions.Right && Free(right + 1);
            var both = directions.Left && directions.Right;
            if (both ? !(growLeft && growRight) : !(growLeft || growRight))
            {
                break;
            }

            left -= growLeft ? 1 : 0;
            right += growRight ? 1 : 0;
        }

        Take(left, right, c);
    }

    /// <summary>Runs of columns showing the same cell.</summary>
    public List<ExcelSegment> Join()
    {
        var segments = new List<ExcelSegment>();
        for (var c = _range.FirstColumn; c <= _range.LastColumn; c++)
        {
            var owner = _owner[c - _range.FirstColumn];
            if (segments.Count > 0 && segments[^1].Owner == owner)
            {
                segments[^1] = segments[^1] with { Last = c };
            }
            else
            {
                segments.Add(new ExcelSegment(c, c, owner));
            }
        }

        return segments;
    }

    private double Room(int left, int right) => _widths.Skip(left - _range.FirstColumn).Take(right - left + 1).Sum();

    // Inside the range, not taken, and blank in the sheet.
    private bool Free(int c) => c >= _range.FirstColumn && c <= _range.LastColumn && Owns(c) && ExcelRowLayout.IsBlank(_sheet.FindCell(_row, c));

    private void Take(int first, int last, int owner)
    {
        for (var k = first; k <= last; k++)
        {
            _owner[k - _range.FirstColumn] = owner;
            _locked[k - _range.FirstColumn] = true;
        }
    }
}
