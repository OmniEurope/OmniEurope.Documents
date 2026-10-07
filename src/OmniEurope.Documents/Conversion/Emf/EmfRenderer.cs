// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;
using System.Text;
using OmniEurope.Documents.Conversion.WordLayout;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Conversion.Emf;

/// <summary>
/// Draws an Enhanced Metafile into a rectangle of a PDF page as vector graphics: lines, polygons,
/// polylines, Bézier curves, rectangles, ellipses, paths, text and bitmaps, with pens, brushes, fonts,
/// map modes, window and viewport mappings and world transforms. Clipping regions, raster operations,
/// dashed pens, hatches, rotated text and EMF+ records are not interpreted (an EMF+ file keeps its GDI
/// fallback records, which are drawn).
/// </summary>
internal static class EmfRenderer
{
    private const uint Signature = 0x464D4520;

    /// <summary>Draws the metafile; false when it is not a readable EMF or draws nothing.</summary>
    public static bool TryDraw(PaintContext paint, byte[] data, double x, double y, double width, double height)
    {
        if (data.Length < 88 || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0)) != 1 || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(40)) != Signature)
        {
            return false;
        }

        var span = data.AsSpan();
        var device = (Int(span, 72), Int(span, 76));
        var millimetres = (Int(span, 80), Int(span, 84));
        var pixelsPerMm = (millimetres.Item1 > 0 ? device.Item1 / (double)millimetres.Item1 : 3.78, millimetres.Item2 > 0 ? device.Item2 / (double)millimetres.Item2 : 3.78);
        var frame = (Left: Int(span, 24) * pixelsPerMm.Item1 / 100, Top: Int(span, 28) * pixelsPerMm.Item2 / 100, Right: Int(span, 32) * pixelsPerMm.Item1 / 100, Bottom: Int(span, 36) * pixelsPerMm.Item2 / 100);
        if (frame.Right - frame.Left < 1 || frame.Bottom - frame.Top < 1)
        {
            frame = (Int(span, 8), Int(span, 12), Int(span, 16) + 1, Int(span, 20) + 1);
        }

        var output = new EmfTransform(width / (frame.Right - frame.Left), 0, 0, height / (frame.Bottom - frame.Top), x - (frame.Left * width / (frame.Right - frame.Left)), y - (frame.Top * height / (frame.Bottom - frame.Top)));
        paint.Canvas.SaveState();
        paint.Canvas.ClipRectangle(x, y, width, height);
        var player = new EmfPlayer(paint, output, pixelsPerMm);
        try
        {
            Play(span, player);
        }
        finally
        {
            paint.Canvas.RestoreState();
        }

        return player.Drawn;
    }

    private static void Play(ReadOnlySpan<byte> data, EmfPlayer player)
    {
        var offset = 0;
        while (offset + 8 <= data.Length)
        {
            var type = BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(data[(offset + 4)..]);
            if (size < 8 || offset + size > data.Length || type == 14)
            {
                return;
            }

            player.Record(type, data.Slice(offset, size));
            offset += size;
        }
    }

    public static int Int(ReadOnlySpan<byte> data, int offset) => offset + 4 <= data.Length ? BinaryPrimitives.ReadInt32LittleEndian(data[offset..]) : 0;

    public static uint UInt(ReadOnlySpan<byte> data, int offset) => offset + 4 <= data.Length ? BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]) : 0;

    public static double Float(ReadOnlySpan<byte> data, int offset) => offset + 4 <= data.Length ? BinaryPrimitives.ReadSingleLittleEndian(data[offset..]) : 0;

    public static PdfColor Color(ReadOnlySpan<byte> data, int offset) =>
        offset + 3 <= data.Length ? new PdfColor(data[offset], data[offset + 1], data[offset + 2]) : PdfColor.Black;

    public static string Utf16(ReadOnlySpan<byte> data, int offset, int characters)
    {
        var length = Math.Min(characters * 2, Math.Max(0, data.Length - offset));
        return length <= 0 ? string.Empty : Encoding.Unicode.GetString(data.Slice(offset, length & ~1));
    }
}

/// <summary>Executes EMF records against a PDF canvas.</summary>
internal sealed class EmfPlayer(PaintContext paint, EmfTransform output, (double X, double Y) pixelsPerMm)
{
    private readonly Dictionary<uint, object> _objects = [];
    private readonly Stack<EmfState> _saved = new();
    private readonly List<List<(double X, double Y)>> _path = [];
    private EmfState _state = new();
    private bool _inPath;

