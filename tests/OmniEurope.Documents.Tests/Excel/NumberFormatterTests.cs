// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Excel;

namespace OmniEurope.Documents.Tests.Excel;

public sealed class NumberFormatterTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(1234.567, "0.00", "1234.57")]
    [InlineData(1234.567, "#,##0.00", "1,234.57")]
    [InlineData(1234567.0, "#,##0", "1,234,567")]
    [InlineData(0.256, "0%", "26%")]
    [InlineData(0.256, "0.0%", "25.6%")]
    [InlineData(-5.0, "0.00", "-5.00")]
    [InlineData(-5.0, "0.00;(0.00)", "(5.00)")]
    [InlineData(0.0, "0.00;(0.00);\"zero\"", "zero")]
    [InlineData(1234567.0, "#,##0,", "1,235")]
    [InlineData(12345.678, "0.00E+00", "1.23E+04")]
    [InlineData(0.000123, "0.0E+00", "1.2E-04")]
    [InlineData(0.5, "#.##", ".5")]
    [InlineData(5.0, "000", "005")]
    [InlineData(12.5, ".00", "12.50")]
    [InlineData(1.5, "0.0#", "1.5")]
    [InlineData(1.567, "0.0#", "1.57")]
    [InlineData(1234.5, "[$€-40C] #,##0.00", "€ 1,234.50")]
    [InlineData(1234.0, "\"Total: \"0", "Total: 1234")]
    [InlineData(1234.0, "0 \\k", "1234 k")]
    [InlineData(3.0, "[Red]0;[Blue]-0", "3")]
    [InlineData(2.5, "0", "3")]
    [InlineData(-0.001, "0.00", "0.00")]
    [InlineData(11700000.0, "General", "11700000")]
    [InlineData(0.30000000000000004, "General", "0.3")]
    [InlineData(1e12, "General", "1E+12")]
    [InlineData(-2.5, "General", "-2.5")]
    public void Formats_numbers(double value, string format, string expected)
    {
        Assert.Equal(expected, NumberFormatter.Format(value, format, Invariant));
    }

    [Theory]
    [InlineData("yyyy-mm-dd", "2026-10-06")]
    [InlineData("dd/mm/yyyy", "06/10/2026")]
    [InlineData("d mmm yy", "6 Oct 26")]
    [InlineData("dddd d mmmm", "Tuesday 6 October")]
    [InlineData("hh:mm:ss", "13:05:09")]
    [InlineData("h:mm AM/PM", "1:05 PM")]
    [InlineData("m/d/yyyy h:mm", "10/6/2026 13:05")]
    [InlineData("mmm", "Oct")]
    public void Formats_dates_and_times(string format, string expected)
    {
        var serial = ExcelDate.ToSerial(new DateTime(2026, 10, 6, 13, 5, 9));

        Assert.Equal(expected, NumberFormatter.Format(serial, format, Invariant));
        Assert.True(NumberFormatter.IsDateFormat(format));
    }

    [Fact]
    public void Formats_elapsed_time_and_fractions_of_a_second()
    {
        Assert.Equal("36:00", NumberFormatter.Format(1.5, "[h]:mm", Invariant));
        Assert.Equal("01:02.5", NumberFormatter.Format(62.5 / 86400, "mm:ss.0", Invariant));
        Assert.False(NumberFormatter.IsDateFormat("0.00"));
        Assert.False(NumberFormatter.IsDateFormat("\"day\" 0"));
    }

    [Fact]
    public void Uses_the_culture_separators_and_names()
    {
        var french = CultureInfo.GetCultureInfo("fr-FR");

        Assert.Equal("1 234,57", NumberFormatter.Format(1234.567, "#,##0.00", french));
        Assert.Equal("6 octobre", NumberFormatter.Format(ExcelDate.ToSerial(new DateTime(2026, 10, 6)), "d mmmm", french));
    }

    [Fact]
    public void Formats_text_with_the_text_section()
    {
        Assert.Equal("pre abc", NumberFormatter.FormatText("abc", "\"pre \"@"));
        Assert.Equal("[abc]", NumberFormatter.FormatText("abc", "0;0;0;\"[\"@\"]\""));
        Assert.Equal("abc", NumberFormatter.FormatText("abc", "0.00"));
    }

    [Theory]
    [InlineData(1.0, 1900, 1, 1)]
    [InlineData(59.0, 1900, 2, 28)]
    [InlineData(61.0, 1900, 3, 1)]
    [InlineData(46301.0, 2026, 10, 6)]
    public void Converts_serials_with_the_1900_leap_year_quirk(double serial, int year, int month, int day)
    {
        var date = new DateTime(year, month, day);

        Assert.Equal(date, ExcelDate.FromSerial(serial));
        Assert.Equal(serial, ExcelDate.ToSerial(date));
    }
}
