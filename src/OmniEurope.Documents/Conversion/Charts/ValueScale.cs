// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Charts;

namespace OmniEurope.Documents.Conversion.Charts;

/// <summary>
/// The value axis of a bar or line chart: from zero (or the lowest negative value) to the highest value, both rounded
/// out to a tick step of 1, 2 or 5 times a power of ten chosen for about five intervals. Stacked charts scale to the
/// sums of each category, percent-stacked ones to the range of the shares (each value over its category's total), so
/// 0 to 100 % for positive values and a narrower or signed range when values of both signs are stacked.
/// </summary>
internal sealed class ValueScale
{
    private const int Intervals = 5;

    private ValueScale(double min, double max, double step, string format)
    {
        (Min, Max, Step, Format) = (min, max, step, format);
    }

    public double Min { get; }

    public double Max { get; }

    public double Step { get; }

    /// <summary>The number format of the tick labels.</summary>
    public string Format { get; }

    /// <summary>The ticks from <see cref="Min"/> to <see cref="Max"/>.</summary>
    public IEnumerable<double> Ticks()
    {
        var count = (int)Math.Round((Max - Min) / Step);
        for (var i = 0; i <= count; i++)
        {
            yield return Min + (i * Step);
        }
    }

    /// <summary>Where a value falls between the ends of the axis, 0 at <see cref="Min"/> and 1 at <see cref="Max"/>.</summary>
    public double Fraction(double value) => (value - Min) / (Max - Min);

    public static ValueScale For(Chart chart, ChartStack stack)
    {
        var (low, high) = stack.Range();
        low = Math.Min(0, low);
        high = Math.Max(0, high);
        if (high - low < 1e-12)
        {
            high = low + 1;
        }

        var step = Nice((high - low) / Intervals);
        var min = Math.Floor((low / step) + 1e-9) * step;
        var max = Math.Ceiling((high / step) - 1e-9) * step;
        var format = chart.Grouping == ChartGrouping.PercentStacked ? "0%" : chart.Series[0].FormatCode;
        return new ValueScale(min, max, step, format);
    }

    // 1, 2 or 5 times a power of ten, the smallest not below the raw step.
    internal static double Nice(double raw)
    {
        var power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var fraction = raw / power;
        var nice = fraction <= 1 + 1e-9 ? 1 : fraction <= 2 + 1e-9 ? 2 : fraction <= 5 + 1e-9 ? 5 : 10;
        return nice * power;
    }
}

/// <summary>
/// The values of a bar or line chart as drawn: each point from where it starts to where it ends, stacked on the
/// series before it when the chart stacks them (negative values below zero, positive ones above), as shares of their
/// category's total when it stacks to 100 %.
/// </summary>
internal sealed class ChartStack
{
    private readonly (double From, double To)?[,] _spans;

    public ChartStack(Chart chart)
    {
        var count = chart.PointCount;
        _spans = new (double, double)?[chart.Series.Count, count];
        var stacked = chart.Grouping != ChartGrouping.Standard;
        for (var p = 0; p < count; p++)
        {
            var total = chart.Series.Sum(s => Math.Abs(ValueOf(s, p) ?? 0));
            var (above, below) = (0d, 0d);
            for (var s = 0; s < chart.Series.Count; s++)
            {
                if (ValueOf(chart.Series[s], p) is not { } value)
                {
                    continue;
                }

                value = chart.Grouping == ChartGrouping.PercentStacked ? (total == 0 ? 0 : value / total) : value;
                if (!stacked)
                {
                    _spans[s, p] = (0, value);
                }
                else if (value >= 0)
                {
                    _spans[s, p] = (above, above += value);
                }
                else
                {
                    _spans[s, p] = (below, below += value);
                }
            }
        }
    }

    public int Points => _spans.GetLength(1);

    /// <summary>Where point <paramref name="point"/> of series <paramref name="series"/> starts and ends, or null without
    /// a value.</summary>
    public (double From, double To)? Span(int series, int point) => _spans[series, point];

    /// <summary>The lowest and highest ends of every span.</summary>
    public (double Low, double High) Range()
    {
        var (low, high) = (double.PositiveInfinity, double.NegativeInfinity);
        foreach (var span in _spans)
        {
            if (span is { } s)
            {
                low = Math.Min(low, Math.Min(s.From, s.To));
                high = Math.Max(high, Math.Max(s.From, s.To));
            }
        }

        return double.IsInfinity(low) ? (0, 0) : (low, high);
    }

    private static double? ValueOf(ChartSeries series, int point) => point < series.Values.Count ? series.Values[point] : null;
}
