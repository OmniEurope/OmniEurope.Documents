// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>A one-input PDF function (types 0 sampled, 2 exponential, 3 stitching).</summary>
internal abstract class ShadingFunction
{
    public abstract double[] Evaluate(double t);

    public static ShadingFunction? Read(PdfObjectStore store, PdfObject? value, int depth = 0)
    {
        if (store.Resolve(value) is not PdfDictionary dictionary || depth > 8)
        {
            return null;
        }

        return (int)store.Number(dictionary, "FunctionType", -1) switch
        {
            2 => new Exponential(Numbers(store, dictionary, "C0") ?? [0], Numbers(store, dictionary, "C1") ?? [1], store.Number(dictionary, "N", 1)),
            3 => Stitching.Read(store, dictionary, depth),
            0 when dictionary is PdfStream stream => Sampled.Read(store, stream),
            _ => null,
        };
    }

    public static double[]? Numbers(PdfObjectStore store, PdfDictionary dictionary, string key) =>
        store.Get<PdfArray>(dictionary, key)?.Items.Select(i => store.Resolve(i) is PdfNumber n ? n.Value : 0).ToArray();

    private sealed class Exponential(double[] c0, double[] c1, double exponent) : ShadingFunction
    {
        public override double[] Evaluate(double t)
        {
            var power = Math.Pow(Math.Max(t, 0), exponent);
            return c0.Select((value, i) => value + (power * ((i < c1.Length ? c1[i] : value) - value))).ToArray();
        }
    }

    private sealed class Stitching(List<ShadingFunction> functions, double[] bounds, double[] encode, double[] domain) : ShadingFunction
    {
        public static Stitching? Read(PdfObjectStore store, PdfDictionary dictionary, int depth)
        {
            var functions = store.Get<PdfArray>(dictionary, "Functions")?.Items.Select(f => ShadingFunction.Read(store, f, depth + 1)).ToList();
            if (functions is null || functions.Count == 0 || functions.Any(f => f is null))
            {
                return null;
            }

            return new Stitching(functions!, Numbers(store, dictionary, "Bounds") ?? [], Numbers(store, dictionary, "Encode") ?? [], Numbers(store, dictionary, "Domain") ?? [0, 1]);
        }

        public override double[] Evaluate(double t)
        {
            var k = 0;
            while (k < bounds.Length && t >= bounds[k])
            {
                k++;
            }

            k = Math.Min(k, functions.Count - 1);
            var low = k == 0 ? domain[0] : bounds[k - 1];
            var high = k == bounds.Length ? domain[^1] : bounds[k];
            var (e0, e1) = encode.Length >= (2 * k) + 2 ? (encode[2 * k], encode[(2 * k) + 1]) : (0, 1);
            var mapped = high > low ? e0 + ((t - low) * (e1 - e0) / (high - low)) : e0;
            return functions[k].Evaluate(mapped);
        }
    }

    private sealed class Sampled(double[][] samples, double[] domain) : ShadingFunction
    {
        public static Sampled? Read(PdfObjectStore store, PdfStream stream)
        {
            var size = Numbers(store, stream, "Size");
            var range = Numbers(store, stream, "Range");
            var bits = (int)store.Number(stream, "BitsPerSample", 8);
            if (size is not { Length: 1 } || range is null || range.Length < 2 || bits is not (8 or 16))
            {
                return null;
            }

            var data = store.DecodeBytes(stream);
            var outputs = range.Length / 2;
            var count = (int)size[0];
            var bytes = bits / 8;
            var samples = new double[count][];
            for (var i = 0; i < count; i++)
            {
                samples[i] = new double[outputs];
                for (var o = 0; o < outputs; o++)
                {
                    var at = ((i * outputs) + o) * bytes;
                    var raw = at + bytes <= data.Length ? (bytes == 1 ? data[at] : (data[at] << 8) | data[at + 1]) : 0;
                    var max = bits == 8 ? 255.0 : 65535.0;
                    samples[i][o] = range[2 * o] + (raw / max * (range[(2 * o) + 1] - range[2 * o]));
                }
            }

            return count == 0 ? null : new Sampled(samples, Numbers(store, stream, "Domain") ?? [0, 1]);
        }

