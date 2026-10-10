// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>
/// Tiling and shading patterns (ISO 32000-1 §8.7) on a 200 x 200 page rendered at 72 dpi: one pixel per point, image
/// y = 200 - PDF y. A cell point q of pattern space shows at p = q + (i XStep, j YStep), placed by the pattern matrix
/// in the default space of the content that uses the pattern.
/// </summary>
public sealed class PdfPatternTests
{
    // A 20 x 20 cell: red in its lower left quarter, blue in its upper right one, nothing elsewhere.
    private const string Checker = "1 0 0 rg 0 0 10 10 re f 0 0 1 rg 10 10 10 10 re f";

    [Theory]
    [InlineData(5, 5, 255, 0, 0)]
    [InlineData(15, 15, 0, 0, 255)]
    [InlineData(15, 5, 255, 255, 255)]
    [InlineData(25, 5, 255, 0, 0)]
    [InlineData(195, 195, 0, 0, 255)]
    [InlineData(105, 125, 255, 0, 0)]
    public void Coloured_tiling_patterns_repeat_their_cell(int x, int y, int r, int g, int b)
    {
        var image = Render("/Pattern cs /P1 scn 0 0 200 200 re f", "/Pattern << /P1 5 0 R >>", string.Empty, Tiling(string.Empty, Checker));

        Assert.Equal((r, g, b), Pixel(image, x, 200 - y));
    }

    [Theory]
    [InlineData(7, 5, 255, 0, 0)] // q = 2
    [InlineData(3, 5, 255, 255, 255)] // q = -2 + 20 = 18, outside the red quarter
    [InlineData(30, 70, 0, 0, 255)] // scaled by 2: q = ((30.5 - 5) / 2, 69.5 / 2 - 20) = (12.75, 14.75)
    public void The_pattern_matrix_places_the_lattice(int x, int y, int r, int g, int b)
    {
        // Matrix [1 0 0 1 5 0] shifts the lattice by 5 for the first fill; [2 0 0 2 5 0] doubles the cell for the
        // second, above y 50.
        var image = Render(
            "/Pattern cs /P1 scn 0 0 200 50 re f /P2 scn 0 50 200 150 re f",
            "/Pattern << /P1 5 0 R /P2 6 0 R >>",
            string.Empty,
            Tiling("/Matrix [1 0 0 1 5 0]", Checker),
            Tiling("/Matrix [2 0 0 2 5 0]", Checker));

        Assert.Equal((r, g, b), Pixel(image, x, 200 - y));
    }

    [Fact]
    public void Uncoloured_patterns_paint_the_colour_given_with_the_name()
    {
        // PaintType 2: the cell's own colour operators are ignored, the green of scn paints it (§8.7.3.3); the
        // stroke takes the same pattern in blue.
        var image = Render(
            "/Cs1 cs 0 1 0 /P1 scn 0 0 200 100 re f /Cs1 CS 0 0 1 /P1 SCN 20 w 0 150 m 200 150 l S",
            "/Pattern << /P1 5 0 R >> /ColorSpace << /Cs1 [/Pattern /DeviceRGB] >>",
            string.Empty,
            Tiling("/PaintType 2", "1 0 0 rg 0.5 g 0 0 1 RG [/Pattern] cs /Missing scn 0 0 10 10 re f"));

        Assert.Equal((0, 255, 0), Pixel(image, 5, 195));
        Assert.Equal((255, 255, 255), Pixel(image, 15, 195));
        Assert.Equal((0, 0, 255), Pixel(image, 5, 55));
        Assert.Equal((255, 255, 255), Pixel(image, 5, 35));
    }

    [Fact]
    public void Cells_larger_than_the_steps_overlap()
    {
        // A 30 wide cell every 20: its blue strip [20, 30) also covers [0, 10) of the next cell, so blue shows at x
        // modulo 20 below 10, through the empty part of the cell drawn after it.
        var image = Render("/Pattern cs /P1 scn 0 0 200 200 re f", "/Pattern << /P1 5 0 R >>", string.Empty,
            Tiling("/BBox [0 0 30 10] /XStep 20 /YStep 10", "0 0 1 rg 20 0 10 10 re f"));

        Assert.Equal((0, 0, 255), Pixel(image, 5, 195));
        Assert.Equal((0, 0, 255), Pixel(image, 45, 195));
        Assert.Equal((255, 255, 255), Pixel(image, 15, 195));
    }

