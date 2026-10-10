// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>
/// Blend modes, alpha constants, transparency groups and soft masks (ISO 32000-1 §11) on a 200 x 200 page rendered
/// at 72 dpi (image y = 200 - PDF y). Each expected colour is computed by hand from the formulas of the
/// specification, components on 0..1 and the result times 255, rounded.
/// </summary>
public sealed class PdfTransparencyTests
{
    // Backdrop Cb = (0.8, 0.4, 0.2) = (204, 102, 51), source Cs = (0.2, 0.6, 0.4) = (51, 153, 102), both opaque, so
    // the page shows B(Cb, Cs) (§11.3.5). Lum(Cb) = 0.498, Lum(Cs) = 0.458, Sat(Cb) = 0.6, Sat(Cs) = 0.4.
    [Theory]
    [InlineData("Normal", 51, 153, 102)]
    [InlineData("Compatible", 51, 153, 102)]
    [InlineData("Multiply", 41, 61, 20)] // 0.16 0.24 0.08
    [InlineData("Screen", 214, 194, 133)] // cb + cs - cb cs: 0.84 0.76 0.52
    [InlineData("Overlay", 173, 122, 41)] // HardLight(cs, cb): Screen(0.2, 0.6) = 0.68, 0.6 x 0.8 = 0.48, 0.4 x 0.4 = 0.16
    [InlineData("Darken", 51, 102, 51)]
    [InlineData("Lighten", 204, 153, 102)]
    [InlineData("ColorDodge", 255, 255, 85)] // min(1, cb / (1 - cs)): 1, 1, 0.2 / 0.6
    [InlineData("ColorBurn", 0, 0, 0)] // 1 - min(1, (1 - cb) / cs): 1 - 1, 1 - 1, 1 - min(1, 2)
    [InlineData("HardLight", 82, 133, 41)] // 0.8 x 0.4 = 0.32, Screen(0.4, 0.2) = 0.52, 0.2 x 0.8 = 0.16
    [InlineData("SoftLight", 180, 114, 43)] // 0.8 - 0.6 x 0.8 x 0.2 = 0.704, 0.4 + 0.2 (sqrt 0.4 - 0.4) = 0.4465, 0.2 - 0.2 x 0.2 x 0.8 = 0.168
    [InlineData("Difference", 153, 51, 51)]
    [InlineData("Exclusion", 173, 133, 112)] // cb + cs - 2 cb cs: 0.68 0.52 0.44
    [InlineData("Hue", 28, 181, 105)] // SetSat(Cs, 0.6) = (0, 0.6, 0.3), lum 0.387, + 0.111
    [InlineData("Saturation", 178, 110, 76)] // SetSat(Cb, 0.4) = (0.4, 0.1333, 0), lum 0.1987, + 0.2993
    [InlineData("Color", 61, 163, 112)] // Cs + (0.498 - 0.458)
    [InlineData("Luminosity", 194, 92, 41)] // Cb + (0.458 - 0.498)
    [InlineData("Unknown", 51, 153, 102)] // an unknown mode is Normal
    public void Every_blend_mode_combines_backdrop_and_source(string mode, int r, int g, int b)
    {
        var image = Render(Square("0.8 0.4 0.2", "0.2 0.6 0.4"), $"/ExtGState << /G1 << /BM /{mode} >> >>");

        Assert.Equal((r, g, b), Pixel(image, 100, 100));
        Assert.Equal((204, 102, 51), Pixel(image, 10, 10));
    }

