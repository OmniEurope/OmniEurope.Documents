// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;

namespace OmniEurope.Documents.Pdf.Text;

/// <summary>
/// A colour space reduced to what image decoding needs: component count, default decode range and the
/// conversion of one sample tuple (each in its decode range) to output pixels.
/// </summary>
internal sealed class PdfColorSpace
{
    private readonly Func<double[], byte[], int, int> _write;
    private readonly double[]? _defaultDecode;

    private PdfColorSpace(int components, ImageColorType output, double[]? defaultDecode, Func<double[], byte[], int, int> write)
    {
        Components = components;
        Output = output;
        _defaultDecode = defaultDecode;
        _write = write;
    }

    public static PdfColorSpace Gray { get; } = new(1, ImageColorType.Gray, [], (s, o, i) => { o[i] = Byte(s[0]); return i + 1; });

    public static PdfColorSpace Rgb { get; } = new(3, ImageColorType.Rgb, [], (s, o, i) =>
    {
        o[i] = Byte(s[0]);
        o[i + 1] = Byte(s[1]);
        o[i + 2] = Byte(s[2]);
        return i + 3;
    });

    public static PdfColorSpace Cmyk { get; } = new(4, ImageColorType.Cmyk, [], (s, o, i) =>
    {
        for (var c = 0; c < 4; c++)
        {
            o[i + c] = Byte(s[c]);
        }

        return i + 4;
    });

    // Declared after Gray, Rgb and Cmyk: static initializers run in text order.
    private static readonly Dictionary<string, PdfColorSpace> DeviceSpaces = new(StringComparer.Ordinal)
    {
        ["DeviceGray"] = Gray, ["G"] = Gray, ["CalGray"] = Gray, ["Pattern"] = Gray,
        ["DeviceRGB"] = Rgb, ["RGB"] = Rgb, ["CalRGB"] = Rgb,
        ["DeviceCMYK"] = Cmyk, ["CMYK"] = Cmyk,
    };

    // Colour space families written as arrays: [/Family parameters...].
    private static readonly Dictionary<string, Func<PdfObjectStore, PdfArray, PdfDictionary?, int, PdfColorSpace>> Families = new(StringComparer.Ordinal)
    {
        ["ICCBased"] = (store, array, _, _) => ByComponents(store.Number(store.Resolve(array.Count > 1 ? array[1] : null) as PdfDictionary, "N", 3)),
        ["CalGray"] = (_, _, _, _) => Gray,
        ["CalRGB"] = (_, _, _, _) => Rgb,
        ["Lab"] = (store, array, _, _) => Lab(store.Get<PdfArray>(store.Resolve(array.Count > 1 ? array[1] : null) as PdfDictionary, "Range")),
        ["Indexed"] = Indexed,
        ["I"] = Indexed,
        ["Separation"] = Inks,
        ["DeviceN"] = Inks,
    };

    public int Components { get; }

    public ImageColorType Output { get; }

    /// <summary>
    /// The range image samples of component <paramref name="component"/> map to when the image has no Decode
    /// array (0 to 1, Lab its Range); null for an index, whose range is every sample value (0 to 2^bits - 1).
    /// </summary>
    public (double Low, double High)? DefaultDecode(int component) =>
        _defaultDecode is null ? null : (2 * component) + 1 < _defaultDecode.Length ? (_defaultDecode[2 * component], _defaultDecode[(2 * component) + 1]) : (0, 1);

    public int Write(double[] samples, byte[] output, int offset) => _write(samples, output, offset);

