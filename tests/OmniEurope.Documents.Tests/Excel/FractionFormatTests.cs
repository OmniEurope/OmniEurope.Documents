// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Excel;

namespace OmniEurope.Documents.Tests.Excel;

/// <summary>
/// Fraction formats. The expected texts follow the documented rules of the number format codes (ECMA-376
/// Part 1, 18.8.30 and 18.8.31, built-in formats 12 <c># ?/?</c> and 13 <c># ??/??</c>; the published guide to
/// number format codes: <c>?</c> stands for a digit or a space so that slashes line up, the number of
/// placeholders of the denominator bounds its digits, a written denominator is kept). Each closest fraction was
/// worked out by hand from the continued fraction of the value: pi is [3; 7, 15, 1, 292...], so its best
/// fraction under 10 is 1/7 and under 100 is 14/99 (311/99 = 3.141414, closer than 22/7 = 3.142857 and
/// 289/92 = 3.141304); 0.123 is [0; 8, 7, 1, 2, 5] so 8/65 (0.1230769, error 7.7e-5) beats 7/57 (error 1.9e-4);
/// 0.3 under 10 is 2/7 (error 0.0143, against 0.0333 for 1/3).
/// </summary>
public sealed class FractionFormatTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(0.5, "# ?/?", " 1/2")]
    [InlineData(1.5, "# ?/?", "1 1/2")]
    [InlineData(0.75, "# ?/?", " 3/4")]
    [InlineData(Math.PI, "# ?/?", "3 1/7")]
    [InlineData(0.3, "# ?/?", " 2/7")]
    [InlineData(-1.25, "# ?/?", "-1 1/4")]
    [InlineData(-0.5, "# ?/?", "- 1/2")]
    [InlineData(2.0, "# ?/?", "2    ")]
    [InlineData(0.0, "# ?/?", "0    ")]
    [InlineData(0.999, "# ?/?", "1    ")]
    [InlineData(0.04, "# ?/?", "0    ")]
    [InlineData(-0.04, "# ?/?", "0    ")]
    [InlineData(0.07, "# ?/?", " 1/9")]
    [InlineData(1e20, "# ?/?", "100000000000000000000    ")]
    public void Formats_one_digit_fractions(double value, string format, string expected)
    {
        Assert.Equal(expected, NumberFormatter.Format(value, format, Invariant));
    }

    [Theory]
    [InlineData(Math.PI, "# ??/??", "3 14/99")]
    [InlineData(0.5, "# ??/??", "  1/2 ")]
    [InlineData(0.333, "# ??/??", "  1/3 ")]
    [InlineData(0.123, "# ??/??", "  8/65")]
    [InlineData(12.2, "# ??/??", "12  1/5 ")]
    [InlineData(1.0, "# ??/??", "1      ")]
    [InlineData(-3.75, "# ??/??", "-3  3/4 ")]
    [InlineData(0.001, "# ???/???", "   1/999")]
    [InlineData(Math.PI, "# ???/???", "3  16/113")]
    public void Formats_fractions_of_several_digits(double value, string format, string expected)
    {
        Assert.Equal(expected, NumberFormatter.Format(value, format, Invariant));
    }

    [Theory]
    [InlineData(0.5, "# ?/8", " 4/8")]
    [InlineData(1.3, "# ?/8", "1 2/8")]
    [InlineData(0.9375, "# ?/8", "1    ")]
    [InlineData(0.0625, "# ?/8", " 1/8")]
    [InlineData(0.25, "# ??/100", " 25/100")]
    [InlineData(1.07, "# ??/100", "1  7/100")]
    [InlineData(0.125, "# ??/100", " 13/100")]
    [InlineData(2.0, "# ??/100", "2       ")]
    [InlineData(1.5, "??/100", "150/100")]
    [InlineData(0.5, "# ?/16", " 8/16")]
    public void Keeps_a_written_denominator(double value, string format, string expected)
    {
        Assert.Equal(expected, NumberFormatter.Format(value, format, Invariant));
    }

    [Theory]
    [InlineData(1.5, "?/?", "3/2")]
    [InlineData(2.0, "?/?", "2/1")]
    [InlineData(0.0, "?/?", "0/1")]
    [InlineData(-2.25, "?/?", "-9/4")]
    [InlineData(0.5, "0 ?/?", "0 1/2")]
    [InlineData(0.5, "#0 ?/?", "0 1/2")]
    [InlineData(1234.5, "#,##0 ?/?", "1,234 1/2")]
    [InlineData(-1.5, "# ?/?;(# ?/?)", "(1 1/2)")]
    [InlineData(1.5, "\"x\" # ?/? \"in\"", "x 1 1/2 in")]
    [InlineData(0.5, "# ? / ?", " 1 / 2")]
    [InlineData(0.5, "# ?/?0", " 1/02")]
    [InlineData(0.5, "# ?/0?", " 1/2 ")]
    public void Handles_improper_fractions_signs_and_literals(double value, string format, string expected)
    {
        Assert.Equal(expected, NumberFormatter.Format(value, format, Invariant));
    }

    [Theory]
    [InlineData("0/", "2/")]
    [InlineData("/0", "/2")]
    [InlineData("0 \"km/h\"", "2 km/h")]
    public void Leaves_sections_that_are_not_fractions_to_the_number_rules(string format, string expected)
    {
        Assert.Equal(expected, NumberFormatter.Format(1.5, format, Invariant));
    }

    [Theory]
    [InlineData("0.0/0")]
    [InlineData("0/0E+0")]
    public void Does_not_read_a_fraction_beside_a_point_or_an_exponent(string format)
    {
        Assert.Null(FractionLayout.Find(FormatTokens.Tokenize(format)));
    }

    [Fact]
    public void Bounds_the_denominator_to_nine_digits()
    {
        Assert.Equal("1/2", NumberFormatter.Format(0.5, "0/##########", Invariant));
        Assert.Equal("1/3", NumberFormatter.Format(1 / 3.0, "0/##########", Invariant));
    }

    [Fact]
    public void Does_not_read_a_written_denominator_of_more_than_nine_digits()
    {
        Assert.Null(FractionLayout.Find(FormatTokens.Tokenize("# ?/1234567890")));
        Assert.Equal(123456789, FractionLayout.Find(FormatTokens.Tokenize("# ?/123456789"))!.FixedDenominator);
    }

    [Fact]
    public void Formats_the_built_in_fraction_formats_of_a_saved_workbook()
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("Fractions");
        sheet.Cell("A1").Value = 1.25;
        sheet.Cell("A1").Style = XlsxStyle.Default with { NumberFormat = "# ?/?" };
        sheet.Cell("A2").Value = Math.PI;
        sheet.Cell("A2").Style = XlsxStyle.Default with { NumberFormat = "# ??/??" };
        sheet.Cell("A3").Value = -0.375;
        sheet.Cell("A3").Style = XlsxStyle.Default with { NumberFormat = "# ?/8" };

        var reloaded = XlsxWorkbook.Load(workbook.ToArray()).Worksheets[0];

        Assert.Equal("1 1/4", reloaded.GetFormattedText(1, 1));
        Assert.Equal("3 14/99", reloaded.GetFormattedText(2, 1));
        Assert.Equal("- 3/8", reloaded.GetFormattedText(3, 1));
        Assert.Equal("-1 1/4", NumberFormatter.Format(-1.25, "# ?/?", CultureInfo.GetCultureInfo("fr-FR")));
    }

    [Theory]
    [InlineData(0.0, 9, 0, 1)]
    [InlineData(0.5, 9, 1, 2)]
    [InlineData(1e-6, 9, 0, 1)]
    [InlineData(0.07, 9, 1, 9)]
    [InlineData(0.142857, 99, 1, 7)]
    [InlineData(0.618034, 99, 55, 89)]
    [InlineData(0.618034, 9, 5, 8)]
    public void Finds_the_closest_fraction(double value, long maxDenominator, long numerator, long denominator)
    {
        Assert.Equal((numerator, denominator), FractionSection.Closest(value, maxDenominator));
    }
}
