// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf.Rendering;

namespace OmniEurope.Documents.Tests.Pdf;

public sealed class PdfPageComparisonTests
{
    [Fact]
    public void Identical_pages_do_not_differ()
    {
        var png = Page(100, 80, (20, 20, 30, 30));

        var result = PdfPageComparison.Compare(png, png);

        Assert.Equal(0, result.DifferingPixels);
        Assert.Equal(0, result.DifferingRatio);
        Assert.False(result.SizeMismatch);
        Assert.Equal(900, result.InkPixelsA);
        Assert.NotNull(result.DiffPng);
    }

    [Fact]
    public void A_one_pixel_shift_is_tolerated_by_the_neighbourhood_only()
    {
        var a = Page(100, 80, (20, 20, 30, 30));
        var b = Page(100, 80, (21, 20, 30, 30));

        var tolerant = PdfPageComparison.Compare(a, b, new PdfPageComparisonOptions { NeighbourhoodRadius = 1 });
        var strict = PdfPageComparison.Compare(a, b, new PdfPageComparisonOptions { NeighbourhoodRadius = 0 });

        Assert.Equal(0, tolerant.DifferingPixels);
        Assert.Equal(60, strict.DifferingPixels);
    }

    [Fact]
    public void Different_content_differs()
    {
        var result = PdfPageComparison.Compare(Page(100, 80, (10, 10, 20, 20)), Page(100, 80, (60, 40, 20, 20)));

        Assert.Equal(800, result.DifferingPixels);
        Assert.Equal(1, result.DifferingRatioOfInk, 6);
        Assert.InRange(result.DifferingRatio, 0.09, 0.11);
    }

    [Fact]
    public void Blank_against_ink_has_a_high_ratio_of_ink_and_a_small_plain_ratio()
    {
        var result = PdfPageComparison.Compare(Page(200, 200), Page(200, 200, (50, 50, 10, 10)));

        Assert.Equal(100, result.DifferingPixels);
        Assert.Equal(0, result.InkPixelsA);
        Assert.Equal(1, result.DifferingRatioOfInk, 6);
        Assert.True(result.DifferingRatio < 0.01);
    }

    [Fact]
    public void Two_blank_pages_have_no_ink_ratio()
    {
        var result = PdfPageComparison.Compare(Page(10, 10), Page(10, 10));

        Assert.Equal(0, result.DifferingRatioOfInk);
    }

    [Fact]
    public void Size_mismatch_compares_the_common_area()
    {
        var result = PdfPageComparison.Compare(Page(100, 80, (10, 10, 5, 5)), Page(120, 60, (10, 10, 5, 5)), new PdfPageComparisonOptions { ProduceDiffImage = false });

        Assert.True(result.SizeMismatch);
        Assert.Equal((100, 60), (result.Width, result.Height));
        Assert.Equal(0, result.DifferingPixels);
        Assert.Null(result.DiffPng);
    }

    [Fact]
    public void Diff_image_marks_differences_in_red()
    {
        var result = PdfPageComparison.Compare(Page(20, 20), Page(20, 20, (5, 5, 2, 2)));

        var diff = PngCodec.Decode(result.DiffPng!);
        Assert.Equal((255, 0, 0), (diff.GetRgba(5, 5).R, diff.GetRgba(5, 5).G, diff.GetRgba(5, 5).B));
        Assert.Equal((255, 255, 255), (diff.GetRgba(15, 15).R, diff.GetRgba(15, 15).G, diff.GetRgba(15, 15).B));
    }

    [Theory]
    [InlineData(0, 32, 1)]
    [InlineData(150, -1, 1)]
    [InlineData(150, 256, 1)]
    [InlineData(150, 32, -1)]
    [InlineData(150, 32, 9)]
    public void Invalid_options_are_refused(double dpi, int threshold, int radius)
    {
        var png = Page(10, 10);
        var options = new PdfPageComparisonOptions { Dpi = dpi, ChannelThreshold = threshold, NeighbourhoodRadius = radius };

        Assert.Throws<ArgumentException>(() => PdfPageComparison.Compare(png, png, options));
    }

    [Fact]
    public void Images_over_the_pixel_limit_are_refused()
    {
        var huge = new RasterImage(10000, 10000, ImageColorType.Gray);

        Assert.Throws<ArgumentException>(() => PdfPageComparison.Compare(huge, huge));
    }

    [Fact]
    public void Pages_are_rendered_and_compared()
    {
        var a = Document("0 g 20 20 60 60 re f");
        var b = Document("0 g 20 20 60 60 re f");
        var c = Document("0 g 100 100 60 60 re f");

        Assert.Equal(0, PdfPageComparison.Compare(a, b, 1).DifferingPixels);
        Assert.True(PdfPageComparison.Compare(a, c, 1).DifferingRatioOfInk > 0.5);
    }

    [Fact]
    public void A_word_365_export_equals_itself()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Word", "rent-receipt-template.word365.pdf");
        var document = OmniEurope.Documents.Pdf.PdfDocument.Open(File.ReadAllBytes(path));

        var result = PdfPageComparison.Compare(document, document, 1);

        Assert.Equal(0, result.DifferingPixels);
        Assert.True(result.InkPixelsA > 0);
    }

    private static OmniEurope.Documents.Pdf.PdfDocument Document(string content) => OmniEurope.Documents.Pdf.PdfDocument.Open(RawPdf.Page(content));

    private static byte[] Page(int width, int height, params (int X, int Y, int W, int H)[] rectangles)
    {
        var pixels = new byte[width * height * 3];
        Array.Fill(pixels, (byte)255);
        foreach (var (rx, ry, rw, rh) in rectangles)
        {
            for (var y = ry; y < ry + rh; y++)
            {
                for (var x = rx; x < rx + rw; x++)
                {
                    var i = ((y * width) + x) * 3;
                    (pixels[i], pixels[i + 1], pixels[i + 2]) = (0, 0, 0);
                }
            }
        }

        return PngCodec.Encode(new RasterImage(width, height, ImageColorType.Rgb, pixels));
    }
}
