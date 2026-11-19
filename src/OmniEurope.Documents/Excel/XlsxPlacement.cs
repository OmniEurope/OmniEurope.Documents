// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;

namespace OmniEurope.Documents.Excel;

/// <summary>
/// Where a row or a cell of a worksheet part sits (ECMA-376 part 1, §18.3.1.73 and §18.3.1.4): the <c>r</c> attribute,
/// or the position after the previous one when the attribute is absent. An attribute that is present but names no row
/// or cell of a worksheet makes the workbook malformed; it is never replaced by a guess.
/// </summary>
internal static class XlsxPlacement
{
    /// <summary>The row of a <c>row</c> element given its <c>r</c> attribute and the previous row.</summary>
    /// <exception cref="DocumentFormatException">The attribute is present and is not a row from 1 to the last row.</exception>
    public static int Row(string? attribute, int previousRow, string sheetName)
    {
        if (attribute is null)
        {
            return previousRow < CellReference.MaxRow ? previousRow + 1 : throw Malformed($"row {previousRow + 1}", sheetName);
        }

        return int.TryParse(attribute, NumberStyles.None, CultureInfo.InvariantCulture, out var row) && row is >= 1 and <= CellReference.MaxRow
            ? row
            : throw Malformed($"row '{attribute}'", sheetName);
    }

    /// <summary>The column of a <c>c</c> element in <paramref name="row"/> given its <c>r</c> attribute and the previous column.</summary>
    /// <exception cref="DocumentFormatException">The attribute is present and is not a cell of a worksheet, or the cell lies past the last column.</exception>
    public static (int Row, int Column) Cell(string? attribute, int row, int previousColumn, string sheetName)
    {
        if (attribute is null)
        {
            return previousColumn < CellReference.MaxColumn ? (row, previousColumn + 1) : throw Malformed($"cell {previousColumn + 1} of row {row}", sheetName);
        }

        return CellReference.TryParse(attribute, out var cellRow, out var column)
            ? (cellRow, column)
            : throw Malformed($"cell reference '{attribute}'", sheetName);
    }

    private static DocumentFormatException Malformed(string what, string sheetName) =>
        new($"The workbook is not valid: {what} in worksheet '{sheetName}' is not a position of a worksheet (columns A to XFD, rows 1 to {CellReference.MaxRow}).");
}
