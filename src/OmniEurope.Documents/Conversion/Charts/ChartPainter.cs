// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Charts;
using OmniEurope.Documents.Conversion.WordLayout;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Conversion.Charts;

/// <summary>
/// Draws a chart as vector graphics in its frame: the chart area's fill, the title centred at the top, the legend on
/// its side (series, or categories for a pie, a doughnut or a bar chart coloured by point), then the plot (bars, lines
/// or slices) in the room left, and the chart area's outline. Everything is clipped to the frame. A chart of a type this
/// package does not draw keeps its frame and title (its series are not read, so it has no legend), and each such type is
/// reported as a gap; a combination chart draws its first supported type and reports the others.
/// </summary>
internal sealed class ChartPainter(LayoutContext layout, Chart chart, CultureInfo culture)
{
    private const double Padding = 7;
    private const double Swatch = 6;
    private const double Spacing = 3;

    public void Paint(PdfCanvas canvas, double x, double y, double width, double height)
    {
        foreach (var type in chart.UnsupportedTypes)
        {
            layout.Gaps.Add("chart type not drawn: " + type);
        }

        if (chart.Truncated)
        {
            layout.Gaps.Add("chart past the point limit: part of its data is not drawn");
        }

        var text = new ChartText(layout, canvas, culture);
        canvas.SaveState();
        canvas.ClipRectangle(x, y, width, height);
        if (chart.Background is { } fill)
        {
            canvas.FillRectangle(x, y, width, height, ChartText.Color(fill));
        }

        var box = Title(text, new ChartBox(x, y, width, height).Inset(Padding, Padding, Padding, Padding));
        box = Legend(text, box);
        Plot(text, box);
        if (chart.Border is { } border)
        {
            canvas.StrokeRectangle(x + 0.375, y + 0.375, width - 0.75, height - 0.75, ChartText.Color(border), 0.75);
        }

        canvas.RestoreState();
    }

    private void Plot(ChartText text, ChartBox box)
    {
        if (chart.Series.Count == 0 || box.Width < 1 || box.Height < 1)
        {
            return;
        }

        switch (chart.Kind)
        {
            case ChartKind.Bar or ChartKind.Line:
                var plot = new CartesianPlot(text, chart, box);
                plot.DrawAxes();
                if (chart.Kind == ChartKind.Bar)
                {
                    BarPlot.Draw(text, chart, plot);
                }
                else
                {
                    LinePlot.Draw(text, chart, plot);
                }

                break;
            case ChartKind.Pie or ChartKind.Doughnut:
                PiePlot.Draw(text, chart, box);
                break;
        }
    }

    private ChartBox Title(ChartText text, ChartBox box)
    {
        if (chart.Title is not { } title)
        {
            return box;
        }

        var line = text.LineHeight(ChartText.TitleSize);
        var lines = title.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            text.Centered(lines[i], box.X + (box.Width / 2), box.Y + (i * line), ChartText.TitleSize);
        }

        return box.Inset(0, (lines.Length * line) + Padding, 0, 0);
    }

    // The entries of the legend: one per series, or one per point when the chart colours its points.
    private List<(string Label, string Color, bool Line)> Entries()
    {
        if (chart.ByPoint)
        {
            var series = chart.Series.FirstOrDefault();
            return series is null ? [] : chart.Categories.Select((c, i) => (c.Length > 0 ? c : (i + 1).ToString(culture), series.ColorOf(i), false)).ToList();
        }

        return chart.Series.Select((s, i) => (s.Name ?? "Series" + (i + 1).ToString(culture), s.Color, chart.Kind == ChartKind.Line)).ToList();
    }

    private ChartBox Legend(ChartText text, ChartBox box)
    {
        if (chart.Legend is not { } position || Entries() is not { Count: > 0 } entries)
        {
            return box;
        }

        var line = text.LineHeight(ChartText.LabelSize) * 1.2;
        var widths = entries.Select(e => SwatchWidth(e.Line) + Spacing + text.Width(e.Label, ChartText.LabelSize)).ToList();
        if (position is ChartLegendPosition.Top or ChartLegendPosition.Bottom)
        {
            return Row(text, box, entries, widths, line, position == ChartLegendPosition.Top);
        }

        var column = widths.Max();
        var height = entries.Count * line;
        var left = position == ChartLegendPosition.Left ? box.X : box.Right - column;
        var top = position == ChartLegendPosition.TopRight ? box.Y : box.Y + ((box.Height - height) / 2);
        for (var i = 0; i < entries.Count; i++)
        {
            Entry(text, entries[i], left, top + (i * line), line);
        }

        return position == ChartLegendPosition.Left ? box.Inset(column + Padding, 0, 0, 0) : box.Inset(0, 0, column + Padding, 0);
    }

    // A legend above or below the plot: the entries in rows as wide as the box allows, each row centred.
    private static ChartBox Row(ChartText text, ChartBox box, List<(string Label, string Color, bool Line)> entries, List<double> widths, double line, bool top)
    {
        const double Between = 10;
        var rows = new List<List<int>> { new() };
        double used = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            if (rows[^1].Count > 0 && used + Between + widths[i] > box.Width)
            {
                rows.Add([]);
                used = 0;
            }

            used += (rows[^1].Count > 0 ? Between : 0) + widths[i];
            rows[^1].Add(i);
        }

        var height = rows.Count * line;
        var y = top ? box.Y : box.Bottom - height;
        foreach (var row in rows)
        {
            var width = row.Sum(i => widths[i]) + ((row.Count - 1) * Between);
            var x = box.X + ((box.Width - width) / 2);
            foreach (var i in row)
            {
                Entry(text, entries[i], x, y, line);
                x += widths[i] + Between;
            }

            y += line;
        }

        return top ? box.Inset(0, height + Padding, 0, 0) : box.Inset(0, 0, 0, height + Padding);
    }

    private static double SwatchWidth(bool line) => line ? Swatch * 2 : Swatch;

    private static void Entry(ChartText text, (string Label, string Color, bool Line) entry, double x, double y, double line)
    {
        var color = ChartText.Color(entry.Color);
        var middle = y + (line / 2);
        if (entry.Line)
        {
            text.Canvas.DrawLine(x, middle, x + (Swatch * 2), middle, color, 2.25);
        }
        else
        {
            text.Canvas.FillRectangle(x, middle - (Swatch / 2), Swatch, Swatch, color);
        }

        text.Draw(entry.Label, x + SwatchWidth(entry.Line) + Spacing, middle - (text.LineHeight(ChartText.LabelSize) / 2), ChartText.LabelSize);
    }
}
