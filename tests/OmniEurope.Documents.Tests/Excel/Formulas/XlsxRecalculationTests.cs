// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Pdf;

namespace OmniEurope.Documents.Tests.Excel.Formulas;

/// <summary>
/// Recalculating a workbook: cells in dependency order across sheets, cycles refused, formulas the engine does not
/// compute kept as stored and reported, and a filled-in template giving its totals in PDF and CSV.
/// </summary>
public sealed class XlsxRecalculationTests
{
    [Fact]
    public void Cells_are_computed_after_the_cells_they_read_whatever_their_place()
    {
        // B1 reads B2 which reads Résumé!A1 which reads Saisie!A1: written in the opposite order.
        var workbook = new XlsxWorkbook();
        var input = workbook.AddWorksheet("Saisie");
        var summary = workbook.AddWorksheet("Résumé");
        input.Cell("B1").Formula = "B2*2";
        input.Cell("B2").Formula = "Résumé!A1+1";
        summary.Cell("A1").Formula = "Saisie!A1*10";
        input.Cell("A1").Value = 4;

        var result = workbook.Recalculate();

        Assert.Equal(3, result.Computed);
        Assert.Equal((40.0, 41.0, 82.0), (summary.Cell("A1").Value, input.Cell("B2").Value, input.Cell("B1").Value));
        Assert.Equal("B2*2", input.Cell("B1").Formula);
    }

    [Fact]
    public void A_chain_of_a_hundred_thousand_cells_is_computed_without_recursion()
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("Chaîne");
        sheet.Cell(1, 1).Value = 1;
        for (var r = 2; r <= 100_000; r++)
        {
            sheet.Cell(r, 1).Formula = $"A{r - 1}+1";
        }

        workbook.Recalculate();

        Assert.Equal(100_000.0, sheet.Cell(100_000, 1).Value);
    }

    [Fact]
    public void A_cycle_is_refused_with_its_cells()
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("Boucle");
        sheet.Cell("A1").Formula = "B1+1";
        sheet.Cell("B1").Formula = "A1+1";
        sheet.Cell("C1").Formula = "SUM(C2:C5)";
        sheet.Cell("C5").Formula = "C1";
        sheet.Cell("D1").Formula = "2*3";

        var error = Assert.Throws<XlsxCircularReferenceException>(() => workbook.Recalculate());

        Assert.Equal(["Boucle!A1", "Boucle!B1", "Boucle!C1", "Boucle!C5"], error.Cells.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("INDIRECT(\"A1\")", "function INDIRECT")]
    [InlineData("OFFSET(A1,1,0)", "function OFFSET")]
    [InlineData("Taux*2", "'Taux'")]
    [InlineData("SUM(Tableau1[Montant])", "'Tableau1[Montant]'")]
    [InlineData("WEBSERVICE(\"x\")", "function WEBSERVICE")]
    [InlineData("1+", "missing operand")]
    [InlineData("SUM(1", "')' expected")]
    public void Formulas_it_does_not_compute_keep_their_stored_result(string formula, string reason)
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("Feuil1");
        sheet.Cell("A1").Value = 5;
        sheet.Cell("B1").Value = 123.0;
        sheet.Cell("B1").Formula = formula;
        sheet.Cell("C1").Formula = "B1+1";

        var result = workbook.Recalculate();

        Assert.Equal(123.0, sheet.Cell("B1").Value);
        Assert.Equal(124.0, sheet.Cell("C1").Value);
        Assert.Contains(reason, Assert.Single(result.Unsupported), StringComparison.Ordinal);
        Assert.StartsWith("Feuil1!B1: ", result.Unsupported[0], StringComparison.Ordinal);
        Assert.Equal(1, result.Computed);
    }

    [Fact]
    public void Results_of_every_kind_are_stored_and_a_date_cell_stays_a_date()
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("Types");
        sheet.Cell("A1").Value = new DateTime(2026, 1, 31);
        sheet.Cell("A2").Value = new DateTime(2000, 1, 1);
        sheet.Cell("A2").Formula = "EDATE(A1,1)";
        sheet.Cell("A3").Formula = "\"x\"&1";
        sheet.Cell("A4").Formula = "1>0";
        sheet.Cell("A5").Formula = "1/0";
        sheet.Cell("A6").Formula = "A7";

        workbook.Recalculate();

        Assert.Equal(new DateTime(2026, 2, 28), sheet.Cell("A2").Value);
        Assert.Equal("x1", sheet.Cell("A3").Value);
        Assert.Equal(true, sheet.Cell("A4").Value);
        Assert.Equal(new XlsxError("#DIV/0!"), sheet.Cell("A5").Value);
        Assert.Equal(0.0, sheet.Cell("A6").Value);
    }

    [Fact]
    public void A_filled_template_prints_its_totals_in_pdf_and_csv()
    {
        // An invoice: quantity x unit price per line, a subtotal, 20 % VAT, the total; computed by hand:
        // 3 x 12.50 = 37.50, 2 x 8 = 16, 10 x 1.25 = 12.50; subtotal 66, VAT 13.20, total 79.20.
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("Facture");
        string[] headers = ["Article", "Quantité", "Prix", "Montant"];
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
        }

        (string Name, int Quantity, double Price)[] lines = [("Cahier", 3, 12.5), ("Stylo", 2, 8), ("Gomme", 10, 1.25)];
        for (var i = 0; i < lines.Length; i++)
        {
            var row = i + 2;
            sheet.Cell(row, 1).Value = lines[i].Name;
            sheet.Cell(row, 2).Value = lines[i].Quantity;
            sheet.Cell(row, 3).Value = lines[i].Price;
            sheet.Cell(row, 4).Formula = $"B{row}*C{row}";
            sheet.Cell(row, 4).Style = XlsxStyle.Default with { NumberFormat = "0.00" };
        }

        sheet.Cell("C6").Value = "Sous-total";
        sheet.Cell("D6").Formula = "SUM(D2:D4)";
        sheet.Cell("C7").Value = "TVA";
        sheet.Cell("D7").Formula = "ROUND(D6*20%,2)";
        sheet.Cell("C8").Value = "Total";
        sheet.Cell("D8").Formula = "D6+D7";
        foreach (var reference in new[] { "D6", "D7", "D8" })
        {
            sheet.Cell(reference).Style = XlsxStyle.Default with { NumberFormat = "0.00" };
        }

        var result = workbook.Recalculate();

        Assert.Equal((6, 0), (result.Computed, result.Unsupported.Count));
        var csv = XlsxCsvConverter.ToCsv(sheet, culture: CultureInfo.InvariantCulture);
        Assert.Contains(",,Sous-total,66.00\r\n,,TVA,13.20\r\n,,Total,79.20\r\n", csv, StringComparison.Ordinal);
        Assert.Contains("Cahier,3,12.5,37.50", csv, StringComparison.Ordinal);
        var text = PdfDocument.Open(ExcelToPdf.Convert(workbook, new ExcelPdfOptions { SheetTitles = false }).Pdf).GetPage(1).Text;
        Assert.Contains("Total 79.20", text, StringComparison.Ordinal);
        Assert.Contains("TVA 13.20", text, StringComparison.Ordinal);
    }
}
