// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Excel;

/// <summary>A serial number broken into the parts the spreadsheet shows for it.</summary>
internal readonly record struct ExcelDateParts(int Year, int Month, int Day, DayOfWeek DayOfWeek, int Hour, int Minute, int Second, int Millisecond);

/// <summary>
/// The calendar of the spreadsheet on serial numbers: in the 1900 system serial 60 is the fictitious 29 February 1900,
/// serial 0 is 1900-01-00, and the weekday counts serial 1 (1 January 1900) as a Sunday although it was a Monday.
/// The 1904 system follows the real calendar.
/// </summary>
internal static class ExcelCalendar
{
    /// <summary>Year, month, day, weekday and time of day of an in-range serial (see <see cref="ExcelDate.IsInRange"/>).</summary>
    public static ExcelDateParts Split(double serial, bool date1904)
    {
        var date = ExcelDate.FromSerial(serial, date1904);
        if (date1904)
        {
            return Parts(date, date.Year, date.Month, date.Day, date.DayOfWeek);
        }

        // Whole days of the serial once rounded to the millisecond, as FromSerial does.
        var days = (long)Math.Floor(Math.Round(serial * 86_400_000d) / 86_400_000d);
        var weekday = (DayOfWeek)((days + 6) % 7);
        return days switch
        {
            0 => Parts(date, 1900, 1, 0, weekday),
            60 => Parts(date, 1900, 2, 29, weekday),
            < 60 => Parts(date, date.Year, date.Month, date.Day, weekday),
            _ => Parts(date, date.Year, date.Month, date.Day, date.DayOfWeek),
        };
    }

    /// <summary>Days of a month; February 1900 has 29 in the 1900 system, the spreadsheet's leap day.</summary>
    public static int DaysInMonth(int year, int month, bool date1904) =>
        !date1904 && year == 1900 && month == 2 ? 29 : DateTime.DaysInMonth(year, month);

    /// <summary>
    /// The serial number of day <paramref name="day"/> of the month starting on <paramref name="firstOfMonth"/>. The day
    /// may lie outside the month (0 is the day before, 30 in February is in March), as in DATE: counting from the first
    /// of the month keeps 29 February 1900 on serial 60.
    /// </summary>
    public static double Serial(DateTime firstOfMonth, double day, bool date1904) =>
        ExcelDate.ToSerial(firstOfMonth, date1904) + day - 1;

    private static ExcelDateParts Parts(DateTime date, int year, int month, int day, DayOfWeek weekday) =>
        new(year, month, day, weekday, date.Hour, date.Minute, date.Second, date.Millisecond);
}