    [Theory]
    [InlineData("ColorDodge", "1 0.2 0", "0 1 1", 255, 255, 255)] // min(1, 1 / 1); cs = 1 gives 1, also over cb = 0
    [InlineData("ColorBurn", "1 0.2 0", "0 1 1", 0, 51, 0)] // cs = 0 gives 0, also under cb = 1; 1 - 0.8; 1 - min(1, 1 / 1)
    [InlineData("Color", "0.2 0.2 0.2", "1 0 0", 170, 0, 0)] // SetLum((1, 0, 0), 0.2) = (0.9, -0.1, -0.1), clipped: 0.2 + (c - 0.2) 0.2 / 0.3
    [InlineData("Luminosity", "1 0 0", "0.8 0.8 0.8", 255, 182, 182)] // SetLum((1, 0, 0), 0.8) = (1.5, 0.5, 0.5), clipped: 0.8 + (c - 0.8) 0.2 / 0.7
    [InlineData("Hue", "0.8 0.4 0.2", "0.5 0.5 0.5", 127, 127, 127)] // a grey source has no saturation: SetSat gives 0, then Lum(Cb) = 0.498
    public void Blend_modes_handle_their_end_cases(string mode, string backdrop, string source, int r, int g, int b)
    {
        var image = Render(Square(backdrop, source), $"/ExtGState << /G1 << /BM [/Unknown /{mode}] >> >>");

        Assert.Equal((r, g, b), Pixel(image, 100, 100));
    }

    [Fact]
    public void Alpha_constants_weigh_the_blended_colour()
    {
        // (1 - 0.4) Cb + 0.4 B(Cb, Cs) for Multiply: 0.6 x 204 + 0.4 x 40.8 = 138.72, 0.6 x 102 + 0.4 x 61.2 = 85.68,
        // 0.6 x 51 + 0.4 x 20.4 = 38.76. The stroke takes CA: black multiplied at 0.5 halves the backdrop (25.5 to even).
        var image = Render(Square("0.8 0.4 0.2", "0.2 0.6 0.4") + " 0 G 10 w 0 20 m 200 20 l S", "/ExtGState << /G1 << /BM /Multiply /ca 0.4 /CA 0.5 >> >>");

        Assert.Equal((139, 86, 39), Pixel(image, 100, 100));
        Assert.Equal((102, 51, 26), Pixel(image, 10, 180));
    }

    [Theory]
    [InlineData("/I false", 41, 61, 20)]
    [InlineData("/I true", 51, 153, 102)]
    public void A_non_isolated_group_blends_with_its_backdrop_and_an_isolated_one_does_not(string isolated, int r, int g, int b)
    {
        // Inside the group the source multiplies its backdrop: the page colour for a non-isolated group, nothing for an
        // isolated one, whose group then holds Cs with alpha 1 and is composited normally (§11.4.7).
        var image = Render(
            "0.8 0.4 0.2 rg 0 0 200 200 re f /Fm1 Do",
            "/XObject << /Fm1 5 0 R >>",
            string.Empty,
            Group($"/Group << /S /Transparency {isolated} >> /Resources << /ExtGState << /G1 << /BM /Multiply >> >> >>", "/G1 gs 0.2 0.6 0.4 rg 50 50 100 100 re f"));

        Assert.Equal((r, g, b), Pixel(image, 100, 100));
        Assert.Equal((204, 102, 51), Pixel(image, 10, 10));
    }

    [Fact]
    public void A_group_is_composited_once_with_the_alpha_of_Do()
    {
        // Red then blue, opaque inside the group, the group drawn with ca 0.4: blue alone shows where they overlap,
        // 0.6 x 255 = 153 of white under it. Drawn without a group, each square takes ca 0.4 and red shows through.
        var squares = "1 0 0 rg 20 20 100 100 re f 0 0 1 rg 80 80 100 100 re f";
        var grouped = Render(
            "/G1 gs /Fm1 Do",
            "/XObject << /Fm1 5 0 R >> /ExtGState << /G1 << /ca 0.4 >> >>",
            string.Empty,
            Group("/Group << /S /Transparency >>", squares));
        var flat = Render("/G1 gs " + squares, "/ExtGState << /G1 << /ca 0.4 >> >>");

        Assert.Equal((153, 153, 255), Pixel(grouped, 100, 100));
        Assert.Equal((255, 153, 153), Pixel(grouped, 40, 160));
        Assert.Equal((153, 92, 194), Pixel(flat, 100, 100));
    }

