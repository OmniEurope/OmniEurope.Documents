// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;

namespace OmniEurope.Documents.Tests.Imaging.Jpeg2000;

/// <summary>
/// The JPXDecode filter: PDF images whose data is JPEG 2000, in the colour space of the data or the one the image
/// names, with the opacity channel as soft mask when SMaskInData asks for it; compared with the reference planes of
/// the conformance samples.
/// </summary>
public sealed class Jpeg2000PdfTests
{
    [Theory]
    [InlineData("lossless-gray-u8-prog1-layers1-res6.jp2", ImageColorType.Gray)]
    [InlineData("lossless-rgb-u8-prog1-layers1-res6-mct.jp2", ImageColorType.Rgb)]
    [InlineData("lossless-indexed-u8-rgb-u8.jp2", ImageColorType.Rgb)]
    [InlineData("lossless-cmyk-u8-prog1-layers1-res6.jp2", ImageColorType.Cmyk)]
    [InlineData("tile-cod.jp2", ImageColorType.Rgb)]
    public void A_jpx_image_without_colour_space_takes_the_colour_space_of_its_data(string file, ImageColorType type)
    {
        var sample = Jpeg2000Conformance.Sample(file);
        var expected = Jpeg2000Conformance.Expected(sample);

        var image = Assert.Single(PdfDocument.Open(Jpeg2000Conformance.Pdf(Jpeg2000Conformance.Data(sample), expected[0].Width, expected[0].Height)).GetPage(1).Images);
        var raster = image.Decode()!;

        Assert.Equal(["JPXDecode"], image.Filters);
        Assert.Equal(type, raster.ColorType);
        Assert.Equal(Interleave(expected, raster.Components), raster.Pixels);
    }

    [Fact]
    public void The_opacity_channel_is_the_soft_mask_only_when_smask_in_data_asks_for_it()
    {
        var sample = Jpeg2000Conformance.Sample("lossless-rgba-u8-prog1-layers1-res6-mct.jp2");
        var expected = Jpeg2000Conformance.Expected(sample);
        var data = Jpeg2000Conformance.Data(sample);
        var (width, height) = (expected[0].Width, expected[0].Height);

        var masked = Assert.Single(PdfDocument.Open(Jpeg2000Conformance.Pdf(data, width, height, "/SMaskInData 1")).GetPage(1).Images).Decode()!;
        var opaque = Assert.Single(PdfDocument.Open(Jpeg2000Conformance.Pdf(data, width, height)).GetPage(1).Images).Decode()!;

        Assert.Equal(ImageColorType.Rgba, masked.ColorType);
        Assert.Equal(Interleave(expected, 4), masked.Pixels);
        Assert.Equal(ImageColorType.Rgb, opaque.ColorType);
        Assert.Equal(Interleave(expected, 3), opaque.Pixels);
    }

    [Fact]
    public void Premultiplied_opacity_divides_the_colours_back()
    {
        var sample = Jpeg2000Conformance.Sample("lossless-gray-alpha-u8-prog1-layers1-res6.jp2");
        var expected = Jpeg2000Conformance.Expected(sample);

        var raster = Assert.Single(PdfDocument.Open(Jpeg2000Conformance.Pdf(Jpeg2000Conformance.Data(sample), expected[0].Width, expected[0].Height,
            "/SMaskInData 2")).GetPage(1).Images).Decode()!;

        Assert.Equal(ImageColorType.GrayAlpha, raster.ColorType);
        for (var i = 0; i < expected[0].Samples.Length; i++)
        {
            var (gray, alpha) = (expected[0].Samples[i], expected[1].Samples[i]);
            var divided = alpha == 0 ? 0 : Math.Min(255, ((gray * 255) + (alpha / 2)) / alpha);
            Assert.Equal((divided, alpha), (raster.Pixels[2 * i], raster.Pixels[(2 * i) + 1]));
        }
    }

    [Theory]
    [InlineData("subsampled-420-a.jp2")]
    [InlineData("subsampled-422-a.jp2")]
    [InlineData("subsampled-420-b.jp2")]
    [InlineData("subsampled-420-cprl.j2k")]
    public void Subsampled_luma_and_chroma_are_converted_to_rgb_as_the_reference_renders_them_within_one_level(string file)
    {
        var sample = Jpeg2000Conformance.Sample(file);
        var rendered = Jpeg2000Conformance.Expected(Path.GetFileNameWithoutExtension(file) + ".rgb.pgm");

        var raster = Assert.Single(PdfDocument.Open(Jpeg2000Conformance.Pdf(Jpeg2000Conformance.Data(sample), rendered[0].Width, rendered[0].Height))
            .GetPage(1).Images).Decode()!;

        Assert.Equal(ImageColorType.Rgb, raster.ColorType);
        var decoded = Enumerable.Range(0, 3).Select(c => new Jpeg2000TestPlane(raster.Width, raster.Height, 255,
            [.. Enumerable.Range(0, raster.Width * raster.Height).Select(i => (int)raster.Pixels[(3 * i) + c])])).ToList();
        var mismatch = Jpeg2000Conformance.Mismatch(decoded, rendered, 1);
        Assert.True(mismatch is null, mismatch);
    }

