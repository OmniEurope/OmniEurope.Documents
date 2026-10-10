// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;
using System.Text;

namespace OmniEurope.Documents.Pdf.Writing;

/// <summary>
/// An ICC version 2.1 display profile (ICC.1:2001-04) for sRGB, computed from the parameters published in
/// IEC 61966-2-1: the primaries x, y (0.64, 0.33), (0.30, 0.60), (0.15, 0.06) and the D65 white (0.3127, 0.3290)
/// give the RGB to XYZ matrix, adapted to the D50 profile connection space by the linear Bradford transform; each
/// channel's tone curve samples the sRGB transfer function (linear below 0.04045, else ((v + 0.055) / 1.055)^2.4)
/// at 1024 points. Nothing is read from a file: the same bytes are produced on every call.
/// </summary>
internal static class SrgbProfile
{
    /// <summary>The profile connection space illuminant of ICC.1 (D50), as the header encodes it.</summary>
    public static readonly double[] D50 = [0.9642, 1.0, 0.8249];

    private const int CurvePoints = 1024;
    private static readonly Lazy<byte[]> Profile = new(Build);

    /// <summary>The profile bytes (a copy).</summary>
    public static byte[] Create() => [.. Profile.Value];

    /// <summary>The colorants (columns: red, green, blue) in the D50 connection space.</summary>
    public static double[,] Colorants()
    {
        (double X, double Y)[] primaries = [(0.64, 0.33), (0.30, 0.60), (0.15, 0.06)];
        var white = Xyz(0.3127, 0.3290);
        var m = new double[3, 3];
        for (var c = 0; c < 3; c++)
        {
            var xyz = Xyz(primaries[c].X, primaries[c].Y);
            for (var r = 0; r < 3; r++)
            {
                m[r, c] = xyz[r];
            }
        }

        // Each primary is scaled so that the three add up to the white point.
        var scale = Multiply(Invert(m), white);
        for (var c = 0; c < 3; c++)
        {
            for (var r = 0; r < 3; r++)
            {
                m[r, c] *= scale[c];
            }
        }

        return Multiply(Bradford(white, D50), m);
    }

