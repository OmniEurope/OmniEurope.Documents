// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Html;

namespace OmniEurope.Documents.Tests.Html;

/// <summary>A descendant combinator survives right after an attribute or pseudo-class closes.</summary>
public sealed class HtmlSelectorDescendantTests
{
    [Theory]
    [InlineData("div span")]
    [InlineData("div > span")]
    [InlineData("div[title] span")]
    [InlineData("div:nth-child(1) span")]
    [InlineData("div:not(.missing) span")]
    [InlineData("div[title]  >  span")]
    public void Descendant_and_explicit_combinators_select_the_same_span(string selector)
    {
        var fragment = HtmlParser.ParseFragment("<div title=a><span>kept</span></div>");

        var found = fragment.QuerySelectorAll(selector);

        Assert.Single(found);
        Assert.Equal("kept", found[0].TextContent);
    }

    [Fact]
    public void Adjacent_compounds_without_a_combinator_are_still_rejected()
    {
        Assert.Throws<FormatException>(() => HtmlParser.ParseFragment("<div></div>").QuerySelectorAll("div[title]span"));
    }
}
