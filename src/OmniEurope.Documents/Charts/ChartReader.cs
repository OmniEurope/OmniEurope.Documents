// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml.Linq;
using OmniEurope.Documents.Excel;
using static OmniEurope.Documents.Charts.ChartPalette;

namespace OmniEurope.Documents.Charts;

/// <summary>
/// Reads a chart part (<c>c:chartSpace</c>, ECMA-376 part 1, §21.2.2.29): the first bar, line, pie or doughnut type of
/// its plot area with its series (names, categories and values from their caches or literals), the title, the axis
/// titles, gridlines and category order, the legend position, data labels showing values or percentages, and the
/// colours of series, points and the chart area. Elements are matched by local name, so the transitional and strict
/// namespaces read alike. Every other type of the plot area is named in <see cref="Chart.UnsupportedTypes"/>.
/// </summary>
internal static class ChartReader
{
    /// <summary>
    /// The most points of one chart: its series times the points of its longest series, which is the size of the matrix
    /// the plot allocates. A series that would pass it, and every one after it, is left out and the chart is marked
    /// <see cref="Chart.Truncated"/>. One million is ten full series of the 100 000 points a series may hold.
    /// </summary>
    internal const int MaxChartPoints = 1_000_000;

    /// <summary>The most points of one series: the highest point index a cache may use, plus one.</summary>
    internal const int MaxPoints = 100_000;

    /// <summary>
    /// A part that cannot be read as a chart, a drawing or a theme: not well-formed XML, a package limit passed, or a
    /// value no reader could parse. The part is skipped and reported; any other exception is a defect and propagates.
    /// </summary>
    internal static bool IsUnreadable(Exception exception) =>
        exception is DocumentFormatException or System.Xml.XmlException or FormatException or InvalidDataException;

    private static readonly Dictionary<string, ChartKind> Supported = new(StringComparer.Ordinal)
    {
        ["barChart"] = ChartKind.Bar,
        ["lineChart"] = ChartKind.Line,
        ["pieChart"] = ChartKind.Pie,
        ["doughnutChart"] = ChartKind.Doughnut,
    };

    private static readonly Dictionary<string, string> Unsupported = new(StringComparer.Ordinal)
    {
        ["areaChart"] = "area chart",
        ["area3DChart"] = "3-D area chart",
        ["bar3DChart"] = "3-D bar chart",
        ["line3DChart"] = "3-D line chart",
        ["pie3DChart"] = "3-D pie chart",
        ["ofPieChart"] = "pie of pie chart",
        ["radarChart"] = "radar chart",
        ["scatterChart"] = "scatter chart",
        ["bubbleChart"] = "bubble chart",
        ["stockChart"] = "stock chart",
        ["surfaceChart"] = "surface chart",
        ["surface3DChart"] = "3-D surface chart",
    };

    private static readonly Dictionary<string, ChartLegendPosition> LegendPositions = new(StringComparer.Ordinal)
    {
        ["l"] = ChartLegendPosition.Left,
        ["t"] = ChartLegendPosition.Top,
        ["b"] = ChartLegendPosition.Bottom,
        ["tr"] = ChartLegendPosition.TopRight,
    };

    public static Chart Read(XElement chartSpace, ChartPalette palette)
    {
        var chart = new Chart();
        var body = Child(chartSpace, "chart");
        var plot = Child(body, "plotArea");
        var groups = plot?.Elements().Where(e => e.Name.LocalName.EndsWith("Chart", StringComparison.Ordinal)).ToList() ?? [];
        var main = groups.Find(g => Supported.ContainsKey(g.Name.LocalName));
        if (main is not null)
        {
            Group(chart, main, palette);
        }

        chart.UnsupportedTypes = groups.Where(g => g != main).Select(Name).Distinct(StringComparer.Ordinal).ToList();
        Axes(chart, plot);
        chart.Title = Title(body, chart);
        chart.Legend = Child(body, "legend") is { } legend ? LegendPositions.GetValueOrDefault(Value(Child(legend, "legendPos")) ?? "r", ChartLegendPosition.Right) : null;
        var area = Child(chartSpace, "spPr");
        chart.Background = None(area) ? null : palette.Fill(area) ?? chart.Background;
        chart.Border = None(area, line: true) ? null : palette.Fill(area, line: true) ?? chart.Border;
        return chart;
    }

    private static string Name(XElement group)
    {
        var local = group.Name.LocalName;
        return Unsupported.TryGetValue(local, out var name) ? name
            : Supported.ContainsKey(local) ? local[..^"Chart".Length] + " chart combined with another type"
            : local;
    }

