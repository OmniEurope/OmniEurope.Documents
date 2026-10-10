// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Excel.Formulas;

/// <summary>
/// Evaluates a parsed formula against a workbook. References are read as the cells hold them now, so cells are
/// evaluated in dependency order by <see cref="XlsxCalculator"/>. Operations on ranges or arrays apply element by
/// element (a single row or column is spread over the other operand); a cell keeps the top-left element.
/// </summary>
internal sealed class FormulaEvaluator(XlsxWorkbook workbook, DateTime now)
{
    private readonly Dictionary<XlsxWorksheet, (int Cells, XlsxRange? Used)> _extents = [];

    /// <summary>The sheet of the formula being evaluated.</summary>
    public XlsxWorksheet Sheet { get; set; } = null!;

    /// <summary>The row and column of the formula being evaluated (ROW and COLUMN without argument).</summary>
    public (int Row, int Column) Current { get; set; }

    public XlsxWorkbook Workbook => workbook;

    public DateTime Now => now;

    public bool Date1904 => workbook.Date1904;

    public FormulaValue Evaluate(FormulaNode node) => node switch
    {
        ConstantNode constant => constant.Value,
        ReferenceNode reference => Read(reference.Area),
        UnaryNode unary => Map(Evaluate(unary.Operand), v => Arithmetic(v, FormulaValue.Of(-1), (a, b) => a * b)),
        PercentNode percent => Map(Evaluate(percent.Operand), v => Arithmetic(v, FormulaValue.Of(100), (a, b) => a / b)),
        BinaryNode binary => Binary(binary.Operator, Evaluate(binary.Left), Evaluate(binary.Right)),
        CallNode call => FormulaFunctions.Call(this, call),
        _ => FormulaValue.Name,
    };

    /// <summary>The worksheet an area belongs to, or null when the workbook has no sheet of that name.</summary>
    public XlsxWorksheet? SheetOf(FormulaArea area) =>
        area.Sheet is null ? Sheet : workbook.Worksheets.FirstOrDefault(s => s.Name.Equals(area.Sheet, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The values of an area: one value for a cell, an array otherwise. Whole rows and columns stop at the last
    /// used row and column of the sheet; every cell beyond is blank.
    /// </summary>
    public FormulaValue Read(FormulaArea area)
    {
        if (SheetOf(area) is not { } sheet)
        {
            return FormulaValue.Ref;
        }

        if (area.Top == area.Bottom && area.Left == area.Right)
        {
            return FormulaValue.FromCell(sheet.FindCell(area.Top, area.Left));
        }

        var (cells, used) = Extent(sheet);
        var bottom = Math.Min(area.Bottom, Math.Max(area.Top, used?.LastRow ?? area.Top));
        var right = Math.Min(area.Right, Math.Max(area.Left, used?.LastColumn ?? area.Left));
        var values = new FormulaValue[bottom - area.Top + 1, right - area.Left + 1];
        if ((long)values.Length <= cells)
        {
            for (var r = area.Top; r <= bottom; r++)
            {
                for (var c = area.Left; c <= right; c++)
                {
                    values[r - area.Top, c - area.Left] = FormulaValue.FromCell(sheet.FindCell(r, c));
                }
            }
        }
        else
        {
            foreach (var cell in sheet.Cells.Where(c => c.Row >= area.Top && c.Row <= bottom && c.Column >= area.Left && c.Column <= right))
            {
                values[cell.Row - area.Top, cell.Column - area.Left] = FormulaValue.FromCell(cell);
            }
        }

        return FormulaValue.Of(values);
    }

    // The cell count and used range of a sheet, measured once per recalculation (formulas fill existing cells).
    private (int Cells, XlsxRange? Used) Extent(XlsxWorksheet sheet)
    {
        if (!_extents.TryGetValue(sheet, out var extent))
        {
            extent = (sheet.Cells.Count(), sheet.UsedRange);
            _extents[sheet] = extent;
        }

        return extent;
    }

    /// <summary>Applies an operation to a value, or to each element of an array.</summary>
    public static FormulaValue Map(FormulaValue value, Func<FormulaValue, FormulaValue> operation)
    {
        if (value.Kind != ValueKind.Array)
        {
            return operation(value);
        }

        var source = value.Array;
        var result = new FormulaValue[source.GetLength(0), source.GetLength(1)];
        for (var r = 0; r < result.GetLength(0); r++)
        {
            for (var c = 0; c < result.GetLength(1); c++)
            {
                result[r, c] = operation(source[r, c]);
            }
        }

        return FormulaValue.Of(result);
    }

    /// <summary>
    /// Applies an operation element by element: the result has the larger size of the two; a single row or
    /// column (or a single value) is repeated, and elements that neither operand covers are #N/A.
    /// </summary>
    public static FormulaValue Zip(FormulaValue left, FormulaValue right, Func<FormulaValue, FormulaValue, FormulaValue> operation)
    {
        if (left.Kind != ValueKind.Array && right.Kind != ValueKind.Array)
        {
            return operation(left, right);
        }

        var a = left.Kind == ValueKind.Array ? left.Array : new[,] { { left } };
        var b = right.Kind == ValueKind.Array ? right.Array : new[,] { { right } };
        var rows = Math.Max(a.GetLength(0), b.GetLength(0));
        var columns = Math.Max(a.GetLength(1), b.GetLength(1));
        var result = new FormulaValue[rows, columns];
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < columns; c++)
            {
                result[r, c] = At(a, r, c) is { } x && At(b, r, c) is { } y ? operation(x, y) : FormulaValue.NotAvailable;
            }
        }

        return FormulaValue.Of(result);
    }

