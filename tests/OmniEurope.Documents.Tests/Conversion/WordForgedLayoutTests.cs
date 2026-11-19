// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Tests.Word;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// Documents that declare sizes no real document has (a page of no size or of kilometres, a tab stop far past the
/// page, a cell spanning a hundred million columns, a word of tens of thousands of letters) convert in a time that
/// follows their real content, and say what was brought back to a limit.
/// </summary>
public sealed class WordForgedLayoutTests
{
    [Theory]
    [InlineData(0, 0, 3, 3)]
    [InlineData(999999999, 999999999, 14400, 14400)]
    [InlineData(11906, 999999999, 595.3, 14400)]
    public void A_page_size_outside_what_a_pdf_page_allows_is_brought_back_and_reported(int width, int height, double pdfWidth, double pdfHeight)
    {
        var document = WordDocument.Load(DocxFactory.Build($"""<w:p><w:r><w:t>x</w:t></w:r></w:p><w:sectPr><w:pgSz w:w="{width}" w:h="{height}"/><w:pgMar w:top="0" w:right="0" w:bottom="0" w:left="0" w:header="0" w:footer="0" w:gutter="0"/></w:sectPr>"""));

        var result = WordToPdf.Convert(document, null, TestContext.Current.CancellationToken);

        var page = PdfDocument.Open(result.Pdf).GetPage(1);
        Assert.Equal(pdfWidth, page.Width, 1);
        Assert.Equal(pdfHeight, page.Height, 1);
        var gap = Assert.Single(result.GapDetails, g => g.Code == "PdfPageSizeLimited");
        Assert.Equal("page size outside the 3 to 14400 points a PDF page allows brought back to that range", gap.Message);
        Assert.Equal("taille de page hors des 3 à 14400 points permis à une page PDF ramenée dans cet intervalle", gap.Describe(CultureInfo.GetCultureInfo("fr-FR")));
    }

    [Fact]
    public void An_ordinary_page_size_reports_nothing()
    {
        var result = WordToPdf.Convert(new WordDocument { Body = { new WordParagraph("x") } }, null, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(result.GapDetails, g => g.Code == "PdfPageSizeLimited");
    }

    [Fact]
    public void A_dot_leader_to_a_tab_stop_far_past_the_page_draws_only_the_dots_a_page_can_show()
    {
        // The stop is 100 000 points to the right of an A4 page: a leader over all of it would be tens of thousands of dots.
        var document = WordDocument.Load(DocxFactory.Build("""<w:p><w:pPr><w:tabs><w:tab w:val="right" w:leader="dot" w:pos="2000000"/></w:tabs></w:pPr><w:r><w:t>a</w:t><w:tab/><w:t>b</w:t></w:r></w:p>"""));

        var page = PdfDocument.Open(WordToPdf.Convert(document, null, TestContext.Current.CancellationToken).Pdf).GetPage(1);

        var dots = page.Letters.Count(l => l.Value == ".");
        Assert.InRange(dots, 1, 400);
        Assert.Contains(page.Letters, l => l.Value == "a");
    }

    [Fact]
    public void A_dot_leader_inside_the_page_still_ends_at_its_stop()
    {
        var document = WordDocument.Load(DocxFactory.Build("""<w:p><w:pPr><w:tabs><w:tab w:val="right" w:leader="dot" w:pos="6000"/></w:tabs></w:pPr><w:r><w:t>a</w:t><w:tab/><w:t>b</w:t></w:r></w:p>"""));

        var page = PdfDocument.Open(WordToPdf.Convert(document, null, TestContext.Current.CancellationToken).Pdf).GetPage(1);

        var dots = page.Letters.Where(l => l.Value == ".").ToList();
        var last = page.Letters.Single(l => l.Value == "b");
        Assert.True(dots.Count > 50, $"{dots.Count} dots");
        Assert.All(dots, dot => Assert.True(dot.X < last.X, $"a dot at {dot.X} is past the text at {last.X}"));
        Assert.True(last.X - dots.Max(d => d.X) < 12, $"the leader stops {last.X - dots.Max(d => d.X)} before the text");
    }

    [Fact]
    public void A_cell_span_and_a_row_offset_of_a_hundred_million_columns_are_read_as_a_thousand()
    {
        var bytes = DocxFactory.Build("""<w:tbl><w:tr><w:trPr><w:gridBefore w:val="100000000"/><w:gridAfter w:val="100000000"/></w:trPr><w:tc><w:tcPr><w:gridSpan w:val="100000000"/></w:tcPr><w:p><w:r><w:t>cell</w:t></w:r></w:p></w:tc></w:tr></w:tbl><w:p/>""");

        var document = WordDocument.Load(bytes);

        var row = Assert.Single(document.Blocks.OfType<WordTable>()).Rows[0];
        Assert.Equal((1000, 1000, 1000), (row.Properties.GridBefore, row.Properties.GridAfter, row.Cells[0].Properties.GridSpan));
        Assert.Contains("cell", WordToMarkdown.Convert(document).Markdown, StringComparison.Ordinal);
        Assert.Contains("cell", PdfDocument.Open(WordToPdf.Convert(document, null, TestContext.Current.CancellationToken).Pdf).GetPage(1).Text, StringComparison.Ordinal);
        Assert.Equal(1000, WordDocument.Load(document.ToArray()).Blocks.OfType<WordTable>().Single().Rows[0].Cells[0].Properties.GridSpan);
    }

    [Fact]
    public void A_word_of_tens_of_thousands_of_letters_breaks_across_lines_in_a_time_that_follows_its_length()
    {
        const int Letters = 40000;
        var document = new WordDocument { Body = { new WordParagraph(new string('x', Letters)) } };

        // Processor time of this thread, so the limit measures the conversion and not the parallel suite around it.
        var (pdf, time) = ThreadCpuTime.Measure(() => PdfDocument.Open(WordToPdf.Convert(document, null, TestContext.Current.CancellationToken).Pdf));

        Assert.Equal(Letters, Enumerable.Range(1, pdf.PageCount).Sum(n => pdf.GetPage(n).Letters.Count(l => l.Value == "x")));
        Assert.True(time < TimeSpan.FromSeconds(10), $"took {time}");
    }
}
