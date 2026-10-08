// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;

namespace OmniEurope.Documents.Tests.Imaging;

/// <summary>
/// GIF files written byte by byte from the GIF89a specification. An independent decoder decodes the
/// interlaced and full-table files of <see cref="GifFixture"/> to the same pixels.
/// </summary>
public sealed class GifDecoderTests
{
    // Logical screen of 1 x 1 pixel with a two-colour global palette (red, blue).
    private static readonly byte[] Screen = [.. "GIF89a"u8, 1, 0, 1, 0, 0x80, 0, 0, 0xFF, 0, 0, 0, 0, 0xFF];

    // Image descriptor and LZW data (minimum code size 2: clear, index 0, end of information).
    private static readonly byte[] Frame = [0x2C, 0, 0, 0, 0, 1, 0, 1, 0, 0, 2, 2, 0x44, 0x01, 0];

    [Fact]
    public void A_graphic_control_extension_makes_its_index_transparent()
    {
        byte[] opaque = [.. Screen, .. Frame, 0x3B];
        byte[] transparent = [.. Screen, 0x21, 0xF9, 4, 1, 0, 0, 0, 0, .. Frame, 0x3B];

        Assert.Equal((255, 0, 0, 255), GifDecoder.Decode(opaque).GetRgba(0, 0));
        Assert.Equal(0, GifDecoder.Decode(transparent).GetRgba(0, 0).A);
    }

    [Fact]
    public void A_frame_larger_than_allowed_is_refused_before_its_pixels_are_allocated()
    {
        // A 1 x 1 screen whose frame claims 32767 x 65535 pixels with empty LZW data (2 GB of indices).
        byte[] frame = [0x2C, 0, 0, 0, 0, 0xFF, 0x7F, 0xFF, 0xFF, 0, 2, 0, 0x3B];

        var error = Assert.Throws<InvalidDataException>(() => GifDecoder.Decode([.. Screen, .. frame]));
        Assert.Contains("32767x65535", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Interlaced_rows_are_stored_in_four_passes()
    {
        // Height 4: pass 1 holds row 0, pass 3 row 2, pass 4 rows 1 and 3.
        var image = GifDecoder.Decode(GifFixture.Image(1, 4, interlaced: true, [0, 1, 2, 3]));

        Assert.Equal([(0, 0, 0), (0, 255, 0), (255, 0, 0), (0, 0, 255)], Enumerable.Range(0, 4).Select(y => Rgb(image, 0, y)));
    }

    [Fact]
    public void Codes_keep_their_12_bit_width_once_the_table_is_full()
    {
        // 4160 literal codes define strings up to code 4095, then go on without a clear code.
        var indexes = Enumerable.Range(0, 64 * 65).Select(i => i * 7 % 4).ToArray();

        var image = GifDecoder.Decode(GifFixture.Image(64, 65, interlaced: false, indexes));

        var palette = GifFixture.Palette;
        Assert.All(Enumerable.Range(0, indexes.Length), i =>
            Assert.Equal((palette[indexes[i] * 3], palette[(indexes[i] * 3) + 1], palette[(indexes[i] * 3) + 2]), Rgb(image, i % 64, i / 64)));
    }

    [Fact]
    public void Codes_cut_short_keep_the_pixels_already_decoded()
    {
        // 1 x 2 frame: clear (100) and index 1 (001), then the data ends.
        byte[] frame = [0x2C, 0, 0, 0, 0, 1, 0, 2, 0, 0, 2, 1, 0x0C, 0];

        var image = GifDecoder.Decode([.. Screen[..6], 1, 0, 2, 0, .. Screen[10..], .. frame, 0x3B]);

        Assert.Equal((0, 0, 255, 255), image.GetRgba(0, 0));
    }

    [Theory]
    [InlineData(0, 2, "GIF frame without a palette.")]
    [InlineData(0x80, 9, "Invalid GIF LZW code size.")]
    public void Frames_without_colours_or_with_a_bad_code_size_are_refused(byte screenFlags, byte codeSize, string message)
    {
        byte[] file = [.. "GIF89a"u8, 1, 0, 1, 0, screenFlags, 0, 0, .. (screenFlags == 0 ? [] : new byte[6]), 0x2C, 0, 0, 0, 0, 1, 0, 1, 0, 0, codeSize, 1, 0x44, 0, 0x3B];

        var error = Assert.Throws<InvalidDataException>(() => GifDecoder.Decode(file));

        Assert.Equal(message, error.Message);
    }

    [Theory]
    [InlineData(new byte[] { 0x3B }, "GIF without an image.")]
    [InlineData(new byte[] { 0x99 }, "Unexpected GIF block.")]
    [InlineData(new byte[] { 0x21, 0xFE, 1, (byte)'x', 0 }, "Truncated GIF.")]
    public void Files_without_a_frame_are_refused(byte[] blocks, string message)
    {
        var error = Assert.Throws<InvalidDataException>(() => GifDecoder.Decode([.. Screen, .. blocks]));

        Assert.Equal(message, error.Message);
    }

    private static (int, int, int) Rgb(RasterImage image, int x, int y)
    {
        var (r, g, b, _) = image.GetRgba(x, y);
        return (r, g, b);
    }
}
