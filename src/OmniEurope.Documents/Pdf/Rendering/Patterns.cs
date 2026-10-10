// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>A colour (0 to 255) and an alpha (0 to 1) given by a pattern at a pixel.</summary>
internal readonly record struct PaintSample(double R, double G, double B, double A);

/// <summary>A pattern used as a colour (ISO 32000-1 §8.7): the colour it gives at each device pixel, or nothing.</summary>
internal abstract class PatternPaint
{
    public abstract PaintSample? At(int x, int y);

    /// <summary>The inverse of a matrix, null when it cannot be inverted.</summary>
    public static Matrix? Inverse(Matrix m)
    {
        var determinant = (m.A * m.D) - (m.B * m.C);
        if (Math.Abs(determinant) < 1e-12)
        {
            return null;
        }

        return new Matrix(m.D / determinant, -m.B / determinant, -m.C / determinant, m.A / determinant,
            ((m.C * m.F) - (m.D * m.E)) / determinant, ((m.B * m.E) - (m.A * m.F)) / determinant);
    }
}

/// <summary>
/// A shading as a colour: the shading's colour at the centre of each pixel. Used as a pattern (not by <c>sh</c>), the
/// Background colour fills what lies outside the shading (§8.7.4.3).
/// </summary>
internal sealed class ShadingPaint(Shading shading, Matrix toDevice, bool background) : PatternPaint
{
    private readonly Matrix? _inverse = Inverse(toDevice);

    public override PaintSample? At(int x, int y)
    {
        if (_inverse is not { } inverse)
        {
            return null;
        }

        var (u, v) = inverse.Transform(x + 0.5, y + 0.5);
        return (shading.ColorAt(u, v) ?? (background ? shading.Background : null)) is { } color ? new PaintSample(color.R, color.G, color.B, 1) : null;
    }
}

/// <summary>A tiling pattern dictionary (§8.7.3): its cell, the lattice steps, the matrix and how the cell is coloured.</summary>
internal sealed record TilingPattern(PdfStream Stream, bool Uncoloured, (double X0, double Y0, double X1, double Y1) Box, double XStep, double YStep, Matrix Matrix)
{
    public static TilingPattern? Read(PdfObjectStore store, PdfObject? value)
    {
        if (store.Resolve(value) is not PdfStream stream || store.Number(stream, "PatternType") != 1)
        {
            return null;
        }

        var box = ShadingFunction.Numbers(store, stream, "BBox");
        var (xStep, yStep) = (store.Number(stream, "XStep"), store.Number(stream, "YStep"));
        var paintType = (int)store.Number(stream, "PaintType", 1);
        if (box is not { Length: 4 } || paintType is not (1 or 2) || !Usable(xStep) || !Usable(yStep) || box.Any(v => !double.IsFinite(v)))
        {
            return null;
        }

        var normalized = (Math.Min(box[0], box[2]), Math.Min(box[1], box[3]), Math.Max(box[0], box[2]), Math.Max(box[1], box[3]));
        if (normalized.Item3 - normalized.Item1 <= 0 || normalized.Item4 - normalized.Item2 <= 0)
        {
            return null;
        }

        return new TilingPattern(stream, paintType == 2, normalized, Math.Abs(xStep), Math.Abs(yStep), Matrix.FromArray(store.Get<PdfArray>(stream, "Matrix")));
    }

    /// <summary>The underlying colour space of a <c>[/Pattern base]</c> space, null for any other space.</summary>
    public static PdfColorSpace? Underlying(PdfObjectStore store, PdfObject? value, PdfDictionary? resources)
    {
        value = store.Resolve(value);
        if (value is PdfName name && store.Get(store.Get<PdfDictionary>(resources, "ColorSpace"), name.Value) is { } named)
        {
            value = named;
        }

        return value is PdfArray { Count: > 1 } array && store.Resolve(array[0]) is PdfName { Value: "Pattern" }
            ? PdfColorSpace.Resolve(store, array[1], resources)
            : null;
    }

    private static bool Usable(double step) => double.IsFinite(step) && Math.Abs(step) > 1e-9;
}

