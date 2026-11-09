// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Excel;

namespace OmniEurope.Documents.Tests.Excel.Formulas;

/// <summary>IFERROR and IFNA evaluate their first operand once; CEILING and FLOOR survive finite numbers beyond the range of decimal.</summary>
public sealed class FormulaOperandOnceTests
{
    [Fact]
    public void IFERROR_and_IFNA_spend_the_budget_of_their_operand_once()
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("S");
        for (var row = 1; row <= 4; row++)
        {
            sheet.Cell(row, 1).Value = 1;
        }

        sheet.Cell("B1").Formula = "SUM(A1:A4)";
        sheet.Cell("B2").Formula = "IFERROR(SUM(A1:A4),0)";
        sheet.Cell("B3").Formula = "IFNA(SUM(A1:A4),0)";

        var result = workbook.Recalculate(new XlsxRecalculationOptions { MaxMatrixCells = 4, MaxFormulaCells = 4 });

        Assert.Empty(result.Unsupported);
        Assert.Equal([4.0, 4.0, 4.0], new[] { "B1", "B2", "B3" }.Select(r => sheet.Cell(r).Value));
    }

    [Fact]
    public void The_replacement_is_evaluated_for_the_matching_errors_only()
    {
        Assert.Equal(7.0, FormulaFixture.Evaluate("IFERROR(1/0,7)"));
        Assert.Equal(7.0, FormulaFixture.Evaluate("IFNA(NA(),7)"));
        Assert.Equal(FormulaFixture.Error("#DIV/0!"), FormulaFixture.Evaluate("IFNA(1/0,7)"));
        Assert.Equal(3.0, FormulaFixture.Evaluate("IFERROR(3,1/0)"));
    }

    [Fact]
    public void CEILING_and_FLOOR_of_huge_finite_numbers_do_not_stop_the_recalculation()
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("S");
        sheet.Cell("A1").Formula = "CEILING(1E100,1)";
        sheet.Cell("B1").Formula = "FLOOR(1E100,1)";
        sheet.Cell("A2").Formula = "1+1";
        sheet.Cell("B2").Formula = "FLOOR(1E308,1E-10)";

        var result = workbook.Recalculate();

        Assert.Empty(result.Unsupported);
        Assert.Equal(1e100, sheet.Cell("A1").Value);
        Assert.Equal(1e100, sheet.Cell("B1").Value);
        Assert.Equal(2.0, sheet.Cell("A2").Value);
        Assert.Equal(FormulaFixture.Error("#NUM!"), sheet.Cell("B2").Value);
    }
}
