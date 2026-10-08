// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>
/// Content stream painting (ISO 32000-1 §8, §9.4, §8.7.4.5) on a 200 x 200 page rendered at 72 dpi: one pixel
/// per point, image y = 200 - PDF y. Expected pixels follow from the operators' definitions.
/// </summary>
public sealed class PdfRenderingDetailTests
{
    private const string Helvetica = "/Font << /F1 5 0 R >>";
    private const string HelveticaObject = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>";

    [Fact]
    public void Curves_of_every_form_close_a_filled_shape()
    {
        // c with two control points, v (first control at the current point), y (second control at the end).
        var image = Render("0 0 1 rg 20 100 m 20 180 180 180 180 100 c 180 20 100 20 v 20 20 20 100 y f");

        Assert.Equal((0, 0, 255), Pixel(image, 100, 100));
        Assert.Equal((0, 0, 255), Pixel(image, 100, 50));
        Assert.Equal((255, 255, 255), Pixel(image, 100, 30));
    }

    [Fact]
    public void Line_caps_decide_dots_and_line_ends()
    {
        // Round caps draw a zero-length line as a dot and extend a line by half its width; butt caps do neither.
        var image = Render("0 g 10 w 1 J 50 100 m 50 100 l S 50 50 m 150 50 l S 0 J 150 100 m 150 100 l S 50 150 m 150 150 l S");

        Assert.Equal((0, 0, 0), Pixel(image, 50, 100));
        Assert.Equal((255, 255, 255), Pixel(image, 150, 100));
        Assert.Equal((0, 0, 0), Pixel(image, 46, 150));
        Assert.Equal((255, 255, 255), Pixel(image, 46, 50));
    }

    [Fact]
    public void A_dash_phase_starts_inside_the_pattern()
    {
        // [10 10] with phase 15: the line starts 5 units into a gap, so it is off for 5, on for 10, off for 10.
        var image = Render("0 g 4 w [10 10] 15 d 0 100 m 200 100 l S");

        Assert.Equal([255, 0, 255, 0], new[] { 2, 10, 20, 28 }.Select(x => Pixel(image, x, 100).R));
    }

    [Fact]
    public void Cmyk_fills_convert_to_rgb()
    {
        Assert.Equal((255, 0, 0), Pixel(Render("0 1 1 0 k 0 0 100 100 re f"), 50, 150));
    }

    [Theory]
    [InlineData("BT /F1 20 Tf 10 150 Td 0 -30 TD (A) Tj T* (B) Tj ET", "BT /F1 20 Tf 10 150 Td 0 -30 Td (A) Tj 0 -30 Td (B) Tj ET")]
    [InlineData("BT /F1 20 Tf 30 TL 10 120 Td (A) ' ET", "BT /F1 20 Tf 10 90 Td (A) Tj ET")]
    [InlineData("BT /F1 20 Tf 30 TL 10 120 Td 0 5 (AB) \" ET", "BT /F1 20 Tf 5 Tc 10 90 Td (AB) Tj ET")]
    [InlineData("BT /F1 20 Tf 10 90 Td (A) Tj (B) \" ET", "BT /F1 20 Tf 10 90 Td (A) Tj ET")]
    public void Text_positioning_operators_match_their_definitions(string content, string equivalent)
    {
        Assert.Equal(RenderText(equivalent).Pixels, RenderText(content).Pixels);
    }

    [Fact]
    public void Text_without_a_usable_font_is_not_drawn()
    {
        var image = RenderText("BT 10 100 Td (A) Tj /F9 20 Tf (B) Tj ET");

        Assert.All(image.Pixels, p => Assert.Equal(255, p));
    }

    [Fact]
    public void Forms_are_drawn_by_Do_and_a_form_drawing_itself_stops()
    {
        // The form fills its 50 x 50 box, scaled twice by its matrix, then calls itself.
        var image = Render(
            "/Fm1 Do /Missing Do /GSX gs cs",
            "/XObject << /Fm1 5 0 R >>",
            string.Empty,
            RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 50 50] /Matrix [2 0 0 2 0 0] /Resources << /XObject << /Fm1 5 0 R >> >>", RawPdf.Ascii("1 0 0 rg 0 0 50 50 re f /Fm1 Do")));

        Assert.Equal((255, 0, 0), Pixel(image, 50, 150));
        Assert.Equal((255, 255, 255), Pixel(image, 150, 50));
    }

