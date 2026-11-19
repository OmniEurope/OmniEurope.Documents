// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Excel;

namespace OmniEurope.Documents.Tests.Excel;

public sealed class NumberFormatterConditionTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(12000.0, "[>=1000]#,##0,\"K\";0", "12K")]
    [InlineData(1000.0, "[>=1000]#,##0,\"K\";0", "1K")]
    [InlineData(999.0, "[>=1000]#,##0,\"K\";0", "999")]
    [InlineData(-5000.0, "[>=1000]#,##0,\"K\";0", "-5000")]
    [InlineData(100.0, "[Red][<=100]0;[Blue][>100]0", "100")]
    [InlineData(101.0, "[Red][<=100]0;[Blue][>100]0", "101")]
    [InlineData(-5.0, "[Red][<=100]0;[Blue][>100]0", "-5")]
    [InlineData(5.0, "[<5][Red]0.0;0", "5")]
    [InlineData(4.0, "[<5][Red]0.0;0", "4.0")]
    [InlineData(1.0, "[=1]\"one\";\"many\"", "one")]
    [InlineData(2.0, "[=1]\"one\";\"many\"", "many")]
    [InlineData(0.0, "[=1]\"one\";\"many\"", "many")]
    [InlineData(-2.0, "[=1]\"one\";\"many\"", "-many")]
    [InlineData(0.0, "[<>0]0.0;\"zero\"", "zero")]
    [InlineData(5.0, "[<>0]0.0;\"zero\"", "5.0")]
    [InlineData(-2.0, "[<>0]0.0;\"zero\"", "-2.0")]
    public void Picks_the_section_by_its_condition(double value, string format, string expected)
    {
        Assert.Equal(expected, NumberFormatter.Format(value, format, Invariant));
    }

    [Theory]
    [InlineData(999.0, "999")]
    [InlineData(-5.0, "-5")]
    [InlineData(1000.0, "1.0K")]
    [InlineData(999999.0, "1000.0K")]
    [InlineData(1000000.0, "1.0M")]
    [InlineData(1500000.0, "1.5M")]
    [InlineData(-1500000.0, "-1500000")]
    public void Picks_among_three_conditional_sections(double value, string expected)
    {
        Assert.Equal(expected, NumberFormatter.Format(value, "[<1000]0;[<1000000]0.0,\"K\";0.0,,\"M\"", Invariant));
    }

    [Theory]
    [InlineData(10.0, "big")]
    [InlineData(5.0, "5.0")]
    [InlineData(0.0, "0.0")]
    [InlineData(-1.0, "-1.00")]
    public void Falls_through_conditions_to_the_last_section(double value, string expected)
    {
        Assert.Equal(expected, NumberFormatter.Format(value, "[>5]\"big\";[>=0]0.0;0.00", Invariant));
    }

    [Theory]
    [InlineData(15.0, "15.0")]
    [InlineData(5.0, "5")]
    [InlineData(25.0, "25.0")]
    public void Shows_a_value_matching_no_condition_with_the_last_section(double value, string expected)
    {
        Assert.Equal(expected, NumberFormatter.Format(value, "[<10]0;[>20]0.0", Invariant));
    }

    [Theory]
    [InlineData(-6.0, "<6")]
    [InlineData(-5.0, "<5")]
    [InlineData(-4.0, "-4")]
    [InlineData(3.0, "3")]
    public void A_condition_admitting_only_negatives_shows_the_sign_in_its_own_text(double value, string expected)
    {
        Assert.Equal(expected, NumberFormatter.Format(value, "[<=-5]\"<\"0;0", Invariant));
    }

    [Fact]
    public void A_negative_only_condition_at_zero_shows_no_automatic_sign()
    {
        Assert.Equal("neg 5", NumberFormatter.Format(-5.0, "[<0]\"neg \"0;0", Invariant));
        Assert.Equal("5", NumberFormatter.Format(5.0, "[<0]\"neg \"0;0", Invariant));
    }

    [Fact]
    public void Ignores_a_bracket_inside_quotes_and_dates_follow_conditions()
    {
        Assert.Equal("[>9]7", NumberFormatter.Format(7.0, "\"[>9]\"0;\"x\"", Invariant));
        Assert.Equal("12:00", NumberFormatter.Format(0.5, "[<1]hh:mm;yyyy-mm-dd", Invariant));
        Assert.Equal("1970-01-01", NumberFormatter.Format(25569.0, "[<1]hh:mm;yyyy-mm-dd", Invariant));
    }

    [Fact]
    public void Formats_without_condition_keep_the_sign_based_pick()
    {
        Assert.Equal("(5.00)", NumberFormatter.Format(-5.0, "0.00;(0.00);\"zero\"", Invariant));
        Assert.Equal("zero", NumberFormatter.Format(0.0, "0.00;(0.00);\"zero\"", Invariant));
        Assert.Equal("5.00", NumberFormatter.Format(5.0, "0.00;(0.00);\"zero\"", Invariant));
        Assert.Equal("3", NumberFormatter.Format(3.0, "[Red]0;[Blue]-0", Invariant));
    }
}
