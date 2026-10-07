// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging;

/// <summary>Geometry of a TIFF page and of its strips or tiles.</summary>
internal sealed record TiffLayout(int Width, int Height, int Samples, int Bits, int ChunkWidth, int ChunkHeight, bool LittleEndian)
{
    public int RowBytes => (int)ChunkRowBytes(Width);

    public int ChunkRowBytes(int columns) => (int)((((long)columns * Samples * Bits) + 7) / 8);

    public int ByteOffsetOfColumn(int column) => (int)((long)column * Samples * Bits / 8);
}

/// <summary>Turns decompressed TIFF samples into an 8-bit image.</summary>
internal sealed class TiffSamples(TiffLayout layout, int photometric, uint[] colorMap, bool extraAlpha)
{
    public RasterImage ToImage(byte[] raw)
    {
        var type = OutputType();
        var image = new RasterImage(layout.Width, layout.Height, type);
        var o = 0;
        for (var y = 0; y < layout.Height; y++)
        {
            var row = raw.AsSpan(y * layout.RowBytes, layout.RowBytes);
            for (var x = 0; x < layout.Width; x++)
            {
                o = WritePixel(row, x, image.Pixels, o);
            }
        }

        return image;
    }

    private ImageColorType OutputType() => photometric switch
    {
        3 => ImageColorType.Rgb,
        2 => extraAlpha && layout.Samples >= 4 ? ImageColorType.Rgba : ImageColorType.Rgb,
        5 => ImageColorType.Cmyk,
        _ => extraAlpha && layout.Samples >= 2 ? ImageColorType.GrayAlpha : ImageColorType.Gray,
    };

    private int WritePixel(ReadOnlySpan<byte> row, int x, byte[] output, int o)
    {
        switch (photometric)
        {
            case 3:
                var index = (int)Sample(row, x, 0);
                var entries = colorMap.Length / 3;
                for (var c = 0; c < 3; c++)
                {
                    output[o + c] = index < entries ? (byte)(colorMap[(c * entries) + index] >> 8) : (byte)0;
                }

                return o + 3;
            case 2 or 5:
                var channels = photometric == 5 || (extraAlpha && layout.Samples >= 4) ? 4 : 3;
                for (var c = 0; c < channels; c++)
                {
                    output[o + c] = Scale(Sample(row, x, c));
                }

                return o + channels;
            default:
                var gray = Scale(Sample(row, x, 0));
                output[o] = photometric == 0 ? (byte)(255 - gray) : gray;
                if (extraAlpha && layout.Samples >= 2)
                {
                    output[o + 1] = Scale(Sample(row, x, 1));
                    return o + 2;
                }

                return o + 1;
        }
    }

    private uint Sample(ReadOnlySpan<byte> row, int x, int channel)
    {
        var bits = layout.Bits;
        var index = ((long)x * layout.Samples) + channel;
        return bits switch
        {
            8 => row[(int)index],
            16 when layout.LittleEndian => (uint)((row[(int)(index * 2) + 1] << 8) | row[(int)(index * 2)]),
            16 => (uint)((row[(int)(index * 2)] << 8) | row[(int)(index * 2) + 1]),
            _ => (uint)((row[(int)(index * bits >> 3)] >> (8 - bits - (int)(index * bits & 7))) & ((1 << bits) - 1)),
        };
    }

    private byte Scale(uint sample) => layout.Bits switch
    {
        8 => (byte)sample,
        16 => (byte)(sample >> 8),
        _ => (byte)(sample * 255 / ((1u << layout.Bits) - 1)),
    };
}

/// <summary>TIFF LZW: codes of 9 to 12 bits, most significant bit first, width growing one code early.</summary>
internal static class TiffLzw
{
    public static byte[] Decode(byte[] data, int expected)
    {
        var output = new byte[expected];
        var table = new byte[4096][];
        for (var i = 0; i < 256; i++)
        {
            table[i] = [(byte)i];
        }

        var next = 258;
        var width = 9;
        byte[]? previous = null;
        var o = 0;
        long bit = 0;
        var totalBits = (long)data.Length * 8;
        while (bit + width <= totalBits && o < expected)
        {
            var code = 0;
            for (var i = 0; i < width; i++, bit++)
            {
                code = (code << 1) | ((data[bit >> 3] >> (7 - (int)(bit & 7))) & 1);
            }

            if (code == 257)
            {
                break;
            }

            if (code == 256)
            {
                next = 258;
                width = 9;
                previous = null;
                continue;
            }

            byte[] entry;
            if (code < next && table[code] is { } known)
            {
                entry = known;
            }
            else if (previous is not null && code == next)
            {
                entry = [.. previous, previous[0]];
            }
            else
            {
                throw new InvalidDataException("Invalid TIFF LZW code.");
            }

            var count = Math.Min(entry.Length, expected - o);
            Array.Copy(entry, 0, output, o, count);
            o += count;
            if (previous is not null && next < 4096)
            {
                table[next++] = [.. previous, entry[0]];
            }

            previous = entry;
            width = next + 1 >= 1 << width && width < 12 ? width + 1 : width;
        }

        return output;
    }
}

/// <summary>PackBits run-length decoding (Apple, TIFF compression 32773).</summary>
internal static class PackBits
{
    public static byte[] Decode(byte[] data, int expected)
    {
        var output = new byte[expected];
        var o = 0;
        var i = 0;
        while (i < data.Length && o < expected)
        {
            var n = (sbyte)data[i++];
            if (n >= 0)
            {
                var count = Math.Min(Math.Min(n + 1, data.Length - i), expected - o);
                Array.Copy(data, i, output, o, count);
                i += n + 1;
                o += count;
            }
            else if (n != -128 && i < data.Length)
            {
                var count = Math.Min(1 - n, expected - o);
                output.AsSpan(o, count).Fill(data[i++]);
                o += count;
            }
        }

        return output;
    }
}

/// <summary>Reverses the TIFF horizontal differencing predictor on 8- and 16-bit samples.</summary>
internal static class HorizontalPredictor
{
    public static void Undo(byte[] bytes, TiffLayout layout, int columns, int rows)
    {
        var rowBytes = layout.ChunkRowBytes(columns);
        var samples = layout.Samples;
        for (var y = 0; y < rows; y++)
        {
            var start = y * rowBytes;
            if (start + rowBytes > bytes.Length)
            {
                return;
            }

            if (layout.Bits == 8)
            {
                for (var i = samples; i < rowBytes; i++)
                {
                    bytes[start + i] += bytes[start + i - samples];
                }
            }
            else if (layout.Bits == 16)
            {
                // Samples are in the file byte order.
                var (high, low) = layout.LittleEndian ? (1, 0) : (0, 1);
                for (var i = samples * 2; i + 1 < rowBytes; i += 2)
                {
                    var left = start + i - (samples * 2);
                    var value = ((bytes[start + i + high] << 8) | bytes[start + i + low]) + ((bytes[left + high] << 8) | bytes[left + low]);
                    bytes[start + i + high] = (byte)(value >> 8);
                    bytes[start + i + low] = (byte)value;
                }
            }
        }
    }
}
