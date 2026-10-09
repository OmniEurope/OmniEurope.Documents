// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging.Jbig2;

namespace OmniEurope.Documents.Tests.Imaging.Jbig2;

/// <summary>Test bitmaps for the JBIG2 tests: deterministic patterns, rows written as text, comparisons.</summary>
internal static class Jbig2Images
{
    /// <summary>A pattern of strokes and dots with runs of repeated rows (for typical prediction), varying with <paramref name="seed"/>.</summary>
    public static Jbig2Bitmap Pattern(int width, int height, int seed)
    {
        var bitmap = new Jbig2Bitmap(width, height);
        for (var y = 0; y < height; y++)
        {
            var source = y % 5 == 2 ? y - 1 : y;
            for (var x = 0; x < width; x++)
            {
                bitmap[x, y] = ((x * 7) + (source * source * 3) + (seed * 11)) % 13 < 5 || (x + seed) % 9 == source % 9 ? 1 : 0;
            }
        }

        return bitmap;
    }

    /// <summary>A symbol: a frame with a mark, varying with <paramref name="seed"/>.</summary>
    public static Jbig2Bitmap Glyph(int width, int height, int seed)
    {
        var bitmap = new Jbig2Bitmap(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var edge = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                bitmap[x, y] = edge || (x + y + seed) % 4 == 0 ? 1 : 0;
            }
        }

        return bitmap;
    }

    /// <summary>Rows written with '#' for black and '.' for white, as pixels.</summary>
    public static byte[] Parse(params string[] rows) => [.. rows.SelectMany(r => r.Select(c => c == '#' ? (byte)1 : (byte)0))];

    public static Jbig2Bitmap Decode(byte[] data, int width, int height)
    {
        var page = Jbig2Decoder.Decode(data);
        Assert.Equal(width, page.Width);
        Assert.Equal(height, page.Height);
        return page;
    }

    public static void AssertSame(Jbig2Bitmap expected, Jbig2Bitmap actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        Assert.Equal(expected.Pixels, actual.Pixels);
    }

    /// <summary>The bitmap <paramref name="symbols"/> make placed with their top-left corners at the given points (OR).</summary>
    public static Jbig2Bitmap Compose(int width, int height, params (Jbig2Bitmap Symbol, int X, int Y)[] symbols)
    {
        var page = new Jbig2Bitmap(width, height);
        foreach (var (symbol, x0, y0) in symbols)
        {
            for (var y = 0; y < symbol.Height; y++)
            {
                for (var x = 0; x < symbol.Width; x++)
                {
                    if ((uint)(x0 + x) < (uint)width && (uint)(y0 + y) < (uint)height && symbol[x, y] == 1)
                    {
                        page[x0 + x, y0 + y] = 1;
                    }
                }
            }
        }

        return page;
    }
}