    public static PdfColorSpace Resolve(PdfObjectStore store, PdfObject? value, PdfDictionary? resources, int depth = 0)
    {
        value = store.Resolve(value);
        if (depth >= 8)
        {
            return Rgb;
        }

        if (value is PdfName name)
        {
            if (DeviceSpaces.TryGetValue(name.Value, out var device))
            {
                return device;
            }

            var named = store.Get(store.Get<PdfDictionary>(resources, "ColorSpace"), name.Value);
            return named is null ? Rgb : Resolve(store, named, resources, depth + 1);
        }

        if (value is not PdfArray { Count: > 0 } array || store.Resolve(array[0]) is not PdfName family)
        {
            return Rgb;
        }

        return Families.TryGetValue(family.Value, out var resolve)
            ? resolve(store, array, resources, depth)
            : Resolve(store, family, resources, depth + 1);
    }
    private static PdfColorSpace Inks(PdfObjectStore store, PdfArray array, PdfDictionary? resources, int depth) =>
        Ink(array.Count > 1 && store.Resolve(array[1]) is PdfArray names ? names.Count : 1);

    private static PdfColorSpace ByComponents(double n) => n switch
    {
        1 => Gray,
        4 => Cmyk,
        _ => Rgb,
    };

    // Lab values (L* 0-100, a* and b* in the Range, -100..100 by default) converted through XYZ to sRGB, the
    // white point taken as D65.
    private static PdfColorSpace Lab(PdfArray? range)
    {
        double[] ab = range is { Count: 4 } && range.Items.All(v => v is PdfNumber)
            ? [.. range.Items.Select(v => ((PdfNumber)v).Value)]
            : [-100, 100, -100, 100];
        return new(3, ImageColorType.Rgb, [0, 100, .. ab], LabToRgb);
    }

    private static int LabToRgb(double[] s, byte[] o, int i)
    {
        var l = s[0];
        var a = s[1];
        var b = s[2];
        var fy = (l + 16) / 116;
        double F(double t) => t > 6.0 / 29 ? t * t * t : 3 * (6.0 / 29) * (6.0 / 29) * (t - (4.0 / 29));
        var x = 0.9505 * F(fy + (a / 500));
        var y = F(fy);
        var z = 1.089 * F(fy - (b / 200));
        o[i] = Gamma((3.2406 * x) - (1.5372 * y) - (0.4986 * z));
        o[i + 1] = Gamma((-0.9689 * x) + (1.8758 * y) + (0.0415 * z));
        o[i + 2] = Gamma((0.0557 * x) - (0.204 * y) + (1.057 * z));
        return i + 3;
    }

    private static PdfColorSpace Indexed(PdfObjectStore store, PdfArray array, PdfDictionary? resources, int depth)
    {
        var baseSpace = Resolve(store, array.Count > 1 ? array[1] : null, resources, depth + 1);
        var high = array.Count > 2 && store.Resolve(array[2]) is PdfNumber h ? h.IntValue : 255;
        var lookup = (array.Count > 3 ? store.Resolve(array[3]) : null) switch
        {
            PdfString s => s.Bytes,
            PdfStream stream => store.DecodeBytes(stream),
            _ => [],
        };
        var n = baseSpace.Components;
        return new PdfColorSpace(1, baseSpace.Output, null, (s, o, i) =>
        {
            var index = Math.Clamp((int)Math.Round(s[0]), 0, high);
            var entry = new double[n];
            for (var c = 0; c < n; c++)
            {
                entry[c] = (index * n) + c < lookup.Length ? lookup[(index * n) + c] / 255.0 : 0;
            }

            return baseSpace.Write(entry, o, i);
        });
    }

    // Spot inks shown as coverage: full ink is black.
    private static PdfColorSpace Ink(int inks) => new(inks, ImageColorType.Gray, [], (s, o, i) =>
    {
        o[i] = Byte(1 - Math.Min(1, s.Take(inks).Max()));
        return i + 1;
    });

    private static byte Byte(double value) => (byte)Math.Clamp((int)Math.Round(value * 255), 0, 255);

    private static byte Gamma(double linear)
    {
        linear = Math.Clamp(linear, 0, 1);
        var srgb = linear <= 0.0031308 ? 12.92 * linear : (1.055 * Math.Pow(linear, 1 / 2.4)) - 0.055;
        return Byte(srgb);
    }
}
