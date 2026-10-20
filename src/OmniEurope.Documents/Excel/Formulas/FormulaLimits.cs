// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;

namespace OmniEurope.Documents.Excel.Formulas;

/// <summary>
/// The bounds that keep a formula read from an untrusted workbook from exhausting the stack or the memory: how
/// deeply a formula may nest, and how many cells of matrices it may hold.
/// </summary>
internal static class FormulaLimits
{
    /// <summary>
    /// Levels of nesting a formula may have (parentheses, function calls, unary signs, percent signs, and the
    /// right operand of an operator; a chain <c>1+1+1+...</c> is read in a loop and does not nest). Excel allows
    /// 64; 256 leaves room for generated formulas and keeps the parser and the evaluator far from the stack limit.
    /// </summary>
    public const int MaxNesting = 256;

    /// <summary>
    /// Cells of one matrix (a range read, an array constant, the result of an operation on arrays): one whole
    /// column of a sheet, so that <c>SUM(A:A)</c> works on a full sheet. A matrix holds 32 bytes per cell.
    /// </summary>
    public const long MaxMatrixCells = CellReference.MaxRow;

    /// <summary>
    /// Cells of all the matrices one formula allocates, counted as they are created whether or not an earlier
    /// one is still alive: eight times <see cref="MaxMatrixCells"/>, about 268 MB, however many operands the
    /// formula has.
    /// </summary>
    public const long MaxFormulaCells = 8 * MaxMatrixCells;
}

/// <summary>
/// A formula went past a limit of <see cref="FormulaLimits"/>: the cell is not calculated, it keeps its stored
/// value and is reported, as for a function the engine does not compute.
/// </summary>
internal sealed class FormulaLimitException(string message) : Exception(message)
{
    public static FormulaLimitException Nesting() =>
        new(string.Create(CultureInfo.InvariantCulture, $"nested more than {FormulaLimits.MaxNesting} levels deep"));

    public static FormulaLimitException Cells(long needed, long allowed) =>
        new(string.Create(CultureInfo.InvariantCulture, $"needs {needed:N0} cells in memory, more than the {allowed:N0} allowed"));
}
