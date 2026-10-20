// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Charts;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Tests.Word;
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Validation;
using static OmniEurope.Documents.Tests.Charts.ChartFactory;

namespace OmniEurope.Documents.Tests.Charts;

/// <summary>The chart model read from Word and Excel packages whose chart parts follow ECMA-376 part 1, §21.2.</summary>
public sealed class ChartReadingTests
{
    private static readonly string[] Quarters = ["T1", "T2", "T3"];

    private static Chart ReadWord(string part, string? theme = null) =>
        WordDocument.Load(Docx(part, theme: theme)).Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordChart>().Single().Chart;

    [Fact]
    public void A_clustered_column_chart_reads_its_series_titles_and_legend()
    {
        var part = ChartSpace(
            Bar(Series(0, "Ventes", Quarters, [10, 20, 40], Fill("FF0000")) + Series(1, "Coûts", Quarters, [5, null, 15])),
            Axes("Trimestre", "Euros", gridlines: true),
            title: "Résultats",
            legend: "b");
        var docx = Docx(part);

        var chart = ReadWord(part);

        Assert.Empty(WordSchemaValidator.Validate(docx).Errors);
        Assert.Equal(ChartKind.Bar, chart.Kind);
        Assert.False(chart.Horizontal);
        Assert.Equal(ChartGrouping.Standard, chart.Grouping);
        Assert.Equal("Résultats", chart.Title);
        Assert.Equal("Trimestre", chart.CategoryAxisTitle);
        Assert.Equal("Euros", chart.ValueAxisTitle);
        Assert.Equal(ChartLegendPosition.Bottom, chart.Legend);
        Assert.True(chart.ValueGridlines);
        Assert.Equal(Quarters, chart.Categories);
        Assert.Equal(["Ventes", "Coûts"], chart.Series.Select(s => s.Name));
        Assert.Equal([10d, 20d, 40d], chart.Series[0].Values);
        Assert.Equal([5d, null, 15d], chart.Series[1].Values);
        Assert.Equal("FF0000", chart.Series[0].Color);
        Assert.Equal("ED7D31", chart.Series[1].Color);
        Assert.Equal("Feuil1!$B$2:$B$4", chart.Series[0].ValuesReference);
        Assert.Equal("Feuil1!$A$2:$A$4", chart.Series[0].CategoriesReference);
        Assert.Equal("Feuil1!$B$1", chart.Series[0].NameReference);
        Assert.Empty(chart.UnsupportedTypes);
    }

    [Theory]
    [InlineData("bar", "stacked", true, ChartGrouping.Stacked)]
    [InlineData("col", "percentStacked", false, ChartGrouping.PercentStacked)]
    public void Bar_direction_and_grouping_are_read(string direction, string grouping, bool horizontal, ChartGrouping expected)
    {
        var chart = ReadWord(ChartSpace(Bar(Series(0, "A", Quarters, [1, 2, 3]), direction, grouping), Axes(reverse: true)));

        Assert.Equal(horizontal, chart.Horizontal);
        Assert.Equal(expected, chart.Grouping);
        Assert.True(chart.ReverseCategories);
        Assert.Equal(150, chart.GapWidth);
    }

    [Fact]
    public void A_line_chart_reads_its_line_colour_markers_and_labels()
    {
        var hidden = "<c:marker><c:symbol val=\"none\"/></c:marker>";
        var part = ChartSpace(LineChart(Series(0, "Avec", Quarters, [1, 3, 2], Line("00B050")) + Series(1, "Sans", Quarters, [2, 2, 2], beforeCategories: hidden), labels: Labels(values: true)), Axes(), legend: "r");

        var chart = ReadWord(part);

        Assert.Equal(ChartKind.Line, chart.Kind);
        Assert.Equal("00B050", chart.Series[0].Color);
        Assert.True(chart.Series[0].Markers);
        Assert.False(chart.Series[1].Markers);
        Assert.True(chart.ShowValues);
        Assert.Equal(ChartLegendPosition.Right, chart.Legend);
    }