    private static void Group(Chart chart, XElement group, ChartPalette palette)
    {
        chart.Kind = Supported[group.Name.LocalName];
        chart.Horizontal = Value(Child(group, "barDir")) == "bar";
        chart.Grouping = Grouping(Value(Child(group, "grouping")));
        chart.GapWidth = Math.Clamp(Number(Child(group, "gapWidth"), 150), 0, 500);
        chart.HoleSize = Math.Clamp(Number(Child(group, "holeSize"), 50), 1, 90);
        chart.FirstSliceAngle = Math.Clamp(Number(Child(group, "firstSliceAng"), 0), 0, 360);
        var labels = Child(group, "dLbls");
        chart.ShowValues = Flag(labels, "showVal");
        chart.ShowPercentages = Flag(labels, "showPercent");
        var elements = group.Elements().Where(e => e.Name.LocalName == "ser").ToList();
        var vary = Value(Child(group, "varyColors")) is "1" or "true" || (Child(group, "varyColors") is { } v && Value(v) is null);
        chart.ByPoint = chart.Kind is ChartKind.Pie or ChartKind.Doughnut || (vary && chart.Kind == ChartKind.Bar && elements.Count == 1);
        var markers = chart.Kind == ChartKind.Line && Child(group, "marker") is { } marker && Value(marker) is null or "1" or "true";
        chart.Series = Series(chart, elements, palette, markers, vary && chart.ByPoint);
    }

    private static ChartGrouping Grouping(string? value) => value switch
    {
        "stacked" => ChartGrouping.Stacked,
        "percentStacked" => ChartGrouping.PercentStacked,
        _ => ChartGrouping.Standard,
    };

    private static List<ChartSeries> Series(Chart chart, List<XElement> elements, ChartPalette palette, bool markers, bool vary)
    {
        var series = new List<ChartSeries>();
        var longest = 1;
        foreach (var element in elements)
        {
            // The plot allocates series x longest series: the budget is checked before the series' lists are.
            longest = Math.Max(longest, Extent(element));
            if ((long)(series.Count + 1) * longest > MaxChartPoints)
            {
                chart.Truncated = true;
                break;
            }

            var read = Series(element, series.Count, chart.Kind, palette);
            read.Markers = markers && Value(Child(Child(element, "marker"), "symbol")) != "none";
            if (vary)
            {
                Vary(read, palette);
            }

            chart.ShowValues |= Flag(Child(element, "dLbls"), "showVal");
            chart.ShowPercentages |= Flag(Child(element, "dLbls"), "showPercent");
            series.Add(read);
        }

        return series;
    }

    // Slices take the accents in turn, unless a point names its own colour.
    private static void Vary(ChartSeries series, ChartPalette palette)
    {
        var colors = new Dictionary<int, string>();
        for (var i = 0; i < Math.Max(series.Values.Count, series.Categories.Count); i++)
        {
            colors[i] = series.PointColors.TryGetValue(i, out var own) ? own : palette.Accent(i);
        }

        series.PointColors = colors;
    }

    private static ChartSeries Series(XElement element, int position, ChartKind kind, ChartPalette palette)
    {
        var index = Number(Child(element, "idx"), position);
        var properties = Child(element, "spPr");
        var line = kind == ChartKind.Line;
        var series = new ChartSeries
        {
            Color = palette.Fill(properties, line) ?? (line ? null : palette.Fill(properties, line: true)) ?? palette.Accent(index),
            NameReference = Formula(Child(element, "tx")),
            CategoriesReference = Formula(Child(element, "cat")),
            ValuesReference = Formula(Child(element, "val")),
        };
        var name = Child(element, "tx");
        series.Name = Child(name, "v")?.Value ?? Points(Cache(name)).Values.FirstOrDefault();
        var (values, format) = Numbers(Cache(Child(element, "val")));
        series.Values = values;
        series.FormatCode = format;
        series.Categories = Labels(Cache(Child(element, "cat")));
        series.PointColors = element.Elements().Where(e => e.Name.LocalName == "dPt")
            .Select(p => (Index: Number(Child(p, "idx"), -1), Color: palette.Fill(Child(p, "spPr"), line)))
            .Where(p => p.Index >= 0 && p.Color is not null)
            .ToDictionary(p => p.Index, p => p.Color!);
        return series;
    }

