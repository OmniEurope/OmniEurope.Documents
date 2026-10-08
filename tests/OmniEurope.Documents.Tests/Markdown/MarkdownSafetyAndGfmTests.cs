// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Markdown;

namespace OmniEurope.Documents.Tests.Markdown;

public sealed class MarkdownSafetyAndGfmTests
{
    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<div onclick=\"x()\">hi</div>")]
    [InlineData("text <img src=x onerror=alert(1)> more")]
    [InlineData("<!-- c --><iframe src=//evil>")]
    public void Raw_html_is_escaped_by_default(string markdown)
    {
        var html = MarkdownRenderer.ToHtml(markdown, MarkdownOptions.GitHub);

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<div", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;", html);
    }

    [Theory]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("[x](JaVaScRiPt:alert(1))")]
    [InlineData("[x](java&#x09;script:alert(1))")]
    [InlineData("[x](vbscript:msgbox)")]
    [InlineData("[x](data:text/html;base64,PHNjcmlwdD4=)")]
    [InlineData("<javascript:alert(1)>")]
    public void Unsafe_link_schemes_lose_their_destination(string markdown)
    {
        var html = MarkdownRenderer.ToHtml(markdown);

        Assert.DoesNotContain("href", html);
    }

    [Fact]
    public void Unsafe_image_renders_its_alt_text_only()
    {
        Assert.Equal("<p>pic</p>\n", MarkdownRenderer.ToHtml("![pic](javascript:x)"));
        Assert.Contains("<img src=\"data:image/png;base64,AA==\"", MarkdownRenderer.ToHtml("![p](data:image/png;base64,AA==)"));
    }

    [Fact]
    public void Raw_html_passes_through_when_allowed()
    {
        var options = new MarkdownOptions { AllowRawHtml = true };

        Assert.Equal("<div class=\"x\">\n<p>not md</p>\n</div>\n", MarkdownRenderer.ToHtml("<div class=\"x\">\n<p>not md</p>\n</div>", options));
        Assert.Equal("<p>a <span>b</span> c</p>\n", MarkdownRenderer.ToHtml("a <span>b</span> c", options));
        Assert.Equal("<!-- note -->\n", MarkdownRenderer.ToHtml("<!-- note -->", options));
    }

    [Fact]
    public void Renders_gfm_tables_with_alignment()
    {
        var markdown = "| Name | Qty | Note |\n| :--- | ---: | :-: |\n| apple | 3 | *ok* |\n| pear |\n";

        var html = MarkdownRenderer.ToHtml(markdown, MarkdownOptions.GitHub);

        Assert.Equal(
            "<table>\n<thead>\n<tr>\n<th align=\"left\">Name</th>\n<th align=\"right\">Qty</th>\n<th align=\"center\">Note</th>\n</tr>\n</thead>\n"
            + "<tbody>\n<tr>\n<td align=\"left\">apple</td>\n<td align=\"right\">3</td>\n<td align=\"center\"><em>ok</em></td>\n</tr>\n"
            + "<tr>\n<td align=\"left\">pear</td>\n<td align=\"right\"></td>\n<td align=\"center\"></td>\n</tr>\n</tbody>\n</table>\n",
            html);
    }

    [Fact]
    public void Table_after_paragraph_text_and_escaped_pipe()
    {
        var html = MarkdownRenderer.ToHtml("intro\n| a |\n| - |\n| x \\| y |\n\nafter", MarkdownOptions.GitHub);

        Assert.Equal("<p>intro</p>\n<table>\n<thead>\n<tr>\n<th>a</th>\n</tr>\n</thead>\n<tbody>\n<tr>\n<td>x | y</td>\n</tr>\n</tbody>\n</table>\n<p>after</p>\n", html);
    }

    [Fact]
    public void Tables_are_off_in_plain_commonmark()
    {
        Assert.StartsWith("<p>| a |", MarkdownRenderer.ToHtml("| a |\n| - |"));
    }

    [Fact]
    public void Renders_task_lists_and_strikethrough()
    {
        var html = MarkdownRenderer.ToHtml("- [x] done ~~old~~\n- [ ] todo", MarkdownOptions.GitHub);

        Assert.Equal("<ul>\n<li><input type=\"checkbox\" checked=\"\" disabled=\"\" /> done <del>old</del></li>\n"
            + "<li><input type=\"checkbox\" disabled=\"\" /> todo</li>\n</ul>\n", html);
    }

    [Theory]
    [InlineData("see https://example.com/path.", "<p>see <a href=\"https://example.com/path\">https://example.com/path</a>.</p>\n")]
    [InlineData("www.example.com/a_(b)", "<p><a href=\"http://www.example.com/a_(b)\">www.example.com/a_(b)</a></p>\n")]
    [InlineData("(www.example.com)", "<p>(<a href=\"http://www.example.com\">www.example.com</a>)</p>\n")]
    [InlineData("www.example.com/q?a&hl;", "<p><a href=\"http://www.example.com/q?a\">www.example.com/q?a</a>&amp;hl;</p>\n")]
    [InlineData("www.example.com/x;", "<p><a href=\"http://www.example.com/x;\">www.example.com/x;</a></p>\n")]
    [InlineData("mail jo.doe@example.org now", "<p>mail <a href=\"mailto:jo.doe@example.org\">jo.doe@example.org</a> now</p>\n")]
    [InlineData("`https://code.example`", "<p><code>https://code.example</code></p>\n")]
    public void Links_bare_urls_and_emails(string markdown, string html)
    {
        Assert.Equal(html, MarkdownRenderer.ToHtml(markdown, MarkdownOptions.GitHub));
    }

    [Fact]
    public void Exposes_a_syntax_tree()
    {
        var document = MarkdownParser.Parse("# Title\n\nBody with **bold**.");

        var heading = Assert.IsType<MarkdownHeading>(document.Children[0]);
        Assert.Equal(1, heading.Level);
        Assert.Equal(1, heading.Line);
        var paragraph = Assert.IsType<MarkdownParagraph>(document.Children[1]);
        Assert.Equal(3, paragraph.Line);
        Assert.Contains(paragraph.Inlines, i => i is MarkdownEmphasis { IsStrong: true });
    }

    [Fact]
    public void Deeply_nested_input_does_not_overflow()
    {
        var markdown = new string('>', 2000) + " deep\n" + new string('[', 5000) + new string('*', 5000);

        var html = MarkdownRenderer.ToHtml(markdown);

        Assert.Contains("deep", html);
    }

    [Fact]
    public void Block_quotes_nest_no_deeper_than_the_limit()
    {
        // A hundred thousand markers: past the limit they are text, so neither parsing nor rendering recurses
        // that deep.
        var html = MarkdownRenderer.ToHtml(new string('>', 100_000) + " deep");

        Assert.Equal(128, html.Split("<blockquote>").Length - 1);
        Assert.Contains(new string('>', 10) + " deep", System.Net.WebUtility.HtmlDecode(html), StringComparison.Ordinal);
    }
}
