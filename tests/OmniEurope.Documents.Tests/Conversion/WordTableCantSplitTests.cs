// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// A row marked cannot-split (<c>w:cantSplit</c>) moves whole to the next page when it fits there; when it is taller than
/// a page body Word ignores the mark and breaks the row across pages instead of drawing it past the bottom margin.
/// </summary>
public sealed class WordTableCantSplitTests
{
    private const double Line = 20;

    [Fact]
    public void A_cannot_split_row_taller_than_a_page_breaks_across_pages_and_keeps_all_its_text_above_the_bottom_margin()
    {
        // 52 lines of 20 are 1040, about 1.5 times the 700.1 an A4 page body holds.
        var document = Document(fillers: 0, paragraphs: 52);

        var pdf = PdfDocument.Open(WordToPdf.Convert(document).Pdf);

        Assert.True(pdf.PageCount >= 2, $"the row stayed on {pdf.PageCount} page(s)");
        var all = string.Join('\n', Enumerable.Range(1, pdf.PageCount).Select(n => pdf.GetPage(n).Text));
        for (var k = 1; k <= 52; k++)
        {
            Assert.Contains($"Para {k:000} ", all);
        }

        var limit = WordPageSetup.A4.Height - WordPageSetup.A4.MarginBottom;
        for (var n = 1; n <= pdf.PageCount; n++)
        {
            var page = pdf.GetPage(n);
            var lowest = page.Letters.Where(l => !string.IsNullOrWhiteSpace(l.Value)).Select(l => page.Height - l.Y).DefaultIfEmpty(0).Max();
            Assert.True(lowest <= limit + 0.5, $"page {n} draws text down to {lowest}, past the bottom margin at {limit}");
        }
    }

    [Fact]
    public void A_cannot_split_row_that_fits_a_page_but_not_the_space_left_moves_whole_to_the_next_page()
    {
        // 33 lines leave 40.1 of the page; the row of 10 lines (200) fits a page, not this space.
        var document = Document(fillers: 33, paragraphs: 10);

        var pdf = PdfDocument.Open(WordToPdf.Convert(document).Pdf);

        Assert.Equal(2, pdf.PageCount);
        Assert.DoesNotContain("Para ", pdf.GetPage(1).Text);
        for (var k = 1; k <= 10; k++)
        {
            Assert.Contains($"Para {k:000} ", pdf.GetPage(2).Text);
        }
    }

    private static WordDocument Document(int fillers, int paragraphs)
    {
        var document = new WordDocument();
        document.Sections[0].Page = WordPageSetup.A4;
        for (var k = 1; k <= fillers; k++)
        {
            document.Body.Add(Paragraph($"Filler {k}"));
        }

        var cell = new WordTableCell();
        for (var k = 1; k <= paragraphs; k++)
        {
            cell.Blocks.Add(Paragraph($"Para {k:000} end"));
        }

        var row = new WordTableRow { Properties = new WordTableRowProperties { CantSplit = true } };
        row.Cells.Add(cell);
        var table = new WordTable(300);
        table.Rows.Add(row);
        document.Body.Add(table);
        document.Body.Add(new WordParagraph("After"));
        return document;
    }

    private static WordParagraph Paragraph(string text) => new(text)
    {
        Properties = new WordParagraphProperties { SpacingBefore = 0, SpacingAfter = 0, LineSpacing = Line, LineSpacingRule = WordLineSpacingRule.Exact },
    };
}
