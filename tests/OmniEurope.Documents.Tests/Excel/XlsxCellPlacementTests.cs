// SPDX-License-Identifier: EUPL-1.2
using System.IO.Compression;
using System.Text;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Excel.Editing;

namespace OmniEurope.Documents.Tests.Excel;

/// <summary>
/// A row or a cell whose <c>r</c> attribute names no position of a worksheet makes the workbook malformed, whichever
/// public entry point reads it; a row or cell without the attribute keeps its position after the previous one.
/// </summary>
public sealed class XlsxCellPlacementTests
{
    private const string Sheet = "Données";

    [Theory]
    [InlineData("A99999999")]
    [InlineData("XFE1")]
    [InlineData("ZZZZ1")]
    [InlineData("A0")]
    [InlineData("A1048577")]
    [InlineData("1A")]
    [InlineData("A")]
    [InlineData("A-1")]
    [InlineData("")]
    public void A_cell_with_an_impossible_reference_refuses_the_workbook_naming_sheet_and_reference(string reference)
    {
        var package = XlsxFactory.Rich($"""<row r="1"><c r="{reference}"><v>1</v></c></row>""");

        var exception = Assert.Throws<DocumentFormatException>(() => XlsxWorkbook.Load(package));

        Assert.Contains($"'{reference}'", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"'{Sheet}'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1048577")]
    [InlineData("99999999")]
    [InlineData("-1")]
    [InlineData("x")]
    [InlineData("")]
    public void A_row_with_an_impossible_number_refuses_the_workbook_naming_sheet_and_row(string row)
    {
        var package = XlsxFactory.Rich($"""<row r="{row}"><c><v>1</v></c></row>""");

        var exception = Assert.Throws<DocumentFormatException>(() => XlsxWorkbook.Load(package));

        Assert.Contains($"'{row}'", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"'{Sheet}'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_row_and_a_cell_without_reference_keep_their_position_after_the_previous_one()
    {
        var workbook = XlsxWorkbook.Load(XlsxFactory.Rich("""<row r="4"><c><v>5</v></c><c><v>6</v></c></row><row><c><v>7</v></c></row>"""));

        var sheet = workbook.Worksheet(Sheet);

        Assert.Equal((5.0, 6.0, 7.0), ((double)sheet.Cell("A4").Value!, (double)sheet.Cell("B4").Value!, (double)sheet.Cell("A5").Value!));
    }

    [Fact]
    public void Every_entry_point_that_reads_a_workbook_refuses_an_impossible_reference_the_same_way()
    {
        var package = XlsxFactory.Rich("""<row r="1"><c r="A99999999"><v>1</v></c></row>""");

        Assert.Throws<DocumentFormatException>(() => XlsxWorkbook.Load(package));
        Assert.Throws<DocumentFormatException>(() => XlsxWorkbook.Load(new MemoryStream(package)));
        Assert.Throws<DocumentFormatException>(() => ExcelToHtml.Convert(package));
        Assert.Throws<DocumentFormatException>(() => ExcelToHtml.ConvertWithStyleSheet(package));
        Assert.Throws<DocumentFormatException>(() => ExcelToPdf.Convert(package, null, TestContext.Current.CancellationToken));
        Assert.Throws<DocumentFormatException>(() => XlsxEditor.Open(package).Sheet(Sheet).GetValue("A1"));
        Assert.Throws<DocumentFormatException>(() => XlsxEditor.Open(new MemoryStream(package)).Sheet(Sheet).GetFormula("A1"));
    }

    [Fact]
    public void The_editor_refuses_an_impossible_row_and_keeps_refusing_it()
    {
        var sheet = XlsxEditor.Open(XlsxFactory.Rich("""<row r="x"><c><v>1</v></c></row>""")).Sheet(Sheet);

        Assert.Throws<DocumentFormatException>(() => sheet.GetValue("A1"));
        Assert.Throws<DocumentFormatException>(() => sheet.SetValue("B2", 1.0));
        Assert.Throws<DocumentFormatException>(() => sheet.GetValue("A1"));
    }

    [Fact]
    public void The_editor_places_a_row_and_a_cell_without_reference_after_the_previous_one()
    {
        var sheet = XlsxEditor.Open(XlsxFactory.Rich("""<row r="4"><c><v>5</v></c><c><v>6</v></c></row><row><c><v>7</v></c></row>""")).Sheet(Sheet);

        Assert.Equal((5.0, 6.0, 7.0), ((double)sheet.GetValue("A4")!, (double)sheet.GetValue("B4")!, (double)sheet.GetValue("A5")!));
    }

    [Fact]
    public void Raw_bytes_that_are_no_workbook_throw_the_same_exception_from_every_entry_point()
    {
        var notZip = Encoding.UTF8.GetBytes("this is not a zip package");
        var noWorkbook = Package(("hello.txt", "text"));
        var truncatedWorkbook = Replace(XlsxFactory.Rich(), "xl/workbook.xml", xml => xml[..(xml.Length / 2)]);

        foreach (var bytes in new[] { notZip, noWorkbook, truncatedWorkbook })
        {
            Assert.Throws<DocumentFormatException>(() => XlsxWorkbook.Load(bytes));
            Assert.Throws<DocumentFormatException>(() => XlsxEditor.Open(bytes));
            Assert.Throws<DocumentFormatException>(() => ExcelToHtml.Convert(bytes));
            Assert.Throws<DocumentFormatException>(() => ExcelToHtml.ConvertWithStyleSheet(bytes));
            Assert.Throws<DocumentFormatException>(() => ExcelToPdf.Convert(bytes, null, TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public void A_truncated_sheet_part_throws_the_same_exception_from_every_entry_point()
    {
        var truncated = Replace(XlsxFactory.Rich(), "xl/worksheets/sheet1.xml", xml => xml[..(xml.Length / 2)]);

        Assert.Throws<DocumentFormatException>(() => XlsxWorkbook.Load(truncated));
        Assert.Throws<DocumentFormatException>(() => ExcelToHtml.Convert(truncated));
        Assert.Throws<DocumentFormatException>(() => ExcelToHtml.ConvertWithStyleSheet(truncated));
        Assert.Throws<DocumentFormatException>(() => ExcelToPdf.Convert(truncated, null, TestContext.Current.CancellationToken));
        Assert.Throws<DocumentFormatException>(() => XlsxEditor.Open(truncated).Sheet(Sheet).GetValue("A1"));
    }

    private static byte[] Package(params (string Name, string Content)[] parts)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in parts)
            {
                using var target = zip.CreateEntry(name).Open();
                target.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        return output.ToArray();
    }

    private static byte[] Replace(byte[] package, string part, Func<string, string> change)
    {
        using var input = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in input.Entries)
            {
                using var source = entry.Open();
                using var buffer = new MemoryStream();
                source.CopyTo(buffer);
                var bytes = entry.FullName == part ? Encoding.UTF8.GetBytes(change(Encoding.UTF8.GetString(buffer.ToArray()))) : buffer.ToArray();
                using var target = zip.CreateEntry(entry.FullName).Open();
                target.Write(bytes);
            }
        }

        return output.ToArray();
    }
}
