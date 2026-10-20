// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Excel;

/// <summary>A rectangle of cells, 1-based and inclusive.</summary>
public readonly record struct XlsxRange(int FirstRow, int FirstColumn, int LastRow, int LastColumn)
{
    /// <summary>The A1 notation (<c>A1:C3</c>).</summary>
    public override string ToString() => FirstRow == LastRow && FirstColumn == LastColumn
        ? CellReference.Format(FirstRow, FirstColumn)
        : CellReference.Format(FirstRow, FirstColumn) + ":" + CellReference.Format(LastRow, LastColumn);

    /// <summary>Parses <c>A1</c> or <c>A1:C3</c>.</summary>
    public static XlsxRange Parse(string range) =>
        CellReference.TryParseRange(range, out var r1, out var c1, out var r2, out var c2)
            ? new XlsxRange(r1, c1, r2, c2)
            : throw new FormatException($"'{range}' is not a cell range.");
}

/// <summary>A worksheet: cells, column widths, merged ranges, frozen panes, an auto-filter and, once loaded, its charts.</summary>
public sealed class XlsxWorksheet
{
    private static readonly char[] ForbiddenNameChars = ['[', ']', ':', '*', '?', '/', '\\'];
    private readonly SortedDictionary<(int Row, int Column), XlsxCell> _cells = new();
    private readonly SortedDictionary<int, double> _columnWidths = new();
    private readonly List<XlsxRange> _merges = [];
    private readonly List<XlsxChart> _charts = [];

    internal XlsxWorksheet(XlsxWorkbook workbook, string name)
    {
        Workbook = workbook;
        Name = ValidateName(name);
    }

    /// <summary>The workbook.</summary>
    public XlsxWorkbook Workbook { get; }

    /// <summary>The sheet name (1-31 characters, none of <c>[]:*?/\</c>).</summary>
    public string Name { get; private set; }

    /// <summary>The cells holding a value, a formula or a style, ordered by row then column.</summary>
    public IEnumerable<XlsxCell> Cells => _cells.Values;

    /// <summary>Column widths in characters, by 1-based column.</summary>
    public IReadOnlyDictionary<int, double> ColumnWidths => _columnWidths;

    /// <summary>Merged ranges.</summary>
    public IReadOnlyList<XlsxRange> MergedRanges => _merges;

    /// <summary>The charts of the sheet's drawing, read from a loaded workbook (bar, line, pie and doughnut charts are
    /// read in full; others keep their frame and title). Saving a workbook does not write them.</summary>
    public IReadOnlyList<XlsxChart> Charts => _charts;

    /// <summary>Rows frozen at the top.</summary>
    public int FrozenRows { get; private set; }

    /// <summary>Columns frozen on the left.</summary>
    public int FrozenColumns { get; private set; }

    /// <summary>The auto-filter range, or null.</summary>
    public XlsxRange? AutoFilter { get; set; }

    /// <summary>The smallest range holding every cell with a value, or null for an empty sheet.</summary>
    public XlsxRange? UsedRange
    {
        get
        {
            var withValues = _cells.Values.Where(c => c.ValueType != XlsxValueType.Empty || c.Formula is not null).ToList();
            if (withValues.Count == 0)
            {
                return null;
            }

            return new XlsxRange(withValues.Min(c => c.Row), withValues.Min(c => c.Column), withValues.Max(c => c.Row), withValues.Max(c => c.Column));
        }
    }

    /// <summary>Renames the sheet.</summary>
    public void Rename(string name)
    {
        var valid = ValidateName(name);
        if (Workbook.Worksheets.Any(w => w != this && w.Name.Equals(valid, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"A sheet named '{valid}' already exists.", nameof(name));
        }

        Name = valid;
    }

    /// <summary>The cell at a 1-based row and column, created when missing.</summary>
    public XlsxCell Cell(int row, int column)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(row, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(row, CellReference.MaxRow);
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(column, CellReference.MaxColumn);
        if (!_cells.TryGetValue((row, column), out var cell))
        {
            cell = new XlsxCell(this, row, column);
            _cells.Add((row, column), cell);
        }

        return cell;
    }

    /// <summary>The cell at an A1 reference, created when missing.</summary>
    public XlsxCell Cell(string reference) =>
        CellReference.TryParse(reference, out var row, out var column)
            ? Cell(row, column)
            : throw new FormatException($"'{reference}' is not a cell reference.");

    /// <summary>The cell at a position when it exists.</summary>
    public XlsxCell? FindCell(int row, int column) => _cells.GetValueOrDefault((row, column));

    /// <summary>The displayed text of a cell, empty when it does not exist.</summary>
    public string GetFormattedText(int row, int column) => FindCell(row, column)?.FormattedText ?? string.Empty;

    /// <summary>Sets the width of a 1-based column, in characters (0-255).</summary>
    public void SetColumnWidth(int column, double width)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, 255);
        _columnWidths[column] = width;
    }

    /// <summary>Sets each used column's width from the longest displayed text it holds (an estimate: no
    /// font metrics), between <paramref name="minimum"/> and <paramref name="maximum"/> characters.</summary>
    public void AutoFitColumns(double minimum = 8, double maximum = 80)
    {
        foreach (var column in _cells.Values.GroupBy(c => c.Column))
        {
            var widest = column.Max(EstimateWidth);
            _columnWidths[column.Key] = Math.Clamp(Math.Round(widest + 2, 1), minimum, maximum);
        }
    }

    /// <summary>Freezes the given number of top rows and left columns.</summary>
    public void FreezePanes(int rows, int columns = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rows);
        ArgumentOutOfRangeException.ThrowIfNegative(columns);
        FrozenRows = rows;
        FrozenColumns = columns;
    }

    /// <summary>Merges a range; the value of its top-left cell is the one displayed.</summary>
    public void Merge(XlsxRange range)
    {
        if (_merges.Exists(m => m.FirstRow <= range.LastRow && range.FirstRow <= m.LastRow
            && m.FirstColumn <= range.LastColumn && range.FirstColumn <= m.LastColumn))
        {
            throw new ArgumentException($"Range {range} overlaps a merged range.", nameof(range));
        }

        _merges.Add(range);
    }

    internal void AddLoadedMerge(XlsxRange range) => _merges.Add(range);

    internal void AddLoadedChart(XlsxChart chart) => _charts.Add(chart);

    /// <summary>The height of a row in points: Excel's default for the default font (row heights are not read).</summary>
    internal const double DefaultRowHeight = 15;

    /// <summary>The width of a 1-based column in Excel pixels: <c>7w + 5</c> for a width of <c>w</c> characters (8.43 by
    /// default), 7 pixels being the widest digit of the default font and 5 the cell padding.</summary>
    internal double ColumnPixels(int column) => ((_columnWidths.TryGetValue(column, out var width) ? width : 8.43) * 7) + 5;

    private static double EstimateWidth(XlsxCell cell)
    {
        var lines = cell.FormattedText.Split('\n');
        var longest = lines.Max(l => l.Length);
        var scale = (cell.Style.FontSize / 11d) * (cell.Style.Bold ? 1.1 : 1);
        return longest * scale;
    }

    private static string ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 31 || name.IndexOfAny(ForbiddenNameChars) >= 0 || name.StartsWith('\'') || name.EndsWith('\''))
        {
            throw new ArgumentException($"'{name}' is not a valid sheet name.", nameof(name));
        }

        return name;
    }
}
