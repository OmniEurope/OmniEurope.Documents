// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Imaging;
using static OmniEurope.Documents.Tests.Imaging.ImageFixtures;

namespace OmniEurope.Documents.Tests.Imaging;

/// <summary>
/// Headers read without decoding: WebP (RFC 9649: VP8X canvas, VP8L and VP8 frame sizes), PNG pHYs, JFIF
/// densities and BMP resolution, written byte by byte.
/// </summary>
public sealed class ImageInfoTests
{
    [Theory]
    [InlineData(ImageFormat.Png, "image/png")]
    [InlineData(ImageFormat.Jpeg, "image/jpeg")]
    [InlineData(ImageFormat.Gif, "image/gif")]
    [InlineData(ImageFormat.Bmp, "image/bmp")]
    [InlineData(ImageFormat.Tiff, "image/tiff")]
    [InlineData(ImageFormat.WebP, "image/webp")]
    [InlineData(ImageFormat.Unknown, "application/octet-stream")]
    public void Content_types_follow_the_format(ImageFormat format, string contentType)
    {
        Assert.Equal(contentType, new ImageInfo(format, 1, 1, 0, 0).ContentType);
    }

    [Theory]
    [InlineData("VP8X", new byte[] { 0, 0, 0, 0, 0x2B, 0x01, 0, 0xC7, 0, 0 }, 300, 200)]
    [InlineData("VP8L", new byte[] { 0x2F, 0x2B, 0x01, 0x1F, 0x00, 0, 0, 0, 0, 0 }, 300, 125)]
    [InlineData("VP8 ", new byte[] { 0, 0, 0, 0x9D, 0x01, 0x2A, 0x40, 0x01, 0xF0, 0x00 }, 320, 240)]
    public void WebP_sizes_come_from_the_first_chunk(string chunk, byte[] body, int width, int height)
    {
        // VP8X: width - 1 and height - 1 on 24 bits; VP8L: 14-bit width - 1 then height - 1 after the 0x2F
        // signature (299 | 124 << 14 = 0x1F012B); VP8: 16-bit sizes after the 9D 01 2A start code.
        byte[] data = [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8, .. Encoding.ASCII.GetBytes(chunk), (byte)body.Length, 0, 0, 0, .. body];

        Assert.True(ImageInfo.TryIdentify(data, out var info));
        Assert.Equal((ImageFormat.WebP, width, height), (info.Format, info.Width, info.Height));
    }

    [Fact]
    public void Png_resolution_comes_from_pHYs_before_the_image_data()
    {
        // 3780 pixels per metre is 96 dots per inch; a pHYs after IDAT and a chunk with a bad length are not read.
        var before = Png(Chunk("pHYs", [0, 0, 0x0E, 0xC4, 0, 0, 0x0E, 0xC4, 1]), Chunk("IDAT", [0]));
        var after = Png(Chunk("IDAT", [0]), Chunk("pHYs", [0, 0, 0x0E, 0xC4, 0, 0, 0x0E, 0xC4, 1]));
        var broken = Png([0xFF, 0xFF, 0xFF, 0xFF, .. "tEXt"u8, 0, 0, 0, 0]);
        var none = Png();

        Assert.Equal((96.0, 96.0), Dpi(before));
        Assert.Equal((0.0, 0.0), Dpi(after));
        Assert.Equal((0.0, 0.0), Dpi(broken));
        Assert.Equal((0.0, 0.0), Dpi(none));
    }

    [Theory]
    [InlineData(1, 96.0)]
    [InlineData(2, 243.84)]
    [InlineData(0, 0.0)]
    public void Jfif_density_units_give_dots_per_inch(byte units, double dpi)
    {
        // The fixture declares a density of 96 in both directions; units 2 mean dots per centimetre.
        var jpeg = Read("baseline.jpg");
        jpeg[13] = units;

        Assert.True(ImageInfo.TryIdentify(jpeg, out var info));
        Assert.Equal((dpi, dpi), (Math.Round(info.DpiX, 2), Math.Round(info.DpiY, 2)));
    }

    [Fact]
    public void Damaged_headers_are_not_identified()
    {
        // A BMP signature followed by an empty DIB header.
        byte[] bmp = [(byte)'B', (byte)'M', .. new byte[40]];

        Assert.False(ImageInfo.TryIdentify(bmp, out _));
    }

    private static (double, double) Dpi(byte[] png)
    {
        Assert.True(ImageInfo.TryIdentify(png, out var info));
        return (info.DpiX, info.DpiY);
    }

    // PNG signature, a 2 x 3 IHDR, then the given chunks (CRCs are not read when identifying).
    private static byte[] Png(params byte[][] chunks) =>
        [137, 80, 78, 71, 13, 10, 26, 10, .. Chunk("IHDR", [0, 0, 0, 2, 0, 0, 0, 3, 8, 2, 0, 0, 0]), .. chunks.SelectMany(c => c)];

    private static byte[] Chunk(string type, byte[] body) =>
        [0, 0, (byte)(body.Length >> 8), (byte)body.Length, .. Encoding.ASCII.GetBytes(type), .. body, 0, 0, 0, 0];
}
