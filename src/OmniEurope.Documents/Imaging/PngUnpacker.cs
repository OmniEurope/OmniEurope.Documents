// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging;

/// <summary>Reverses PNG row filters, de-interlaces Adam7 passes and expands samples to 8 bits.</summary>
internal sealed class PngUnpacker(PngHeader header, byte[]? palette, byte[]? transparency)
{
    internal readonly record struct Pass(int StartX, int StartY, int StepX, int StepY)
    {
        public (int Width, int Height) Size(int width, int height) =>
            ((width - StartX + StepX - 1) / StepX, (height - StartY + StepY - 1) / StepY);
    }

    internal static readonly Pass[] Passes =
    [
        new(0, 0, 8, 8), new(4, 0, 8, 8), new(0, 4, 4, 8), new(2, 0, 4, 4), new(0, 2, 2, 4), new(1, 0, 2, 2), new(0, 1, 1, 2),
    ];

    public RasterImage Unpack(byte[] raw)
    {
        var image = new RasterImage(header.Width, header.Height, OutputType());
        if (!header.Interlaced)
        {
            Decode(raw, 0, new Pass(0, 0, 1, 1), image);
            return image;
        }

        var offset = 0;
        foreach (var pass in Passes)
        {
            offset = Decode(raw, offset, pass, image);
        }

        return image;
    }

    private ImageColorType OutputType() => header.ColorType switch
    {
        0 => transparency is { Length: >= 2 } ? ImageColorType.GrayAlpha : ImageColorType.Gray,
        2 => transparency is { Length: >= 6 } ? ImageColorType.Rgba : ImageColorType.Rgb,
        3 => transparency is { Length: > 0 } ? ImageColorType.Rgba : ImageColorType.Rgb,
        4 => ImageColorType.GrayAlpha,
        _ => ImageColorType.Rgba,
    };

    // Unfilters one pass (or the whole image) in place and writes its pixels; returns the next offset.
    private int Decode(byte[] raw, int offset, Pass pass, RasterImage image)
    {
        var (width, height) = pass.Size(header.Width, header.Height);
        if (width <= 0 || height <= 0)
        {
            return offset;
        }

        var rowBytes = (int)header.RowBytes(width) - 1;
        var bpp = Math.Max(1, header.BitsPerPixel / 8);
        for (var y = 0; y < height; y++)
        {
            var filter = raw[offset];
            var row = raw.AsSpan(offset + 1, rowBytes);
            var previous = y == 0 ? Span<byte>.Empty : raw.AsSpan(offset - rowBytes, rowBytes);
            Unfilter(filter, row, previous, bpp);
            for (var x = 0; x < width; x++)
            {
                WritePixel(row, x, image, pass.StartX + (x * pass.StepX), pass.StartY + (y * pass.StepY));
            }

            offset += rowBytes + 1;
        }

        return offset;
    }

    // The first row has no previous row: previous is then empty and reads as zeros.
    private static void Unfilter(byte filter, Span<byte> row, ReadOnlySpan<byte> previous, int bpp)
    {
        switch (filter)
        {
            case 0:
                return;
            case 1:
                Sub(row, bpp);
                return;
            case 2:
                Up(row, previous);
                return;
            case 3:
                Average(row, previous, bpp);
                return;
            case 4:
                PaethRow(row, previous, bpp);
                return;
            default:
                throw new InvalidDataException($"Unknown PNG filter type {filter}.");
        }
    }

    private static void Sub(Span<byte> row, int bpp)
    {
        for (var i = bpp; i < row.Length; i++)
        {
            row[i] += row[i - bpp];
        }
    }

    private static void Up(Span<byte> row, ReadOnlySpan<byte> previous)
    {
        for (var i = 0; i < previous.Length; i++)
        {
            row[i] += previous[i];
        }
    }

    private static void Average(Span<byte> row, ReadOnlySpan<byte> previous, int bpp)
    {
        for (var i = 0; i < row.Length; i++)
        {
            var left = i >= bpp ? row[i - bpp] : 0;
            var up = previous.IsEmpty ? 0 : previous[i];
            row[i] += (byte)((left + up) >> 1);
        }
    }

    private static void PaethRow(Span<byte> row, ReadOnlySpan<byte> previous, int bpp)
    {
        for (var i = 0; i < row.Length; i++)
        {
            var left = i >= bpp ? row[i - bpp] : (byte)0;
            var up = previous.IsEmpty ? (byte)0 : previous[i];
            var upLeft = !previous.IsEmpty && i >= bpp ? previous[i - bpp] : (byte)0;
            row[i] += Paeth(left, up, upLeft);
        }
    }

