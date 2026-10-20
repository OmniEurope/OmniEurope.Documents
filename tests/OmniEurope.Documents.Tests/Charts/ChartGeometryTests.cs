// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Charts;
using OmniEurope.Documents.Conversion.Charts;
using OmniEurope.Documents.Excel;
using static OmniEurope.Documents.Tests.Charts.ChartFactory;

namespace OmniEurope.Documents.Tests.Charts;

/// <summary>
/// The value axis, the stacking of series, the outline of pie slices and the limit on the points read from cells, on
/// charts built in memory. The expected ticks are worked out by hand: the raw step is the span divided by five, the
/// tick step the smallest 1, 2 or 5 times a power of ten not below it, and the ends of the axis are the multiples of the
/// step around the lowest and highest value (zero always inside).
/// </summary>
public sealed class ChartGeometryTests
{
    private static Chart Make(ChartGrouping grouping, params double?[][] series) => new()
    {
        Kind = ChartKind.Bar,
        Grouping = grouping,
        Series = [.. series.Select(values => new ChartSeries { Values = values })],
    };

    private static ValueScale Scale(ChartGrouping grouping, params double?[][] series)
    {
        var chart = Make(grouping, series);
        return ValueScale.For(chart, new ChartStack(chart));
    }

    private static void AssertTicks(double[] expected, ValueScale scale)
    {
        var ticks = scale.Ticks().ToList();
        Assert.Equal(expected.Length, ticks.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], ticks[i], 9);
        }
    }

    [Fact]
    public void A_scale_of_positive_values_starts_at_zero_and_ends_on_the_step_above_the_highest()
    {
        // 1234 / 5 = 246.8 -> a step of 500; the highest value rounds up to 1500.
        var scale = Scale(ChartGrouping.Standard, [100, 1234]);

        Assert.Equal(500, scale.Step);
        AssertTicks([0, 500, 1000, 1500], scale);
        Assert.Equal("General", scale.Format);
    }

    [Fact]
    public void A_highest_value_on_a_tick_does_not_add_a_step()
    {
        // 50 / 5 = 10 exactly, a step of 10: the axis ends at 50, not at 60.
        AssertTicks([0, 10, 20, 30, 40, 50], Scale(ChartGrouping.Standard, [50]));
    }

    [Fact]
    public void Negative_values_extend_the_axis_below_zero()
    {
        // From -30 to 20: span 50, raw step 10, step 10.
        AssertTicks([-30, -20, -10, 0, 10, 20], Scale(ChartGrouping.Standard, [-30, 20]));
        // From -7 to 3: span 10, raw step 2, step 2: the ends round out to -8 and 4.
        AssertTicks([-8, -6, -4, -2, 0, 2, 4], Scale(ChartGrouping.Standard, [-7, 3]));
    }

    [Fact]
    public void Only_negative_values_end_the_axis_at_zero()
    {
        // From -12 to 0: span 12, raw step 2.4, step 5: -15 to 0.
        AssertTicks([-15, -10, -5, 0], Scale(ChartGrouping.Standard, [-12, -3]));
    }

    [Fact]
    public void A_series_of_zeros_or_without_values_gets_the_axis_0_to_1()
    {
        // No span: the axis is made one unit high, raw step 0.2, step 0.2.
        AssertTicks([0, 0.2, 0.4, 0.6, 0.8, 1], Scale(ChartGrouping.Standard, [0, 0]));
        AssertTicks([0, 0.2, 0.4, 0.6, 0.8, 1], Scale(ChartGrouping.Standard, new double?[] { null, null }));
    }

    [Fact]
    public void A_stacked_scale_covers_the_sums_and_a_percent_stacked_one_runs_from_0_to_100_percent()
    {
        // Sums 15 and 45: raw step 9, step 10, up to 50.
        AssertTicks([0, 10, 20, 30, 40, 50], Scale(ChartGrouping.Stacked, [10, 20], [5, 25]));

        var percent = Scale(ChartGrouping.PercentStacked, [1, 3], [3, 1]);
        AssertTicks([0, 0.2, 0.4, 0.6, 0.8, 1], percent);
        Assert.Equal("0%", percent.Format);
    }

    [Fact]
    public void A_percent_stacked_scale_of_negative_values_runs_from_minus_100_percent_to_zero()
    {
        // Shares -0.25, -0.75 and -0.75, -0.25 stack down to -1.
        AssertTicks([-1, -0.8, -0.6, -0.4, -0.2, 0], Scale(ChartGrouping.PercentStacked, [-1, -3], [-3, -1]));
    }

    [Fact]
    public void The_scale_maps_a_value_to_its_place_between_the_ends_of_the_axis()
    {
        var scale = Scale(ChartGrouping.Standard, [-30, 20]);

        Assert.Equal(0, scale.Fraction(-30), 9);
        Assert.Equal(0.6, scale.Fraction(0), 9);
        Assert.Equal(1, scale.Fraction(20), 9);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1.0000000001, 1)]
    [InlineData(1.5, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 5)]
    [InlineData(5, 5)]
    [InlineData(5.1, 10)]
    [InlineData(7, 10)]
    [InlineData(250, 500)]
    [InlineData(0.03, 0.05)]
    [InlineData(0.0004, 0.0005)]
    public void The_tick_step_is_the_smallest_1_2_or_5_times_a_power_of_ten_not_below_the_raw_step(double raw, double expected)
    {
        Assert.Equal(expected, ValueScale.Nice(raw), 12);
    }

    [Fact]
    public void Standard_series_each_start_from_zero_and_a_missing_value_has_no_span()
    {
        var stack = new ChartStack(Make(ChartGrouping.Standard, [10, null, -4], [5]));

        Assert.Equal(3, stack.Points);
        Assert.Equal((0, 10), stack.Span(0, 0));
        Assert.Null(stack.Span(0, 1));
        Assert.Equal((0, -4), stack.Span(0, 2));
        Assert.Equal((0, 5), stack.Span(1, 0));
        Assert.Null(stack.Span(1, 1));
        Assert.Equal((-4, 10), stack.Range());
    }

    [Fact]
    public void Stacked_series_pile_positive_values_up_and_negative_values_down()
    {
        // Point 0: 10 then 5 on top of it; -4 from zero down, then -2 below it. Point 1: the second series has no value, the
        // third (+6) rests on the first (20); the fourth starts from zero down.
        var stack = new ChartStack(Make(ChartGrouping.Stacked, [10, 20], [5, null], [-4, 6], [-2, -1]));

        Assert.Equal((0, 10), stack.Span(0, 0));
        Assert.Equal((10, 15), stack.Span(1, 0));
        Assert.Equal((0, -4), stack.Span(2, 0));
        Assert.Equal((-4, -6), stack.Span(3, 0));
        Assert.Equal((0, 20), stack.Span(0, 1));
        Assert.Null(stack.Span(1, 1));
        Assert.Equal((20, 26), stack.Span(2, 1));
        Assert.Equal((0, -1), stack.Span(3, 1));
        Assert.Equal((-6, 26), stack.Range());
    }

    [Fact]
    public void Percent_stacked_series_are_shares_of_the_category_total_and_an_empty_total_gives_empty_spans()
    {
        // Point 0: 1 and 3 of 4 -> 0.25 and 0.75. Point 1: nothing to share. Point 2: -1 and 3 of 4 (absolute values).
        var stack = new ChartStack(Make(ChartGrouping.PercentStacked, [1, 0, -1], [3, 0, 3]));

        Assert.Equal((0, 0.25), stack.Span(0, 0));
        Assert.Equal((0.25, 1), stack.Span(1, 0));
        Assert.Equal((0, 0), stack.Span(0, 1));
        Assert.Equal((0, 0), stack.Span(1, 1));
        Assert.Equal((0, -0.25), stack.Span(0, 2));
        Assert.Equal((0, 0.75), stack.Span(1, 2));
    }

    [Fact]
    public void A_series_shorter_than_the_longest_has_no_value_for_the_missing_points()
    {
        var stack = new ChartStack(Make(ChartGrouping.Standard, [1, 2, 3], [4]));

        Assert.Equal(3, stack.Points);
        Assert.Null(stack.Span(1, 2));
    }

    [Fact]
    public void A_chart_without_values_has_the_empty_range_0_to_0()
    {
        Assert.Equal((0d, 0d), new ChartStack(Make(ChartGrouping.Standard, new double?[] { null })).Range());
    }

    [Fact]
    public void Angles_run_clockwise_from_the_top_of_the_page()
    {
        // Page y grows downwards: 0 degrees is straight up (smaller y), 90 to the right, 180 down, 270 to the left.
        var (up, right, down, left) = (PiePlot.Point((5, 5), 10, 0), PiePlot.Point((5, 5), 10, 90), PiePlot.Point((5, 5), 10, 180), PiePlot.Point((5, 5), 10, 270));

        Assert.Equal((5d, -5d), (Math.Round(up.X, 9), Math.Round(up.Y, 9)));
        Assert.Equal((15d, 5d), (Math.Round(right.X, 9), Math.Round(right.Y, 9)));
        Assert.Equal((5d, 15d), (Math.Round(down.X, 9), Math.Round(down.Y, 9)));
        Assert.Equal((-5d, 5d), (Math.Round(left.X, 9), Math.Round(left.Y, 9)));
    }

    [Fact]
    public void A_pie_slice_runs_along_its_arc_from_its_start_angle_and_closes_at_the_centre()
    {
        // 90 degrees from the right (start 90): an outline vertex every 2 degrees at most -> 45 steps, 46 vertices, then the centre.
        var slice = PiePlot.Slice((100, 100), 50, 0, 90, 90);

        Assert.Equal(47, slice.Count);
        Assert.Equal((150d, 100d), (Math.Round(slice[0].X, 9), Math.Round(slice[0].Y, 9)));
        Assert.Equal((100d, 150d), (Math.Round(slice[45].X, 9), Math.Round(slice[45].Y, 9)));
        Assert.Equal((100d, 100d), slice[46]);
    }

    [Fact]
    public void A_doughnut_slice_returns_along_the_inner_arc()
    {
        // 46 vertices out along the outer arc (right to bottom), then 46 back along the inner arc (bottom to right).
        var slice = PiePlot.Slice((100, 100), 50, 20, 90, 90);

        Assert.Equal(92, slice.Count);
        Assert.Equal((100d, 150d), (Math.Round(slice[45].X, 9), Math.Round(slice[45].Y, 9)));
        Assert.Equal((100d, 120d), (Math.Round(slice[46].X, 9), Math.Round(slice[46].Y, 9)));
        Assert.Equal((120d, 100d), (Math.Round(slice[91].X, 9), Math.Round(slice[91].Y, 9)));
    }

    [Fact]
    public void A_very_thin_slice_still_has_both_ends_of_its_arc()
    {
        Assert.Equal(3, PiePlot.Slice((0, 0), 10, 0, 0, 0.5).Count);
    }

    [Theory]
    [InlineData(126.5, "7E7E7E")]
    [InlineData(127.5, "808080")]
    [InlineData(128.5, "808080")]
    [InlineData(129.5, "828282")]
    public void A_channel_halfway_between_two_values_rounds_to_the_even_one_whatever_the_floating_point_noise(double channel, string expected)
    {
        // Black lightened by channel / 255 has all three channels at exactly `channel`.
        Assert.Equal(expected, ChartPalette.Luminance("000000", 1, channel / 255));
    }

    // ---- The limit on the points read from cells -----------------------------------------------------------------

    private static Chart ReadFromCells(int points)
    {
        // The used range reaches the last row, so the formulas' areas are not cut short; the chart has no cache.
        var data = """<row r="1"><c r="B1" t="inlineStr"><is><t>Ventes</t></is></c></row><row r="2"><c r="A2" t="inlineStr"><is><t>début</t></is></c><c r="B2"><v>3</v></c></row>"""
            + $"""<row r="{points + 1}"><c r="A{points + 1}" t="inlineStr"><is><t>fin</t></is></c><c r="B{points + 1}"><v>7</v></c></row>""";
        var part = ChartSpace(Bar(Series(0, "Ventes", new string[points], new double?[points], cached: false)), Axes());

        return XlsxWorkbook.Load(Xlsx(data, part, TwoCell(3, 1, 9, 16))).Worksheets[0].Charts.Single().Chart;
    }

    [Fact]
    public void A_formula_naming_more_than_100000_cells_is_not_read()
    {
        // 100001 rows of the series: past the limit, the chart keeps its name and no point.
        var chart = ReadFromCells(100_001);

        Assert.Equal("Ventes", chart.Series[0].Name);
        Assert.Empty(chart.Series[0].Values);
        Assert.Empty(chart.Series[0].Categories);
    }

    [Fact]
    public void A_formula_naming_exactly_100000_cells_is_read()
    {
        var chart = ReadFromCells(100_000);

        Assert.Equal(100_000, chart.Series[0].Values.Count);
        Assert.Equal(3, chart.Series[0].Values[0]);
        Assert.Null(chart.Series[0].Values[50_000]);
        Assert.Equal(7, chart.Series[0].Values[99_999]);
        Assert.Equal("fin", chart.Series[0].Categories[99_999]);
    }
}
