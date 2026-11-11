// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion.WordHtml;
using OmniEurope.Documents.Pdf.Objects;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// Numbers written into PDF and HTML on a rounding midpoint: the same text on every framework (.NET 11 changed
/// Math.Round(double, digits) on midpoints, the number format did not change).
/// </summary>
public sealed class MidpointFormattingTests
{
    [Theory]
    [InlineData(303.05, "303.05pt")]
    [InlineData(2.675, "2.68pt")]
    [InlineData(1.005, "1.01pt")]
    [InlineData(-0.001, "-0pt")]
    public void Html_points_are_the_same_on_every_framework(double value, string expected)
    {
        Assert.Equal(expected, HtmlCss.Points(value));
    }

    [Theory]
    [InlineData(0.0005, "0.001")]
    [InlineData(2.6755, "2.676")]
    public void Html_numbers_are_the_same_on_every_framework(double value, string expected)
    {
        Assert.Equal(expected, HtmlCss.Number(value));
    }

    [Theory]
    [InlineData(303.00005, "303.0001")]
    [InlineData(1.00005, "1.0001")]
    [InlineData(-0.00001, "0")]
    public void Pdf_reals_are_the_same_on_every_framework(double value, string expected)
    {
        Assert.Equal(expected, PdfFormat.Real(value));
    }
}
