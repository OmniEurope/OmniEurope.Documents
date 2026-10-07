// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>A polyline or polygon in device pixels (y down).</summary>
internal sealed class Polyline(List<(double X, double Y)> points, bool closed)
{
    public List<(double X, double Y)> Points { get; } = points;

    public bool Closed { get; set; } = closed;
}

/// <summary>
/// Scan conversion with anti-aliasing: each pixel row is sampled on five sub-scanlines and every span is
/// accumulated with its exact horizontal coverage, for the non-zero winding or the even-odd rule. Rows are
/// handed to a sink as coverage values from 0 to 1.
/// </summary>
internal static class Rasterizer
{
    private const int Samples = 5;
    private const float Weight = 1f / Samples;

    public delegate void RowSink(int y, int left, ReadOnlySpan<float> coverage);

    public static void Fill(IEnumerable<IReadOnlyList<(double X, double Y)>> polygons, bool evenOdd, int width, int height, RowSink sink)
    {
        var edges = Edges(polygons, out var bounds);
        var top = Math.Max(0, (int)Math.Floor(bounds.MinY));
        var bottom = Math.Min(height, (int)Math.Ceiling(bounds.MaxY));
        var left = Math.Max(0, (int)Math.Floor(bounds.MinX));
        var right = Math.Min(width, (int)Math.Ceiling(bounds.MaxX) + 1);
        if (edges.Count == 0 || top >= bottom || left >= right)
        {
            return;
        }

        edges.Sort((a, b) => a.Y0.CompareTo(b.Y0));
        var scan = new Scan(left, right, evenOdd);
        var next = 0;
        var active = new List<Edge>();
        for (var y = top; y < bottom; y++)
        {
            scan.Clear();
            for (var s = 0; s < Samples; s++)
            {
                var sy = y + ((s + 0.5) / Samples);
                while (next < edges.Count && edges[next].Y0 <= sy)
                {
                    active.Add(edges[next++]);
                }

                active.RemoveAll(e => e.Y1 <= sy);
                scan.Sample(active, sy);
            }

            sink(y, left, scan.Finish());
        }
    }

    private static List<Edge> Edges(IEnumerable<IReadOnlyList<(double X, double Y)>> polygons, out (double MinX, double MinY, double MaxX, double MaxY) bounds)
    {
        var edges = new List<Edge>();
        bounds = (double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
        foreach (var polygon in polygons)
        {
            for (var i = 0; i < polygon.Count; i++)
            {
                var (p, q) = (polygon[i], polygon[(i + 1) % polygon.Count]);
                if (!double.IsFinite(p.X) || !double.IsFinite(p.Y) || !double.IsFinite(q.X) || !double.IsFinite(q.Y))
                {
                    continue;
                }

                bounds = (Math.Min(bounds.MinX, p.X), Math.Min(bounds.MinY, p.Y), Math.Max(bounds.MaxX, p.X), Math.Max(bounds.MaxY, p.Y));
                if (p.Y != q.Y)
                {
                    edges.Add(p.Y < q.Y ? new Edge(p.X, p.Y, q.Y, (q.X - p.X) / (q.Y - p.Y), 1) : new Edge(q.X, q.Y, p.Y, (p.X - q.X) / (p.Y - q.Y), -1));
                }
            }
        }

        return edges;
    }

    private readonly record struct Edge(double X0, double Y0, double Y1, double Slope, int Direction);

    /// <summary>Coverage of one pixel row: partial ends added directly, full interiors through a running sum.</summary>
    private sealed class Scan(int left, int right, bool evenOdd)
    {
        private readonly float[] _coverage = new float[right - left + 1];
        private readonly float[] _run = new float[right - left + 2];
        private readonly List<(double X, int Direction)> _crossings = [];

        public void Clear()
        {
            Array.Clear(_coverage);
            Array.Clear(_run);
        }

        public void Sample(List<Edge> active, double y)
        {
            _crossings.Clear();
            foreach (var edge in active)
            {
                if (edge.Y0 <= y && y < edge.Y1)
                {
                    _crossings.Add((edge.X0 + ((y - edge.Y0) * edge.Slope), edge.Direction));
                }
            }

            _crossings.Sort((a, b) => a.X.CompareTo(b.X));
            var winding = 0;
            for (var i = 0; i < _crossings.Count - 1; i++)
            {
                winding += evenOdd ? 1 : _crossings[i].Direction;
                var inside = evenOdd ? (winding & 1) == 1 : winding != 0;
                if (inside)
                {
                    Span(_crossings[i].X, _crossings[i + 1].X);
                }
            }
        }

        private void Span(double start, double end)
        {
            start = Math.Max(start, left);
            end = Math.Min(end, right);
            if (end <= start)
            {
                return;
            }

            var first = (int)Math.Floor(start) - left;
            var last = (int)Math.Floor(end) - left;
            if (first == last)
            {
                _coverage[first] += (float)(end - start) * Weight;
                return;
            }

            _coverage[first] += (float)(first + left + 1 - start) * Weight;
            _run[first + 1] += Weight;
            _run[last] -= Weight;
            _coverage[last] += (float)(end - (last + left)) * Weight;
        }

        public ReadOnlySpan<float> Finish()
        {
            var sum = 0f;
            for (var i = 0; i < _coverage.Length - 1; i++)
            {
                sum += _run[i];
                _coverage[i] = Math.Min(1f, _coverage[i] + sum);
            }

            return _coverage.AsSpan(0, _coverage.Length - 1);
        }
    }
}
