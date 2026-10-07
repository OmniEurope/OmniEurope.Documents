// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Markdown;

namespace OmniEurope.Documents.Tests.Markdown;

/// <summary>CommonMark behaviour, one case per construct. Inputs and outputs written for this repository.</summary>
public sealed class CommonMarkTests
{
    public static TheoryData<string, string> Blocks => new()
    {
        { "Hello world", "<p>Hello world</p>\n" },
        { "a\nb", "<p>a\nb</p>\n" },
        { "a\n\nb", "<p>a</p>\n<p>b</p>\n" },
        { "# One\n## Two ##\n###### Six", "<h1>One</h1>\n<h2>Two</h2>\n<h6>Six</h6>\n" },
        { "####### Seven", "<p>####### Seven</p>\n" },
        { "#nospace", "<p>#nospace</p>\n" },
        { "# Title #not", "<h1>Title #not</h1>\n" },
        { "Title\n=====", "<h1>Title</h1>\n" },
        { "Sub\n---", "<h2>Sub</h2>\n" },
        { "***\n---\n___", "<hr />\n<hr />\n<hr />\n" },
        { " - - -", "<hr />\n" },
        { "    code\n    more", "<pre><code>code\nmore\n</code></pre>\n" },
        { "```cs\nvar x = 1 < 2;\n```", "<pre><code class=\"language-cs\">var x = 1 &lt; 2;\n</code></pre>\n" },
        { "~~~\nraw\n~~~", "<pre><code>raw\n</code></pre>\n" },
        { "````\n```\n````", "<pre><code>```\n</code></pre>\n" },
        { "```\nunclosed", "<pre><code>unclosed\n</code></pre>\n" },
        { "  ```\n  a\n    b\n  ```", "<pre><code>a\n  b\n</code></pre>\n" },
        { "> quote\ncontinued", "<blockquote>\n<p>quote\ncontinued</p>\n</blockquote>\n" },
        { "> a\n>\n> b", "<blockquote>\n<p>a</p>\n<p>b</p>\n</blockquote>\n" },
        { "> # h\n> - x", "<blockquote>\n<h1>h</h1>\n<ul>\n<li>x</li>\n</ul>\n</blockquote>\n" },
        { "- a\n- b", "<ul>\n<li>a</li>\n<li>b</li>\n</ul>\n" },
        { "- a\n\n- b", "<ul>\n<li>\n<p>a</p>\n</li>\n<li>\n<p>b</p>\n</li>\n</ul>\n" },
        { "1. one\n2. two", "<ol>\n<li>one</li>\n<li>two</li>\n</ol>\n" },
        { "3) three", "<ol start=\"3\">\n<li>three</li>\n</ol>\n" },
        { "- a\n  - b\n    - c", "<ul>\n<li>a\n<ul>\n<li>b\n<ul>\n<li>c</li>\n</ul>\n</li>\n</ul>\n</li>\n</ul>\n" },
        { "- a\n+ b", "<ul>\n<li>a</li>\n</ul>\n<ul>\n<li>b</li>\n</ul>\n" },
        { "- item\n\n  second para", "<ul>\n<li>\n<p>item</p>\n<p>second para</p>\n</li>\n</ul>\n" },
        { "-\n  foo", "<ul>\n<li>foo</li>\n</ul>\n" },
        { "para\n2. not a list", "<p>para\n2. not a list</p>\n" },
        { "para\n1. a list", "<p>para</p>\n<ol>\n<li>a list</li>\n</ol>\n" },
        { "-     code in item", "<ul>\n<li>\n<pre><code>code in item\n</code></pre>\n</li>\n</ul>\n" },
        { "-    one", "<ul>\n<li>one</li>\n</ul>\n" },
        { "- a\n- ```\n  b\n  ```", "<ul>\n<li>a</li>\n<li>\n<pre><code>b\n</code></pre>\n</li>\n</ul>\n" },
        { "[foo]: /url \"title\"\n\n[foo]", "<p><a href=\"/url\" title=\"title\">foo</a></p>\n" },
        { "[FOO]: /u\n[Bar]: /v\n\n[foo] [bar][]", "<p><a href=\"/u\">foo</a> <a href=\"/v\">bar</a></p>\n" },
        { "[a]: <my url>\n\n[a]", "<p><a href=\"my%20url\">a</a></p>\n" },
        { "[only]: /x", "" },
        { "\tindented", "<pre><code>indented\n</code></pre>\n" },
        { "- a\n\tb", "<ul>\n<li>a\nb</li>\n</ul>\n" },
        { "> quote\nlazy line", "<blockquote>\n<p>quote\nlazy line</p>\n</blockquote>\n" },
        { "> quote\n\nout", "<blockquote>\n<p>quote</p>\n</blockquote>\n<p>out</p>\n" },
        { "- one\n\n  two\n- three", "<ul>\n<li>\n<p>one</p>\n<p>two</p>\n</li>\n<li>\n<p>three</p>\n</li>\n</ul>\n" },
        { "1. a\n\n   b\n\n2. c", "<ol>\n<li>\n<p>a</p>\n<p>b</p>\n</li>\n<li>\n<p>c</p>\n</li>\n</ol>\n" },
        { "[t]: /u\n'title'\n\n[t]", "<p><a href=\"/u\" title=\"title\">t</a></p>\n" },
        { "[t]: /u 'title' junk\n\n[t]", "<p>[t]: /u 'title' junk</p>\n<p>[t]</p>\n" },
        { "* a\n*\n\n* c", "<ul>\n<li>\n<p>a</p>\n</li>\n<li></li>\n<li>\n<p>c</p>\n</li>\n</ul>\n" },
        { "Foo\nbar\n---", "<h2>Foo\nbar</h2>\n" },
        { "- foo\n---", "<ul>\n<li>foo</li>\n</ul>\n<hr />\n" },
    };

