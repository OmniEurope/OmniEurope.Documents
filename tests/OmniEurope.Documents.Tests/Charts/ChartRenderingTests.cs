// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Pdf.Text;
using OmniEurope.Documents.Word;
using static OmniEurope.Documents.Tests.Charts.ChartFactory;

namespace OmniEurope.Documents.Tests.Charts;

/// <summary>
/// Charts drawn in a PDF, from Word and from Excel. The text is read back from the PDF (title, legend, category and
/// tick labels, data labels, with the tick steps worked out by hand from the rule 1, 2 or 5 times a power of ten for
/// about five intervals); the shapes are checked at the pixel with the library's own renderer (72 dpi, one pixel per
/// point) at points computed by hand from the chart geometry, away from edges so that the width of the substituted
/// font cannot move them across one.
/// </summary>
public sealed class ChartRenderingTests
{
    private static readonly string[] Quarters = ["T1", "T2", "T3"];

    // ---- Text: what the PDF says --------------------------------------------------------------------------------

    [Fact]
    public void A_clustered_column_chart_shows_its_title_legend_categories_ticks_and_value_labels()
    {
        // Highest value 40: raw step 40 / 5 = 8, the smallest 1, 2 or 5 times a power of ten not below 8 is 10, so the
        // axis runs 0 to 40 in tens. The labels are the five values; the empty point of the second series has none.
        var part = ChartSpace(
            Bar(Series(0, "Ventes", Quarters, [10, 20, 40]) + Series(1, "Coûts", Quarters, [5, null, 15]), labels: Labels(values: true)),
            Axes("Trimestre", "Euros"),
            title: "Résultats",
            legend: "b");

        var tokens = Tokens(Pdf(Docx(part)).Page);

        Assert.Equal(Sorted("0", "10", "20", "30", "40", "10", "20", "40", "5", "15"), Numbers(tokens));
        Assert.All(new[] { "Résultats", "Ventes", "Coûts", "T1", "T2", "T3", "Trimestre", "Euros" }, word => Assert.Contains(word, tokens));
    }

    [Fact]
    public void A_clustered_column_chart_puts_the_categories_along_the_bottom_and_the_ticks_up_the_left_side()
    {
        var part = ChartSpace(Bar(Series(0, "Ventes", Quarters, [10, 20, 40])), Axes());

        var words = Words(Pdf(Docx(part)).Page);

        Assert.True(Of(words, "T1").Left < Of(words, "T2").Left && Of(words, "T2").Left < Of(words, "T3").Left);
        Assert.Equal(Of(words, "T1").Bottom, Of(words, "T3").Bottom, 0.5);
        Assert.True(Of(words, "40").Bottom > Of(words, "30").Bottom && Of(words, "30").Bottom > Of(words, "0").Bottom, "ticks climb the page");
        Assert.True(Of(words, "40").Right < Of(words, "T1").Left, "the tick labels stand left of the first category");
        Assert.True(Of(words, "0").Bottom > Of(words, "T1").Bottom, "the categories stand under the lowest tick");
    }

    [Fact]
    public void A_horizontal_bar_chart_puts_the_first_category_at_the_bottom_and_the_ticks_along_the_bottom()
    {
        // Highest value 12: raw step 2.4, so 5; the axis runs 0 to 15 in fives.
        var part = ChartSpace(Bar(Series(0, "Ventes", Quarters, [3, 7, 12]), direction: "bar"), Axes(categoryPosition: "l", valuePosition: "b"), legend: "r");

        var page = Pdf(Docx(part)).Page;
        var words = Words(page);

        Assert.Equal(Sorted("0", "5", "10", "15"), Numbers(Tokens(page)));
        Assert.True(Of(words, "T1").Bottom < Of(words, "T3").Bottom, "the first category is the lowest");
        Assert.True(Of(words, "0").Left < Of(words, "5").Left && Of(words, "5").Left < Of(words, "10").Left && Of(words, "10").Left < Of(words, "15").Left);
        Assert.True(Of(words, "15").Top < Of(words, "T1").Bottom, "the ticks stand under the lowest category");
        Assert.True(Of(words, "T1").Right < Of(words, "0").Left + 1, "the categories stand left of the axis");
        Assert.True(Of(words, "Ventes").Left > Of(words, "15").Left, "the legend stands right of the plot");
    }

