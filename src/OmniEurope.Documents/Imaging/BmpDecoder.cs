// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;
using System.Numerics;

namespace OmniEurope.Documents.Imaging;

/// <summary>
/// BMP files and device-independent bitmaps (the DIB inside EMF records and clipboard data): 1, 4, 8, 16, 24
/// and 32 bits per pixel, palettes, bit-field masks, RLE4 and RLE8, bottom-up or top-down rows.
/// </summary>
public static class BmpDecoder
{
    private const int BiRgb = 0;
    private const int BiRle8 = 1;
    private const int BiRle4 = 2;
    private const int BiBitFields = 3;
    private const int BiAlphaBitFields = 6;

    /// <summary>True when <paramref name="data"/> starts with the BMP file signature.</summary>
    public static bool IsBmp(ReadOnlySpan<byte> data) => data.Length > 26 && data[0] == 'B' && data[1] == 'M';

    /// <summary>Decodes a BMP file (with its 14-byte file header).</summary>
    public static RasterImage Decode(ReadOnlySpan<byte> data)
    {
        if (!IsBmp(data))
        {
            throw new InvalidDataException("Not a BMP file.");
        }

        var pixelOffset = BinaryPrimitives.ReadInt32LittleEndian(data[10..]);
        return DecodeDib(data[14..], pixelOffset - 14);
    }

    /// <summary>
    /// Decodes a DIB: a BITMAPINFOHEADER (or core, V4, V5 header), its palette or masks, then the pixels at
    /// <paramref name="pixelOffset"/> (counted from the header start; negative means right after the palette).
    /// </summary>
    public static RasterImage DecodeDib(ReadOnlySpan<byte> dib, int pixelOffset = -1)
    {
        var header = DibHeader.Parse(dib);
        if (pixelOffset < 0)
        {
            pixelOffset = header.HeaderSize + header.MaskBytes + (header.PaletteCount * header.PaletteEntrySize);
        }

        if (pixelOffset > dib.Length)
        {
            throw new InvalidDataException("BMP pixel data offset beyond the data.");
        }

        var palette = ReadPalette(dib, header);
        var pixels = dib[pixelOffset..];
        var hasAlpha = header.BitCount == 32 && (header.Compression == BiAlphaBitFields || header.AlphaMask != 0 || (header.Compression == BiRgb && HasAlpha(pixels, header)));
        var image = new RasterImage(header.Width, Math.Abs(header.Height), hasAlpha ? ImageColorType.Rgba : ImageColorType.Rgb);
        image.DpiX = Math.Round(header.PixelsPerMeterX * 0.0254, 1);
        image.DpiY = Math.Round(header.PixelsPerMeterY * 0.0254, 1);
        if (header.Compression is BiRle8 or BiRle4)
        {
            BmpRle.Decode(pixels, header, palette, image);
        }
        else
        {
            DecodeRows(pixels, header, palette, image);
        }

        return image;
    }

    private static byte[] ReadPalette(ReadOnlySpan<byte> dib, DibHeader header)
    {
        var palette = new byte[header.PaletteCount * 3];
        var start = header.HeaderSize + header.MaskBytes;
        for (var i = 0; i < header.PaletteCount && start + (i * header.PaletteEntrySize) + 2 < dib.Length; i++)
        {
            var entry = dib[(start + (i * header.PaletteEntrySize))..];
            palette[i * 3] = entry[2];
            palette[(i * 3) + 1] = entry[1];
            palette[(i * 3) + 2] = entry[0];
        }

        return palette;
    }

