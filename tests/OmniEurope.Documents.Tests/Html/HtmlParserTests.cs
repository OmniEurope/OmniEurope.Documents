// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Html;

namespace OmniEurope.Documents.Tests.Html;

public sealed class HtmlParserTests
{
    [Fact]
    public void Parses_a_document_into_head_and_body()
    {
        var document = HtmlParser.ParseDocument("<!DOCTYPE html><html lang=fr><title>T &amp; co</title><meta charset=utf-8><p>Hello <b>world</b>");

        Assert.Equal("T & co", document.Title);
        Assert.Equal("fr", document.DocumentElement.GetAttribute("lang"));
        Assert.NotNull(document.Head.QuerySelector("meta"));
        Assert.Equal("<p>Hello <b>world</b></p>", document.Body.InnerHtml);
        Assert.Equal("Hello world", document.Body.TextContent);
    }

    [Theory]
    [InlineData("<p>a<p>b", "<p>a</p><p>b</p>")]
    [InlineData("<ul><li>a<li>b</ul>", "<ul><li>a</li><li>b</li></ul>")]
    [InlineData("<dl><dt>t<dd>d<dt>u</dl>", "<dl><dt>t</dt><dd>d</dd><dt>u</dt></dl>")]
    [InlineData("<table><tr><td>a<td>b<tr><td>c</table>", "<table><tbody><tr><td>a</td><td>b</td></tr><tr><td>c</td></tr></tbody></table>")]
    [InlineData("<p>x<div>y</div>", "<p>x</p><div>y</div>")]
    [InlineData("<b>bold</i></b>", "<b>bold</b>")]
    [InlineData("a</p>b", "a<p></p>b")]
    [InlineData("x<br/>y</br>z", "x<br>y<br>z")]
    [InlineData("<div><span>open", "<div><span>open</span></div>")]
    [InlineData("<h1>a<h2>b</h2>", "<h1>a</h1><h2>b</h2>")]
    [InlineData("<a href=1>one<a href=2>two</a>", "<a href=\"1\">one</a><a href=\"2\">two</a>")]
    [InlineData("1 < 2 & 3 > 2", "1 &lt; 2 &amp; 3 &gt; 2")]
    [InlineData("<img src=\"a.png\" alt='x \"y\"'>", "<img src=\"a.png\" alt=\"x &quot;y&quot;\">")]
    [InlineData("<!-- note --><p>p</p>", "<!-- note --><p>p</p>")]
    public void Repairs_fragments_like_a_browser(string html, string expected)
    {
        Assert.Equal(expected, HtmlParser.ParseFragment(html).InnerHtml);
    }

    [Fact]
    public void Raw_text_elements_keep_markup_as_text()
    {
        var fragment = HtmlParser.ParseFragment("<script>if (a < b && c) { x = '</p>'; }</script><textarea>&lt;b&gt;</textarea>");

        var script = fragment.QuerySelector("script")!;
        Assert.Equal("if (a < b && c) { x = '</p>'; }", script.TextContent);
        Assert.Equal("<b>", fragment.QuerySelector("textarea")!.TextContent);
    }

    [Fact]
    public void Deep_nesting_is_flattened_beyond_the_limit()
    {
        var html = string.Concat(Enumerable.Repeat("<div>", 20_000)) + "deep";

        var fragment = HtmlParser.ParseFragment(html);

        Assert.Equal("deep", fragment.TextContent);
        Assert.Contains("deep", fragment.InnerHtml);
    }

    [Fact]
    public void Dom_can_be_edited_and_serialised()
    {
        var fragment = HtmlParser.ParseFragment("<p>a</p>");
        var p = fragment.QuerySelector("p")!;

        var span = p.AppendChild(new HtmlElement("SPAN"));
        span.SetAttribute("class", "x y");
        span.AppendChild(new HtmlText("<b>"));
        p.InsertBefore(new HtmlComment("c"), p.FirstChild);

        Assert.Equal("<p><!--c-->a<span class=\"x y\">&lt;b&gt;</span></p>", fragment.InnerHtml);
        Assert.Equal(["x", "y"], span.ClassList);
        Assert.Same(p, span.ParentElement);
        span.Remove();
        Assert.Equal("<p><!--c-->a</p>", fragment.InnerHtml);
    }

    [Theory]
    [InlineData("a[href*='/en/members/']", 2)]
    [InlineData("a[href^=https]", 1)]
    [InlineData("a[href$='.pdf' i]", 1)]
    [InlineData("div.box > a", 2)]
    [InlineData("#main a", 3)]
    [InlineData("li:first-child", 1)]
    [InlineData("li:last-child a", 1)]
    [InlineData("li:nth-child(2)", 1)]
    [InlineData("li + li", 2)]
    [InlineData("a:not([href^=https])", 2)]
    [InlineData("p, li", 4)]
    [InlineData("*", 9)]
    [InlineData("[data-x]", 1)]
    public void Selects_elements(string selector, int count)
    {
        var fragment = HtmlParser.ParseFragment(
            "<div id=main class='box wide'><a href='/en/members/1'>A</a><a href='/en/members/2.PDF' data-x>B</a>"
            + "<ul><li>1</li><li>2</li><li><a href='https://x'>3</a></li></ul><p>p</p></div>");

        Assert.Equal(count, fragment.QuerySelectorAll(selector).Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a[href")]
    [InlineData(":hover")]
    [InlineData("a >")]
    public void Rejects_malformed_selectors(string selector)
    {
        Assert.Throws<FormatException>(() => HtmlSelector.Parse(selector));
    }
}
