// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Csv;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Integration;

/// <summary>
/// A workbook through its whole life: written, saved and reread cell by cell, exported to CSV and imported
/// back, converted to Word and to PDF and read back.
/// </summary>
public sealed class ExcelWorkflowTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    [Fact]
    public void Every_cell_value_style_and_sheet_setting_survives_saving()
    {
        var original = SampleDocuments.Workbook();

        var reread = XlsxWorkbook.Load(original.ToArray());

        Assert.Equal(("Ventes 2026", "Service commercial"), (reread.Title, reread.Author));
        Assert.Equal(["Ventes", "Notes"], reread.Worksheets.Select(s => s.Name));
        foreach (var sheet in original.Worksheets)
        {
            var copy = reread.Worksheet(sheet.Name);
            Assert.Equal(sheet.UsedRange, copy.UsedRange);
            Assert.Equal(sheet.MergedRanges, copy.MergedRanges);
            Assert.Equal((sheet.FrozenRows, sheet.FrozenColumns, sheet.AutoFilter), (copy.FrozenRows, copy.FrozenColumns, copy.AutoFilter));
            foreach (var cell in sheet.Cells.Where(c => c.ValueType != XlsxValueType.Empty || c.Formula is not null))
            {
                var other = copy.FindCell(cell.Row, cell.Column);
                Assert.NotNull(other);
                Assert.Equal((cell.ValueType, cell.Value, cell.Formula), (other.ValueType, other.Value, other.Formula));
                // Text starting like a formula is saved with the literal-text marker (ProtectFormulaLikeText).
                var formulaLike = cell.Value is string s && s.Length > 0 && "=+-@".Contains(s[0], StringComparison.Ordinal);
                Assert.Equal(cell.Style with { QuotePrefix = formulaLike }, other.Style);
                Assert.Equal(cell.FormatValue(French), other.FormatValue(French));
            }
        }

        Assert.Equal(25, reread.Worksheet("Ventes").ColumnWidths[1], 1);
    }

    [Fact]
    public void Displayed_values_go_to_csv_and_come_back_as_numbers()
    {
        var sheet = SampleDocuments.Workbook().Worksheet("Ventes");

        var csv = XlsxCsvConverter.ToCsv(sheet, new CsvWriterOptions { Delimiter = ';' }, French);
        var rows = CsvReader.ReadAll(csv, new CsvReaderOptions { HasHeader = false });

        Assert.Equal(sheet.UsedRange!.Value.LastRow, rows.Count);
        Assert.Equal(["Région", "Montant", "Part", "Date", "Payé", "Total"], rows[1]);
        var group = French.NumberFormat.NumberGroupSeparator;
        Assert.Equal(["Nord", $"1{group}234,50", "25%", "16/07/2026", "TRUE", "308,63"], rows[2]);
        Assert.Equal(["Sud", "987,25", "75%", "03/08/2026", "FALSE", string.Empty], rows[3]);
        Assert.Equal("=cmd|' /C calc'!A0", rows[4][0]);

        using var reader = CsvReader.Open(new StringReader(XlsxCsvConverter.ToCsv(sheet, culture: CultureInfo.InvariantCulture)), new CsvReaderOptions { Delimiter = ',' });
        var imported = XlsxCsvConverter.FromCsv(reader, "Import", CultureInfo.InvariantCulture).Worksheet("Import");

        // The title row became the header: bold, frozen, filtered over the whole import.
        Assert.True(imported.Cell("A1").Style.Bold);
        Assert.Equal(1, imported.FrozenRows);
        Assert.Equal(new XlsxRange(1, 1, rows.Count, 6), imported.AutoFilter);
        Assert.Equal(1234.5, imported.Cell("B3").Value);
        Assert.Equal(XlsxValueType.Text, imported.Cell("C3").ValueType);
        Assert.Equal("=cmd|' /C calc'!A0", imported.Cell("A5").Value);
    }

    [Fact]
    public void The_workbook_converts_to_word_and_pdf_with_its_displayed_values()
    {
        var workbook = XlsxWorkbook.Load(SampleDocuments.Workbook().ToArray());
        var options = new ExcelPdfOptions { Culture = CultureInfo.InvariantCulture, SheetTitles = true };

        var word = WordDocument.Load(ExcelToPdf.ToWord(workbook, options).ToArray());
        var tables = word.Blocks.OfType<WordTable>().ToList();
        Assert.Equal(2, tables.Count);
        Samples.InOrder(word.Text, "Ventes", "Région", "Nord", "1,234.50", "25%", "Notes", "Remarque longue");

        var conversion = ExcelToPdf.Convert(workbook.ToArray(), options);
        var pdf = PdfDocument.Open(conversion.Pdf);
        Assert.Equal(conversion.PageCount, pdf.PageCount);
        Assert.Equal("Ventes 2026", pdf.Information.Title);
        var text = Samples.AllText(pdf);
        Samples.InOrder(text, "Ventes", "Bilan des ventes", "Région", "Montant", "Total", "Nord", "1,234.50", "25%", "16/07/2026", "308.63", "Sud", "987.25");
        Assert.Contains("#DIV/0!", text, StringComparison.Ordinal);
        Samples.InOrder(text, "Notes", "longue", "proche.", "36:00");

        // A number never wraps: the formatted one fills with '#', the General one loses digits.
        Assert.Matches(@"\s#{5,}\s", text);
        Assert.DoesNotContain("1,234,567", text, StringComparison.Ordinal);
        Assert.Matches(@"\s1\.2\d*E\+08\s", text);
    }
}
