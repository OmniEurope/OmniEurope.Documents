// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml;
using OmniEurope.Documents.Csv;
using OmniEurope.Documents.Internal;

namespace OmniEurope.Documents.Excel;

/// <summary>Writes a workbook as SpreadsheetML parts.</summary>
internal sealed class XlsxPackageWriter
{
    private const string Main = XlsxStyleTable.Main;
    private const string Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

    private readonly XlsxWorkbook _workbook;
    private readonly XlsxStyleTable _styles = new();
    private readonly Dictionary<string, int> _sharedIndex = new(StringComparer.Ordinal);
    private readonly List<string> _shared = [];
    private int _sharedCount;

    private XlsxPackageWriter(XlsxWorkbook workbook) => _workbook = workbook;

    public static void Write(XlsxWorkbook workbook, Stream stream)
    {
        var writer = new XlsxPackageWriter(workbook);
        using var package = new OpenXmlPackageWriter(stream);
        var sheetCount = workbook.Worksheets.Count;
        package.WriteXml("[Content_Types].xml", writer.WriteContentTypes);
        package.WriteXml("_rels/.rels", WriteRootRelationships);
        package.WriteXml("docProps/core.xml", writer.WriteCoreProperties);
        package.WriteXml("docProps/app.xml", WriteAppProperties);
        package.WriteXml("xl/workbook.xml", writer.WriteWorkbook);
        package.WriteXml("xl/_rels/workbook.xml.rels", xml => WriteWorkbookRelationships(xml, sheetCount));
        for (var i = 0; i < sheetCount; i++)
        {
            var sheet = workbook.Worksheets[i];
            package.WriteXml($"xl/worksheets/sheet{i + 1}.xml", xml => writer.WriteSheet(xml, sheet));
        }

        package.WriteXml("xl/sharedStrings.xml", writer.WriteSharedStrings);
        package.WriteXml("xl/styles.xml", writer._styles.Write);
    }

