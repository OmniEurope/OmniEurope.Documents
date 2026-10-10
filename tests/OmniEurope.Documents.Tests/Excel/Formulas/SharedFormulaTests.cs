// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Excel.Formulas;

namespace OmniEurope.Documents.Tests.Excel.Formulas;

/// <summary>
/// Shared formulas (ECMA-376 part 1, §18.3.1.40): a formula filled down or across is stored once, the other cells of
/// the group holding only its index; reading gives each cell its own formula, the first one moved to its place, so
/// recalculation computes every cell of the group.
/// </summary>
public sealed class SharedFormulaTests
{
    [Theory]
    [InlineData("B1*2", 1, 0, "B2*2")]
    [InlineData("$B$1+B$1+$B1", 2, 1, "$B$1+C$1+$B3")]
    [InlineData("SUM(A1:B2)", 1, 1, "SUM(B2:C3)")]
    [InlineData("SUM(a1)", 0, 26, "SUM(AA1)")]
    [InlineData("SUM(A:A)+SUM($A:B)", 0, 2, "SUM(C:C)+SUM($A:D)")]
    [InlineData("SUM(1:2)+SUM(1:$2)", 3, 0, "SUM(4:5)+SUM(4:$2)")]
    [InlineData("\"A1\"&A1", 1, 0, "\"A1\"&A2")]
    [InlineData("\"il dit \"\"A1\"\"\"&A1", 1, 0, "\"il dit \"\"A1\"\"\"&A2")]
    [InlineData("'A1 x'!A1+'L''an'!A1+Feuil1!B2+AB1!A1", 1, 0, "'A1 x'!A2+'L''an'!A2+Feuil1!B3+AB1!A2")]
    [InlineData("LOG10(A1)+ATAN2(1,2)+_xlfn.CONCAT(A1)", 1, 0, "LOG10(A2)+ATAN2(1,2)+_xlfn.CONCAT(A2)")]
    [InlineData("Tableau1[[#This Row],[A1]]+A1", 1, 0, "Tableau1[[#This Row],[A1]]+A2")]
    [InlineData("1.5E2+A1", 1, 0, "1.5E2+A2")]
    [InlineData("A1-1", -1, 0, "#REF!-1")]
    [InlineData("SUM(A2:A1)", -1, 0, "SUM(#REF!)")]
    [InlineData("A:A", 0, -1, "#REF!")]
    [InlineData("1:1", -1, 0, "#REF!")]
    [InlineData("XFD1", 0, 1, "#REF!")]
    [InlineData("\"ouvert A1", 1, 0, "\"ouvert A1")]
    [InlineData("Tableau1[A1", 1, 0, "Tableau1[A1")]
    public void A_formula_moves_its_relative_references_only(string formula, int rows, int columns, string expected)
    {
        Assert.Equal(expected, FormulaShifter.Shift(formula, rows, columns));
    }

    [Fact]
    public void A_cell_of_a_group_met_before_its_first_formula_has_none()
    {
        var shared = new SharedFormulas();

        Assert.Null(shared.Resolve(string.Empty, "0", 2, 2));
        Assert.Equal("A1", shared.Resolve("A1", "0", 3, 2));
        Assert.Equal("A2", shared.Resolve(null, "0", 4, 2));
        Assert.Equal("B1", shared.Resolve("B1", "0", 5, 2));
        Assert.Equal("C1", shared.Resolve("C1", null, 6, 2));
    }

    [Fact]
    public void A_filled_down_formula_is_read_and_computed_in_every_cell_of_its_group()
    {
        // B1 = A1*2+$A$1 filled down to B3 and across to C1, stored once; cached results are stale (0).
        const string data = """
            <row r="1"><c r="A1"><v>1</v></c><c r="B1"><f t="shared" ref="B1:C3" si="0">A1*2+$A$1</f><v>0</v></c><c r="C1"><f t="shared" si="0"/><v>0</v></c></row>
            <row r="2"><c r="A2"><v>2</v></c><c r="B2"><f t="shared" si="0"/><v>0</v></c></row>
            <row r="3"><c r="A3"><v>3</v></c><c r="B3"><f t="shared" si="0"/><v>0</v></c></row>
            """;
        var workbook = XlsxWorkbook.Load(XlsxFactory.Rich(data));
        var sheet = workbook.Worksheet("Données");

        var result = workbook.Recalculate();

        Assert.Equal(("A2*2+$A$1", "A3*2+$A$1", "B1*2+$A$1"), (sheet.Cell("B2").Formula, sheet.Cell("B3").Formula, sheet.Cell("C1").Formula));
        Assert.Equal((3.0, 5.0, 7.0, 7.0), (sheet.Cell("B1").Value, sheet.Cell("B2").Value, sheet.Cell("B3").Value, sheet.Cell("C1").Value));
        Assert.Equal((4, 0), (result.Computed, result.Unsupported.Count));
    }
}
