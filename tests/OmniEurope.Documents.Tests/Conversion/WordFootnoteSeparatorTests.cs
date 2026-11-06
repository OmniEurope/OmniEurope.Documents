// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// The rule above the footnotes is the document's own separator (the <c>w:separator</c> footnote of
/// <c>footnotes.xml</c>) when it is not Word's default one, measured on the page drawn by the library's renderer.
/// </summary>
public sealed class WordFootnoteSeparatorTests
{
    private const double Dpi = 288;
    private const double PixelsPerPoint = Dpi / 72;

    [Fact]
    public void A_separator_holding_word_s_separator_mark_keeps_the_default_rule_of_144_points()
    {
        var loaded = WordDocument.Load(WithFootnote().ToArray());

        Assert.Null(loaded.FootnoteSeparator);
        Assert.Equal(144, LongestRule(WordToPdf.Convert(loaded).Pdf), 1);
    }

    [Fact]
    public void A_separator_paragraph_with_a_top_border_and_a_right_indent_draws_a_rule_of_its_own_width()
    {
        var document = WithFootnote();
        var page = document.Sections[0].Page;
        document.FootnoteSeparator = [Rule(page.ContentWidth - 52)];

        var loaded = WordDocument.Load(document.ToArray());

        var separator = Assert.IsType<WordParagraph>(Assert.Single(loaded.FootnoteSeparator!));
        Assert.Equal(page.ContentWidth - 52, separator.Properties.IndentRight!.Value, 1);
        Assert.Equal(52, LongestRule(WordToPdf.Convert(loaded).Pdf), 1);
    }

    [Fact]
    public void A_taller_separator_takes_its_height_from_the_body_of_the_page()
    {
        var plain = WithFootnote(lines: 80);
        var tall = WithFootnote(lines: 80);
        tall.FootnoteSeparator = [new WordParagraph { Properties = Spacing(60) }, Rule(0)];

        var plainLines = BodyLines(WordToPdf.Convert(plain).Pdf);
        var tallLines = BodyLines(WordToPdf.Convert(tall).Pdf);

        Assert.True(tallLines < plainLines, $"{tallLines} lines under a 60 pt taller separator, {plainLines} under the default rule");
    }

    private static WordDocument WithFootnote(int lines = 1)
    {
        var document = new WordDocument();
        document.AddParagraph("Texte").Add(document.AddFootnote("note"));
        for (var k = 1; k < lines; k++)
        {
            document.AddParagraph($"Ligne {k}");
        }

        return document;
    }

    private static int BodyLines(byte[] pdf) =>
        PdfDocument.Open(pdf).GetPage(1).Letters.Where(l => l.Value == "L").Select(l => Math.Round(l.Y)).Distinct().Count();

    private static WordParagraph Rule(double rightIndent) => new()
    {
        Properties = Spacing(0) with { IndentRight = rightIndent, Borders = new WordParagraphBorders(Top: new WordBorder("single", 0.5)) },
    };

    private static WordParagraphProperties Spacing(double after) =>
        new() { SpacingBefore = 0, SpacingAfter = after, LineSpacing = 1, LineSpacingRule = WordLineSpacingRule.Multiple };

    // The longest horizontal run of dark pixels on the first page, in points.
    private static double LongestRule(byte[] pdf)
    {
        var image = PdfRenderer.Render(PdfDocument.Open(pdf).GetPage(1), new PdfRenderOptions { Dpi = Dpi }).Image;
        var longest = 0;
        for (var y = 0; y < image.Height; y++)
        {
            var run = 0;
            for (var x = 0; x < image.Width; x++)
            {
                run = image.Pixels[(y * image.Stride) + (x * image.Components)] < 128 ? run + 1 : 0;
                longest = Math.Max(longest, run);
            }
        }

        return longest / PixelsPerPoint;
    }
}
