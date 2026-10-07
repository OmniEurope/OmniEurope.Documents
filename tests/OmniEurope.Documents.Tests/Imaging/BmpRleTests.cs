// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;

namespace OmniEurope.Documents.Tests.Imaging;

/// <summary>
/// Run-length encoded bitmaps (BI_RLE8 and BI_RLE4), written byte by byte from the BMP format: encoded runs,
/// absolute runs with their word padding, end of line, delta and end of bitmap. An independent
/// decoder decodes the same files to the same pixels.
/// </summary>
public sealed class BmpRleTests
{
    private static readonly (byte R, byte G, byte B)[] Colors = [(0, 0, 0), (255, 0, 0), (0, 255, 0), (0, 0, 255)];

    // Rows from the top; '.' is a pixel the delta skips, left undefined by the format.
    private static readonly string[] Expected = ["33333333", "12311111", "..222222", "11123230"];

    [Fact]
    public void Decodes_rle8()
    {
        byte[] data =
        [
            3, 1, 0, 4, 2, 3, 2, 3, 1, 0, 0, 0,
            0, 2, 2, 0, 6, 2, 0, 0,
            0, 3, 1, 2, 3, 0, 5, 1, 0, 0,
            8, 3, 0, 1,
        ];

        AssertPixels(BmpDecoder.Decode(Bitmap(8, data)));
    }

    [Fact]
    public void Decodes_rle4()
    {
        byte[] data =
        [
            3, 0x11, 0, 4, 0x23, 0x23, 1, 0x00, 0, 0,
            0, 2, 2, 0, 6, 0x22, 0, 0,
            0, 3, 0x12, 0x30, 5, 0x11, 0, 0,
            8, 0x33, 0, 1,
        ];

        AssertPixels(BmpDecoder.Decode(Bitmap(4, data)));
    }

    [Theory]
    [InlineData(8, new byte[] { 2, 1, 0, 2 })]
    [InlineData(8, new byte[] { 2, 1, 0, 2, 1 })]
    [InlineData(8, new byte[] { 2, 1, 0, 6, 1, 2 })]
    [InlineData(4, new byte[] { 2, 0x11, 0, 7, 0x12 })]
    public void Truncated_data_keeps_what_was_decoded(int bits, byte[] data)
    {
        var image = BmpDecoder.Decode(Bitmap(bits, data));

        var (r, g, b, _) = image.GetRgba(1, 3);
        Assert.Equal((255, 0, 0), (r, g, b));
    }

    private static void AssertPixels(RasterImage image)
    {
        Assert.Equal((8, 4), (image.Width, image.Height));
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                if (Expected[y][x] == '.')
                {
                    continue;
                }

                var (r, g, b, _) = image.GetRgba(x, y);
                Assert.Equal(Colors[Expected[y][x] - '0'], (r, g, b));
            }
        }
    }

    // BITMAPFILEHEADER, BITMAPINFOHEADER (8 x 4 pixels, bottom-up) and a four-colour palette.
    private static byte[] Bitmap(int bits, byte[] data)
    {
        const int offset = 14 + 40 + 16;
        var file = new byte[offset + data.Length];
        void Put16(int at, int value) => BitConverter.GetBytes((ushort)value).CopyTo(file, at);
        void Put32(int at, int value) => BitConverter.GetBytes(value).CopyTo(file, at);
        file[0] = (byte)'B';
        file[1] = (byte)'M';
        Put32(2, file.Length);
        Put32(10, offset);
        Put32(14, 40);
        Put32(18, 8);
        Put32(22, 4);
        Put16(26, 1);
        Put16(28, bits);
        Put32(30, bits == 8 ? 1 : 2);
        Put32(34, data.Length);
        Put32(46, 4);
        for (var i = 0; i < Colors.Length; i++)
        {
            file[54 + (i * 4)] = Colors[i].B;
            file[55 + (i * 4)] = Colors[i].G;
            file[56 + (i * 4)] = Colors[i].R;
        }

        data.CopyTo(file, offset);
        return file;
    }
}