/// <summary>
/// A tiling pattern as a colour: the cell drawn once on its own transparent raster at about the device resolution,
/// then looked up for each device pixel through the lattice of cells (every cell holding the point, later cells over
/// earlier ones). The lattice is exact; the cell's pixels are taken nearest to each device pixel centre.
/// </summary>
internal sealed class TilingPaint : PatternPaint
{
    /// <summary>Most pixels of one cell raster (the resolution is lowered for larger cells).</summary>
    public const int MaxCellPixels = 1 << 20;

    private const int MaxOverlap = 4;
    private readonly TilingPattern _pattern;
    private readonly Matrix? _toPattern;
    private readonly GroupCanvas _cell;
    private readonly (double X, double Y) _scale;

    public TilingPaint(TilingPattern pattern, Matrix toDevice, GroupCanvas cell, (double X, double Y) scale)
    {
        _pattern = pattern;
        _toPattern = Inverse(toDevice);
        _cell = cell;
        _scale = scale;
    }

    /// <summary>The cell raster's pixels per pattern unit along each axis and the matrix from pattern space to the raster.</summary>
    public static ((double X, double Y) Scale, int Width, int Height, Matrix ToCell) Layout(TilingPattern pattern, Matrix toDevice)
    {
        var (x0, y0, x1, y1) = pattern.Box;
        var sx = Math.Max(1e-3, Math.Sqrt((toDevice.A * toDevice.A) + (toDevice.B * toDevice.B)));
        var sy = Math.Max(1e-3, Math.Sqrt((toDevice.C * toDevice.C) + (toDevice.D * toDevice.D)));
        var pixels = (x1 - x0) * sx * (y1 - y0) * sy;
        if (pixels > MaxCellPixels)
        {
            var reduce = Math.Sqrt(MaxCellPixels / pixels);
            (sx, sy) = (sx * reduce, sy * reduce);
        }

        var width = (int)Math.Clamp(Math.Ceiling((x1 - x0) * sx), 1, MaxCellPixels);
        var height = (int)Math.Clamp(Math.Ceiling((y1 - y0) * sy), 1, MaxCellPixels / width);
        return ((sx, sy), width, height, new Matrix(sx, 0, 0, -sy, -x0 * sx, y1 * sy));
    }

    public override PaintSample? At(int x, int y)
    {
        if (_toPattern is not { } toPattern)
        {
            return null;
        }

        var (px, py) = toPattern.Transform(x + 0.5, y + 0.5);
        var (x0, y0, x1, y1) = _pattern.Box;
        var (r, g, b, a) = (0.0, 0.0, 0.0, 0.0);
        foreach (var j in Cells(py, y0, y1, _pattern.YStep))
        {
            foreach (var i in Cells(px, x0, x1, _pattern.XStep))
            {
                var (qx, qy) = (px - (i * _pattern.XStep), py - (j * _pattern.YStep));
                var sample = _cell.Read((int)Math.Floor((qx - x0) * _scale.X), (int)Math.Floor((y1 - qy) * _scale.Y));
                var alpha = sample.A + (a * (1 - sample.A));
                if (alpha > 0)
                {
                    var under = a * (1 - sample.A);
                    (r, g, b) = (((sample.R * sample.A) + (r * under)) / alpha, ((sample.G * sample.A) + (g * under)) / alpha, ((sample.B * sample.A) + (b * under)) / alpha);
                }

                a = alpha;
            }
        }

        return a > 0 ? new PaintSample(r, g, b, a) : null;
    }

    // The lattice indices k for which p - k step lies in [low, high), at most a few when cells overlap.
    private static IEnumerable<long> Cells(double p, double low, double high, double step)
    {
        var last = Math.Floor((p - low) / step);
        var first = Math.Max(Math.Floor((p - high) / step) + 1, last - MaxOverlap + 1);
        if (!double.IsFinite(first) || !double.IsFinite(last) || Math.Abs(last) > 1e15)
        {
            yield break;
        }

        for (var k = (long)first; k <= (long)last; k++)
        {
            yield return k;
        }
    }
}
