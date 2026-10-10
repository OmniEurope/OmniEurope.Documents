// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OmniEurope.Documents.Excel.Formulas;

/// <summary>
/// Moves the relative references of a formula by a number of rows and columns, as copying the formula to another
/// cell does: <c>B1*2</c> moved one row down is <c>B2*2</c>, <c>$B$1</c> stays. Text literals, quoted sheet
/// names and table references are copied unchanged; a reference moved off the sheet becomes <c>#REF!</c>.
/// </summary>
internal static class FormulaShifter
{
    private const RegexOptions Options = RegexOptions.CultureInvariant;

    // A reference ends where no name, call or sheet name goes on (LOG10( is a function, AB1! a sheet).
    private const string End = "(?![A-Za-z0-9_.(!])";
    private const string CellPattern = @"(\$?)([A-Za-z]{1,3})(\$?)([0-9]{1,7})";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    // Each form of reference with how it moves; a range of cells is tried before a single cell.
    private static readonly (Regex Pattern, Func<Match, int, int, string?> Move)[] Forms =
    [
        (new(@"\G" + CellPattern + ":" + CellPattern + End, Options, Timeout), (m, rows, columns) => Range(CellAt(m, 1, rows, columns), CellAt(m, 5, rows, columns))),
        (new(@"\G(\$?)([A-Za-z]{1,3}):(\$?)([A-Za-z]{1,3})" + End, Options, Timeout), (m, _, columns) => Range(Column(m, 1, columns), Column(m, 3, columns))),
        (new(@"\G(\$?)([0-9]{1,7}):(\$?)([0-9]{1,7})" + End, Options, Timeout), (m, rows, _) => Range(Row(m, 1, rows), Row(m, 3, rows))),
        (new(@"\G" + CellPattern + End, Options, Timeout), (m, rows, columns) => CellAt(m, 1, rows, columns)),
    ];

    public static string Shift(string formula, int rows, int columns)
    {
        var builder = new StringBuilder(formula.Length + 8);
        var i = 0;
        while (i < formula.Length)
        {
            var c = formula[i];
            if (c is '"' or '\'' or '[')
            {
                var length = c == '[' ? Bracketed(formula, i) : Quoted(formula, i);
                builder.Append(formula, i, length);
                i += length;
            }
            else if ((i == 0 || !IsNameCharacter(formula[i - 1])) && Reference(formula, i, rows, columns, builder) is > 0 and var moved)
            {
                i += moved;
            }
            else
            {
                builder.Append(c);
                i++;
            }
        }

        return builder.ToString();
    }

    private static bool IsNameCharacter(char c) => char.IsLetterOrDigit(c) || c is '_' or '.' or '\\' or '$';

    // The length of "text" or 'sheet' from position, a doubled quote standing for one.
    private static int Quoted(string formula, int start)
    {
        var quote = formula[start];
        var i = start + 1;
        while (i < formula.Length)
        {
            if (formula[i] != quote)
            {
                i++;
            }
            else if (i + 1 < formula.Length && formula[i + 1] == quote)
            {
                i += 2;
            }
            else
            {
                return i + 1 - start;
            }
        }

        return formula.Length - start;
    }

    // The length of a table reference part, [Column] or [[#This Row],[Column]].
    private static int Bracketed(string formula, int start)
    {
        var depth = 0;
        for (var i = start; i < formula.Length; i++)
        {
            depth += formula[i] switch { '[' => 1, ']' => -1, _ => 0 };
            if (depth == 0)
            {
                return i + 1 - start;
            }
        }

        return formula.Length - start;
    }

    // Writes the moved reference found at position and returns its length; 0 when none starts there.
    // A range moved off the sheet is #REF! as a whole, as Excel writes it.
    private static int Reference(string formula, int start, int rows, int columns, StringBuilder builder)
    {
        foreach (var (pattern, move) in Forms)
        {
            var match = pattern.Match(formula, start);
            if (match.Success)
            {
                builder.Append(move(match, rows, columns) ?? "#REF!");
                return match.Length;
            }
        }

        return 0;
    }

    private static string? Range(string? first, string? last) => first is null || last is null ? null : $"{first}:{last}";

    // The cell whose $ marker and column letters start at group index.
    private static string? CellAt(Match match, int index, int rows, int columns) =>
        Column(match, index, columns) is { } column && Row(match, index + 2, rows) is { } row ? column + row : null;

    // The group at index is the $ marker, the next one the column letters.
    private static string? Column(Match match, int index, int delta)
    {
        var letters = match.Groups[index + 1].Value;
        if (match.Groups[index].Length > 0)
        {
            return "$" + letters;
        }

        var column = letters.Aggregate(0, (total, letter) => (total * 26) + (char.ToUpperInvariant(letter) - 'A' + 1)) + delta;
        return column is >= 1 and <= CellReference.MaxColumn ? CellReference.ColumnName(column) : null;
    }

    private static string? Row(Match match, int index, int delta)
    {
        var digits = match.Groups[index + 1].Value;
        if (match.Groups[index].Length > 0)
        {
            return "$" + digits;
        }

        var row = int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture) + delta;
        return row is >= 1 and <= CellReference.MaxRow ? row.ToString(CultureInfo.InvariantCulture) : null;
    }
}
