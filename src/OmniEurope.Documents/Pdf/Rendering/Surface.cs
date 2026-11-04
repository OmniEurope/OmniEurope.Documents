// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf.Text;

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
    public static ClipMask? Intersect(ClipMask? current, IEnumerable<IReadOnlyList<(double X, double Y)>> polygons, bool evenOdd, int width, int height)
    {
        // A rectangle covering the whole surface keeps every pixel at full coverage: the clip is unchanged (Word
        // exports open many drawings with one), so the full-page mask is neither allocated nor rasterised.
        if (CoversSurface(polygons, width, height))
        {
            return current;
        }

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

    // How far inside the surface an edge of a covering rectangle may lie: a border pixel then keeps a coverage above
    // 0.999, which rounds to 255 like an inner one (Word places its page rectangle a few hundred-thousandths in).
    private const double EdgeTolerance = 1.0 / 1024;

    // One axis-aligned rectangle (four corners, the first possibly repeated at the end) reaching every edge.
    private static bool CoversSurface(IEnumerable<IReadOnlyList<(double X, double Y)>> polygons, int width, int height)
    {
        using var enumerator = polygons.GetEnumerator();
        var single = enumerator.MoveNext() ? enumerator.Current : null;
        if (single is null || enumerator.MoveNext() || AxisRectangle(single) is not { } box)
        {
            return false;
        }

        return box.MinX <= EdgeTolerance && box.MinY <= EdgeTolerance && box.MaxX >= width - EdgeTolerance && box.MaxY >= height - EdgeTolerance;
    }

    // The bounds of a polygon of four corners (the first possibly repeated) whose every side is horizontal or vertical.
    private static (double MinX, double MinY, double MaxX, double MaxY)? AxisRectangle(IReadOnlyList<(double X, double Y)> points)
    {
        if (points.Count is not (4 or 5))
        {
            return null;
        }

        var box = (MinX: double.MaxValue, MinY: double.MaxValue, MaxX: double.MinValue, MaxY: double.MinValue);
        for (var i = 0; i < points.Count; i++)
        {
            var (p, q) = (points[i], points[(i + 1) % points.Count]);
            if (!double.IsFinite(p.X + p.Y) || (p.X != q.X && p.Y != q.Y))
            {
                return null;
            }

            box = (Math.Min(box.MinX, p.X), Math.Min(box.MinY, p.Y), Math.Max(box.MaxX, p.X), Math.Max(box.MaxY, p.Y));
        }

        return box;
    }
}

/// <summary>A rectangle of device pixels, right and bottom excluded.</summary>
internal readonly record struct PixelBounds(int Left, int Top, int Right, int Bottom)
{
    public int Width => Math.Max(0, Right - Left);

    public int Height => Math.Max(0, Bottom - Top);

    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;

    public PixelBounds Intersect(PixelBounds other) =>
        new(Math.Max(Left, other.Left), Math.Max(Top, other.Top), Math.Min(Right, other.Right), Math.Min(Bottom, other.Bottom));

    /// <summary>The pixels touched by a rectangle of user space placed by a matrix, within a width and height.</summary>
    public static PixelBounds Of(Matrix toDevice, (double X0, double Y0, double X1, double Y1) box, int width, int height)
    {
        (double X, double Y)[] corners = [toDevice.Transform(box.X0, box.Y0), toDevice.Transform(box.X1, box.Y0), toDevice.Transform(box.X0, box.Y1), toDevice.Transform(box.X1, box.Y1)];
        var left = Math.Floor(Math.Max(0, corners.Min(c => c.X)));
        var top = Math.Floor(Math.Max(0, corners.Min(c => c.Y)));
        var right = Math.Ceiling(Math.Min(width, corners.Max(c => c.X)));
        var bottom = Math.Ceiling(Math.Min(height, corners.Max(c => c.Y)));
        return double.IsFinite(left + top + right + bottom) ? new((int)left, (int)top, (int)right, (int)bottom) : default;
    }
}

/// <summary>
/// How a painted object combines with what lies under it: the clip (shape), the blend mode, the soft mask and
/// whether the soft mask and alpha constant count as shape rather than opacity (the AIS flag).
/// </summary>
internal readonly record struct Compositing(ClipMask? Clip, BlendMode Mode, SoftMask? Mask, bool AlphaIsShape);

