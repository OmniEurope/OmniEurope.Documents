// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Conversion.ExcelHtml;
using OmniEurope.Documents.Csv;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Html;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// Excel to HTML on workbooks written by the package, saved and loaded again (the injection case is converted
/// as built, since its colour cannot be saved). The displayed values are checked
/// against the CSV export of the same sheets (each table, read back by the package's HTML parser and with its
/// merged cells spread over the grid, gives the CSV records), styles against the rules of the embedded style
/// sheet, and workbook text meant to inject markup must come back as plain text.
/// </summary>
public sealed class ExcelToHtmlTests
{
    private static readonly CsvReaderOptions CsvOptions = new() { HasHeader = false, Delimiter = ',' };

    [Fact]
    public void Gives_the_texts_of_the_csv_export()
    {
        var workbook = Reloaded(Sample());
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var document = HtmlParser.ParseDocument(ExcelToHtml.Convert(workbook, new ExcelHtmlOptions { Culture = culture }));

        var tables = document.QuerySelectorAll("table." + ExcelToHtml.TableClass);
        Assert.Equal(2, tables.Count);
        for (var i = 0; i < tables.Count; i++)
        {
            var csv = CsvReader.ReadAll(XlsxCsvConverter.ToCsv(workbook.Worksheets[i], culture: culture), CsvOptions);
            Assert.Equal(csv, Grid(tables[i]));
        }

        Assert.Contains("1" + culture.NumberFormat.NumberGroupSeparator + "234,57", Grid(tables[0]).SelectMany(r => r));
        Assert.Contains("3 1/7", Grid(tables[0]).SelectMany(r => r));
        Assert.Equal(["Ventes", "Vide"], document.QuerySelectorAll("h2").Select(h => h.TextContent));
    }

    [Fact]
    public void Spans_merged_cells_and_leaves_out_the_cells_they_cover()
    {
        var html = HtmlParser.ParseDocument(ExcelToHtml.Convert(Reloaded(Sample())));
        var rows = html.QuerySelectorAll("table." + ExcelToHtml.TableClass)[0].QuerySelectorAll("tr");

        var title = rows[0].QuerySelectorAll("td")[0];
        Assert.Equal(("3", null, "Rapport"), (title.GetAttribute("colspan"), title.GetAttribute("rowspan"), title.TextContent));
        Assert.Single(rows[0].QuerySelectorAll("td"));
        var block = rows[3].QuerySelectorAll("td")[0];
        Assert.Equal(("2", "2", "Bloc"), (block.GetAttribute("colspan"), block.GetAttribute("rowspan"), block.TextContent));
        Assert.Equal(2, rows[3].QuerySelectorAll("td").Count);
        Assert.Single(rows[4].QuerySelectorAll("td"));
    }

    [Fact]
    public void Gives_each_column_its_width_in_excel_pixels()
    {
        var page = ExcelToHtml.Convert(Reloaded(Sample()));
        var document = HtmlParser.ParseDocument(page);
        var css = document.Head.QuerySelector("style")!.TextContent;
        var table = document.QuerySelectorAll("table." + ExcelToHtml.TableClass)[0];

        // Widths 20 and 5 characters, then the default 8.43: 7w + 5 pixels, rounded.
        var widths = table.QuerySelectorAll("col").Select(c => Rule(css, "." + c.GetAttribute("class"))).ToList();
        Assert.Equal(["width:145px", "width:40px", "width:64px"], widths);
        Assert.Equal("width:249px", Rule(css, "." + table.ClassList[1]));
    }

