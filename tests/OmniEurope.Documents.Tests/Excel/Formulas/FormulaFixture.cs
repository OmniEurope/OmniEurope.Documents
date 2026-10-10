// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Excel;

namespace OmniEurope.Documents.Tests.Excel.Formulas;

/// <summary>
/// One sheet "Données" of known values for formula tests: A1:A5 = 1, 2, 3, 4, 5; B1:B5 = "pomme", "poire",
/// "Pomme", "kiwi", empty; C1:C5 = 10, "texte", TRUE, empty, 2.5; D1 = #DIV/0!. Formulas are evaluated in Z100,
/// outside every range the tests read.
/// </summary>
internal static class FormulaFixture
{
    public static readonly DateTime Now = new(2026, 10, 8, 14, 30, 0);

    public static XlsxWorkbook Workbook()
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("Données");
        for (var r = 1; r <= 5; r++)
        {
            sheet.Cell(r, 1).Value = r;
        }

        string[] fruits = ["pomme", "poire", "Pomme", "kiwi"];
        for (var r = 1; r <= fruits.Length; r++)
        {
            sheet.Cell(r, 2).Value = fruits[r - 1];
        }

        sheet.Cell("C1").Value = 10;
        sheet.Cell("C2").Value = "texte";
        sheet.Cell("C3").Value = true;
        sheet.Cell("C5").Value = 2.5;
        sheet.Cell("D1").Value = new XlsxError("#DIV/0!");
        return workbook;
    }

    /// <summary>The value of <paramref name="formula"/> computed in Z100 of the fixture sheet.</summary>
    public static object? Evaluate(string formula, Action<XlsxWorksheet>? setup = null, bool date1904 = false)
    {
        var workbook = Workbook();
        workbook.Date1904 = date1904;
        var sheet = workbook.Worksheet("Données");
        setup?.Invoke(sheet);
        sheet.Cell("Z100").Formula = formula;
        var result = workbook.Recalculate(new XlsxRecalculationOptions { Now = Now });
        Assert.Empty(result.Unsupported);
        return sheet.Cell("Z100").Value;
    }

    public static XlsxError Error(string code) => new(code);
}
