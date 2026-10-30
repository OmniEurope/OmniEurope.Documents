// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Editing;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Tests.Pdf;

namespace OmniEurope.Documents.Tests.Integration;

public sealed class RemainingAuditRegressionTests
{
    [Theory]
    [InlineData("INDEX(A1:A10,10)", 0.0)]
    [InlineData("COUNTBLANK(A1:A10)", 9.0)]
    [InlineData("SUM(A1:A10+1)", 11.0)]
    [InlineData("INDEX(A:A,1)", 1.0)]
    public void Explicit_ranges_preserve_blank_positions_and_whole_columns_remain_bounded(string formula, double expected)
    {
        var book = new XlsxWorkbook();
        var sheet = book.AddWorksheet("Data");
        sheet.Cell("A1").Value = 1;
        sheet.Cell("B1").Formula = formula;
        Assert.Empty(book.Recalculate().Unsupported);
        Assert.Equal(expected, sheet.Cell("B1").NumberValue);
    }

    [Theory]
    [InlineData("ROUND(1,-100)", 0.0)]
    [InlineData("ROUND(1,-308)", 0.0)]
    [InlineData("ROUND(1E30,-30)", 1E30)]
    [InlineData("ROUND(-1.25E30,-30)", -1E30)]
    [InlineData("ROUND(1E308,-308)", 1E308)]
    [InlineData("ROUND(7.9228162514264E28,-17)", 7.92281625143E28)]
    [InlineData("ROUND(-7.9228162514264E28,-17)", -7.92281625143E28)]
    [InlineData("ROUND(2.675,2)", 2.68)]
    [InlineData("ROUNDUP(1,-100)", 1E100)]
    [InlineData("ROUNDDOWN(1,-100)", 0.0)]
    public void Extreme_rounding_does_not_abort_the_workbook(string formula, double expected)
    {
        var book = new XlsxWorkbook();
        var sheet = book.AddWorksheet("Data");
        sheet.Cell("A1").Formula = formula;
        sheet.Cell("B1").Formula = "1+1";
        Assert.Empty(book.Recalculate().Unsupported);
        var actual = sheet.Cell("A1").NumberValue!.Value;
        if (expected == 0) Assert.Equal(0, actual);
        else Assert.InRange(Math.Abs((actual - expected) / expected), 0, 1E-14);
        Assert.Equal(2.0, sheet.Cell("B1").NumberValue);
    }

    [Theory]
    [InlineData("/DeviceRGB", 8, "/Mask [255 255 0 0 0 0]", "FF0000>")]
    [InlineData("/DeviceRGB", 8, "/Mask [255 255 0 0 0 0] /Decode [1 0 1 0 1 0]", "FF0000>")]
    [InlineData("/DeviceGray", 8, "/Mask [0 0]", "00>")]
    [InlineData("[/Indexed /DeviceRGB 1 <000000FF0000>]", 1, "/Mask [1 1]", "80>")]
    public void Color_key_masks_are_applied_before_decode_and_compositing(string space, int bits, string mask, string data)
    {
        var source = RawPdf.Page("0 0 1 rg 0 0 50 50 re f q 50 0 0 50 0 0 cm /Im Do Q", "/XObject << /Im 5 0 R >>", "",
            RawPdf.Stream($"/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace {space} /BitsPerComponent {bits} /Filter /ASCIIHexDecode {mask}", RawPdf.Ascii(data)));
        var page = PdfDocument.Open(source).GetPage(1);
        var rendered = PdfRenderer.Render(page, new PdfRenderOptions { Dpi = 72 });
        Assert.Equal(((byte)0, (byte)0, (byte)255, (byte)255), rendered.Image.GetRgba(25, 175));
        Assert.Empty(rendered.Gaps);
        Assert.Equal(0, page.Images[0].Decode()!.GetRgba(0, 0).A);
    }

