// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>Line drawing parameters in device pixels.</summary>
internal sealed record StrokeStyle(double Width, int Cap, int Join, double MiterLimit, double[]? Dash, double DashPhase);

/// <summary>
/// Turns polylines into the polygons of their outline: one quadrilateral per segment, joins (miter within
/// the limit, else bevel; round) and caps (butt, round, square), after cutting dashes. All polygons are
/// given the same orientation so that filling them with the non-zero rule draws their union.
/// </summary>
internal static class Stroker
{
    public static List<IReadOnlyList<(double X, double Y)>> Outline(IEnumerable<Polyline> paths, StrokeStyle style)
    {
        var result = new List<IReadOnlyList<(double X, double Y)>>();
        var half = Math.Max(style.Width, 1) / 2;
        var pieces = style.Dash is { Length: > 0 } dash && dash.Any(d => d > 0) ? Dashes(paths, dash, style.DashPhase) : paths;
        foreach (var path in pieces)
        {
            var points = Distinct(path.Points);
            if (points.Count == 1)
            {
                if (style.Cap == 1)
                {
                    Add(result, Circle(points[0], half));
                }

                continue;
            }

            Segments(result, points, path.Closed, half, style);
        }

        return result;
    }

    private static void Segments(List<IReadOnlyList<(double X, double Y)>> result, List<(double X, double Y)> points, bool closed, double half, StrokeStyle style)
    {
        var count = closed ? points.Count : points.Count - 1;
        for (var i = 0; i < count; i++)
        {
            var (a, b) = (points[i], points[(i + 1) % points.Count]);
            var (nx, ny) = Normal(a, b, half);
            var (ex, ey) = (0.0, 0.0);
            if (!closed && style.Cap == 2)
            {
                // Square caps extend the first and last segments by half the width.
                (ex, ey) = (ny, -nx);
            }

            var start = i == 0 && !closed ? (a.X - ex, a.Y - ey) : a;
            var end = i == count - 1 && !closed ? (b.X + ex, b.Y + ey) : b;
            Add(result, [(start.Item1 + nx, start.Item2 + ny), (end.Item1 + nx, end.Item2 + ny), (end.Item1 - nx, end.Item2 - ny), (start.Item1 - nx, start.Item2 - ny)]);
            if (closed || i < count - 1)
            {
                Join(result, a, b, points[(i + 2) % points.Count], half, style);
            }
        }

        if (!closed && style.Cap == 1)
        {
            Add(result, Circle(points[0], half));
            Add(result, Circle(points[^1], half));
        }
    }

    private static void Join(List<IReadOnlyList<(double X, double Y)>> result, (double X, double Y) a, (double X, double Y) v, (double X, double Y) c, double half, StrokeStyle style)
    {
        if (style.Join == 1)
        {
            Add(result, Circle(v, half));
            return;
        }

        var n1 = Normal(a, v, half);
        var n2 = Normal(v, c, half);
        var d2 = Direction(v, c);
        var side = (n1.X * d2.X) + (n1.Y * d2.Y) < 0 ? 1 : -1;
        var p1 = (v.X + (side * n1.X), v.Y + (side * n1.Y));
        var p2 = (v.X + (side * n2.X), v.Y + (side * n2.Y));
        var cos = ((n1.X * n2.X) + (n1.Y * n2.Y)) / (half * half);
        var ratio = 1 / Math.Sqrt(Math.Max(1e-12, (1 + cos) / 2));
        if (style.Join == 0 && ratio <= style.MiterLimit)
        {
            var (mx, my) = (n1.X + n2.X, n1.Y + n2.Y);
            var length = Math.Sqrt((mx * mx) + (my * my));
            if (length > 1e-9)
            {
                var reach = half * ratio / length;
                Add(result, [v, p1, (v.X + (side * mx * reach), v.Y + (side * my * reach)), p2]);
                return;
            }
        }

        Add(result, [v, p1, p2]);
    }

