// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.IO.Compression;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Tests.Excel;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>The fidelity diagnostics of a workbook (a drawing that could not be read) reach the HTML result as they reach the PDF one.</summary>
public sealed class ExcelHtmlGapTests
{
    [Fact]
    public void The_html_result_keeps_the_gaps_of_the_workbook()
    {
        var xlsx = UnreadableDrawing();
        var workbook = XlsxWorkbook.Load(xlsx);
        var expected = Assert.Single(workbook.GapDetails.Distinct());
        Assert.Equal("ExcelDrawingUnreadable", expected.Code);
        var french = CultureInfo.GetCultureInfo("fr");

        foreach (var result in new[] { ExcelToHtml.ConvertWithStyleSheet(workbook), ExcelToHtml.ConvertWithStyleSheet(xlsx) })
        {
            Assert.Equal([expected.Message], result.Gaps);
            var detail = Assert.Single(result.GapDetails);
            Assert.Equal(expected.Code, detail.Code);
            Assert.Equal(expected.Message, detail.Message);
            Assert.Equal(expected.Describe(french), detail.Describe(french));
        }
    }

    [Fact]
    public void A_clean_workbook_has_no_gap_and_the_two_parameter_result_still_deconstructs()
    {
        var result = ExcelToHtml.ConvertWithStyleSheet(XlsxFactory.Rich());
        Assert.Empty(result.Gaps);
        Assert.Empty(result.GapDetails);

        var (html, css) = new ExcelHtmlResult("<p/>", "a{}");
        Assert.Equal(("<p/>", "a{}"), (html, css));
    }

    private static byte[] UnreadableDrawing()
    {
        using var stream = new MemoryStream(XlsxFactory.Rich());
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            zip.GetEntry("xl/drawings/drawing1.xml")!.Delete();
            using var output = zip.CreateEntry("xl/drawings/drawing1.xml").Open();
            output.Write("<?xml version=\"1.0\"?><xdr:wsDr xmlns:xdr=\"http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing\"><xdr:twoCellAnchor>"u8);
        }

        return stream.ToArray();
    }
}
