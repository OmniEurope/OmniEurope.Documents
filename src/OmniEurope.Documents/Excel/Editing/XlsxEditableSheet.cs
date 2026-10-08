// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using static OmniEurope.Documents.Excel.Editing.XlsxEditor;

namespace OmniEurope.Documents.Excel.Editing;

/// <summary>
/// One worksheet of an <see cref="XlsxEditor"/>. Reading gives the values the file holds (cached formula results
/// included); writing changes one cell in the worksheet part and leaves every other element of it (formats,
/// merges, conditional formats, validations, drawings) as it was. A new cell takes the format of its row or
/// column, as Excel gives it; text is written inline, so the shared string table is not rewritten.
/// </summary>
public sealed class XlsxEditableSheet
{
    private const int MaxText = 32767;
    private const int MaxFormula = 8192;
    private const string TableType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/table";

    private readonly XlsxEditor _editor;
    private readonly string _part;
    private bool _normalized;

    internal XlsxEditableSheet(XlsxEditor editor, string name, string part)
    {
        (_editor, Name, _part) = (editor, name, part);
    }

    /// <summary>The name of the worksheet.</summary>
    public string Name { get; }

    private XElement Root => _editor.Package.GetXml(_part)?.Root ?? throw new DocumentFormatException($"Worksheet part '{_part}' is not readable.");

    /// <summary>
    /// The value of a cell as the file holds it: a <see cref="double"/>, a <see cref="string"/> (text, a formula's
    /// text result, an error such as <c>#DIV/0!</c>), a <see cref="bool"/>, or null for an empty cell. Dates are
    /// numbers (serials); <see cref="ExcelDate.FromSerial"/> turns them into dates.
    /// </summary>
    public object? GetValue(string reference)
    {
        var cell = FindCell(Parse(reference));
        if (cell is null)
        {
            return null;
        }

        var type = (string?)cell.Attribute("t");
        if (type == "inlineStr")
        {
            return cell.Element(S + "is") is { } inline ? InlineText(inline) : null;
        }

        if (cell.Element(S + "v")?.Value is not { } value)
        {
            return null;
        }

        return type switch
        {
            "s" => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ? _editor.SharedString(index) : null,
            "b" => value == "1",
            "str" or "e" or "d" => value,
            _ => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : value,
        };
    }

    /// <summary>The formula of a cell without its leading <c>=</c>, or null when it has none.</summary>
    public string? GetFormula(string reference) => FindCell(Parse(reference))?.Element(S + "f")?.Value is { Length: > 0 } formula ? formula : null;

    /// <summary>
    /// Sets a cell to a number (any numeric type), a <see cref="string"/>, a <see cref="bool"/>, a
    /// <see cref="DateTime"/> (written as its serial in the workbook's date system; the cell keeps its own number
    /// format, so a date shows as one only in a cell formatted as a date) or null (the value is
    /// cleared, the format kept). A formula the cell held is removed.
    /// </summary>
    /// <exception cref="ArgumentException">The reference, the value type or the text (longer than 32,767
    /// characters, or holding characters XML cannot carry) is not accepted.</exception>
    /// <exception cref="NotSupportedException">The cell heads a shared formula, lies in an array formula, or is a
    /// header of a table, whose column names would no longer match.</exception>
    public void SetValue(string reference, object? value)
    {
        var position = Parse(reference);
        var content = Content(value);
        var cell = EditableCell(position, out var hadFormula);
        Clear(cell);
        if (content is { } written)
        {
            if (written.Type is not null)
            {
                cell.SetAttributeValue("t", written.Type);
            }

            cell.AddFirst(written.Element);
        }

        _editor.CellChanged(hadFormula);
    }

    /// <summary>Sets the formula of a cell (a leading <c>=</c> is dropped); Excel computes its result on opening.</summary>
    /// <exception cref="ArgumentException">The reference or the formula (empty, longer than 8,192 characters, or
    /// holding characters XML cannot carry) is not accepted.</exception>
    /// <exception cref="NotSupportedException">As for <see cref="SetValue"/>.</exception>
    public void SetFormula(string reference, string formula)
    {
        ArgumentNullException.ThrowIfNull(formula);
        var text = formula.StartsWith('=') ? formula[1..] : formula;
        if (text.Length is 0 or > MaxFormula || !IsXmlText(text))
        {
            throw new ArgumentException("A formula holds 1 to 8,192 characters XML can carry.", nameof(formula));
        }

        var cell = EditableCell(Parse(reference), out _);
        Clear(cell);
        cell.AddFirst(new XElement(S + "f", text));
        _editor.CellChanged(formulaChanged: true);
    }

