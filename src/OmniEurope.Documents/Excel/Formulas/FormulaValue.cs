// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;

namespace OmniEurope.Documents.Excel.Formulas;

/// <summary>What a formula value is.</summary>
internal enum ValueKind
{
    Blank,
    Number,
    Text,
    Boolean,
    Error,
    Array,
}

/// <summary>
/// One value met while evaluating a formula: a number, a text, a boolean, an error code, a blank (an empty cell)
/// or an array of values (a range read into memory, or the result of an operation on ranges).
/// </summary>
internal readonly struct FormulaValue
{
    public static readonly FormulaValue Blank = default;
    public static readonly FormulaValue True = new(ValueKind.Boolean, 1, null, null);
    public static readonly FormulaValue False = new(ValueKind.Boolean, 0, null, null);
    public static readonly FormulaValue Div0 = Fail("#DIV/0!");
    public static readonly FormulaValue ValueError = Fail("#VALUE!");
    public static readonly FormulaValue Ref = Fail("#REF!");
    public static readonly FormulaValue Name = Fail("#NAME?");
    public static readonly FormulaValue Num = Fail("#NUM!");
    public static readonly FormulaValue NotAvailable = Fail("#N/A");
    public static readonly FormulaValue Null = Fail("#NULL!");

    private readonly string? _text;
    private readonly FormulaValue[,]? _array;

    private FormulaValue(ValueKind kind, double number, string? text, FormulaValue[,]? array)
    {
        Kind = kind;
        Number = number;
        _text = text;
        _array = array;
    }

    public ValueKind Kind { get; }

    public double Number { get; }

    public string Text => _text ?? string.Empty;

    public bool Boolean => Number != 0;

    public FormulaValue[,] Array => _array ?? new FormulaValue[1, 1];

    public bool IsError => Kind == ValueKind.Error;

    public static FormulaValue Of(double number) => double.IsFinite(number) ? new(ValueKind.Number, number, null, null) : Num;

    public static FormulaValue Of(string text) => new(ValueKind.Text, 0, text, null);

    public static FormulaValue Of(bool value) => value ? True : False;

    public static FormulaValue Of(FormulaValue[,] array) => new(ValueKind.Array, 0, null, array);

    public static FormulaValue Fail(string code) => new(ValueKind.Error, 0, code, null);

    /// <summary>The value of a cell as the workbook holds it (a date is its serial number).</summary>
    public static FormulaValue FromCell(XlsxCell? cell) => cell?.ValueType switch
    {
        null or XlsxValueType.Empty => Blank,
        XlsxValueType.Text => Of((string)cell.Value!),
        XlsxValueType.Boolean => Of((bool)cell.Value!),
        XlsxValueType.Error => Fail(((XlsxError)cell.Value!).Code),
        _ => Of(cell.NumberValue!.Value),
    };

    /// <summary>A single value: the top-left element of an array, the value itself otherwise.</summary>
    public FormulaValue Scalar() => Kind == ValueKind.Array ? Array[0, 0].Scalar() : this;

    /// <summary>The number of a value for arithmetic (ECMA-376 part 1, §18.17.2.2): text is parsed, a boolean is 1 or 0.</summary>
    public FormulaValue ToNumber()
    {
        var value = Scalar();
        return value.Kind switch
        {
            ValueKind.Number => value,
            ValueKind.Blank => Of(0),
            ValueKind.Boolean => Of(value.Number),
            ValueKind.Text => TryParseNumber(value.Text, out var number) ? Of(number) : ValueError,
            _ => value.Kind == ValueKind.Error ? value : ValueError,
        };
    }

    /// <summary>The text of a value as concatenation gives it: numbers with up to 15 significant digits, TRUE or FALSE.</summary>
    public FormulaValue ToText()
    {
        var value = Scalar();
        return value.Kind switch
        {
            ValueKind.Text => value,
            ValueKind.Blank => Of(string.Empty),
            ValueKind.Boolean => Of(value.Boolean ? "TRUE" : "FALSE"),
            ValueKind.Number => Of(value.Number.ToString("G15", CultureInfo.InvariantCulture)),
            _ => value,
        };
    }

    /// <summary>The boolean of a value for a condition: a number is true unless 0, text TRUE or FALSE.</summary>
    public FormulaValue ToBoolean()
    {
        var value = Scalar();
        return value.Kind switch
        {
            ValueKind.Boolean => value,
            ValueKind.Blank => False,
            ValueKind.Number => Of(value.Number != 0),
            ValueKind.Text when value.Text.Equals("TRUE", StringComparison.OrdinalIgnoreCase) => True,
            ValueKind.Text when value.Text.Equals("FALSE", StringComparison.OrdinalIgnoreCase) => False,
            ValueKind.Error => value,
            _ => ValueError,
        };
    }

    public static bool TryParseNumber(string text, out double number)
    {
        var trimmed = text.Trim();
        if (trimmed.EndsWith('%') && double.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
        {
            number = percent / 100;
            return true;
        }

        return double.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);
    }

    /// <summary>The value written back into a cell: number, text, boolean or error; a blank gives 0, as in Excel.</summary>
    public object? ToCellValue()
    {
        var value = Scalar();
        return value.Kind switch
        {
            ValueKind.Number => value.Number,
            ValueKind.Text => value.Text,
            ValueKind.Boolean => value.Boolean,
            ValueKind.Error => new XlsxError(value.Text),
            _ => 0.0,
        };
    }
}