    [Fact]
    public void A_stacked_column_chart_scales_to_the_sum_of_each_category_and_labels_every_segment()
    {
        // Sums 15, 30 and 45: raw step 9, so 10; the axis runs 0 to 50 in tens.
        var part = ChartSpace(
            Bar(Series(0, "A", Quarters, [10, 20, 30]) + Series(1, "B", Quarters, [5, 10, 15]), grouping: "stacked", labels: Labels(values: true)),
            Axes(),
            legend: "r");

        var tokens = Tokens(Pdf(Docx(part)).Page);

        Assert.Equal(Sorted("0", "10", "20", "30", "40", "50", "10", "20", "30", "5", "10", "15"), Numbers(tokens));
        Assert.Contains("A", tokens);
        Assert.Contains("B", tokens);
    }

    [Fact]
    public void A_percent_stacked_column_chart_runs_from_0_to_100_percent_in_steps_of_20()
    {
        // Every category adds up to 100 %: shares 0 to 1, raw step 0.2, so 0.2: 0 %, 20 % ... 100 %.
        var part = ChartSpace(Bar(Series(0, "A", Quarters, [1, 3, 2]) + Series(1, "B", Quarters, [3, 1, 2]), grouping: "percentStacked"), Axes());

        var tokens = Tokens(Pdf(Docx(part)).Page);

        Assert.Equal(Sorted("0%", "20%", "40%", "60%", "80%", "100%"), Numbers(tokens));
    }

    [Fact]
    public void A_line_chart_labels_its_points_but_not_the_missing_one_and_names_its_series_in_the_legend()
    {
        // Highest value 6: raw step 1.2, so 2; the axis runs 0 to 6 in twos. The second point has no value.
        var part = ChartSpace(LineChart(Series(0, "Courbe", ["a", "b", "c", "d"], [2, null, 6, 4], Line("00B050")), labels: Labels(values: true)), Axes(), legend: "t");

        var tokens = Tokens(Pdf(Docx(part)).Page);

        Assert.Equal(Sorted("0", "2", "4", "6", "2", "6", "4"), Numbers(tokens));
        Assert.All(new[] { "Courbe", "a", "b", "c", "d" }, word => Assert.Contains(word, tokens));
    }

    [Fact]
    public void A_pie_shows_its_percentages_and_names_its_slices_in_the_legend()
    {
        var part = ChartSpace(Pie(Series(0, "Parts", ["Nord", "Sud", "Est"], [1, 1, 2]), Labels(values: false, percentages: true)), legend: "r", afterTitle: "<c:autoTitleDeleted val=\"0\"/>");

        var tokens = Tokens(Pdf(Docx(part)).Page);

        Assert.Equal(Sorted("25%", "25%", "50%"), Numbers(tokens));
        Assert.All(new[] { "Parts", "Nord", "Sud", "Est" }, word => Assert.Contains(word, tokens));
    }

    [Fact]
    public void A_pie_labelled_with_values_and_percentages_shows_both_in_each_slice()
    {
        var part = ChartSpace(Pie(Series(0, "Parts", ["Nord", "Sud", "Est"], [10, 10, 20]), Labels(values: true, percentages: true)));

        var tokens = Tokens(Pdf(Docx(part)).Page);

        Assert.Equal(Sorted("10;", "25%", "10;", "25%", "20;", "50%"), Numbers(tokens));
    }

    [Fact]
    public void A_doughnut_names_its_slices_in_the_legend_and_labels_each_ring()
    {
        var part = ChartSpace(Doughnut(Series(0, "Un", Quarters, [1, 1, 2]) + Series(1, "Deux", Quarters, [2, 1, 1]), labels: Labels(values: true)), legend: "b", title: "Anneaux");

        var tokens = Tokens(Pdf(Docx(part)).Page);

        Assert.Equal(Sorted("1", "1", "2", "2", "1", "1"), Numbers(tokens));
        Assert.All(new[] { "Anneaux", "T1", "T2", "T3" }, word => Assert.Contains(word, tokens));
    }

    [Fact]
    public void A_chart_of_a_type_the_package_does_not_draw_keeps_its_title_and_reports_the_gap()
    {
        var scatter = "<c:scatterChart><c:scatterStyle val=\"lineMarker\"/><c:varyColors val=\"0\"/><c:axId val=\"1\"/><c:axId val=\"2\"/></c:scatterChart>";
        var part = ChartSpace(scatter, title: "Nuage");

        var (page, _, gaps) = Pdf(Docx(part));

        Assert.Contains("chart type not drawn: scatter chart", gaps);
        Assert.Contains("Nuage", Tokens(page));
    }

