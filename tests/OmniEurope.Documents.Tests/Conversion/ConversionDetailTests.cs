// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Tests.Imaging;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// HTML to Word details (orphan list items, embedded images, trailing spaces, skipped elements, table groups
/// and spans, text alignment), Excel cell alignment, merges and borders, multi-page TIFF and right-to-left text.
/// </summary>
public sealed class ConversionDetailTests
{
    // A 1 x 1 PNG, base 64.
    private const string Pixel = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAAAAAA6fptVAAAACklEQVR4nGNgAAAAAgABSK+kcQAAAABJRU5ErkJggg==";

    [Fact]
    public void A_list_item_outside_a_list_starts_a_bullet_list()
    {
        var document = HtmlToWord.Convert("<li>seul</li>");

        var paragraph = Assert.Single(document.Body.OfType<WordParagraph>());
        Assert.NotNull(paragraph.Properties.NumberingId);
        Assert.Equal("seul", paragraph.Text);
    }

    [Fact]
    public void Embedded_images_are_sized_from_their_attributes_and_kept_within_the_page()
    {
        var document = HtmlToWord.Convert(
            $"<p><img src=\"data:image/png;base64,{Pixel}\" width=\"2000\" height=\"100\"><img src=\"data:image/png;base64,AAAA\"></p>");

        var picture = Assert.Single(document.Body.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordPicture>());
        var limit = document.Sections[^1].Page.ContentWidth;

        // 2000 x 100 CSS pixels (1500 x 75 points) scaled down to the text width.
        Assert.Equal(Math.Round(limit, 2), Math.Round(picture.Width, 2));
        Assert.Equal(Math.Round(75 * limit / 1500, 2), Math.Round(picture.Height, 2));
        Assert.Contains("unrecognised embedded image skipped", document.Gaps);
    }

    [Fact]
    public void Trailing_spaces_and_empty_paragraphs_are_dropped_and_scripts_skipped()
    {
        var document = HtmlToWord.Convert("<script>alert(1)</script><p>texte <b> </b></p><p>   </p>");

        var paragraph = Assert.Single(document.Body.OfType<WordParagraph>());
        Assert.Equal("texte", paragraph.Text);
    }

    [Theory]
    [InlineData("right", WordAlignment.Right)]
    [InlineData("end", WordAlignment.Right)]
    [InlineData("justify", WordAlignment.Justify)]
    [InlineData("left", WordAlignment.Left)]
    [InlineData("start", WordAlignment.Left)]
    public void Text_alignment_follows_the_style(string value, WordAlignment alignment)
    {
        var paragraph = Assert.Single(HtmlToWord.Convert($"<p style=\"text-align: {value}\">x</p>").Body.OfType<WordParagraph>());

        Assert.Equal(alignment, paragraph.Properties.Alignment);
    }

    [Fact]
    public void Tables_read_rows_outside_groups_and_footers_last_and_skip_spanned_columns()
    {
        // A 2 x 2 cell at the top left, then rows directly in the table, then a footer written first.
        var document = HtmlToWord.Convert(
            "<table><tfoot><tr><td>pied</td><td>p2</td><td>p3</td></tr></tfoot>"
            + "<tr><td colspan=\"2\" rowspan=\"2\">bloc</td><td>c1</td></tr><tr><td>c2</td></tr></table><table></table>");

        var table = Assert.Single(document.Body.OfType<WordTable>());
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal("c2", table.Rows[1].Cells[^1].Text);
        Assert.Equal(WordVerticalMerge.Continue, table.Rows[1].Cells[0].Properties.VerticalMerge);
        Assert.Equal("pied", table.Rows[2].Cells[0].Text);
    }