    [Theory]
    [InlineData("/K true", false, 153, 153, 255)]
    [InlineData("/K false", false, 153, 92, 194)]
    [InlineData("/K true", true, 153, 0, 102)]
    public void Knockout_groups_composite_each_object_with_the_initial_backdrop(string knockout, bool alphaIsShape, int r, int g, int b)
    {
        // Red then blue at 0.4 in an isolated group. Knocked out, blue meets the transparent initial backdrop: alpha
        // 0.4, blue (§11.4.6). Not knocked out, ar = 0.64 and C = (0.24 red + 0.4 blue) / 0.64, which on white gives
        // the same pixel as drawing without a group. With AIS the blue square's 0.4 is shape: 0.6 red + 0.4 blue at
        // alpha 1 (red drawn at alpha 1 there).
        var red = alphaIsShape ? "1 0 0 rg 20 20 100 100 re f" : "/G1 gs 1 0 0 rg 20 20 100 100 re f";
        var image = Render(
            "/Fm1 Do",
            "/XObject << /Fm1 5 0 R >>",
            string.Empty,
            Group(
                $"/Group << /S /Transparency /I true {knockout} >> /Resources << /ExtGState << /G1 << /ca 0.4 /AIS {(alphaIsShape ? "true" : "false")} >> >> >>",
                red + " /G1 gs 0 0 1 rg 80 80 100 100 re f"));

        Assert.Equal((r, g, b), Pixel(image, 100, 100));
    }

    [Fact]
    public void Luminosity_masks_use_the_group_luminosity_and_the_backdrop_outside_it()
    {
        // The mask group paints white over its 100 x 100 box; BC 0.4 grey gives 0.4 elsewhere, inside the box and out
        // (§11.5.2). Red then shows fully in the box and at 0.4 over white elsewhere: 255 - 0.4 x 255 = 153.
        var image = Render(
            "/G1 gs 1 0 0 rg 0 0 200 200 re f",
            "/ExtGState << /G1 << /SMask << /S /Luminosity /G 5 0 R /BC [0.4] >> >> >>",
            string.Empty,
            Group("/BBox [0 0 100 100] /Group << /S /Transparency /CS /DeviceGray >>", "1 g 0 0 50 100 re f 0.6 g 50 0 50 100 re f", box: false));

        Assert.Equal((255, 0, 0), Pixel(image, 25, 150));
        Assert.Equal((255, 102, 102), Pixel(image, 75, 150)); // 0.6 grey: 255 - 0.6 x 255
        Assert.Equal((255, 153, 153), Pixel(image, 150, 150));
        Assert.Equal((255, 153, 153), Pixel(image, 150, 50));
    }

    [Fact]
    public void Transfer_functions_map_the_mask_values()
    {
        // TR inverts: the white half gives 0, BC 0.4 gives 0.6, red at 0.6 over white is 255 - 153 = 102.
        var image = Render(
            "/G1 gs 1 0 0 rg 0 0 200 200 re f",
            "/ExtGState << /G1 << /SMask << /S /Luminosity /G 5 0 R /BC [0.4] /TR << /FunctionType 2 /Domain [0 1] /C0 [1] /C1 [0] /N 1 >> >> >> >>",
            string.Empty,
            Group("/BBox [0 0 100 200] /Group << /S /Transparency /CS /DeviceGray >>", "1 g 0 0 100 200 re f", box: false));

        Assert.Equal((255, 255, 255), Pixel(image, 50, 100));
        Assert.Equal((255, 102, 102), Pixel(image, 150, 100));
    }

    [Fact]
    public void Alpha_masks_use_the_group_alpha_and_none_ends_the_mask()
    {
        // The group fills its box at ca 0.6: red at 0.6 there (255 - 153 = 102), nothing outside. After /SMask /None
        // the second fill is unmasked.
        var image = Render(
            "q /G1 gs 1 0 0 rg 0 0 200 100 re f Q 0 0 1 rg 0 100 200 100 re f q /G1 gs /G2 gs 0 1 0 rg 0 150 50 50 re f Q",
            "/ExtGState << /G1 << /SMask << /S /Alpha /G 5 0 R >> >> /G2 << /SMask /None >> >>",
            string.Empty,
            Group("/BBox [0 0 100 100] /Group << /S /Transparency >> /Resources << /ExtGState << /A << /ca 0.6 >> >> >>", "/A gs 0 g 0 0 100 100 re f", box: false));

        Assert.Equal((255, 102, 102), Pixel(image, 50, 150));
        Assert.Equal((255, 255, 255), Pixel(image, 150, 150));
        Assert.Equal((0, 0, 255), Pixel(image, 100, 50));
        Assert.Equal((0, 255, 0), Pixel(image, 25, 25));
    }

