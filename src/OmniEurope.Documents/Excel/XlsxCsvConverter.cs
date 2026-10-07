// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Csv;

namespace OmniEurope.Documents.Excel;

/// <summary>Conversions between worksheets and CSV.</summary>
public static class XlsxCsvConverter
{
    /// <summary>
    /// Writes the used range of <paramref name="sheet"/> as CSV, each cell as Excel displays it (number
    /// formats applied with <paramref name="culture"/>, invariant by default). Formulas give their last result.
    /// </summary>
    public static void WriteCsv(XlsxWorksheet sheet, TextWriter output, CsvWriterOptions? options = null, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(output);
        culture ??= CultureInfo.InvariantCulture;
        using var csv = new CsvWriter(output, options, leaveOpen: true);
        if (sheet.UsedRange is not { } range)
        {
            return;
        }

        for (var row = range.FirstRow; row <= range.LastRow; row++)
        {
            for (var column = range.FirstColumn; column <= range.LastColumn; column++)
            {
                csv.WriteField(sheet.FindCell(row, column)?.FormatValue(culture) ?? string.Empty);
            }

            csv.NextRecord();
        }
    }

    /// <summary>Returns the used range of <paramref name="sheet"/> as CSV text.</summary>
    public static string ToCsv(XlsxWorksheet sheet, CsvWriterOptions? options = null, CultureInfo? culture = null)
    {
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        WriteCsv(sheet, text, options, culture);
        return text.ToString();
    }

    /// <summary>
    /// Builds a one-sheet workbook from CSV records. Values stay text unless <paramref name="numberCulture"/> is
    /// given, in which case fields that parse as numbers in that culture become numbers. The header row (when
    /// the reader has one) is written in bold, frozen and given an auto-filter.
    /// </summary>
    public static XlsxWorkbook FromCsv(CsvReader reader, string sheetName = "Sheet1", CultureInfo? numberCulture = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet(sheetName);
        var row = 0;
        if (reader.Headers.Count > 0)
        {
            row++;
            for (var i = 0; i < reader.Headers.Count; i++)
            {
                var cell = sheet.Cell(row, i + 1);
                cell.Value = reader.Headers[i];
                cell.Style = cell.Style with { Bold = true };
            }

            sheet.FreezePanes(1);
            sheet.AutoFilter = new XlsxRange(1, 1, 1, reader.Headers.Count);
        }

        while (reader.Read())
        {
            row++;
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var text = reader.GetString(i);
                sheet.Cell(row, i + 1).Value = numberCulture is not null
                    && double.TryParse(text, NumberStyles.Number | NumberStyles.AllowExponent, numberCulture, out var number)
                    ? number
                    : text;
            }
        }

        if (sheet.AutoFilter is { } filter && row > 1)
        {
            sheet.AutoFilter = filter with { LastRow = row };
        }

        sheet.AutoFitColumns();
        return workbook;
    }
}