    // The cache or literal of a data source: c:numRef/c:numCache, c:strRef/c:strCache, c:multiLvlStrRef (its first
    // level), c:numLit or c:strLit.
    private static XElement? Cache(XElement? source)
    {
        var data = source?.Elements().FirstOrDefault();
        if (data is null)
        {
            return null;
        }

        return data.Name.LocalName switch
        {
            "numLit" or "strLit" => data,
            "multiLvlStrRef" => Child(Child(data, "multiLvlStrCache"), "lvl"),
            _ => data.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith("Cache", StringComparison.Ordinal)),
        };
    }

    private static string? Formula(XElement? source) => source?.Elements().FirstOrDefault() is { } data ? Child(data, "f")?.Value : null;

    private static SortedDictionary<int, string> Points(XElement? cache)
    {
        var points = new SortedDictionary<int, string>();
        foreach (var point in cache?.Elements().Where(e => e.Name.LocalName == "pt") ?? [])
        {
            var index = Number(point, -1, "idx");
            if (index >= 0 && index < MaxPoints && Child(point, "v") is { } v)
            {
                points[index] = v.Value;
            }
        }

        return points;
    }

    // The points a cache holds: up to its highest point index. Its ptCount is a claim, not a size: a cache that declares
    // 100 000 points and holds none is empty (the lists are sized by what the file contains, not by what it says).
    private static int Count(SortedDictionary<int, string> points) => points.Count == 0 ? 0 : points.Keys.Max() + 1;

    // The longest of a series' category and value caches, read before either is turned into a list.
    private static int Extent(XElement series) =>
        Math.Max(Count(Points(Cache(Child(series, "val")))), Count(Points(Cache(Child(series, "cat")))));

    // A finite number, else null: NaN and the infinities (and numbers past the range of a double) are no values.
    private static double? Finite(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) ? number : null;

    private static (List<double?> Values, string Format) Numbers(XElement? cache)
    {
        var points = Points(cache);
        var values = new List<double?>();
        var count = Count(points);
        for (var i = 0; i < count; i++)
        {
            values.Add(points.TryGetValue(i, out var text) ? Finite(text) : null);
        }

        return (values, Child(cache, "formatCode")?.Value is { Length: > 0 } format ? format : "General");
    }

    // Category labels; numbers are shown in their cache's format (a date axis keeps its serial numbers otherwise).
    private static List<string> Labels(XElement? cache)
    {
        var points = Points(cache);
        var format = Child(cache, "formatCode")?.Value;
        var labels = new List<string>();
        var count = Count(points);
        for (var i = 0; i < count; i++)
        {
            var text = points.GetValueOrDefault(i, string.Empty);
            labels.Add(format is { Length: > 0 } && Finite(text) is { } number
                ? NumberFormatter.Format(number, format, CultureInfo.InvariantCulture)
                : text);
        }

        return labels;
    }

    private static void Axes(Chart chart, XElement? plot)
    {
        var axes = plot?.Elements().Where(e => e.Name.LocalName is "catAx" or "valAx" or "dateAx" or "serAx").ToList() ?? [];
        var category = axes.Find(a => a.Name.LocalName is "catAx" or "dateAx");
        var value = axes.Find(a => a.Name.LocalName == "valAx");
        chart.CategoryAxisTitle = Text(Child(category, "title"));
        chart.ValueAxisTitle = Text(Child(value, "title"));
        chart.ValueGridlines = Child(value, "majorGridlines") is not null;
        chart.ReverseCategories = Value(Child(Child(category, "scaling"), "orientation")) == "maxMin";
    }

    // An explicit title, or the name of the only series when the title has no text of its own (as the editors show it).
    private static string? Title(XElement? body, Chart chart)
    {
        var title = Child(body, "title");
        if (title is null)
        {
            return chart.Series.Count == 1 && Value(Child(body, "autoTitleDeleted")) is "0" or "false" ? chart.Series[0].Name : null;
        }

        return Text(title) ?? (chart.Series.Count == 1 ? chart.Series[0].Name : "Chart Title");
    }

    // The text of a title: its rich text paragraphs (one line each) or its string reference's cache.
    private static string? Text(XElement? title)
    {
        var tx = Child(title, "tx");
        if (tx is null)
        {
            return null;
        }

        if (Child(tx, "rich") is { } rich)
        {
            var lines = rich.Elements().Where(e => e.Name.LocalName == "p")
                .Select(p => string.Concat(p.Descendants().Where(e => e.Name.LocalName == "t").Select(t => t.Value)));
            var text = string.Join("\n", lines).Trim();
            return text.Length > 0 ? text : null;
        }

        return Points(Cache(tx)).Values.FirstOrDefault();
    }

    private static bool Flag(XElement? labels, string name) => Child(labels, name) is { } flag && Value(flag) is null or "1" or "true";

    private static string? Value(XElement? element) => (string?)element?.Attribute("val");

    private static int Number(XElement? element, int fallback, string attribute = "val") =>
        int.TryParse((string?)element?.Attribute(attribute), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