    [Fact]
    public void A_pie_colours_its_slices_by_point_and_reads_its_percent_labels()
    {
        var point = "<c:dPt><c:idx val=\"1\"/><c:bubble3D val=\"0\"/><c:spPr><a:solidFill><a:schemeClr val=\"accent6\"><a:lumMod val=\"50000\"/></a:schemeClr></a:solidFill></c:spPr></c:dPt>";
        var part = ChartSpace(Pie(Series(0, "Parts", ["Nord", "Sud", "Est"], [1, 1, 2], beforeCategories: point), Labels(values: false, percentages: true), firstSlice: 90), legend: "t", afterTitle: "<c:autoTitleDeleted val=\"0\"/>");

        var chart = ReadWord(part);

        Assert.Equal(ChartKind.Pie, chart.Kind);
        Assert.True(chart.ShowPercentages);
        Assert.False(chart.ShowValues);
        Assert.Equal(90, chart.FirstSliceAngle);
        Assert.Equal("4472C4", chart.Series[0].ColorOf(0));
        // accent6 (112,173,71) at lumMod 50%: HSL luminance 0.4784 -> 0.2392 gives the exact channels 56.0, 86.5 and 35.5,
        // so 56 for red; the Office palette swatch 375623 (red 55) comes from the picker's integer 0-240 scale, not from
        // the transform. The ties of green (86.5) and blue (35.5) round to the even channel, 86 and 36, whatever the
        // floating-point noise around them.
        Assert.Equal("385624", chart.Series[0].ColorOf(1));
        Assert.Equal("A5A5A5", chart.Series[0].ColorOf(2));
        Assert.Equal("Parts", chart.Title);
        Assert.Equal(ChartLegendPosition.Top, chart.Legend);
    }

    [Fact]
    public void A_doughnut_reads_its_hole_and_its_rings()
    {
        var chart = ReadWord(ChartSpace(Doughnut(Series(0, "Un", Quarters, [1, 2, 3]) + Series(1, "Deux", Quarters, [3, 2, 1]), hole: 60)));

        Assert.Equal(ChartKind.Doughnut, chart.Kind);
        Assert.Equal(60, chart.HoleSize);
        Assert.Equal(2, chart.Series.Count);
    }

    [Fact]
    public void Automatic_colours_come_from_the_theme_accents_and_theme_colours_take_their_transforms()
    {
        var themed = "<c:spPr><a:solidFill><a:schemeClr val=\"tx1\"><a:lumOff val=\"50000\"/></a:schemeClr></a:solidFill></c:spPr>";
        var part = ChartSpace(Bar(Series(0, "A", Quarters, [1, 2, 3]) + Series(1, "B", Quarters, [1, 2, 3], themed)), Axes());

        var chart = ReadWord(part, theme: "112233");

        Assert.Equal("112233", chart.Series[0].Color);
        Assert.Equal("808080", chart.Series[1].Color);
    }

    [Fact]
    public void Accents_after_the_sixth_series_darken_then_lighten()
    {
        var series = string.Concat(Enumerable.Range(0, 13).Select(i => Series(i, "S" + i, Quarters, [1, 2, 3])));

        var chart = ReadWord(ChartSpace(Bar(series), Axes()));

        Assert.Equal("4472C4", chart.Series[0].Color);
        Assert.Equal("264478", chart.Series[6].Color);
        Assert.Equal("698ED0", chart.Series[12].Color);
    }

    [Fact]
    public void Unsupported_types_are_named_and_a_combination_keeps_its_first_type()
    {
        var scatter = "<c:scatterChart><c:scatterStyle val=\"lineMarker\"/><c:varyColors val=\"0\"/><c:axId val=\"1\"/><c:axId val=\"2\"/></c:scatterChart>";
        var combination = ReadWord(ChartSpace(Bar(Series(0, "A", Quarters, [1, 2, 3])) + LineChart(Series(1, "B", Quarters, [3, 2, 1])), Axes()));
        var alone = ReadWord(ChartSpace(scatter));

        Assert.Equal(ChartKind.Bar, combination.Kind);
        Assert.Equal(["line chart combined with another type"], combination.UnsupportedTypes);
        Assert.Equal(ChartKind.Unsupported, alone.Kind);
        Assert.Equal(["scatter chart"], alone.UnsupportedTypes);
    }

