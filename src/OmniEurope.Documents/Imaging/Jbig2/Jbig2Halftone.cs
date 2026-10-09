// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jbig2;

/// <summary>
/// Pattern dictionaries (T.88 6.7) and halftone regions (T.88 6.6): patterns cut from one collective bitmap, then a
/// grid of grey values, coded as Gray-coded bit planes (annex C.5), each cell drawing the pattern of its value.
/// </summary>
internal static class Jbig2Halftone
{
    /// <summary>Decodes a pattern dictionary segment's data into its patterns.</summary>
    public static Jbig2Bitmap[] Patterns(byte[] data, int start, int end)
    {
        var flags = Jbig2Bytes.Byte(data, start);
        var mmr = (flags & 1) != 0;
        var template = (flags >> 1) & 3;
        var width = Jbig2Bytes.Byte(data, start + 1);
        var height = Jbig2Bytes.Byte(data, start + 2);
        var grayMax = Jbig2Bytes.Int32(data, start + 3);
        if (width == 0 || height == 0 || grayMax < 0 || (long)(grayMax + 1L) * width > RasterImage.MaxPixels)
        {
            throw new InvalidDataException("A JBIG2 pattern dictionary has impossible dimensions.");
        }

        var count = grayMax + 1;
        var totalWidth = count * width;
        var collective = mmr
            ? Jbig2GenericDecoder.DecodeMmr(data, start + 7, end, totalWidth, height, out _)
            : new Jbig2GenericDecoder(template).Decode(new MqDecoder(data, start + 7, end), totalWidth, height,
                [(-width, 0), (-3, -1), (2, -2), (-2, -2)], typicalPrediction: false);
        var patterns = new Jbig2Bitmap[count];
        for (var i = 0; i < count; i++)
        {
            patterns[i] = collective.Extract(i * width, 0, width, height);
        }

        return patterns;
    }

    /// <summary>Decodes a halftone region segment's data (after its region information) into the region bitmap.</summary>
    public static Jbig2Bitmap Region(byte[] data, int start, int end, int width, int height, IReadOnlyList<Jbig2Bitmap> patterns)
    {
        if (patterns.Count == 0)
        {
            throw new InvalidDataException("A JBIG2 halftone region has no patterns.");
        }

        var flags = Jbig2Bytes.Byte(data, start);
        var grid = new Jbig2Grid(
            Jbig2Bytes.Int32(data, start + 1),
            Jbig2Bytes.Int32(data, start + 5),
            Jbig2Bytes.Int32(data, start + 9),
            Jbig2Bytes.Int32(data, start + 13),
            Jbig2Bytes.UInt16(data, start + 17),
            Jbig2Bytes.UInt16(data, start + 19));
        if (grid.Width < 0 || grid.Height < 0 || (long)grid.Width * grid.Height > RasterImage.MaxPixels)
        {
            throw new InvalidDataException("A JBIG2 halftone grid is out of range.");
        }

        var region = new Jbig2Bitmap(width, height, (byte)((flags >> 7) & 1));
        var skip = (flags & 8) != 0 ? Skip(grid, width, height, patterns[0]) : null;
        var bits = Jbig2SymbolDictionary.CodeLength(patterns.Count);
        var values = GrayValues(data, start + 21, end, grid, bits, (flags & 1) != 0, (flags >> 1) & 3, skip);
        var combination = (Jbig2Combination)Math.Min((flags >> 4) & 7, 4);
        for (var m = 0; m < grid.Height; m++)
        {
            for (var n = 0; n < grid.Width; n++)
            {
                var (x, y) = grid.Cell(m, n);
                var value = Math.Min(values[(m * grid.Width) + n], patterns.Count - 1);
                region.Combine(patterns[value], x, y, combination);
            }
        }

        return region;
    }

    // The cells whose pattern falls wholly outside the region (T.88 6.6.5.1).
    private static Jbig2Bitmap Skip(Jbig2Grid grid, int width, int height, Jbig2Bitmap pattern)
    {
        var skip = new Jbig2Bitmap(grid.Width, grid.Height);
        for (var m = 0; m < grid.Height; m++)
        {
            for (var n = 0; n < grid.Width; n++)
            {
                var (x, y) = grid.Cell(m, n);
                if (x + pattern.Width <= 0 || x >= width || y + pattern.Height <= 0 || y >= height)
                {
                    skip[n, m] = 1;
                }
            }
        }

        return skip;
    }

    // The grey value of each cell, from its bit planes, most significant first, Gray coded (T.88 C.5).
    private static int[] GrayValues(byte[] data, int start, int end, Jbig2Grid grid, int bits, bool mmr, int template, Jbig2Bitmap? skip)
    {
        var values = new int[grid.Width * grid.Height];
        var generic = new Jbig2GenericDecoder(template);
        var decoder = mmr ? null : new MqDecoder(data, start, end);
        (int X, int Y)[] adaptive = template == 0 ? [(3, -1), (-3, -1), (2, -2), (-2, -2)] : [(template <= 1 ? 3 : 2, -1)];
        Jbig2Bitmap? previous = null;
        var at = start;
        for (var plane = bits - 1; plane >= 0; plane--)
        {
            var bitmap = decoder is null
                ? Jbig2GenericDecoder.DecodeMmr(data, at, end, grid.Width, grid.Height, out at)
                : generic.Decode(decoder, grid.Width, grid.Height, adaptive, typicalPrediction: false, skip);
            if (previous is not null)
            {
                for (var i = 0; i < bitmap.Pixels.Length; i++)
                {
                    bitmap.Pixels[i] ^= previous.Pixels[i];
                }
            }

            for (var i = 0; i < values.Length; i++)
            {
                values[i] |= bitmap.Pixels[i] << plane;
            }

            previous = bitmap;
        }

        return values;
    }
}

/// <summary>A halftone grid (T.88 7.4.5.1.2): its size, origin and vector, in 1/256 pixel.</summary>
internal readonly record struct Jbig2Grid(int Width, int Height, int X, int Y, int StepX, int StepY)
{
    /// <summary>The top-left pixel of cell (row <paramref name="m"/>, column <paramref name="n"/>).</summary>
    public (int X, int Y) Cell(int m, int n)
    {
        var x = ((long)X + ((long)m * StepY) + ((long)n * StepX)) >> 8;
        var y = ((long)Y + ((long)m * StepX) - ((long)n * StepY)) >> 8;
        return ((int)Math.Clamp(x, int.MinValue / 2, int.MaxValue / 2), (int)Math.Clamp(y, int.MinValue / 2, int.MaxValue / 2));
    }
}
