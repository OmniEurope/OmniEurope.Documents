// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Pdf.Text;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>
/// Header and footer zones that reserve space, table borders per side, and fixed-width blocks and padded boxes
/// that advance the flow. Positions are read back from the written PDF (letters) or from its rendering at 72 dpi.
/// </summary>
public sealed class PdfFlowLayoutTests
{
    private const double PageHeight = 300;
    private const double SquarePage = 200;
    private static readonly PdfColor Black = new(0, 0, 0);
    private static readonly PdfColor Red = PdfColor.Red;

    [Fact]
    public void A_declared_header_height_pushes_the_flow_below_the_header_on_every_page()
    {
        var document = new PdfDocumentBuilder();
        var options = new PdfFlowOptions
        {
            PageWidth = 300,
            PageHeight = PageHeight,
            Margins = (20, 20, 20, 20),
            HeaderHeight = 60,
            Header = (page, number) =>
            {
                for (var line = 0; line < 3; line++)
                {
                    page.DrawText($"HEAD{number}", 20, 20 + page.Metrics(PdfFont.Sans, 12).Ascent + (line * 14), PdfFont.Sans, 12);
                }
            },
        };
        var flow = new PdfFlowLayout(document, options);
        flow.AddParagraph(string.Join(' ', Enumerable.Repeat("Lorem ipsum dolor sit amet.", 140)), PdfFont.Serif, 10);
        flow.Finish();

        var pdf = PdfDocument.Open(document.ToArray());

        Assert.True(pdf.PageCount >= 3);
        for (var number = 1; number <= pdf.PageCount; number++)
        {
            var page = pdf.GetPage(number);
            var header = page.Letters.Where(l => l.FontSize == 12).ToList();
            var content = page.Letters.Where(l => l.FontSize == 10).ToList();

            Assert.Contains($"HEAD{number}", page.Text);
            Assert.NotEmpty(content);
            Assert.All(header, l => Assert.InRange(PageHeight - l.BoundingBox.Top, 20, 80));
            Assert.True(content.Min(l => PageHeight - l.BoundingBox.Top) >= 80, $"page {number} content overlaps the header zone");
        }
    }

    [Fact]
    public void A_declared_footer_height_stops_the_flow_above_the_footer_zone()
    {
        var document = new PdfDocumentBuilder();
        var flow = new PdfFlowLayout(document, new PdfFlowOptions { PageWidth = 300, PageHeight = PageHeight, Margins = (20, 20, 20, 20), FooterHeight = 50 });
        flow.AddParagraph(string.Join(' ', Enumerable.Repeat("Lorem ipsum dolor sit amet.", 140)), PdfFont.Serif, 10);
        flow.Finish();

        var pdf = PdfDocument.Open(document.ToArray());

        Assert.True(pdf.PageCount >= 3);
        Assert.Equal(PageHeight - 20 - 50, flow.Bottom);
        for (var number = 1; number <= pdf.PageCount; number++)
        {
            Assert.All(pdf.GetPage(number).Letters, l => Assert.True(PageHeight - l.BoundingBox.Bottom <= PageHeight - 70 + 0.01, $"page {number} runs into the footer zone"));
        }
    }

