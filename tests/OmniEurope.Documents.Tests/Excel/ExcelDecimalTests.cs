// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Excel;

namespace OmniEurope.Documents.Tests.Excel;

/// <summary>
/// The double to decimal conversion at Excel's 15 significant digits, identical on every framework (the runtime's
/// own conversion is exact from .NET 11 on).
/// </summary>
public sealed class ExcelDecimalTests
{
    [Theory]
    [InlineData(2.675, "2.675")]
    [InlineData(0.1 + 0.2, "0.3")]
    [InlineData(123456789012345678d, "123456789012346000")]
    [InlineData(-1.005, "-1.005")]
    [InlineData(-0.0, "0")]
    [InlineData(1e-30, "0")]
    [InlineData(7.9e27, "7900000000000000000000000000")]
    public void A_double_becomes_its_15_digit_decimal(double value, string expected)
    {
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), ExcelDecimal.From(value));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(1e29)]
    [InlineData(-1e300)]
    public void A_value_outside_the_decimal_range_overflows(double value)
    {
        Assert.Throws<OverflowException>(() => ExcelDecimal.From(value));
    }

#if NET10_0
    [Fact]
    public void It_matches_the_net10_runtime_conversion()
    {
        // Before .NET 11 the runtime rounded to 15 significant digits too, less exactly (ties, and tiny values cut by the
        // 28 decimal places): the two agree within one unit of the 15th significant digit.
        var random = new Random(20261111);
        for (var i = 0; i < 20_000; i++)
        {
            var value = (random.NextDouble() - 0.5) * Math.Pow(10, random.Next(-30, 28));
            var unit = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(value))) - 14);
            Assert.InRange(Math.Abs((double)((decimal)value - ExcelDecimal.From(value))), 0, (unit * 1.0001) + 1e-28);
        }
    }
#endif
}