    [Theory]
    [InlineData("A1:B1048576", "SUM({0})", 10.0)]
    [InlineData("A1:B1048576", "COUNT({0})", 4.0)]
    [InlineData("A1:B1048576", "COUNTA({0})", 4.0)]
    [InlineData("A1:Z100000", "MAX({0})", 4.0)]
    [InlineData("A1:B1048576", "VLOOKUP(3,{0},2,FALSE)", 4.0)]
    [InlineData("A1:D1048576", "VLOOKUP(3,{0},4,FALSE)", 0.0)]
    [InlineData("A1:B1048576", "INDEX({0},2,2)", 4.0)]
    [InlineData("A1:B1048576", "INDEX({0},1048576,2)", 0.0)]
    [InlineData("A1:A1048576", "MATCH(3,{0},0)", 2.0)]
    [InlineData("A1:B1048576", "COUNTIF({0},1)", 1.0)]
    [InlineData("A1:B1048576", "COUNTIF({0},\"<>1\")", 2097151.0)]
    [InlineData("A1:B1048576", "COUNTBLANK({0})", 2097148.0)]
    [InlineData("A1:B1048576", "ROWS({0})", 1048576.0)]
    public void Large_explicit_ranges_are_read_without_allocating_their_blank_cells(string range, string formula, double expected)
    {
        var book = new XlsxWorkbook();
        var sheet = book.AddWorksheet("Data");
        sheet.Cell("A1").Value = 1;
        sheet.Cell("B1").Value = 2;
        sheet.Cell("A2").Value = 3;
        sheet.Cell("B2").Value = 4;
        sheet.Cell("AB5").Formula = string.Format(CultureInfo.InvariantCulture, formula, range);
        Assert.Empty(book.Recalculate().Unsupported);
        Assert.Equal(expected, sheet.Cell("AB5").NumberValue);
    }

    [Theory]
    [InlineData("SUMIF(Keys!A1:A6,\"\",Data!B1:B6)", 30.0)]
    [InlineData("SUMIF(Keys!A1:A6,1,Data!B1:B6)", 10.0)]
    [InlineData("AVERAGEIF(Keys!A1:A6,\"\",Data!B1:B6)", 15.0)]
    [InlineData("COUNTIFS(Keys!A1:A6,\"\",Data!B1:B6,\">10\")", 1.0)]
    public void Conditions_on_blank_cells_reach_values_past_the_criteria_range_used_cells(string formula, double expected)
    {
        var book = new XlsxWorkbook();
        var keys = book.AddWorksheet("Keys");
        var data = book.AddWorksheet("Data");
        keys.Cell("A1").Value = 1;
        data.Cell("B1").Value = 10;
        data.Cell("B4").Value = 10;
        data.Cell("B6").Value = 20;
        keys.Cell("C1").Formula = formula;
        Assert.Empty(book.Recalculate().Unsupported);
        Assert.Equal(expected, keys.Cell("C1").NumberValue);
    }

