// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jpeg2000;

/// <summary>
/// How CIE Lab samples map to L*, a* and b* (ISO/IEC 15444-2 M.11.7.4): each value is (sample / full scale - offset)
/// times its range, the offsets given as fractions of the full scale; and the illuminant (D50 unless D65).
/// </summary>
internal sealed record Jpeg2000Lab(double RangeL, double OffsetL, double RangeA, double OffsetA, double RangeB, double OffsetB, bool D65)
{
    /// <summary>The default parameters: L* over 100 from 0, a* over 170 centred, b* over 200 offset by three eighths.</summary>
    public static Jpeg2000Lab Default(Jpeg2000Plane[] colors) => From(null, colors);

    /// <summary>The parameters RL, OL, RA, OA, RB, OB of the colour specification, or the defaults.</summary>
    public static Jpeg2000Lab From(uint[]? parameters, Jpeg2000Plane[] colors, bool d65 = false)
    {
        double Full(int c) => (1L << colors[c].Precision) - 1;
        double Half(int c) => 1L << (colors[c].Precision - 1);
        return parameters is null
            ? new Jpeg2000Lab(100, 0, 170, Half(1) / Full(1), 200, 0.75 * Half(2) / Full(2), d65)
            : new Jpeg2000Lab(parameters[0], parameters[1] / Full(0), parameters[2], parameters[3] / Full(1), parameters[4], parameters[5] / Full(2), d65);
    }
}

/// <summary>Colour conversions of decoded JPEG 2000 pixels: sYCC and CIE Lab to RGB, and the opacity channel as alpha.</summary>
internal static class Jpeg2000Colors
{
    /// <summary>sYCC (IEC 61966-2-1 amendment 1, chroma centred on 128) to RGB, in place.</summary>
    public static void YccToRgb(byte[] pixels)
    {
        for (var i = 0; i + 2 < pixels.Length; i += 3)
        {
            var y = (double)pixels[i];
            var cb = pixels[i + 1] - 128.0;
            var cr = pixels[i + 2] - 128.0;
            pixels[i] = Byte(y + (1.402 * cr));
            pixels[i + 1] = Byte(y - (0.344136 * cb) - (0.714136 * cr));
            pixels[i + 2] = Byte(y + (1.772 * cb));
        }
    }

    /// <summary>CIE L*a*b* to sRGB through XYZ, in place; a D50 white is adapted to D65 (Bradford).</summary>
    public static void LabToRgb(byte[] pixels, Jpeg2000Lab lab)
    {
        var (wx, wz) = lab.D65 ? (0.9505, 1.089) : (0.9642, 0.8249);
        double[] m = lab.D65
            ? [3.2406, -1.5372, -0.4986, -0.9689, 1.8758, 0.0415, 0.0557, -0.2040, 1.0570]
            : [3.1339, -1.6169, -0.4906, -0.9788, 1.9161, 0.0335, 0.0719, -0.2290, 1.4052];
        for (var i = 0; i + 2 < pixels.Length; i += 3)
        {
            var l = ((pixels[i] / 255.0) - lab.OffsetL) * lab.RangeL;
            var a = ((pixels[i + 1] / 255.0) - lab.OffsetA) * lab.RangeA;
            var b = ((pixels[i + 2] / 255.0) - lab.OffsetB) * lab.RangeB;
            var fy = (l + 16) / 116;
            var x = wx * Inverse(fy + (a / 500));
            var y = Inverse(fy);
            var z = wz * Inverse(fy - (b / 200));
            pixels[i] = Gamma((m[0] * x) + (m[1] * y) + (m[2] * z));
            pixels[i + 1] = Gamma((m[3] * x) + (m[4] * y) + (m[5] * z));
            pixels[i + 2] = Gamma((m[6] * x) + (m[7] * y) + (m[8] * z));
        }
    }

    /// <summary>The colour image with <paramref name="alpha"/> as its alpha channel (premultiplied colours divided back).</summary>
    public static RasterImage WithAlpha(RasterImage image, RasterImage alpha, bool premultiplied)
    {
        var gray = image.ColorType == ImageColorType.Gray;
        var color = gray ? image : image.ColorType == ImageColorType.Rgb ? image : image.ConvertTo(ImageColorType.Rgb);
        var components = gray ? 1 : 3;
        var result = new RasterImage(image.Width, image.Height, gray ? ImageColorType.GrayAlpha : ImageColorType.Rgba);
        for (var i = 0; i < alpha.Pixels.Length; i++)
        {
            var a = alpha.Pixels[i];
            for (var c = 0; c < components; c++)
            {
                var value = color.Pixels[(i * components) + c];
                result.Pixels[(i * (components + 1)) + c] = premultiplied ? (byte)(a == 0 ? 0 : Math.Min(255, ((value * 255) + (a / 2)) / a)) : value;
            }

            result.Pixels[(i * (components + 1)) + components] = a;
        }

        return result;
    }

    private static double Inverse(double t) => t > 6.0 / 29 ? t * t * t : 3 * (6.0 / 29) * (6.0 / 29) * (t - (4.0 / 29));

    private static byte Gamma(double linear)
    {
        var v = Math.Clamp(linear, 0, 1);
        return Byte(255 * (v <= 0.0031308 ? 12.92 * v : (1.055 * Math.Pow(v, 1 / 2.4)) - 0.055));
    }

    private static byte Byte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
}
