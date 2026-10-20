// SPDX-License-Identifier: EUPL-1.2
using System.IO.Compression;
using System.Text;
using OmniEurope.Documents.Charts;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Tests.Word;
using OmniEurope.Documents.Word;
using static OmniEurope.Documents.Tests.Charts.ChartFactory;

namespace OmniEurope.Documents.Tests.Charts;

/// <summary>
/// Chart parts a hostile or damaged file can forge: the reader never throws a raw exception nor allocates by a
/// declared size, an unreadable chart is skipped and reported, and a number outside its range is read as missing or
/// clamped to the range ECMA-376 part 1, §21.2 gives it.
/// </summary>
public sealed class ChartRobustnessTests
{
    private static readonly string[] Quarters = ["T1", "T2", "T3"];

    private static Chart ReadWord(string part) =>
        WordDocument.Load(Docx(part)).Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordChart>().Single().Chart;

    private static XlsxChart ReadExcel(string part, string data = "", string? anchor = null) =>
        XlsxWorkbook.Load(Xlsx(data, part, anchor ?? TwoCell(3, 1, 9, 16))).Worksheets[0].Charts.Single();

    // A series whose caches declare `declared` points and hold the given point elements.
    private static string Declared(int index, string declared, string points = "") =>
        $"<c:ser><c:idx val=\"{index}\"/><c:order val=\"{index}\"/><c:cat><c:strRef><c:f>Feuil1!$A$1</c:f><c:strCache><c:ptCount val=\"{declared}\"/>{points}</c:strCache></c:strRef></c:cat>"
        + $"<c:val><c:numRef><c:f>Feuil1!$B$1</c:f><c:numCache><c:formatCode>General</c:formatCode><c:ptCount val=\"{declared}\"/>{points}</c:numCache></c:numRef></c:val></c:ser>";

    // A series with no cache, only the formula of its values.
    private static string Uncached(int index, string values) =>
        $"<c:ser><c:idx val=\"{index}\"/><c:order val=\"{index}\"/><c:val><c:numRef><c:f>{values}</c:f></c:numRef></c:val></c:ser>";

    private static string Cached(string values) =>
        $"<c:ser><c:idx val=\"0\"/><c:order val=\"0\"/><c:val><c:numRef><c:f>Feuil1!$B$1</c:f><c:numCache><c:formatCode>General</c:formatCode><c:ptCount val=\"{values.Split(',').Length}\"/>"
        + string.Concat(values.Split(',').Select((v, i) => $"<c:pt idx=\"{i}\"><c:v>{v}</c:v></c:pt>")) + "</c:numCache></c:numRef></c:val></c:ser>";

    private static byte[] Replace(byte[] package, string part, string content)
    {
        using var stream = new MemoryStream();
        stream.Write(package);
        stream.Position = 0;
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            zip.GetEntry(part)!.Delete();
            using var entry = zip.CreateEntry(part).Open();
            entry.Write(Encoding.UTF8.GetBytes(content));
        }

