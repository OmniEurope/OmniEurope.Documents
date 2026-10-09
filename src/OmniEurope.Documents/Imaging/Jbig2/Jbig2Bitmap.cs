// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jbig2;

/// <summary>How a bitmap is combined onto another (T.88 7.4.6 external combination operator).</summary>
internal enum Jbig2Combination
{
    Or,
    And,
    Xor,
    Xnor,
    Replace,
}

/// <summary>A JBIG2 bitmap, one byte per pixel (1 is black); pixels outside it read as 0.</summary>
internal sealed class Jbig2Bitmap
{
    public Jbig2Bitmap(int width, int height, byte fill = 0)
    {
        if (width < 0 || height < 0 || (long)width * height > RasterImage.MaxPixels)
        {
            throw new InvalidDataException($"A JBIG2 bitmap of {width} by {height} pixels is out of range.");
        }

        Width = width;
        Height = height;
        Pixels = new byte[width * height];
        if (fill != 0)
        {
            Array.Fill(Pixels, (byte)1);
        }
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    public int this[int x, int y]
    {
        get => (uint)x < (uint)Width && (uint)y < (uint)Height ? Pixels[(y * Width) + x] : 0;
        set => Pixels[(y * Width) + x] = (byte)value;
    }

    /// <summary>Combines <paramref name="source"/> onto this bitmap with its top-left corner at (x, y).</summary>
    public void Combine(Jbig2Bitmap source, int x, int y, Jbig2Combination operation)
    {
        var top = Math.Max(0, -y);
        var bottom = Math.Min(source.Height, Height - y);
        var left = Math.Max(0, -x);
        var right = Math.Min(source.Width, Width - x);
        for (var row = top; row < bottom; row++)
        {
            var target = ((row + y) * Width) + x;
            var from = row * source.Width;
            for (var column = left; column < right; column++)
            {
                var s = source.Pixels[from + column];
                ref var d = ref Pixels[target + column];
                d = operation switch
                {
                    Jbig2Combination.Or => (byte)(d | s),
                    Jbig2Combination.And => (byte)(d & s),
                    Jbig2Combination.Xor => (byte)(d ^ s),
                    Jbig2Combination.Xnor => (byte)(1 - (d ^ s)),
                    _ => s,
                };
            }
        }
    }

    /// <summary>The part of this bitmap at (x, y), <paramref name="width"/> by <paramref name="height"/>.</summary>
    public Jbig2Bitmap Extract(int x, int y, int width, int height)
    {
        var part = new Jbig2Bitmap(width, height);
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                part.Pixels[(row * width) + column] = (byte)this[x + column, y + row];
            }
        }

        return part;
    }

    /// <summary>The same bitmap grown to <paramref name="height"/> rows (a page of unknown height that a stripe extends).</summary>
    public Jbig2Bitmap Grow(int height, byte fill)
    {
        var grown = new Jbig2Bitmap(Width, height, fill);
        Array.Copy(Pixels, grown.Pixels, Math.Min(Pixels.Length, grown.Pixels.Length));
        return grown;
    }
}