    internal static string InlineText(XElement container) =>
        string.Concat(container.Elements(S + "t").Concat(container.Elements(S + "r").Elements(S + "t")).InDocumentOrder().Select(t => t.Value));

    private static (int Row, int Column) Parse(string reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return CellReference.TryParse(reference, out var row, out var column)
            ? (row, column)
            : throw new ArgumentException($"'{reference}' is not a cell reference.", nameof(reference));
    }

    private (string? Type, XElement Element)? Content(object? value) => value switch
    {
        null => null,
        string text => ("inlineStr", Inline(text)),
        bool flag => ("b", new XElement(S + "v", flag ? "1" : "0")),
        DateTime date => (null, Number(Serial(date))),
        IConvertible number when IsNumber(number.GetTypeCode()) => (null, Number(number.ToDouble(CultureInfo.InvariantCulture))),
        _ => throw new ArgumentException($"A cell cannot hold a {value.GetType().Name}.", nameof(value)),
    };

    private static bool IsNumber(TypeCode code) => code is >= TypeCode.SByte and <= TypeCode.Decimal;

    private static XElement Inline(string text) => text.Length <= MaxText && IsXmlText(text)
        ? new XElement(S + "is", new XElement(S + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text))
        : throw new ArgumentException("A text holds at most 32,767 characters XML can carry.", "value");

    private static XElement Number(double number) => double.IsFinite(number)
        ? new XElement(S + "v", number.ToString("R", CultureInfo.InvariantCulture))
        : throw new ArgumentException("Excel holds finite numbers only.", "value");

    private double Serial(DateTime date)
    {
        var serial = ExcelDate.ToSerial(date, _editor.Date1904);
        return ExcelDate.IsInRange(serial, _editor.Date1904) ? serial : throw new ArgumentException("The date is outside the date system of the workbook.", "value");
    }

