// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;

namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>Options of <see cref="PdfPageComparison"/>.</summary>
public sealed record PdfPageComparisonOptions
{
    /// <summary>Largest accepted neighbourhood radius.</summary>
    public const int MaxNeighbourhoodRadius = 8;

    /// <summary>Resolution used when pages are rendered (pages are compared pixel for pixel at this scale).</summary>
    public double Dpi { get; init; } = 150;

    /// <summary>Largest tolerated difference (0 to 255) on every colour channel for two pixels to match.</summary>
    public int ChannelThreshold { get; init; } = 32;

    /// <summary>
    /// A pixel of one page matches when some pixel of the other page within this many pixels (square window)
    /// matches it, so anti-aliasing and small shifts are not counted. 0 compares position for position.
    /// </summary>
    public int NeighbourhoodRadius { get; init; } = 1;

    /// <summary>Produces <see cref="PdfPageComparisonResult.DiffPng"/>.</summary>
    public bool ProduceDiffImage { get; init; } = true;

    internal void Validate()
    {
        if (!(Dpi >= 1 && Dpi <= 1200))
        {
            throw new ArgumentException("The resolution must be between 1 and 1200 dpi.", nameof(Dpi));
        }

        if (ChannelThreshold is < 0 or > 255)
        {
            throw new ArgumentException("The channel threshold must be between 0 and 255.", nameof(ChannelThreshold));
        }

        if (NeighbourhoodRadius is < 0 or > MaxNeighbourhoodRadius)
        {
            throw new ArgumentException($"The neighbourhood radius must be between 0 and {MaxNeighbourhoodRadius}.", nameof(NeighbourhoodRadius));
        }
    }
}

/// <summary>The outcome of comparing two rendered pages.</summary>
/// <param name="Width">Width in pixels of the compared area (the common area when the sizes differ).</param>
/// <param name="Height">Height in pixels of the compared area.</param>
/// <param name="DifferingPixels">Pixels of the compared area where either page has no matching pixel on the other.</param>
/// <param name="TotalPixels">Pixels of the compared area.</param>
/// <param name="InkPixelsA">Non-white pixels of the first page inside the compared area.</param>
/// <param name="InkPixelsB">Non-white pixels of the second page inside the compared area.</param>
/// <param name="SizeMismatch">The two images do not have the same size; only the common area was compared.</param>
/// <param name="DiffPng">Differing pixels in red over a faded first page, or null when not requested.</param>
public sealed record PdfPageComparisonResult(
    int Width,
    int Height,
    long DifferingPixels,
    long TotalPixels,
    long InkPixelsA,
    long InkPixelsB,
    bool SizeMismatch,
    byte[]? DiffPng)
{
    /// <summary>Differing pixels over all pixels, from 0 to 1.</summary>
    public double DifferingRatio => TotalPixels == 0 ? 0 : (double)DifferingPixels / TotalPixels;

    /// <summary>
    /// Differing pixels over the larger ink count, from 0 to 1: a mostly blank page makes
    /// <see cref="DifferingRatio"/> look tiny, this ratio does not. 0 when both pages are blank.
    /// </summary>
    public double DifferingRatioOfInk
    {
        get
        {
            var ink = Math.Max(InkPixelsA, InkPixelsB);
            return ink == 0 ? 0 : Math.Min(1, (double)DifferingPixels / ink);
        }
    }
}

/// <summary>
/// Compares two rendered pages honestly: a pixel differs only when no pixel within the neighbourhood of the
/// other page is close to it in colour. Deterministic, and bounded by the rendering pixel limit.
/// </summary>
public static class PdfPageComparison
{
    private const long MaxComparedPixels = RasterImage.MaxPixels / 3;

    /// <summary>Renders both pages at <see cref="PdfPageComparisonOptions.Dpi"/> and compares them.</summary>
    public static PdfPageComparisonResult Compare(PdfPage a, PdfPage b, PdfPageComparisonOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        options ??= new PdfPageComparisonOptions();
        options.Validate();
        var render = new PdfRenderOptions { Dpi = options.Dpi };
        return Compare(PdfRenderer.Render(a, render).Image, PdfRenderer.Render(b, render).Image, options);
    }

    /// <summary>Compares page <paramref name="pageNumber"/> (from 1) of two documents.</summary>
    public static PdfPageComparisonResult Compare(PdfDocument a, PdfDocument b, int pageNumber, PdfPageComparisonOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return Compare(a.GetPage(pageNumber), b.GetPage(pageNumber), options);
    }

    /// <summary>Compares two PNG images.</summary>
    public static PdfPageComparisonResult Compare(byte[] pngA, byte[] pngB, PdfPageComparisonOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(pngA);
        ArgumentNullException.ThrowIfNull(pngB);
        return Compare(PngCodec.Decode(pngA), PngCodec.Decode(pngB), options);
    }

