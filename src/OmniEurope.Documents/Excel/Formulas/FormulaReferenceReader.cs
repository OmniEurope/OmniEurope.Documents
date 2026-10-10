// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;

namespace OmniEurope.Documents.Excel.Formulas;

/// <summary>
/// Reads the references of a formula: <c>A1</c>, <c>$A$1</c>, <c>A1:B2</c>, whole columns <c>A:C</c> and whole
/// rows <c>1:3</c>, each corner optionally absolute, and the quoted sheet names that may precede them.
/// </summary>
internal static class FormulaReferenceReader
{
    /// <summary>The reference at the cursor, moved past; or null with the cursor unchanged.</summary>
    public static ReferenceNode? TryRead(FormulaCursor cursor, string? sheet)
    {
        var start = cursor.Position;
        if (ReadArea(cursor) is { } area && !FollowsName(cursor))
        {
            return new ReferenceNode(area with { Sheet = sheet });
        }

        cursor.Position = start;
        return null;
    }

    /// <summary>A sheet name between quotes, a doubled quote standing for one, followed by <c>!</c>.</summary>
    /// <exception cref="FormatException">The name is not closed by <c>'!</c>.</exception>
    public static string ReadQuotedSheet(FormulaCursor cursor)
    {
        cursor.Position++;
        var builder = new System.Text.StringBuilder();
        while (!cursor.AtEnd)
        {
            var c = cursor.Current;
            cursor.Position++;
            if (c != '\'')
            {
                builder.Append(c);
            }
            else if (cursor.Skip('\''))
            {
                builder.Append('\'');
            }
            else
            {
                return cursor.Skip('!') ? builder.ToString() : throw cursor.Error("unterminated sheet name");
            }
        }

        throw cursor.Error("unterminated sheet name");
    }

    // A cell or a range; a bare column (A) or row (3) is a name or a number, not a reference.
    private static FormulaArea? ReadArea(FormulaCursor cursor)
    {
        if (ReadCorner(cursor) is not { } first)
        {
            return null;
        }

        if (ReadSecondCorner(cursor, first) is { } second)
        {
            return Area(first, second);
        }

        return first.IsCell ? Area(first, first) : null;
    }

    // After ':' a corner of the same kind; otherwise the cursor goes back before ':'.
    private static Corner? ReadSecondCorner(FormulaCursor cursor, Corner first)
    {
        var colon = cursor.Position;
        if (cursor.Skip(':') && ReadCorner(cursor) is { } second && second.SameKind(first))
        {
            return second;
        }

        cursor.Position = colon;
        return null;
    }

    // One corner: letters and digits (a cell), letters alone (a column) or digits alone (a row).
    private static Corner? ReadCorner(FormulaCursor cursor)
    {
        cursor.Skip('$');
        var lettersStart = cursor.Position;
        var letters = cursor.SkipWhile(char.IsAsciiLetter, 3);
        if (letters > 0)
        {
            cursor.Skip('$');
        }

        var digitsStart = cursor.Position;
        var digits = cursor.SkipWhile(char.IsAsciiDigit, 8);
        var column = ColumnNumber(cursor.Text.AsSpan(lettersStart, letters));
        var row = digits == 0 ? 0 : int.Parse(cursor.Text.AsSpan(digitsStart, digits), NumberStyles.None, CultureInfo.InvariantCulture);
        var valid = letters + digits > 0 && column <= CellReference.MaxColumn && row <= CellReference.MaxRow && (digits == 0 || row > 0);
        return valid ? new Corner(row, column) : null;
    }

    private static int ColumnNumber(ReadOnlySpan<char> letters)
    {
        var column = 0;
        foreach (var letter in letters)
        {
            column = (column * 26) + (char.ToUpperInvariant(letter) - 'A' + 1);
        }

        return column;
    }

    // A1B or A1( continues a name or a function, Sheet1! a sheet name: not a reference.
    private static bool FollowsName(FormulaCursor cursor) => char.IsLetterOrDigit(cursor.Current) || cursor.Current is '(' or '_' or '!' or '.';

    private static FormulaArea Area(Corner first, Corner second)
    {
        var top = first.IsColumn ? 1 : Math.Min(first.Row, second.Row);
        var bottom = first.IsColumn ? CellReference.MaxRow : Math.Max(first.Row, second.Row);
        var left = first.IsRow ? 1 : Math.Min(first.Column, second.Column);
        var right = first.IsRow ? CellReference.MaxColumn : Math.Max(first.Column, second.Column);
        return new FormulaArea(null, top, left, bottom, right);
    }

    /// <summary>A corner of a reference: row 0 for a whole column, column 0 for a whole row.</summary>
    private readonly record struct Corner(int Row, int Column)
    {
        public bool IsRow => Column == 0;

        public bool IsColumn => Row == 0;

        public bool IsCell => !IsRow && !IsColumn;

        public bool SameKind(Corner other) => IsRow == other.IsRow && IsColumn == other.IsColumn;
    }
}
