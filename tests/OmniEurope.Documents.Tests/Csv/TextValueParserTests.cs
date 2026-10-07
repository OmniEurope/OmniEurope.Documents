// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Csv;

namespace OmniEurope.Documents.Tests.Csv;

public sealed class TextValueParserTests
{
    [Theory]
    [InlineData("1 234,56", "1234.56")]
    [InlineData(" 1 234,56 ", "1234.56")]
    [InlineData("1 234,5", "1234.5")]
    [InlineData("1,234.56", "1234.56")]
    [InlineData("1.234,56", "1234.56")]
    [InlineData("1.234.567", "1234567")]
    [InlineData("-12,5", "-12.5")]
    [InlineData("12,5-", "-12.5")]
    [InlineData("0", "0")]
    [InlineData("1'000.25", "1000.25")]
    public void Parses_amounts(string text, string expected)
    {
        Assert.True(TextValueParser.TryParseDecimal(text, out var value));
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-")]
    [InlineData("12a")]
    public void Rejects_non_amounts(string text)
    {
        Assert.False(TextValueParser.TryParseDecimal(text, out _));
    }

    [Theory]
    [InlineData("20221231", true)]
    [InlineData("31/01/2022", false)]
    [InlineData("20221332", false)]
    public void Parses_compact_dates(string text, bool ok)
    {
        Assert.Equal(ok, TextValueParser.TryParseCompactDate(text, out _));
    }

    [Theory]
    [InlineData("1:30", 90 * 60)]
    [InlineData("25:00:30", 25 * 3600 + 30)]
    [InlineData("1,5", 90 * 60)]
    [InlineData("0.25", 15 * 60)]
    public void Parses_durations(string text, int seconds)
    {
        Assert.True(TextValueParser.TryParseDuration(text, out var duration));
        Assert.Equal(seconds, (int)duration.TotalSeconds);
    }

    [Theory]
    [InlineData("1:75")]
    [InlineData("a:b")]
    [InlineData("1:2:3:4")]
    [InlineData("-1")]
    public void Rejects_bad_durations(string text)
    {
        Assert.False(TextValueParser.TryParseDuration(text, out _));
    }
}