    [Fact]
    public void Excel_alignments_merges_and_borders_carry_over()
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("Feuille");
        sheet.Cell("A1").Value = "haut";
        sheet.Cell("A1").Style = XlsxStyle.Default with { VerticalAlignment = XlsxVerticalAlignment.Top, HorizontalAlignment = XlsxHorizontalAlignment.Left, Border = true };
        sheet.Cell("B1").Value = "milieu";
        sheet.Cell("B1").Style = XlsxStyle.Default with { VerticalAlignment = XlsxVerticalAlignment.Center, HorizontalAlignment = XlsxHorizontalAlignment.Center, Border = true, BorderColor = "FF0000" };
        sheet.Cell("C1").Value = "droite";
        sheet.Cell("C1").Style = XlsxStyle.Default with { HorizontalAlignment = XlsxHorizontalAlignment.Right };
        sheet.Cell("D1").Value = "justifié";
        sheet.Cell("D1").Style = XlsxStyle.Default with { HorizontalAlignment = XlsxHorizontalAlignment.Justify };
        sheet.Cell("A2").Value = true;
        sheet.Cell("B2").Value = new XlsxError("#N/A");
        sheet.Cell("C2").Value = "fusion";
        sheet.Merge(XlsxRange.Parse("C2:C3"));
        sheet.Cell("A3").Value = "fin";

        var table = ExcelToPdf.ToWord(workbook).Blocks.OfType<WordTable>().First();

        Assert.Equal(
            [WordAlignment.Left, WordAlignment.Center, WordAlignment.Right, WordAlignment.Justify],
            table.Rows[0].Cells.Select(c => ((WordParagraph)c.Blocks[0]).Properties.Alignment!.Value));
        Assert.Equal([WordCellAlignment.Top, WordCellAlignment.Center], table.Rows[0].Cells.Take(2).Select(c => c.Properties.VerticalAlignment!.Value));
        Assert.Equal("FF0000", table.Rows[0].Cells[1].Properties.Borders!.Top!.Color);
        Assert.Equal("000000", table.Rows[0].Cells[0].Properties.Borders!.Top!.Color);
        Assert.Equal([WordAlignment.Center, WordAlignment.Center], table.Rows[1].Cells.Take(2).Select(c => ((WordParagraph)c.Blocks[0]).Properties.Alignment!.Value));
        Assert.Equal(WordVerticalMerge.Restart, table.Rows[1].Cells[2].Properties.VerticalMerge);
        Assert.Equal(WordVerticalMerge.Continue, table.Rows[2].Cells[2].Properties.VerticalMerge);
        Assert.Empty(table.Rows[2].Cells[2].Blocks);
    }

    [Fact]
    public void Each_page_of_a_tiff_becomes_a_pdf_page()
    {
        var tiff = TiffWriter.Write(
            true,
            new TiffWriter.Page(2, 1, [(258, TiffWriter.Short, [8]), (262, TiffWriter.Short, [1])], [10, 20]),
            new TiffWriter.Page(1, 1, [(258, TiffWriter.Short, [8]), (262, TiffWriter.Short, [1])], [30]));

        var pdf = PdfDocument.Open(ImagesToPdf.Convert([tiff]));

        Assert.Equal(2, pdf.PageCount);
        Assert.Equal(new byte[] { 10, 20 }, Assert.Single(pdf.GetPage(1).Images).Decode()!.Pixels);
        Assert.Equal(new byte[] { 30 }, Assert.Single(pdf.GetPage(2).Images).Decode()!.Pixels);

        // One pixel at 96 dpi is 0.75 point: the page keeps the 3-point minimum, the image its own size.
        Assert.Equal((3.0, 3.0), (pdf.GetPage(2).Width, pdf.GetPage(2).Height));
        Assert.Equal(0.75, Assert.Single(pdf.GetPage(2).Images).Bounds.Width, 2);
    }

    [Fact]
    public void Images_larger_than_the_largest_page_are_scaled_down()
    {
        // 20000 x 100 pixels at 96 dpi is 15000 x 75 points; the page may be 14400 points wide at most.
        var wide = Documents.Imaging.PngCodec.Encode(new Documents.Imaging.RasterImage(20000, 100, Documents.Imaging.ImageColorType.Gray));

        var page = PdfDocument.Open(ImagesToPdf.Convert([wide])).GetPage(1);

        Assert.Equal((14400.0, 72.0), (Math.Round(page.Width, 2), Math.Round(page.Height, 2)));
    }

    [Fact]
    public void Right_to_left_paragraphs_are_reported()
    {
        var document = new WordDocument();
        document.Body.Add(new WordParagraph("שלום") { Properties = new WordParagraphProperties { RightToLeft = true } });

        Assert.Contains("right-to-left paragraphs laid out left to right", WordToPdf.Convert(document).Gaps);
    }
}
