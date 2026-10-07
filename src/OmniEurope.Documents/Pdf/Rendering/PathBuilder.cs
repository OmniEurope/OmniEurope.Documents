// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>
/// The current path of a content stream, built in user space and flattened to device polylines: cubic
/// Bézier curves are cut into straight segments short enough to look smooth at the output resolution.
/// </summary>
internal sealed class PathBuilder
{
    private readonly List<List<Segment>> _subpaths = [];
    private readonly List<bool> _closed = [];
    private (double X, double Y) _current;
    private (double X, double Y) _start;

    public bool IsEmpty => _subpaths.Count == 0;

    public void MoveTo(double x, double y)
    {
        _subpaths.Add([new Segment(x, y, null, null)]);
        _closed.Add(false);
        _current = _start = (x, y);
    }

    public void LineTo(double x, double y)
    {
        EnsureSubpath();
        _subpaths[^1].Add(new Segment(x, y, null, null));
        _current = (x, y);
    }

    public void CurveTo(double x1, double y1, double x2, double y2, double x3, double y3)
    {
        EnsureSubpath();
        _subpaths[^1].Add(new Segment(x3, y3, (x1, y1), (x2, y2)));
        _current = (x3, y3);
    }

    /// <summary>The <c>v</c> operator: the first control point is the current point.</summary>
    public void CurveFromCurrent(double x2, double y2, double x3, double y3) => CurveTo(_current.X, _current.Y, x2, y2, x3, y3);

    public void Rectangle(double x, double y, double width, double height)
    {
        MoveTo(x, y);
        LineTo(x + width, y);
        LineTo(x + width, y + height);
        LineTo(x, y + height);
        Close();
    }

    public void Close()
    {
        if (_subpaths.Count > 0)
        {
            _closed[^1] = true;
            _current = _start;
        }
    }

    public void Clear()
    {
        _subpaths.Clear();
        _closed.Clear();
    }

    private void EnsureSubpath()
    {
        if (_subpaths.Count == 0)
        {
            MoveTo(_current.X, _current.Y);
        }
    }

    /// <summary>The path in device pixels.</summary>
    public List<Polyline> Flatten(Matrix toDevice)
    {
        var result = new List<Polyline>(_subpaths.Count);
        for (var s = 0; s < _subpaths.Count; s++)
        {
            var subpath = _subpaths[s];
            var points = new List<(double X, double Y)>(subpath.Count) { toDevice.Transform(subpath[0].X, subpath[0].Y) };
            for (var i = 1; i < subpath.Count; i++)
            {
                var segment = subpath[i];
                var end = toDevice.Transform(segment.X, segment.Y);
                if (segment.Control1 is { } c1 && segment.Control2 is { } c2)
                {
                    Curve(points, points[^1], toDevice.Transform(c1.X, c1.Y), toDevice.Transform(c2.X, c2.Y), end);
                }
                else
                {
                    points.Add(end);
                }
            }

            result.Add(new Polyline(points, _closed[s]));
        }

        return result;
    }

    private static void Curve(List<(double X, double Y)> points, (double X, double Y) p0, (double X, double Y) p1, (double X, double Y) p2, (double X, double Y) p3)
    {
        var length = Distance(p0, p1) + Distance(p1, p2) + Distance(p2, p3);
        var steps = Math.Clamp((int)Math.Ceiling(Math.Sqrt(length) * 1.5), 2, 100);
        for (var i = 1; i <= steps; i++)
        {
            var t = i / (double)steps;
            var u = 1 - t;
            points.Add((
                (u * u * u * p0.X) + (3 * u * u * t * p1.X) + (3 * u * t * t * p2.X) + (t * t * t * p3.X),
                (u * u * u * p0.Y) + (3 * u * u * t * p1.Y) + (3 * u * t * t * p2.Y) + (t * t * t * p3.Y)));
        }
    }

    private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt(((b.X - a.X) * (b.X - a.X)) + ((b.Y - a.Y) * (b.Y - a.Y)));

    private readonly record struct Segment(double X, double Y, (double X, double Y)? Control1, (double X, double Y)? Control2);
}
