// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>HTML to Word keeps CSS keywords of any case, table cell character styles and preformatted line breaks.</summary>
public sealed class HtmlToWordFormattingTests
{
    private static WordDocument RoundTrip(string html) => WordDocument.Load(HtmlToWord.Convert(html).ToArray());

    private static IEnumerable<WordText> Texts(IEnumerable<WordBlock> blocks) =>
        blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines.OfType<WordText>());

    [Theory]
    [InlineData("bold", "italic")]
    [InlineData("BOLD", "ITALIC")]
    [InlineData("Bold", "Oblique")]
    public void Weight_and_style_keywords_are_case_insensitive(string weight, string style)
    {
        var document = RoundTrip($"<p><span style=\"font-weight:{weight};font-style:{style};font-family:Arial\">t</span></p>");

        var text = Texts(document.Body).Single(t => t.Value == "t");
        Assert.True(text.Properties.Bold);
        Assert.True(text.Properties.Italic);
        Assert.Equal("Arial", text.Properties.Font);
    }

    [Fact]
    public void Cell_character_styles_apply_to_the_cell_content_and_do_not_leak()
    {
        var document = RoundTrip("<table><tr><td style=\"color:#ff0000;font-weight:bold;font-size:20pt\">a</td><td>b</td></tr></table>"
            + "<p><span style=\"color:#ff0000;font-weight:bold;font-size:20pt\">c</span></p>");

        var cells = document.Body.OfType<WordTable>().Single().Rows[0].Cells;
        var a = Texts(cells[0].Blocks).Single().Properties;
        var b = Texts(cells[1].Blocks).Single().Properties;
        var span = Texts(document.Body).Single(t => t.Value == "c").Properties;

        Assert.Equal(span.Color, a.Color);
        Assert.Equal("FF0000", a.Color, ignoreCase: true);
        Assert.True(a.Bold);
        Assert.Equal(20, a.FontSize);
        Assert.NotEqual("FF0000", b.Color?.ToUpperInvariant());
        Assert.NotEqual(true, b.Bold);
        Assert.NotEqual(20, b.FontSize);
    }

    [Fact]
    public void Header_cells_stay_bold_unless_their_own_style_says_otherwise()
    {
        var document = RoundTrip("<table><tr><th>h</th><th style=\"font-weight:normal\">n</th></tr></table>");

        var cells = document.Body.OfType<WordTable>().Single().Rows[0].Cells;
        Assert.True(Texts(cells[0].Blocks).Single().Properties.Bold);
        Assert.NotEqual(true, Texts(cells[1].Blocks).Single().Properties.Bold);
    }

    [Theory]
    [InlineData("<pre>a\n<b>b</b>\nc</pre>", "a\nb\nc")]
    [InlineData("<pre>a\n<b>b</b>\n<i>c</i>\nd</pre>", "a\nb\nc\nd")]
    [InlineData("<pre>a\nb\n</pre>", "a\nb")]
    public void Preformatted_line_breaks_survive_inline_elements(string html, string expected)
    {
        var document = RoundTrip(html);

        var paragraph = document.Body.OfType<WordParagraph>().Single();
        var flat = string.Concat(paragraph.Inlines.Select(i => i switch { WordText t => t.Value, WordBreak => "\n", _ => string.Empty }));
        Assert.Equal(expected, flat);
        if (html.Contains("<b>", StringComparison.Ordinal))
        {
            Assert.True(paragraph.Inlines.OfType<WordText>().Single(t => t.Value == "b").Properties.Bold);
        }
    }
}