    [Fact]
    public void A_second_chart_type_of_a_combination_is_reported_while_the_first_is_drawn()
    {
        var part = ChartSpace(Bar(Series(0, "A", Quarters, [1, 2, 3])) + LineChart(Series(1, "B", Quarters, [3, 2, 1])), Axes());

        var (page, _, gaps) = Pdf(Docx(part));

        Assert.Contains("chart type not drawn: line chart combined with another type", gaps);
        Assert.Contains("T1", Tokens(page));
    }

    // ---- Pixels: where the shapes stand -------------------------------------------------------------------------

    // The chart is 300 x 200 pt anchored at (100, 100) on the page; its fill is FFFFCC and every point below is given
    // in chart coordinates, so the page pixel is the point plus (100, 100). With no title and no legend the plot area
    // is the frame inset by 7 pt. The tick labels are 9 pt: the widest, "40", takes about 9.1 pt plus 4 pt before the
    // plot (13.1 pt, give or take the substituted font), a line of the label font about 11 pt: so the plot runs from
    // x = 20.1 to 287.5 (half a label, 5.5 pt, of room at the right end) and from y = 12.5 (value 40) to 178 (value 0,
    // 15 pt above the bottom for the category labels). Three categories of 89.1 pt; with the gap width at 150 % a bar is
    // 89.1 / 2.5 = 35.6 pt wide and centred in its band: the bars' centres stand at x = 64.6, 153.7 and 242.8. A bar's
    // height is its share of the 165.5 pt axis: 10 -> top at y = 136.6, 20 -> 95.3, 40 -> 12.5.
    [Fact]
    public void The_bars_of_a_column_chart_stand_where_their_values_put_them()
    {
        var part = ChartSpace(Bar(Series(0, "Ventes", Quarters, [10, 20, 40], Fill("FF0000"))), Axes(), area: Fill("FFFFCC"));

        var image = Pdf(Docx(part, page: (100, 100))).Image;

        Assert.Equal("FFFFFF", At(image, 50, 50));
        Assert.Equal("FFFFCC", At(image, 100 + 5, 100 + 5));
        Assert.Equal("FF0000", At(image, 100 + 65, 100 + 160));
        Assert.Equal("FFFFCC", At(image, 100 + 65, 100 + 120));
        Assert.Equal("FF0000", At(image, 100 + 154, 100 + 140));
        Assert.Equal("FFFFCC", At(image, 100 + 154, 100 + 60));
        Assert.Equal("FF0000", At(image, 100 + 243, 100 + 95));
        Assert.Equal("FF0000", At(image, 100 + 243, 100 + 30));
        Assert.Equal("FFFFCC", At(image, 100 + 110, 100 + 160));
        Assert.Equal("FFFFCC", At(image, 100 + 200, 100 + 160));
    }

    // Two series side by side: the plot is the same, a group (35.6 pt) holds two bars of 17.8 pt, the first on the left.
    // In the first band the group starts at x = 20.1 + 26.7 = 46.8: the first bar covers 46.8 to 64.6, the second 64.6 to 82.4.
    [Fact]
    public void Clustered_series_stand_side_by_side_in_their_colours()
    {
        var part = ChartSpace(
            Bar(Series(0, "A", Quarters, [40, 40, 40], Fill("FF0000")) + Series(1, "B", Quarters, [40, 40, 40], Fill("0000FF"))),
            Axes(),
            area: Fill("FFFFCC"));

        var image = Pdf(Docx(part, page: (100, 100))).Image;

        Assert.Equal("FF0000", At(image, 100 + 55, 100 + 100));
        Assert.Equal("0000FF", At(image, 100 + 74, 100 + 100));
        Assert.Equal("FF0000", At(image, 100 + 144, 100 + 100));
        Assert.Equal("0000FF", At(image, 100 + 163, 100 + 100));
    }