        return stream.ToArray();
    }

    private static string Cell(int row, string value) => $"""<row r="{row}"><c r="B{row}"><v>{value}</v></c></row>""";

    // ---- Point budget -----------------------------------------------------------------------------------------

    [Fact]
    public void A_cache_declaring_many_points_without_any_is_not_expanded()
    {
        var series = string.Concat(Enumerable.Range(0, 20).Select(i => Declared(i, "100000")));

        var chart = ReadWord(ChartSpace(Bar(series), Axes()));

        Assert.Equal(20, chart.Series.Count);
        Assert.All(chart.Series, s => Assert.Empty(s.Values));
        Assert.All(chart.Series, s => Assert.Empty(s.Categories));
        Assert.False(chart.Truncated);
    }

    [Fact]
    public void Thousands_of_series_declaring_many_points_are_read_without_allocating_them()
    {
        // About 1 MB of XML that the declared sizes would turn into 8 000 x 100 000 points.
        var series = string.Concat(Enumerable.Range(0, 8000).Select(i => Declared(i, "100000")));

        var chart = ReadWord(ChartSpace(Bar(series), Axes()));

        Assert.Equal(8000, chart.Series.Count);
        Assert.All(chart.Series, s => Assert.Empty(s.Values));
    }

    [Fact]
    public void Series_past_the_point_budget_are_left_out_and_the_chart_is_marked_truncated()
    {
        // One point at the last index makes each series 100 000 points long: ten of them fill the budget.
        var point = "<c:pt idx=\"99999\"><c:v>1</c:v></c:pt>";
        var series = string.Concat(Enumerable.Range(0, 20).Select(i => Declared(i, "100000", point)));

        var chart = ReadWord(ChartSpace(Bar(series), Axes()));

        Assert.Equal(ChartReader.MaxChartPoints / 100_000, chart.Series.Count);
        Assert.True(chart.Truncated);
        Assert.All(chart.Series, s => Assert.Equal(100_000, s.Values.Count));
    }

    [Fact]
    public void A_chart_within_the_budget_is_not_marked_truncated()
    {
        var series = string.Concat(Enumerable.Range(0, 10).Select(i => Declared(i, "100000", "<c:pt idx=\"99999\"><c:v>1</c:v></c:pt>")));

        var chart = ReadWord(ChartSpace(Bar(series), Axes()));

        Assert.Equal(10, chart.Series.Count);
        Assert.False(chart.Truncated);
    }

    [Fact]
    public void A_truncated_chart_is_drawn_and_reported_as_a_gap()
    {
        var document = WordDocument.Load(Docx(ChartSpace(Bar(Series(0, "A", Quarters, [1, 2, 3])), Axes())));
        document.Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordChart>().Single().Chart.Truncated = true;

        var result = WordToPdf.Convert(document);

        Assert.Contains(result.Gaps, g => g.StartsWith("chart past the point limit", StringComparison.Ordinal));
    }

    [Fact]
    public void A_workbook_chart_with_many_series_over_large_ranges_stays_within_the_point_budget()
    {
        // 500 series share the budget: 2 000 points each, and every one names 3 000 cells.
        var data = string.Concat(Enumerable.Range(1, 3000).Select(r => Cell(r, "1")));
        var series = string.Concat(Enumerable.Range(0, 500).Select(i => Uncached(i, "Feuil1!$B$1:$B$3000")));

        var chart = ReadExcel(ChartSpace(Bar(series), Axes()), data);

        Assert.All(chart.Chart.Series, s => Assert.Empty(s.Values));
        Assert.True(chart.Chart.Truncated);
    }

    [Fact]
    public void A_workbook_series_within_its_share_of_the_budget_reads_its_cells()
    {
        var data = string.Concat(Enumerable.Range(1, 3000).Select(r => Cell(r, "2")));

        var chart = ReadExcel(ChartSpace(Bar(Uncached(0, "Feuil1!$B$1:$B$3000")), Axes()), data);

        Assert.Equal(3000, chart.Chart.Series[0].Values.Count);
        Assert.False(chart.Chart.Truncated);
    }

    // ---- Accent index and isolation of unreadable parts --------------------------------------------------------

    [Theory]
    [InlineData(-1)]
    [InlineData(-7)]
    [InlineData(int.MinValue)]
    public void A_negative_series_index_takes_an_accent_colour(int index)
    {
        var part = ChartSpace(Bar(Series(index, "A", Quarters, [1, 2, 3])), Axes());

        var word = ReadWord(part);
        var excel = ReadExcel(part);

        Assert.Matches("^[0-9A-F]{6}$", word.Series[0].Color);
        Assert.Matches("^[0-9A-F]{6}$", excel.Chart.Series[0].Color);
    }

    [Fact]
    public void A_chart_part_that_is_not_well_formed_is_skipped_and_reported_in_a_document()
    {
        var document = WordDocument.Load(Docx("<c:chartSpace"));

        Assert.Empty(document.Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordChart>());
        Assert.Contains("unreadable chart skipped", document.Gaps);
    }

    [Fact]
    public void A_chart_part_that_is_not_well_formed_is_skipped_and_reported_in_a_workbook()
    {
        var workbook = XlsxWorkbook.Load(Xlsx(string.Empty, "<c:chartSpace", TwoCell(3, 1, 9, 16)));

        Assert.Empty(workbook.Worksheets[0].Charts);
        Assert.Contains("unreadable chart skipped", ExcelToPdf.ToWord(workbook).Gaps);
    }

    [Fact]
    public void A_workbook_exposes_the_gap_of_a_chart_part_it_skipped()
    {
        var workbook = XlsxWorkbook.Load(Xlsx(string.Empty, "<c:chartSpace", TwoCell(3, 1, 9, 16)));

        Assert.Equal(["unreadable chart skipped"], workbook.Gaps);
    }

    [Fact]
    public void A_workbook_with_a_readable_chart_has_no_gap()
    {
        var workbook = XlsxWorkbook.Load(Xlsx(string.Empty, ChartSpace(Pie(Series(0, "P", Quarters, [1, 2, 3]))), TwoCell(3, 1, 9, 16)));

        Assert.Single(workbook.Worksheets[0].Charts);
        Assert.Empty(workbook.Gaps);
    }

    [Fact]
    public void A_drawing_part_that_is_not_well_formed_is_skipped_and_reported()
    {
        var good = Xlsx(string.Empty, ChartSpace(Pie(Series(0, "P", Quarters, [1, 2, 3]))), TwoCell(3, 1, 9, 16));

        var workbook = XlsxWorkbook.Load(Replace(good, "xl/drawings/drawing1.xml", "<xdr:wsDr"));

        Assert.Empty(workbook.Worksheets[0].Charts);
        Assert.Contains("unreadable drawing skipped", ExcelToPdf.ToWord(workbook).Gaps);
    }

    [Fact]
    public void A_theme_part_that_is_not_well_formed_leaves_the_workbook_charts_in_the_office_colours()
    {
        var good = Xlsx(string.Empty, ChartSpace(Bar(Series(0, "A", Quarters, [1, 2, 3])), Axes()), TwoCell(3, 1, 9, 16), theme: "112233");

        var workbook = XlsxWorkbook.Load(Replace(good, "xl/theme/theme1.xml", "<a:theme"));

        Assert.Equal("4472C4", workbook.Worksheets[0].Charts.Single().Chart.Series[0].Color);
        Assert.Contains("unreadable theme skipped", ExcelToPdf.ToWord(workbook).Gaps);
    }

    [Fact]
    public void A_theme_part_that_is_not_well_formed_leaves_the_document_chart_in_the_office_colours()
    {
        var good = Docx(ChartSpace(Bar(Series(0, "A", Quarters, [1, 2, 3])), Axes()), theme: "112233");

        var document = WordDocument.Load(Replace(good, "word/theme/theme1.xml", "<a:theme"));

        Assert.Equal("4472C4", document.Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordChart>().Single().Chart.Series[0].Color);
        Assert.Contains("unreadable theme skipped", document.Gaps);
    }

    // ---- Formula budget per cell --------------------------------------------------------------------------------

    [Fact]
    public void Each_formula_cell_of_a_series_has_the_whole_formula_budget()
    {
        // SUM over a column of a million rows allocates a million cells each time; the budget is eight million per
        // formula, so ten cells fit only if the budget starts afresh with each one.
        var data = string.Concat(Enumerable.Range(1, 10).Select(r => $"""<row r="{r}"><c r="B{r}"><f>SUM(C1:C1000000)</f></c></row>"""))
            + """<row r="1000000"><c r="C1000000"><v>1</v></c></row>""";

        var chart = ReadExcel(ChartSpace(Bar(Uncached(0, "Feuil1!$B$1:$B$10")), Axes()), data);

        Assert.Equal(Enumerable.Repeat<double?>(1, 10), chart.Chart.Series[0].Values);
    }

    // ---- Colours ----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("  1234", "")]
    [InlineData(" 12345", "")]
    [InlineData("12345 ", "")]
    [InlineData("  1234", "<a:lumMod val=\"50000\"/>")]
    [InlineData(" 12345", "<a:tint val=\"50000\"/>")]
    public void A_colour_with_white_space_in_its_value_is_not_a_colour(string value, string transform)
    {
        var fill = $"<c:spPr><a:solidFill><a:srgbClr val=\"{value}\">{transform}</a:srgbClr></a:solidFill></c:spPr>";

        var chart = ReadWord(ChartSpace(Bar(Series(0, "A", Quarters, [1, 2, 3], fill)), Axes()));

        Assert.Equal("4472C4", chart.Series[0].Color);
    }

    // ---- Numbers ----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1E999")]
    public void A_cached_value_that_is_not_a_finite_number_is_a_missing_value(string value)
    {
        var chart = ReadWord(ChartSpace(Bar(Cached($"1,{value},3")), Axes()));

        Assert.Equal([1d, null, 3d], chart.Series[0].Values);
    }

    [Fact]
    public void A_category_that_is_not_a_finite_number_keeps_its_text()
    {
        var series = "<c:ser><c:idx val=\"0\"/><c:order val=\"0\"/><c:cat><c:numRef><c:f>Feuil1!$A$1</c:f><c:numCache><c:formatCode>0.00</c:formatCode><c:ptCount val=\"2\"/>"
            + "<c:pt idx=\"0\"><c:v>NaN</c:v></c:pt><c:pt idx=\"1\"><c:v>2</c:v></c:pt></c:numCache></c:numRef></c:cat></c:ser>";

        var chart = ReadWord(ChartSpace(Bar(series), Axes()));

        Assert.Equal(["NaN", "2.00"], chart.Series[0].Categories);
    }

    [Theory]
    [InlineData("NaN", "Infinity")]
    [InlineData("-Infinity", "NaN")]
    public void An_anchor_with_a_non_finite_number_reads_it_as_missing(string x, string y)
    {
        var anchor = $"<xdr:oneCellAnchor><xdr:from><xdr:col>1</xdr:col><xdr:colOff>{x}</xdr:colOff><xdr:row>2</xdr:row><xdr:rowOff>{y}</xdr:rowOff></xdr:from><xdr:ext cx=\"{x}\" cy=\"{y}\"/>{{frame}}</xdr:oneCellAnchor>";

        var chart = ReadExcel(ChartSpace(Pie(Series(0, "P", Quarters, [1, 2, 3]))), anchor: anchor);

        Assert.Equal(1, chart.Width);
        Assert.Equal(1, chart.Height);
        Assert.Equal(0, chart.OffsetX);
        Assert.Equal(0, chart.OffsetY);
    }

    [Fact]
    public void An_anchor_position_and_extent_are_bounded()
    {
        var anchor = "<xdr:absoluteAnchor><xdr:pos x=\"1E300\" y=\"1E300\"/><xdr:ext cx=\"1E300\" cy=\"1E300\"/>{frame}</xdr:absoluteAnchor>";

        var chart = ReadExcel(ChartSpace(Pie(Series(0, "P", Quarters, [1, 2, 3]))), anchor: anchor);

        Assert.Equal(XlsxChartReader.MaxExtent, chart.Width);
        Assert.Equal(XlsxChartReader.MaxExtent, chart.Height);
        Assert.True(chart.Cells.LastRow <= CellReference.MaxRow && chart.Cells.LastColumn <= CellReference.MaxColumn);
    }

    [Theory]
    [InlineData(-100, 0)]
    [InlineData(9999, 500)]
    [InlineData(150, 150)]
    [InlineData(0, 0)]
    public void The_gap_width_of_bars_stays_within_zero_and_five_hundred(int declared, int expected)
    {
        var part = ChartSpace(Bar(Series(0, "A", Quarters, [1, 2, 3])).Replace("<c:gapWidth val=\"150\"/>", $"<c:gapWidth val=\"{declared}\"/>", StringComparison.Ordinal), Axes());

        Assert.Equal(expected, ReadWord(part).GapWidth);
        Assert.Equal(expected, ReadExcel(part).Chart.GapWidth);
    }

    [Fact]
    public void A_chart_extent_past_the_range_of_a_double_reads_as_missing()
    {
        var docx = Docx(ChartSpace(Pie(Series(0, "P", Quarters, [1, 2, 3]))));
        var forged = Replace(docx, "word/document.xml", DocxFactory.Part(docx, "word/document.xml").Replace("cx=\"3810000\"", "cx=\"1E999\"", StringComparison.Ordinal));

        var chart = WordDocument.Load(forged).Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordChart>().Single();

        Assert.Equal(0, chart.Width);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(200, 90)]
    [InlineData(-5, 1)]
    [InlineData(60, 60)]
    public void The_hole_of_a_doughnut_stays_within_one_and_ninety_percent(int declared, int expected) =>
        Assert.Equal(expected, ReadWord(ChartSpace(Doughnut(Series(0, "A", Quarters, [1, 2, 3]), hole: declared))).HoleSize);

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(999, 360)]
    [InlineData(90, 90)]
    public void The_first_slice_angle_stays_within_zero_and_three_hundred_sixty_degrees(int declared, int expected) =>
        Assert.Equal(expected, ReadWord(ChartSpace(Pie(Series(0, "A", Quarters, [1, 2, 3]), firstSlice: declared))).FirstSliceAngle);

    [Fact]
    public void A_vary_colours_element_without_a_value_colours_the_slices_in_turn()
    {
        var pie = Pie(Series(0, "A", Quarters, [1, 2, 3])).Replace("<c:varyColors val=\"1\"/>", "<c:varyColors/>", StringComparison.Ordinal);

        var chart = ReadWord(ChartSpace(pie));

        Assert.Equal(["4472C4", "ED7D31", "A5A5A5"], [chart.Series[0].ColorOf(0), chart.Series[0].ColorOf(1), chart.Series[0].ColorOf(2)]);
    }
}
