// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>
/// Image colour spaces (ISO 32000-1 §8.6): CIE Lab with its default range, an explicit Decode array or its own
/// Range; spot inks; ICC profiles by component count; named and nested spaces; indexed lookups in a stream;
/// 16-bit samples; inline images and images inside forms. sRGB red is L* 53.24, a* 80.09, b* 67.20 (D65).
/// </summary>
public sealed class PdfImageColorTests
{
    [Theory]
    [InlineData("/ColorSpace [/Lab << /WhitePoint [0.9505 1 1.089] >>]", new byte[] { 136, 230, 213 })]
    [InlineData("/ColorSpace [/Lab << /WhitePoint [0.9505 1 1.089] >>] /Decode [0 100 -100 100 -100 100]", new byte[] { 136, 230, 213 })]
    [InlineData("/ColorSpace [/Lab << /WhitePoint [0.9505 1 1.089] /Range [-128 127 -128 127] >>]", new byte[] { 136, 208, 195 })]
    public void Lab_samples_map_through_the_decode_range_to_srgb(string dictionary, byte[] samples)
    {
        // 136/255 x 100 = 53.3; -100 + 230/255 x 200 = 80.4 and 67.1; -128 + 208 = 80, -128 + 195 = 67.
        var (r, g, b, _) = Single(dictionary + " /BitsPerComponent 8", samples).GetRgba(0, 0);

        Assert.True(r >= 250 && g <= 12 && b <= 12, $"({r}, {g}, {b})");
    }

    [Fact]
    public void Lab_fill_colours_are_used_as_given()
    {
        var pdf = RawPdf.Page("/CS0 cs 53.24 80.09 67.2 scn 0 0 200 200 re f", "/ColorSpace << /CS0 [/Lab << /WhitePoint [0.9505 1 1.089] >>] >>");

        var (r, g, b, _) = Documents.Pdf.Rendering.PdfRenderer.Render(PdfDocument.Open(pdf).GetPage(1), new Documents.Pdf.Rendering.PdfRenderOptions { Dpi = 72 }).Image.GetRgba(100, 100);

        Assert.True(r >= 250 && g <= 12 && b <= 12, $"({r}, {g}, {b})");
    }

    [Fact]
    public void Lab_white_is_white()
    {
        var image = Single("/ColorSpace [/Lab << /WhitePoint [0.9505 1 1.089] >>] /Decode [0 100 0 0 0 0] /BitsPerComponent 8", [255, 9, 9]);

        Assert.Equal(((byte)255, (byte)255, (byte)255, (byte)255), image.GetRgba(0, 0));
    }

    [Theory]
    [InlineData("[/Separation /Gold /DeviceCMYK 9 0 R]", new byte[] { 255, 0 }, new byte[] { 0, 255 })]
    [InlineData("[/DeviceN [/Gold /Silver] /DeviceCMYK 9 0 R]", new byte[] { 255, 0, 0, 64 }, new byte[] { 0, 191 })]
    public void Spot_inks_show_their_coverage_in_grey(string space, byte[] samples, byte[] grey)
    {
        // Full ink is black; with several inks the strongest one counts.
        var image = Single($"/ColorSpace {space} /BitsPerComponent 8", samples, width: 2);

        Assert.Equal(grey, image.Pixels);
    }

    [Theory]
    [InlineData("<< /N 1 >>", new byte[] { 77 }, Documents.Imaging.ImageColorType.Gray)]
    [InlineData("<< /N 4 >>", new byte[] { 0, 0, 0, 255 }, Documents.Imaging.ImageColorType.Cmyk)]
    [InlineData("<< /N 3 >>", new byte[] { 1, 2, 3 }, Documents.Imaging.ImageColorType.Rgb)]
    public void Icc_profiles_count_as_their_number_of_components(string profile, byte[] samples, Documents.Imaging.ImageColorType type)
    {
        var image = Single("/ColorSpace [/ICCBased 9 0 R] /BitsPerComponent 8", samples, extra: RawPdf.Stream(profile, []));

        Assert.Equal((type, samples), (image.ColorType, image.Pixels), TupleComparer.Instance);
    }

    [Theory]
    [InlineData("/CS0", "/CS0 [/ICCBased 9 0 R]", 77)]
    [InlineData("[/DeviceGray]", "", 77)]
    [InlineData("/CS1", "/CS1 /CS1", 77)]
    [InlineData("/Unknown", "", 77)]
    public void Named_and_unusual_spaces_resolve_or_fall_back_to_rgb(string space, string resources, byte first)
    {
        // A space named in the resources, a family alone in an array, a name that names itself, an unknown name.
        var pdf = RawPdf.Page(
            "q 1 0 0 1 0 0 cm /Im1 Do Q",
            $"/XObject << /Im1 5 0 R >> /ColorSpace << {resources} >>",
            string.Empty,
            RawPdf.Stream($"/Type /XObject /Subtype /Image /Width 1 /Height 1 /BitsPerComponent 8 /ColorSpace {space}", [77, 78, 79]),
            "0", "0", "0",
            RawPdf.Stream("/N 1", []));

        var image = Assert.Single(PdfDocument.Open(pdf).GetPage(1).Images).Decode()!;

        Assert.Equal(first, image.Pixels[0]);
        Assert.Equal(space is "/CS0" or "[/DeviceGray]" ? 1 : 3, image.Pixels.Length);
    }