/// <summary>
/// Something to paint on: the page or a transparency group. Colours are 0 to 255 per component; each painted pixel
/// has a shape (its coverage) and an opacity, combined with the compositing of the graphics state.
/// </summary>
internal abstract class Canvas(int width, int height)
{
    /// <summary>Width of the coordinate space (the clip masks and soft masks have this width).</summary>
    public int Width { get; } = width;

    public int Height { get; } = height;

    /// <summary>The pixels that hold paint.</summary>
    public abstract PixelBounds Bounds { get; }

    /// <summary>Fills polygons with a colour, through the clip and with an opacity.</summary>
    public void Fill(IEnumerable<IReadOnlyList<(double X, double Y)>> polygons, bool evenOdd, PdfColor color, double opacity, Compositing compositing)
    {
        if (opacity <= 0)
        {
            return;
        }

        Rasterizer.Fill(polygons, evenOdd, Width, Height, (y, left, coverage) =>
        {
            for (var i = 0; i < coverage.Length; i++)
            {
                Composite(left + i, y, color.R, color.G, color.B, coverage[i], opacity, compositing);
            }
        });
    }

    /// <summary>Fills polygons with a pattern; pixels where the pattern holds nothing are left untouched.</summary>
    public void Fill(IEnumerable<IReadOnlyList<(double X, double Y)>> polygons, bool evenOdd, PatternPaint paint, double opacity, Compositing compositing)
    {
        Rasterizer.Fill(polygons, evenOdd, Width, Height, (y, left, coverage) =>
        {
            for (var i = 0; i < coverage.Length; i++)
            {
                if (coverage[i] > 0 && paint.At(left + i, y) is { } color)
                {
                    Composite(left + i, y, color.R, color.G, color.B, coverage[i], opacity * color.A, compositing);
                }
            }
        });
    }

    /// <summary>Paints one pixel of an object: its colour, shape and opacity, through the compositing.</summary>
    public abstract void Composite(int x, int y, double r, double g, double b, double shape, double opacity, Compositing compositing);

    /// <summary>The colour (0 to 255) and alpha (0 to 1) of a pixel.</summary>
    public abstract (double R, double G, double B, double A) Read(int x, int y);
}

/// <summary>The page being drawn: opaque RGB pixels on a white background, the backdrop of the page group.</summary>
internal sealed class Surface : Canvas
{
    public Surface(int width, int height)
        : base(width, height)
    {
        Pixels = new byte[width * height * 3];
        Array.Fill(Pixels, (byte)255);
    }

    public byte[] Pixels { get; }

    public override PixelBounds Bounds => new(0, 0, Width, Height);

    // The backdrop is opaque: the result is (1 - as) Cb + as B(Cb, Cs), as being the shape times the opacity.
    public override void Composite(int x, int y, double r, double g, double b, double shape, double opacity, Compositing compositing)
    {
        var amount = shape * opacity;
        if (compositing.Clip is not null)
        {
            amount *= compositing.Clip.Alpha[(y * Width) + x] / 255.0;
        }

        if (compositing.Mask is not null)
        {
            amount *= compositing.Mask.Value(x, y);
        }

        if (amount <= 0.002)
        {
            return;
        }

        amount = Math.Min(1, amount);
        var index = ((y * Width) + x) * 3;
        if (compositing.Mode != BlendMode.Normal)
        {
            var blended = Blending.Apply(compositing.Mode, (Pixels[index] / 255.0, Pixels[index + 1] / 255.0, Pixels[index + 2] / 255.0), (r / 255.0, g / 255.0, b / 255.0));
            (r, g, b) = (blended.R * 255, blended.G * 255, blended.B * 255);
        }

        Pixels[index] = Mix(Pixels[index], r, amount);
        Pixels[index + 1] = Mix(Pixels[index + 1], g, amount);
        Pixels[index + 2] = Mix(Pixels[index + 2], b, amount);
    }

    public override (double R, double G, double B, double A) Read(int x, int y)
    {
        var index = ((y * Width) + x) * 3;
        return (Pixels[index], Pixels[index + 1], Pixels[index + 2], 1);
    }

    private static byte Mix(byte under, double over, double amount) => (byte)Math.Clamp(Math.Round(under + ((over - under) * amount)), 0, 255);

    public RasterImage ToImage()
    {
        var image = new RasterImage(Width, Height, ImageColorType.Rgb);
        Pixels.CopyTo(image.Pixels, 0);
        return image;
    }
}
