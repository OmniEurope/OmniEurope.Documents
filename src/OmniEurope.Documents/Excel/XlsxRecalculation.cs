// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Excel;

/// <summary>Options of <see cref="XlsxWorkbook.Recalculate"/>.</summary>
public sealed record XlsxRecalculationOptions
{
    /// <summary>The date and time TODAY and NOW give; the local clock when null.</summary>
    public DateTime? Now { get; init; }
}

/// <summary>What a recalculation did.</summary>
/// <param name="Computed">Formulas computed and stored.</param>
/// <param name="Unsupported">Formulas the engine does not compute, as <c>Sheet!A1: reason</c>; they keep the
/// result the file held.</param>
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