    [Fact]
    public void Indexed_lookups_may_be_streams_or_missing()
    {
        var fromStream = Single("/ColorSpace [/Indexed /DeviceRGB 1 9 0 R] /BitsPerComponent 8", [1, 0], width: 2, extra: RawPdf.Stream(string.Empty, [10, 20, 30, 40, 50, 60]));
        var missing = Single("/ColorSpace [/Indexed /DeviceRGB 1] /BitsPerComponent 8", [1], extra: "0");

        Assert.Equal(new byte[] { 40, 50, 60, 10, 20, 30 }, fromStream.Pixels);
        Assert.Equal(new byte[] { 0, 0, 0 }, missing.Pixels);
    }

    [Fact]
    public void Sixteen_bit_samples_keep_their_high_byte()
    {
        var image = Single("/ColorSpace /DeviceGray /BitsPerComponent 16", [0xAB, 0xCD], extra: "0");

        Assert.Equal(new byte[] { 0xAB }, image.Pixels);
    }

    [Fact]
    public void Undecodable_images_give_no_pixels()
    {
        var image = Assert.Single(PdfDocument.Open(Pdf("/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode", [1, 2, 3], 1, "0")).GetPage(1).Images);

        Assert.Null(image.Decode());
    }

    [Fact]
    public void Inline_images_and_images_inside_forms_are_listed()
    {
        // The inline image is hexadecimal (abbreviated filter names in an array); the form draws image 6.
        var pdf = RawPdf.Page(
            "q 10 0 0 10 0 0 cm BI /W 1 /H 1 /CS /G /BPC 8 /F [/AHx] /DP [null] ID 7F> EI Q /Fm1 Do",
            "/XObject << /Fm1 5 0 R >>",
            string.Empty,
            RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 100 100] /Matrix [2 0 0 2 0 0] /Resources << /XObject << /Im1 6 0 R >> >>", RawPdf.Ascii("q 5 0 0 5 0 0 cm /Im1 Do Q")),
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter [/ASCIIHexDecode]", RawPdf.Ascii("40>")));

        var images = PdfDocument.Open(pdf).GetPage(1).Images;

        Assert.Equal(2, images.Count);
        Assert.True(images[0].IsInline);
        Assert.Equal(new byte[] { 0x7F }, images[0].Decode()!.Pixels);
        Assert.False(images[1].IsInline);
        Assert.Equal(["ASCIIHexDecode"], images[1].Filters);
        Assert.Equal(new byte[] { 0x40 }, images[1].Decode()!.Pixels);
        Assert.Equal(new PdfRectangle(0, 0, 10, 10), images[1].Bounds);
    }

    private static Documents.Imaging.RasterImage Single(string dictionary, byte[] samples, int width = 1, object? extra = null)
    {
        var image = Assert.Single(PdfDocument.Open(Pdf(dictionary, samples, width, extra)).GetPage(1).Images);
        return image.Decode()!;
    }

    // Image 5 drawn on the page; objects 6 to 8 are fillers so that an extra object (a profile, a lookup) is 9.
    private static byte[] Pdf(string dictionary, byte[] samples, int width, object? extra) => RawPdf.Page(
        "q 1 0 0 1 0 0 cm /Im1 Do Q",
        "/XObject << /Im1 5 0 R >>",
        string.Empty,
        RawPdf.Stream($"/Type /XObject /Subtype /Image /Width {width} /Height 1 {dictionary}", samples),
        "0", "0", "0",
        extra ?? RawPdf.Stream("/FunctionType 2 /Domain [0 1] /C0 [0 0 0 0] /C1 [0 0 0 1] /N 1", []));

    private sealed class TupleComparer : IEqualityComparer<(Documents.Imaging.ImageColorType, byte[])>
    {
        public static readonly TupleComparer Instance = new();

        public bool Equals((Documents.Imaging.ImageColorType, byte[]) x, (Documents.Imaging.ImageColorType, byte[]) y) => x.Item1 == y.Item1 && x.Item2.SequenceEqual(y.Item2);

        public int GetHashCode((Documents.Imaging.ImageColorType, byte[]) obj) => obj.Item1.GetHashCode();
    }
}