    public static TheoryData<string, string> Inlines => new()
    {
        { "*em* and **strong**", "<p><em>em</em> and <strong>strong</strong></p>\n" },
        { "_em_ __strong__", "<p><em>em</em> <strong>strong</strong></p>\n" },
        { "***both***", "<p><em><strong>both</strong></em></p>\n" },
        { "snake_case_name", "<p>snake_case_name</p>\n" },
        { "a * b * c", "<p>a * b * c</p>\n" },
        { "*foo**bar**baz*", "<p><em>foo<strong>bar</strong>baz</em></p>\n" },
        { "**foo*", "<p>*<em>foo</em></p>\n" },
        { "*(*foo*)*", "<p><em>(<em>foo</em>)</em></p>\n" },
        { "foo*bar*", "<p>foo<em>bar</em></p>\n" },
        { "`code <b>`", "<p><code>code &lt;b&gt;</code></p>\n" },
        { "`` a ` b ``", "<p><code>a ` b</code></p>\n" },
        { "` `` `", "<p><code>``</code></p>\n" },
        { "`unclosed", "<p>`unclosed</p>\n" },
        { "\\*not em\\*", "<p>*not em*</p>\n" },
        { "\\a", "<p>\\a</p>\n" },
        { "&amp; &copy; &#233; &#x1F600; &nope;", "<p>&amp; © é \uD83D\uDE00 &amp;nope;</p>\n" },
        { "line  \nbreak", "<p>line<br />\nbreak</p>\n" },
        { "line\\\nbreak", "<p>line<br />\nbreak</p>\n" },
        { "soft   \n   wrap", "<p>soft<br />\nwrap</p>\n" },
        { "[link](/url \"t\")", "<p><a href=\"/url\" title=\"t\">link</a></p>\n" },
        { "[link](</a b>)", "<p><a href=\"/a%20b\">link</a></p>\n" },
        { "[link]()", "<p><a href=\"\">link</a></p>\n" },
        { "[a](foo(bar))", "<p><a href=\"foo(bar)\">a</a></p>\n" },
        { "[*em* link](/u)", "<p><a href=\"/u\"><em>em</em> link</a></p>\n" },
        { "[outer [inner](/i)](/o)", "<p>[outer <a href=\"/i\">inner</a>](/o)</p>\n" },
        { "![alt *text*](/img.png \"T\")", "<p><img src=\"/img.png\" alt=\"alt text\" title=\"T\" /></p>\n" },
        { "[not a link]", "<p>[not a link]</p>\n" },
        { "<https://example.com/a?b=1&c>", "<p><a href=\"https://example.com/a?b=1&amp;c\">https://example.com/a?b=1&amp;c</a></p>\n" },
        { "<me@example.com>", "<p><a href=\"mailto:me@example.com\">me@example.com</a></p>\n" },
        { "a < b > c", "<p>a &lt; b &gt; c</p>\n" },
        { "[é](/café)", "<p><a href=\"/caf%C3%A9\">é</a></p>\n" },
        { "*foo**bar*", "<p><em>foo**bar</em></p>\n" },
        { "__foo__bar", "<p>__foo__bar</p>\n" },
        { "**foo**bar", "<p><strong>foo</strong>bar</p>\n" },
        { "*foo _bar* baz_", "<p><em>foo _bar</em> baz_</p>\n" },
        { "[![img](/i.png)](/target)", "<p><a href=\"/target\"><img src=\"/i.png\" alt=\"img\" /></a></p>\n" },
        { "[foo][bar]\n\n[bar]: /b", "<p><a href=\"/b\">foo</a></p>\n" },
        { "[foo][nope]\n\n[foo]: /f", "<p>[foo][nope]</p>\n" },
        { "*a `*` b*", "<p><em>a <code>*</code> b</em></p>\n" },
        { "a  ", "<p>a</p>\n" },
    };

