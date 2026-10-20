// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Charts;
using OmniEurope.Documents.Pdf;

namespace OmniEurope.Documents.Conversion.Charts;

/// <summary>
/// Bars and columns: each category's band holds a group narrower by the gap width (<c>c:gapWidth</c>, in percent of one
/// bar); clustered series share the group side by side (the first series nearest the value axis' origin, so at the
/// bottom of a horizontal group), stacked ones fill it on top of each other. Value labels stand past the end of a
/// clustered bar and in the middle of a stacked one.
/// </summary>
internal static class BarPlot
{
    public static void Draw(ChartText text, Chart chart, CartesianPlot plot)
    {
        var stacked = chart.Grouping != ChartGrouping.Standard;
        var bars = stacked ? 1 : chart.Series.Count;
        for (var p = 0; p < plot.Count; p++)
        {
            var (start, length) = plot.Band(p);
            var group = length / (1 + (chart.GapWidth / 100d));
            var bar = group / bars;
            var first = start + ((length - group) / 2);
            for (var s = 0; s < chart.Series.Count; s++)
            {
                if (plot.Stack.Span(s, p) is not { } span)
                {
                    continue;
                }

                var slot = stacked ? 0 : plot.Horizontal ? bars - 1 - s : s;
                var box = plot.Rectangle(first + (slot * bar), bar, span.From, span.To);
                text.Canvas.FillRectangle(box.X, box.Y, box.Width, box.Height, ChartText.Color(chart.Series[s].ColorOf(p)));
                if (chart.ShowValues)
                {
                    Label(text, chart.Series[s], p, box, stacked, plot.Horizontal, span.To < span.From);
                }
            }
        }
    }

    private static void Label(ChartText text, ChartSeries series, int point, ChartBox box, bool inside, bool horizontal, bool negative)
    {
        var label = text.Format(series.Values[point]!.Value, series.FormatCode);
        var (width, height) = (text.Width(label, ChartText.LabelSize), text.LineHeight(ChartText.LabelSize));
        double x, y;
        if (inside)
        {
            (x, y) = (box.X + ((box.Width - width) / 2), box.Y + ((box.Height - height) / 2));
        }
        else if (horizontal)
        {
            (x, y) = (negative ? box.X - width - 2 : box.Right + 2, box.Y + ((box.Height - height) / 2));
        }
        else
        {
            (x, y) = (box.X + ((box.Width - width) / 2), negative ? box.Bottom + 1 : box.Y - height - 1);
        }

        text.Draw(label, x, y, ChartText.LabelSize, new PdfColor(0x40, 0x40, 0x40));
    }
}

/// <summary>
/// Lines through the middle of each category's band, broken where a point has no value, with round markers when the
/// series shows them; stacked lines add up like stacked bars. Value labels stand above the points.
/// </summary>
internal static class LinePlot
{
    private const double Width = 2.25;
    private const double MarkerRadius = 2.5;

    public static void Draw(ChartText text, Chart chart, CartesianPlot plot)
    {
        for (var s = 0; s < chart.Series.Count; s++)
        {
            var series = chart.Series[s];
            var color = ChartText.Color(series.Color);
            var points = Points(plot, s);
            foreach (var run in Runs(points))
            {
                text.Canvas.StrokePolyline(run, color, Width);
            }

            for (var p = 0; p < points.Count; p++)
            {
                if (points[p] is not { } point)
                {
                    continue;
                }

                if (series.Markers)
                {
                    text.Canvas.FillPolygon(Circle(point.X, point.Y, MarkerRadius), ChartText.Color(series.ColorOf(p)));
                }

                if (chart.ShowValues)
                {
                    var label = text.Format(series.Values[p]!.Value, series.FormatCode);
                    text.Centered(label, point.X, point.Y - MarkerRadius - 1 - text.LineHeight(ChartText.LabelSize), ChartText.LabelSize, new PdfColor(0x40, 0x40, 0x40));
                }
            }
        }
    }

    private static List<(double X, double Y)?> Points(CartesianPlot plot, int series)
    {
        var points = new List<(double X, double Y)?>();
        for (var p = 0; p < plot.Count; p++)
        {
            var (start, length) = plot.Band(p);
            points.Add(plot.Stack.Span(series, p) is { } span ? (start + (length / 2), plot.At(span.To)) : null);
        }

        return points;
    }

