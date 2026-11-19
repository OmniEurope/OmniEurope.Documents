// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Excel;

namespace OmniEurope.Documents.Tests.Excel;

/// <summary>
/// The 1900 date system as the spreadsheet counts it: serial 1 is 1900-01-01 (a Sunday for WEEKDAY and the day
/// names), serial 60 is the fictitious 29 February 1900, serial 61 is 1900-03-01. The 1904 system is untouched.
/// </summary>
public sealed class ExcelDate1900Tests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    [Theory]
    [InlineData(1.0, 1900, 1, 1, 0, 0)]
    [InlineData(59.0, 1900, 2, 28, 0, 0)]
    [InlineData(60.0, 1900, 2, 28, 0, 0)]
    [InlineData(60.5, 1900, 2, 28, 12, 0)]
    [InlineData(60.75, 1900, 2, 28, 18, 0)]
    [InlineData(61.0, 1900, 3, 1, 0, 0)]
    [InlineData(61.5, 1900, 3, 1, 12, 0)]
    public void Serial_60_and_its_fraction_give_28_february_1900_with_the_time_of_day(double serial, int year, int month, int day, int hour, int minute)
    {
        Assert.Equal(new DateTime(year, month, day, hour, minute, 0), ExcelDate.FromSerial(serial));
    }

    [Theory]
    [InlineData(1900, 1, 1, 1.0)]
    [InlineData(1900, 2, 28, 59.0)]
    [InlineData(1900, 3, 1, 61.0)]
    [InlineData(1900, 3, 2, 62.0)]
    public void Dates_from_1_january_1900_give_the_spreadsheet_serial(int year, int month, int day, double serial)
    {
        Assert.Equal(serial, ExcelDate.ToSerial(new DateTime(year, month, day)));
    }

    [Fact]
    public void Serial_60_cannot_come_back_from_a_date()
    {
        Assert.Equal(59.0, ExcelDate.ToSerial(ExcelDate.FromSerial(60)));
        Assert.Equal(59.5, ExcelDate.ToSerial(ExcelDate.FromSerial(60.5)));
    }

    [Fact]
    public void The_1904_system_is_unchanged_around_1900_serials()
    {
        Assert.Equal(new DateTime(1904, 3, 1), ExcelDate.FromSerial(60, date1904: true));
        Assert.Equal(60.0, ExcelDate.ToSerial(new DateTime(1904, 3, 1), date1904: true));
        Assert.Equal("1904-03-01", NumberFormatter.Format(60, "yyyy-mm-dd", English, date1904: true));
        Assert.Equal("Tuesday", NumberFormatter.Format(60, "dddd", English, date1904: true));
    }

    [Theory]
    [InlineData(1.0, "yyyy-mm-dd", "1900-01-01")]
    [InlineData(59.0, "yyyy-mm-dd", "1900-02-28")]
    [InlineData(60.0, "yyyy-mm-dd", "1900-02-29")]
    [InlineData(60.0, "d mmmm yyyy", "29 February 1900")]
    [InlineData(60.5, "yyyy-mm-dd hh:mm", "1900-02-29 12:00")]
    [InlineData(61.0, "yyyy-mm-dd", "1900-03-01")]
    [InlineData(0.0, "yyyy-mm-dd", "1900-01-00")]
    [InlineData(0.5, "yyyy-mm-dd hh:mm", "1900-01-00 12:00")]
    public void Dates_show_as_the_spreadsheet_shows_them(double serial, string format, string expected)
    {
        Assert.Equal(expected, NumberFormatter.Format(serial, format, English));
    }

    [Theory]
    [InlineData(0.0, "Saturday", "Sat")]
    [InlineData(1.0, "Sunday", "Sun")]
    [InlineData(2.0, "Monday", "Mon")]
    [InlineData(7.0, "Saturday", "Sat")]
    [InlineData(8.0, "Sunday", "Sun")]
    [InlineData(59.0, "Tuesday", "Tue")]
    [InlineData(60.0, "Wednesday", "Wed")]
    [InlineData(61.0, "Thursday", "Thu")]
    [InlineData(62.0, "Friday", "Fri")]
    [InlineData(46303.0, "Thursday", "Thu")]
    public void Day_names_follow_the_spreadsheet_weekday(double serial, string full, string abbreviated)
    {
        Assert.Equal(full, NumberFormatter.Format(serial, "dddd", English));
        Assert.Equal(abbreviated, NumberFormatter.Format(serial, "ddd", English));
    }

    [Fact]
    public void A_time_only_cell_keeps_its_time()
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("Times");
        sheet.Cell("A1").Value = 0.5;
        sheet.Cell("A1").Style = XlsxStyle.Default with { NumberFormat = "h:mm" };

        var cell = XlsxWorkbook.Load(workbook.ToArray()).Worksheet("Times").Cell("A1");

        Assert.Equal("12:00", cell.FormatValue(English));
        Assert.Equal(0.5, cell.NumberValue);
    }

    [Fact]
    public void A_cell_on_serial_60_keeps_the_serial_and_shows_29_february_1900()
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("Dates");
        sheet.Cell("A1").Value = 60.0;
        sheet.Cell("A1").Style = XlsxStyle.Default with { NumberFormat = "yyyy-mm-dd" };
        sheet.Cell("A2").Value = 59.0;
        sheet.Cell("A2").Style = XlsxStyle.Default with { NumberFormat = "yyyy-mm-dd" };

        var reread = XlsxWorkbook.Load(workbook.ToArray()).Worksheet("Dates");

        Assert.Equal(60.0, reread.Cell("A1").NumberValue);
        Assert.Equal("1900-02-29", reread.Cell("A1").FormatValue(English));
        Assert.Equal(new DateTime(1900, 2, 28), reread.Cell("A2").Value);
        Assert.Equal("1900-02-28", reread.Cell("A2").FormatValue(English));
    }
}