    /// <summary>Compares two raster images (transparency is composited over white).</summary>
    public static PdfPageComparisonResult Compare(RasterImage a, RasterImage b, PdfPageComparisonOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        options ??= new PdfPageComparisonOptions();
        options.Validate();
        if ((long)a.Width * a.Height > MaxComparedPixels || (long)b.Width * b.Height > MaxComparedPixels)
        {
            throw new ArgumentException("An image is larger than the rendering pixel limit.");
        }

        var (width, height) = (Math.Min(a.Width, b.Width), Math.Min(a.Height, b.Height));
        var rgbA = ToRgb(a);
        var rgbB = ToRgb(b);
        var threshold = options.ChannelThreshold;
        var radius = options.NeighbourhoodRadius;
        var diff = options.ProduceDiffImage ? new byte[width * height * 3] : null;
        long differing = 0, inkA = 0, inkB = 0;
        for (var y = 0; y < height; y++)
        {
            var rowA = y * a.Width * 3;
            var rowB = y * b.Width * 3;
            var rowDiff = y * width * 3;
            for (var x = 0; x < width; x++)
            {
                var ia = rowA + (x * 3);
                var ib = rowB + (x * 3);
                inkA += IsInk(rgbA, ia, threshold) ? 1 : 0;
                inkB += IsInk(rgbB, ib, threshold) ? 1 : 0;

                // The pixel at the same position lies in both windows: when it is close, both directions match and
                // the window scans are skipped (most pixels of two renderings of one page are equal or near).
                var differs = !Close(rgbA, ia, rgbB, ib, threshold)
                    && (!Matches(rgbA, ia, rgbB, b.Width, x, y, width, height, threshold, radius)
                        || !Matches(rgbB, ib, rgbA, a.Width, x, y, width, height, threshold, radius));
                differing += differs ? 1 : 0;
                if (diff is not null)
                {
                    PaintDiff(diff, rowDiff + (x * 3), rgbA, ia, differs);
                }
            }
        }

        var png = diff is null ? null : PngCodec.Encode(new RasterImage(width, height, ImageColorType.Rgb, diff));
        var mismatch = a.Width != b.Width || a.Height != b.Height;
        return new PdfPageComparisonResult(width, height, differing, (long)width * height, inkA, inkB, mismatch, png);
    }

    // A differing pixel in red, any other the first page's pixel faded towards white.
    private static void PaintDiff(byte[] diff, int o, byte[] rgbA, int ia, bool differs)
    {
        if (differs)
        {
            (diff[o], diff[o + 1], diff[o + 2]) = (255, 0, 0);
        }
        else
        {
            var fade = Fade;
            (diff[o], diff[o + 1], diff[o + 2]) = (fade[rgbA[ia]], fade[rgbA[ia + 1]], fade[rgbA[ia + 2]]);
        }
    }

    // A channel faded to 30 % of its strength over white.
    private static readonly byte[] Fade = Enumerable.Range(0, 256).Select(v => (byte)(((v * 3) + (255 * 7) + 5) / 10)).ToArray();

    private static bool Close(byte[] a, int ia, byte[] b, int ib, int threshold) =>
        Math.Abs(a[ia] - b[ib]) <= threshold
        && Math.Abs(a[ia + 1] - b[ib + 1]) <= threshold
        && Math.Abs(a[ia + 2] - b[ib + 2]) <= threshold;

    // Does the pixel at offset "from" (position x, y) have a close pixel in "to" within the radius,
    // kept inside the common area?
    private static bool Matches(byte[] source, int from, byte[] to, int toWidth, int x, int y, int areaWidth, int areaHeight, int threshold, int radius)
    {
        int x0 = Math.Max(0, x - radius), x1 = Math.Min(areaWidth - 1, x + radius);
        int y0 = Math.Max(0, y - radius), y1 = Math.Min(areaHeight - 1, y + radius);
        for (var yy = y0; yy <= y1; yy++)
        {
            for (var xx = x0; xx <= x1; xx++)
            {
                var t = ((yy * toWidth) + xx) * 3;
                if (Math.Abs(source[from] - to[t]) <= threshold
                    && Math.Abs(source[from + 1] - to[t + 1]) <= threshold
                    && Math.Abs(source[from + 2] - to[t + 2]) <= threshold)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsInk(byte[] rgb, int i, int threshold)
    {
        var limit = 255 - threshold;
        return rgb[i] < limit || rgb[i + 1] < limit || rgb[i + 2] < limit;
    }

    // The pixels as RGB over white; an RGB image is used as it is (only read).
    private static byte[] ToRgb(RasterImage image) => image.ColorType switch
    {
        ImageColorType.Rgb => image.Pixels,
        ImageColorType.Rgba => RgbaToRgb(image.Pixels),
        _ => ToRgbPerPixel(image),
    };

    private static byte[] RgbaToRgb(byte[] rgba)
    {
        var rgb = new byte[rgba.Length / 4 * 3];
        for (int i = 0, o = 0; i < rgba.Length; i += 4, o += 3)
        {
            var alpha = rgba[i + 3];
            if (alpha == 255)
            {
                (rgb[o], rgb[o + 1], rgb[o + 2]) = (rgba[i], rgba[i + 1], rgba[i + 2]);
            }
            else
            {
                (rgb[o], rgb[o + 1], rgb[o + 2]) = (Over(rgba[i], alpha), Over(rgba[i + 1], alpha), Over(rgba[i + 2], alpha));
            }
        }

        return rgb;
    }

    private static byte[] ToRgbPerPixel(RasterImage image)
    {
        var rgb = new byte[image.Width * image.Height * 3];
        var o = 0;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var (r, g, bl, al) = image.GetRgba(x, y);
                rgb[o++] = Over(r, al);
                rgb[o++] = Over(g, al);
                rgb[o++] = Over(bl, al);
            }
        }

        return rgb;
    }

    private static byte Over(byte value, byte alpha) => (byte)(((value * alpha) + (255 * (255 - alpha)) + 127) / 255);
}