    [Fact]
    public void Without_header_or_footer_height_the_content_area_is_the_margins()
    {
        var flow = new PdfFlowLayout(new PdfDocumentBuilder(), new PdfFlowOptions { PageHeight = 300, Margins = (10, 20, 10, 30) });

        Assert.Equal(20, flow.Y);
        Assert.Equal(270, flow.Bottom);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfFlowLayout(new PdfDocumentBuilder(), new PdfFlowOptions { HeaderHeight = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfFlowLayout(new PdfDocumentBuilder(), new PdfFlowOptions { FooterHeight = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfFlowLayout(new PdfDocumentBuilder(), new PdfFlowOptions { PageWidth = 100, Margins = (50, 10, 50, 10) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfFlowLayout(new PdfDocumentBuilder(), new PdfFlowOptions { PageHeight = 100, Margins = (10, 40, 10, 40), HeaderHeight = 20 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfFlowLayout(new PdfDocumentBuilder(), new PdfFlowOptions { Margins = (double.NaN, 0, 0, 0) }));
    }

    [Fact]
    public void A_first_row_taller_than_the_page_starts_on_the_current_page_and_leaves_no_blank_page()
    {
        var document = new PdfDocumentBuilder();
        var flow = new PdfFlowLayout(document, new PdfFlowOptions { PageWidth = SquarePage, PageHeight = SquarePage, Margins = (20, 20, 20, 20) });
        flow.AddTable(new PdfTable(1, 1) { HeaderFill = null }.Row(string.Join(' ', Enumerable.Repeat("tall", 120)), "A").Row("next", "B"));
        flow.Finish();

        var pdf = PdfDocument.Open(document.ToArray());

        Assert.Equal(2, pdf.PageCount);
        Assert.Contains("tall", pdf.GetPage(1).Text, StringComparison.Ordinal);
        Assert.Contains("next", pdf.GetPage(2).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_table_with_row_separators_only_draws_horizontal_rules_and_no_vertical_ones()
    {
        var document = new PdfDocumentBuilder();
        var flow = new PdfFlowLayout(document, new PdfFlowOptions { PageWidth = 200, PageHeight = 200, Margins = (20, 20, 20, 20) });
        var top = flow.Y;
        flow.AddTable(new PdfTable(1, 1) { BorderColor = Black, Borders = PdfBorderSides.Bottom, HeaderFill = null }.Row("A", "B").Row("C", "D").Row("E", "F"));
        var rowHeight = (flow.Y - top) / 3;
        flow.Finish();

        var image = Render(document);

        // The column boundary is x = 100: white in the middle of every row, a rule under each row, none above the first.
        for (var row = 0; row < 3; row++)
        {
            Assert.False(HasDark(image, 98, 102, top + (row * rowHeight) + 2.5, top + ((row + 1) * rowHeight) - 2.5), $"vertical rule in row {row}");
            Assert.True(HasDark(image, 60, 70, top + ((row + 1) * rowHeight) - 1, top + ((row + 1) * rowHeight) + 1), $"no rule under row {row}");
        }

        Assert.False(HasDark(image, 60, 70, top - 1, top + 0.4));
        Assert.False(HasDark(image, 19, 21, top + 2.5, top + rowHeight - 2.5));
        Assert.False(HasDark(image, 179, 181, top + 2.5, top + rowHeight - 2.5));
    }

    [Fact]
    public void Header_borders_and_cell_borders_override_the_table_borders()
    {
        var document = new PdfDocumentBuilder();
        var flow = new PdfFlowLayout(document, new PdfFlowOptions { PageWidth = 200, PageHeight = 200, Margins = (20, 20, 20, 20) });
        var top = flow.Y;
        var table = new PdfTable(1, 1) { BorderColor = Black, Borders = PdfBorderSides.None, HeaderBorders = PdfBorderSides.Bottom, HeaderFill = null };
        table.Header("H1", "H2").Row(new PdfTableCell("A"), new PdfTableCell("B") { Borders = PdfBorderSides.Left });
        flow.AddTable(table);
        var headerHeight = (flow.Y - top) / 2;
        flow.Finish();

        var image = Render(document);

        Assert.True(HasDark(image, 60, 70, top + headerHeight - 1, top + headerHeight + 1));
        Assert.True(HasDark(image, 99, 101, top + headerHeight + 3, top + (2 * headerHeight) - 3));
        Assert.False(HasDark(image, 60, 70, top + (2 * headerHeight) - 1, top + (2 * headerHeight) + 1));
        Assert.False(HasDark(image, 98, 102, top + 2.5, top + headerHeight - 2.5));
    }

    [Fact]
    public void A_table_keeps_its_full_grid_by_default()
    {
        var document = new PdfDocumentBuilder();
        var flow = new PdfFlowLayout(document, new PdfFlowOptions { PageWidth = 200, PageHeight = 200, Margins = (20, 20, 20, 20) });
        var top = flow.Y;
        flow.AddTable(new PdfTable(1, 1) { BorderColor = Black, HeaderFill = null }.Row("A", "B"));
        var height = flow.Y - top;
        flow.Finish();

        var image = Render(document);

        Assert.True(HasDark(image, 99, 101, top + 3, top + height - 3));
        Assert.True(HasDark(image, 60, 70, top - 1, top + 1));
        Assert.True(HasDark(image, 60, 70, top + height - 1, top + height + 1));
    }

    [Fact]
    public void A_right_aligned_block_flows_its_text_inside_its_width_and_the_next_paragraph_starts_below()
    {
        var document = new PdfDocumentBuilder();
        var flow = new PdfFlowLayout(document);
        var right = flow.Left + flow.ContentWidth;
        var width = flow.ContentWidth;
        var start = flow.Y;
        flow.AddBlock(200, PdfTextAlignment.Right, block =>
        {
            Assert.Equal(200, block.ContentWidth);
            Assert.Equal(right - 200, block.Left, 6);
            block.AddParagraph("Total", PdfFont.Sans, 12);
            block.AddParagraph("TTC", PdfFont.Sans, 12);
        });
        var afterBlock = flow.Y;
        flow.AddParagraph("Next", PdfFont.Sans, 12);
        flow.Finish();

        var letters = PdfDocument.Open(document.ToArray()).GetPage(1).Letters;
        var total = letters.First(l => l.Value == "T");
        var next = letters.First(l => l.Value == "N");

        Assert.Equal(right - 200, total.X, 2);
        Assert.Equal(2 * document.Metrics(PdfFont.Sans, 12).LineHeight, afterBlock - start, 6);
        Assert.True(PageHeightOf(document) - next.BoundingBox.Top >= afterBlock - 0.01);
        Assert.Equal(width, flow.ContentWidth);
    }

    [Theory]
    [InlineData(PdfTextAlignment.Left, 0.0)]
    [InlineData(PdfTextAlignment.Center, 0.5)]
    public void A_block_is_placed_left_or_centred_in_the_content_width(PdfTextAlignment alignment, double share)
    {
        var document = new PdfDocumentBuilder();
        var flow = new PdfFlowLayout(document);
        var expected = flow.Left + ((flow.ContentWidth - 150) * share);
        flow.AddBlock(150, alignment, block => block.AddParagraph("Xyz", PdfFont.Sans, 10));
        flow.Finish();

        Assert.Equal(expected, PdfDocument.Open(document.ToArray()).GetPage(1).Letters[0].X, 2);
    }

    [Fact]
    public void A_table_flows_inside_a_block_and_a_block_wider_than_the_content_is_clamped()
    {
        var document = new PdfDocumentBuilder();
        var flow = new PdfFlowLayout(document);
        var right = flow.Left + flow.ContentWidth;
        flow.AddBlock(180, PdfTextAlignment.Right, block => block.AddTable(new PdfTable(1, 1) { BorderColor = null }.Row("Left", "Right")));
        var clamped = 0.0;
        flow.AddBlock(10_000, PdfTextAlignment.Right, block => clamped = block.ContentWidth);
        flow.Finish();

        var letters = PdfDocument.Open(document.ToArray()).GetPage(1).Letters;

        Assert.Equal(right - 180 + 3, letters[0].X, 2);
        Assert.InRange(letters[^1].X + letters[^1].Width, right - 90, right);
        Assert.Equal(flow.ContentWidth, clamped);
        Assert.Throws<ArgumentOutOfRangeException>(() => flow.AddBlock(0, PdfTextAlignment.Left, _ => { }));
        Assert.Throws<ArgumentNullException>(() => flow.AddBlock(10, PdfTextAlignment.Left, null!));
    }

    [Fact]
    public void A_block_that_runs_past_the_page_continues_on_the_next_page_in_the_same_column()
    {
        var document = new PdfDocumentBuilder();
        var flow = new PdfFlowLayout(document, new PdfFlowOptions { PageWidth = 300, PageHeight = 100, Margins = (20, 20, 20, 20) });
        var right = flow.Left + flow.ContentWidth;
        flow.AddBlock(100, PdfTextAlignment.Right, block =>
        {
            for (var i = 0; i < 12; i++)
            {
                block.AddParagraph("Row", PdfFont.Sans, 10);
            }
        });
        flow.Finish();

        var pdf = PdfDocument.Open(document.ToArray());

        Assert.True(pdf.PageCount >= 2);
        Assert.All(Enumerable.Range(1, pdf.PageCount).SelectMany(n => pdf.GetPage(n).Letters).Where(l => l.Value == "R"), l => Assert.Equal(right - 100, l.X, 2));
    }

    [Fact]
    public void A_filled_box_draws_its_fill_around_the_text_and_advances_by_the_text_height_plus_twice_the_padding()
    {
        var document = new PdfDocumentBuilder();
        var flow = new PdfFlowLayout(document, new PdfFlowOptions { PageWidth = 200, PageHeight = 200, Margins = (20, 20, 20, 20) });
        var top = flow.Y;
        flow.AddBox("Paid", PdfFont.Sans, 10, new PdfBoxStyle { Padding = 6, Fill = Red });
        var height = flow.Y - top;
        var textWidth = document.AddPage(10, 10).MeasureText("Paid", PdfFont.Sans, 10);
        flow.AddParagraph("After", PdfFont.Sans, 10);
        flow.Finish();

        var pdf = PdfDocument.Open(document.ToArray());
        var image = Render(document);
        var letters = pdf.GetPage(1).Letters;

        Assert.Equal(document.Metrics(PdfFont.Sans, 10).LineHeight + 12, height, 6);
        Assert.Equal(20 + 6, letters[0].X, 2);
        Assert.True(SquarePage - letters[0].BoundingBox.Top >= top + 6 - 0.01);
        Assert.Equal((255, 0, 0), Pixel(image, 22, (int)top + 2));
        Assert.Equal((255, 0, 0), Pixel(image, (int)(20 + textWidth + 12 - 2), (int)(top + height - 2)));
        Assert.Equal((255, 255, 255), Pixel(image, (int)(20 + textWidth + 12 + 3), (int)top + 2));
        Assert.Equal((255, 255, 255), Pixel(image, 70, (int)(top + height + 2)));
        Assert.True(SquarePage - letters[4].BoundingBox.Top >= top + height - 0.01);
    }

    [Fact]
    public void A_box_can_be_aligned_wide_and_bordered_on_all_four_sides_without_fill()
    {
        var document = new PdfDocumentBuilder();
        var flow = new PdfFlowLayout(document, new PdfFlowOptions { PageWidth = 200, PageHeight = 200, Margins = (20, 20, 20, 20) });
        var top = flow.Y;
        flow.AddBox("OK", PdfFont.Sans, 10, new PdfBoxStyle { Padding = 8, Border = Black, BorderWidth = 1, Width = 100, BoxAlignment = PdfTextAlignment.Right, TextAlignment = PdfTextAlignment.Center });
        var height = flow.Y - top;
        flow.Finish();

        var image = Render(document);
        var mid = (int)(top + (height / 2));

        Assert.Equal(top + height, flow.Y, 6);
        Assert.True(HasDark(image, 79, 81, mid - 1, mid + 1), "left edge");
        Assert.True(HasDark(image, 179, 181, mid - 1, mid + 1), "right edge");
        Assert.True(HasDark(image, 100, 110, top - 1, top + 1), "top edge");
        Assert.True(HasDark(image, 100, 110, top + height - 1, top + height + 1), "bottom edge");
        Assert.Equal((255, 255, 255), Pixel(image, 85, mid - 4));
    }

    [Fact]
    public void A_box_that_does_not_fit_moves_to_the_next_page_and_wraps_long_text()
    {
        var document = new PdfDocumentBuilder();
        var flow = new PdfFlowLayout(document, new PdfFlowOptions { PageWidth = 200, PageHeight = 200, Margins = (20, 20, 20, 20) }) { Y = 150 };
        flow.AddBox(string.Join(' ', Enumerable.Repeat("wrapped", 12)), PdfFont.Sans, 10, new PdfBoxStyle { Padding = 6, Fill = Red });
        var height = flow.Y - 20;
        flow.Finish();

        var pdf = PdfDocument.Open(document.ToArray());

        Assert.Equal(2, pdf.PageCount);
        Assert.Empty(pdf.GetPage(1).Letters);
        Assert.True(height > document.Metrics(PdfFont.Sans, 10).LineHeight + 12);
        Assert.Equal((255, 0, 0), Pixel(PdfRenderer.Render(pdf.GetPage(2), new PdfRenderOptions { Dpi = 72 }, TestContext.Current.CancellationToken).Image, 22, 22));
        Assert.Throws<ArgumentNullException>(() => flow.AddBox(null!, PdfFont.Sans, 10));
    }

    private static double PageHeightOf(PdfDocumentBuilder document) => document.Pages.First().Height;

    private static RasterImage Render(PdfDocumentBuilder document) =>
        PdfRenderer.Render(PdfDocument.Open(document.ToArray()).GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image;

    private static bool HasDark(RasterImage image, double x0, double x1, double y0, double y1)
    {
        for (var y = (int)Math.Floor(y0); y <= (int)Math.Ceiling(y1); y++)
        {
            for (var x = (int)Math.Floor(x0); x <= (int)Math.Ceiling(x1); x++)
            {
                if (Pixel(image, x, y).R < 140)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static (int R, int G, int B) Pixel(RasterImage image, int x, int y)
    {
        var (r, g, b, _) = image.GetRgba(x, y);
        return (r, g, b);
    }
}
