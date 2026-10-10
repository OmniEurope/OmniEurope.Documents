// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Editing;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Pdf.Rendering.Fonts;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Tests.Pdf;

public sealed class PdfRendererTests
{
    [Fact]
    public void Fills_paths_with_both_rules_and_anti_aliased_edges()
    {
        var image = Render(RawPdf.Page("1 0 0 rg 20 20 60 60 re f 0 0 1 rg 100.5 20 50 50 re f 0 g 120 120 60 60 re 135 135 30 30 re f* 20 120 60 60 re 35 135 30 30 re f"));

        Assert.Equal((255, 0, 0), Pixel(image, 50, 150));
        Assert.Equal((255, 255, 255), Pixel(image, 10, 10));
        var edge = Pixel(image, 100, 150);
        Assert.InRange(edge.B, 250, 255);
        Assert.InRange(edge.R, 100, 160);
        Assert.Equal((255, 255, 255), Pixel(image, 150, 50));
        Assert.Equal((0, 0, 0), Pixel(image, 50, 50));
    }

    [Fact]
    public void Strokes_follow_width_dashes_and_caps()
    {
        var image = Render(RawPdf.Page("0 0 1 RG 4 w 20 100 m 180 100 l S 0 G [10 10] 0 d 2 w 20 50 m 180 50 l S [] 0 d 10 w 2 J 50 180 m 150 180 l S 0 J 50 165 m 150 165 l S"));

        Assert.Equal((0, 0, 255), Pixel(image, 100, 100));
        Assert.Equal((255, 255, 255), Pixel(image, 100, 104));
        Assert.Equal((0, 0, 0), Pixel(image, 25, 150));
        Assert.Equal((255, 255, 255), Pixel(image, 35, 150));
        Assert.Equal((0, 0, 0), Pixel(image, 47, 20));
        Assert.Equal((255, 255, 255), Pixel(image, 47, 35));
    }

    [Fact]
    public void Clips_and_opacity_apply()
    {
        var image = Render(RawPdf.Page("q 50 50 100 100 re W n 1 0 0 rg 0 0 200 200 re f Q /G1 gs 0 g 0 0 40 40 re f", "/ExtGState << /G1 << /ca 0.5 /BM /Multiply >> >>"));

        Assert.Equal((255, 255, 255), Pixel(image, 40, 100));
        Assert.Equal((255, 0, 0), Pixel(image, 100, 100));
        Assert.InRange(Pixel(image, 20, 180).R, 125, 130);
    }

    [Fact]
    public void Draws_images_inline_images_and_stencil_masks()
    {
        var builder = new PdfDocumentBuilder();
        var source = new RasterImage(2, 2, ImageColorType.Rgb, [255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 0]);
        builder.AddPage(200, 200).DrawImage(builder.AddImage(source), 50, 50, 100, 100);
        var drawn = Render(builder.ToArray());
        var inline = Render(RawPdf.Page("q 100 0 0 50 50 100 cm BI /W 2 /H 1 /CS /RGB /BPC 8 ID ÿ\u0000\u0000\u0000\u0000ÿ EI Q 0 1 0 rg q 100 0 0 50 50 20 cm BI /W 2 /H 1 /IM true ID @ EI Q"));

        Assert.Equal((255, 0, 0), Pixel(drawn, 70, 70));
        Assert.Equal((0, 255, 0), Pixel(drawn, 130, 70));
        Assert.Equal((0, 0, 255), Pixel(drawn, 70, 130));
        Assert.Equal((255, 255, 0), Pixel(drawn, 130, 130));
        Assert.Equal((255, 0, 0), Pixel(inline, 60, 75));
        Assert.Equal((0, 0, 255), Pixel(inline, 140, 75));
        Assert.Equal((0, 255, 0), Pixel(inline, 60, 155));
        Assert.Equal((255, 255, 255), Pixel(inline, 140, 155));
    }

    [Fact]
    public void Draws_text_from_embedded_true_type_fonts()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(200, 200).DrawText("H", 20, 150, PdfFont.Sans, 120);

        var image = Render(builder.ToArray());