    [Theory]
    [InlineData(180, 175, 25)]
    [InlineData(270, 175, 175)]
    public void Page_rotation_turns_the_page_clockwise(int rotation, int x, int y)
    {
        // A red square at the bottom left corner of the page.
        var page = RawPdf.Page("1 0 0 rg 0 0 50 50 re f", string.Empty, $"/Rotate {rotation}");

        var image = PdfRenderer.Render(PdfDocument.Open(page).GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image;

        Assert.Equal((255, 0, 0), Pixel(image, x, y));
    }

    [Fact]
    public void Resolutions_too_large_are_refused()
    {
        var page = PdfDocument.Open(RawPdf.Page(string.Empty, string.Empty, "/MediaBox [0 0 14400 14400]")).GetPage(1);

        Assert.Throws<ArgumentException>(() => PdfRenderer.Render(page, new PdfRenderOptions { Dpi = 1_000_000 }));
    }

    [Fact]
    public void Images_and_shadings_that_cannot_be_drawn_are_reported()
    {
        var rendering = PdfRenderer.Render(
            PdfDocument.Open(RawPdf.Page(
                "q 50 0 0 50 0 0 cm /Im1 Do Q /Sh1 sh",
                "/XObject << /Im1 5 0 R >> /Shading << /Sh1 << /ShadingType 4 /ColorSpace /DeviceRGB >> >>",
                string.Empty,
                RawPdf.Stream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /JPXDecode", [1, 2, 3]))).GetPage(1),
            new PdfRenderOptions { Dpi = 72 });

        Assert.Contains("images in an unsupported format are not drawn", rendering.Gaps);
        Assert.Contains("shadings of this kind are not drawn", rendering.Gaps);
    }

    [Theory]
    [InlineData("[80 100 10 120 100 50]", "[true false]", 80, 255, 0, 0)]
    [InlineData("[80 100 10 120 100 50]", "[false false]", 80, 255, 255, 255)]
    [InlineData("[80 100 10 120 100 50]", "[false false]", 110, 190, 0, 65)]
    [InlineData("[80 100 10 120 100 60]", "[true false]", 110, 198, 0, 57)]
    [InlineData("[80 100 10 120 100 60]", "[true false]", 60, 13, 0, 242)]
    [InlineData("[80 100 10 120 100 60]", "[true false]", 190, 255, 255, 255)]
    public void Radial_shadings_take_the_largest_circle_through_each_point(string coords, string extend, int x, int r, int g, int b)
    {
        // Circles (80, 100, 10) and (120, 100, 50) touch inside: the quadratic term vanishes, so s = c / 2b; at the
        // centre of pixel (110, 100), s = 830.5 / 3240 = 0.256 (red to blue); at (80, 100), s = -0.12 with a positive
        // radius, painted only when the start is extended. With (120, 100, 60), (110, 100) lies on the circle s = 0.22,
        // the centre of pixel (60, 100) on s = 0.95, (190, 100) beyond s = 1. An independent PDF renderer gives the same colours.
        var image = Render(
            "/Sh1 sh",
            $"/Shading << /Sh1 << /ShadingType 3 /ColorSpace /DeviceRGB /Coords {coords} /Extend {extend} /Function << /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 1] /N 1 >> >> >>");

        Assert.Equal((r, g, b), Pixel(image, x, 100), new NearColour(2));
    }

    [Fact]
    public void Shading_functions_that_cannot_be_read_are_reported()
    {
        var stitchedBadly = "<< /FunctionType 3 /Domain [0 1] /Functions [<< /FunctionType 9 >>] /Bounds [] /Encode [0 1] >>";
        var sampledTwoInputs = "6 0 R";
        var rendering = PdfRenderer.Render(
            PdfDocument.Open(RawPdf.Page(
                "/Sh1 sh /Sh2 sh",
                $"/Shading << /Sh1 << /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 200 0] /Function {stitchedBadly} >> /Sh2 << /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 200 0] /Function {sampledTwoInputs} >> >>",
                string.Empty,
                "0",
                RawPdf.Stream("/FunctionType 0 /Domain [0 1 0 1] /Range [0 1 0 1 0 1] /Size [2 2] /BitsPerSample 8", new byte[12]))).GetPage(1),
            new PdfRenderOptions { Dpi = 72 });

        Assert.Contains("shadings of this kind are not drawn", rendering.Gaps);
        Assert.All(rendering.Image.Pixels, p => Assert.Equal(255, p));
    }

    [Fact]
    public void A_sampled_function_larger_than_its_stream_is_not_read()
    {
        // /Size [2147483647] over twelve bytes would allocate two billion sample rows before reading them.
        var rendering = PdfRenderer.Render(
            PdfDocument.Open(RawPdf.Page(
                "/Sh1 sh",
                "/Shading << /Sh1 << /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 200 0] /Function 6 0 R >> >>",
                string.Empty,
                "0",
                RawPdf.Stream("/FunctionType 0 /Domain [0 1] /Range [0 1 0 1 0 1] /Size [2147483647] /BitsPerSample 8", new byte[12]))).GetPage(1),
            new PdfRenderOptions { Dpi = 72 });

        Assert.Contains("shadings of this kind are not drawn", rendering.Gaps);
    }

    [Fact]
    public void Annotation_appearances_follow_their_state_and_skip_empty_boxes()
    {
        // /AS /On picks the blue appearance; the second annotation's form has an empty box.
        var image = Render(
            string.Empty,
            string.Empty,
            "/Annots [5 0 R 8 0 R]",
            "<< /Type /Annot /Subtype /Widget /Rect [0 0 100 100] /AS /On /AP << /N << /On 6 0 R /Off 7 0 R >> >> >>",
            RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 10 10]", RawPdf.Ascii("0 0 1 rg 0 0 10 10 re f")),
            RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 10 10]", RawPdf.Ascii("1 0 0 rg 0 0 10 10 re f")),
            "<< /Type /Annot /Subtype /Stamp /Rect [100 100 200 200] /AP << /N 9 0 R >> >>",
            RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 0 0]", RawPdf.Ascii("1 0 0 rg 0 0 10 10 re f")));

        Assert.Equal((0, 0, 255), Pixel(image, 50, 150));
        Assert.Equal((255, 255, 255), Pixel(image, 150, 50));
    }

    private static RasterImage RenderText(string content) => Render(content, Helvetica, string.Empty, HelveticaObject);

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