    [Fact]
    public void A_mask_keeps_the_matrix_of_its_gs_operator()
    {
        // Set under a scale of 2, the 50 x 50 white box covers the device square of 100; scaling back before filling
        // does not move it. BC is black by default: no paint outside.
        var image = Render(
            "q 2 0 0 2 0 0 cm /G1 gs 0.5 0 0 0.5 0 0 cm 1 0 0 rg 0 0 200 200 re f Q",
            "/ExtGState << /G1 << /SMask << /S /Luminosity /G 5 0 R >> >> >>",
            string.Empty,
            Group("/BBox [0 0 50 50] /Group << /S /Transparency >>", "1 g 0 0 50 50 re f", box: false));

        Assert.Equal((255, 0, 0), Pixel(image, 50, 150));
        Assert.Equal((255, 255, 255), Pixel(image, 150, 150));
        Assert.Equal((255, 255, 255), Pixel(image, 50, 50));
    }

    [Fact]
    public void Masks_apply_to_images_text_and_groups()
    {
        // A mask white on the left half: an inline image and a group are kept on the left only, the group masked when
        // it is composited.
        var image = Render(
            "/G1 gs q 200 0 0 50 0 150 cm BI /W 1 /H 1 /CS /RGB /BPC 8 ID \u0000\u0000ÿ EI Q /Fm2 Do",
            "/ExtGState << /G1 << /SMask << /S /Luminosity /G 5 0 R >> >> >> /XObject << /Fm2 6 0 R >>",
            string.Empty,
            Group("/BBox [0 0 100 200] /Group << /S /Transparency >>", "1 g 0 0 100 200 re f", box: false),
            Group("/Group << /S /Transparency >>", "0 1 0 rg 0 0 200 50 re f"));

        Assert.Equal((0, 0, 255), Pixel(image, 50, 25));
        Assert.Equal((255, 255, 255), Pixel(image, 150, 25));
        Assert.Equal((0, 255, 0), Pixel(image, 50, 175));
        Assert.Equal((255, 255, 255), Pixel(image, 150, 175));
    }