    [Fact]
    public void An_untitled_chart_of_several_series_shows_the_default_title_and_no_fill_drops_the_area()
    {
        var area = "<c:spPr><a:noFill/><a:ln><a:noFill/></a:ln></c:spPr>";
        var part = ChartSpace(Bar(Series(0, "A", Quarters, [1, 2, 3]) + Series(1, "B", Quarters, [1, 2, 3])), Axes(), area: area, afterTitle: "<c:title><c:overlay val=\"0\"/></c:title><c:autoTitleDeleted val=\"0\"/>");

        var chart = ReadWord(part);

        Assert.Equal("Chart Title", chart.Title);
        Assert.Null(chart.Background);
        Assert.Null(chart.Border);
    }

    [Fact]
    public void Saving_a_document_writes_the_chart_part_back_and_it_reads_again()
    {
        var part = ChartSpace(Bar(Series(0, "Ventes", Quarters, [10, 20, 40])), Axes(), title: "Résultats").Replace("</c:chartSpace>", "<c:externalData r:id=\"rId9\"><c:autoUpdate val=\"0\"/></c:externalData></c:chartSpace>", StringComparison.Ordinal);
        var document = WordDocument.Load(Docx(part));

        var saved = document.ToArray();
        var again = WordDocument.Load(saved).Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordChart>().Single();

        Assert.Empty(WordSchemaValidator.Validate(saved).Errors);
        Assert.Contains("drawingml.chart+xml", DocxFactory.Part(saved, "[Content_Types].xml"), StringComparison.Ordinal);
        Assert.DoesNotContain("externalData", DocxFactory.Part(saved, "word/charts/chart1.xml"), StringComparison.Ordinal);
        Assert.Equal("Résultats", again.Chart.Title);
        Assert.Equal([10d, 20d, 40d], again.Chart.Series[0].Values);
        Assert.Equal(300, again.Width, 3);
    }

    [Fact]
    public void Html_and_markdown_report_the_charts_they_leave_out()
    {
        var docx = Docx(ChartSpace(Bar(Series(0, "A", Quarters, [1, 2, 3])), Axes()));

        Assert.Contains("charts are left out", WordToHtml.Convert(WordDocument.Load(docx)).Gaps);
        Assert.Contains("charts are left out", WordToMarkdown.Convert(WordDocument.Load(docx)).Gaps);
    }

    [Fact]
    public void A_chart_of_an_office_2016_type_is_skipped_and_reported()
    {
        var document = WordDocument.Load(Docx(ChartSpace(Bar(Series(0, "A", Quarters, [1, 2, 3]))), uri: "http://schemas.microsoft.com/office/drawing/2014/chartex"));

        Assert.Contains(document.Gaps, g => g.StartsWith("chart of a type introduced by Office 2016", StringComparison.Ordinal));
    }

