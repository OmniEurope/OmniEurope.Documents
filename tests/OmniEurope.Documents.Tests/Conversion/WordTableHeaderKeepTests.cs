// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// Word never leaves the header rows of a table (<c>w:tblHeader</c>) alone at the bottom of a page: they move to the next
/// page with the first body row when that row does not fit under them.
/// </summary>
public sealed class WordTableHeaderKeepTests
{
    private const double Line = 20;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_header_row_whose_first_body_row_does_not_fit_under_it_moves_to_the_next_page_with_it(bool keepNext)
    {
        // 33 lines of 20 leave 40.1 of the 700.1 an A4 page holds: the header row (20) fits, header and row 1 (60) do not.
        var document = Document(fillers: 33, keepNext, bodyRows: 3);

        var pdf = PdfDocument.Open(WordToPdf.Convert(document.ToArray()).Pdf);

        Assert.DoesNotContain("Header", pdf.GetPage(1).Text);
        // A paragraph kept with the table (w:keepNext) goes along with its header row.
        Assert.Equal(keepNext ? 2 : 1, pdf.GetPage(1).Text.Contains("Filler 33", StringComparison.Ordinal) ? 1 : 2);
        var second = pdf.GetPage(2).Text;
        Assert.StartsWith(keepNext ? "Filler 33" : "Header", second.TrimStart());
        Assert.Contains("Row 1", second);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(second, "Header"));
    }

    [Fact]
    public void A_header_row_and_its_first_body_row_that_fit_stay_on_the_page()
    {
        // 31 lines leave 80.1: the header row and row 1 (60) fit, row 2 goes on with a repeated header.
        var document = Document(fillers: 31, keepNext: false, bodyRows: 3);

        var pdf = PdfDocument.Open(WordToPdf.Convert(document).Pdf);

        Assert.Contains("Header", pdf.GetPage(1).Text);
        Assert.Contains("Row 1", pdf.GetPage(1).Text);
        Assert.StartsWith("Header", pdf.GetPage(2).Text.TrimStart());
        Assert.Contains("Row 2", pdf.GetPage(2).Text);
    }

    private static WordDocument Document(int fillers, bool keepNext, int bodyRows)
    {
        var document = new WordDocument();
        document.Sections[0].Page = WordPageSetup.A4;
        for (var k = 1; k <= fillers; k++)
        {
            document.Body.Add(new WordParagraph($"Filler {k}")
            {
                Properties = new WordParagraphProperties
                {
                    SpacingBefore = 0, SpacingAfter = 0, LineSpacing = Line, LineSpacingRule = WordLineSpacingRule.Exact,
                    KeepNext = keepNext && k == fillers,
                },
            });
        }

        var table = new WordTable(200);
        table.AddRow("Header").Properties = Row(Line, header: true);
        for (var r = 1; r <= bodyRows; r++)
        {
            table.AddRow($"Row {r}").Properties = Row(2 * Line, header: false);
        }

        document.Body.Add(table);
        document.Body.Add(new WordParagraph("After"));
        return document;
    }

    private static WordTableRowProperties Row(double height, bool header) =>
        new() { Height = height, HeightRule = WordRowHeightRule.Exact, IsHeader = header ? true : null, CantSplit = true };
}
