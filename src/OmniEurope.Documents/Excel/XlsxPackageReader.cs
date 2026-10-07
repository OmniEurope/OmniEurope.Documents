// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using System.Xml;
using OmniEurope.Documents.Internal;

namespace OmniEurope.Documents.Excel;

/// <summary>Reads a .xlsx package into an <see cref="XlsxWorkbook"/>.</summary>
internal static class XlsxPackageReader
{
    private const string OfficeDocument = "/officeDocument";

    public static XlsxWorkbook Read(Stream stream, PackageLimits? limits)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var zip = SafeZip.Open(stream, limits, leaveOpen: true);
        try
        {
            return ReadPackage(zip);
        }
        catch (XmlException exception)
        {
            throw new DocumentFormatException("The workbook contains malformed XML.", exception);
        }
    }

    private static XlsxWorkbook ReadPackage(SafeZip zip)
    {
        var root = OpcRelationships.Read(zip, string.Empty);
        var workbookPath = root.Find(r => r.Type.EndsWith(OfficeDocument, StringComparison.Ordinal))?.Target ?? "xl/workbook.xml";
        if (!zip.Contains(workbookPath))
        {
            throw new DocumentFormatException("The package has no workbook part.");
        }

        var workbook = new XlsxWorkbook();
        ReadCoreProperties(zip, root, workbook);
        var relationships = OpcRelationships.Read(zip, workbookPath);
        var shared = ReadSharedStrings(zip, relationships.Find(r => r.Type.EndsWith("/sharedStrings", StringComparison.Ordinal))?.Target);
        var styles = XlsxStyleReader.Read(zip, relationships.Find(r => r.Type.EndsWith("/styles", StringComparison.Ordinal))?.Target);
        foreach (var (name, relationshipId) in ReadWorkbook(zip, workbookPath, workbook))
        {
            var target = relationships.Find(r => r.Id == relationshipId);
            if (target is null || !target.Type.EndsWith("/worksheet", StringComparison.Ordinal))
            {
                continue;
            }

            var sheet = workbook.AddWorksheet(name);
            new XlsxSheetReader(sheet, shared, styles).Read(zip, target.Target);
        }

        if (workbook.Worksheets.Count == 0)
        {
            throw new DocumentFormatException("The workbook has no worksheet.");
        }

        return workbook;
    }

    private static List<(string Name, string RelationshipId)> ReadWorkbook(SafeZip zip, string path, XlsxWorkbook workbook)
    {
        var sheets = new List<(string, string)>();
        using var xml = zip.ReadXml(path)!;
        while (xml.Read())
        {
            if (xml.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (xml.LocalName == "workbookPr")
            {
                workbook.Date1904 = IsTrue(xml.GetAttribute("date1904"));
            }
            else if (xml.LocalName == "sheet")
            {
                var id = xml.GetAttribute("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                sheets.Add((xml.GetAttribute("name") ?? $"Sheet{sheets.Count + 1}", id ?? string.Empty));
            }
        }

        return sheets;
    }

    private static void ReadCoreProperties(SafeZip zip, List<OpcRelationship> root, XlsxWorkbook workbook)
    {
        var path = root.Find(r => r.Type.EndsWith("/core-properties", StringComparison.Ordinal))?.Target;
        using var xml = path is null ? null : zip.ReadXml(path);
        if (xml is null)
        {
            return;
        }

        xml.Read();
        while (!xml.EOF)
        {
            if (xml.NodeType == XmlNodeType.Element && xml.LocalName is "title" or "creator")
            {
                // ReadElementContentAsString already moves to the next node.
                var isTitle = xml.LocalName == "title";
                var value = xml.ReadElementContentAsString();
                if (isTitle)
                {
                    workbook.Title = value;
                }
                else
                {
                    workbook.Author = value;
                }

                continue;
            }

            xml.Read();
        }
    }

    private static List<string> ReadSharedStrings(SafeZip zip, string? path)
    {
        var strings = new List<string>();
        using var xml = path is null ? null : zip.ReadXml(path);
        if (xml is null)
        {
            return strings;
        }

        while (xml.Read())
        {
            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "si")
            {
                strings.Add(ReadRichText(xml));
            }
        }

        return strings;
    }

    /// <summary>Reads the text of an <c>si</c> or <c>is</c> element: its <c>t</c> and the <c>t</c> of its runs,
    /// skipping phonetic runs.</summary>
    internal static string ReadRichText(XmlReader xml)
    {
        if (xml.IsEmptyElement)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        var depth = xml.Depth;
        xml.Read();
        while (!xml.EOF && xml.Depth > depth)
        {
            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "rPh")
            {
                xml.Skip();
            }
            else if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "t")
            {
                text.Append(xml.ReadElementContentAsString());
            }
            else
            {
                xml.Read();
            }
        }

        return OpenXmlPackageWriter.UnescapeOfficeText(text.ToString());
    }

    internal static bool IsTrue(string? value) => value is "1" or "true" or "on";

    internal static double ParseDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0;
}
