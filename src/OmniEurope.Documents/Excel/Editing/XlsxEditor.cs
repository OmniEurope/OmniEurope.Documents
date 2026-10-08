// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using OmniEurope.Documents.Internal;

namespace OmniEurope.Documents.Excel.Editing;

/// <summary>
/// Edits an existing .xlsx in place: charts, pictures, conditional formats, validations, comments, pivot tables
/// and every other part are kept, and saving rewrites only the parts that changed (the others are copied byte
/// for byte). A cell edit rewrites its worksheet part and asks Excel to recalculate the workbook when it opens
/// (<c>fullCalcOnLoad</c> in <c>xl/workbook.xml</c>), since cached formula results may depend on the cell; the
/// calculation chain, which lists formula cells, is dropped when a formula cell changes (Excel rebuilds it).
/// Opening applies the size limits of <see cref="PackageLimits"/>; DTDs are refused.
/// </summary>
public sealed class XlsxEditor
{
    internal static readonly XNamespace S = XlsxStyleTable.Main;
    internal static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string OfficeDocumentType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";
    private const string WorksheetType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet";
    private const string SharedStringsType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings";
    private const string CalcChainType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/calcChain";

    // CT_Workbook children that come before calcPr (ECMA-376 part 1, §18.2.27).
    private static readonly HashSet<string> BeforeCalcPr = new(StringComparer.Ordinal)
    {
        "fileVersion", "fileSharing", "workbookPr", "workbookProtection", "bookViews", "sheets", "functionGroups", "externalReferences", "definedNames",
    };

    private readonly OpcPackage _package;
    private readonly string _workbookPart;
    private readonly List<(string Name, string Part)> _sheets;
    private readonly Dictionary<string, XlsxEditableSheet> _opened = new(StringComparer.OrdinalIgnoreCase);
    private List<string>? _sharedStrings;

    private XlsxEditor(OpcPackage package)
    {
        _package = package;
        _workbookPart = package.Relationships(string.Empty).Find(r => r.Type == OfficeDocumentType && !r.External)?.Target ?? "xl/workbook.xml";
        var workbook = package.GetXml(_workbookPart)?.Root ?? throw new DocumentFormatException("The package has no workbook part.");
        Date1904 = (string?)workbook.Element(S + "workbookPr")?.Attribute("date1904") is "1" or "true";
        _sheets = Worksheets(package, workbook, package.Relationships(_workbookPart));
        if (_sheets.Count == 0)
        {
            throw new DocumentFormatException("The workbook has no worksheet.");
        }
    }

    // The worksheets the workbook lists whose part is in the package (chart sheets have another type).
    private static List<(string Name, string Part)> Worksheets(OpcPackage package, XElement workbook, List<OpcRelationship> relationships)
    {
        var sheets = new List<(string, string)>();
        foreach (var sheet in workbook.Element(S + "sheets")?.Elements(S + "sheet") ?? [])
        {
            var id = (string?)sheet.Attribute(R + "id");
            if (relationships.Find(r => r.Id == id && r.Type == WorksheetType && !r.External)?.Target is { } target && package.Contains(target))
            {
                sheets.Add(((string?)sheet.Attribute("name") ?? target, target));
            }
        }

        return sheets;
    }

    /// <summary>The names of the worksheets, in workbook order (chart sheets are not listed).</summary>
    public IReadOnlyList<string> SheetNames => _sheets.Select(s => s.Name).ToList();

    /// <summary>The parts added or modified so far.</summary>
    public IReadOnlyCollection<string> ChangedParts => _package.ChangedParts.ToList();

    /// <summary>True when the workbook counts dates from 1904.</summary>
    public bool Date1904 { get; }

    internal OpcPackage Package => _package;

    /// <summary>Opens a workbook for editing.</summary>
    public static XlsxEditor Open(Stream stream, PackageLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new XlsxEditor(OpcPackage.Open(stream, limits));
    }

    /// <summary>Opens a workbook for editing from bytes.</summary>
    public static XlsxEditor Open(byte[] bytes, PackageLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Open(new MemoryStream(bytes, writable: false), limits);
    }

    /// <summary>The worksheet of that name (ignoring case).</summary>
    /// <exception cref="ArgumentException">The workbook has no worksheet of that name.</exception>
    public XlsxEditableSheet Sheet(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (_opened.TryGetValue(name, out var opened))
        {
            return opened;
        }

        var (sheetName, part) = _sheets.Find(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (part is null)
        {
            throw new ArgumentException($"The workbook has no worksheet named '{name}'.", nameof(name));
        }

        var sheet = new XlsxEditableSheet(this, sheetName, part);
        _opened[name] = sheet;
        return sheet;
    }

    /// <summary>Writes the workbook; unchanged parts keep their bytes.</summary>
    public void Save(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _package.Save(stream);
    }

    /// <summary>The workbook as bytes.</summary>
    public byte[] ToArray()
    {
        using var stream = new MemoryStream();
        Save(stream);
        return stream.ToArray();
    }

    // The text of shared string i: its runs joined, phonetic hints left out.
    internal string? SharedString(int index)
    {
        if (_sharedStrings is null)
        {
            var part = _package.Relationships(_workbookPart).Find(r => r.Type == SharedStringsType && !r.External)?.Target;
            var root = part is null ? null : _package.GetXml(part)?.Root;
            _sharedStrings = root?.Elements(S + "si").Select(XlsxEditableSheet.InlineText).ToList() ?? [];
        }

        return index >= 0 && index < _sharedStrings.Count ? _sharedStrings[index] : null;
    }

    // A cell changed: cached formula results may be stale, and a formula change makes the chain wrong.
    internal void CellChanged(bool formulaChanged)
    {
        var workbook = _package.GetXml(_workbookPart)!.Root!;
        var calcPr = workbook.Element(S + "calcPr");
        if (calcPr is null)
        {
            calcPr = new XElement(S + "calcPr");
            var before = workbook.Elements().LastOrDefault(e => e.Name.Namespace == S && BeforeCalcPr.Contains(e.Name.LocalName));
            if (before is null)
            {
                workbook.AddFirst(calcPr);
            }
            else
            {
                before.AddAfterSelf(calcPr);
            }
        }

        if ((string?)calcPr.Attribute("fullCalcOnLoad") != "1")
        {
            calcPr.SetAttributeValue("fullCalcOnLoad", "1");
        }

        if (formulaChanged && _package.Relationships(_workbookPart).Find(r => r.Type == CalcChainType && !r.External)?.Target is { } chain)
        {
            _package.RemovePart(chain);
        }
    }
}