    [Fact]
    public void Soft_mask_images_with_a_matte_are_unpremultiplied()
    {
        // Stored colour m + a (c - m) with m white, a 0.6 and c blue: (102, 102, 255). Over black the true colour
        // composited at 0.6 gives (0, 0, 153); taking the stored colour as is would give (61, 61, 153).
        var image = Render(
            "0 g 0 0 200 200 re f q 100 0 0 100 50 50 cm /Im1 Do Q",
            "/XObject << /Im1 5 0 R >>",
            string.Empty,
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /SMask 6 0 R", [102, 102, 255]),
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Matte [1 1 1]", [153]));

        Assert.Equal((0, 0, 153), Pixel(image, 100, 100));
    }

    [Fact]
    public void An_image_soft_mask_replaces_the_soft_mask_of_the_graphics_state()
    {
        // The state mask keeps the left half only; the image has its own opaque soft mask, which applies instead, so
        // the blue image covers the page (§11.6.5.3). The plain image beside it stays under the state mask.
        var image = Render(
            "/G1 gs q 200 0 0 100 0 100 cm /Im1 Do Q q 200 0 0 100 0 0 cm /Im2 Do Q",
            "/XObject << /Im1 6 0 R /Im2 8 0 R >> /ExtGState << /G1 << /SMask << /S /Luminosity /G 5 0 R >> >> >>",
            string.Empty,
            Group("/BBox [0 0 100 200] /Group << /S /Transparency >>", "1 g 0 0 100 200 re f", box: false),
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /SMask 7 0 R", [0, 0, 255]),
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8", [255]),
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8", [255, 0, 0]));

        Assert.Equal((0, 0, 255), Pixel(image, 150, 50));
        Assert.Equal((255, 0, 0), Pixel(image, 50, 150));
        Assert.Equal((255, 255, 255), Pixel(image, 150, 150));
    }

    [Fact]
    public void Masks_that_cannot_be_read_are_reported()
    {
        var rendering = PdfRenderer.Render(PdfDocument.Open(RawPdf.Page(
            "/G1 gs /G2 gs 1 0 0 rg 0 0 200 200 re f",
            "/ExtGState << /G1 << /SMask << /S /Luminosity >> >> /G2 << /SMask << /S /Alpha /G 5 0 R /TR 6 0 R >> >> >>",
            string.Empty,
            Group("/BBox [0 0 100 200] /Group << /S /Transparency >>", "0 g 0 0 100 200 re f", box: false),
            RawPdf.Stream("/FunctionType 4 /Domain [0 1] /Range [0 1]", RawPdf.Ascii("{ 1 exch sub }")))).GetPage(1), new PdfRenderOptions { Dpi = 72 });

        Assert.Equal(["soft mask transfer functions that cannot be read are ignored", "soft masks that cannot be read are not applied"], rendering.Gaps);
        Assert.Equal((255, 0, 0), Pixel(rendering.Image, 50, 100));
        Assert.Equal((255, 255, 255), Pixel(rendering.Image, 150, 100));
    }

    [Fact]
    public void A_mask_group_using_its_own_mask_stops()
    {
        // The mask's group sets the same mask: the inner use finds the group already running and draws nothing more.
        var image = Render(
            "/G1 gs 1 0 0 rg 0 0 200 200 re f",
            "/ExtGState << /G1 << /SMask << /S /Luminosity /G 5 0 R >> >> >>",
            string.Empty,
            Group("/BBox [0 0 100 200] /Group << /S /Transparency >> /Resources << /ExtGState << /G1 << /SMask << /S /Luminosity /G 5 0 R >> >> >> >>", "/G1 gs 1 g 0 0 100 200 re f", box: false));

        Assert.Equal((255, 255, 255), Pixel(image, 50, 100));
        Assert.Equal((255, 255, 255), Pixel(image, 150, 100));
    }

    // An empty mask group leaves BC everywhere. Three components are RGB: blue has luminosity 0.11, red shows at 0.11
    // (255 - 28.05); four are CMYK: black at 0.6 is grey 0.4, red shows at 0.4 (255 - 102).
    [Theory]
    [InlineData("[0 0 1]", 227)]
    [InlineData("[0 0 0 0.6]", 153)]
    public void A_backdrop_without_a_group_colour_space_is_read_by_its_component_count(string backdrop, int greenAndBlue)
    {
        var image = Render(
            "/G1 gs 1 0 0 rg 0 0 200 200 re f",
            $"/ExtGState << /G1 << /SMask << /S /Luminosity /G 5 0 R /BC {backdrop} >> >> >>",
            string.Empty,
            Group("/BBox [0 0 0 0] /Group << /S /Transparency >>", string.Empty, box: false));

        Assert.Equal((255, greenAndBlue, greenAndBlue), Pixel(image, 100, 100));
    }

    [Fact]
    public void Groups_outside_the_page_and_transparent_paint_change_nothing()
    {
        var image = Render(
            "/Fm1 Do /G1 gs 1 0 0 rg 0 0 200 200 re f",
            "/XObject << /Fm1 5 0 R >> /ExtGState << /G1 << /ca 0 >> >>",
            string.Empty,
            Group("/BBox [500 500 600 600] /Group << /S /Transparency >>", "1 0 0 rg 500 500 100 100 re f", box: false));

        Assert.All(image.Pixels, p => Assert.Equal(255, p));
    }

    private static string Square(string backdrop, string source) => $"{backdrop} rg 0 0 200 200 re f /G1 gs {source} rg 50 50 100 100 re f";

    private static byte[] Group(string dictionary, string content, bool box = true) =>
        RawPdf.Stream($"/Type /XObject /Subtype /Form {(box ? "/BBox [0 0 200 200] " : string.Empty)}{dictionary}", RawPdf.Ascii(content));

    private static RasterImage Render(string content, string resources = "", string pageExtra = "", params object[] objects) =>
        PdfRenderer.Render(PdfDocument.Open(RawPdf.Page(content, resources, pageExtra, objects)).GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image;

    private static (int R, int G, int B) Pixel(RasterImage image, int x, int y)
    {
        var (r, g, b, _) = image.GetRgba(x, y);
        return (r, g, b);
    }
}