    private static bool HasAlpha(ReadOnlySpan<byte> pixels, DibHeader header)
    {
        // A plain 32-bit DIB usually leaves the fourth byte at zero; only a non-zero value means alpha.
        var count = Math.Min(pixels.Length / 4, header.Width * Math.Abs(header.Height));
        for (var i = 0; i < count; i++)
        {
            if (pixels[(i * 4) + 3] != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void DecodeRows(ReadOnlySpan<byte> pixels, DibHeader header, byte[] palette, RasterImage image)
    {
        var stride = ((header.Width * header.BitCount) + 31) / 32 * 4;
        var height = image.Height;
        var bottomUp = header.Height > 0;
        var masks = header.Masks();
        for (var row = 0; row < height; row++)
        {
            var source = row * stride;
            if (source + stride > pixels.Length)
            {
                break;
            }

            var line = pixels.Slice(source, stride);
            var y = bottomUp ? height - 1 - row : row;
            for (var x = 0; x < header.Width; x++)
            {
                var o = (y * image.Stride) + (x * image.Components);
                var (r, g, b, a) = Pixel(line, x, header.BitCount, palette, masks);
                image.Pixels[o] = r;
                image.Pixels[o + 1] = g;
                image.Pixels[o + 2] = b;
                if (image.Components == 4)
                {
                    image.Pixels[o + 3] = a;
                }
            }
        }
    }

    private static (byte R, byte G, byte B, byte A) Pixel(ReadOnlySpan<byte> line, int x, int bitCount, byte[] palette, (uint R, uint G, uint B, uint A) masks)
    {
        switch (bitCount)
        {
            case 1 or 2 or 4 or 8:
                var bit = x * bitCount;
                var index = (line[bit >> 3] >> (8 - bitCount - (bit & 7))) & ((1 << bitCount) - 1);
                return (index * 3) + 2 < palette.Length
                    ? (palette[index * 3], palette[(index * 3) + 1], palette[(index * 3) + 2], (byte)255)
                    : ((byte)0, (byte)0, (byte)0, (byte)255);
            case 24:
                return (line[(x * 3) + 2], line[(x * 3) + 1], line[x * 3], (byte)255);
            case 16:
                return Masked(BinaryPrimitives.ReadUInt16LittleEndian(line[(x * 2)..]), masks);
            default:
                return Masked(BinaryPrimitives.ReadUInt32LittleEndian(line[(x * 4)..]), masks);
        }
    }

    private static (byte, byte, byte, byte) Masked(uint value, (uint R, uint G, uint B, uint A) masks) =>
        (Channel(value, masks.R), Channel(value, masks.G), Channel(value, masks.B), masks.A == 0 ? (byte)255 : Channel(value, masks.A));

    private static byte Channel(uint value, uint mask)
    {
        if (mask == 0)
        {
            return 0;
        }

        var shift = BitOperations.TrailingZeroCount(mask);
        var bits = BitOperations.PopCount(mask);
        var raw = (value & mask) >> shift;
        return bits >= 8 ? (byte)(raw >> (bits - 8)) : (byte)(raw * 255 / ((1u << bits) - 1));
    }
}

/// <summary>The fields of a DIB header.</summary>
internal readonly record struct DibHeader(
    int HeaderSize, int Width, int Height, int BitCount, int Compression, int PaletteCount, int PaletteEntrySize,
    int MaskBytes, uint RedMask, uint GreenMask, uint BlueMask, uint AlphaMask, int PixelsPerMeterX, int PixelsPerMeterY)
{
    private static readonly HashSet<int> BitCounts = [1, 2, 4, 8, 16, 24, 32];

    // BI_RGB, BI_RLE8, BI_RLE4, BI_BITFIELDS and BI_ALPHABITFIELDS.
    private static readonly HashSet<int> Compressions = [0, 1, 2, 3, 6];

    public static DibHeader Parse(ReadOnlySpan<byte> dib)
    {
        var size = BinaryPrimitives.ReadInt32LittleEndian(dib);
        if (size == 12)
        {
            return Core(dib);
        }

        if (size < 40 || dib.Length < size)
        {
            throw new InvalidDataException("Unsupported DIB header.");
        }

        var width = BinaryPrimitives.ReadInt32LittleEndian(dib[4..]);
        var height = BinaryPrimitives.ReadInt32LittleEndian(dib[8..]);
        var bitCount = BinaryPrimitives.ReadUInt16LittleEndian(dib[14..]);
        var compression = BinaryPrimitives.ReadInt32LittleEndian(dib[16..]);
        Validate(width, height, bitCount, compression);
        var used = BinaryPrimitives.ReadInt32LittleEndian(dib[32..]);
        var paletteCount = bitCount <= 8 ? PaletteSize(used, bitCount) : 0;
        var bitFields = compression is 3 or 6;
        var masksInHeader = size >= 52;
        var maskBytes = bitFields && !masksInHeader ? (compression == 6 ? 16 : 12) : 0;
        var (red, green, blue, alpha) = bitFields ? ReadMasks(masksInHeader ? dib[40..] : dib[size..], compression == 6 || size >= 56) : default;
        return new DibHeader(size, width, height, bitCount, compression, paletteCount, 4, maskBytes, red, green, blue, alpha,
            BinaryPrimitives.ReadInt32LittleEndian(dib[24..]), BinaryPrimitives.ReadInt32LittleEndian(dib[28..]));
    }

    // BITMAPCOREHEADER (OS/2 1.x): 16-bit sizes and three-byte palette entries.
    private static DibHeader Core(ReadOnlySpan<byte> dib)
    {
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(dib[10..]);
        return new DibHeader(12, BinaryPrimitives.ReadUInt16LittleEndian(dib[4..]), BinaryPrimitives.ReadInt16LittleEndian(dib[6..]),
            bits, 0, bits <= 8 ? 1 << bits : 0, 3, 0, 0, 0, 0, 0, 0, 0);
    }

    private static void Validate(int width, int height, int bitCount, int compression)
    {
        if (width <= 0 || height == 0 || !BitCounts.Contains(bitCount) || (long)width * Math.Abs((long)height) > RasterImage.MaxPixels)
        {
            throw new InvalidDataException("Unsupported or invalid DIB.");
        }

        if (!Compressions.Contains(compression))
        {
            throw new NotSupportedException($"DIB compression {compression} is not supported.");
        }
    }

    private static int PaletteSize(int used, int bitCount) => used > 0 ? Math.Min(used, 1 << bitCount) : 1 << bitCount;

    private static (uint Red, uint Green, uint Blue, uint Alpha) ReadMasks(ReadOnlySpan<byte> source, bool withAlpha) => (
        BinaryPrimitives.ReadUInt32LittleEndian(source),
        BinaryPrimitives.ReadUInt32LittleEndian(source[4..]),
        BinaryPrimitives.ReadUInt32LittleEndian(source[8..]),
        withAlpha ? BinaryPrimitives.ReadUInt32LittleEndian(source[12..]) : 0);
    public (uint R, uint G, uint B, uint A) Masks() => Compression is 3 or 6
        ? (RedMask, GreenMask, BlueMask, AlphaMask)
        : BitCount == 16 ? (0x7C00u, 0x03E0u, 0x001Fu, 0u) : (0x00FF0000u, 0x0000FF00u, 0x000000FFu, 0xFF000000u);
}

/// <summary>Run-length encoded DIBs (RLE8, RLE4), always bottom-up and opaque (RGB).</summary>
internal static class BmpRle
{
    public static void Decode(ReadOnlySpan<byte> data, DibHeader header, byte[] palette, RasterImage image)
    {
        var four = header.BitCount == 4;
        var x = 0;
        var y = image.Height - 1;
        var i = 0;
        while (i + 1 < data.Length && y >= 0)
        {
            int count = data[i];
            int value = data[i + 1];
            i += 2;
            if (count > 0)
            {
                for (var k = 0; k < count; k++)
                {
                    Put(image, palette, x++, y, Nibble(four, value, k));
                }
            }
            else if (value == 0)
            {
                x = 0;
                y--;
            }
            else if (value == 2 && i + 1 < data.Length)
            {
                x += data[i];
                y -= data[i + 1];
                i += 2;
            }
            else if (value is 1 or 2 || !Absolute(data, ref i, value, four, palette, image, ref x, y))
            {
                return;
            }
        }
    }

    // An absolute run: count indexes stored as is, padded to a 16-bit boundary; false when the data stops short.
    private static bool Absolute(ReadOnlySpan<byte> data, ref int i, int count, bool four, byte[] palette, RasterImage image, ref int x, int y)
    {
        var used = four ? (count + 1) / 2 : count;
        if (i + used > data.Length)
        {
            return false;
        }

        for (var k = 0; k < count; k++)
        {
            Put(image, palette, x++, y, Nibble(four, data[i + (four ? k / 2 : k)], k));
        }

        i += used + (used & 1);
        return true;
    }

    private static int Nibble(bool four, int value, int k) => !four ? value : k % 2 == 0 ? value >> 4 : value & 15;

    private static void Put(RasterImage image, byte[] palette, int x, int y, int index)
    {
        if (x >= image.Width || y < 0 || (index * 3) + 2 >= palette.Length)
        {
            return;
        }

        var o = (y * image.Stride) + (x * image.Components);
        image.Pixels[o] = palette[index * 3];
        image.Pixels[o + 1] = palette[(index * 3) + 1];
        image.Pixels[o + 2] = palette[(index * 3) + 2];
    }
}