    public bool Drawn { get; private set; }

    private PdfCanvas Canvas => paint.Canvas;

    private static readonly object[] StockObjects =
    [
        new EmfBrush(PdfColor.White, true), new EmfBrush(new PdfColor(192, 192, 192), true), new EmfBrush(new PdfColor(128, 128, 128), true),
        new EmfBrush(new PdfColor(64, 64, 64), true), new EmfBrush(PdfColor.Black, true), new EmfBrush(PdfColor.White, false),
        new EmfPen(PdfColor.White, 0, true), new EmfPen(PdfColor.Black, 0, true), new EmfPen(PdfColor.Black, 0, false),
    ];

    // What each record type does; the types not listed (clipping, palettes, comments...) are skipped.
    private static readonly Dictionary<uint, Handler> Handlers = new()
    {
        [9] = (p, r) => p._state = p._state with { WindowExtent = Pair(r) },
        [10] = (p, r) => p._state = p._state with { WindowOrigin = Pair(r) },
        [11] = (p, r) => p._state = p._state with { ViewportExtent = Pair(r) },
        [12] = (p, r) => p._state = p._state with { ViewportOrigin = Pair(r) },
        [17] = (p, r) => p._state = p._state with { MapMode = EmfRenderer.Int(r, 8) },
        [19] = (p, r) => p._state = p._state with { EvenOdd = EmfRenderer.Int(r, 8) != 2 },
        [22] = (p, r) => p._state = p._state with { TextAlign = EmfRenderer.Int(r, 8) },
        [24] = (p, r) => p._state = p._state with { TextColor = EmfRenderer.Color(r, 8) },
        [27] = (p, r) => p._state = p._state with { Position = Pair(r) },
        [33] = (p, _) => p._saved.Push(p._state),
        [34] = (p, r) => p.Restore(EmfRenderer.Int(r, 8)),
        [35] = (p, r) => p._state = p._state with { World = Transform(r, 8) },
        [36] = (p, r) => p.ModifyWorld(r),
        [37] = (p, r) => p.Select(EmfRenderer.UInt(r, 8)),
        [40] = (p, r) => p._objects.Remove(EmfRenderer.UInt(r, 8)),
        [38] = (p, r) => p._objects[EmfRenderer.UInt(r, 8)] = new EmfPen(EmfRenderer.Color(r, 24), EmfRenderer.Int(r, 16), (EmfRenderer.UInt(r, 12) & 0xF) != 5),
        [95] = (p, r) => p._objects[EmfRenderer.UInt(r, 8)] = new EmfPen(EmfRenderer.Color(r, 40), EmfRenderer.UInt(r, 32), (EmfRenderer.UInt(r, 28) & 0xF) != 5),
        [39] = (p, r) => p._objects[EmfRenderer.UInt(r, 8)] = new EmfBrush(EmfRenderer.Color(r, 16), EmfRenderer.UInt(r, 12) != 1),
        [82] = (p, r) => p._objects[EmfRenderer.UInt(r, 8)] = Font(r),
        [86] = (p, r) => p.Shape([Points(r, 24, small: true)], closed: true),
        [3] = (p, r) => p.Shape([Points(r, 24, small: false)], closed: true),
        [87] = (p, r) => p.Shape([Points(r, 24, small: true)], closed: false),
        [4] = (p, r) => p.Shape([Points(r, 24, small: false)], closed: false),
        [85] = (p, r) => p.Shape([Bezier(Points(r, 24, small: true))], closed: false),
        [2] = (p, r) => p.Shape([Bezier(Points(r, 24, small: false))], closed: false),
        [91] = (p, r) => p.Shape(PolyPoints(r, small: true), closed: true),
        [8] = (p, r) => p.Shape(PolyPoints(r, small: false), closed: true),
        [90] = (p, r) => p.Shape(PolyPoints(r, small: true), closed: false),
        [7] = (p, r) => p.Shape(PolyPoints(r, small: false), closed: false),
        [89] = (p, r) => p.LineTo(Points(r, 24, small: true)),
        [6] = (p, r) => p.LineTo(Points(r, 24, small: false)),
        [88] = (p, r) => p.BezierTo(Points(r, 24, small: true)),
        [5] = (p, r) => p.BezierTo(Points(r, 24, small: false)),
        [54] = (p, r) => p.LineTo([Pair(r)]),
        [43] = (p, r) => p.Shape([Rectangle(r)], closed: true),
        [44] = (p, r) => p.Shape([Rectangle(r)], closed: true),
        [42] = (p, r) => p.Shape([Ellipse(r)], closed: true),
        [59] = (p, _) => p.BeginPath(),
        [60] = (p, _) => p._inPath = false,
        [68] = (p, _) => p.BeginPath(open: false),
        [62] = (p, _) => p.DrawPath(fill: true, stroke: false, closed: true),
        [63] = (p, _) => p.DrawPath(fill: true, stroke: true, closed: true),
        [64] = (p, _) => p.DrawPath(fill: false, stroke: true, closed: false),
        [84] = (p, r) => p.Text(r),
        [81] = (p, r) => p.Bitmap(r, EmfRenderer.Int(r, 24), EmfRenderer.Int(r, 28), EmfRenderer.Int(r, 72), EmfRenderer.Int(r, 76), 48),
        [76] = (p, r) => p.Bitmap(r, EmfRenderer.Int(r, 24), EmfRenderer.Int(r, 28), EmfRenderer.Int(r, 32), EmfRenderer.Int(r, 36), 84),
        [77] = (p, r) => p.Bitmap(r, EmfRenderer.Int(r, 24), EmfRenderer.Int(r, 28), EmfRenderer.Int(r, 32), EmfRenderer.Int(r, 36), 84),
    };

