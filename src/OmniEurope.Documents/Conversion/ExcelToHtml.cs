// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Conversion.ExcelHtml;
using OmniEurope.Documents.Conversion.WordHtml;
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

    /// <summary>
    /// The address of the style sheet when the consumer serves it as a file (its text is
    /// <see cref="ExcelHtmlResult.StyleSheet"/>, from <see cref="ExcelToHtml.ConvertWithStyleSheet(XlsxWorkbook, ExcelHtmlOptions?)"/>):
    /// the page then links it with <c>&lt;link rel="stylesheet"&gt;</c> and embeds no <c>style</c> element, which a
    /// Content-Security-Policy <c>style-src 'self'</c> allows. Null by default: the sheet is embedded.
    /// </summary>
    public string? StyleSheetHref { get; init; }

    /// <summary>
    /// The nonce written on the <c>style</c> element (or on the <c>link</c> element of <see cref="StyleSheetHref"/>),
    /// for a Content-Security-Policy <c>style-src 'nonce-…'</c>. Only a CSP <c>base64-value</c> is accepted: ASCII
    /// letters, digits, <c>+ / - _</c>, then at most two <c>=</c>; anything else throws
    /// <see cref="ArgumentException"/>. Null by default: no nonce.
    /// </summary>
    public string? StyleSheetNonce { get; init => field = HtmlPageStyle.CheckNonce(value); }
}

/// <summary>A converted workbook: the HTML page and its style sheet, the one it embeds or links
/// (<see cref="ExcelHtmlOptions.StyleSheetHref"/>).</summary>
public sealed record ExcelHtmlResult(string Html, string StyleSheet)
{
    /// <summary>What the workbook holds that the page does not carry faithfully (a drawing that could not be read
    /// and was skipped...), in English; empty when nothing was lost.</summary>
    public IReadOnlyList<string> Gaps { get; init; } = [];

    /// <summary>The <see cref="Gaps"/> in their order, each with its code, to describe them in another language
    /// (<see cref="DocumentGap.Describe"/>).</summary>
    public IReadOnlyList<DocumentGap> GapDetails { get => field ?? Gaps.Select(DocumentGap.FromText).ToList(); init; }
}

/// <summary>
/// A workbook to one HTML page (UTF-8, style sheet embedded or linked, nothing else fetched): each sheet's used range
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
    public static string Convert(XlsxWorkbook workbook, ExcelHtmlOptions? options = null) => ConvertWithStyleSheet(workbook, options).Html;

    /// <summary>Loads an .xlsx package and converts it.</summary>
    public static string Convert(byte[] xlsx, ExcelHtmlOptions? options = null) => Convert(XlsxWorkbook.Load(xlsx), options);

    /// <summary>Converts a loaded workbook and gives the style sheet apart too, for a consumer that serves it as a
    /// file (<see cref="ExcelHtmlOptions.StyleSheetHref"/>).</summary>
    public static ExcelHtmlResult ConvertWithStyleSheet(XlsxWorkbook workbook, ExcelHtmlOptions? options = null)
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

    /// <summary>Loads an .xlsx package, converts it and gives the style sheet apart too.</summary>
    public static ExcelHtmlResult ConvertWithStyleSheet(byte[] xlsx, ExcelHtmlOptions? options = null) => ConvertWithStyleSheet(XlsxWorkbook.Load(xlsx), options);
}
