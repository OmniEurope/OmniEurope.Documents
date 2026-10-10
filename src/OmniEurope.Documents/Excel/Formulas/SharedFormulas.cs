// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Excel.Formulas;

/// <summary>
/// The shared formulas of a worksheet (ECMA-376 part 1, §18.3.1.40): the first cell of a group holds the formula,
/// the other cells of the group only its index, and their formula is the first one moved to their place.
/// </summary>
internal sealed class SharedFormulas
{
    private readonly Dictionary<string, (string Text, int Row, int Column)> _groups = new(StringComparer.Ordinal);

    /// <summary>
    /// The formula of a cell from its <c>f</c> element: its own text, or the text of its group moved to it; null
    /// for a cell of a group whose first formula was not met.
    /// </summary>
    public string? Resolve(string? text, string? group, int row, int column)
    {
        if (group is null)
        {
            return text;
        }

        if (!string.IsNullOrEmpty(text))
        {
            _groups.TryAdd(group, (text, row, column));
            return text;
        }

        return _groups.TryGetValue(group, out var first) ? FormulaShifter.Shift(first.Text, row - first.Row, column - first.Column) : null;
    }
}