    [Fact]
    public void Patterns_fill_text_take_the_alpha_and_use_the_space_of_their_form()
    {
        // At ca 0.4 red over white is (255, 153, 153). In the form translated by 5, the cell origin follows the form:
        // x = 3 is q = -2, empty, where the page lattice would give red; x = 7 is q = 2, red (ca 0.4 still applies).
        var image = Render(
            "/G1 gs /Pattern cs /P1 scn 0 0 200 50 re f /Fm1 Do",
            "/Pattern << /P1 5 0 R >> /ExtGState << /G1 << /ca 0.4 >> >> /XObject << /Fm1 6 0 R >>",
            string.Empty,
            Tiling(string.Empty, Checker),
            RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [-5 0 195 200] /Matrix [1 0 0 1 5 0] /Resources << /Pattern << /P1 5 0 R >> >>", RawPdf.Ascii("/Pattern cs /P1 scn -5 100 200 100 re f")));

        Assert.Equal((255, 153, 153), Pixel(image, 5, 195));
        Assert.Equal((255, 255, 255), Pixel(image, 3, 95));
        Assert.Equal((255, 153, 153), Pixel(image, 7, 95));
    }

    [Fact]
    public void Text_and_stencil_masks_are_painted_with_the_pattern()
    {
        // A cell all green: a Type 3 glyph square and a stencil mask take it.
        var image = Render(
            "/Pattern cs /P1 scn BT /F1 50 Tf 20 120 Td (A) Tj ET q 50 0 0 50 120 20 cm BI /W 1 /H 1 /IM true ID \u0000 EI Q",
            "/Pattern << /P1 5 0 R >> /Font << /F1 6 0 R >>",
            string.Empty,
            Tiling(string.Empty, "0 1 0 rg 0 0 20 20 re f"),
            "<< /Type /Font /Subtype /Type3 /FontMatrix [0.001 0 0 0.001 0 0] /FontBBox [0 0 1000 1000] /CharProcs << /sq 7 0 R >> /Encoding << /Type /Encoding /Differences [65 /sq] >> /FirstChar 65 /LastChar 65 /Widths [1000] >>",
            RawPdf.Stream(string.Empty, RawPdf.Ascii("1000 0 0 0 1000 1000 d1 0 0 1000 1000 re f")));

        Assert.Equal((0, 255, 0), Pixel(image, 45, 55));
        Assert.Equal((0, 255, 0), Pixel(image, 145, 155));
        Assert.Equal((255, 255, 255), Pixel(image, 100, 100));
    }

    [Fact]
    public void Shading_patterns_stroke_and_fill_their_background()
    {
        // Red to blue from x 0 to 100, not extended: beyond, the Background green fills the pattern; the stroke at
        // y 150 takes the same shading pattern, red at its start. The sh operator ignores the Background.
        var shading = "<< /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 100 0] /Background [0 1 0] /Function << /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 1] /N 1 >> >>";
        var image = Render(
            $"/Pattern cs /P1 scn 0 0 200 50 re f /Pattern CS /P1 SCN 10 w 0 150 m 200 150 l S q 0 60 200 20 re W n /Sh1 sh Q",
            $"/Pattern << /P1 << /PatternType 2 /Shading {shading} >> >> /Shading << /Sh1 {shading} >>",
            string.Empty);

        Assert.Equal((0, 255, 0), Pixel(image, 150, 175));
        Assert.Equal((0, 255, 0), Pixel(image, 150, 50));
        Assert.Equal((254, 0, 1), Pixel(image, 0, 50), new NearColour(3));
        Assert.Equal((255, 255, 255), Pixel(image, 150, 130));
    }

    [Fact]
    public void Patterns_that_cannot_be_read_keep_the_colour_and_are_reported()
    {
        var rendering = PdfRenderer.Render(PdfDocument.Open(RawPdf.Page(
            "1 0 0 rg /Pattern cs /P1 scn /P2 scn /P3 scn /P4 scn 0 0 200 200 re f",
            "/Pattern << /P2 << /PatternType 1 >> /P3 5 0 R /P4 6 0 R >>",
            string.Empty,
            Tiling("/XStep 0", Checker),
            Tiling("/PaintType 3", Checker))).GetPage(1), new PdfRenderOptions { Dpi = 72 });

        Assert.Equal(["patterns that cannot be read are drawn as a flat colour"], rendering.Gaps);
        Assert.Equal((0, 0, 0), Pixel(rendering.Image, 100, 100));
    }

    [Fact]
    public void A_pattern_drawing_itself_stops_and_huge_cells_are_drawn_at_a_lower_resolution()
    {
        // P1's cell uses P1: the inner use is refused and keeps the flat colour (black after cs). P2's cell of 4000 x
        // 4000 points is drawn on a raster of at most a million pixels, still green.
        var image = Render(
            "/Pattern cs /P1 scn 0 0 200 100 re f /P2 scn 0 100 200 100 re f",
            "/Pattern << /P1 5 0 R /P2 6 0 R >>",
            string.Empty,
            Tiling("/Resources << /Pattern << /P1 5 0 R >> >>", "1 0 0 rg 0 0 20 20 re f /Pattern cs /P1 scn 0 0 10 10 re f"),
            Tiling("/BBox [0 0 4000 4000] /XStep 4000 /YStep 4000", "0 1 0 rg 0 0 4000 4000 re f"));

        Assert.Equal((255, 0, 0), Pixel(image, 15, 195));
        Assert.Equal((0, 0, 0), Pixel(image, 5, 195));
        Assert.Equal((0, 255, 0), Pixel(image, 100, 50));
    }

    [Fact]
    public void Degenerate_pattern_matrices_draw_nothing_and_cells_are_reused()
    {
        // A zero matrix cannot be inverted: neither the shading nor the tiling pattern gives a colour. The second fill
        // with P3 reuses the cell drawn for the first.
        var image = Render(
            "/Pattern cs /P1 scn 0 0 100 100 re f /P2 scn 100 0 100 100 re f /P3 scn 0 100 100 100 re f /P3 scn 100 100 100 100 re f",
            "/Pattern << /P1 << /PatternType 2 /Matrix [0 0 0 0 0 0] /Shading << /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 1 0] /Extend [true true] /Function << /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [1 0 0] /N 1 >> >> >> /P2 5 0 R /P3 6 0 R >>",
            string.Empty,
            Tiling("/Matrix [0 0 0 0 0 0]", Checker),
            Tiling(string.Empty, "0 1 0 rg 0 0 20 20 re f"));

        Assert.Equal((255, 255, 255), Pixel(image, 50, 150));
        Assert.Equal((255, 255, 255), Pixel(image, 150, 150));
        Assert.Equal((0, 255, 0), Pixel(image, 50, 50));
        Assert.Equal((0, 255, 0), Pixel(image, 150, 50));
    }

    private static byte[] Tiling(string extra, string content)
    {
        var dictionary = "/PatternType 1 /PaintType 1 /TilingType 1 /BBox [0 0 20 20] /XStep 20 /YStep 20 /Resources << >> " + extra;
        return RawPdf.Stream(dictionary, RawPdf.Ascii(content));
    }

    private static RasterImage Render(string content, string resources = "", string pageExtra = "", params object[] objects) =>
        PdfRenderer.Render(PdfDocument.Open(RawPdf.Page(content, resources, pageExtra, objects)).GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image;

    private static (int R, int G, int B) Pixel(RasterImage image, int x, int y)
    {
        var (r, g, b, _) = image.GetRgba(x, y);
        return (r, g, b);
    }

    private sealed class NearColour(int tolerance) : IEqualityComparer<(int R, int G, int B)>
    {
        public bool Equals((int R, int G, int B) x, (int R, int G, int B) y) =>
            Math.Abs(x.R - y.R) <= tolerance && Math.Abs(x.G - y.G) <= tolerance && Math.Abs(x.B - y.B) <= tolerance;

        public int GetHashCode((int R, int G, int B) obj) => 0;
    }
}
