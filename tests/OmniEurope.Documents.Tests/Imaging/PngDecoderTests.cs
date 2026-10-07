// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;

namespace OmniEurope.Documents.Tests.Imaging;

/// <summary>
/// PNG files written from the specification (<see cref="PngFixture"/>): row filters reversed on hand-computed rows
/// (clause 9), 16-bit samples, transparency chunks, interlacing, damaged files. An independent decoder
/// decodes the valid files to the same pixels (it zeroes the colour of fully transparent ones).
/// </summary>
public sealed class PngDecoderTests
{
    [Fact]
    public void Average_and_Paeth_rows_add_the_predictor_to_each_byte()
    {
        // Average with no row above (left only), Average with one, then Paeth.
        byte[] raw = [3, 10, 4, 6, 3, 20, 1, 2, 4, 1, 1, 1];

        var image = new PngUnpacker(new PngHeader(3, 3, 8, 0, false), null, null).Unpack(raw);

        Assert.Equal(new byte[] { 10, 9, 10, 25, 18, 16, 26, 19, 17 }, image.Pixels);
    }

    [Fact]
    public void Sixteen_bit_samples_keep_their_high_byte()
    {
        var png = PngFixture.Png(1, 1, 16, 2, false, [0, 0x12, 0x34, 0xAB, 0xCD, 0xFF, 0x00]);

        Assert.Equal(new byte[] { 0x12, 0xAB, 0xFF }, PngCodec.Decode(png).Pixels);
    }

    [Fact]
    public void Transparency_chunks_give_grey_rgb_and_palette_images_an_alpha_channel()
    {
        // Grey key 100, RGB key (10, 20, 30), and a palette whose first entry is half transparent.
        var grey = PngFixture.Png(2, 1, 8, 0, false, [0, 100, 50], ("tRNS", [0, 100]));
        var rgb = PngFixture.Png(2, 1, 8, 2, false, [0, 10, 20, 30, 10, 20, 31], ("tRNS", [0, 10, 0, 20, 0, 30]));
        var indexed = PngFixture.Png(2, 1, 8, 3, false, [0, 0, 1], ("PLTE", [255, 0, 0, 0, 0, 255]), ("tRNS", [128]));

        Assert.Equal(new byte[] { 100, 0, 50, 255 }, PngCodec.Decode(grey).Pixels);
        Assert.Equal(new byte[] { 10, 20, 30, 0, 10, 20, 31, 255 }, PngCodec.Decode(rgb).Pixels);
        Assert.Equal(new byte[] { 255, 0, 0, 128, 0, 0, 255, 255 }, PngCodec.Decode(indexed).Pixels);
    }

    [Fact]
    public void An_interlaced_single_pixel_has_only_the_first_pass()
    {
        var png = PngFixture.Png(1, 1, 8, 0, true, [0, 77]);

        Assert.Equal(new byte[] { 77 }, PngCodec.Decode(png).Pixels);
    }

    [Fact]
    public void Cmyk_images_are_written_as_rgb()
    {
        var cmyk = new RasterImage(1, 1, ImageColorType.Cmyk, [255, 0, 0, 0]);

        var decoded = PngCodec.Decode(PngCodec.Encode(cmyk));

        Assert.Equal(ImageColorType.Rgb, decoded.ColorType);
        Assert.Equal(new byte[] { 0, 255, 255 }, decoded.Pixels);
    }

    public static TheoryData<string, string> Damaged => new()
    {
        { "signature", "Not a PNG file." },
        { "no header", "PNG without IHDR." },
        { "short header", "Short IHDR chunk." },
        { "bad depth", "Unsupported or invalid PNG header." },
        { "short data", "PNG image data is shorter than its size requires." },
        { "too large", "PNG image data is too large." },
        { "palette index", "PNG palette index out of range." },
    };

    [Theory]
    [MemberData(nameof(Damaged))]
    public void Damaged_files_are_rejected(string defect, string message)
    {
        var png = defect switch
        {
            "signature" => [1, 2, 3, 4, 5, 6, 7, 8],
            "no header" => PngFixture.File(("IEND", [])),
            "short header" => PngFixture.File(("IHDR", [0, 0, 0, 1, 0])),
            "bad depth" => PngFixture.Png(1, 1, 4, 2, false, [0, 0]),
            "short data" => PngFixture.Png(2, 2, 8, 0, false, [0, 1, 2]),
            "too large" => PngFixture.Png(16384, 16384, 16, 6, false, []),
            _ => PngFixture.Png(1, 1, 8, 3, false, [0, 5], ("PLTE", [255, 0, 0, 0, 0, 255])),
        };

        var error = Assert.Throws<InvalidDataException>(() => PngCodec.Decode(png));

        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void An_unknown_filter_type_is_rejected()
    {
        var unpacker = new PngUnpacker(new PngHeader(1, 1, 8, 0, false), null, null);

        Assert.Throws<InvalidDataException>(() => unpacker.Unpack([5, 0]));
    }
}