    // Stacked: B rests on A. The sums are 40 for the first category: the axis runs 0 to 40 (step 10) over 165.5 pt from
    // y = 178. A = 10 takes y 136.6 to 178, B = 30 takes y 12.5 to 136.6.
    [Fact]
    public void Stacked_series_rest_on_each_other()
    {
        var part = ChartSpace(
            Bar(Series(0, "A", Quarters, [10, 10, 10], Fill("FF0000")) + Series(1, "B", Quarters, [30, 30, 30], Fill("0000FF")), grouping: "stacked"),
            Axes(),
            area: Fill("FFFFCC"));

        var image = Pdf(Docx(part, page: (100, 100))).Image;

        Assert.Equal("FF0000", At(image, 100 + 65, 100 + 160));
        Assert.Equal("0000FF", At(image, 100 + 65, 100 + 100));
        Assert.Equal("0000FF", At(image, 100 + 65, 100 + 30));
        Assert.Equal("FFFFCC", At(image, 100 + 65, 100 + 8));
    }

    // Four categories over a plot 271.8 pt wide (the tick labels are one digit, 8.6 pt with their gap: the plot starts at
    // x = 15.6): the points stand at the middle of each band, x = 49.6, 117.5, 185.5 and 253.4. The axis runs 0 to 6 over
    // 165.5 pt from y = 178: the value 6 is at y = 12.5, 4 at y = 67.7. The second point has no value: nothing is drawn
    // around x = 117.5, and the line goes on between the third and fourth points.
    [Fact]
    public void A_line_is_broken_where_a_point_has_no_value_and_keeps_its_markers()
    {
        var part = ChartSpace(LineChart(Series(0, "Courbe", ["a", "b", "c", "d"], [2, null, 6, 4], Line("00B050"))), Axes(), area: Fill("FFFFCC"));

        var image = Pdf(Docx(part, page: (100, 100))).Image;

        Assert.True(Has(image, 100 + 46, 100 + 100, 100 + 54, 100 + 140, "00B050"), "the marker of the first point");
        Assert.False(Has(image, 100 + 112, 100 + 10, 100 + 123, 100 + 176, "00B050"), "nothing around the missing point");
        Assert.True(Has(image, 100 + 182, 100 + 8, 100 + 189, 100 + 18, "00B050"), "the marker of the third point");
        Assert.True(Has(image, 100 + 215, 100 + 36, 100 + 224, 100 + 44, "00B050"), "the line between the third and fourth points");
    }

    // The pie fills the frame inset by 7 pt (no title, no legend, no labels): the box is 286 x 186 pt, its middle (150, 100),
    // the radius 95 % of the half of the shorter side: 88.35 pt. Angles run clockwise from the top: with the first slice
    // at 0 and the values 3 and 1 the first slice takes 0 to 270 degrees (right, bottom and left) and the second the
    // quarter from 270 to 360 (top left). A point at radius r and angle a stands at (150 + r sin a, 100 - r cos a).
    [Fact]
    public void A_two_slice_pie_fills_its_quarter_and_three_quarters_in_the_colours_of_the_theme()
    {
        var part = ChartSpace(Pie(Series(0, "Parts", ["A", "B"], [3, 1])), area: Fill("FFFFCC"));

        var image = Pdf(Docx(part, page: (100, 100))).Image;

        Assert.Equal("4472C4", At(image, 100 + 181, 100 + 69)); // "45 degrees, radius 44"
        Assert.Equal("4472C4", At(image, 100 + 181, 100 + 131)); // "135 degrees"
        Assert.Equal("4472C4", At(image, 100 + 119, 100 + 131)); // "225 degrees"
        Assert.Equal("ED7D31", At(image, 100 + 119, 100 + 69)); // "315 degrees"
        Assert.Equal("ED7D31", At(image, 100 + 150 + 60 * Math.Sin(280 * Math.PI / 180), 100 + 100 - 60 * Math.Cos(280 * Math.PI / 180))); // "280 degrees"
        Assert.Equal("4472C4", At(image, 100 + 150 + 60 * Math.Sin(260 * Math.PI / 180), 100 + 100 - 60 * Math.Cos(260 * Math.PI / 180))); // "260 degrees"
        Assert.Equal("4472C4", At(image, 100 + 150, 100 + 100 + 85)); // "straight down, radius 85 inside the 88.35 edge"
        Assert.Equal("FFFFCC", At(image, 100 + 150, 100 + 100 + 92)); // "straight down, radius 92 outside it"
        Assert.Equal("FFFFCC", At(image, 100 + 10, 100 + 100)); // "far left"
    }