    private delegate void Handler(EmfPlayer player, ReadOnlySpan<byte> record);

    public void Record(uint type, ReadOnlySpan<byte> r)
    {
        if (Handlers.TryGetValue(type, out var handler))
        {
            handler(this, r);
        }
    }

    private static (double X, double Y) Pair(ReadOnlySpan<byte> r) => (EmfRenderer.Int(r, 8), EmfRenderer.Int(r, 12));

    private static EmfFont Font(ReadOnlySpan<byte> r) =>
        new(EmfRenderer.Int(r, 12), EmfRenderer.Int(r, 28), r.Length > 32 && r[32] != 0, EmfRenderer.Utf16(r, 40, 32).Split('\0')[0], EmfRenderer.Int(r, 20));

    // Stock objects have the high bit set: brushes 0-5 (white, light grey, grey, dark grey, black, none), pens 6-8.
    private void Select(uint handle)
    {
        var selected = (handle & 0x80000000) != 0 ? StockObjects.ElementAtOrDefault((int)(handle & 0xFF)) : _objects.GetValueOrDefault(handle);
        _state = selected switch
        {
            EmfPen pen => _state with { Pen = pen },
            EmfBrush brush => _state with { Brush = brush },
            EmfFont font => _state with { Font = font },
            _ => _state,
        };
    }

    private void BeginPath(bool open = true)
    {
        _path.Clear();
        _inPath = open;
    }

    private void DrawPath(bool fill, bool stroke, bool closed)
    {
        Draw(_path, fill, stroke, closed);
        _path.Clear();
    }

    private void BezierTo(List<(double X, double Y)> points) => LineTo(Bezier([_state.Position, .. points]).Skip(1).ToList());

    private void Restore(int relative)
    {
        var count = relative < 0 ? -relative : 1;
        for (var i = 0; i < count && _saved.Count > 0; i++)
        {
            _state = _saved.Pop();
        }
    }

    private static EmfTransform Transform(ReadOnlySpan<byte> r, int at) => new(
        EmfRenderer.Float(r, at), EmfRenderer.Float(r, at + 4), EmfRenderer.Float(r, at + 8),
        EmfRenderer.Float(r, at + 12), EmfRenderer.Float(r, at + 16), EmfRenderer.Float(r, at + 20));

    private void ModifyWorld(ReadOnlySpan<byte> r)
    {
        var transform = Transform(r, 8);
        _state = _state with
        {
            World = EmfRenderer.UInt(r, 32) switch
            {
                1 => EmfTransform.Identity,
                2 => transform.Then(_state.World),
                3 => _state.World.Then(transform),
                _ => transform,
            },
        };
    }

