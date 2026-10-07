// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;

namespace OmniEurope.Documents.Tests.Imaging;

/// <summary>
/// Uncompressed bitmaps written byte by byte from the BMP format: OS/2 core header, 16-bit 5-5-5 and bit-field
/// masks, 32-bit alpha (plain, V5 header, BI_ALPHABITFIELDS), top-down rows, short palettes and data. An independent
/// decoder decodes the core, 5-5-5, 5-6-5, V5 and top-down files to the same pixels; it refuses an empty
/// mask and BI_ALPHABITFIELDS, and keeps plain 32-bit pixels opaque where a set fourth byte is read here as alpha.
/// </summary>
public sealed class BmpDecoderTests
{
    [Fact]
    public void Core_header_uses_three_byte_palette_entries()
    {
        // 3 x 2, 1 bit: bottom row 1 0 1, top row 0 1 0; palette black, white.
        var file = File(Core(3, 2, 1), [0, 0, 0, 255, 255, 255], [0xA0, 0, 0, 0, 0x40, 0, 0, 0]);

        var image = BmpDecoder.Decode(file);

        Assert.Equal("0 1 0 / 1 0 1", Grey(image));
    }

    [Fact]
    public void Sixteen_bit_pixels_default_to_five_bits_per_channel()
    {
        var image = BmpDecoder.Decode(File(Info(2, 1, 16, 0), [], [0x00, 0x7C, 0x1F, 0x00]));

        Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), image.GetRgba(0, 0));
        Assert.Equal(((byte)0, (byte)0, (byte)255, (byte)255), image.GetRgba(1, 0));
    }

    [Fact]
    public void Bit_field_masks_select_the_channels_and_an_empty_mask_reads_zero()
    {
        // 5-6-5 masks after the header; the second file has no blue mask.
        byte[] masks565 = [.. U32(0xF800), .. U32(0x07E0), .. U32(0x001F)];
        byte[] noBlue = [.. U32(0xF800), .. U32(0x07E0), .. U32(0)];

        var image = BmpDecoder.Decode(File(Info(2, 1, 16, 3), masks565, [0xE0, 0x07, 0xFF, 0xFF]));
        var yellow = BmpDecoder.Decode(File(Info(1, 1, 16, 3), noBlue, [0xFF, 0xFF, 0, 0]));

        Assert.Equal(((byte)0, (byte)255, (byte)0, (byte)255), image.GetRgba(0, 0));
        Assert.Equal(((byte)255, (byte)255, (byte)255, (byte)255), image.GetRgba(1, 0));
        Assert.Equal(((byte)255, (byte)255, (byte)0, (byte)255), yellow.GetRgba(0, 0));
    }

    [Fact]
    public void Plain_32_bit_pixels_carry_alpha_only_when_a_fourth_byte_is_set()
    {
        var withAlpha = BmpDecoder.Decode(File(Info(1, 1, 32, 0), [], [10, 20, 30, 128]));
        var opaque = BmpDecoder.Decode(File(Info(1, 1, 32, 0), [], [10, 20, 30, 0]));

        Assert.Equal((ImageColorType.Rgba, ((byte)30, (byte)20, (byte)10, (byte)128)), (withAlpha.ColorType, withAlpha.GetRgba(0, 0)));
        Assert.Equal((ImageColorType.Rgb, ((byte)30, (byte)20, (byte)10, (byte)255)), (opaque.ColorType, opaque.GetRgba(0, 0)));
    }

    [Fact]
    public void V5_header_and_alpha_bit_fields_read_the_alpha_mask()
    {
        // V5: masks inside the 124-byte header (BGRA order). BI_ALPHABITFIELDS: four masks after a 40-byte header, RGBA order.
        var v5 = Info(1, 1, 32, 3, size: 124);
        U32(0x00FF0000).CopyTo(v5, 40);
        U32(0x0000FF00).CopyTo(v5, 44);
        U32(0x000000FF).CopyTo(v5, 48);
        U32(0xFF000000).CopyTo(v5, 52);
        byte[] rgbaMasks = [.. U32(0x000000FF), .. U32(0x0000FF00), .. U32(0x00FF0000), .. U32(0xFF000000)];

        var fromV5 = BmpDecoder.Decode(File(v5, [], [1, 2, 3, 4]));
        var fromAlphaFields = BmpDecoder.Decode(File(Info(1, 1, 32, 6), rgbaMasks, [1, 2, 3, 4]));

        Assert.Equal(((byte)3, (byte)2, (byte)1, (byte)4), fromV5.GetRgba(0, 0));
        Assert.Equal(((byte)1, (byte)2, (byte)3, (byte)4), fromAlphaFields.GetRgba(0, 0));
    }

    [Fact]
    public void Top_down_rows_short_palettes_and_dib_without_file_header()
    {
        // 1 x 3, 8 bits, top-down; two palette entries, so index 7 has no colour (black).
        var info = Info(1, -3, 8, 0, used: 2);
        var file = File(info, [0, 0, 0, 0, 0, 0, 255, 0], [1, 0, 0, 0, 0, 0, 0, 0, 7, 0, 0, 0]);

        var image = BmpDecoder.Decode(file);
        var dib = BmpDecoder.DecodeDib(file.AsSpan(14));

        Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), image.GetRgba(0, 0));
        Assert.Equal(((byte)0, (byte)0, (byte)0, (byte)255), image.GetRgba(0, 2));
        Assert.Equal(image.Pixels, dib.Pixels);
        Assert.Equal((96.0, 96.0), (image.DpiX, image.DpiY));
    }

    [Fact]
    public void Rows_missing_from_short_data_stay_black()
    {
        // 1 x 3 bottom-up with only the bottom row present.
        var image = BmpDecoder.Decode(File(Info(1, 3, 8, 0, used: 2), [0, 0, 0, 0, 255, 255, 255, 0], [1, 0, 0, 0]));

        Assert.Equal("0 / 0 / 1", Grey(image));
    }

    [Fact]
    public void Rle_runs_past_the_right_edge_are_clipped()
    {
        // 2 x 1 RLE8: a run of three white pixels, then end of bitmap.
        var image = BmpDecoder.Decode(File(Info(2, 1, 8, 1, used: 2), [0, 0, 0, 0, 255, 255, 255, 0], [3, 1, 0, 1]));

        Assert.Equal("1 1", Grey(image));
    }

    public static TheoryData<string, Type> Invalid => new()
    {
        { "short", typeof(InvalidDataException) },
        { "offset", typeof(InvalidDataException) },
        { "header", typeof(InvalidDataException) },
        { "width", typeof(InvalidDataException) },
        { "bits", typeof(InvalidDataException) },
        { "jpeg", typeof(NotSupportedException) },
    };

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Invalid_or_unsupported_files_are_rejected(string defect, Type exception)
    {
        var file = defect switch
        {
            "short" => [(byte)'B', (byte)'M', 0, 0],
            "offset" => File(Info(1, 1, 24, 0), [], [0, 0, 0, 0], offset: 5000),
            "header" => File(Info(1, 1, 24, 0, size: 20), [], [0, 0, 0, 0]),
            "width" => File(Info(0, 1, 24, 0), [], [0, 0, 0, 0]),
            "bits" => File(Info(1, 1, 3, 0), [], [0, 0, 0, 0]),
            _ => File(Info(1, 1, 24, 4), [], [0, 0, 0, 0]),
        };

        Assert.Throws(exception, () => BmpDecoder.Decode(file));
    }

    // BITMAPINFOHEADER (or a longer one with zeros past 40 bytes), 3780 pixels per metre (96 dpi).
    private static byte[] Info(int width, int height, int bits, int compression, int size = 40, int used = 0)
    {
        var header = new byte[Math.Max(size, 40)];
        U32((uint)size).CopyTo(header, 0);
        U32((uint)width).CopyTo(header, 4);
        U32((uint)height).CopyTo(header, 8);
        header[12] = 1;
        header[14] = (byte)bits;
        U32((uint)compression).CopyTo(header, 16);
        U32(3780).CopyTo(header, 24);
        U32(3780).CopyTo(header, 28);
        U32((uint)used).CopyTo(header, 32);
        return header;
    }

    // BITMAPCOREHEADER: 16-bit width and height.
    private static byte[] Core(int width, int height, int bits) => [12, 0, 0, 0, (byte)width, 0, (byte)height, 0, 1, 0, (byte)bits, 0];

    // File header, DIB header, masks or palette, pixels; the pixel offset may be forced.
    private static byte[] File(byte[] header, byte[] between, byte[] pixels, int offset = 0)
    {
        var at = 14 + header.Length + between.Length;
        byte[] file = [(byte)'B', (byte)'M', .. U32((uint)(at + pixels.Length)), 0, 0, 0, 0, .. U32((uint)(offset > 0 ? offset : at)), .. header, .. between, .. pixels];
        return file;
    }

    private static byte[] U32(uint value) => BitConverter.GetBytes(value);

    // Rows from the top, '1' for white and '0' for black.
    private static string Grey(RasterImage image) => string.Join(" / ", Enumerable.Range(0, image.Height).Select(y =>
        string.Join(" ", Enumerable.Range(0, image.Width).Select(x => image.GetRgba(x, y).R == 255 ? "1" : "0"))));
}