    // The stretches of consecutive points with a value.
    private static IEnumerable<List<(double X, double Y)>> Runs(List<(double X, double Y)?> points)
    {
        var run = new List<(double X, double Y)>();
        foreach (var point in points)
        {
            if (point is { } p)
            {
                run.Add(p);
                continue;
            }

            if (run.Count > 0)
            {
                yield return run;
            }

            run = [];
        }

        if (run.Count > 0)
        {
            yield return run;
        }
    }

    internal static List<(double X, double Y)> Circle(double x, double y, double radius) =>
        Enumerable.Range(0, 16).Select(i => (x + (radius * Math.Cos(i * Math.PI / 8)), y + (radius * Math.Sin(i * Math.PI / 8)))).ToList();
}

/// <summary>
/// Pies and doughnuts: the slices of a pie are the points of its first series, from the first slice angle clockwise
/// from the top, in proportion to their absolute values, outlined in white; a doughnut draws one ring per series inside
/// each other around its hole. Labels show the value, the percentage, or both, in the middle of each slice.
/// </summary>
internal static class PiePlot
{
    // Arcs are drawn as polygons with a vertex at least every this many degrees.
    private const double StepDegrees = 2;

    public static void Draw(ChartText text, Chart chart, ChartBox box)
    {
        var radius = Math.Min(box.Width, box.Height) / 2 * 0.95;
        var (cx, cy) = (box.X + (box.Width / 2), box.Y + (box.Height / 2));
        var rings = chart.Kind == ChartKind.Doughnut ? chart.Series.Count : Math.Min(1, chart.Series.Count);
        var hole = chart.Kind == ChartKind.Doughnut ? radius * Math.Clamp(chart.HoleSize, 0, 90) / 100 : 0;
        var ring = rings == 0 ? 0 : (radius - hole) / rings;
        for (var s = 0; s < rings; s++)
        {
            var outer = radius - (s * ring);
            Ring(text, chart, chart.Series[s], (cx, cy), outer, outer - ring);
        }
    }

    private static void Ring(ChartText text, Chart chart, ChartSeries series, (double X, double Y) center, double outer, double inner)
    {
        var total = series.Values.Sum(v => Math.Abs(v ?? 0));
        if (total <= 0)
        {
            return;
        }

        var angle = (double)chart.FirstSliceAngle;
        for (var p = 0; p < series.Values.Count; p++)
        {
            var value = Math.Abs(series.Values[p] ?? 0);
            if (value <= 0)
            {
                continue;
            }

            var sweep = value / total * 360;
            var slice = Slice(center, outer, inner, angle, sweep);
            text.Canvas.DrawPath([slice], ChartText.Color(series.ColorOf(p)), PdfColor.White, 0.75);
            if (chart.ShowValues || chart.ShowPercentages)
            {
                var middle = Point(center, inner + ((outer - inner) * (inner > 0 ? 0.5 : 0.65)), angle + (sweep / 2));
                var label = Label(text, chart, series, p, value / total);
                text.Centered(label, middle.X, middle.Y - (text.LineHeight(ChartText.LabelSize) / 2), ChartText.LabelSize, new PdfColor(0x40, 0x40, 0x40));
            }

            angle += sweep;
        }
    }

    private static string Label(ChartText text, Chart chart, ChartSeries series, int point, double share)
    {
        var parts = new List<string>();
        if (chart.ShowValues)
        {
            parts.Add(text.Format(series.Values[point]!.Value, series.FormatCode));
        }

        if (chart.ShowPercentages)
        {
            parts.Add(text.Format(share, "0%"));
        }

        return string.Join("; ", parts);
    }

    /// <summary>The outline of a slice: the outer arc clockwise, then the inner arc back (or the centre for a pie).</summary>
    internal static List<(double X, double Y)> Slice((double X, double Y) center, double outer, double inner, double start, double sweep)
    {
        var steps = Math.Max(1, (int)Math.Ceiling(sweep / StepDegrees));
        var outline = new List<(double X, double Y)>();
        for (var i = 0; i <= steps; i++)
        {
            outline.Add(Point(center, outer, start + (sweep * i / steps)));
        }

        if (inner <= 0)
        {
            outline.Add(center);
            return outline;
        }

        for (var i = steps; i >= 0; i--)
        {
            outline.Add(Point(center, inner, start + (sweep * i / steps)));
        }

        return outline;
    }

    // Angles run clockwise from the top; page y grows downwards.
    internal static (double X, double Y) Point((double X, double Y) center, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return (center.X + (radius * Math.Sin(radians)), center.Y - (radius * Math.Cos(radians)));
    }
}
