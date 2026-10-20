// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Excel;

namespace OmniEurope.Documents.Tests.Excel.Formulas;

/// <summary>
/// A formula nested too deeply, or reading or building a matrix too large, is not calculated: the cell keeps the
/// result the file stored and is reported like an unsupported formula, and the other formulas are still computed.
/// A stack overflow cannot be caught, so a regression here shows as the whole test run crashing.
/// </summary>
public sealed class FormulaLimitTests
{
    private const int MaxNesting = 256;

    private const double Stored = 123;

    public static TheoryData<string> TooDeepFormulas() => new()
    {
        Repeat("(", 10_000) + "1" + Repeat(")", 10_000),
        Repeat("(", MaxNesting + 1) + "1" + Repeat(")", MaxNesting + 1),
        Repeat("-", 100_000) + "1",
        Repeat("+", 100_000) + "1",
        Repeat("-", MaxNesting + 1) + "1",
        "1" + Repeat("%", 100_000),
        "100" + Repeat("%", MaxNesting + 1),
        Repeat("ABS(", 10_000) + "1" + Repeat(")", 10_000),
        Repeat("ABS(", MaxNesting + 1) + "1" + Repeat(")", MaxNesting + 1),
        Repeat("IF(TRUE,", 10_000) + "1" + Repeat(",0)", 10_000),
        Repeat("1+(", 10_000) + "1" + Repeat(")", 10_000),
        Repeat("2*(", 10_000) + "1" + Repeat(")", 10_000),
        Repeat("SUM(1,", 10_000) + "1" + Repeat(")", 10_000),
        Repeat("-(", 10_000) + "1" + Repeat(")", 10_000),
    };

    public static TheoryData<string> DeepestAllowedFormulas() => new()
    {
        Repeat("(", MaxNesting) + "1" + Repeat(")", MaxNesting),
        Repeat("-", MaxNesting) + "1",
        "100" + Repeat("%", MaxNesting),
        Repeat("ABS(", MaxNesting) + "1" + Repeat(")", MaxNesting),
        Repeat("IF(TRUE,", MaxNesting) + "1" + Repeat(",0)", MaxNesting),
        Repeat("1+(", MaxNesting) + "1" + Repeat(")", MaxNesting),
    };

    [Theory]
    [MemberData(nameof(TooDeepFormulas))]
    public void A_formula_nested_too_deeply_is_not_calculated_and_keeps_its_stored_value(string formula)
    {
        var (result, sheet) = Recalculate(formula);

        AssertNotCalculated(result, sheet, "256");
    }

    [Theory]
    [MemberData(nameof(DeepestAllowedFormulas))]
    public void A_formula_nested_exactly_to_the_limit_is_still_calculated(string formula)
    {
        var (result, sheet) = Recalculate(formula);

        Assert.Empty(result.Unsupported);
        Assert.Equal(2, result.Computed);
        Assert.NotEqual(Stored, sheet.Cell("B1").Value);
    }

    [Fact]
    public void A_formula_at_the_limit_is_evaluated_on_a_small_stack()
    {
        // 256 nested IF calls, the heaviest nesting, on a 1 MB stack (the default of a Windows thread) with the
        // frames of the caller on top: the cap is only useful if it keeps the evaluation well inside that stack.
        var formula = Repeat("IF(TRUE,", MaxNesting) + "7" + Repeat(",0)", MaxNesting);
        (XlsxRecalculation Result, XlsxWorksheet Sheet)? outcome = null;
        var thread = new Thread(() => outcome = Recalculate(formula), 1024 * 1024);
        thread.Start();
        thread.Join();

        Assert.NotNull(outcome);
        Assert.Equal(7.0, outcome.Value.Sheet.Cell("B1").Value);
    }

    [Fact]
    public void A_long_chain_of_additions_is_calculated_without_recursion()
    {
        var (result, sheet) = Recalculate("1" + Repeat("+1", 200_000));

        Assert.Empty(result.Unsupported);
        Assert.Equal(200_001.0, sheet.Cell("B1").Value);
    }

    [Fact]
    public void A_long_chain_of_comparisons_and_concatenations_is_calculated_without_recursion()
    {
        var (result, sheet) = Recalculate("\"a\"" + Repeat("&\"b\"", 50_000));

        Assert.Empty(result.Unsupported);
        Assert.Equal(1 + 50_000, ((string)sheet.Cell("B1").Value!).Length);
    }

    [Fact]
    public void A_range_larger_than_the_cell_budget_is_not_calculated_and_keeps_its_stored_value()
    {
        // Two cells set at the far corners make the used range the whole sheet, about 17 billion cells.
        var (result, sheet) = Recalculate("SUM(Data!A1:XFD1048576)", s =>
        {
            s.Cell("A1").Value = 1;
            s.Cell("XFD1048576").Value = 2;
        });

        AssertNotCalculated(result, sheet, "cells");
    }

