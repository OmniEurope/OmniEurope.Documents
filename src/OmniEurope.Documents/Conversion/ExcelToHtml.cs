// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Conversion.ExcelHtml;
using OmniEurope.Documents.Excel;

namespace OmniEurope.Documents.Conversion;

/// <summary>Options of <see cref="ExcelToHtml"/>.</summary>
public sealed record ExcelHtmlOptions
{
    /// <summary>Culture used to display numbers and dates (invariant by default).</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    /// <summary>Each sheet's table follows its name as a heading.</summary>
    public bool SheetTitles { get; init; } = true;

    /// <summary>Light grid lines between cells, as on screen.</summary>
    public bool Gridlines { get; init; } = true;

    /// <summary>Most cells the used ranges of all sheets may hold together, empty cells included. A larger
    /// workbook, such as a sheet with values in <c>A1</c> and <c>XFD1048576</c> only, throws
    /// <see cref="DocumentFormatException"/> before anything is written. Default 1,000,000.</summary>
    public long MaxCells { get; init; } = 1_000_000;
}

/// <summary>
/// A workbook to one standalone HTML page (UTF-8, styles embedded, nothing fetched): each sheet's used range
/// becomes a table of the values as Excel displays them (number formats applied with
/// <see cref="ExcelHtmlOptions.Culture"/>, formulas giving their last result), with the column widths of the
/// sheet (<c>7w + 5</c> pixels for a width of <c>w</c> characters), merged cells as <c>colspan</c> and
/// <c>rowspan</c>, and the font, size, weight, slant, underline, strike, colour, fill, alignment (numbers and
/// dates on the right under the General alignment), wrapping and border of each cell. Styles are classes of an
/// embedded style sheet, so a page sanitised with the default <see cref="Html.HtmlSanitizerOptions"/> keeps its
/// structure and its classes. All workbook text (values, sheet names, title) is encoded and font names are
/// reduced to letters, digits, spaces, hyphens and underscores: nothing in the workbook can add markup, style
/// rules or script. Not rendered: row heights, hidden rows and columns, text running on into empty neighbouring
/// cells (it is clipped to its cell), colours chosen by a number format (<c>[Red]</c>), conditional formats,
/// pictures and charts.
/// </summary>
public static class ExcelToHtml
{
    /// <summary>The class of every sheet table.</summary>
    public const string TableClass = "omni-sheet";

    /// <summary>Converts a loaded workbook.</summary>
    public static string Convert(XlsxWorkbook workbook, ExcelHtmlOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        options ??= new ExcelHtmlOptions();
        var cells = workbook.Worksheets.Sum(s => s.UsedRange is { } used ? (long)(used.LastRow - used.FirstRow + 1) * (used.LastColumn - used.FirstColumn + 1) : 0);
        if (cells > options.MaxCells)
        {
            throw new DocumentFormatException(string.Create(CultureInfo.InvariantCulture, $"The used ranges hold {cells:N0} cells, more than the {options.MaxCells:N0} allowed by {nameof(ExcelHtmlOptions.MaxCells)}."));
        }

        return new ExcelHtmlWriter(options).Write(workbook);
    }

    /// <summary>Loads an .xlsx package and converts it.</summary>
    public static string Convert(byte[] xlsx, ExcelHtmlOptions? options = null) => Convert(XlsxWorkbook.Load(xlsx), options);
}
