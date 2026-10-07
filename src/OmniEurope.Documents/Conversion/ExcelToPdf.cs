// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion;

/// <summary>Options of <see cref="ExcelToPdf"/>.</summary>
public sealed record ExcelPdfOptions
{
    /// <summary>Culture used to display numbers and dates (invariant by default).</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    /// <summary>Each sheet starts with its name as a heading.</summary>
    public bool SheetTitles { get; init; } = true;

    /// <summary>Light grid lines between cells, as on screen.</summary>
    public bool Gridlines { get; init; } = true;

    /// <summary>Options of the PDF rendering.</summary>
    public WordPdfOptions? Pdf { get; init; }
}

/// <summary>
/// A workbook to PDF: each sheet's used range becomes a table in its own section (landscape when it is wider
/// than a portrait page), with the displayed values, fonts, colours, fills, alignment, borders, merged cells
/// and column widths of the sheet. Frozen rows repeat at the top of each page. A number too wide for its
/// column is shortened or filled with '#', and text that does not wrap stays on one line, running on into
/// empty neighbouring cells (a fill or a border does not stop it) and is clipped at the first one holding a
/// value; text that would pass the edge of the printed range wraps in its cell instead, so nothing is lost.
/// </summary>
public static class ExcelToPdf
{
    /// <summary>Converts a loaded workbook.</summary>
    public static WordPdfResult Convert(XlsxWorkbook workbook, ExcelPdfOptions? options = null) =>
        WordToPdf.Convert(ToWord(workbook, options), options?.Pdf);

    /// <summary>Loads an .xlsx package and converts it.</summary>
    public static WordPdfResult Convert(byte[] xlsx, ExcelPdfOptions? options = null) => Convert(XlsxWorkbook.Load(xlsx), options);

    /// <summary>The Word document the PDF is laid out from.</summary>
    public static WordDocument ToWord(XlsxWorkbook workbook, ExcelPdfOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        options ??= new ExcelPdfOptions();
        var document = new WordDocument { Information = new WordInformation { Title = workbook.Title, Author = workbook.Author } };
        document.Styles.DefaultParagraphProperties = new WordParagraphProperties { SpacingAfter = 0, LineSpacing = 1, LineSpacingRule = WordLineSpacingRule.Multiple };
        for (var i = 0; i < workbook.Worksheets.Count; i++)
        {
            var sheet = workbook.Worksheets[i];
            var table = sheet.UsedRange is { } range ? new ExcelSheetTable(sheet, range, options).Build() : null;
            var width = table?.Columns.Sum() ?? 0;
            var page = width > WordPageSetup.A4.ContentWidth ? WordPageSetup.A4.ToLandscape() : WordPageSetup.A4;
            if (i == 0)
            {
                document.Sections[0].Page = page;
            }
            else
            {
                document.AddSection(page);
            }

            if (options.SheetTitles)
            {
                document.AddParagraph(sheet.Name, "Heading2");
            }

            if (table is not null)
            {
                document.AddTable(table);
            }
        }

        return document;
    }
}