    // The first slice angle turns the pie clockwise: at 90 degrees the three-quarter slice runs from 90 to 360 and the
    // small one takes 0 to 90 (top right).
    [Fact]
    public void The_first_slice_angle_turns_the_pie_clockwise()
    {
        var part = ChartSpace(Pie(Series(0, "Parts", ["A", "B"], [3, 1]), firstSlice: 90), area: Fill("FFFFCC"));

        var image = Pdf(Docx(part, page: (100, 100))).Image;

        Assert.Equal("ED7D31", At(image, 100 + 181, 100 + 69)); // "45 degrees now holds the small slice"
        Assert.Equal("4472C4", At(image, 100 + 181, 100 + 131)); // "135 degrees"
        Assert.Equal("4472C4", At(image, 100 + 119, 100 + 69)); // "315 degrees now holds the big slice"
    }

    // The same frame as the pie, one series with the default hole of 50 %: the hole is 44.2 pt wide in radius, the ring
    // runs from there to 88.35.
    [Fact]
    public void A_doughnut_leaves_its_hole_empty()
    {
        var part = ChartSpace(Doughnut(Series(0, "Parts", ["A", "B"], [3, 1])), area: Fill("FFFFCC"));

        var image = Pdf(Docx(part, page: (100, 100))).Image;

        Assert.Equal("FFFFCC", At(image, 100 + 150, 100 + 100));
        Assert.Equal("FFFFCC", At(image, 100 + 150 + 30, 100 + 100));
        Assert.Equal("4472C4", At(image, 100 + 150 + 66 * Math.Sin(135 * Math.PI / 180), 100 + 100 - 66 * Math.Cos(135 * Math.PI / 180))); // "ring, 135 degrees"
        Assert.Equal("ED7D31", At(image, 100 + 150 + 66 * Math.Sin(315 * Math.PI / 180), 100 + 100 - 66 * Math.Cos(315 * Math.PI / 180))); // "ring, 315 degrees"
    }

    // ---- Nothing to draw ----------------------------------------------------------------------------------------

    [Fact]
    public void A_pie_whose_values_are_all_zero_draws_no_slice()
    {
        var part = ChartSpace(Pie(Series(0, "Parts", ["A", "B"], [0, 0])), title: "Vide", area: Fill("FFFFCC"), afterTitle: "<c:autoTitleDeleted val=\"0\"/>");

        var (page, image, _) = Pdf(Docx(part, page: (100, 100)));

        Assert.Contains("Vide", Tokens(page));
        Assert.Equal("FFFFCC", At(image, 100 + 150, 100 + 120));
        Assert.False(Has(image, 100, 100, 400, 300, "4472C4") || Has(image, 100, 100, 400, 300, "ED7D31"));
    }

    [Fact]
    public void A_column_chart_without_values_draws_its_axes_and_no_bar()
    {
        var part = ChartSpace(Bar(Series(0, "Ventes", Quarters, [null, null, null], Fill("FF0000"))), Axes(), area: Fill("FFFFCC"));

        var (page, image, _) = Pdf(Docx(part, page: (100, 100)));

        Assert.Equal(Sorted("0", "0.2", "0.4", "0.6", "0.8", "1"), Numbers(Tokens(page)));
        Assert.False(Has(image, 100, 100, 400, 300, "FF0000"));
    }

    [Fact]
    public void A_chart_without_series_keeps_its_frame_and_title()
    {
        var part = ChartSpace(Bar(string.Empty), Axes(), title: "Rien", area: Fill("FFFFCC"));

        var (page, image, _) = Pdf(Docx(part, page: (100, 100)));

        Assert.Contains("Rien", Tokens(page));
        Assert.Equal("FFFFCC", At(image, 100 + 150, 100 + 120));
    }

    // ---- Excel: charts over cells -------------------------------------------------------------------------------

    private const string Cells = """<row r="1"><c r="A1" t="inlineStr"><is><t>Mois</t></is></c><c r="B1" t="inlineStr"><is><t>Ventes</t></is></c></row><row r="2"><c r="A2" t="inlineStr"><is><t>T1</t></is></c><c r="B2"><v>10</v></c></row>""";