    [Fact]
    public void A_named_colour_space_takes_the_samples_and_an_indexed_one_takes_them_as_indices()
    {
        var sample = Jpeg2000Conformance.Sample("lossless-indexed-u8-rgb-u8.jp2");
        var data = Jpeg2000Conformance.Data(sample);
        var expected = Jpeg2000Conformance.Expected(sample);
        var (width, height) = (expected[0].Width, expected[0].Height);
        var palette = Palette(data);
        var lookup = string.Concat(palette.Select(b => b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)));

        var indexed = Assert.Single(PdfDocument.Open(Jpeg2000Conformance.Pdf(data, width, height,
            $"/ColorSpace [/Indexed /DeviceRGB {(palette.Length / 3) - 1} <{lookup}>] /BitsPerComponent 8")).GetPage(1).Images).Decode()!;
        var gray = Assert.Single(PdfDocument.Open(Jpeg2000Conformance.Pdf(Jpeg2000Conformance.Data(Jpeg2000Conformance.Sample("lossless-rgb-u8-prog1-layers1-res6-mct.jp2")),
            width, height, "/ColorSpace /DeviceGray")).GetPage(1).Images).Decode()!;
        var masked = Assert.Single(PdfDocument.Open(Jpeg2000Conformance.Pdf(Jpeg2000Conformance.Data(Jpeg2000Conformance.Sample("lossless-gray-u8-prog1-layers1-res6.jp2")),
            width, height, "/ImageMask true")).GetPage(1).Images).Decode()!;

        Assert.Equal(Interleave(expected, 3), indexed.Pixels);
        var red = Jpeg2000Conformance.Expected(Jpeg2000Conformance.Sample("lossless-rgb-u8-prog1-layers1-res6-mct.jp2"))[0];
        Assert.Equal(red.Samples.Select(v => (byte)v), gray.Pixels);
        Assert.Equal(ImageColorType.Gray, masked.ColorType);
    }

    [Fact]
    public void A_jpx_image_is_drawn_by_the_renderer_and_one_it_cannot_decode_is_reported()
    {
        var sample = Jpeg2000Conformance.Sample("lossless-gray-u8-prog1-layers1-res6.jp2");
        var expected = Jpeg2000Conformance.Expected(sample)[0];
        var data = Jpeg2000Conformance.Data(sample);
        var highThroughput = (byte[])data.Clone();
        highThroughput[Cod(highThroughput) + 12] |= 0x40;

        var drawn = PdfRenderer.Render(PdfDocument.Open(Jpeg2000Conformance.Pdf(data, expected.Width, expected.Height)).GetPage(1), new PdfRenderOptions { Dpi = 72 });
        var refused = PdfDocument.Open(Jpeg2000Conformance.Pdf(highThroughput, expected.Width, expected.Height));
        var reported = PdfRenderer.Render(refused.GetPage(1), new PdfRenderOptions { Dpi = 72 });

        Assert.Empty(drawn.Gaps);
        Assert.Equal(expected.Samples[(50 * expected.Width) + 60], drawn.Image.GetRgba(60, 50).R);
        Assert.Null(Assert.Single(refused.GetPage(1).Images).Decode());
        Assert.Contains("images in an unsupported format are not drawn", reported.Gaps);
    }

    // The colour planes then the given number of planes in all, interleaved as 8-bit pixels.
    private static byte[] Interleave(List<Jpeg2000TestPlane> planes, int count)
    {
        var size = planes[0].Samples.Length;
        var output = new byte[size * count];
        for (var i = 0; i < size; i++)
        {
            for (var c = 0; c < count; c++)
            {
                output[(i * count) + c] = (byte)planes[c].Samples[i];
            }
        }

        return output;
    }

    // Where the COD marker of the codestream lies.
    internal static int Cod(byte[] data)
    {
        for (var i = 0; i + 1 < data.Length; i++)
        {
            if (data[i] == 0xFF && data[i + 1] == 0x52)
            {
                return i;
            }
        }

        throw new InvalidDataException("no COD marker");
    }

    // The entries of the palette box, three 8-bit columns each.
    private static byte[] Palette(byte[] data)
    {
        var at = Encoding.ASCII.GetString(data).IndexOf("pclr", StringComparison.Ordinal) + 4;
        var entries = (data[at] << 8) | data[at + 1];
        Assert.Equal(3, data[at + 2]);
        return data[(at + 6)..(at + 6 + (3 * entries))];
    }
}
