// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;

namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>The blend modes of ISO 32000-1 §11.3.5: twelve separable ones, then four non-separable ones.</summary>
internal enum BlendMode
{
    Normal,
    Multiply,
    Screen,
    Overlay,
    Darken,
    Lighten,
    ColorDodge,
    ColorBurn,
    HardLight,
    SoftLight,
    Difference,
    Exclusion,
    Hue,
    Saturation,
    Color,
    Luminosity,
}

/// <summary>
/// The blend function B(Cb, Cs) of every blend mode (ISO 32000-1 §11.3.5), on components from 0 to 1 in the RGB
/// blending space.
/// </summary>
internal static class Blending
{
    private static readonly Dictionary<string, BlendMode> Names = new(StringComparer.Ordinal)
    {
        ["Normal"] = BlendMode.Normal, ["Compatible"] = BlendMode.Normal, ["Multiply"] = BlendMode.Multiply,
        ["Screen"] = BlendMode.Screen, ["Overlay"] = BlendMode.Overlay, ["Darken"] = BlendMode.Darken,
        ["Lighten"] = BlendMode.Lighten, ["ColorDodge"] = BlendMode.ColorDodge, ["ColorBurn"] = BlendMode.ColorBurn,
        ["HardLight"] = BlendMode.HardLight, ["SoftLight"] = BlendMode.SoftLight, ["Difference"] = BlendMode.Difference,
        ["Exclusion"] = BlendMode.Exclusion, ["Hue"] = BlendMode.Hue, ["Saturation"] = BlendMode.Saturation,
        ["Color"] = BlendMode.Color, ["Luminosity"] = BlendMode.Luminosity,
    };

    /// <summary>The mode a BM entry names: a name, or the first known name of an array; Normal otherwise (§11.3.5).</summary>
    public static BlendMode Parse(PdfObjectStore store, PdfObject? value)
    {
        IEnumerable<PdfName> names = store.Resolve(value) switch
        {
            PdfName name => [name],
            PdfArray array => array.Items.Select(store.Resolve).OfType<PdfName>(),
            _ => [],
        };
        foreach (var name in names)
        {
            if (Names.TryGetValue(name.Value, out var mode))
            {
                return mode;
            }
        }

        return BlendMode.Normal;
    }

    /// <summary>B(Cb, Cs) for a backdrop and a source colour.</summary>
    public static (double R, double G, double B) Apply(BlendMode mode, (double R, double G, double B) backdrop, (double R, double G, double B) source)
    {
        if (mode < BlendMode.Hue)
        {
            return (Separable(mode, backdrop.R, source.R), Separable(mode, backdrop.G, source.G), Separable(mode, backdrop.B, source.B));
        }

        return mode switch
        {
            BlendMode.Hue => SetLum(SetSat(source, Sat(backdrop)), Lum(backdrop)),
            BlendMode.Saturation => SetLum(SetSat(backdrop, Sat(source)), Lum(backdrop)),
            BlendMode.Color => SetLum(source, Lum(backdrop)),
            _ => SetLum(backdrop, Lum(source)),
        };
    }

    public static double Separable(BlendMode mode, double cb, double cs) => mode switch
    {
        BlendMode.Multiply => cb * cs,
        BlendMode.Screen => Screen(cb, cs),
        BlendMode.Overlay => HardLight(cs, cb),
        BlendMode.Darken => Math.Min(cb, cs),
        BlendMode.Lighten => Math.Max(cb, cs),
        BlendMode.ColorDodge => ColorDodge(cb, cs),
        BlendMode.ColorBurn => ColorBurn(cb, cs),
        BlendMode.HardLight => HardLight(cb, cs),
        BlendMode.SoftLight => SoftLight(cb, cs),
        BlendMode.Difference => Math.Abs(cb - cs),
        BlendMode.Exclusion => cb + cs - (2 * cb * cs),
        _ => cs,
    };

    /// <summary>The luminosity of a colour: 0.3 R + 0.59 G + 0.11 B.</summary>
    public static double Lum((double R, double G, double B) c) => (0.3 * c.R) + (0.59 * c.G) + (0.11 * c.B);

    private static double Screen(double cb, double cs) => cb + cs - (cb * cs);

    private static double HardLight(double cb, double cs) => cs <= 0.5 ? cb * 2 * cs : Screen(cb, (2 * cs) - 1);

    private static double ColorDodge(double cb, double cs) => cs < 1 ? Math.Min(1, cb / (1 - cs)) : 1;

    private static double ColorBurn(double cb, double cs) => cs > 0 ? 1 - Math.Min(1, (1 - cb) / cs) : 0;

    private static double SoftLight(double cb, double cs)
    {
        if (cs <= 0.5)
        {
            return cb - ((1 - (2 * cs)) * cb * (1 - cb));
        }

        var d = cb <= 0.25 ? ((((16 * cb) - 12) * cb) + 4) * cb : Math.Sqrt(cb);
        return cb + (((2 * cs) - 1) * (d - cb));
    }

    private static (double R, double G, double B) SetLum((double R, double G, double B) c, double l)
    {
        var d = l - Lum(c);
        return ClipColor((c.R + d, c.G + d, c.B + d));
    }

    private static (double R, double G, double B) ClipColor((double R, double G, double B) c)
    {
        var l = Lum(c);
        var n = Math.Min(c.R, Math.Min(c.G, c.B));
        var x = Math.Max(c.R, Math.Max(c.G, c.B));
        if (n < 0)
        {
            c = (l + ((c.R - l) * l / (l - n)), l + ((c.G - l) * l / (l - n)), l + ((c.B - l) * l / (l - n)));
        }

        if (x > 1)
        {
            c = (l + ((c.R - l) * (1 - l) / (x - l)), l + ((c.G - l) * (1 - l) / (x - l)), l + ((c.B - l) * (1 - l) / (x - l)));
        }

        return c;
    }

    private static double Sat((double R, double G, double B) c) => Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B));

    // The largest component becomes s, the smallest 0 and the middle one keeps its place between them.
    private static (double R, double G, double B) SetSat((double R, double G, double B) c, double s)
    {
        double[] v = [c.R, c.G, c.B];
        int[] order = [0, 1, 2];
        Array.Sort(order, (a, b) => v[a].CompareTo(v[b]));
        var (min, mid, max) = (order[0], order[1], order[2]);
        var range = v[max] - v[min];
        var result = new double[3];
        if (range > 0)
        {
            result[mid] = (v[mid] - v[min]) * s / range;
            result[max] = s;
        }

        return (result[0], result[1], result[2]);
    }
}
