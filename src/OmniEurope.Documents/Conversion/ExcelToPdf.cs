// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Fonts;
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
/// empty neighbouring cells and clipped where it meets a filled one, as Excel shows them.
/// </summary>
public static class ExcelToPdf
{
    private const double DefaultColumnWidth = 8.43;

    // Left and right cell margins together, in points.
    private const double CellPadding = 5;

    private static readonly WordBorder Grid = new("single", 0.25, 0, "BFBFBF");

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
            var table = sheet.UsedRange is { } range ? Table(sheet, range, options) : null;
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

    private static WordTable Table(XlsxWorksheet sheet, XlsxRange range, ExcelPdfOptions options)
    {
        var table = new WordTable
        {
            Properties = new WordTableProperties
            {
                Borders = options.Gridlines ? new WordTableBorders(Grid, Grid, Grid, Grid, Grid, Grid) : null,
                CellMargins = new WordCellMargins(1, 2.5, 1, 2.5),
                FixedLayout = true,
            },
        };
        for (var c = range.FirstColumn; c <= range.LastColumn; c++)
        {
            var characters = sheet.ColumnWidths.TryGetValue(c, out var width) ? width : DefaultColumnWidth;
            table.Columns.Add(((characters * 7) + 5) * 0.75);
        }

        var merges = sheet.MergedRanges;
        for (var r = range.FirstRow; r <= range.LastRow; r++)
        {
            var row = new WordTableRow { Properties = new WordTableRowProperties { IsHeader = r < range.FirstRow + sheet.FrozenRows ? true : null, CantSplit = true } };
            foreach (var segment in ExcelRowLayout.Segments(sheet, range, r, table.Columns, c => Width(c, c.FormatValue(options.Culture), options) + CellPadding))
            {
                var c = segment.Owner;
                var merge = merges.FirstOrDefault(m => r >= m.FirstRow && r <= m.LastRow && c >= m.FirstColumn && c <= m.LastColumn);
                var room = table.Columns.Skip(segment.First - range.FirstColumn).Take(segment.Last - segment.First + 1).Sum() - CellPadding;
                var cell = Cell(sheet.FindCell(r, c), merge, r, room, options);
                if (merge == default && segment.Last > segment.First)
                {
                    cell.Properties = cell.Properties with { GridSpan = segment.Last - segment.First + 1 };
                }

                row.Cells.Add(cell);
            }

            table.Rows.Add(row);
        }

        return table;
    }

    private static WordTableCell Cell(XlsxCell? cell, XlsxRange merge, int r, double room, ExcelPdfOptions options)
    {
        var style = cell?.Style ?? XlsxStyle.Default;
        var properties = CellProperties(style, merge, r);
        var result = new WordTableCell { Properties = properties };
        if (properties.VerticalMerge == WordVerticalMerge.Continue)
        {
            return result;
        }

        var alignment = Alignment(style, cell);
        var paragraph = new WordParagraph { Properties = new WordParagraphProperties { Alignment = alignment } };
        var text = cell?.FormatValue(options.Culture) ?? string.Empty;
        if (cell?.ValueType is XlsxValueType.Number or XlsxValueType.DateTime)
        {
            text = Fit(cell, text, room, options);
        }
        else if (cell is not null && ExcelRowLayout.RunsOn(cell) && Width(cell, text, options) is var width && width > room)
        {
            // One line, clipped at the cell edges (the row layout already let it run on where it could).
            result.Properties = properties with { Overflow = new WordCellOverflow(width + 1, alignment) };
        }

        if (text.Length > 0)
        {
            paragraph.AddText(text, Run(style));
        }

        result.Blocks.Add(paragraph);
        return result;
    }

    // Excel never wraps a number: under the General format it drops decimal digits until the value fits,
    // otherwise (or when even one digit does not fit) the cell fills with '#'.
    private static string Fit(XlsxCell cell, string text, double room, ExcelPdfOptions options)
    {
        bool Fits(string candidate) => Width(cell, candidate, options) <= room;
        if (Fits(text))
        {
            return text;
        }

        if (cell.ValueType == XlsxValueType.Number && cell.Style.NumberFormat.Equals("General", StringComparison.OrdinalIgnoreCase))
        {
            var value = (double)cell.Value!;
            for (var digits = 9; digits >= 1; digits--)
            {
                var shorter = value.ToString("G" + digits.ToString(CultureInfo.InvariantCulture), options.Culture);
                if (Fits(shorter))
                {
                    return shorter;
                }
            }
        }

        var count = Math.Max(1, (int)(room / Width(cell, "#", options)));
        return new string('#', count);
    }

    // The width of the text on one line in the cell's font, as the PDF draws it.
    private static double Width(XlsxCell cell, string text, ExcelPdfOptions options) =>
        (options.Pdf?.Fonts ?? FontLibrary.Default).Resolve(cell.Style.FontName, cell.Style.Bold, cell.Style.Italic).MeasureText(text, cell.Style.FontSize);

    private static WordTableCellProperties CellProperties(XlsxStyle style, XlsxRange merge, int r)
    {
        var merged = merge != default;
        return new WordTableCellProperties
        {
            GridSpan = merged && merge.LastColumn > merge.FirstColumn ? merge.LastColumn - merge.FirstColumn + 1 : null,
            VerticalMerge = VerticalMerge(merged, merge, r),
            Shading = style.FillColor is { } fill ? XlsxStyle.NormalizeColor(fill) : null,
            VerticalAlignment = style.VerticalAlignment switch
            {
                XlsxVerticalAlignment.Top => WordCellAlignment.Top,
                XlsxVerticalAlignment.Center => WordCellAlignment.Center,
                _ => WordCellAlignment.Bottom,
            },
            Borders = style.Border ? Borders(style.BorderColor) : null,
        };
    }

    private static WordVerticalMerge? VerticalMerge(bool merged, XlsxRange merge, int r)
    {
        if (!merged || merge.LastRow == merge.FirstRow)
        {
            return null;
        }

        return r == merge.FirstRow ? WordVerticalMerge.Restart : WordVerticalMerge.Continue;
    }

    private static WordTableBorders Borders(string? color)
    {
        var line = new WordBorder("single", 0.5, 0, color is null ? "000000" : XlsxStyle.NormalizeColor(color));
        return new WordTableBorders(line, line, line, line);
    }

    // "General" alignment puts numbers and dates on the right, text on the left, booleans and errors in the centre.
    private static WordAlignment Alignment(XlsxStyle style, XlsxCell? cell) => style.HorizontalAlignment switch
    {
        XlsxHorizontalAlignment.Left => WordAlignment.Left,
        XlsxHorizontalAlignment.Center => WordAlignment.Center,
        XlsxHorizontalAlignment.Right => WordAlignment.Right,
        XlsxHorizontalAlignment.Justify => WordAlignment.Justify,
        _ => cell?.ValueType switch
        {
            XlsxValueType.Number or XlsxValueType.DateTime => WordAlignment.Right,
            XlsxValueType.Boolean or XlsxValueType.Error => WordAlignment.Center,
            _ => WordAlignment.Left,
        },
    };

    private static WordRunProperties Run(XlsxStyle style) => new()
    {
        Font = style.FontName,
        FontSize = style.FontSize,
        Bold = style.Bold ? true : null,
        Italic = style.Italic ? true : null,
        Underline = style.Underline ? WordUnderline.Single : null,
        Strike = style.Strike ? true : null,
        Color = style.FontColor is { } color ? XlsxStyle.NormalizeColor(color) : null,
    };
}
