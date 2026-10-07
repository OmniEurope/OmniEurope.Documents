// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;

namespace OmniEurope.Documents.Excel;

/// <summary>A1-style cell references: column letters, rows and ranges (1-based).</summary>
public static class CellReference
{
    /// <summary>Largest column of a worksheet (XFD).</summary>
    public const int MaxColumn = 16384;

    /// <summary>Largest row of a worksheet.</summary>
    public const int MaxRow = 1048576;

    /// <summary>The letters of a 1-based column: 1 is A, 27 is AA.</summary>
    public static string ColumnName(int column)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(column, MaxColumn);
        var builder = new StringBuilder(3);
        while (column > 0)
        {
            var remainder = (column - 1) % 26;
            builder.Insert(0, (char)('A' + remainder));
            column = (column - 1) / 26;
        }

        return builder.ToString();
    }

    /// <summary>The A1 reference of a cell.</summary>
    public static string Format(int row, int column) =>
        ColumnName(column) + row.ToString(CultureInfo.InvariantCulture);

    /// <summary>Parses <c>B12</c> (or <c>$B$12</c>) into a row and a column.</summary>
    public static bool TryParse(ReadOnlySpan<char> reference, out int row, out int column)
    {
        row = 0;
        column = 0;
        var i = 0;
        if (i < reference.Length && reference[i] == '$')
        {
            i++;
        }

        while (i < reference.Length && char.IsAsciiLetter(reference[i]))
        {
            column = (column * 26) + (char.ToUpperInvariant(reference[i]) - 'A' + 1);
            if (column > MaxColumn)
            {
                return false;
            }

            i++;
        }

        if (column == 0)
        {
            return false;
        }

        if (i < reference.Length && reference[i] == '$')
        {
            i++;
        }

        return int.TryParse(reference[i..], NumberStyles.None, CultureInfo.InvariantCulture, out row) && row is >= 1 and <= MaxRow;
    }

    /// <summary>Parses <c>A1</c> or <c>A1:C3</c> into its corners.</summary>
    public static bool TryParseRange(ReadOnlySpan<char> range, out int firstRow, out int firstColumn, out int lastRow, out int lastColumn)
    {
        lastRow = 0;
        lastColumn = 0;
        var colon = range.IndexOf(':');
        if (colon < 0)
        {
            var ok = TryParse(range, out firstRow, out firstColumn);
            lastRow = firstRow;
            lastColumn = firstColumn;
            return ok;
        }

        if (!TryParse(range[..colon], out firstRow, out firstColumn) || !TryParse(range[(colon + 1)..], out lastRow, out lastColumn))
        {
            return false;
        }

        (firstRow, lastRow) = (Math.Min(firstRow, lastRow), Math.Max(firstRow, lastRow));
        (firstColumn, lastColumn) = (Math.Min(firstColumn, lastColumn), Math.Max(firstColumn, lastColumn));
        return true;
    }
}

/// <summary>Conversions between dates and Excel serial numbers.</summary>
public static class ExcelDate
{
    private static readonly DateTime Epoch1900 = new(1899, 12, 30, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime Epoch1904 = new(1904, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>The serial number of <paramref name="value"/> in the 1900 (default) or 1904 date system.</summary>
    public static double ToSerial(DateTime value, bool date1904 = false)
    {
        if (date1904)
        {
            return (value - Epoch1904).TotalDays;
        }

        var serial = (value - Epoch1900).TotalDays;

        // Excel counts the non-existent 29 February 1900, so dates before 1 March 1900 are one lower.
        return serial < 61 ? serial - 1 : serial;
    }

    /// <summary>The date of a serial number in the 1900 (default) or 1904 date system.</summary>
    public static DateTime FromSerial(double serial, bool date1904 = false)
    {
        if (double.IsNaN(serial) || serial < 0 || serial > 2958466)
        {
            throw new ArgumentOutOfRangeException(nameof(serial), serial, "Outside the Excel date range.");
        }

        if (date1904)
        {
            return Epoch1904.AddMilliseconds(Math.Round(serial * 86_400_000d));
        }

        var adjusted = serial < 61 && serial >= 1 ? serial + 1 : serial;
        return Epoch1900.AddMilliseconds(Math.Round(adjusted * 86_400_000d));
    }
}