    [Fact]
    public void Carries_fonts_colours_fills_alignment_wrapping_and_borders()
    {
        var document = HtmlParser.ParseDocument(ExcelToHtml.Convert(Reloaded(Sample())));
        var css = document.Head.QuerySelector("style")!.TextContent;
        var rows = document.QuerySelectorAll("table." + ExcelToHtml.TableClass)[0].QuerySelectorAll("tr");
        string Declarations(int row, int cell) => Rule(css, "table.omni-sheet td." + rows[row].QuerySelectorAll("td")[cell].GetAttribute("class"));

        var title = Declarations(0, 0);
        Assert.Contains("font-weight:bold;", title, StringComparison.Ordinal);
        Assert.Contains("font-style:italic;", title, StringComparison.Ordinal);
        Assert.Contains("color:#C00000;", title, StringComparison.Ordinal);
        Assert.Contains("background-color:#FFF2CC;", title, StringComparison.Ordinal);
        Assert.Contains("text-align:center;", title, StringComparison.Ordinal);
        Assert.Contains("font-family:'Arial';font-size:14pt;", title, StringComparison.Ordinal);
        Assert.Contains("text-align:right;", Declarations(1, 1), StringComparison.Ordinal);
        Assert.Contains("text-align:left;", Declarations(1, 0), StringComparison.Ordinal);
        Assert.Contains("text-align:center;", Declarations(2, 2), StringComparison.Ordinal);
        var bordered = Declarations(2, 0);
        Assert.Contains("border:1px solid #1F4E79;", bordered, StringComparison.Ordinal);
        Assert.Contains("text-decoration:underline line-through;", bordered, StringComparison.Ordinal);
        Assert.Contains("vertical-align:top;", bordered, StringComparison.Ordinal);
        Assert.Contains("white-space:pre-wrap;", bordered, StringComparison.Ordinal);
        Assert.Contains("vertical-align:middle;", Declarations(2, 1), StringComparison.Ordinal);
        Assert.Contains("border:1px solid #000000;", Declarations(2, 1), StringComparison.Ordinal);
        Assert.Contains("text-align:justify;", Declarations(5, 0), StringComparison.Ordinal);
        Assert.Contains("border:1px solid #D9D9D9", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Encodes_workbook_text_so_nothing_can_inject_markup_or_style_rules()
    {
        var workbook = new XlsxWorkbook { Title = "</title><script>alert(1)</script>" };
        var sheet = workbook.AddWorksheet("<i>Feuille &amp;");
        const string Script = "<script>alert(1)</script>";
        const string Breakout = "</style></td><img src=x onerror=alert(1)>&amp;\"'";
        sheet.Cell("A1").Value = Script;
        sheet.Cell("A2").Value = Breakout;
        sheet.Cell("A1").Style = XlsxStyle.Default with { FontName = "Arial'}</style><script>alert(2)</script>", FontColor = "red;}td{x" };

        // Converted as built: a colour that is not RRGGBB cannot be saved, but a workbook in memory may hold it.
        var page = ExcelToHtml.Convert(workbook);
        var document = HtmlParser.ParseDocument(page);

        Assert.Empty(document.QuerySelectorAll("script, img, i"));
        Assert.Equal("</title><script>alert(1)</script>", document.Title);
        Assert.Equal("<i>Feuille &amp;", document.QuerySelector("h2")!.TextContent);
        Assert.Equal([[Script], [Breakout]], Grid(document.QuerySelector("table")!));
        var css = document.Head.QuerySelector("style")!.TextContent;
        Assert.DoesNotContain("<", css, StringComparison.Ordinal);
        Assert.Contains("font-family:'Arialstylescriptalert2script';", css, StringComparison.Ordinal);
        Assert.DoesNotContain("red", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_its_tables_through_the_default_sanitiser()
    {
        var document = HtmlParser.ParseDocument(ExcelToHtml.Convert(Reloaded(Sample())));
        var body = document.Body.InnerHtml;

        Assert.Equal(body, new HtmlSanitizer().Sanitize(body));
    }

    [Fact]
    public void Writes_an_empty_sheet_as_its_title_and_follows_the_options()
    {
        var workbook = new XlsxWorkbook();
        workbook.AddWorksheet("Seule");

        var page = ExcelToHtml.Convert(workbook.ToArray(), new ExcelHtmlOptions { SheetTitles = false, Gridlines = false });
        var document = HtmlParser.ParseDocument(page);

        Assert.Empty(document.QuerySelectorAll("table, h2"));
        Assert.Equal("Seule", document.Title);
        Assert.Contains("border:none", page, StringComparison.Ordinal);
        Assert.StartsWith("<!DOCTYPE html>", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_used_range_beyond_the_cell_bound()
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("Loin");
        sheet.Cell("A1").Value = 1;
        sheet.Cell("XFD1048576").Value = 2;

        Assert.Throws<DocumentFormatException>(() => ExcelToHtml.Convert(workbook));
        Assert.Throws<DocumentFormatException>(() => ExcelToHtml.Convert(Sample(), new ExcelHtmlOptions { MaxCells = 10 }));
    }

    [Fact]
    public void Clips_merges_to_the_used_range_and_ignores_overlapping_ones()
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("Fusions");
        sheet.Cell("B2").Value = "a";
        sheet.Cell("C3").Value = "b";
        sheet.Merge(XlsxRange.Parse("A1:B2"));
        sheet.Merge(XlsxRange.Parse("C2:D9"));
        sheet.Merge(XlsxRange.Parse("E1:F1"));
        sheet.AddLoadedMerge(XlsxRange.Parse("B3:C3"));

        var table = HtmlParser.ParseDocument(ExcelToHtml.Convert(workbook)).QuerySelector("table")!;

        // The used range is B2:C3; A1:B2 is left with B2, C2:D9 with C2:C3, E1:F1 is outside, and B3:C3 (as a
        // damaged file may hold) overlaps C2:C3.
        var cells = table.QuerySelectorAll("td");
        Assert.Equal(3, cells.Count);
        Assert.Equal("2", cells[1].GetAttribute("rowspan"));
        Assert.Null(cells[0].GetAttribute("rowspan"));
        Assert.All(cells, c => Assert.Null(c.GetAttribute("colspan")));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("#1f4e79", "1F4E79")]
    [InlineData("FF00B050", "00B050")]
    [InlineData("red", null)]
    public void Accepts_only_rrggbb_colours(string? color, string? expected)
    {
        Assert.Equal(expected, ExcelHtmlCss.Color(color));
    }

    [Fact]
    public void Leaves_out_a_font_name_with_nothing_left_after_reduction()
    {
        Assert.Equal(string.Empty, ExcelHtmlCss.FontName("'}<>;"));
        Assert.Equal(string.Empty, ExcelHtmlCss.FontName(null));
        var css = new ExcelHtmlCss(gridlines: true);
        css.CellClass(XlsxStyle.Default with { FontName = "{}" }, XlsxValueType.Text);
        Assert.DoesNotContain("font-family:'", css.StyleSheet(), StringComparison.Ordinal);
    }

    private static XlsxWorkbook Sample()
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("Ventes");
        sheet.SetColumnWidth(1, 20);
        sheet.SetColumnWidth(2, 5);
        sheet.Cell("A1").Value = "Rapport";
        sheet.Cell("A1").Style = XlsxStyle.Default with
        {
            FontName = "Arial", FontSize = 14, Bold = true, Italic = true, FontColor = "C00000", FillColor = "FFF2CC",
            HorizontalAlignment = XlsxHorizontalAlignment.Center,
        };
        sheet.Merge(XlsxRange.Parse("A1:C1"));
        sheet.Cell("A2").Value = "Total";
        sheet.Cell("B2").Value = 1234.567;
        sheet.Cell("B2").Style = XlsxStyle.Default with { NumberFormat = "#,##0.00" };
        sheet.Cell("C2").Value = Math.PI;
        sheet.Cell("C2").Style = XlsxStyle.Default with { NumberFormat = "# ?/?" };
        sheet.Cell("A3").Value = "Bordé, souligné\net barré";
        sheet.Cell("A3").Style = XlsxStyle.Default with
        {
            Border = true, BorderColor = "1F4E79", Underline = true, Strike = true, WrapText = true,
            VerticalAlignment = XlsxVerticalAlignment.Top,
        };
        sheet.Cell("B3").Value = new DateTime(2026, 10, 10);
        sheet.Cell("B3").Style = XlsxStyle.Default with { NumberFormat = "yyyy-mm-dd", Border = true, VerticalAlignment = XlsxVerticalAlignment.Center };
        sheet.Cell("C3").Value = true;
        sheet.Cell("A4").Value = "Bloc";
        sheet.Merge(XlsxRange.Parse("A4:B5"));
        sheet.Cell("C4").Value = 0.25;
        sheet.Cell("C4").Style = XlsxStyle.Default with { NumberFormat = "0%" };
        sheet.Cell("C5").Formula = "C4*2";
        sheet.Cell("A6").Value = "Justifié";
        sheet.Cell("A6").Style = XlsxStyle.Default with { HorizontalAlignment = XlsxHorizontalAlignment.Justify };
        sheet.Cell("C6").Value = "\"guillemets\", virgule";
        workbook.AddWorksheet("Vide").Cell("B2").Value = "seul";
        workbook.Recalculate();
        return workbook;
    }

    private static XlsxWorkbook Reloaded(XlsxWorkbook workbook) => XlsxWorkbook.Load(workbook.ToArray());

    // The table as rows of texts, a merged cell's text in its first position and the positions it covers empty.
    private static List<string[]> Grid(HtmlElement table)
    {
        var grid = new Dictionary<(int Row, int Column), string>();
        var rows = table.QuerySelectorAll("tr");
        var width = 0;
        for (var r = 0; r < rows.Count; r++)
        {
            var c = 0;
            foreach (var cell in rows[r].QuerySelectorAll("td"))
            {
                while (grid.ContainsKey((r, c)))
                {
                    c++;
                }

                var (rowSpan, columnSpan) = (Span(cell, "rowspan"), Span(cell, "colspan"));
                for (var dr = 0; dr < rowSpan; dr++)
                {
                    for (var dc = 0; dc < columnSpan; dc++)
                    {
                        grid[(r + dr, c + dc)] = dr == 0 && dc == 0 ? cell.TextContent : string.Empty;
                    }
                }

                c += columnSpan;
                width = Math.Max(width, c);
            }
        }

        return Enumerable.Range(0, rows.Count).Select(r => Enumerable.Range(0, width).Select(c => grid.GetValueOrDefault((r, c), string.Empty)).ToArray()).ToList();
    }

    private static int Span(HtmlElement cell, string attribute) =>
        int.TryParse(cell.GetAttribute(attribute), CultureInfo.InvariantCulture, out var span) ? span : 1;

    private static string Rule(string css, string selector)
    {
        var line = css.Split('\n').Single(l => l.StartsWith(selector + "{", StringComparison.Ordinal));
        return line[(selector.Length + 1)..^1];
    }
}
