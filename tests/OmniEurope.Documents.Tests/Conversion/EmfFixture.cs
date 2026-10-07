// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;
using System.Text;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>Writes small Enhanced Metafiles record by record, following the EMF specification layouts.</summary>
internal sealed class EmfFixture
{
    private readonly List<byte[]> _records = [];

    /// <summary>A 400 x 200 device-unit picture (about 105.8 x 52.9 mm); <paramref name="emptyFrame"/> leaves the frame at zero.</summary>
    public byte[] ToArray(bool emptyFrame = false)
    {
        var header = new byte[88];
        var span = header.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], 88);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 399);
        BinaryPrimitives.WriteInt32LittleEndian(span[20..], 199);
        BinaryPrimitives.WriteInt32LittleEndian(span[32..], emptyFrame ? 0 : 10583);
        BinaryPrimitives.WriteInt32LittleEndian(span[36..], emptyFrame ? 0 : 5291);
        BinaryPrimitives.WriteUInt32LittleEndian(span[40..], 0x464D4520);
        BinaryPrimitives.WriteInt32LittleEndian(span[72..], 1000);
        BinaryPrimitives.WriteInt32LittleEndian(span[76..], 1000);
        BinaryPrimitives.WriteInt32LittleEndian(span[80..], 264);
        BinaryPrimitives.WriteInt32LittleEndian(span[84..], 264);
        var end = Record(14, w => { w.Write(0u); w.Write(16u); w.Write(20u); });

        // Version 1.0, total size, record count (header and end included) and handle table size.
        BinaryPrimitives.WriteUInt32LittleEndian(span[44..], 0x10000);
        BinaryPrimitives.WriteInt32LittleEndian(span[48..], header.Length + _records.Sum(r => r.Length) + end.Length);
        BinaryPrimitives.WriteInt32LittleEndian(span[52..], _records.Count + 2);
        BinaryPrimitives.WriteUInt16LittleEndian(span[56..], 16);
        return [.. header, .. _records.SelectMany(r => r), .. end];
    }

    public EmfFixture Add(uint type, Action<BinaryWriter> write)
    {
        _records.Add(Record(type, write));
        return this;
    }

    public EmfFixture Ints(uint type, params int[] values) => Add(type, w => Array.ForEach(values, w.Write));

    public EmfFixture Brush(uint handle, int rgb) => Add(39, w => { w.Write(handle); w.Write(0u); w.Write(rgb); w.Write(0u); });

    /// <summary>CREATEPEN; style 5 is the null pen. Colours are COLORREF values (0x00BBGGRR).</summary>
    public EmfFixture Pen(uint handle, int width, int rgb, uint style = 0) => Add(38, w => { w.Write(handle); w.Write(style); w.Write(width); w.Write(0); w.Write(rgb); });

    public EmfFixture Select(uint handle) => Ints(37, (int)handle);

    /// <summary>A 16-bit point record (polygon, polyline, Bézier) with a bounds rectangle.</summary>
    public EmfFixture Points16(uint type, params (short X, short Y)[] points) => Add(type, w =>
    {
        w.Write(0); w.Write(0); w.Write(400); w.Write(200);
        w.Write(points.Length);
        foreach (var (x, y) in points)
        {
            w.Write(x);
            w.Write(y);
        }
    });

    public EmfFixture PolyPolygon16(params (short X, short Y)[][] polygons) => PolyPoly(91, small: true, polygons);

    /// <summary>A 32-bit point record (polygon 3, polyline 4, Bézier 2, Bézier-to 5, polyline-to 6).</summary>
    public EmfFixture Points32(uint type, params (short X, short Y)[] points) => Add(type, w =>
    {
        w.Write(0); w.Write(0); w.Write(400); w.Write(200);
        w.Write(points.Length);
        foreach (var (x, y) in points)
        {
            w.Write((int)x);
            w.Write((int)y);
        }
    });

    /// <summary>A poly-polygon (91, 8) or poly-polyline (90, 7) record with 16- or 32-bit points.</summary>
    public EmfFixture PolyPoly(uint type, bool small, params (short X, short Y)[][] polygons) => Add(type, w =>
    {
        w.Write(0); w.Write(0); w.Write(400); w.Write(200);
        w.Write(polygons.Length);
        w.Write(polygons.Sum(p => p.Length));
        Array.ForEach(polygons, p => w.Write(p.Length));
        foreach (var (x, y) in polygons.SelectMany(p => p))
        {
            if (small)
            {
                w.Write(x);
                w.Write(y);
            }
            else
            {
                w.Write((int)x);
                w.Write((int)y);
            }
        }
    });

    /// <summary>SETWORLDTRANSFORM (35) or MODIFYWORLDTRANSFORM (36, with its mode): an XFORM of six floats.</summary>
    public EmfFixture World(uint type, float m11, float m12, float m21, float m22, float dx, float dy, int mode = 0) => Add(type, w =>
    {
        w.Write(m11); w.Write(m12); w.Write(m21); w.Write(m22); w.Write(dx); w.Write(dy);
        if (type == 36)
        {
            w.Write(mode);
        }
    });

    public EmfFixture Font(uint handle, int height, int weight, string face, int escapement = 0) => Add(82, w =>
    {
        w.Write(handle);
        w.Write(height); w.Write(0); w.Write(escapement); w.Write(0); w.Write(weight);
        w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); w.Write((byte)0);
        w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); w.Write((byte)0);
        var name = new byte[64];
        Encoding.Unicode.GetBytes(face).CopyTo(name, 0);
        w.Write(name);
    });

    public EmfFixture Text(string text, int x, int y) => Add(84, w =>
    {
        w.Write(0); w.Write(0); w.Write(400); w.Write(200);
        w.Write(1u); w.Write(1f); w.Write(1f);
        w.Write(x); w.Write(y); w.Write(text.Length); w.Write(76u); w.Write(0u);
        w.Write(0); w.Write(0); w.Write(0); w.Write(0); w.Write(0u);
        w.Write(Encoding.Unicode.GetBytes(text));
    });

    /// <summary>A STRETCHDIBITS record drawing a 2 x 2 bitmap (24 bits) into the destination rectangle.</summary>
    public EmfFixture Bitmap(int x, int y, int width, int height) => Add(81, w =>
    {
        w.Write(0); w.Write(0); w.Write(400); w.Write(200);
        w.Write(x); w.Write(y); w.Write(0); w.Write(0); w.Write(2); w.Write(2);
        w.Write(80); w.Write(40); w.Write(120); w.Write(16);
        w.Write(0u); w.Write(0x00CC0020u); w.Write(width); w.Write(height);
        w.Write(40); w.Write(2); w.Write(2); w.Write((short)1); w.Write((short)24);
        w.Write(0); w.Write(16); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
        w.Write(new byte[] { 0, 0, 255, 0, 255, 0, 0, 0, 255, 0, 0, 255, 255, 255, 0, 0 });
    });

    private static byte[] Record(uint type, Action<BinaryWriter> write)
    {
        using var payload = new MemoryStream();
        using (var writer = new BinaryWriter(payload, Encoding.Unicode, leaveOpen: true))
        {
            write(writer);
        }

        using var record = new MemoryStream();
        using (var writer = new BinaryWriter(record))
        {
            writer.Write(type);
            writer.Write((uint)(8 + payload.Length));
            writer.Write(payload.ToArray());
        }

        return record.ToArray();
    }
}
