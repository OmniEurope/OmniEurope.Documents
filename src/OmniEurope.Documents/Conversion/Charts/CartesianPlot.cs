// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Charts;
using OmniEurope.Documents.Pdf;

namespace OmniEurope.Documents.Conversion.Charts;

/// <summary>
/// The plot area of a bar or line chart and its two axes. Columns and lines put the categories along the bottom and
/// the values up the left side; horizontal bars put the categories up the left side (the first at the bottom, or at the
/// top when the axis runs from its far end) and the values along the bottom. Tick labels, category labels and axis
/// titles take their room from the box first; gridlines and the category axis line are drawn under the data.
/// </summary>
internal sealed class CartesianPlot
{
    private const double Gap = 4;
    private static readonly PdfColor GridColor = new(0xD9, 0xD9, 0xD9);

    private readonly ChartText _text;
    private readonly Chart _chart;
    private readonly List<string> _ticks;
    private readonly IReadOnlyList<string> _categories;
    private readonly ChartBox _area;

    public CartesianPlot(ChartText text, Chart chart, ChartBox box)
    {
        (_text, _chart, _area) = (text, chart, box);
        Stack = new ChartStack(chart);
        Scale = ValueScale.For(chart, Stack);
        Count = Math.Max(1, Stack.Points);
        _ticks = Scale.Ticks().Select(t => text.Format(t, Scale.Format)).ToList();
        var names = chart.Categories;
        _categories = Enumerable.Range(0, Count).Select(i => i < names.Count && names[i].Length > 0 ? names[i] : (i + 1).ToString(text.Culture)).ToList();
        Plot = Layout(box);
    }

    public ChartStack Stack { get; }

    public ValueScale Scale { get; }

    /// <summary>The number of categories.</summary>
    public int Count { get; }

    /// <summary>The rectangle the data is drawn in.</summary>
    public ChartBox Plot { get; }

    public bool Horizontal => _chart.Kind == ChartKind.Bar && _chart.Horizontal;

    /// <summary>The page coordinate of a value along the value axis (y for columns and lines, x for horizontal bars).</summary>
    public double At(double value)
    {
        var fraction = Scale.Fraction(value);
        return Horizontal ? Plot.X + (fraction * Plot.Width) : Plot.Bottom - (fraction * Plot.Height);
    }

    /// <summary>The stretch of the category axis category <paramref name="index"/> owns: its start (left or top edge)
    /// and length.</summary>
    public (double Start, double Length) Band(int index)
    {
        var slot = _chart.ReverseCategories ^ Horizontal ? Count - 1 - index : index;
        var length = (Horizontal ? Plot.Height : Plot.Width) / Count;
        return ((Horizontal ? Plot.Y : Plot.X) + (slot * length), length);
    }

    /// <summary>The page rectangle between two values across a stretch of the category axis.</summary>
    public ChartBox Rectangle(double start, double length, double from, double to)
    {
        var (a, b) = (At(from), At(to));
        return Horizontal
            ? new ChartBox(Math.Min(a, b), start, Math.Abs(a - b), length)
            : new ChartBox(start, Math.Min(a, b), length, Math.Abs(a - b));
    }

    /// <summary>Gridlines, tick and category labels, the category axis line and the axis titles.</summary>
    public void DrawAxes()
    {
        var canvas = _text.Canvas;
        var ticks = Scale.Ticks().ToList();
        var line = _text.LineHeight(ChartText.LabelSize);
        for (var i = 0; i < ticks.Count; i++)
        {
            var at = At(ticks[i]);
            if (_chart.ValueGridlines)
            {
                if (Horizontal)
                {
                    canvas.DrawLine(at, Plot.Y, at, Plot.Bottom, GridColor, 0.75);
                }
                else
                {
                    canvas.DrawLine(Plot.X, at, Plot.Right, at, GridColor, 0.75);
                }
            }

            if (Horizontal)
            {
                _text.Centered(_ticks[i], at, Plot.Bottom + Gap, ChartText.LabelSize);
            }
            else
            {
                _text.Draw(_ticks[i], Plot.X - Gap - _text.Width(_ticks[i], ChartText.LabelSize), at - (line / 2), ChartText.LabelSize);
            }
        }

        var zero = At(Math.Clamp(0, Scale.Min, Scale.Max));
        if (Horizontal)
        {
            canvas.DrawLine(zero, Plot.Y, zero, Plot.Bottom, GridColor, 0.75);
        }
        else
        {
            canvas.DrawLine(Plot.X, zero, Plot.Right, zero, GridColor, 0.75);
        }

        DrawCategories(line);
        DrawTitles();
    }

    private void DrawCategories(double line)
    {
        for (var i = 0; i < Count; i++)
        {
            var (start, length) = Band(i);
            if (Horizontal)
            {
                _text.Draw(_categories[i], Plot.X - Gap - _text.Width(_categories[i], ChartText.LabelSize), start + ((length - line) / 2), ChartText.LabelSize);
            }
            else
            {
                _text.Centered(_categories[i], start + (length / 2), Plot.Bottom + Gap, ChartText.LabelSize);
            }
        }
    }

    // The title of the axis along the bottom sits under its labels; the one up the left side reads from bottom to top.
    private void DrawTitles()
    {
        var (bottom, left) = Horizontal ? (_chart.ValueAxisTitle, _chart.CategoryAxisTitle) : (_chart.CategoryAxisTitle, _chart.ValueAxisTitle);
        var labels = _text.LineHeight(ChartText.LabelSize) + Gap;
        if (bottom is not null)
        {
            _text.Centered(bottom, Plot.X + (Plot.Width / 2), Plot.Bottom + labels + Gap, ChartText.AxisTitleSize);
        }

        if (left is not null)
        {
            _text.Rotated(left, _area.X + (_text.LineHeight(ChartText.AxisTitleSize) / 2), Plot.Y + (Plot.Height / 2), ChartText.AxisTitleSize);
        }
    }

    private ChartBox Layout(ChartBox box)
    {
        var line = _text.LineHeight(ChartText.LabelSize);
        var title = _text.LineHeight(ChartText.AxisTitleSize) + Gap;
        var leftLabels = Horizontal ? _categories : (IEnumerable<string>)_ticks;
        var left = leftLabels.Max(l => _text.Width(l, ChartText.LabelSize)) + Gap;
        var (bottomTitle, leftTitle) = Horizontal ? (_chart.ValueAxisTitle, _chart.CategoryAxisTitle) : (_chart.CategoryAxisTitle, _chart.ValueAxisTitle);
        left += leftTitle is null ? 0 : title;
        var bottom = line + Gap + (bottomTitle is null ? 0 : title + Gap);

        // Half a label of room at the ends of the value axis, for the labels centred on its first and last ticks.
        var end = line / 2;
        return box.Inset(left, end, Horizontal ? end * 2 : end, bottom);
    }
}
