// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Markdown;

namespace OmniEurope.Documents.Tests.Markdown;

public sealed class MarkdownWriterTests
{
    [Theory]
    [InlineData("*not bold* and _not em_")]
    [InlineData("# not a title")]
    [InlineData("- not a list")]
    [InlineData("1. not ordered")]
    [InlineData("42) neither")]
    [InlineData("<script>x</script> & [link](javascript:x) `code` ~~s~~ | pipe \\ back")]
    [InlineData("> not a quote")]
    [InlineData("=== not setext")]
    public void Escaped_text_reads_back_literally(string text)
    {
        var markdown = new MarkdownWriter().Paragraph(text).ToString();

        var document = MarkdownParser.Parse(markdown, MarkdownOptions.GitHub);

        var paragraph = Assert.IsType<MarkdownParagraph>(Assert.Single(document.Children));
        var plain = new StringBuilder();
        foreach (var inline in paragraph.Inlines)
        {
            Assert.IsType<MarkdownText>(inline);
            plain.Append(((MarkdownText)inline).Text);
        }

        Assert.Equal(text, plain.ToString());
    }

    [Fact]
    public void Builds_headings_lists_and_tables()
    {
        var markdown = new MarkdownWriter()
            .Heading(2, "Report *1*")
            .List(["a", "b|c"])
            .List(["first"], ordered: true)
            .Table(["Key", "Value"], [["x", "1"], ["y|z"]], [MarkdownTableAlignment.Left, MarkdownTableAlignment.Right])
            .ToString();

        var html = MarkdownRenderer.ToHtml(markdown, MarkdownOptions.GitHub);

        Assert.Contains("<h2>Report *1*</h2>", html);
        Assert.Contains("<li>b|c</li>", html);
        Assert.Contains("<ol>\n<li>first</li>", html);
        Assert.Contains("<td align=\"left\">y|z</td>\n<td align=\"right\"></td>", html);
    }

    [Fact]
    public void Line_breaks_in_values_do_not_break_the_structure()
    {
        var markdown = new MarkdownWriter().Table(["h"], [["two\nlines"]]).ToString();

        var table = Assert.IsType<MarkdownTable>(Assert.Single(MarkdownParser.Parse(markdown, MarkdownOptions.GitHub).Children));
        Assert.Single(table.Rows);
    }
}
