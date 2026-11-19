// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// A merged range as large as the sheet costs what the cells printed cost, not what its area costs: the merges are
/// kept as ranges and listed on the printed rows only, and a merge reaching past the printed range spans the
/// printed columns it covers, no more.
/// </summary>
public sealed class ExcelMergeScaleTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(2);

    [Theory]
    [InlineData("A1:XFD1048576")]
    [InlineData("A1:Z100000")]
    public void Excel_to_pdf_with_a_huge_merge_spans_the_printed_cells_only(string merge)
    {
        var workbook = XlsxWorkbook.Load(Workbook(merge, "C3").ToArray());
        var options = new ExcelPdfOptions { SheetTitles = false };

        var (pdf, time) = ThreadCpuTime.Measure(() => ExcelToPdf.Convert(workbook, options, TestContext.Current.CancellationToken));

        Assert.True(pdf.PageCount >= 1);
        Assert.True(time < Limit, $"took {time}");
        var table = ExcelToPdf.ToWord(workbook, options, TestContext.Current.CancellationToken).Blocks.OfType<WordTable>().Single();
        Assert.Equal(3, table.Columns.Count);
        Assert.Equal(3, table.Rows.Count);
        Assert.All(table.Rows, row => Assert.Equal(3, Assert.Single(row.Cells).Properties.GridSpan));
        Assert.Equal(WordVerticalMerge.Restart, table.Rows[0].Cells[0].Properties.VerticalMerge);
        Assert.Equal(WordVerticalMerge.Continue, table.Rows[2].Cells[0].Properties.VerticalMerge);
    }

    [Theory]
    [InlineData("A1:XFD1048576")]
    [InlineData("A1:Z100000")]
    public void Excel_to_pdf_with_a_huge_merge_and_a_tall_printed_range_costs_the_printed_rows(string merge)
    {
        var workbook = XlsxWorkbook.Load(Workbook(merge, "E5000").ToArray());
        var options = new ExcelPdfOptions { SheetTitles = false };

        var (pdf, time) = ThreadCpuTime.Measure(() => ExcelToPdf.Convert(workbook, options, TestContext.Current.CancellationToken));

        Assert.True(pdf.PageCount >= 1);
        Assert.True(time < Limit, $"took {time}");
    }

    [Theory]
    [InlineData("A1:XFD1048576")]
    [InlineData("A1:Z100000")]
    public void Merges_are_listed_on_the_printed_rows_only(string merge)
    {
        var sheet = XlsxWorkbook.Load(Workbook(merge, "C3").ToArray()).Worksheets[0];

        var (rows, time) = ThreadCpuTime.Measure(() => ExcelRowLayout.MergesByRow(sheet, sheet.UsedRange!.Value));

        Assert.Equal([1, 2, 3], rows.Keys.Order());
        Assert.Equal([XlsxRange.Parse(merge)], rows[2]);
        Assert.True(time < TimeSpan.FromMilliseconds(500), $"took {time}");
    }

    [Theory]
    [InlineData("A1:XFD1048576")]
    [InlineData("A1:Z100000")]
    public void Excel_to_html_with_a_huge_merge_spans_the_printed_cells_only(string merge)
    {
        var workbook = XlsxWorkbook.Load(Workbook(merge, "C3").ToArray());

        var (html, time) = ThreadCpuTime.Measure(() => ExcelToHtml.Convert(workbook));

        Assert.True(time < Limit, $"took {time}");
        Assert.Equal(1, html.Split("<td").Length - 1);
        Assert.Contains("colspan=\"3\"", html, StringComparison.Ordinal);
        Assert.Contains("rowspan=\"3\"", html, StringComparison.Ordinal);
        Assert.Contains("Fusion", html, StringComparison.Ordinal);
    }

    // A merge over the whole sheet (or a huge part of it) holding values from A1 to the given corner.
    private static XlsxWorkbook Workbook(string merge, string corner)
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("Feuille");
        sheet.Cell("A1").Value = "Fusion";
        sheet.Cell(corner).Value = "fin";
        sheet.Merge(XlsxRange.Parse(merge));
        return workbook;
    }
}