    [Fact]
    public void A_workbook_chart_without_cache_reads_the_cells_its_formulas_name()
    {
        var data = """<row r="1"><c r="A1" t="inlineStr"><is><t>Mois</t></is></c><c r="B1" t="inlineStr"><is><t>Ventes</t></is></c></row>"""
            + """<row r="2"><c r="A2" t="inlineStr"><is><t>Janvier</t></is></c><c r="B2"><v>12</v></c></row>"""
            + """<row r="3"><c r="A3" t="inlineStr"><is><t>Février</t></is></c><c r="B3"><f>B2*2</f></c></row>"""
            + """<row r="4"><c r="A4"><v>3</v></c><c r="B4" t="inlineStr"><is><t>n/a</t></is></c></row>""";
        var part = ChartSpace(Bar(Series(0, "Ventes", ["", "", ""], [null, null, null], cached: false)), Axes());

        var sheet = XlsxWorkbook.Load(Xlsx(data, part, TwoCell(3, 1, 9, 16, fromColumnOffset: 12700 * 6, fromRowOffset: 12700 * 3), theme: "C00000")).Worksheets[0];
        var chart = sheet.Charts.Single();

        Assert.Equal("Ventes", chart.Chart.Series[0].Name);
        Assert.Equal(["Janvier", "Février", "3"], chart.Chart.Series[0].Categories);
        Assert.Equal([12d, 24d, null], chart.Chart.Series[0].Values);
        Assert.Equal("C00000", chart.Chart.Series[0].Color);
        Assert.Equal(new XlsxRange(2, 4, 17, 10), chart.Cells);
        Assert.Equal(6, chart.OffsetX, 6);
        Assert.Equal(3, chart.OffsetY, 6);
        Assert.Equal((6 * 48) - 6, chart.Width, 6);
        Assert.Equal((15 * 15) - 3, chart.Height, 6);
    }

    [Fact]
    public void A_cell_past_the_formula_limits_is_read_as_empty_not_thrown()
    {
        var deep = new string('(', 10_000) + "1" + new string(')', 10_000);
        var data = """<row r="1"><c r="A1" t="inlineStr"><is><t>Mois</t></is></c><c r="B1" t="inlineStr"><is><t>Ventes</t></is></c></row>"""
            + """<row r="2"><c r="A2" t="inlineStr"><is><t>Janvier</t></is></c><c r="B2"><v>12</v></c></row>"""
            + $"""<row r="3"><c r="A3" t="inlineStr"><is><t>Février</t></is></c><c r="B3"><f>{deep}</f></c></row>"""
            + """<row r="4"><c r="A4" t="inlineStr"><is><t>Mars</t></is></c><c r="B4"><f>SUM(C1:XFD1048576)</f></c></row>"""
            + """<row r="1048576"><c r="XFD1048576"><v>1</v></c></row>""";
        var part = ChartSpace(Bar(Series(0, "Ventes", ["", "", ""], [null, null, null], cached: false)), Axes());

        var chart = XlsxWorkbook.Load(Xlsx(data, part, TwoCell(3, 1, 9, 16))).Worksheets[0].Charts.Single();

        Assert.Equal([12d, null, null], chart.Chart.Series[0].Values);
    }

    [Theory]
    // The extent is 96 x 75 points over default cells of 48 x 15 points. The one-cell chart starts at column B (48 pt)
    // and ends at 144 pt, the left edge of column D, whose cell holds the edge as the rows do (row 8 at 105 pt): the
    // last column is 4. The absolute chart starts at 72 pt (inside column B) and ends at 168 pt, inside column D: 4.
    [InlineData("<xdr:oneCellAnchor><xdr:from><xdr:col>1</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>2</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from><xdr:ext cx=\"1219200\" cy=\"952500\"/>{frame}</xdr:oneCellAnchor>", 3, 2, 3, 4)]
    [InlineData("<xdr:absoluteAnchor><xdr:pos x=\"914400\" y=\"381000\"/><xdr:ext cx=\"1219200\" cy=\"952500\"/>{frame}</xdr:absoluteAnchor>", 3, 2, 3, 4)]
    public void One_cell_and_absolute_anchors_place_the_chart_by_its_extent(string anchor, int firstRow, int firstColumn, int lastRowOffset, int lastColumn)
    {
        var part = ChartSpace(Pie(Series(0, "P", Quarters, [1, 2, 3])));

        var chart = XlsxWorkbook.Load(Xlsx(string.Empty, part, anchor)).Worksheets[0].Charts.Single();

        Assert.Equal(96, chart.Width, 6);
        Assert.Equal(75, chart.Height, 6);
        Assert.Equal(firstRow, chart.Cells.FirstRow);
        Assert.Equal(firstColumn, chart.Cells.FirstColumn);
        Assert.Equal(firstRow + lastRowOffset + 2, chart.Cells.LastRow);
        Assert.Equal(lastColumn, chart.Cells.LastColumn);
    }
}
