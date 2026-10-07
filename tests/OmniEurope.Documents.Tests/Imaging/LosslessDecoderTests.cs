// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using static OmniEurope.Documents.Tests.Imaging.ImageFixtures;

namespace OmniEurope.Documents.Tests.Imaging;

/// <summary>Files written by an independent encoder, decoded by our code, compared pixel for pixel.</summary>
public sealed class LosslessDecoderTests
{
    public static TheoryData<string> RgbFiles => new() { "rgb.png", "rgb.bmp", "rgb-none.tif", "rgb-lzw.tif", "rgb-zip.tif", "rgb-rle.tif" };

    public static TheoryData<string> PaletteFiles => new() { "indexed.png", "indexed.gif", "indexed.bmp", "indexed-lzw.tif" };

    public static TheoryData<string> BilevelFiles => new() { "bilevel.png", "bilevel-none.tif", "bilevel-rle.tif", "bilevel-ccitt3.tif", "bilevel-ccitt4.tif" };

    [Theory]
    [MemberData(nameof(RgbFiles))]
    public void Decodes_rgb_exactly(string name)
    {
        var image = Decode(name);

        AssertPixels(image, Width, Height, (x, y) => { var (r, g, b) = Rgb(x, y); return (r, g, b, 255); });
    }

    [Theory]
    [MemberData(nameof(PaletteFiles))]
    public void Decodes_palette_images_exactly(string name)
    {
        AssertPixels(Decode(name), Width, Height, (x, y) => { var (r, g, b) = Palette(x, y); return (r, g, b, 255); });
    }

    [Theory]
    [MemberData(nameof(BilevelFiles))]
    public void Decodes_bilevel_images_exactly(string name)
    {
        AssertPixels(Decode(name), 1800, 22, (x, y) => IsBlack(x, y) ? ((byte)0, (byte)0, (byte)0, (byte)255) : ((byte)255, (byte)255, (byte)255, (byte)255));
    }

    [Theory]
    [InlineData("rgba.png")]
    [InlineData("rgba-interlaced.png")]
    public void Decodes_alpha_exactly(string name)
    {
        var image = Decode(name);

        Assert.Equal(ImageColorType.Rgba, image.ColorType);
        AssertPixels(image, Width, Height, (x, y) => { var (r, g, b) = Rgb(x, y); return (r, g, b, Alpha(x, y)); });
    }

    [Fact]
    public void Decodes_grey_exactly()
    {
        var image = Decode("gray.png");

        Assert.Equal(ImageColorType.Gray, image.ColorType);
        AssertPixels(image, Width, Height, (x, _) => (Gray(x), Gray(x), Gray(x), 255));
    }

    [Theory]
    [InlineData(ImageColorType.Gray)]
    [InlineData(ImageColorType.GrayAlpha)]
    [InlineData(ImageColorType.Rgb)]
    [InlineData(ImageColorType.Rgba)]
    public void Png_encoding_round_trips(ImageColorType type)
    {
        var source = Decode("rgba.png").ConvertTo(type);
        source.DpiX = source.DpiY = 150;

        var back = PngCodec.Decode(PngCodec.Encode(source));

        Assert.Equal(type, back.ColorType);
        Assert.Equal(source.Pixels, back.Pixels);
        Assert.Equal(150, back.DpiX);
    }

    [Fact]
    public void Corrupted_files_are_rejected()
    {
        var png = Read("rgb.png");
        png[40] ^= 0xFF;

        Assert.Throws<InvalidDataException>(() => PngCodec.Decode(png));
        Assert.Throws<InvalidDataException>(() => PngCodec.Decode(Read("rgb.png").AsSpan(0, 30)));
        Assert.Throws<InvalidDataException>(() => GifDecoder.Decode("GIF89a"u8));
        Assert.Throws<InvalidDataException>(() => TiffDecoder.Decode([1, 2, 3, 4, 5, 6, 7, 8, 9]));
    }

    [Fact]
    public void Ccitt_code_sets_are_prefix_free()
    {
        foreach (var white in (bool[])[true, false])
        {
            var codes = CcittCodes.Codes(white).Select(c => c.Code).ToList();
            foreach (var a in codes)
            {
                Assert.DoesNotContain(codes, b => b != a && b.StartsWith(a, StringComparison.Ordinal));
            }

            Assert.Equal(codes.Count, codes.Distinct().Count());
        }
    }

    private static RasterImage Decode(string name)
    {
        var data = Read(name);
        if (PngCodec.IsPng(data))
        {
            return PngCodec.Decode(data);
        }

        if (GifDecoder.IsGif(data))
        {
            return GifDecoder.Decode(data);
        }

        return BmpDecoder.IsBmp(data) ? BmpDecoder.Decode(data) : TiffDecoder.Decode(data);
    }

    private static void AssertPixels(RasterImage image, int width, int height, Func<int, int, (byte R, byte G, byte B, byte A)> expected)
    {
        Assert.Equal((width, height), (image.Width, image.Height));
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var actual = image.GetRgba(x, y);
                var wanted = expected(x, y);
                if (actual != wanted)
                {
                    Assert.Fail($"Pixel ({x},{y}): expected {wanted}, got {actual}.");
                }
            }
        }
    }
}
