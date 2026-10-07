// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Fonts;

/// <summary>A point of a glyph contour in font units; off-curve points are quadratic control points.</summary>
public readonly record struct GlyphPoint(double X, double Y, bool OnCurve);

/// <summary>The contours of a glyph (composites already resolved and transformed).</summary>
public sealed class GlyphOutline(IReadOnlyList<IReadOnlyList<GlyphPoint>> contours)
{
    /// <summary>The closed contours.</summary>
    public IReadOnlyList<IReadOnlyList<GlyphPoint>> Contours { get; } = contours;
}

/// <summary>Access to the <c>glyf</c> and <c>loca</c> tables: raw glyph data, components and outlines.</summary>
public sealed class TrueTypeGlyphs
{
    private const int MaxComponentDepth = 8;
    private readonly TrueTypeFont _owner;
    private readonly FontReader _font;
    private readonly FontTable _glyf;
    private readonly FontTable _loca;

    internal TrueTypeGlyphs(TrueTypeFont owner, FontReader font, FontTable glyf, FontTable loca)
    {
        _owner = owner;
        _font = font;
        _glyf = glyf;
        _loca = loca;
    }

    /// <summary>The raw bytes of a glyph (empty for a glyph with no outline).</summary>
    public ReadOnlySpan<byte> RawGlyph(int glyph)
    {
        var (start, end) = Range(glyph);
        return end > start ? _font.Slice(_glyf.Offset + start, end - start) : [];
    }

    /// <summary>The glyphs a composite glyph is built from (empty for a simple glyph).</summary>
    public IReadOnlyList<int> Components(int glyph)
    {
        var data = RawGlyph(glyph);
        var result = new List<int>();
        if (data.Length < 10 || (short)((data[0] << 8) | data[1]) >= 0)
        {
            return result;
        }

        var position = 10;
        while (position + 4 <= data.Length)
        {
            var flags = (data[position] << 8) | data[position + 1];
            result.Add((data[position + 2] << 8) | data[position + 3]);
            position += ComponentSize(flags);
            if ((flags & 0x0020) == 0)
            {
                break;
            }
        }

        return result;
    }

    /// <summary>The outline of a glyph, or an empty outline for spaces and missing glyphs.</summary>
    public GlyphOutline GetOutline(int glyph) => new(Contours(glyph, 0));

    internal static int ComponentSize(int flags)
    {
        var size = 4 + ((flags & 0x0001) != 0 ? 4 : 2);
        if ((flags & 0x0008) != 0)
        {
            size += 2;
        }
        else if ((flags & 0x0040) != 0)
        {
            size += 4;
        }
        else if ((flags & 0x0080) != 0)
        {
            size += 8;
        }

        return size;
    }

    private (int Start, int End) Range(int glyph)
    {
        if (glyph < 0 || glyph >= _owner.GlyphCount)
        {
            return (0, 0);
        }

        int start;
        int end;
        if (_owner.IndexToLocLong)
        {
            start = (int)_font.U32(_loca.Offset + (glyph * 4));
            end = (int)_font.U32(_loca.Offset + (glyph * 4) + 4);
        }
        else
        {
            start = _font.U16(_loca.Offset + (glyph * 2)) * 2;
            end = _font.U16(_loca.Offset + (glyph * 2) + 2) * 2;
        }

        return end > start && end <= _glyf.Length ? (start, end) : (0, 0);
    }

    private List<IReadOnlyList<GlyphPoint>> Contours(int glyph, int depth)
    {
        var data = RawGlyph(glyph);
        if (data.Length < 10)
        {
            return [];
        }

        var contourCount = (short)((data[0] << 8) | data[1]);
        try
        {
            return contourCount >= 0 ? SimpleContours(data, contourCount) : CompositeContours(data, depth);
        }
        catch (IndexOutOfRangeException)
        {
            // A truncated glyph draws nothing rather than failing the whole page.
            return [];
        }
    }

