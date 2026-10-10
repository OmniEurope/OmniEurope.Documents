// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml;
using OmniEurope.Documents.Internal;

namespace OmniEurope.Documents.Excel;

/// <summary>Reads one worksheet part: cells, column widths, frozen panes, merges and the auto-filter.</summary>
internal sealed class XlsxSheetReader(XlsxWorksheet sheet, List<string> sharedStrings, List<XlsxStyle> styles)
{
    private readonly Formulas.SharedFormulas _sharedFormulas = new();
    private int _row;
    private int _column;

    public void Read(SafeZip zip, string path)
    {
        using var xml = zip.ReadXml(path) ?? throw new DocumentFormatException($"Worksheet part '{path}' is missing.");
        xml.Read();
        while (!xml.EOF)
        {
            if (xml.NodeType != XmlNodeType.Element)
            {
                xml.Read();
                continue;
            }

            switch (xml.LocalName)
            {
                case "pane":
                    ReadPane(xml);
                    break;
                case "col":
                    ReadColumn(xml);
                    break;
                case "row":
                    _row = int.TryParse(xml.GetAttribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out var r) ? r : _row + 1;
                    _column = 0;
                    break;
                case "c":
                    ReadCell(xml);
                    continue;
                case "mergeCell":
                    sheet.AddLoadedMerge(XlsxRange.Parse(xml.GetAttribute("ref") ?? "A1"));
                    break;
                case "autoFilter" when xml.GetAttribute("ref") is { } filter:
                    sheet.AutoFilter = XlsxRange.Parse(filter);
                    break;
            }

            xml.Read();
        }
    }

    private void ReadPane(XmlReader xml)
    {
        if (xml.GetAttribute("state") is "frozen" or "frozenSplit")
        {
            sheet.FreezePanes((int)XlsxPackageReader.ParseDouble(xml.GetAttribute("ySplit")), (int)XlsxPackageReader.ParseDouble(xml.GetAttribute("xSplit")));
        }
    }

    private void ReadColumn(XmlReader xml)
    {
        var min = (int)XlsxPackageReader.ParseDouble(xml.GetAttribute("min"));
        var max = (int)XlsxPackageReader.ParseDouble(xml.GetAttribute("max"));
        var width = XlsxPackageReader.ParseDouble(xml.GetAttribute("width"));

        // A whole-sheet default ("1:16384") is not a column setting worth one entry per column.
        if (min < 1 || max < min || max - min > 255 || width is <= 0 or > 255)
        {
            return;
        }

        for (var column = min; column <= max; column++)
        {
            sheet.SetColumnWidth(column, width);
        }
    }

    // Leaves the reader after the cell element.
    private void ReadCell(XmlReader xml)
    {
        if (!CellReference.TryParse(xml.GetAttribute("r"), out var row, out var column))
        {
            row = Math.Max(_row, 1);
            column = _column + 1;
        }

        _row = row;
        _column = column;
        var type = xml.GetAttribute("t") ?? "n";
        var style = int.TryParse(xml.GetAttribute("s"), NumberStyles.None, CultureInfo.InvariantCulture, out var s) && s < styles.Count ? styles[s] : XlsxStyle.Default;
        var (value, formula, group, inline) = xml.IsEmptyElement ? default : ReadContent(xml);
        formula = _sharedFormulas.Resolve(formula, group, row, column);
        xml.Read();
        if (value is null && formula is null && inline is null && style == XlsxStyle.Default)
        {
            return;
        }

        var cell = sheet.Cell(row, column);
        cell.Style = style;
        cell.Formula = string.IsNullOrEmpty(formula) ? null : formula;
        var (converted, kind) = Convert(type, value, inline, style);
        cell.SetLoadedValue(converted, kind);
    }

    // The <v> value, <f> formula (with its shared group) and <is> inline string of a <c> element; leaves the
    // reader on its end tag.
    private static (string? Value, string? Formula, string? Group, string? Inline) ReadContent(XmlReader xml)
    {
        string? value = null;
        string? formula = null;
        string? group = null;
        string? inline = null;
        var depth = xml.Depth;
        xml.Read();
        while (!xml.EOF && xml.Depth > depth)
        {
            var element = xml.NodeType == XmlNodeType.Element ? xml.LocalName : null;
            if (element == "f" && xml.GetAttribute("t") == "shared")
            {
                group = xml.GetAttribute("si");
            }

            if (element is "v" or "f")
            {
                var content = xml.ReadElementContentAsString();
                (value, formula) = element == "v" ? (content, formula) : (value, content);
                continue;
            }

            if (element == "is")
            {
                inline = XlsxPackageReader.ReadRichText(xml);
            }

            xml.Read();
        }

        return (value, formula, group, inline);
    }

    private (object? Value, XlsxValueType Type) Convert(string type, string? value, string? inline, XlsxStyle style) =>
        ConvertTyped(type, value, inline) ?? ConvertNumber(value, style);

    // Cell types other than n (number): shared, inline and formula strings, booleans, errors, ISO dates.
    private (object? Value, XlsxValueType Type)? ConvertTyped(string type, string? value, string? inline)
    {
        switch (type)
        {
            case "inlineStr":
                return (inline ?? string.Empty, XlsxValueType.Text);
            case "s":
                var index = (int)XlsxPackageReader.ParseDouble(value);
                return (index >= 0 && index < sharedStrings.Count ? sharedStrings[index] : string.Empty, XlsxValueType.Text);
            case "str":
                return (OpenXmlPackageWriter.UnescapeOfficeText(value ?? string.Empty), XlsxValueType.Text);
            case "b":
                return (value is "1" or "true", XlsxValueType.Boolean);
            case "e":
                return (new XlsxError(value ?? "#N/A"), XlsxValueType.Error);
            case "d":
                return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
                    ? (date, XlsxValueType.DateTime)
                    : (value ?? string.Empty, XlsxValueType.Text);
            default:
                return null;
        }
    }

    // A number, read as a date when the number format shows one.
    private (object? Value, XlsxValueType Type) ConvertNumber(string? value, XlsxStyle style)
    {
        if (value is null)
        {
            return (null, XlsxValueType.Empty);
        }

        var number = XlsxPackageReader.ParseDouble(value);
        // A duration ([h]:mm) stays a number of days, as a TimeSpan value is stored.
        if (NumberFormatter.IsDateFormat(style.NumberFormat) && !NumberFormatter.IsDurationFormat(style.NumberFormat) && number <= 2958465 && ExcelDate.IsInRange(number, sheet.Workbook.Date1904))
        {
            return (ExcelDate.FromSerial(number, sheet.Workbook.Date1904), XlsxValueType.DateTime);
        }

        return (number, XlsxValueType.Number);
    }
}