    private static bool IsXmlText(string text)
    {
        try
        {
            XmlConvert.VerifyXmlChars(text);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    // Value, formula and inline text go; the style and the other attributes stay.
    private static void Clear(XElement cell)
    {
        cell.Elements().Where(e => e.Name == S + "v" || e.Name == S + "f" || e.Name == S + "is").Remove();
        cell.Attribute("t")?.Remove();
    }

    private XElement? FindCell((int Row, int Column) position)
    {
        var name = CellReference.Format(position.Row, position.Column);
        Normalize();
        return Row(position.Row)?.Elements(S + "c").FirstOrDefault(c => (string?)c.Attribute("r") == name);
    }

    private XElement EditableCell((int Row, int Column) position, out bool hadFormula)
    {
        Guard(position);
        var cell = FindCell(position) ?? AddCell(position);
        hadFormula = cell.Element(S + "f") is not null;
        return cell;
    }

    private void Guard((int Row, int Column) position)
    {
        var name = CellReference.Format(position.Row, position.Column);
        if (Root.Descendants(S + "f").FirstOrDefault(f => InGroup(f, name, position)) is { } group)
        {
            throw new NotSupportedException($"Cell {name} belongs to a {(string?)group.Attribute("t")} formula, which cannot be changed one cell at a time.");
        }

        foreach (var table in _editor.Package.Relationships(_part).Where(r => r.Type == TableType && !r.External))
        {
            if (_editor.Package.GetXml(table.Target)?.Root is { } root && InHeader(root, position))
            {
                throw new NotSupportedException($"Cell {name} is a header of table '{(string?)root.Attribute("displayName")}', whose column names would no longer match.");
            }
        }
    }

    // The head of a shared formula, or any cell of an array formula.
    private static bool InGroup(XElement formula, string name, (int Row, int Column) position) =>
        (string?)formula.Attribute("ref") is { } range && (string?)formula.Attribute("t") switch
        {
            "shared" => (string?)formula.Parent?.Attribute("r") == name,
            "array" => Contains(range, position),
            _ => false,
        };

    // The header rows of a table (one unless headerRowCount says otherwise) across its columns.
    private static bool InHeader(XElement table, (int Row, int Column) position)
    {
        var headers = int.TryParse((string?)table.Attribute("headerRowCount"), NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : 1;
        return headers > 0 && CellReference.TryParseRange((string?)table.Attribute("ref") ?? string.Empty, out var top, out var left, out _, out var right)
            && Contains(new XlsxRange(top, left, top + headers - 1, right).ToString(), position);
    }

    private static bool Contains(string range, (int Row, int Column) position) =>
        CellReference.TryParseRange(range, out var top, out var left, out var bottom, out var right)
        && position.Row >= top && position.Row <= bottom && position.Column >= left && position.Column <= right;

    private XElement? Row(int row) => SheetData().Elements(S + "row").FirstOrDefault(r => (int?)r.Attribute("r") == row);

    private XElement SheetData() => Root.Element(S + "sheetData") ?? throw new DocumentFormatException($"Worksheet part '{_part}' has no sheetData.");

    // Rows and cells may leave their position implicit (ECMA-376 part 1, §18.3.1.73, §18.3.1.4): it is written
    // out once, so that cells can be found and placed by reference.
    private void Normalize()
    {
        if (_normalized)
        {
            return;
        }

        _normalized = true;
        var row = 0;
        foreach (var element in SheetData().Elements(S + "row"))
        {
            row = (int?)element.Attribute("r") ?? row + 1;
            if (element.Attribute("r") is null)
            {
                element.SetAttributeValue("r", row);
            }

            var column = 0;
            foreach (var cell in element.Elements(S + "c"))
            {
                column = CellReference.TryParse((string?)cell.Attribute("r") ?? string.Empty, out _, out var at) ? at : column + 1;
                if (cell.Attribute("r") is null)
                {
                    cell.SetAttributeValue("r", CellReference.Format(row, column));
                }
            }
        }
    }

    private XElement AddCell((int Row, int Column) position)
    {
        var row = Row(position.Row);
        if (row is null)
        {
            row = new XElement(S + "row", new XAttribute("r", position.Row));
            var next = SheetData().Elements(S + "row").FirstOrDefault(r => (int?)r.Attribute("r") > position.Row);
            if (next is null)
            {
                SheetData().Add(row);
            }
            else
            {
                next.AddBeforeSelf(row);
            }
        }

        // The spans hint would no longer cover the row's cells.
        row.Attribute("spans")?.Remove();
        var cell = new XElement(S + "c", new XAttribute("r", CellReference.Format(position.Row, position.Column)));
        if (InheritedStyle(row, position.Column) is { } style)
        {
            cell.SetAttributeValue("s", style);
        }

        var after = row.Elements(S + "c").FirstOrDefault(c => CellReference.TryParse((string?)c.Attribute("r") ?? string.Empty, out _, out var column) && column > position.Column);
        if (after is null)
        {
            row.Add(cell);
        }
        else
        {
            after.AddBeforeSelf(cell);
        }

        ExtendDimension(position);
        return cell;
    }

    private string? InheritedStyle(XElement row, int column)
    {
        if ((string?)row.Attribute("customFormat") is "1" or "true" && (string?)row.Attribute("s") is { } rowStyle)
        {
            return rowStyle;
        }

        return Root.Element(S + "cols")?.Elements(S + "col")
            .FirstOrDefault(c => (int?)c.Attribute("min") <= column && (int?)c.Attribute("max") >= column)?.Attribute("style")?.Value;
    }

    private void ExtendDimension((int Row, int Column) position)
    {
        if (Root.Element(S + "dimension") is not { } dimension || !CellReference.TryParseRange((string?)dimension.Attribute("ref") ?? string.Empty, out var top, out var left, out var bottom, out var right))
        {
            return;
        }

        var range = new XlsxRange(Math.Min(top, position.Row), Math.Min(left, position.Column), Math.Max(bottom, position.Row), Math.Max(right, position.Column));
        if (range != new XlsxRange(top, left, bottom, right))
        {
            dimension.SetAttributeValue("ref", range.ToString());
        }
    }
}
