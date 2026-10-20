// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Charts;

/// <summary>The type of a chart as this package draws it.</summary>
public enum ChartKind
{
    /// <summary>Bars or columns (<c>c:barChart</c>).</summary>
    Bar,

    /// <summary>Lines, with or without markers (<c>c:lineChart</c>).</summary>
    Line,

    /// <summary>A pie (<c>c:pieChart</c>).</summary>
    Pie,

    /// <summary>A doughnut, one ring per series (<c>c:doughnutChart</c>).</summary>
    Doughnut,

    /// <summary>A type this package does not draw (named in <see cref="Chart.UnsupportedTypes"/>): the chart is drawn as
    /// its frame and title only.</summary>
    Unsupported,
}

/// <summary>How the series of a bar or line chart share a category (<c>c:grouping</c>).</summary>
public enum ChartGrouping
{
    /// <summary>Side by side (clustered bars) or each from zero (standard lines).</summary>
    Standard,

    /// <summary>Each series on top of the previous ones.</summary>
    Stacked,

    /// <summary>Stacked, each category scaled to 100 %.</summary>
    PercentStacked,
}

/// <summary>Where the legend stands (<c>c:legendPos</c>).</summary>
public enum ChartLegendPosition
{
    /// <summary>Right of the plot area.</summary>
    Right,

    /// <summary>Left of the plot area.</summary>
    Left,

    /// <summary>Above the plot area, under the title.</summary>
    Top,

    /// <summary>Below the plot area.</summary>
    Bottom,

    /// <summary>In the top-right corner.</summary>
    TopRight,
}

/// <summary>One series of a chart: its name, categories, values and colour.</summary>
public sealed class ChartSeries
{
    internal ChartSeries()
    {
    }

    /// <summary>The series name (<c>c:tx</c>), or null.</summary>
    public string? Name { get; internal set; }

    /// <summary>The category labels, one per point, as the chart shows them.</summary>
    public IReadOnlyList<string> Categories { get; internal set; } = [];

    /// <summary>The values, one per point; null for a point without a value.</summary>
    public IReadOnlyList<double?> Values { get; internal set; } = [];

    /// <summary>The number format of the values (<c>c:formatCode</c>), <c>General</c> by default.</summary>
    public string FormatCode { get; internal set; } = "General";

    /// <summary>The colour of the series as <c>RRGGBB</c>: its explicit fill (or line, for a line chart), else the
    /// theme accent colour of its index.</summary>
    public string Color { get; internal set; } = "4472C4";

    /// <summary>Colours of single points (<c>c:dPt</c>), or of each slice of a pie whose colours vary, by point index.</summary>
    public IReadOnlyDictionary<int, string> PointColors { get; internal set; } = new Dictionary<int, string>();

    /// <summary>Markers are drawn on the points of a line series.</summary>
    public bool Markers { get; internal set; }

    /// <summary>The formula of the series name (<c>Sheet1!$B$1</c>), or null.</summary>
    public string? NameReference { get; internal set; }

    /// <summary>The formula of the categories, or null.</summary>
    public string? CategoriesReference { get; internal set; }

    /// <summary>The formula of the values, or null.</summary>
    public string? ValuesReference { get; internal set; }

    /// <summary>The colour of point <paramref name="index"/>: its own, else the series colour.</summary>
    public string ColorOf(int index) => PointColors.TryGetValue(index, out var color) ? color : Color;
}

/// <summary>
/// A DrawingML chart (ECMA-376 part 1, §21.2) as read from a workbook or a document: its type, series, titles, legend
/// and data labels. The values and categories are those cached in the chart part, or, for a workbook chart without a
/// cache, read from the cells its formulas name.
/// </summary>
public sealed class Chart
{
    internal Chart()
    {
    }

    /// <summary>The chart type drawn (the first supported type of the plot area).</summary>
    public ChartKind Kind { get; internal set; } = ChartKind.Unsupported;

    /// <summary>For a bar chart: horizontal bars (<c>c:barDir</c> <c>bar</c>) rather than columns.</summary>
    public bool Horizontal { get; internal set; }

    /// <summary>How the series share a category (bar and line charts).</summary>
    public ChartGrouping Grouping { get; internal set; }

    /// <summary>The chart title, or null when it has none.</summary>
    public string? Title { get; internal set; }

    /// <summary>The title of the category axis, or null.</summary>
    public string? CategoryAxisTitle { get; internal set; }

    /// <summary>The title of the value axis, or null.</summary>
    public string? ValueAxisTitle { get; internal set; }

    /// <summary>Where the legend stands, or null when the chart has none.</summary>
    public ChartLegendPosition? Legend { get; internal set; }

    /// <summary>Data labels show the values (<c>c:showVal</c>).</summary>
    public bool ShowValues { get; internal set; }

    /// <summary>Data labels of a pie or doughnut show the percentages (<c>c:showPercent</c>).</summary>
    public bool ShowPercentages { get; internal set; }

    /// <summary>The value axis has major gridlines.</summary>
    public bool ValueGridlines { get; internal set; }

    /// <summary>The categories run from the far end of their axis (<c>c:orientation</c> <c>maxMin</c>).</summary>
    public bool ReverseCategories { get; internal set; }

    /// <summary>Space between bar groups, in percent of a bar's width (<c>c:gapWidth</c>, 150 by default).</summary>
    public int GapWidth { get; internal set; } = 150;

    /// <summary>The hole of a doughnut, in percent of its radius (<c>c:holeSize</c>).</summary>
    public int HoleSize { get; internal set; } = 50;

    /// <summary>The angle of the first slice of a pie, in degrees clockwise from the top (<c>c:firstSliceAng</c>).</summary>
    public int FirstSliceAngle { get; internal set; }

    /// <summary>The fill of the chart area as <c>RRGGBB</c>, or null when it has none.</summary>
    public string? Background { get; internal set; } = "FFFFFF";

    /// <summary>The outline of the chart area as <c>RRGGBB</c>, or null when it has none.</summary>
    public string? Border { get; internal set; } = "D9D9D9";

    /// <summary>The series, in plot order.</summary>
    public IReadOnlyList<ChartSeries> Series { get; internal set; } = [];

    /// <summary>The chart types of the plot area this package does not draw (<c>scatter chart</c>, <c>3-D bar chart</c>,
    /// a second type of a combination chart...), each named once.</summary>
    public IReadOnlyList<string> UnsupportedTypes { get; internal set; } = [];

    /// <summary>The category labels: those of the series with the most points.</summary>
    public IReadOnlyList<string> Categories =>
        Series.Count == 0 ? [] : Series.MaxBy(s => Math.Max(s.Categories.Count, s.Values.Count))!.Categories;

    /// <summary>The legend names the points (categories) rather than the series: a pie, a doughnut, or a bar chart of one
    /// series whose colours vary by point.</summary>
    internal bool ByPoint { get; set; }

    /// <summary>Series (or the data of series) past the chart point budget were left out when the chart was read; the
    /// chart is drawn with what was kept and reported as a gap.</summary>
    internal bool Truncated { get; set; }

    /// <summary>The number of points of the longest series.</summary>
    internal int PointCount => Series.Count == 0 ? 0 : Series.Max(s => Math.Max(s.Categories.Count, s.Values.Count));
}