    private void WriteContentTypes(XmlWriter xml)
    {
        xml.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
        Default(xml, "rels", "application/vnd.openxmlformats-package.relationships+xml");
        Default(xml, "xml", "application/xml");
        Override(xml, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
        for (var i = 1; i <= _workbook.Worksheets.Count; i++)
        {
            Override(xml, $"/xl/worksheets/sheet{i}.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
        }

        Override(xml, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
        Override(xml, "/xl/sharedStrings.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml");
        Override(xml, "/docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml");
        Override(xml, "/docProps/app.xml", "application/vnd.openxmlformats-officedocument.extended-properties+xml");
        xml.WriteEndElement();
    }

    private static void Default(XmlWriter xml, string extension, string type)
    {
        xml.WriteStartElement("Default");
        xml.WriteAttributeString("Extension", extension);
        xml.WriteAttributeString("ContentType", type);
        xml.WriteEndElement();
    }

    private static void Override(XmlWriter xml, string part, string type)
    {
        xml.WriteStartElement("Override");
        xml.WriteAttributeString("PartName", part);
        xml.WriteAttributeString("ContentType", type);
        xml.WriteEndElement();
    }

    private static void WriteRootRelationships(XmlWriter xml)
    {
        xml.WriteStartElement("Relationships", PackageRelationships);
        Relationship(xml, "rId1", Relationships + "/officeDocument", "xl/workbook.xml");
        Relationship(xml, "rId2", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties", "docProps/core.xml");
        Relationship(xml, "rId3", Relationships + "/extended-properties", "docProps/app.xml");
        xml.WriteEndElement();
    }

    private static void WriteWorkbookRelationships(XmlWriter xml, int sheets)
    {
        xml.WriteStartElement("Relationships", PackageRelationships);
        for (var i = 1; i <= sheets; i++)
        {
            Relationship(xml, $"rId{i}", Relationships + "/worksheet", $"worksheets/sheet{i}.xml");
        }

        Relationship(xml, $"rId{sheets + 1}", Relationships + "/styles", "styles.xml");
        Relationship(xml, $"rId{sheets + 2}", Relationships + "/sharedStrings", "sharedStrings.xml");
        xml.WriteEndElement();
    }

    private static void Relationship(XmlWriter xml, string id, string type, string target)
    {
        xml.WriteStartElement("Relationship");
        xml.WriteAttributeString("Id", id);
        xml.WriteAttributeString("Type", type);
        xml.WriteAttributeString("Target", target);
        xml.WriteEndElement();
    }

    private void WriteCoreProperties(XmlWriter xml)
    {
        xml.WriteStartElement("cp", "coreProperties", "http://schemas.openxmlformats.org/package/2006/metadata/core-properties");
        xml.WriteAttributeString("xmlns", "dc", null, "http://purl.org/dc/elements/1.1/");
        if (_workbook.Title is not null)
        {
            xml.WriteElementString("dc", "title", "http://purl.org/dc/elements/1.1/", _workbook.Title);
        }

        if (_workbook.Author is not null)
        {
            xml.WriteElementString("dc", "creator", "http://purl.org/dc/elements/1.1/", _workbook.Author);
        }

        xml.WriteEndElement();
    }

    private static void WriteAppProperties(XmlWriter xml)
    {
        xml.WriteStartElement("Properties", "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties");
        xml.WriteElementString("Application", "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties", "OmniEurope.Documents");
        xml.WriteEndElement();
    }

    private void WriteWorkbook(XmlWriter xml)
    {
        xml.WriteStartElement("workbook", Main);
        xml.WriteAttributeString("xmlns", "r", null, Relationships);
        if (_workbook.Date1904)
        {
            xml.WriteStartElement("workbookPr");
            xml.WriteAttributeString("date1904", "1");
            xml.WriteEndElement();
        }

        xml.WriteStartElement("bookViews");
        xml.WriteElementString("workbookView", Main, string.Empty);
        xml.WriteEndElement();
        xml.WriteStartElement("sheets");
        for (var i = 0; i < _workbook.Worksheets.Count; i++)
        {
            xml.WriteStartElement("sheet");
            xml.WriteAttributeString("name", _workbook.Worksheets[i].Name);
            xml.WriteAttributeString("sheetId", Number(i + 1));
            xml.WriteAttributeString("r", "id", Relationships, $"rId{i + 1}");
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
        WriteFilterNames(xml);
        xml.WriteEndElement();
    }

    private void WriteFilterNames(XmlWriter xml)
    {
        var filtered = _workbook.Worksheets.Select((sheet, index) => (sheet, index)).Where(x => x.sheet.AutoFilter is not null).ToList();
        if (filtered.Count == 0)
        {
            return;
        }

        xml.WriteStartElement("definedNames");
        foreach (var (sheet, index) in filtered)
        {
            var range = sheet.AutoFilter!.Value;
            xml.WriteStartElement("definedName");
            xml.WriteAttributeString("name", "_xlnm._FilterDatabase");
            xml.WriteAttributeString("localSheetId", Number(index));
            xml.WriteAttributeString("hidden", "1");
            var absolute = $"${CellReference.ColumnName(range.FirstColumn)}${range.FirstRow}:${CellReference.ColumnName(range.LastColumn)}${range.LastRow}";
            xml.WriteString($"'{sheet.Name.Replace("'", "''", StringComparison.Ordinal)}'!{absolute}");
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }

    private void WriteSheet(XmlWriter xml, XlsxWorksheet sheet)
    {
        xml.WriteStartElement("worksheet", Main);
        xml.WriteAttributeString("xmlns", "r", null, Relationships);
        var used = sheet.UsedRange;
        xml.WriteStartElement("dimension");
        xml.WriteAttributeString("ref", used?.ToString() ?? "A1");
        xml.WriteEndElement();
        WriteSheetView(xml, sheet);
        WriteColumns(xml, sheet);
        xml.WriteStartElement("sheetData");
        foreach (var row in sheet.Cells.GroupBy(c => c.Row))
        {
            xml.WriteStartElement("row");
            xml.WriteAttributeString("r", Number(row.Key));
            foreach (var cell in row)
            {
                WriteCell(xml, cell);
            }

            xml.WriteEndElement();
        }

        xml.WriteEndElement();
        if (sheet.AutoFilter is { } filter)
        {
            xml.WriteStartElement("autoFilter");
            xml.WriteAttributeString("ref", filter.ToString());
            xml.WriteEndElement();
        }

        WriteMerges(xml, sheet);
        xml.WriteEndElement();
    }

    private static void WriteSheetView(XmlWriter xml, XlsxWorksheet sheet)
    {
        xml.WriteStartElement("sheetViews");
        xml.WriteStartElement("sheetView");
        xml.WriteAttributeString("workbookViewId", "0");
        if (sheet.FrozenRows > 0 || sheet.FrozenColumns > 0)
        {
            xml.WriteStartElement("pane");
            if (sheet.FrozenColumns > 0)
            {
                xml.WriteAttributeString("xSplit", Number(sheet.FrozenColumns));
            }

            if (sheet.FrozenRows > 0)
            {
                xml.WriteAttributeString("ySplit", Number(sheet.FrozenRows));
            }

            xml.WriteAttributeString("topLeftCell", CellReference.Format(sheet.FrozenRows + 1, sheet.FrozenColumns + 1));
            xml.WriteAttributeString("activePane", sheet.FrozenRows > 0 && sheet.FrozenColumns > 0 ? "bottomRight" : sheet.FrozenRows > 0 ? "bottomLeft" : "topRight");
            xml.WriteAttributeString("state", "frozen");
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    private static void WriteColumns(XmlWriter xml, XlsxWorksheet sheet)
    {
        if (sheet.ColumnWidths.Count == 0)
        {
            return;
        }

        xml.WriteStartElement("cols");
        foreach (var (column, width) in sheet.ColumnWidths)
        {
            xml.WriteStartElement("col");
            xml.WriteAttributeString("min", Number(column));
            xml.WriteAttributeString("max", Number(column));
            xml.WriteAttributeString("width", width.ToString("0.###", CultureInfo.InvariantCulture));
            xml.WriteAttributeString("customWidth", "1");
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }

    private void WriteCell(XmlWriter xml, XlsxCell cell)
    {
        var style = cell.Style;
        if (_workbook.ProtectFormulaLikeText && cell.ValueType == XlsxValueType.Text && cell.Formula is null && CsvFormula.IsDangerous((string)cell.Value!))
        {
            style = style with { QuotePrefix = true };
        }

        xml.WriteStartElement("c");
        xml.WriteAttributeString("r", cell.Reference);
        var styleIndex = _styles.IndexOf(style);
        if (styleIndex != 0)
        {
            xml.WriteAttributeString("s", Number(styleIndex));
        }

        var (type, value) = Serialize(cell);
        if (type is not null)
        {
            xml.WriteAttributeString("t", type);
        }

        if (cell.Formula is not null)
        {
            xml.WriteElementString("f", Main, cell.Formula);
        }

        if (value is not null)
        {
            xml.WriteElementString("v", Main, value);
        }

        xml.WriteEndElement();
    }

    private (string? Type, string? Value) Serialize(XlsxCell cell) => cell.ValueType switch
    {
        XlsxValueType.Text when cell.Formula is not null => ("str", OpenXmlPackageWriter.EscapeOfficeText((string)cell.Value!)),
        XlsxValueType.Text => ("s", Number(Share((string)cell.Value!))),
        XlsxValueType.Number or XlsxValueType.DateTime => (null, cell.NumberValue!.Value.ToString("R", CultureInfo.InvariantCulture)),
        XlsxValueType.Boolean => ("b", (bool)cell.Value! ? "1" : "0"),
        XlsxValueType.Error => ("e", ((XlsxError)cell.Value!).Code),
        _ => (null, null),
    };

    private int Share(string text)
    {
        _sharedCount++;
        if (!_sharedIndex.TryGetValue(text, out var index))
        {
            index = _shared.Count;
            _shared.Add(text);
            _sharedIndex.Add(text, index);
        }

        return index;
    }

    private void WriteSharedStrings(XmlWriter xml)
    {
        xml.WriteStartElement("sst", Main);
        xml.WriteAttributeString("count", Number(_sharedCount));
        xml.WriteAttributeString("uniqueCount", Number(_shared.Count));
        foreach (var text in _shared)
        {
            xml.WriteStartElement("si");
            xml.WriteStartElement("t");
            if (text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1])))
            {
                xml.WriteAttributeString("xml", "space", null, "preserve");
            }

            xml.WriteString(OpenXmlPackageWriter.EscapeOfficeText(text));
            xml.WriteEndElement();
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }

    private static void WriteMerges(XmlWriter xml, XlsxWorksheet sheet)
    {
        if (sheet.MergedRanges.Count == 0)
        {
            return;
        }

        xml.WriteStartElement("mergeCells");
        xml.WriteAttributeString("count", Number(sheet.MergedRanges.Count));
        foreach (var range in sheet.MergedRanges)
        {
            xml.WriteStartElement("mergeCell");
            xml.WriteAttributeString("ref", range.ToString());
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
