// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Layout;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>Text drawn at an angle: vertical column headings, text read downwards, a page turned by 90 degrees.</summary>
public sealed class PdfLayoutDirectionTests
{
    [Fact]
    public void Vertical_column_headings_form_whole_words_and_lines_beside_horizontal_text()
    {
        var document = Document(canvas =>
        {
            canvas.DrawText("Category of vehicle", 72, 200, PdfFont.Sans, 10);
            canvas.DrawText("Second row", 72, 214, PdfFont.Sans, 10);
            canvas.DrawRotatedText("Total amount", 250, 180, 90, PdfFont.Sans, 9);
            canvas.DrawRotatedText("Share", 280, 180, 90, PdfFont.Sans, 9);
        });
        var page = document.GetPage(1);

        var words = PdfLayoutAnalyzer.Words(page).Select(w => w.Text).ToList();
        var layout = Assert.Single(PdfLayoutAnalyzer.Analyze(document));

        Assert.Equal(["Category", "of", "vehicle", "Second", "row", "Total", "amount", "Share"], words);
        Assert.Equal(["Category of vehicle", "Second row", "Total amount", "Share"], PdfLayoutAnalyzer.Lines(page.Letters).Select(l => l.Text));
        Assert.Equal("Category of vehicle\nSecond row\nTotal amount\nShare", page.Text);
        Assert.Equal(["Category of vehicle\nSecond row", "Total amount", "Share"], layout.Blocks.Select(b => b.Text));
    }

    [Fact]
    public void Text_read_downwards_is_read_line_after_line_from_the_right()
    {
        var page = Document(canvas =>
        {
            canvas.DrawRotatedText("first heading line", 312, 100, 270, PdfFont.Sans, 10);
            canvas.DrawRotatedText("second heading line", 300, 100, 270, PdfFont.Sans, 10);
        }).GetPage(1);

        var layout = PdfLayoutAnalyzer.AnalyzePage(page);

        Assert.All(page.Letters, l => Assert.Equal(-90, l.Rotation));
        Assert.Equal("first heading line\nsecond heading line", Assert.Single(layout.Blocks).Text);
        Assert.Equal("first heading line\nsecond heading line", page.Text);
    }

    [Fact]
    public void A_turned_page_is_read_along_its_text_and_only_its_running_head_and_page_number_are_decoration()
    {
        var builder = new PdfDocumentBuilder();
        for (var i = 0; i < 2; i++)
        {
            TurnedPage(builder.AddPage(), numbered: i == 0);
        }

        var layouts = PdfLayoutAnalyzer.Analyze(PdfDocument.Open(builder.ToArray()));

        foreach (var layout in layouts)
        {
            var first = layout.Page.Number == 1;
            Assert.Equal(first ? ["5", "Journal of tests"] : ["Journal of tests"], layout.Blocks.Where(b => b.IsDecoration).Select(b => b.Text).Order(StringComparer.Ordinal));
            var paragraph = Assert.Single(layout.Blocks, b => b.Lines.Count > 1);
            Assert.Equal("First line of the paragraph\nsecond line of the paragraph\nthird line of it", paragraph.Text);
            Samples.InOrder(layout.Text, "ANNEX I", "First line of the paragraph", "third line of it", "Headlamps", "Mirrors");
            Assert.Contains("1", layout.Blocks.Where(b => !b.IsDecoration).Select(b => b.Text));
            Assert.Equal(
                (first ? "Journal of tests\n5\n" : "Journal of tests\n")
                + "ANNEX I\nFirst line of the paragraph\nsecond line of the paragraph\nthird line of it\n1 Headlamps\n3 Mirrors",
                layout.Page.Text);
        }
    }

    // A page whose content is turned by 90 degrees (read from the bottom to the top, its first line along the left
    // edge), under a horizontal running head and over a horizontal page number; the first column of its table
    // stands along the bottom edge.
    private static void TurnedPage(PdfCanvas canvas, bool numbered)
    {
        canvas.DrawText("Journal of tests", 240, 30, PdfFont.Sans, 9);
        if (numbered)
        {
            canvas.DrawText("5", 295, 820, PdfFont.Sans, 9);
        }

        canvas.DrawRotatedText("ANNEX I", 110, 700, 90, PdfFont.Sans, 12);
        canvas.DrawRotatedText("First line of the paragraph", 140, 780, 90, PdfFont.Sans, 10);
        canvas.DrawRotatedText("second line of the paragraph", 152, 780, 90, PdfFont.Sans, 10);
        canvas.DrawRotatedText("third line of it", 164, 780, 90, PdfFont.Sans, 10);
        canvas.DrawRotatedText("1", 200, 790, 90, PdfFont.Sans, 10);
        canvas.DrawRotatedText("Headlamps", 200, 700, 90, PdfFont.Sans, 10);
        canvas.DrawRotatedText("3", 230, 790, 90, PdfFont.Sans, 10);
        canvas.DrawRotatedText("Mirrors", 230, 700, 90, PdfFont.Sans, 10);
    }

    private static PdfDocument Document(Action<PdfCanvas> draw)
    {
        var builder = new PdfDocumentBuilder();
        draw(builder.AddPage());
        return PdfDocument.Open(builder.ToArray());
    }
}
