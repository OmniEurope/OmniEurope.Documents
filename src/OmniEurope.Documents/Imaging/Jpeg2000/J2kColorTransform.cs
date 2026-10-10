// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jpeg2000;

/// <summary>
/// The inverse multiple component transforms of the first three components (ISO/IEC 15444-1 annex G): the reversible
/// component transform for the 5-3 filter (G.2), the irreversible one for the 9-7 filter (G.3).
/// </summary>
internal static class J2kColorTransform
{
    public static void Inverse(J2kTileComponent[] components)
    {
        var (y, u, v) = (components[0], components[1], components[2]);
        if (u.X1 - u.X0 != y.X1 - y.X0 || v.X1 - v.X0 != y.X1 - y.X0 || u.Y1 - u.Y0 != y.Y1 - y.Y0 || v.Y1 - v.Y0 != y.Y1 - y.Y0)
        {
            throw new InvalidDataException("A JPEG 2000 component transform joins components of different sizes.");
        }

        if (y.Integers is { } y0 && u.Integers is { } y1 && v.Integers is { } y2)
        {
            Reversible(y0, y1, y2);
        }
        else if (y.Reals is { } r0 && u.Reals is { } r1 && v.Reals is { } r2)
        {
            Irreversible(r0, r1, r2);
        }
        else
        {
            throw new InvalidDataException("A JPEG 2000 component transform joins reversible and irreversible components.");
        }
    }

    // G-6: G = Y0 - floor((Y1 + Y2) / 4), R = Y2 + G, B = Y1 + G.
    private static void Reversible(int[] y0, int[] y1, int[] y2)
    {
        for (var i = 0; i < y0.Length; i++)
        {
            var g = y0[i] - ((y1[i] + y2[i]) >> 2);
            var r = y2[i] + g;
            var b = y1[i] + g;
            (y0[i], y1[i], y2[i]) = (r, g, b);
        }
    }

    // G-9: R = Y + 1.402 Cr, G = Y - 0.34413 Cb - 0.71414 Cr, B = Y + 1.772 Cb.
    private static void Irreversible(float[] y0, float[] y1, float[] y2)
    {
        for (var i = 0; i < y0.Length; i++)
        {
            var (y, cb, cr) = (y0[i], y1[i], y2[i]);
            y0[i] = y + (1.402f * cr);
            y1[i] = y - (0.34413f * cb) - (0.71414f * cr);
            y2[i] = y + (1.772f * cb);
        }
    }
}
