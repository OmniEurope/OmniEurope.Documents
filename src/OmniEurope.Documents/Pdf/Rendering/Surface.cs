// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;

namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>Coverage of a clipping path, one byte per pixel (255 inside).</summary>
internal sealed class ClipMask
{
    private ClipMask(int width, int height, byte[] alpha)
    {
        Width = width;
        Height = height;
        Alpha = alpha;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Alpha { get; }

    /// <summary>The intersection of <paramref name="current"/> (null for the whole page) with a path.</summary>
    public static ClipMask Intersect(ClipMask? current, IEnumerable<IReadOnlyList<(double X, double Y)>> polygons, bool evenOdd, int width, int height)
    {
        var alpha = new byte[width * height];
        Rasterizer.Fill(polygons, evenOdd, width, height, (y, left, coverage) =>
        {
            for (var i = 0; i < coverage.Length; i++)
            {
                var index = (y * width) + left + i;
                var value = (int)Math.Round(coverage[i] * 255);
                alpha[index] = current is null ? (byte)value : (byte)(value * current.Alpha[index] / 255);
            }
        });
        return new ClipMask(width, height, alpha);
    }
}

/// <summary>The page being drawn: opaque RGB pixels on a white background.</summary>
internal sealed class Surface
{
    public Surface(int width, int height)
    {
        Width = width;
        Height = height;
        Pixels = new byte[width * height * 3];
        Array.Fill(Pixels, (byte)255);
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    /// <summary>Fills polygons with a colour, through the clip and with an opacity.</summary>
    public void Fill(IEnumerable<IReadOnlyList<(double X, double Y)>> polygons, bool evenOdd, PdfColor color, double opacity, ClipMask? clip)
    {
        if (opacity <= 0)
        {
            return;
        }

        Rasterizer.Fill(polygons, evenOdd, Width, Height, (y, left, coverage) =>
        {
            for (var i = 0; i < coverage.Length; i++)
            {
                Blend(left + i, y, color.R, color.G, color.B, coverage[i] * opacity, clip);
            }
        });
    }

    /// <summary>Fills polygons with a colour computed per pixel (null leaves the pixel untouched).</summary>
    public void Fill(IEnumerable<IReadOnlyList<(double X, double Y)>> polygons, bool evenOdd, Func<int, int, PdfColor?> colorAt, double opacity, ClipMask? clip)
    {
        Rasterizer.Fill(polygons, evenOdd, Width, Height, (y, left, coverage) =>
        {
            for (var i = 0; i < coverage.Length; i++)
            {
                if (coverage[i] > 0 && colorAt(left + i, y) is { } color)
                {
                    Blend(left + i, y, color.R, color.G, color.B, coverage[i] * opacity, clip);
                }
            }
        });
    }

    /// <summary>Mixes one pixel towards a colour by <paramref name="amount"/> (0 to 1), through the clip.</summary>
    public void Blend(int x, int y, byte r, byte g, byte b, double amount, ClipMask? clip)
    {
        if (clip is not null)
        {
            amount *= clip.Alpha[(y * Width) + x] / 255.0;
        }

        if (amount <= 0.002)
        {
            return;
        }

        amount = Math.Min(1, amount);
        var index = ((y * Width) + x) * 3;
        Pixels[index] = Mix(Pixels[index], r, amount);
        Pixels[index + 1] = Mix(Pixels[index + 1], g, amount);
        Pixels[index + 2] = Mix(Pixels[index + 2], b, amount);
    }

    private static byte Mix(byte under, byte over, double amount) => (byte)Math.Round(under + ((over - under) * amount));

    public RasterImage ToImage()
    {
        var image = new RasterImage(Width, Height, ImageColorType.Rgb);
        Pixels.CopyTo(image.Pixels, 0);
        return image;
    }
}
