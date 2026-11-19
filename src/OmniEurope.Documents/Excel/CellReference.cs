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

/// <summary>
/// Conversions between dates and Excel serial numbers, in the 1900 (default) or 1904 date system. The 1900 system is
/// counted as the spreadsheet counts it: serial 1 is 1 January 1900, serial 60 is the fictitious 29 February 1900
/// and serial 61 is 1 March 1900.
/// </summary>
public static class ExcelDate
{
    private const double MillisecondsPerDay = 86_400_000d;
    private static readonly DateTime Epoch1900 = new(1899, 12, 30, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime Epoch1904 = new(1904, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>
    /// The serial number of <paramref name="value"/>. In the 1900 system the dates from 1 January 1900 to 28 February
    /// 1900 give the serials 1 to 59 and the dates from 1 March 1900 on give 61 and more: no date gives 60, so
    /// <see cref="FromSerial"/> followed by this method does not give 60 back. A value on 30 December 1899 is a time
    /// of day (serial 0 plus its fraction), as <see cref="FromSerial"/> gives it.
    /// </summary>
    public static double ToSerial(DateTime value, bool date1904 = false)
    {
        if (date1904)
        {
            return (value - Epoch1904).TotalDays;
        }

        var serial = (value - Epoch1900).TotalDays;

        // The spreadsheet counts the non-existent 29 February 1900, so dates before 1 March 1900 are one lower.
        return serial is >= 1 and < 61 ? serial - 1 : serial;
    }

    /// <summary>
    /// The date of a serial number. In the 1900 system the serials 1 to 59 give 1 January to 28 February 1900, 61 and
    /// more give 1 March 1900 on. Serial 60 is the fictitious 29 February 1900, which <see cref="DateTime"/> cannot
    /// hold: it and its fraction (60.x) give 28 February 1900 with the time of day. Serial 0 and its fraction (a time
    /// of day, shown as 1900-01-00 by the spreadsheet) give 30 December 1899 with the time of day.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The serial is negative, not a number, or after 31 December
    /// 9999 in its date system.</exception>
    public static DateTime FromSerial(double serial, bool date1904 = false)
    {
        if (!IsInRange(serial, date1904))
        {
            throw new ArgumentOutOfRangeException(nameof(serial), serial, "Outside the Excel date range.");
        }

        return (date1904 ? Epoch1904 : Epoch1900).AddMilliseconds(Milliseconds(serial, date1904));
    }

    /// <summary>True when <see cref="FromSerial"/> gives a date for <paramref name="serial"/>: the bound depends
    /// on the date system (the 1904 one ends 1,462 days sooner) and holds after rounding to the millisecond.</summary>
    internal static bool IsInRange(double serial, bool date1904) =>
        serial >= 0 && Milliseconds(serial, date1904) <= ((date1904 ? DateTime.MaxValue - Epoch1904 : DateTime.MaxValue - Epoch1900).Ticks / TimeSpan.TicksPerMillisecond);

    /// <summary>True for serial 60 and its fraction in the 1900 system: the fictitious 29 February 1900, which
    /// <see cref="FromSerial"/> cannot give as a date.</summary>
    internal static bool IsFictitiousDay(double serial, bool date1904)
    {
        var milliseconds = Math.Round(serial * MillisecondsPerDay);
        return !date1904 && milliseconds >= 60 * MillisecondsPerDay && milliseconds < 61 * MillisecondsPerDay;
    }

    // Milliseconds after the epoch in the real calendar. The spreadsheet counts the non-existent 29 February 1900, so
    // the serials from 1 to 59 are one day early; serial 60 stays on 28 February.
    private static double Milliseconds(double serial, bool date1904)
    {
        var milliseconds = Math.Round(serial * MillisecondsPerDay);
        return !date1904 && milliseconds >= MillisecondsPerDay && milliseconds < 60 * MillisecondsPerDay ? milliseconds + MillisecondsPerDay : milliseconds;
    }
}