    private static (double X, double Y) Direction((double X, double Y) a, (double X, double Y) b)
    {
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        return length < 1e-12 ? (0, 0) : (dx / length, dy / length);
    }

    private static (double X, double Y) Normal((double X, double Y) a, (double X, double Y) b, double half)
    {
        var (dx, dy) = Direction(a, b);
        return (-dy * half, dx * half);
    }

    private static List<(double X, double Y)> Circle((double X, double Y) center, double radius)
    {
        var steps = Math.Clamp((int)Math.Ceiling(radius * 1.5), 8, 64);
        return Enumerable.Range(0, steps).Select(i => (center.X + (radius * Math.Cos(2 * Math.PI * i / steps)), center.Y + (radius * Math.Sin(2 * Math.PI * i / steps)))).ToList();
    }

    // Every polygon gets a positive signed area, so overlapping pieces add up under the non-zero rule.
    private static void Add(List<IReadOnlyList<(double X, double Y)>> result, List<(double X, double Y)> polygon)
    {
        var area = 0.0;
        for (var i = 0; i < polygon.Count; i++)
        {
            var (p, q) = (polygon[i], polygon[(i + 1) % polygon.Count]);
            area += (p.X * q.Y) - (q.X * p.Y);
        }

        if (area < 0)
        {
            polygon.Reverse();
        }

        if (Math.Abs(area) > 1e-9)
        {
            result.Add(polygon);
        }
    }

    private static List<(double X, double Y)> Distinct(List<(double X, double Y)> points)
    {
        var result = new List<(double X, double Y)>(points.Count);
        foreach (var point in points)
        {
            if (result.Count == 0 || Math.Abs(result[^1].X - point.X) > 1e-9 || Math.Abs(result[^1].Y - point.Y) > 1e-9)
            {
                result.Add(point);
            }
        }

        return result;
    }

    // Cuts each path into the "on" pieces of the dash pattern, which runs on across segments.
    private static List<Polyline> Dashes(IEnumerable<Polyline> paths, double[] dash, double phase)
    {
        var result = new List<Polyline>();
        var total = dash.Sum();
        foreach (var path in paths)
        {
            var points = path.Closed && path.Points.Count > 1 ? [.. path.Points, path.Points[0]] : path.Points;
            var (index, remaining) = DashStart(dash, phase % total);
            var on = index % 2 == 0;
            var current = on ? new List<(double X, double Y)> { points[0] } : null;
            for (var i = 1; i < points.Count; i++)
            {
                (index, remaining, on, current) = Walk(result, points[i - 1], points[i], dash, index, remaining, on, current);
            }

            if (current is { Count: > 1 })
            {
                result.Add(new Polyline(current, false));
            }
        }

        return result;
    }

    private static (int Index, double Remaining) DashStart(double[] dash, double phase)
    {
        var index = 0;
        while (phase >= dash[index % dash.Length] && dash[index % dash.Length] > 0)
        {
            phase -= dash[index % dash.Length];
            index++;
        }

        return (index % dash.Length, dash[index % dash.Length] - phase);
    }

    private static (int, double, bool, List<(double X, double Y)>?) Walk(List<Polyline> result, (double X, double Y) a, (double X, double Y) b, double[] dash, int index, double remaining, bool on, List<(double X, double Y)>? current)
    {
        var length = Math.Sqrt(((b.X - a.X) * (b.X - a.X)) + ((b.Y - a.Y) * (b.Y - a.Y)));
        var travelled = 0.0;
        while (length - travelled > remaining)
        {
            travelled += remaining;
            var t = travelled / length;
            var point = (a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t));
            if (on)
            {
                current!.Add(point);
                result.Add(new Polyline(current, false));
                current = null;
            }
            else
            {
                current = [point];
            }

            on = !on;
            index = (index + 1) % dash.Length;
            remaining = Math.Max(dash[index], 1e-6);
        }

        remaining -= length - travelled;
        current?.Add(b);
        return (index, remaining, on, current);
    }
}
