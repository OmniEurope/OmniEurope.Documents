// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Excel;

/// <summary>Options of <see cref="XlsxWorkbook.Recalculate"/>.</summary>
public sealed record XlsxRecalculationOptions
{
    /// <summary>The date and time TODAY and NOW give; the local clock when null.</summary>
    public DateTime? Now { get; init; }

    /// <summary>
    /// Most cells one matrix (a range read, an array, the result of an operation on arrays) may hold. Defaults to
    /// 1 048 576, one whole column of a sheet, so that <c>SUM(A:A)</c> works. A matrix takes 32 bytes per cell
    /// (32 MB at the default). A formula that needs a bigger matrix is not calculated: it keeps the result the file
    /// held and is listed in <see cref="XlsxRecalculation.Unsupported"/>. Array constants typed in a formula are
    /// bounded by the default whatever this value is.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public long MaxMatrixCells
    {
        get => _maxMatrixCells ?? DefaultMaxMatrixCells;
        init => _maxMatrixCells = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The matrix budget must be positive.");
    }

    /// <summary>
    /// Most cells of all the matrices one formula allocates, counted as they are created whether or not an earlier
    /// one is still alive. Defaults to eight times 1 048 576 (about 268 MB at 32 bytes per cell), however many
    /// operands the formula has. It must not be below <see cref="MaxMatrixCells"/>, which
    /// <see cref="XlsxWorkbook.Recalculate"/> checks. A formula over it is not calculated, as for
    /// <see cref="MaxMatrixCells"/>. The nesting depth (256 levels) is not configurable: it protects the stack.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public long MaxFormulaCells
    {
        get => _maxFormulaCells ?? DefaultMaxFormulaCells;
        init => _maxFormulaCells = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The formula budget must be positive.");
    }

    private const long DefaultMaxMatrixCells = 1_048_576;
    private const long DefaultMaxFormulaCells = 8 * DefaultMaxMatrixCells;

    private readonly long? _maxMatrixCells;
    private readonly long? _maxFormulaCells;

    internal void Validate()
    {
        if (MaxFormulaCells < MaxMatrixCells)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxFormulaCells), MaxFormulaCells, "The formula budget must not be below the matrix budget.");
        }
    }
}

/// <summary>What a recalculation did.</summary>
/// <param name="Computed">Formulas computed and stored.</param>
/// <param name="Unsupported">Formulas the engine does not compute, or that nest more than 256 levels or hold
/// more cells in memory than the options allow (<see cref="XlsxRecalculationOptions.MaxMatrixCells"/>, by default a
/// range or array over about a million cells), as <c>Sheet!A1: reason</c>;
/// they keep the result the file held.</param>
public sealed record XlsxRecalculation(int Computed, IReadOnlyList<string> Unsupported);

/// <summary>Formulas that depend on their own result: recalculation refuses them.</summary>
public sealed class XlsxCircularReferenceException : InvalidOperationException
{
    /// <summary>Creates the exception for the cells caught in a cycle or waiting on one.</summary>
    public XlsxCircularReferenceException(IReadOnlyList<string> cells)
        : base($"Circular reference: {string.Join(", ", cells.Take(10))}{(cells.Count > 10 ? ", ..." : string.Empty)}.")
    {
        Cells = cells;
    }

    /// <summary>The cells (<c>Sheet!A1</c>) in a cycle or depending on one.</summary>
    public IReadOnlyList<string> Cells { get; }
}
