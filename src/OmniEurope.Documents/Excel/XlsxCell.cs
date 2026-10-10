// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;

namespace OmniEurope.Documents.Excel;

/// <summary>The kind of value a cell holds.</summary>
public enum XlsxValueType
{
    /// <summary>No value.</summary>
    Empty,

    /// <summary>Text.</summary>
    Text,

    /// <summary>A number.</summary>
    Number,

    /// <summary>A date and time (stored as a serial number with a date format). A duration, shown with an
    /// elapsed-time format such as <c>[h]:mm</c>, is a <see cref="Number"/> of days.</summary>
    DateTime,

    /// <summary>A boolean.</summary>
    Boolean,

    /// <summary>An error value such as <c>#DIV/0!</c>.</summary>
    Error,
}

/// <summary>A cell of a worksheet.</summary>
public sealed class XlsxCell
{
    private object? _value;

    internal XlsxCell(XlsxWorksheet worksheet, int row, int column)
    {
        Worksheet = worksheet;
        Row = row;
        Column = column;
    }

    /// <summary>The worksheet holding the cell.</summary>
    public XlsxWorksheet Worksheet { get; }

    /// <summary>The 1-based row.</summary>
    public int Row { get; }

    /// <summary>The 1-based column.</summary>
    public int Column { get; }

    /// <summary>The A1 reference.</summary>
    public string Reference => CellReference.Format(Row, Column);

    /// <summary>The kind of <see cref="Value"/>.</summary>
    public XlsxValueType ValueType { get; private set; }

    /// <summary>
    /// The value: null, <see cref="string"/>, <see cref="double"/>, <see cref="bool"/>, <see cref="DateTime"/>
    /// or <see cref="XlsxError"/>. Setting accepts any integer or floating type, <see cref="decimal"/>,
    /// <see cref="DateOnly"/>, <see cref="DateTimeOffset"/> and <see cref="TimeSpan"/> (stored as a fraction of
    /// a day); a date gets a date format when the cell has the general format.
    /// </summary>
    public object? Value
    {
        get => _value;
        set => SetValue(value);
    }

    /// <summary>The formula without its leading <c>=</c>, or null. Its last computed result is <see cref="Value"/>,
    /// computed again by <see cref="XlsxWorkbook.Recalculate"/>.</summary>
    public string? Formula { get; set; }

    /// <summary>The look of the cell.</summary>
    public XlsxStyle Style { get; set; } = XlsxStyle.Default;

    /// <summary>The value as Excel would display it with <see cref="XlsxStyle.NumberFormat"/> (invariant culture).</summary>
    public string FormattedText => FormatValue(CultureInfo.InvariantCulture);

    /// <summary>The value as Excel would display it, with the separators of <paramref name="culture"/>.</summary>
    public string FormatValue(CultureInfo culture) => ValueType switch
    {
        XlsxValueType.Empty => string.Empty,
        XlsxValueType.Text => NumberFormatter.FormatText((string)_value!, Style.NumberFormat),
        XlsxValueType.Number => NumberFormatter.Format((double)_value!, Style.NumberFormat, culture, Worksheet.Workbook.Date1904),
        XlsxValueType.DateTime => NumberFormatter.Format(ExcelDate.ToSerial((DateTime)_value!, Worksheet.Workbook.Date1904), Style.NumberFormat, culture, Worksheet.Workbook.Date1904),
        XlsxValueType.Boolean => (bool)_value! ? "TRUE" : "FALSE",
        _ => ((XlsxError)_value!).Code,
    };

    /// <summary>The value as a number when it is one (a date gives its serial number).</summary>
    public double? NumberValue => ValueType switch
    {
        XlsxValueType.Number => (double)_value!,
        XlsxValueType.DateTime => ExcelDate.ToSerial((DateTime)_value!, Worksheet.Workbook.Date1904),
        _ => null,
    };

    private void SetValue(object? value)
    {
        (_value, ValueType) = Convert(value);
        if (ValueType == XlsxValueType.Number && !double.IsFinite((double)_value!))
        {
            (_value, ValueType) = (new XlsxError("#NUM!"), XlsxValueType.Error);
        }

        if (Style.NumberFormat == "General" && DefaultFormat(value, _value) is { } format)
        {
            Style = Style with { NumberFormat = format };
        }
    }

    private static (object? Value, XlsxValueType Type) Convert(object? value) => value switch
    {
        null => (null, XlsxValueType.Empty),
        string s => (s, XlsxValueType.Text),
        bool b => (b, XlsxValueType.Boolean),
        DateTime d => (d, XlsxValueType.DateTime),
        DateOnly d => (d.ToDateTime(TimeOnly.MinValue), XlsxValueType.DateTime),
        DateTimeOffset d => (d.DateTime, XlsxValueType.DateTime),
        TimeSpan t => (t.TotalDays, XlsxValueType.Number),
        XlsxError e => (e, XlsxValueType.Error),
        char c => (c.ToString(), XlsxValueType.Text),
        IConvertible n when IsNumeric(n) => (n.ToDouble(CultureInfo.InvariantCulture), XlsxValueType.Number),
        _ => throw new ArgumentException($"A cell cannot hold a {value.GetType().Name}.", nameof(value)),
    };

    // Dates and durations stored without a number format get a readable one.
    private static string? DefaultFormat(object? original, object? stored) => (original, stored) switch
    {
        (TimeSpan, _) => "[h]:mm:ss",
        (_, DateTime date) => date.TimeOfDay == TimeSpan.Zero ? "yyyy-mm-dd" : "yyyy-mm-dd hh:mm:ss",
        _ => null,
    };

    private static bool IsNumeric(IConvertible value) => value.GetTypeCode() is TypeCode.SByte or TypeCode.Byte or TypeCode.Int16
        or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single
        or TypeCode.Double or TypeCode.Decimal;

    internal void SetLoadedValue(object? value, XlsxValueType type)
    {
        _value = value;
        ValueType = type;
    }
}

/// <summary>An Excel error value.</summary>
/// <param name="Code">The error code: <c>#NULL!</c>, <c>#DIV/0!</c>, <c>#VALUE!</c>, <c>#REF!</c>, <c>#NAME?</c>,
/// <c>#NUM!</c>, <c>#N/A</c>...</param>
public sealed record XlsxError(string Code);