    [Fact]
    public void A_chart_over_cells_is_drawn_with_its_text_in_the_printed_sheet()
    {
        var part = ChartSpace(Bar(Series(0, "Ventes", Quarters, [10, 20, 40])), Axes(), title: "Résultats", legend: "b");

        var result = ExcelToPdf.Convert(XlsxWorkbook.Load(Xlsx(Cells, part, TwoCell(3, 0, 9, 15))), new ExcelPdfOptions { SheetTitles = false });
        var tokens = Tokens(PdfDocument.Open(result.Pdf).GetPage(1));

        // The ticks 0 to 40, and the 10 of cell B2.
        Assert.Equal(Sorted("0", "10", "10", "20", "30", "40"), Numbers(tokens));
        Assert.All(new[] { "Résultats", "Ventes", "T1", "T2", "T3" }, word => Assert.Contains(word, tokens));
    }

    [Fact]
    public void The_printed_range_grows_to_hold_a_chart_placed_beyond_the_data()
    {
        // The data fills A1:B2. The chart runs from column C, row 26 to column I, row 39 (1-based, ends included): the
        // table has 39 rows of 9 columns instead of 2 rows of 2.
        var part = ChartSpace(Pie(Series(0, "Parts", Quarters, [1, 2, 3])), area: Fill("FFFFCC"));
        var workbook = XlsxWorkbook.Load(Xlsx(Cells, part, TwoCell(2, 25, 8, 38)));
        var options = new ExcelPdfOptions { SheetTitles = false };

        var table = ExcelToPdf.ToWord(workbook, options).Blocks.OfType<WordTable>().Single();
        var image = Render(ExcelToPdf.Convert(workbook, options).Pdf).Image;

        Assert.Equal(39, table.Rows.Count);
        Assert.Equal(9, table.Columns.Count);
        var (left, top, right, bottom) = Extent(image, "FFFFCC");
        // The chart area is 288 x 195 pt; its 1 pt outline (D9D9D9) is not part of the fill.
        Assert.Equal((6 * 48d) - 2, right - left + 1d, 1.5d);
        Assert.Equal((13 * 15d) - 2, bottom - top + 1d, 1.5d);
    }

    [Fact]
    public void A_chart_wider_than_a_portrait_page_makes_the_sheet_landscape_and_a_narrow_one_does_not()
    {
        // Eleven default columns of 48 pt are 528 pt, more than the 453 pt of an A4 portrait page; five are 240 pt.
        var part = ChartSpace(Pie(Series(0, "Parts", Quarters, [1, 2, 3])));

        var wide = PdfDocument.Open(ExcelToPdf.Convert(XlsxWorkbook.Load(Xlsx(Cells, part, TwoCell(0, 4, 10, 14))), new ExcelPdfOptions { SheetTitles = false }).Pdf).GetPage(1);
        var narrow = PdfDocument.Open(ExcelToPdf.Convert(XlsxWorkbook.Load(Xlsx(Cells, part, TwoCell(0, 4, 4, 14))), new ExcelPdfOptions { SheetTitles = false }).Pdf).GetPage(1);

        Assert.True(wide.Width > wide.Height, "landscape");
        Assert.True(narrow.Width < narrow.Height, "portrait");
    }

    [Fact]
    public void A_chart_anchored_in_a_merged_cell_stands_where_its_anchor_puts_it()
    {
        // The chart starts at column B, row 3, inside the merged cell A1:C3: it is placed from the merged cell's corner, two
        // rows down, so it stands exactly where the same chart does over an unmerged sheet.
        var part = ChartSpace(Pie(Series(0, "Parts", Quarters, [1, 2, 3])), area: Fill("FFFFCC"));
        var anchor = TwoCell(1, 2, 4, 8);
        var data = Cells + """<row r="12"><c r="A12"><v>1</v></c></row>""";
        var merged = Xlsx(data, part, anchor, afterData: """<mergeCells count="1"><mergeCell ref="A1:C3"/></mergeCells>""");
        var plain = Xlsx(data, part, anchor);

        var withMerge = Extent(Render(ExcelToPdf.Convert(XlsxWorkbook.Load(merged), new ExcelPdfOptions { SheetTitles = false }).Pdf).Image, "FFFFCC");
        var without = Extent(Render(ExcelToPdf.Convert(XlsxWorkbook.Load(plain), new ExcelPdfOptions { SheetTitles = false }).Pdf).Image, "FFFFCC");

        Assert.Equal((double)without.Left, withMerge.Left, 1d);
        Assert.Equal((double)without.Top, withMerge.Top, 2d);
        Assert.Equal((3 * 48d) - 2, withMerge.Right - withMerge.Left + 1d, 1.5d);
        Assert.Equal((6 * 15d) - 2, withMerge.Bottom - withMerge.Top + 1d, 1.5d);
    }

