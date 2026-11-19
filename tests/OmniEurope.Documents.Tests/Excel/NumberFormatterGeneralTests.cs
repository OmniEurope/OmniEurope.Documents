// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Excel;

namespace OmniEurope.Documents.Tests.Excel;

public sealed class NumberFormatterGeneralTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(0.0, "0")]
    [InlineData(0.00001, "0.00001")]
    [InlineData(123456789012.0, "123456789012")]
    [InlineData(11700000.0, "11700000")]
    [InlineData(1e12, "1000000000000")]
    [InlineData(1e14, "100000000000000")]
    [InlineData(999999999999999.0, "999999999999999")]
    [InlineData(1e15, "1E+15")]
    [InlineData(1234567890123456789.0, "1.23456789012346E+18")]
    [InlineData(1.5e21, "1.5E+21")]
    [InlineData(1e-20, "1E-20")]
    [InlineData(1e-10, "0.0000000001")]
    [InlineData(1e-11, "1E-11")]
    [InlineData(0.0000123456789012345, "1.23456789012345E-05")]
    [InlineData(123456789.123456789, "123456789.123457")]
    [InlineData(-0.5, "-0.5")]
    [InlineData(-1234567890123456789.0, "-1.23456789012346E+18")]
    [InlineData(0.1 + 0.2, "0.3")]
    [InlineData(1.0 / 3.0, "0.333333333333333")]
    [InlineData(double.MaxValue, "1.79769313486232E+308")]
    public void Renders_up_to_15_significant_digits(double value, string expected)
    {
        Assert.Equal(expected, NumberFormatter.General(value, Invariant));
        Assert.Equal(expected, NumberFormatter.Format(value, "General", Invariant));
    }

    [Fact]
    public void Uses_the_culture_separators()
    {
        var french = CultureInfo.GetCultureInfo("fr-FR");

        Assert.Equal("1234,5", NumberFormatter.General(1234.5, french));
        Assert.Equal("1,5E+21", NumberFormatter.General(1.5e21, french));
        Assert.Equal("1E-20", NumberFormatter.General(1e-20, french));
    }

    [Theory]
    [InlineData(1234.5, "General\" units\"", "1234.5 units")]
    [InlineData(1235.0, "\"x\"General", "x1235")]
    [InlineData(-3.0, "General\" units\"", "-3 units")]
    [InlineData(5.0, "general\" u\"", "5 u")]
    [InlineData(5.0, "[Red]General", "5")]
    [InlineData(-2.5, "0;General", "2.5")]
    [InlineData(2.0, "0;General", "2")]
    [InlineData(0.0, "0;0;General\"!\"", "0!")]
    [InlineData(1e-20, "\"<\"General", "<1E-20")]
    [InlineData(150.0, "[>100]General\" big\";0", "150 big")]
    [InlineData(50.0, "[>100]General\" big\";0", "50")]
    public void Renders_General_inside_a_custom_section(double value, string format, string expected)
    {
        Assert.Equal(expected, NumberFormatter.Format(value, format, Invariant));
    }

    [Fact]
    public void Renders_General_inside_a_section_with_the_culture_separator()
    {
        Assert.Equal("1234,5 units", NumberFormatter.Format(1234.5, "General\" units\"", CultureInfo.GetCultureInfo("fr-FR")));
    }
}
