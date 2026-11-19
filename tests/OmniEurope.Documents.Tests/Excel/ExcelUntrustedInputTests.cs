// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.IO.Compression;
using System.Text;
using OmniEurope.Documents.Csv;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Tests.Excel.Formulas;

namespace OmniEurope.Documents.Tests.Excel;

/// <summary>
/// What a file or a text declares and a sheet cannot hold: a number that is not finite, a cell or a range outside the
/// sheet, a sheet name that is not valid. Each one ends as text, as an error value or as a format error, never as a
/// workbook that cannot be saved or as an argument exception.
/// </summary>
public sealed class ExcelUntrustedInputTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e999")]
    public void Csv_text_that_is_not_a_finite_number_stays_text(string text)
    {
        using var csv = CsvReader.Open(new StringReader("Amount\n" + text + "\n12.5\n"));

        var sheet = XlsxCsvConverter.FromCsv(csv, "Import", Invariant).Worksheet("Import");

        Assert.Equal((XlsxValueType.Text, text), (sheet.Cell(2, 1).ValueType, sheet.Cell(2, 1).Value));
        Assert.Equal(12.5, sheet.Cell(3, 1).Value);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("1e999")]
    [InlineData("-Infinity")]
    public void A_stored_number_that_is_not_finite_reads_as_a_number_error_and_saves_again(string stored)
    {
        var workbook = XlsxWorkbook.Load(XlsxFactory.Rich($"""<row r="1"><c r="A1" s="1"><v>{stored}</v></c><c r="B1"><v>2</v></c></row>"""));

        var cell = workbook.Worksheets[0].Cell("A1");
        Assert.Equal((XlsxValueType.Error, new XlsxError("#NUM!")), (cell.ValueType, cell.Value));
        Assert.Equal("#NUM!", cell.FormattedText);

        var saved = XlsxWorkbook.Load(workbook.ToArray()).Worksheets[0];
        Assert.Equal(new XlsxError("#NUM!"), saved.Cell("A1").Value);
        Assert.Equal(2.0, saved.Cell("B1").Value);
    }

    [Theory]
    [InlineData(double.NaN, "0.00")]
    [InlineData(double.PositiveInfinity, "#,##0")]
    [InlineData(double.NegativeInfinity, "General")]
    [InlineData(double.NaN, "0.00E+00")]
    public void A_number_that_is_not_finite_is_displayed_as_a_number_error(double value, string format)
    {
        Assert.Equal("#NUM!", NumberFormatter.Format(value, format, Invariant));
    }

    [Theory]
    [InlineData("xl/worksheets/sheet1.xml", "<conditionalFormatting", "<mergeCells count=\"1\"><mergeCell ref=\"nowhere\"/></mergeCells><conditionalFormatting")]
    [InlineData("xl/worksheets/sheet1.xml", "<conditionalFormatting", "<autoFilter ref=\"A1:nowhere\"/><conditionalFormatting")]
    [InlineData("xl/worksheets/sheet1.xml", "<cols>", "<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"-1\" state=\"frozen\"/></sheetView></sheetViews><cols>")]
    [InlineData("xl/workbook.xml", "name=\"Autre\"", "name=\"Données\"")]
    [InlineData("xl/workbook.xml", "name=\"Autre\"", "name=\"\"")]
    [InlineData("xl/workbook.xml", "name=\"Autre\"", "name=\"a/b\"")]
    [InlineData("xl/workbook.xml", "name=\"Autre\"", "name=\"a name longer than the thirty-one characters allowed\"")]
    public void A_workbook_that_declares_what_a_sheet_cannot_hold_is_a_format_error(string part, string from, string to)
    {
        var package = Rewrite(XlsxFactory.Rich(), part, xml =>
        {
            Assert.Contains(from, xml, StringComparison.Ordinal);
            return xml.Replace(from, to, StringComparison.Ordinal);
        });

        var exception = Assert.Throws<DocumentFormatException>(() => XlsxWorkbook.Load(package));

        Assert.StartsWith("The workbook is not valid: ", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void A_row_of_more_cells_than_a_sheet_has_columns_is_a_format_error()
    {
        var cells = string.Concat(Enumerable.Repeat("<c><v>1</v></c>", CellReference.MaxColumn + 1));

        Assert.Throws<DocumentFormatException>(() => XlsxWorkbook.Load(XlsxFactory.Rich("<row r=\"1\">" + cells + "</row>")));
    }

    [Fact]
    public void A_column_width_is_refused_past_the_last_column()
    {
        var sheet = new XlsxWorkbook().AddWorksheet("S");

        sheet.SetColumnWidth(CellReference.MaxColumn, 12);

        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.SetColumnWidth(CellReference.MaxColumn + 1, 12));
    }

    [Theory]
    [InlineData("CEILING(0.3,0.1)", 0.3)]
    [InlineData("CEILING(0.25,0.1)", 0.3)]
    [InlineData("FLOOR(0.7,0.1)", 0.7)]
    [InlineData("CEILING(1.1,0.1)", 1.1)]
    [InlineData("FLOOR(-0.3,-0.1)", -0.3)]
    [InlineData("CEILING(123456789012345,2)", 123456789012346.0)]
    [InlineData("CEILING(1,3)", 3.0)]
    public void CEILING_and_FLOOR_give_the_multiple_itself_and_not_its_binary_neighbour(string formula, double expected)
    {
        // Compared exactly: 3 times 0.1 computed in binary is 0.30000000000000004, which shows in a text join.
        Assert.Equal(expected, FormulaFixture.Evaluate(formula));
    }

    private static byte[] Rewrite(byte[] package, string part, Func<string, string> change)
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
