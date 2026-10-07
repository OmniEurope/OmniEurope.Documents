// SPDX-License-Identifier: EUPL-1.2
using System.Buffers;
using System.Globalization;

namespace OmniEurope.Documents.Csv;

/// <summary>
/// Protection against formula injection (CSV injection): a spreadsheet opening a CSV runs a cell that
/// starts with <c>=</c>, <c>+</c>, <c>-</c> or <c>@</c> as a formula, also after leading white space or a
/// control character. Such a value is prefixed with an apostrophe so it stays text. A plain number
/// (<c>-12.5</c>) is left alone: it cannot run anything.
/// </summary>
public static class CsvFormula
{
    private static readonly SearchValues<char> CellBreaks = SearchValues.Create([(char)9, (char)10, (char)13]);

    private static readonly SearchValues<char> FormulaSigns = SearchValues.Create("=+-@");

    /// <summary>True when a spreadsheet would read <paramref name="value"/> as a formula.</summary>
    public static bool IsDangerous(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return false;
        }

        // A tab or line break first can start a new cell or row in a spreadsheet.
        if (CellBreaks.Contains(value[0]))
        {
            return true;
        }

        var start = 0;
        while (start < value.Length && (char.IsWhiteSpace(value[start]) || char.IsControl(value[start])))
        {
            start++;
        }

        // A formula sign, unless the whole value is a plain number such as -12.5 or +3.
        return start < value.Length && FormulaSigns.Contains(value[start])
            && !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }
    /// <summary>Returns <paramref name="value"/> prefixed with an apostrophe when it is dangerous.</summary>
    public static string Neutralize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return IsDangerous(value) ? "'" + value : value;
    }
}