    private static List<(double X, double Y)> Points(ReadOnlySpan<byte> r, int countAt, bool small)
    {
        var count = Math.Max(0, EmfRenderer.Int(r, countAt));
        var points = new List<(double, double)>(count);
        var at = countAt + 4;
        for (var i = 0; i < count; i++)
        {
            if (small)
            {
                points.Add((BinaryPrimitives.ReadInt16LittleEndian(r[(at + (i * 4))..]), BinaryPrimitives.ReadInt16LittleEndian(r[(at + (i * 4) + 2)..])));
            }
            else
            {
                points.Add((EmfRenderer.Int(r, at + (i * 8)), EmfRenderer.Int(r, at + (i * 8) + 4)));
            }
        }

        return points;
    }

    private static List<List<(double X, double Y)>> PolyPoints(ReadOnlySpan<byte> r, bool small)
    {
        var polygons = Math.Max(0, EmfRenderer.Int(r, 24));
        var at = 32 + (polygons * 4);
        var result = new List<List<(double, double)>>();
        for (var p = 0; p < polygons; p++)
        {
            var count = EmfRenderer.Int(r, 32 + (p * 4));
            var polygon = new List<(double, double)>(count);
            for (var i = 0; i < count && at + (small ? 4 : 8) <= r.Length; i++, at += small ? 4 : 8)
            {
                polygon.Add(small
                    ? (BinaryPrimitives.ReadInt16LittleEndian(r[at..]), BinaryPrimitives.ReadInt16LittleEndian(r[(at + 2)..]))
                    : (EmfRenderer.Int(r, at), EmfRenderer.Int(r, at + 4)));
            }

            result.Add(polygon);
        }

        return result;
    }

    private static List<(double X, double Y)> Rectangle(ReadOnlySpan<byte> r)
    {
        double left = EmfRenderer.Int(r, 8), top = EmfRenderer.Int(r, 12), right = EmfRenderer.Int(r, 16), bottom = EmfRenderer.Int(r, 20);
        return [(left, top), (right, top), (right, bottom), (left, bottom)];
    }

    private static List<(double X, double Y)> Ellipse(ReadOnlySpan<byte> r)
    {
        double left = EmfRenderer.Int(r, 8), top = EmfRenderer.Int(r, 12), right = EmfRenderer.Int(r, 16), bottom = EmfRenderer.Int(r, 20);
        var (cx, cy, rx, ry) = ((left + right) / 2, (top + bottom) / 2, (right - left) / 2, (bottom - top) / 2);
        return Enumerable.Range(0, 48).Select(i => (cx + (rx * Math.Cos(i * Math.PI / 24)), cy + (ry * Math.Sin(i * Math.PI / 24)))).ToList();
    }

    // Cubic Bézier segments (start, then three points per segment) flattened into lines.
    private static List<(double X, double Y)> Bezier(List<(double X, double Y)> points)
    {
        if (points.Count < 4)
        {
            return points;
        }

        var result = new List<(double, double)> { points[0] };
        for (var i = 1; i + 2 < points.Count; i += 3)
        {
            var (p0, p1, p2, p3) = (points[i - 1], points[i], points[i + 1], points[i + 2]);
            for (var step = 1; step <= 12; step++)
            {
                var t = step / 12.0;
                var u = 1 - t;
                result.Add((
                    (u * u * u * p0.X) + (3 * u * u * t * p1.X) + (3 * u * t * t * p2.X) + (t * t * t * p3.X),
                    (u * u * u * p0.Y) + (3 * u * u * t * p1.Y) + (3 * u * t * t * p2.Y) + (t * t * t * p3.Y)));
            }
        }

        return result;
    }

    private void LineTo(List<(double X, double Y)> points)
    {
        if (points.Count == 0)
        {
            return;
        }

        var line = new List<(double X, double Y)> { _state.Position };
        line.AddRange(points);
        _state = _state with { Position = points[^1] };
        if (_inPath)
        {
            AppendToPath(line);
            return;
        }

        Draw([line], fill: false, stroke: true, closed: false);
    }

    private void AppendToPath(List<(double X, double Y)> line)
    {
        if (_path.Count > 0 && _path[^1].Count > 0 && _path[^1][^1] == line[0])
        {
            _path[^1].AddRange(line.Skip(1));
        }
        else
        {
            _path.Add(line);
        }
    }