    private static List<IReadOnlyList<GlyphPoint>> SimpleContours(ReadOnlySpan<byte> data, int contourCount)
    {
        var ends = new int[contourCount];
        for (var i = 0; i < contourCount; i++)
        {
            ends[i] = (data[10 + (i * 2)] << 8) | data[11 + (i * 2)];
        }

        var pointCount = contourCount == 0 ? 0 : ends[^1] + 1;
        var position = 10 + (contourCount * 2);
        var instructionLength = (data[position] << 8) | data[position + 1];
        position += 2 + instructionLength;
        var flags = new byte[pointCount];
        for (var i = 0; i < pointCount;)
        {
            var flag = data[position++];
            flags[i++] = flag;
            if ((flag & 0x08) != 0)
            {
                for (var repeat = data[position++]; repeat > 0 && i < pointCount; repeat--)
                {
                    flags[i++] = flag;
                }
            }
        }

        var xs = ReadCoordinates(data, ref position, flags, 0x02, 0x10);
        var ys = ReadCoordinates(data, ref position, flags, 0x04, 0x20);
        var contours = new List<IReadOnlyList<GlyphPoint>>(contourCount);
        var start = 0;
        foreach (var end in ends)
        {
            var contour = new List<GlyphPoint>(end - start + 1);
            for (var p = start; p <= end && p < pointCount; p++)
            {
                contour.Add(new GlyphPoint(xs[p], ys[p], (flags[p] & 0x01) != 0));
            }

            contours.Add(contour);
            start = end + 1;
        }

        return contours;
    }

    private static int[] ReadCoordinates(ReadOnlySpan<byte> data, ref int position, byte[] flags, int shortFlag, int sameFlag)
    {
        var values = new int[flags.Length];
        var current = 0;
        for (var i = 0; i < flags.Length; i++)
        {
            var flag = flags[i];
            if ((flag & shortFlag) != 0)
            {
                var delta = data[position++];
                current += (flag & sameFlag) != 0 ? delta : -delta;
            }
            else if ((flag & sameFlag) == 0)
            {
                current += (short)((data[position] << 8) | data[position + 1]);
                position += 2;
            }

            values[i] = current;
        }

        return values;
    }

    private List<IReadOnlyList<GlyphPoint>> CompositeContours(ReadOnlySpan<byte> data, int depth)
    {
        var result = new List<IReadOnlyList<GlyphPoint>>();
        if (depth >= MaxComponentDepth)
        {
            return result;
        }

        var position = 10;
        while (position + 4 <= data.Length)
        {
            var flags = (data[position] << 8) | data[position + 1];
            var component = (data[position + 2] << 8) | data[position + 3];
            var transform = CompositeTransform.Read(data, position + 4, flags);
            foreach (var contour in Contours(component, depth + 1))
            {
                result.Add(contour.Select(transform.Apply).ToList());
            }

            position += ComponentSize(flags);
            if ((flags & 0x0020) == 0)
            {
                break;
            }
        }

        return result;
    }

    // The placement of a component: offsets (point matching is approximated by zero offsets) and a 2x2 scale.
    private readonly record struct CompositeTransform(double A, double B, double C, double D, double Dx, double Dy)
    {
        public static CompositeTransform Read(ReadOnlySpan<byte> data, int position, int flags)
        {
            double dx;
            double dy;
            if ((flags & 0x0001) != 0)
            {
                dx = (short)((data[position] << 8) | data[position + 1]);
                dy = (short)((data[position + 2] << 8) | data[position + 3]);
                position += 4;
            }
            else
            {
                dx = (sbyte)data[position];
                dy = (sbyte)data[position + 1];
                position += 2;
            }

            if ((flags & 0x0002) == 0)
            {
                dx = 0;
                dy = 0;
            }

            if ((flags & 0x0008) != 0)
            {
                var s = F2Dot14(data, position);
                return new CompositeTransform(s, 0, 0, s, dx, dy);
            }

            if ((flags & 0x0040) != 0)
            {
                return new CompositeTransform(F2Dot14(data, position), 0, 0, F2Dot14(data, position + 2), dx, dy);
            }

            return (flags & 0x0080) != 0
                ? new CompositeTransform(F2Dot14(data, position), F2Dot14(data, position + 2), F2Dot14(data, position + 4), F2Dot14(data, position + 6), dx, dy)
                : new CompositeTransform(1, 0, 0, 1, dx, dy);
        }

        public GlyphPoint Apply(GlyphPoint p) => new((A * p.X) + (C * p.Y) + Dx, (B * p.X) + (D * p.Y) + Dy, p.OnCurve);

        private static double F2Dot14(ReadOnlySpan<byte> data, int position) => (short)((data[position] << 8) | data[position + 1]) / 16384.0;
    }
}
