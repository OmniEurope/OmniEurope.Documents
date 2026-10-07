// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Html;

namespace OmniEurope.Documents.Tests.Html;

/// <summary>
/// Selectors (Selectors Level 3: attribute operators, structural pseudo-classes, sibling combinators), DOM
/// editing rules (DOM Standard: insertion, removal, hierarchy checks) and serialisation (HTML Standard §13.3).
/// </summary>
public sealed class HtmlDomTests
{
    private const string Sample = "<div lang='en-GB' class='a b'><span></span> <i>x</i></div><p lang='en'><b>seul</b></p><ul><li>1</li><li>2</li><li>3</li></ul><p class='fin'>z</p>";

    [Theory]
    [InlineData("[lang|=en]", 2)]
    [InlineData("[class~=b]", 1)]
    [InlineData("[class='a b']", 1)]
    [InlineData("[class=\"fin\"]", 1)]
    [InlineData(":empty", 1)]
    [InlineData("b:only-child", 1)]
    [InlineData("span + i", 1)]
    [InlineData("ul ~ p", 1)]
    [InlineData("li:nth-child(odd)", 2)]
    [InlineData("li:nth-child(even)", 1)]
    [InlineData("div i", 1)]
    public void Selects_by_attribute_operators_structure_and_siblings(string selector, int count)
    {
        Assert.Equal(count, HtmlParser.ParseFragment(Sample).QuerySelectorAll(selector).Count);
    }

    [Theory]
    [InlineData("a!")]
    [InlineData("[a%=b]")]
    [InlineData("[a=b")]
    [InlineData(":nth-child")]
    [InlineData(":nth-child(2n+1)")]
    [InlineData(":not")]
    [InlineData(":not(a")]
    [InlineData(":not(a b)")]
    [InlineData("[a='b]")]
    [InlineData(".")]
    public void Malformed_selectors_are_rejected(string selector)
    {
        Assert.Throws<FormatException>(() => HtmlSelector.Parse(selector));
    }

    [Fact]
    public void Elements_match_selectors_and_find_their_closest_ancestor()
    {
        var b = HtmlParser.ParseFragment(Sample).QuerySelector("b")!;

        Assert.True(b.Matches("p > b"));
        Assert.Equal("p", b.Closest("[lang]")!.LocalName);
        Assert.Null(b.Closest("table"));
    }

    [Fact]
    public void Insertion_and_removal_follow_the_hierarchy_rules()
    {
        var fragment = HtmlParser.ParseFragment("<div><p>1</p><p>2</p></div>");
        var div = fragment.QuerySelector("div")!;
        var (first, second) = (div.Children.First(), div.Children.Last());

        div.InsertBefore(new HtmlText("fin"), null);
        div.InsertBefore(first, div.LastChild);
        first.SetAttribute("id", "a");
        first.SetAttribute("id", "b");

        Assert.Equal("<div><p>2</p><p id=\"b\">1</p>fin</div>", fragment.InnerHtml);
        Assert.Null(fragment.PreviousSibling);
        Assert.Same(second, first.PreviousSibling);
        Assert.Throws<ArgumentException>(() => div.InsertBefore(new HtmlText("x"), new HtmlText("ailleurs")));
        Assert.Throws<ArgumentException>(() => div.RemoveChild(new HtmlText("ailleurs")));
        Assert.Throws<InvalidOperationException>(() => first.AppendChild(div));
    }

    [Fact]
    public void Serialisation_escapes_text_and_attributes_but_not_raw_text()
    {
        var fragment = new HtmlFragment();
        var p = fragment.AppendChild(new HtmlElement("p"));
        p.SetAttribute("title", "a \"<b>\"");
        p.AppendChild(new HtmlText("1 < 2 & 3 > 0 !"));
        p.AppendChild(new HtmlComment("note"));
        fragment.AppendChild(new HtmlElement("script")).AppendChild(new HtmlText("if (a < b) {}"));

        Assert.Equal(
            "<p title=\"a&nbsp;&quot;&lt;b&gt;&quot;\">1 &lt; 2 &amp; 3 &gt; 0&nbsp;!<!--note--></p><script>if (a < b) {}</script>",
            fragment.OuterHtml);
        Assert.Equal("1 < 2 & 3 > 0 !", p.TextContent);
        Assert.Equal(string.Empty, p.ChildNodes[1].TextContent);
    }

    [Theory]
    [InlineData("<table><td>x</td></table>", "<table><tbody><tr><td>x</td></tr></tbody></table>")]
    [InlineData("<table><tr><td>x</td></tr></table>", "<table><tbody><tr><td>x</td></tr></tbody></table>")]
    [InlineData("<button><p>a</button>b", "<button><p>a</p></button>b")]
    [InlineData("<script>a < b", "<script>a < b</script>")]
    [InlineData("a</>b", "ab")]
    [InlineData("<!-->a", "<!---->a")]
    [InlineData("<!doctype html><?php x ?>a", "<!--?php x ?-->a")]
    [InlineData("<p title=>a</p><p title", "<p title=\"\">a</p><p title=\"\"></p>")]
    public void Markup_is_repaired_like_a_browser(string html, string expected)
    {
        Assert.Equal(expected, HtmlParser.ParseFragment(html).InnerHtml);
    }
}