    [Fact]
    public void Many_formulas_over_a_large_explicit_range_stay_cheap()
    {
        var book = new XlsxWorkbook();
        var sheet = book.AddWorksheet("Data");
        sheet.Cell("A1").Value = 1;
        for (var row = 1; row <= 100; row++) sheet.Cell(row, 3).Formula = "SUM($A$1:$A$1048576)";
        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Empty(book.Recalculate().Unsupported);
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, 64 * 1024 * 1024);
        Assert.Equal(1.0, sheet.Cell(100, 3).NumberValue);
    }

    [Fact]
    public void An_operation_on_every_element_of_a_too_large_range_is_reported_not_truncated()
    {
        var book = new XlsxWorkbook();
        var sheet = book.AddWorksheet("Data");
        sheet.Cell("A1").Value = 1;
        sheet.Cell("B1").Value = 2;
        sheet.Cell("D1").Formula = "SUM(A1:B1048576+1)";
        Assert.Single(book.Recalculate().Unsupported);
    }

    [Theory]
    [InlineData((byte)0x00, "", 255, 0, 0)]
    [InlineData((byte)0xFF, "", 0, 0, 255)]
    [InlineData((byte)0xFF, "/Decode [1 0]", 255, 0, 0)]
    public void An_explicit_mask_paints_where_its_stencil_marks(byte stencil, string decode, int r, int g, int b)
    {
        var source = RawPdf.Page("0 0 1 rg 0 0 50 50 re f q 50 0 0 50 0 0 cm /Im Do Q", "/XObject << /Im 5 0 R >>", "",
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Mask 6 0 R", [255, 0, 0]),
            RawPdf.Stream($"/Type /XObject /Subtype /Image /Width 1 /Height 1 /ImageMask true /BitsPerComponent 1 {decode}", [stencil]));
        var page = PdfDocument.Open(source).GetPage(1);
        var rendered = PdfRenderer.Render(page, new PdfRenderOptions { Dpi = 72 });
        Assert.Equal(((byte)r, (byte)g, (byte)b, (byte)255), rendered.Image.GetRgba(25, 175));
        Assert.Empty(rendered.Gaps);
        Assert.NotNull(page.Images[0].Decode());
    }

    [Fact]
    public void An_explicit_mask_finer_than_its_image_keeps_its_own_edges()
    {
        // A one-pixel colour shaped by a 2 x 1 stencil: the left half is painted, the right half shows the page.
        var source = RawPdf.Page("0 0 1 rg 0 0 50 50 re f q 50 0 0 50 0 0 cm /Im Do Q", "/XObject << /Im 5 0 R >>", "",
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Mask 6 0 R", [255, 0, 0]),
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 2 /Height 1 /ImageMask true /BitsPerComponent 1", [0x40]));
        var image = PdfRenderer.Render(PdfDocument.Open(source).GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image;
        Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), image.GetRgba(10, 175));
        Assert.Equal(((byte)0, (byte)0, (byte)255, (byte)255), image.GetRgba(40, 175));
    }

    [Fact]
    public void An_explicit_mask_that_would_multiply_the_pixels_is_refused_and_reported()
    {
        // A wide image and a tall stencil must not grow into a square of 16384 x 16384 pixels.
        var source = RawPdf.Page("q 50 0 0 50 0 0 cm /Im Do Q", "/XObject << /Im 5 0 R >>", "",
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 16384 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Mask 6 0 R", new byte[16384]),
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 1 /Height 16384 /ImageMask true /BitsPerComponent 1", new byte[16384]));
        var page = PdfDocument.Open(source).GetPage(1);
        Assert.Null(page.Images[0].Decode());
        Assert.NotEmpty(PdfRenderer.Render(page, new PdfRenderOptions { Dpi = 72 }).Gaps);
    }

    [Fact]
    public void An_explicit_mask_across_its_image_keeps_both_on_their_own_axis()
    {
        // Four columns of colour, four rows of stencil: every column keeps its colour and only the top row is painted.
        var source = RawPdf.Page("q 50 0 0 50 0 0 cm /Im Do Q", "/XObject << /Im 5 0 R >>", "",
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 4 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Mask 6 0 R", [0, 80, 160, 240]),
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 1 /Height 4 /ImageMask true /BitsPerComponent 1", [0x00, 0x80, 0x80, 0x80]));
        var decoded = PdfDocument.Open(source).GetPage(1).Images[0].Decode()!;
        Assert.Equal((4, 4), (decoded.Width, decoded.Height));
        Assert.Equal((240, 255), (decoded.GetRgba(3, 0).R, decoded.GetRgba(3, 0).A));
        Assert.Equal(0, decoded.GetRgba(3, 1).A);
    }

    [Fact]
    public void An_explicit_mask_that_is_not_a_stencil_is_reported()
    {
        var source = RawPdf.Page("q 50 0 0 50 0 0 cm /Im Do Q", "/XObject << /Im 5 0 R >>", "",
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Mask 6 0 R", [255, 0, 0]),
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8", [255]));
        var result = PdfRenderer.Render(PdfDocument.Open(source).GetPage(1));
        Assert.NotEmpty(result.Gaps);
    }

    [Fact]
    public void Redaction_keeps_an_explicit_mask_and_blanks_only_the_covered_pixels()
    {
        var source = RawPdf.Page("q 100 0 0 100 0 0 cm /Im Do Q", "/XObject << /Im 5 0 R >>", "",
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 2 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Mask 6 0 R", [255, 0, 0, 0, 255, 0]),
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 2 /Height 1 /ImageMask true /BitsPerComponent 1", [0x00]));
        var redacted = PdfRedactor.Redact(PdfDocument.Open(source), [new PdfRedaction(1, new PdfRectangle(0, 0, 50, 100))]).Pdf;
        var image = PdfRenderer.Render(PdfDocument.Open(redacted).GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image;
        Assert.Equal(((byte)0, (byte)255, (byte)0, (byte)255), image.GetRgba(75, 150));
        Assert.NotEqual(((byte)255, (byte)0, (byte)0, (byte)255), image.GetRgba(25, 150));
    }
}
