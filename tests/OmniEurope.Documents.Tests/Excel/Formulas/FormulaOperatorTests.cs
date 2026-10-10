// SPDX-License-Identifier: EUPL-1.2
using static OmniEurope.Documents.Tests.Excel.Formulas.FormulaFixture;

namespace OmniEurope.Documents.Tests.Excel.Formulas;

/// <summary>
/// Operators, precedence, coercion and comparison (ECMA-376 part 1, §18.17.2). Expected values are worked out by
/// hand from the rules Excel documents: negation before power, numbers before text before booleans.
/// </summary>
public sealed class FormulaOperatorTests
{
    [Theory]
    [InlineData("1+2*3", 7.0)]
    [InlineData("(1+2)*3", 9.0)]
    [InlineData("-2^2", 4.0)]
    [InlineData("2^3^2", 64.0)]
    [InlineData("10-4-3", 3.0)]
    [InlineData("12/4/3", 1.0)]
    [InlineData("50%", 0.5)]
    [InlineData("200%*3", 6.0)]
    [InlineData("=A2*A3+A4", 10.0)]
    [InlineData("\"3\"+4", 7.0)]
    [InlineData("TRUE+TRUE", 2.0)]
    [InlineData("C4+1", 1.0)]
    [InlineData("1.5E2+.5", 150.5)]
    [InlineData("SUM({1,2;3,4})", 10.0)]
    [InlineData("SUM({1,-2,3})", 2.0)]
    public void Arithmetic_follows_excel_precedence_and_coercion(string formula, double expected)
    {
        Assert.Equal(expected, Evaluate(formula));
    }

    [Theory]
    [InlineData("1/0", "#DIV/0!")]
    [InlineData("\"a\"+1", "#VALUE!")]
    [InlineData("D1+1", "#DIV/0!")]
    [InlineData("1+D1*0", "#DIV/0!")]
    [InlineData("0^0", "#NUM!")]
    [InlineData("(-8)^0.5", "#NUM!")]
    [InlineData("10^400", "#NUM!")]
    [InlineData("Absente!A1", "#REF!")]
    [InlineData("#N/A", "#N/A")]
    public void Errors_are_the_ones_excel_gives(string formula, string error)
    {
        Assert.Equal(Error(error), Evaluate(formula));
    }

    [Theory]
    [InlineData("\"a\"&\"b\"", "ab")]
    [InlineData("A1&B1", "1pomme")]
    [InlineData("1/3&\"\"", "0.333333333333333")]
    [InlineData("TRUE&1", "TRUE1")]
    [InlineData("C4&\"x\"", "x")]
    [InlineData("\"il dit \"\"oui\"\"\"", "il dit \"oui\"")]
    public void Concatenation_writes_numbers_with_fifteen_digits(string formula, string expected)
    {
        Assert.Equal(expected, Evaluate(formula));
    }

    [Theory]
    [InlineData("1<2", true)]
    [InlineData("\"a\"=\"A\"", true)]
    [InlineData("\"abc\"<\"abd\"", true)]
    [InlineData("1<\"0\"", true)]
    [InlineData("\"z\"<TRUE", true)]
    [InlineData("C4=0", true)]
    [InlineData("C4=\"\"", true)]
    [InlineData("C4=FALSE", true)]
    [InlineData("2<>2", false)]
    [InlineData("3>=3", true)]
    [InlineData("A1+1=A2", true)]
    public void Comparison_orders_numbers_then_text_then_booleans(string formula, bool expected)
    {
        Assert.Equal(expected, Evaluate(formula));
    }

    [Fact]
    public void Ranges_operate_element_by_element()
    {
        // SUMPRODUCT((A1:A5>2)*A1:A5): 3 + 4 + 5.
        Assert.Equal(12.0, Evaluate("SUMPRODUCT((A1:A5>2)*A1:A5)"));
        Assert.Equal(30.0, Evaluate("SUM(A1:A5*2)"));
        Assert.Equal(11.0, Evaluate("SUM(A1:A2*{1;5})"));
    }

    [Fact]
    public void References_may_name_their_sheet_and_be_absolute()
    {
        Assert.Equal(7.0, Evaluate("'Autre feuille'!$B$2+Données!A3+Prix!A1", sheet =>
        {
            sheet.Workbook.AddWorksheet("Autre feuille").Cell("B2").Value = 3;
            sheet.Workbook.AddWorksheet("Prix").Cell("A1").Value = 1;
        }));
        Assert.Equal(15.0, Evaluate("SUM(A:A)"));
        Assert.Equal(7.5, Evaluate("SUM(5:5)"));
    }
}