    /// <summary>The sRGB transfer function (encoded value to linear light).</summary>
    public static double Linear(double v) => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);

    private static byte[] Build()
    {
        var colorants = Colorants();
        var tags = new List<(string Signature, byte[] Data)>
        {
            ("desc", Description("sRGB IEC61966-2.1")),
            ("cprt", Text("Computed from the sRGB parameters of IEC 61966-2-1")),
            ("wtpt", XyzTag(D50)),
            ("rXYZ", XyzTag([colorants[0, 0], colorants[1, 0], colorants[2, 0]])),
            ("gXYZ", XyzTag([colorants[0, 1], colorants[1, 1], colorants[2, 1]])),
            ("bXYZ", XyzTag([colorants[0, 2], colorants[1, 2], colorants[2, 2]])),
        };
        var curve = Curve();
        var offsets = new List<(string Signature, int Offset, int Size)>();
        var position = 128 + 4 + ((tags.Count + 3) * 12);
        using var data = new MemoryStream();
        foreach (var (signature, bytes) in tags)
        {
            offsets.Add((signature, position + (int)data.Length, bytes.Length));
            Append(data, bytes);
        }

        // The three tone curves share one data block.
        var curveOffset = position + (int)data.Length;
        Append(data, curve);
        offsets.AddRange(new[] { "rTRC", "gTRC", "bTRC" }.Select(s => (s, curveOffset, curve.Length)));
        var profile = new byte[position + data.Length];
        Header(profile);
        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(128), (uint)offsets.Count);
        for (var i = 0; i < offsets.Count; i++)
        {
            var entry = profile.AsSpan(132 + (i * 12));
            Encoding.ASCII.GetBytes(offsets[i].Signature).CopyTo(entry);
            BinaryPrimitives.WriteUInt32BigEndian(entry[4..], (uint)offsets[i].Offset);
            BinaryPrimitives.WriteUInt32BigEndian(entry[8..], (uint)offsets[i].Size);
        }

        data.ToArray().CopyTo(profile, position);
        return profile;
    }

    // ICC.1:2001-04 §6.1: size, version 2.1, display class, RGB data, XYZ connection space, a fixed date (equal bytes on
    // every call), the 'acsp' signature, perceptual intent and the D50 illuminant.
    private static void Header(byte[] profile)
    {
        BinaryPrimitives.WriteUInt32BigEndian(profile, (uint)profile.Length);
        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(8), 0x02100000);
        "mntrRGB XYZ "u8.CopyTo(profile.AsSpan(12));
        ushort[] date = [2026, 10, 10, 0, 0, 0];
        for (var i = 0; i < date.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(profile.AsSpan(24 + (i * 2)), date[i]);
        }

        "acsp"u8.CopyTo(profile.AsSpan(36));
        for (var i = 0; i < 3; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(profile.AsSpan(68 + (i * 4)), Fixed(D50[i]));
        }
    }

    private static void Append(MemoryStream data, byte[] bytes)
    {
        data.Write(bytes);
        while (data.Length % 4 != 0)
        {
            data.WriteByte(0);
        }
    }

    // textDescriptionType (ICC.1:2001-04 §6.5.17): the ASCII description, no Unicode or ScriptCode text.
    private static byte[] Description(string text)
    {
        var data = new byte[12 + text.Length + 1 + 8 + 3 + 67];
        "desc"u8.CopyTo(data);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), (uint)text.Length + 1);
        Encoding.ASCII.GetBytes(text).CopyTo(data, 12);
        return data;
    }

    // textType (§6.5.18): NUL-terminated ASCII.
    private static byte[] Text(string text)
    {
        var data = new byte[8 + text.Length + 1];
        "text"u8.CopyTo(data);
        Encoding.ASCII.GetBytes(text).CopyTo(data, 8);
        return data;
    }

    // XYZType (§6.5.19): one s15Fixed16 triplet.
    private static byte[] XyzTag(double[] xyz)
    {
        var data = new byte[20];
        "XYZ "u8.CopyTo(data);
        for (var i = 0; i < 3; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(8 + (i * 4)), Fixed(xyz[i]));
        }

        return data;
    }

    // curveType (§6.5.3): the transfer function sampled at evenly spaced inputs, as unsigned 16-bit fractions.
    private static byte[] Curve()
    {
        var data = new byte[12 + (CurvePoints * 2)];
        "curv"u8.CopyTo(data);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), CurvePoints);
        for (var i = 0; i < CurvePoints; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(12 + (i * 2)), (ushort)Math.Round(Linear(i / (double)(CurvePoints - 1)) * 65535));
        }

        return data;
    }

    private static int Fixed(double value) => (int)Math.Round(value * 65536);

    private static double[] Xyz(double x, double y) => [x / y, 1, (1 - x - y) / y];

    // The linear Bradford chromatic adaptation from one white to another: cone responses scaled by the ratio of the whites.
    private static double[,] Bradford(double[] from, double[] to)
    {
        double[,] cone = { { 0.8951, 0.2664, -0.1614 }, { -0.7502, 1.7135, 0.0367 }, { 0.0389, -0.0685, 1.0296 } };
        var (source, target) = (Multiply(cone, from), Multiply(cone, to));
        var scale = new double[3, 3];
        for (var i = 0; i < 3; i++)
        {
            scale[i, i] = target[i] / source[i];
        }

        return Multiply(Invert(cone), Multiply(scale, cone));
    }

    private static double[] Multiply(double[,] m, double[] v) =>
        [.. Enumerable.Range(0, 3).Select(r => (m[r, 0] * v[0]) + (m[r, 1] * v[1]) + (m[r, 2] * v[2]))];

    private static double[,] Multiply(double[,] a, double[,] b)
    {
        var result = new double[3, 3];
        for (var r = 0; r < 3; r++)
        {
            for (var c = 0; c < 3; c++)
            {
                result[r, c] = (a[r, 0] * b[0, c]) + (a[r, 1] * b[1, c]) + (a[r, 2] * b[2, c]);
            }
        }

        return result;
    }

    // The inverse by cofactors (the matrices here are well conditioned).
    private static double[,] Invert(double[,] m)
    {
        var determinant = (m[0, 0] * ((m[1, 1] * m[2, 2]) - (m[1, 2] * m[2, 1])))
            - (m[0, 1] * ((m[1, 0] * m[2, 2]) - (m[1, 2] * m[2, 0])))
            + (m[0, 2] * ((m[1, 0] * m[2, 1]) - (m[1, 1] * m[2, 0])));
        var result = new double[3, 3];
        for (var r = 0; r < 3; r++)
        {
            for (var c = 0; c < 3; c++)
            {
                var (r1, r2) = ((c + 1) % 3, (c + 2) % 3);
                var (c1, c2) = ((r + 1) % 3, (r + 2) % 3);
                result[r, c] = ((m[r1, c1] * m[r2, c2]) - (m[r1, c2] * m[r2, c1])) / determinant;
            }
        }

        return result;
    }
}