    internal static byte Paeth(byte a, byte b, byte c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private void WritePixel(ReadOnlySpan<byte> row, int x, RasterImage image, int px, int py)
    {
        var o = (py * image.Stride) + (px * image.Components);
        var target = image.Pixels;
        switch (header.ColorType)
        {
            case 3:
                WritePalette(Sample(row, x, 0), target, o, image.Components);
                break;
            case 0:
                var gray = Sample(row, x, 0);
                target[o] = Scale(gray);
                if (image.Components == 2)
                {
                    target[o + 1] = gray == TransparentSample(0) ? (byte)0 : (byte)255;
                }

                break;
            default:
                WriteChannels(row, x, target, o, image.Components);
                break;
        }
    }

    private void WriteChannels(ReadOnlySpan<byte> row, int x, byte[] target, int o, int components)
    {
        var channels = header.Channels;
        var matchesKey = true;
        for (var c = 0; c < channels; c++)
        {
            var sample = Sample(row, x, c);
            target[o + c] = Scale(sample);
            matchesKey &= sample == TransparentSample(c);
        }

        if (components > channels)
        {
            // Only an RGB image with a tRNS key colour gains an alpha channel here.
            target[o + channels] = matchesKey ? (byte)0 : (byte)255;
        }
    }

    private void WritePalette(int index, byte[] target, int o, int components)
    {
        if (palette is null || (index * 3) + 2 >= palette.Length)
        {
            throw new InvalidDataException("PNG palette index out of range.");
        }

        target[o] = palette[index * 3];
        target[o + 1] = palette[(index * 3) + 1];
        target[o + 2] = palette[(index * 3) + 2];
        if (components == 4)
        {
            target[o + 3] = transparency is not null && index < transparency.Length ? transparency[index] : (byte)255;
        }
    }

    // The raw sample value (before scaling) of channel c of pixel x.
    private int Sample(ReadOnlySpan<byte> row, int x, int c)
    {
        var depth = header.BitDepth;
        if (depth == 16)
        {
            var i = ((x * header.Channels) + c) * 2;
            return (row[i] << 8) | row[i + 1];
        }

        if (depth == 8)
        {
            return row[(x * header.Channels) + c];
        }

        var bit = x * depth;
        return (row[bit >> 3] >> (8 - depth - (bit & 7))) & ((1 << depth) - 1);
    }

    private byte Scale(int sample) => header.BitDepth switch
    {
        16 => (byte)(sample >> 8),
        8 => (byte)sample,
        _ => (byte)(sample * 255 / ((1 << header.BitDepth) - 1)),
    };

    private int TransparentSample(int channel) =>
        transparency is null || transparency.Length < (channel * 2) + 2
            ? -1
            : (transparency[channel * 2] << 8) | transparency[(channel * 2) + 1];
}

/// <summary>Chooses a filter per row (the one with the smallest sum of absolute values) for encoding.</summary>
internal static class PngFilters
{
    public static byte[] FilterRows(RasterImage image)
    {
        var stride = image.Stride;
        var bpp = image.Components;
        var output = new byte[(long)(stride + 1) * image.Height];
        var candidate = new byte[stride];
        var best = new byte[stride];
        for (var y = 0; y < image.Height; y++)
        {
            var row = image.Pixels.AsSpan(y * stride, stride);
            var previous = y == 0 ? ReadOnlySpan<byte>.Empty : image.Pixels.AsSpan((y - 1) * stride, stride);
            var bestFilter = 0;
            var bestScore = long.MaxValue;
            for (var filter = 0; filter < 5; filter++)
            {
                var score = Apply(filter, row, previous, bpp, candidate);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestFilter = filter;
                    candidate.CopyTo(best, 0);
                }
            }

            var o = (long)y * (stride + 1);
            output[o] = (byte)bestFilter;
            best.CopyTo(output.AsSpan((int)o + 1, stride));
        }

        return output;
    }

    private static long Apply(int filter, ReadOnlySpan<byte> row, ReadOnlySpan<byte> previous, int bpp, byte[] output)
    {
        long score = 0;
        for (var i = 0; i < row.Length; i++)
        {
            var left = i >= bpp ? row[i - bpp] : (byte)0;
            var up = previous.IsEmpty ? (byte)0 : previous[i];
            var upLeft = !previous.IsEmpty && i >= bpp ? previous[i - bpp] : (byte)0;
            var value = filter switch
            {
                0 => row[i],
                1 => (byte)(row[i] - left),
                2 => (byte)(row[i] - up),
                3 => (byte)(row[i] - ((left + up) >> 1)),
                _ => (byte)(row[i] - PngUnpacker.Paeth(left, up, upLeft)),
            };
            output[i] = value;
            score += value < 128 ? value : 256 - value;
        }

        return score;
    }
}