    [Fact]
    public void A_chart_of_an_unsupported_type_in_a_workbook_reports_its_gap_in_the_pdf_conversion()
    {
        var scatter = "<c:scatterChart><c:scatterStyle val=\"lineMarker\"/><c:varyColors val=\"0\"/><c:axId val=\"1\"/><c:axId val=\"2\"/></c:scatterChart>";

        var result = ExcelToPdf.Convert(XlsxWorkbook.Load(Xlsx(Cells, ChartSpace(scatter, title: "Nuage"), TwoCell(3, 0, 9, 15))));

        Assert.Contains("chart type not drawn: scatter chart", result.Gaps);
    }

    // ---- Helpers ------------------------------------------------------------------------------------------------

    private static (PdfPage Page, RasterImage Image, IReadOnlyList<string> Gaps) Pdf(byte[] docx)
    {
        var result = WordToPdf.Convert(WordDocument.Load(docx));
        var (page, image) = Render(result.Pdf);
        return (page, image, result.Gaps);
    }

    private static (PdfPage Page, RasterImage Image) Render(byte[] pdf)
    {
        var page = PdfDocument.Open(pdf).GetPage(1);
        return (page, PdfRenderer.Render(page, new PdfRenderOptions { Dpi = 72 }).Image);
    }

    // The words of a page: whitespace separated tokens, in reading order as the text extractor gives them.
    private static List<string> Tokens(PdfPage page) => [.. page.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)];

    // The tokens starting with a digit or a minus sign: the numbers a chart shows, their units and separators attached.
    private static List<string> Numbers(IEnumerable<string> tokens) => Sorted(tokens.Where(t => char.IsDigit(t[0]) || t[0] == '-').ToArray());

    private static List<string> Sorted(params string[] values) => [.. values.OrderBy(v => v, StringComparer.Ordinal)];

    private sealed record Word(string Text, double Left, double Right, double Bottom, double Top);

    // The horizontal runs of letters of a page (letters whose gap is below 1.5 pt, on one baseline), rotated text aside.
    private static List<Word> Words(PdfPage page)
    {
        var words = new List<Word>();
        foreach (var line in page.Letters.Where(l => l.Rotation == 0 && l.Value.Trim().Length > 0).GroupBy(l => Math.Round(l.Y, 0)))
        {
            var run = new List<PdfLetter>();
            foreach (var letter in line.OrderBy(l => l.X))
            {
                if (run.Count > 0 && letter.X - (run[^1].X + run[^1].Width) > 1.5)
                {
                    words.Add(Join(run));
                    run = [];
                }

                run.Add(letter);
            }

            words.Add(Join(run));
        }

        return words;
    }

    private static Word Join(List<PdfLetter> run) =>
        new(string.Concat(run.Select(l => l.Value)), run.Min(l => l.BoundingBox.Left), run.Max(l => l.BoundingBox.Right), run.Min(l => l.BoundingBox.Bottom), run.Max(l => l.BoundingBox.Top));

    private static Word Of(List<Word> words, string text) => words.Single(w => w.Text == text);

    // The colour of the pixel holding a point (72 dpi), as RRGGBB.
    private static string At(RasterImage image, double x, double y)
    {
        var (r, g, b, _) = image.GetRgba((int)Math.Floor(x), (int)Math.Floor(y));
        return string.Create(CultureInfo.InvariantCulture, $"{r:X2}{g:X2}{b:X2}");
    }

    // True when a pixel of exactly the colour lies in the rectangle (ends included).
    private static bool Has(RasterImage image, int left, int top, int right, int bottom, string color)
    {
        for (var y = top; y <= bottom; y++)
        {
            for (var x = left; x <= right; x++)
            {
                if (At(image, x, y) == color)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // The pixels (left, top, right, bottom, ends included) that bound all the pixels of a colour.
    private static (int Left, int Top, int Right, int Bottom) Extent(RasterImage image, string color)
    {
        var (left, top, right, bottom) = (int.MaxValue, int.MaxValue, -1, -1);
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (At(image, x, y) == color)
                {
                    (left, top, right, bottom) = (Math.Min(left, x), Math.Min(top, y), Math.Max(right, x), Math.Max(bottom, y));
                }
            }
        }

        Assert.True(right >= 0, $"no pixel of colour {color}");
        return (left, top, right, bottom);
    }
}
