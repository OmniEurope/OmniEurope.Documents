// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Text;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// A PAGE or NUMPAGES field of a header or footer takes the width of the value it shows on each page, not that of its
/// cached result, so a "n/N" footer never overlaps its own glyphs.
/// </summary>
public sealed class WordPageFieldWidthTests
{
    [Theory]
    [InlineData(WordAlignment.Left)]
    [InlineData(WordAlignment.Center)]
    [InlineData(WordAlignment.Right)]
    public void A_page_of_n_footer_prints_two_digit_page_numbers_without_overlapping_glyphs(WordAlignment alignment)
    {
        var document = new WordDocument();
        for (var k = 1; k <= 27; k++)
        {
            document.Body.Add(new WordParagraph($"Page {k}") { Properties = new WordParagraphProperties { PageBreakBefore = k > 1 } });
        }

        var footer = new WordParagraph { Properties = new WordParagraphProperties { Alignment = alignment } };
        footer.Add(new WordField("PAGE", "1")).Add(new WordText("/")).Add(new WordField("NUMPAGES", "1"));
        document.Sections[0].Footers[WordHeaderFooterKind.Default] = new WordHeaderFooter(footer);

        var pdf = PdfDocument.Open(WordToPdf.Convert(document.ToArray()).Pdf);

        Assert.Equal(27, pdf.PageCount);
        foreach (var number in new[] { 1, 10, 20, 27 })
        {
            var letters = Footer(pdf.GetPage(number));
            Assert.Equal($"{number}/27", string.Concat(letters.Select(l => l.Value)));
            Assert.All(letters.Zip(letters.Skip(1)), pair => Assert.True(pair.Second.X >= pair.First.X + pair.First.Width - 0.1,
                $"page {number}: '{pair.Second.Value}' at {pair.Second.X:F2} overlaps '{pair.First.Value}' ending at {pair.First.X + pair.First.Width:F2}"));
        }
    }

    [Fact]
    public void A_centred_footer_stays_centred_on_the_margins_whatever_the_width_of_its_page_number()
    {
        var document = new WordDocument();
        for (var k = 1; k <= 12; k++)
        {
            document.Body.Add(new WordParagraph($"Page {k}") { Properties = new WordParagraphProperties { PageBreakBefore = k > 1 } });
        }

        var footer = new WordParagraph { Properties = new WordParagraphProperties { Alignment = WordAlignment.Center } };
        footer.Add(new WordField("PAGE", "1"));
        document.Sections[0].Footers[WordHeaderFooterKind.Default] = new WordHeaderFooter(footer);
        var page = document.Sections[0].Page;

        var pdf = PdfDocument.Open(WordToPdf.Convert(document).Pdf);

        var middle = page.MarginLeft + (page.ContentWidth / 2);
        foreach (var number in new[] { 9, 12 })
        {
            var letters = Footer(pdf.GetPage(number));
            Assert.Equal(middle, (letters[0].X + letters[^1].X + letters[^1].Width) / 2, 0.5);
        }
    }

    // The letters of the lowest line of the page, left to right.
    private static List<PdfLetter> Footer(PdfPage page)
    {
        var visible = page.Letters.Where(l => !string.IsNullOrWhiteSpace(l.Value)).ToList();
        var bottom = visible.Min(l => l.Y);
        return visible.Where(l => Math.Abs(l.Y - bottom) < 1).OrderBy(l => l.X).ToList();
    }
}
