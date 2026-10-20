// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Excel;

namespace OmniEurope.Documents.Tests.Excel.Formulas;

/// <summary>
/// The memory budget of the formula engine is set by <see cref="XlsxRecalculationOptions"/>; the defaults are one
/// whole column per matrix and eight times that per formula.
/// </summary>
public sealed class FormulaBudgetTests
{
    private const double Stored = 123;

    [Fact]
    public void Defaults_are_one_column_per_matrix_and_eight_columns_per_formula()
    {
        var options = new XlsxRecalculationOptions();

        Assert.Equal(1_048_576, options.MaxMatrixCells);
        Assert.Equal(8L * 1_048_576, options.MaxFormulaCells);
    }

    [Fact]
    public void A_range_of_1_2_million_cells_is_not_calculated_with_the_defaults()
    {
        var (result, value) = SumOfTwentyColumnsOver60000Rows(new XlsxRecalculationOptions());

        Assert.Equal(Stored, value);
        var reported = Assert.Single(result.Unsupported);
        Assert.Contains("1,200,000 cells", reported, StringComparison.Ordinal);
    }

    [Fact]
    public void A_range_of_1_2_million_cells_is_calculated_with_a_raised_matrix_budget()
    {
        var (result, value) = SumOfTwentyColumnsOver60000Rows(new XlsxRecalculationOptions { MaxMatrixCells = 2_000_000 });

        Assert.Empty(result.Unsupported);
        Assert.Equal(17.0, value); // 1 + 2 + 4 and the two 5 of the shared fixture
    }

    [Fact]
    public void A_matrix_budget_above_the_formula_budget_is_refused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SumOfTwentyColumnsOver60000Rows(new XlsxRecalculationOptions { MaxMatrixCells = 10_000_000 }));

    [Fact]
    public void A_lowered_matrix_budget_refuses_a_smaller_range()
    {
        var workbook = Workbook("SUM(Data!A1:A200)");

        var result = workbook.Recalculate(new XlsxRecalculationOptions { MaxMatrixCells = 100, MaxFormulaCells = 100 });

        Assert.Equal(Stored, workbook.Worksheets[0].Cell("B1").Value);
        Assert.Contains("more than the 100 allowed", Assert.Single(result.Unsupported), StringComparison.Ordinal);
    }

    [Fact]
    public void A_lowered_formula_budget_refuses_a_formula_holding_several_matrices()
    {
        var workbook = Workbook("SUM(Data!A1:A60)+SUM(Data!A1:A60)");

        var result = workbook.Recalculate(new XlsxRecalculationOptions { MaxMatrixCells = 100, MaxFormulaCells = 100 });

        Assert.Equal(Stored, workbook.Worksheets[0].Cell("B1").Value);
        Assert.Contains("more than the 100 allowed", Assert.Single(result.Unsupported), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0L, 10L)]
    [InlineData(-1L, 10L)]
    [InlineData(10L, 0L)]
    [InlineData(10L, -5L)]
    public void A_budget_that_is_not_positive_is_refused(long matrix, long formula) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new XlsxRecalculationOptions { MaxMatrixCells = matrix, MaxFormulaCells = formula });

    [Fact]
    public void A_formula_budget_below_the_matrix_budget_is_refused()
    {
        var workbook = Workbook("1");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            workbook.Recalculate(new XlsxRecalculationOptions { MaxMatrixCells = 100, MaxFormulaCells = 99 }));
    }

    // Three cells make the used range of Data A1:T60000, 1.2 million cells, without storing them all.
    private static (XlsxRecalculation Result, object? Value) SumOfTwentyColumnsOver60000Rows(XlsxRecalculationOptions options)
    {
        var workbook = Workbook("SUM(Data!A:T)");
        var data = workbook.Worksheets[1];
        data.Cell("A1").Value = 1;
        data.Cell("J30000").Value = 2;
        data.Cell("T60000").Value = 4;
        var result = workbook.Recalculate(options);
        return (result, workbook.Worksheets[0].Cell("B1").Value);
    }

    private static XlsxWorkbook Workbook(string formula)
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("S");
        var data = workbook.AddWorksheet("Data");
        data.Cell("A200").Value = 5;
        data.Cell("A60").Value = 5;
        sheet.Cell("B1").Value = Stored;
        sheet.Cell("B1").Formula = formula;
        return workbook;
    }
}