    private static FormulaValue? At(FormulaValue[,] array, int row, int column)
    {
        var r = array.GetLength(0) == 1 ? 0 : row;
        var c = array.GetLength(1) == 1 ? 0 : column;
        return r < array.GetLength(0) && c < array.GetLength(1) ? array[r, c] : null;
    }

    private static FormulaValue Binary(string op, FormulaValue left, FormulaValue right) => op switch
    {
        "+" => Zip(left, right, (a, b) => Arithmetic(a, b, (x, y) => x + y)),
        "-" => Zip(left, right, (a, b) => Arithmetic(a, b, (x, y) => x - y)),
        "*" => Zip(left, right, (a, b) => Arithmetic(a, b, (x, y) => x * y)),
        "/" => Zip(left, right, (a, b) => Arithmetic(a, b, (x, y) => y == 0 ? double.NaN : x / y, FormulaValue.Div0)),
        "^" => Zip(left, right, Power),
        "&" => Zip(left, right, Concatenate),
        _ => Zip(left, right, (a, b) => Compare(op, a, b)),
    };

    private static FormulaValue Arithmetic(FormulaValue left, FormulaValue right, Func<double, double, double> operation, FormulaValue? onNaN = null)
    {
        var a = left.ToNumber();
        if (a.IsError)
        {
            return a;
        }

        var b = right.ToNumber();
        if (b.IsError)
        {
            return b;
        }

        var result = operation(a.Number, b.Number);
        return double.IsNaN(result) && onNaN is { } error ? error : FormulaValue.Of(result);
    }

    // 0^0 and a negative number to a fractional power have no real value (#NUM!).
    private static FormulaValue Power(FormulaValue left, FormulaValue right) =>
        Arithmetic(left, right, (x, y) => x == 0 && y == 0 ? double.NaN : Math.Pow(x, y));

    private static FormulaValue Concatenate(FormulaValue left, FormulaValue right)
    {
        var a = left.ToText();
        if (a.IsError)
        {
            return a;
        }

        var b = right.ToText();
        return b.IsError ? b : FormulaValue.Of(a.Text + b.Text);
    }

    private static FormulaValue Compare(string op, FormulaValue left, FormulaValue right)
    {
        var a = left.Scalar();
        var b = right.Scalar();
        if (a.IsError)
        {
            return a;
        }

        if (b.IsError)
        {
            return b;
        }

        var order = CompareValues(a, b);
        return FormulaValue.Of(op switch
        {
            "=" => order == 0,
            "<>" => order != 0,
            "<" => order < 0,
            ">" => order > 0,
            "<=" => order <= 0,
            _ => order >= 0,
        });
    }

    /// <summary>
    /// Orders two values as Excel compares them: numbers before text before booleans, text without regard to case;
    /// a blank is 0, empty text or FALSE, whichever it is compared with.
    /// </summary>
    public static int CompareValues(FormulaValue a, FormulaValue b)
    {
        if (a.Kind == ValueKind.Blank)
        {
            a = Empty(b);
        }

        if (b.Kind == ValueKind.Blank)
        {
            b = Empty(a);
        }

        var rank = Rank(a).CompareTo(Rank(b));
        if (rank != 0)
        {
            return rank;
        }

        return a.Kind == ValueKind.Text
            ? string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase)
            : a.Number.CompareTo(b.Number);
    }

    private static FormulaValue Empty(FormulaValue other) => other.Kind switch
    {
        ValueKind.Text => FormulaValue.Of(string.Empty),
        ValueKind.Boolean => FormulaValue.False,
        _ => FormulaValue.Of(0),
    };

    private static int Rank(FormulaValue value) => value.Kind switch
    {
        ValueKind.Number => 0,
        ValueKind.Text => 1,
        _ => 2,
    };
}
