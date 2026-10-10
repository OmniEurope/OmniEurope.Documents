// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Excel;
using static OmniEurope.Documents.Tests.Excel.Formulas.FormulaFixture;

namespace OmniEurope.Documents.Tests.Excel.Formulas;

/// <summary>
/// How formulas are read (ECMA-376 part 1, §18.17): references and sheet names, numbers, literals, function
/// prefixes; and what the engine reports as not computed (3-D references, the union and intersection operators,
/// malformed text) instead of computing a wrong result.
/// </summary>
public sealed class FormulaSyntaxTests
{
    [Theory]
    [InlineData("$A$2+A$3+$A4", 9.0)]
    [InlineData("SUM($A$1:A2)", 3.0)]
    [InlineData("SUM(A2:A1)", 3.0)]
    [InlineData("SUM(A:A)", 15.0)]
    [InlineData("SUM($A:$A)", 15.0)]
    [InlineData("SUM(2:2)", 2.0)]
    [InlineData("SUM($3:$4)", 7.0)]
    [InlineData("SUM( A1 , A2 )", 3.0)]
    [InlineData("1E2", 100.0)]
    [InlineData("1e+2+2.5E-1", 100.25)]
    [InlineData("+A2", 2.0)]
    [InlineData("--A2", 2.0)]
    [InlineData("A2%%", 0.0002)]
    [InlineData("_xlfn.CONCAT(A1,A2)", "12")]
    [InlineData("_xlfn._xlws.CONCAT(A1)", "1")]
    [InlineData("sum(a1:a2)", 3.0)]
    [InlineData("#n/a", "#N/A")]
    [InlineData("#NULL!", "#NULL!")]
    [InlineData("true", true)]
    [InlineData("SUM({1,2;3,4}*{1;0})", 3.0)]
    [InlineData("SUM({\"a\",TRUE,#N/A})", "#N/A")]
    [InlineData("PI()*0", 0.0)]
    [InlineData("IF(,1,2)", 2.0)]
    [InlineData("AND(TRUE(),NOT(FALSE()))", true)]
    public void References_numbers_and_literals_are_read_as_excel_writes_them(string formula, object expected)
    {
        var value = Evaluate(formula);
        Assert.Equal(expected is string { Length: > 0 } text && text[0] == '#' ? Error(text) : expected, value is double d ? Math.Round(d, 10) : value);
    }

    [Fact]
    public void Sheet_names_may_be_quoted_hold_quotes_and_look_like_cells()
    {
        Assert.Equal(6.0, Evaluate("'L''an'!A1+AB1!A1+'AB1'!A2", sheet =>
        {
            sheet.Workbook.AddWorksheet("L'an").Cell("A1").Value = 1;
            var lookalike = sheet.Workbook.AddWorksheet("AB1");
            lookalike.Cell("A1").Value = 2;
            lookalike.Cell("A2").Value = 3;
        }));
    }

    [Theory]
    [InlineData("SUM(Jan:Mar!A1)", "'Jan:Mar!A1' is not computed")]
    [InlineData("SUM('Jan:Mar'!A1:B2)", "''Jan:Mar'!A1:B2' is not computed")]
    [InlineData("SUM(Feuil1:Feuil3!A1)", "'Feuil1:Feuil3!A1' is not computed")]
    [InlineData("Taux", "'Taux' is not computed")]
    [InlineData("Tableau1[", "'Tableau1[' is not computed")]
    [InlineData("5E", "unexpected text")]
    [InlineData("SUM(A1 A2)", "')' expected")]
    [InlineData("SUM((A1,A2))", "')' expected")]
    [InlineData("A1:B", "unexpected text")]
    [InlineData("\"ouvert", "unterminated text")]
    [InlineData("'Feuille!A1", "unterminated sheet name")]
    [InlineData("'Feuille'A1", "unterminated sheet name")]
    [InlineData("'Feuille'!", "reference expected after the sheet name")]
    [InlineData("Feuille!B", "reference expected after the sheet name")]
    [InlineData("#FAUX!", "unknown error value")]
    [InlineData("{1,2;3}", "array rows of different lengths")]
    [InlineData("{1|2}", "bad array")]
    [InlineData("{1,A1}", "an array holds constants only")]
    [InlineData("1..2", "bad number")]
    [InlineData("@A1", "unexpected character")]
    [InlineData("(1", "')' expected")]
    [InlineData("1)", "unexpected text")]
    [InlineData("", "missing operand")]
    public void What_it_cannot_read_or_compute_is_reported_not_guessed(string formula, string reason)
    {
        var workbook = Workbook();
        var sheet = workbook.Worksheet("Données");
        sheet.Cell("Z100").Value = 42.0;
        sheet.Cell("Z100").Formula = formula;

        var result = workbook.Recalculate();

        Assert.Equal(42.0, sheet.Cell("Z100").Value);
        Assert.Contains(reason, Assert.Single(result.Unsupported), StringComparison.Ordinal);
    }

    [Fact]
    public void A_reference_beyond_the_last_row_or_column_is_not_a_reference()
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("Feuil1");
        sheet.Cell("A1").Formula = "XFE1";
        sheet.Cell("A2").Formula = "A1048577";
        sheet.Cell("A3").Formula = "A0";
        sheet.Cell("A4").Formula = "XFD1048576+1";

        var result = workbook.Recalculate();

        Assert.Equal(["Feuil1!A1", "Feuil1!A2", "Feuil1!A3"], result.Unsupported.Select(u => u[..u.IndexOf(':', StringComparison.Ordinal)]));
        Assert.Equal(1.0, sheet.Cell("A4").Value);
    }
}
