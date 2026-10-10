// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;

namespace OmniEurope.Documents.Tests.Pdf;

public sealed class PdfRendererFontTests
{
    private const string Descriptor = "<< /Type /FontDescriptor /FontName /Test /Flags 32 /FontBBox [0 0 1000 1000] /ItalicAngle 0 /Ascent 800 /Descent -200 /CapHeight 600 /StemV 80 /FontFile3 7 0 R >>";

    [Theory]
    [InlineData(1, -1, true)]
    [InlineData(2, -1, false)]
    [InlineData(0, 0, false)]
    [InlineData(1, 1, true)]
    public void Reads_cff_charsets_encodings_and_real_numbers(int charsetFormat, int encodingFormat, bool realMatrix)
    {
        var cff = FontPrograms.Cff(charsetFormat, encodingFormat, realMatrix);
        var shown = encodingFormat >= 0 ? "B" : "A";

        var image = SimpleFont(cff, "/Subtype /Type1C", shown);

        AssertSquare(image);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Reads_cid_keyed_cff_fonts(int fdSelectFormat)
    {
        var cff = FontPrograms.Cff(fdSelectFormat: fdSelectFormat);
        var pdf = RawPdf.Page(
            "BT /F1 100 Tf 20 20 Td <0005> Tj ET",
            "/Font << /F1 5 0 R >>",
            string.Empty,
            "<< /Type /Font /Subtype /Type0 /BaseFont /Test /Encoding /Identity-H /DescendantFonts [8 0 R] >>",
            Descriptor,
            RawPdf.Stream("/Subtype /CIDFontType0C", cff),
            "<< /Type /Font /Subtype /CIDFontType0 /BaseFont /Test /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor 6 0 R /DW 1000 /W [5 [700]] >>");

        AssertSquare(Render(pdf));
    }

    [Fact]
    public void Opens_open_type_wrappers_around_cff_and_true_type_outlines()
    {
        var cff = SimpleFont(FontPrograms.OpenType(FontPrograms.Cff()), "/Subtype /OpenType", "A");
        var trueType = SimpleFont(FontLibrary.Bundled("LiberationSans", false, false).Data, "/Subtype /OpenType", "H");

        AssertSquare(cff);
        var dark = Enumerable.Range(0, 200).Sum(x => Enumerable.Range(0, 200).Count(y => trueType.GetRgba(x, y).R < 100));
        Assert.True(dark > 500, $"{dark} dark pixels");
    }

    [Fact]
    public void Joins_corners_and_closes_paths()
    {
        var miter = Render(RawPdf.Page("10 w 0 j 20 20 m 100 100 l 180 20 l S"));
        var bevel = Render(RawPdf.Page("10 w 2 j 20 20 m 100 100 l 180 20 l S"));
        var round = Render(RawPdf.Page("10 w 1 j 1 J 20 20 m 100 100 l 180 20 l s 0 0 1 RG 3 w 0 j 30 150 m 170 150 l 100 190 l h S"));

        Assert.Equal(0, miter.GetRgba(100, 95).R);
        Assert.Equal(255, bevel.GetRgba(100, 94).R);
        Assert.Equal(0, round.GetRgba(100, 97).R);
        Assert.Equal(0, round.GetRgba(100, 180).R);
        Assert.Equal((0, 0, 255, 255), round.GetRgba(100, 50));
    }

    [Fact]
    public void Text_rendering_modes_stroke_and_clip()
    {
        var font = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>";
        var stroked = Render(RawPdf.Page("BT /F1 150 Tf 1 Tr 0.5 w 20 40 Td (I) Tj ET", "/Font << /F1 5 0 R >>", string.Empty, font));
        var clipped = Render(RawPdf.Page("BT /F1 150 Tf 7 Tr 20 40 Td (I) Tj ET 0 0 1 rg 0 0 200 200 re f", "/Font << /F1 5 0 R >>", string.Empty, font));

        var row = Enumerable.Range(0, 200).Select(x => stroked.GetRgba(x, 100).R).ToList();
        var inked = row.Select((value, x) => (value, x)).Where(p => p.value < 128).Select(p => p.x).ToList();
        Assert.True(inked.Count >= 2 && row[(inked[0] + inked[^1]) / 2] > 200, "a stroked stem is hollow");
        var blue = Enumerable.Range(0, 200).Count(x => clipped.GetRgba(x, 100).B > 200 && clipped.GetRgba(x, 100).R < 50);
        Assert.InRange(blue, 5, 40);
        Assert.Equal((255, 255, 255, 255), clipped.GetRgba(150, 100));
    }

    [Fact]
    public void Named_colour_spaces_patterns_and_positioned_text_arrays()
    {
        var rendering = PdfRenderer.Render(PdfDocument.Open(RawPdf.Page(
            "/Cs1 cs 1 0 0 sc 0 0 50 50 re f /Pattern cs /P1 scn 60 0 20 20 re f BT /F1 50 Tf 0 g 20 100 Td [(I) -2000 (I)] TJ ET",
            "/ColorSpace << /Cs1 /DeviceRGB >> /Font << /F1 5 0 R >>",
            string.Empty,
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")).GetPage(1), new PdfRenderOptions { Dpi = 72 });

        Assert.Equal((255, 0, 0, 255), rendering.Image.GetRgba(25, 175));
        Assert.Contains("patterns that cannot be read are drawn as a flat colour", rendering.Gaps);
        var inked = Enumerable.Range(0, 200).Where(x => rendering.Image.GetRgba(x, 85).R < 100).ToList();
        Assert.Contains(inked, x => x < 40);
        Assert.Contains(inked, x => x is > 120 and < 150);
    }

    [Fact]
    public void Simple_font_encodings_take_base_encodings_differences_and_symbol_tables()
    {
        var pdf = RawPdf.Page(
            "BT /F1 12 Tf 10 100 Td (\u008EA) Tj /F2 12 Tf (a) Tj ET",
            "/Font << /F1 5 0 R /F2 6 0 R >>",
            string.Empty,
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding << /BaseEncoding /MacRomanEncoding /Differences [65 /eacute] >> >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Symbol >>");

        Assert.Equal("ééα", PdfDocument.Open(pdf).GetPage(1).Text);
    }

    [Fact]
    public void Cyrillic_afii_glyph_names_map_to_unicode()
    {
        var pdf = RawPdf.Page(
            "BT /F1 12 Tf 10 100 Td (ABCDEF) Tj ET",
            "/Font << /F1 5 0 R >>",
            string.Empty,
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding << /Differences [65 /afii10017 /afii10023 /afii10049 /afii10065 /afii10071 /afii10097] >> >>");

        Assert.Equal("АЁЯаёя", PdfDocument.Open(pdf).GetPage(1).Text);
    }

    [Fact]
    public void Indexed_images_look_colours_up_in_a_string_or_a_stream()
    {
        var pdf = RawPdf.Page(
            "q 100 0 0 50 50 120 cm /Im1 Do Q q 100 0 0 50 50 30 cm /Im2 Do Q",
            "/XObject << /Im1 5 0 R /Im2 6 0 R >>",
            string.Empty,
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 2 /Height 1 /BitsPerComponent 8 /ColorSpace [/Indexed /DeviceRGB 1 <FF00000000FF>]", [0, 1]),
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 2 /Height 1 /BitsPerComponent 1 /ColorSpace [/Indexed /DeviceGray 1 7 0 R]", [0b0100_0000]),
            RawPdf.Stream(string.Empty, [0x00, 0xFF]));

        var image = Render(pdf);

        Assert.Equal((255, 0, 0, 255), image.GetRgba(70, 55));
        Assert.Equal((0, 0, 255, 255), image.GetRgba(130, 55));
        Assert.Equal((0, 0, 0, 255), image.GetRgba(70, 145));
        Assert.Equal((255, 255, 255, 255), image.GetRgba(130, 145));
    }

    [Fact]
    public void Paints_axial_and_radial_shadings()
    {
        var axial = "<< /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 200 0] /Extend [true true] /Function << /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 1] /N 1 >> >>";
        var radial = "<< /PatternType 2 /Shading << /ShadingType 3 /ColorSpace /DeviceRGB /Coords [100 100 0 100 100 100] /Function << /FunctionType 3 /Domain [0 1] /Bounds [0.5] /Encode [0 1 0 1] /Functions [<< /FunctionType 2 /Domain [0 1] /C0 [1 1 1] /C1 [1 0 0] /N 1 >> << /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 0] /N 1 >>] >> >> >>";
        var sampled = "<< /ShadingType 2 /ColorSpace /DeviceGray /Coords [0 0 0 200] /Function 6 0 R >>";
        var pdf = RawPdf.Page(
            "q 0 150 200 50 re W n /Sh1 sh Q /Pattern cs /P1 scn 0 0 200 150 re f q 150 0 50 50 re W n /Sh2 sh Q /Sh3 sh /Pattern cs /P2 scn 0 0 1 1 re f",
            "/Shading << /Sh1 " + axial + " /Sh2 " + sampled + " /Sh3 << /ShadingType 4 >> >> /Pattern << /P1 " + radial + " /P2 << /PatternType 1 >> >>",
            string.Empty,
            "<< >>",
            RawPdf.Stream("/FunctionType 0 /Domain [0 1] /Range [0 1] /Size [2] /BitsPerSample 8", [0, 255]));

        var rendering = PdfRenderer.Render(PdfDocument.Open(pdf).GetPage(1), new PdfRenderOptions { Dpi = 72 });
        var image = rendering.Image;

        Assert.InRange(image.GetRgba(2, 25).R, 245, 255);
        Assert.InRange(image.GetRgba(197, 25).B, 245, 255);
        Assert.InRange(image.GetRgba(100, 25).R, 115, 140);
        Assert.InRange(image.GetRgba(100, 100).G, 245, 255);
        Assert.InRange(image.GetRgba(150, 100).G, 0, 10);
        Assert.InRange(image.GetRgba(150, 100).R, 240, 255);
        Assert.InRange(image.GetRgba(197, 100).R, 0, 20);
        Assert.InRange(image.GetRgba(175, 175).R, 30, 70);
        Assert.Equal(["patterns that cannot be read are drawn as a flat colour", "shadings of this kind are not drawn"], rendering.Gaps);
    }

    [Fact]
    public void Quadratic_contours_may_start_off_the_curve()
    {
        var outline = new GlyphOutline([[new GlyphPoint(0, 0, false), new GlyphPoint(100, 0, false), new GlyphPoint(100, 100, false), new GlyphPoint(0, 100, false)]]);

        var shape = GlyphShape.FromQuadratic(outline, 0.01);

        Assert.Equal('M', shape.Commands[0].Op);
        Assert.Equal((0.5, 0), (shape.Commands[0].X1, shape.Commands[0].Y1));
        Assert.Equal(4, shape.Commands.Count(c => c.Op == 'C'));
        Assert.Equal('Z', shape.Commands[^1].Op);
    }

    private static RasterImage SimpleFont(byte[] program, string dictionary, string shown) => Render(RawPdf.Page(
        $"BT /F1 100 Tf 20 20 Td ({shown}) Tj ET",
        "/Font << /F1 5 0 R >>",
        string.Empty,
        "<< /Type /Font /Subtype /Type1 /BaseFont /Test /FirstChar 65 /LastChar 72 /Widths [700 700 700 700 700 700 700 700] /FontDescriptor 6 0 R >>",
        Descriptor,
        RawPdf.Stream(dictionary, program)));

    private static void AssertSquare(RasterImage image)
    {
        Assert.Equal((0, 0, 0, 255), image.GetRgba(55, 150));
        Assert.Equal((255, 255, 255, 255), image.GetRgba(25, 150));
        Assert.Equal((255, 255, 255, 255), image.GetRgba(55, 115));
    }

    private static RasterImage Render(byte[] pdf) => PdfRenderer.Render(PdfDocument.Open(pdf).GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image;
}