    private void Shape(List<List<(double X, double Y)>> polygons, bool closed)
    {
        if (_inPath)
        {
            _path.AddRange(polygons);
            return;
        }

        Draw(polygons, fill: closed, stroke: true, closed);
    }

    private void Draw(List<List<(double X, double Y)>> polygons, bool fill, bool stroke, bool closed)
    {
        var transform = _state.PageTransform(pixelsPerMm).Then(output);
        var mapped = polygons.Where(p => p.Count > 1).Select(p => (IReadOnlyList<(double X, double Y)>)p.Select(q => transform.Apply(q.X, q.Y)).ToList()).ToList();
        var fillColor = fill && _state.Brush.Visible ? _state.Brush.Color : (PdfColor?)null;
        var strokeColor = stroke && _state.Pen.Visible ? _state.Pen.Color : (PdfColor?)null;
        if (mapped.Count == 0 || (fillColor is null && strokeColor is null))
        {
            return;
        }

        var scale = Math.Sqrt(Math.Abs((transform.M11 * transform.M22) - (transform.M12 * transform.M21)));
        Canvas.DrawPath(mapped, fillColor, strokeColor, Math.Max(_state.Pen.Width * scale, 0.25), _state.EvenOdd, closed);
        Drawn = true;
    }

    private void Text(ReadOnlySpan<byte> r)
    {
        var text = EmfRenderer.Utf16(r, EmfRenderer.Int(r, 48), EmfRenderer.Int(r, 44));
        if (text.Length == 0)
        {
            return;
        }

        var transform = _state.PageTransform(pixelsPerMm).Then(output);
        var reference = (_state.TextAlign & 1) != 0 ? _state.Position : (EmfRenderer.Int(r, 36), EmfRenderer.Int(r, 40));
        var (x, y) = transform.Apply(reference.Item1, reference.Item2);
        var font = _state.Font;
        var size = Math.Abs(font.Height) * Math.Abs(transform.M22) * (font.Height > 0 ? 0.85 : 1);
        if (size < 0.5)
        {
            return;
        }

        var pdfFont = new PdfFont(font.Face.Length > 0 ? font.Face : "Arial", font.Weight >= 600, font.Italic);
        var width = paint.Layout.Measure(text, new TextStyle(pdfFont, size, _state.TextColor));
        var metrics = paint.Layout.Builder.Metrics(pdfFont, size);
        x -= (_state.TextAlign & 6) switch
        {
            6 => width / 2,
            2 => width,
            _ => 0,
        };
        var baseline = (_state.TextAlign & 24) switch
        {
            24 => y,
            8 => y - metrics.Descent,
            _ => y + metrics.Ascent,
        };
        if (font.Escapement != 0)
        {
            paint.Layout.Gaps.Add("rotated text in pictures drawn horizontally");
        }

        Canvas.DrawText(text, x, baseline, pdfFont, size, _state.TextColor);
        Drawn = true;
    }

    private void Bitmap(ReadOnlySpan<byte> r, int x, int y, int width, int height, int bmiAt)
    {
        var (offBmi, cbBmi, offBits, cbBits) = (EmfRenderer.Int(r, bmiAt), EmfRenderer.Int(r, bmiAt + 4), EmfRenderer.Int(r, bmiAt + 8), EmfRenderer.Int(r, bmiAt + 12));
        if (cbBmi <= 0 || cbBits <= 0 || offBmi + cbBmi > r.Length || offBits + cbBits > r.Length)
        {
            return;
        }

        var dib = new byte[cbBmi + cbBits];
        r.Slice(offBmi, cbBmi).CopyTo(dib);
        r.Slice(offBits, cbBits).CopyTo(dib.AsSpan(cbBmi));
        RasterImage image;
        try
        {
            image = BmpDecoder.DecodeDib(dib, cbBmi);
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or ArgumentException)
        {
            paint.Layout.Gaps.Add("unreadable bitmap in a picture skipped");
            return;
        }

        var transform = _state.PageTransform(pixelsPerMm).Then(output);
        var (x1, y1) = transform.Apply(x, y);
        var (x2, y2) = transform.Apply(x + width, y + height);
        Canvas.DrawImage(paint.Layout.Builder.AddImage(image), Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        Drawn = true;
    }
}