    [Theory]
    [InlineData("SUM(Data!A:B)")]
    [InlineData("SUM(Data!A1:B1048576)")]
    [InlineData("COUNT(Data!A1:XFD1048576)")]
    [InlineData("SUMPRODUCT(Data!A1:B1048576,Data!A1:B1048576)")]
    public void A_range_just_over_the_cell_budget_is_not_calculated(string formula)
    {
        var (result, sheet) = Recalculate(formula, s =>
        {
            s.Cell("A1").Value = 1;
            s.Cell("B1048576").Value = 2;
        });

        AssertNotCalculated(result, sheet, "cells");
    }

    [Fact]
    public void A_whole_column_is_within_the_cell_budget()
    {
        var (result, sheet) = Recalculate("SUM(Data!A:A)", s =>
        {
            s.Cell("A1").Value = 1;
            s.Cell("A1048576").Value = 2;
        });

        Assert.Empty(result.Unsupported);
        Assert.Equal(3.0, sheet.Cell("B1").Value);
    }

    [Fact]
    public void A_range_over_the_budget_beside_a_formula_that_fits_does_not_stop_the_recalculation()
    {
        var (result, sheet) = Recalculate(
            "SUM(Data!A1:XFD1048576)",
            data =>
            {
                data.Cell("A1").Value = 1;
                data.Cell("XFD1048576").Value = 2;
            },
            own => own.Cell("D1").Formula = "SUM(Data!A1:A3)+1");

        Assert.Equal(1.0 + 1, sheet.Cell("D1").Value);
        Assert.Equal(Stored + 1, sheet.Cell("C1").Value);
        Assert.Equal(2, result.Computed);
        Assert.Single(result.Unsupported);
    }

    [Fact]
    public void An_operation_on_two_vectors_whose_matrix_is_over_the_cell_budget_is_not_calculated()
    {
        // A column of 2 000 cells against a row of 16 384 cells: each is small, their element by element
        // product would hold 32 million.
        var (result, sheet) = Recalculate("SUM(Data!A1:A2000*Data!A1:XFD1)", s =>
        {
            s.Cell("A2000").Value = 1;
            s.Cell("XFD1").Value = 1;
        });

        AssertNotCalculated(result, sheet, "cells");
    }

    [Theory]
    [InlineData("SUM(Data!A1:A2000+Data!A1:XFD1)")]
    [InlineData("SUM(Data!A1:A2000&Data!A1:XFD1)")]
    [InlineData("SUM(Data!A1:A2000=Data!A1:XFD1)")]
    [InlineData("SUM(Data!A1:A2000^Data!A1:XFD1)")]
    public void Every_binary_operator_checks_the_size_of_its_matrix(string formula)
    {
        var (result, sheet) = Recalculate(formula, s =>
        {
            s.Cell("A2000").Value = 1;
            s.Cell("XFD1").Value = 1;
        });

        AssertNotCalculated(result, sheet, "cells");
    }

    [Fact]
    public void Many_large_ranges_in_one_formula_are_refused_before_they_exhaust_memory()
    {
        // Each whole column fits the budget, ten of them held by one formula do not.
        var formula = "SUM(" + string.Join("+", Enumerable.Repeat("Data!A:A", 10)) + ")";
        var (result, sheet) = Recalculate(formula, s =>
        {
            s.Cell("A1").Value = 1;
            s.Cell("A1048576").Value = 2;
        });

        AssertNotCalculated(result, sheet, "cells");
    }

    [Fact]
    public void An_array_constant_over_the_cell_budget_is_not_calculated()
    {
        var (result, sheet) = Recalculate("SUM({1" + Repeat(",1", 1_048_576) + "})");

        AssertNotCalculated(result, sheet, "cells");
    }

    [Fact]
    public void An_array_constant_of_a_few_cells_is_calculated()
    {
        var (result, sheet) = Recalculate("SUM({1,2;3,4})");

        Assert.Empty(result.Unsupported);
        Assert.Equal(10.0, sheet.Cell("B1").Value);
    }

    private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

    // B1 holds the formula and a stored result, C1 reads B1: the stored value must reach C1 when B1 is skipped.
    private static (XlsxRecalculation Result, XlsxWorksheet Sheet) Recalculate(string formula, Action<XlsxWorksheet>? data = null, Action<XlsxWorksheet>? own = null)
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("S");
        data?.Invoke(workbook.AddWorksheet("Data"));
        own?.Invoke(sheet);
        sheet.Cell("B1").Value = Stored;
        sheet.Cell("B1").Formula = formula;
        sheet.Cell("C1").Formula = "B1+1";
        return (workbook.Recalculate(), sheet);
    }

    private static void AssertNotCalculated(XlsxRecalculation result, XlsxWorksheet sheet, string reasonPart)
    {
        Assert.Equal(Stored, sheet.Cell("B1").Value);
        Assert.Equal(Stored + 1, sheet.Cell("C1").Value);
        Assert.Equal(1, result.Computed);
        var reported = Assert.Single(result.Unsupported);
        Assert.StartsWith("S!B1: ", reported, StringComparison.Ordinal);
        Assert.Contains(reasonPart, reported, StringComparison.Ordinal);
    }
}