    public static TheoryData<string, string> EdgeCases => new()
    {
        { "&#0; &#X22; &#xD800; &#x110000;", "<p>� &quot; � �</p>\n" },
        { "&#12345678; &#x; &#xG;", "<p>&amp;#12345678; &amp;#x; &amp;#xG;</p>\n" },
        { "[a](<b)c>)", "<p><a href=\"b)c\">a</a></p>\n" },
        { "[a](<b\nc>)", "<p>[a](&lt;b\nc&gt;)</p>\n" },
        { "[a](\\(b\\))", "<p><a href=\"(b)\">a</a></p>\n" },
        { "[a](" + new string('(', 33) + "x" + new string(')', 33) + ")", "<p>[a](" + new string('(', 33) + "x" + new string(')', 34) + "</p>\n" },
        { "[a](/u 'single')", "<p><a href=\"/u\" title=\"single\">a</a></p>\n" },
        { "[a](/u (paren))", "<p><a href=\"/u\" title=\"paren\">a</a></p>\n" },
        { "[a](/u \"t\\\"q\")", "<p><a href=\"/u\" title=\"t&quot;q\">a</a></p>\n" },
        { "[a](/u (pa(ren))", "<p>[a](/u (pa(ren))</p>\n" },
        { "[r]: /u\n'title'\n\n[r]", "<p><a href=\"/u\" title=\"title\">r</a></p>\n" },
    };

    public static TheoryData<string, string> RawHtml => new()
    {
        { "a <?php x ?> b", "<p>a <?php x ?> b</p>\n" },
        { "a <![CDATA[ <x> ]]> b", "<p>a <![CDATA[ <x> ]]> b</p>\n" },
        { "a <!DOCTYPE html> b", "<p>a <!DOCTYPE html> b</p>\n" },
        { "a <!--> b <!---> c", "<p>a <!--> b <!---> c</p>\n" },
        { "a <x y=1 z='2' w=\"3\" v/> b </x > c", "<p>a <x y=1 z='2' w=\"3\" v/> b </x > c</p>\n" },
        { "a <? open", "<p>a &lt;? open</p>\n" },
        { "a <!x", "<p>a &lt;!x</p>\n" },
        { "<pre>\n*a*\n</pre>\nafter", "<pre>\n*a*\n</pre>\n<p>after</p>\n" },
        { "<TEXTAREA>\nx\n</textarea>\nafter", "<TEXTAREA>\nx\n</textarea>\n<p>after</p>\n" },
        { "<!-- a\nb -->\nafter", "<!-- a\nb -->\n<p>after</p>\n" },
        { "<?x\ny ?>\nafter", "<?x\ny ?>\n<p>after</p>\n" },
        { "<!DOCTYPE html>\nafter", "<!DOCTYPE html>\n<p>after</p>\n" },
        { "<![CDATA[\nx\n]]>\nafter", "<![CDATA[\nx\n]]>\n<p>after</p>\n" },
        { "<div>\n*x*\n\nafter", "<div>\n*x*\n<p>after</p>\n" },
        { "<custom-tag a=\"1\">\n*x*\n\nafter", "<custom-tag a=\"1\">\n*x*\n<p>after</p>\n" },
    };

    [Theory]
    [MemberData(nameof(EdgeCases))]
    public void Renders_edge_cases(string markdown, string html) => Assert.Equal(html, MarkdownRenderer.ToHtml(markdown));

    [Theory]
    [MemberData(nameof(RawHtml))]
    public void Passes_raw_html_through_when_allowed(string markdown, string html) =>
        Assert.Equal(html, MarkdownRenderer.ToHtml(markdown, new MarkdownOptions { AllowRawHtml = true }));

    [Theory]
    [MemberData(nameof(Blocks))]
    public void Renders_blocks(string markdown, string html) => Assert.Equal(html, MarkdownRenderer.ToHtml(markdown));

    [Theory]
    [MemberData(nameof(Inlines))]
    public void Renders_inlines(string markdown, string html) => Assert.Equal(html, MarkdownRenderer.ToHtml(markdown));
}