        Assert.Equal((0, 0, 0), Pixel(image, 36, 110));
        Assert.Equal((255, 255, 255), Pixel(image, 63, 80));
    }

    [Theory]
    [InlineData("Type1C")]
    [InlineData("Type1")]
    public void Draws_text_from_embedded_cff_and_type1_fonts(string kind)
    {
        var (program, dictionary) = kind == "Type1C"
            ? (FontPrograms.Cff(), "/Subtype /Type1C")
            : ProgramOf(FontPrograms.Type1());
        var file = kind == "Type1C" ? "FontFile3" : "FontFile";
        var pdf = RawPdf.Page(
            "BT /F1 100 Tf 20 20 Td (A) Tj ET",
            "/Font << /F1 5 0 R >>",
            string.Empty,
            "<< /Type /Font /Subtype /Type1 /BaseFont /Test /FirstChar 65 /LastChar 65 /Widths [700] /FontDescriptor 6 0 R >>",
            $"<< /Type /FontDescriptor /FontName /Test /Flags 32 /FontBBox [0 0 1000 1000] /ItalicAngle 0 /Ascent 800 /Descent -200 /CapHeight 600 /StemV 80 /{file} 7 0 R >>",
            RawPdf.Stream(dictionary, program));

        var rendering = PdfRenderer.Render(PdfDocument.Open(pdf).GetPage(1), new PdfRenderOptions { Dpi = 72 });

        Assert.Empty(rendering.Gaps);
        Assert.Equal((0, 0, 0), Pixel(rendering.Image, 55, 150));
        Assert.Equal((255, 255, 255), Pixel(rendering.Image, 25, 150));
        Assert.Equal((255, 255, 255), Pixel(rendering.Image, 55, 115));
    }

    [Fact]
    public void Draws_type3_glyphs_and_fonts_that_are_not_embedded()
    {
        var type3 = RawPdf.Page(
            "BT /F1 50 Tf 20 20 Td (A) Tj ET",
            "/Font << /F1 5 0 R >>",
            string.Empty,
            "<< /Type /Font /Subtype /Type3 /FontMatrix [0.001 0 0 0.001 0 0] /FontBBox [0 0 1000 1000] /CharProcs << /sq 6 0 R >> /Encoding << /Type /Encoding /Differences [65 /sq] >> /FirstChar 65 /LastChar 65 /Widths [1000] >>",
            RawPdf.Stream(string.Empty, RawPdf.Ascii("1000 0 0 0 1000 1000 d1 0 0 1000 1000 re f")));
        var helvetica = RawPdf.Page("BT /F1 80 Tf 20 60 Td (Il) Tj /F2 40 Tf (x) Tj ET", "/Font << /F1 5 0 R /F2 6 0 R >>", string.Empty,
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>", "<< /Type /Font /Subtype /TrueType /BaseFont /Fantaisie >>");

        var square = Render(type3);
        var text = PdfRenderer.Render(PdfDocument.Open(helvetica).GetPage(1), new PdfRenderOptions { Dpi = 72 });

        Assert.Equal((0, 0, 0), Pixel(square, 45, 155));
        Assert.Equal((255, 255, 255), Pixel(square, 75, 155));
        var dark = Enumerable.Range(0, 200).Sum(x => Enumerable.Range(0, 200).Count(y => Pixel(text.Image, x, y).R < 100));
        Assert.True(dark > 300, $"{dark} dark pixels");
        Assert.Contains("fonts that are not embedded are drawn with bundled look-alikes", text.Gaps);
    }

    [Fact]
    public void Applies_page_rotation_and_annotation_appearances()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(200, 100).FillRectangle(0, 0, 20, 20, PdfColor.Red);
        var rotated = PdfRenderer.Render(PdfDocument.Open(PdfEditor.RotatePages(PdfDocument.Open(builder.ToArray()), [1], 90)).GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image;
        var annotated = Render(RawPdf.Page(string.Empty, string.Empty, "/Annots [5 0 R 7 0 R]",
            "<< /Type /Annot /Subtype /Stamp /Rect [100 100 150 150] /AP << /N 6 0 R >> >>",
            RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 10 10]", RawPdf.Ascii("0 0 1 rg 0 0 10 10 re f")),
            "<< /Type /Annot /Subtype /Stamp /F 2 /Rect [0 0 50 50] /AP << /N 6 0 R >> >>"));

        Assert.Equal((100, 200), (rotated.Width, rotated.Height));
        Assert.Equal((255, 0, 0), Pixel(rotated, 90, 10));
        Assert.Equal((255, 255, 255), Pixel(rotated, 10, 10));
        Assert.Equal((0, 0, 255), Pixel(annotated, 125, 75));
        Assert.Equal((255, 255, 255), Pixel(annotated, 25, 175));
    }

    [Fact]
    public void Reports_what_it_does_not_draw_and_writes_png()
    {
        var pdf = RawPdf.Page("/Sh1 sh /G1 gs 0 g 10 10 20 20 re f", "/ExtGState << /G1 << /BM /Multiply /SMask << /S /Luminosity >> >> >>");

        var rendering = PdfRenderer.Render(PdfDocument.Open(pdf).GetPage(1), new PdfRenderOptions { Dpi = 144 });
        var png = PngCodec.Decode(rendering.ToPng());

        Assert.Equal(["shadings of this kind are not drawn", "soft masks that cannot be read are not applied"], rendering.Gaps);
        Assert.Equal((400, 400), (png.Width, png.Height));
        Assert.Equal(PdfRenderer.RenderToPng(PdfDocument.Open(pdf), 1, new PdfRenderOptions { Dpi = 144 }), rendering.ToPng());
    }

    [Theory]
    [InlineData(0, ".notdef")]
    [InlineData(34, "A")]
    [InlineData(66, "a")]
    [InlineData(116, "bullet")]
    [InlineData(229, "exclamsmall")]
    [InlineData(274, "Asmall")]
    [InlineData(379, "001.000")]
    [InlineData(390, "Semibold")]
    public void Knows_the_cff_standard_strings(int sid, string name)
    {
        Assert.Equal(391, CffStandardStrings.Count);
        Assert.Equal(name, CffStandardStrings.Get(sid));
    }

    private static (byte[] Program, string Dictionary) ProgramOf((byte[] Program, int Length1, int Length2) font) =>
        (font.Program, $"/Length1 {font.Length1} /Length2 {font.Length2} /Length3 0");

    private static RasterImage Render(byte[] pdf) => PdfRenderer.Render(PdfDocument.Open(pdf).GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image;

    private static (int R, int G, int B) Pixel(RasterImage image, int x, int y)
    {
        var (r, g, b, _) = image.GetRgba(x, y);
        return (r, g, b);
    }
}