        public override double[] Evaluate(double t)
        {
            var position = (t - domain[0]) / Math.Max(1e-12, domain[^1] - domain[0]) * (samples.Length - 1);
            var index = Math.Clamp((int)Math.Floor(position), 0, samples.Length - 1);
            var next = Math.Min(index + 1, samples.Length - 1);
            var fraction = Math.Clamp(position - index, 0, 1);
            return samples[index].Select((value, o) => value + ((samples[next][o] - value) * fraction)).ToArray();
        }
    }
}

/// <summary>
/// Axial (type 2) and radial (type 3) shadings: the colour of a point of shading space, with the extend
/// flags deciding what lies beyond the end points. Other shading types are not drawn.
/// </summary>
internal sealed class Shading
{
    private readonly PdfColorSpace _space;
    private readonly ShadingFunction _function;
    private readonly double[] _coords;
    private readonly double[] _domain;
    private readonly bool _extendStart;
    private readonly bool _extendEnd;
    private readonly bool _radial;

    private Shading(PdfColorSpace space, ShadingFunction function, double[] coords, double[] domain, bool extendStart, bool extendEnd, bool radial)
    {
        (_space, _function, _coords, _domain, _extendStart, _extendEnd, _radial) = (space, function, coords, domain, extendStart, extendEnd, radial);
    }

    public static Shading? Read(PdfObjectStore store, PdfObject? value, PdfDictionary? resources)
    {
        if (store.Resolve(value) is not PdfDictionary dictionary)
        {
            return null;
        }

        var type = (int)store.Number(dictionary, "ShadingType");
        var coords = ShadingFunction.Numbers(store, dictionary, "Coords");
        var function = ShadingFunction.Read(store, store.Get(dictionary, "Function"));
        if (type is not (2 or 3) || coords is null || coords.Length < (type == 2 ? 4 : 6) || function is null)
        {
            return null;
        }

        var extend = store.Get<PdfArray>(dictionary, "Extend")?.Items.Select(i => store.Resolve(i) is PdfBoolean { Value: true }).ToArray() ?? [false, false];
        var space = PdfColorSpace.Resolve(store, store.Get(dictionary, "ColorSpace"), resources);
        return new Shading(space, function, coords, ShadingFunction.Numbers(store, dictionary, "Domain") ?? [0, 1], extend.ElementAtOrDefault(0), extend.ElementAtOrDefault(1), type == 3);
    }

    /// <summary>The colour at a point of shading space, or null outside the shading.</summary>
    public PdfColor? ColorAt(double x, double y)
    {
        var s = _radial ? Radial(x, y) : Axial(x, y);
        if (s is not { } parameter)
        {
            return null;
        }

        var t = _domain[0] + (parameter * (_domain[^1] - _domain[0]));
        return GraphicsState.ToRgb(_space, _function.Evaluate(t));
    }

    private double? Axial(double x, double y)
    {
        var (dx, dy) = (_coords[2] - _coords[0], _coords[3] - _coords[1]);
        var length = (dx * dx) + (dy * dy);
        var s = length < 1e-12 ? 0 : (((x - _coords[0]) * dx) + ((y - _coords[1]) * dy)) / length;
        return Clamp(s);
    }

    // The largest s for which the point lies on the circle interpolated between the two circles.
    private double? Radial(double x, double y)
    {
        var (x0, y0, r0, x1, y1, r1) = (_coords[0], _coords[1], _coords[2], _coords[3], _coords[4], _coords[5]);
        var (cdx, cdy, dr) = (x1 - x0, y1 - y0, r1 - r0);
        var (px, py) = (x - x0, y - y0);
        var a = (cdx * cdx) + (cdy * cdy) - (dr * dr);
        var b = (px * cdx) + (py * cdy) + (r0 * dr);
        var c = (px * px) + (py * py) - (r0 * r0);
        double[] roots;
        if (Math.Abs(a) < 1e-12)
        {
            roots = Math.Abs(b) < 1e-12 ? [] : [c / (2 * b)];
        }
        else
        {
            var discriminant = (b * b) - (a * c);
            roots = discriminant < 0 ? [] : [(b + Math.Sqrt(discriminant)) / a, (b - Math.Sqrt(discriminant)) / a];
        }

        foreach (var s in roots.OrderDescending())
        {
            if (r0 + (s * dr) >= 0 && Clamp(s) is { } value)
            {
                return value;
            }
        }

        return null;
    }

    private double? Clamp(double s) => s switch
    {
        < 0 => _extendStart ? 0 : null,
        > 1 => _extendEnd ? 1 : null,
        _ => s,
    };
}
