// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// The vertical alignment of a section (<c>w:sectPr/w:vAlign</c>) places the body of its pages between the top and
/// bottom margins, measured on the baselines of the converted PDF.
/// </summary>
public sealed class WordPageAlignmentTests
{
    private static readonly WordPageSetup Page = WordPageSetup.A4;

    [Fact]
    public void A_justified_section_ends_the_last_line_of_its_full_page_on_the_bottom_margin_and_leaves_its_last_page_at_the_top()
    {
        var top = Baselines(WordToPdf.Convert(Lines(70, WordPageVerticalAlignment.Top)).Pdf);
        var both = Baselines(WordToPdf.Convert(WordDocument.Load(Lines(70, WordPageVerticalAlignment.Both).ToArray())).Pdf);

        Assert.Equal(2, both.Count);
        var (pitch, ascent) = (top[0][1] - top[0][0], top[0][0] - Page.MarginTop);
        Assert.Equal(top[0][0], both[0][0], 0.5);
        Assert.Equal(Page.Height - Page.MarginBottom - (pitch - ascent), both[0][^1], 0.5);
        var share = (both[0][^1] - top[0][^1]) / (top[0].Count - 1);
        Assert.All(both[0].Zip(both[0].Skip(1)), pair => Assert.Equal(pitch + share, pair.Second - pair.First, 0.05));
        Assert.Equal(top[1], both[1]);
    }

    [Fact]
    public void A_justified_page_shares_its_free_space_between_paragraphs_not_between_the_lines_of_one()
    {
        var document = Lines(60, WordPageVerticalAlignment.Both);
        document.Body.Insert(1, new WordParagraph(string.Join(' ', Enumerable.Repeat("long", 60))) { Properties = Tight });

        var page = Baselines(WordToPdf.Convert(document).Pdf)[0];

        var steps = page.Zip(page.Skip(1), (a, b) => Math.Round(b - a, 2)).ToList();
        Assert.Equal(steps[1], steps[2], 0.05);
        Assert.True(steps[0] > steps[1], "space is added above the long paragraph, not inside it");
    }

    [Theory]
    [InlineData(WordPageVerticalAlignment.Center, 0.5)]
    [InlineData(WordPageVerticalAlignment.Bottom, 1.0)]
    public void A_centred_or_bottom_aligned_page_moves_its_body_down_by_its_free_space(WordPageVerticalAlignment alignment, double share)
    {
        var top = Baselines(WordToPdf.Convert(Lines(3, WordPageVerticalAlignment.Top)).Pdf)[0];
        var moved = Baselines(WordToPdf.Convert(Lines(3, alignment)).Pdf)[0];

        var (pitch, ascent) = (top[1] - top[0], top[0] - Page.MarginTop);
        var free = Page.Height - Page.MarginBottom - (top[^1] + pitch - ascent);
        Assert.All(top.Zip(moved), pair => Assert.Equal(pair.First + (free * share), pair.Second, 0.5));
    }

    [Fact]
    public void A_page_with_a_floating_shape_stays_at_the_top_and_is_reported()
    {
        var png = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "rgb.png"));
        var document = Lines(3, WordPageVerticalAlignment.Center);
        ((WordParagraph)document.Body[0]).Add(new WordPicture(WordImage.FromBytes(png), 20, 20) { Floating = new WordFloatingPosition(300, "page", 300, "page", WordWrap.None) });

        var result = WordToPdf.Convert(document);

        Assert.Contains(result.GapDetails, g => g.Code == "PdfVerticalAlignmentNotApplied");
        Assert.Equal(Page.MarginTop, Baselines(result.Pdf)[0][0], 20.0);
    }

    private static readonly WordParagraphProperties Tight =
        new() { SpacingBefore = 0, SpacingAfter = 0, LineSpacing = 1, LineSpacingRule = WordLineSpacingRule.Multiple };

    private static WordDocument Lines(int count, WordPageVerticalAlignment alignment)
    {
        var document = new WordDocument();
        document.Sections[0].Page = Page with { VerticalAlignment = alignment };
        for (var k = 0; k < count; k++)
        {
            document.Body.Add(new WordParagraph($"Ligne {k}") { Properties = Tight });
        }

        return document;
    }

    // The baselines of each page, measured down from the top of the page, top to bottom.
    private static List<List<double>> Baselines(byte[] pdf)
    {
        var document = PdfDocument.Open(pdf);
        return Enumerable.Range(1, document.PageCount).Select(n =>
        {
            var page = document.GetPage(n);
            return page.Letters.Where(l => !string.IsNullOrWhiteSpace(l.Value)).Select(l => Math.Round(page.Height - l.Y, 2)).Distinct().Order().ToList();
        }).ToList();
    }
}
