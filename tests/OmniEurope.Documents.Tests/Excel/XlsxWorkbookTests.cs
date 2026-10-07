// SPDX-License-Identifier: EUPL-1.2
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using OmniEurope.Documents.Csv;
using OmniEurope.Documents.Excel;

namespace OmniEurope.Documents.Tests.Excel;

public sealed class XlsxWorkbookTests
{
    [Fact]
    public void Cells_convert_each_kind_of_value_and_pick_a_date_format()
    {
        var sheet = new XlsxWorkbook().AddWorksheet("Values");
        var cells = Enumerable.Range(1, 8).Select(c => sheet.Cell(1, c)).ToArray();

        cells[0].Value = new DateOnly(2026, 10, 6);
        cells[1].Value = new DateTimeOffset(2026, 10, 6, 14, 30, 0, TimeSpan.FromHours(2));
        cells[2].Value = TimeSpan.FromHours(30);
        cells[3].Value = 'x';
        cells[4].Value = double.NaN;
        cells[5].Value = 7m;
        cells[6].Value = new XlsxError("#N/A");
        cells[7].Value = null;

        Assert.Equal((XlsxValueType.DateTime, "yyyy-mm-dd"), (cells[0].ValueType, cells[0].Style.NumberFormat));
        Assert.Equal((XlsxValueType.DateTime, "yyyy-mm-dd hh:mm:ss"), (cells[1].ValueType, cells[1].Style.NumberFormat));
        Assert.Equal((XlsxValueType.Number, "[h]:mm:ss", 1.25), (cells[2].ValueType, cells[2].Style.NumberFormat, (double)cells[2].Value!));
        Assert.Equal((XlsxValueType.Text, "x"), (cells[3].ValueType, cells[3].Value));
        Assert.Equal(XlsxValueType.Error, cells[4].ValueType);
        Assert.Equal((XlsxValueType.Number, 7.0), (cells[5].ValueType, cells[5].Value));
        Assert.Equal(XlsxValueType.Error, cells[6].ValueType);
        Assert.Equal(XlsxValueType.Empty, cells[7].ValueType);
        Assert.Throws<ArgumentException>(() => sheet.Cell(2, 1).Value = new Uri("https://example.com"));
    }

    [Fact]
    public void Saved_workbook_loads_back_with_values_styles_and_layout()
    {
        var workbook = new XlsxWorkbook { Title = "Audit", Author = "OE" };
        var summary = workbook.AddWorksheet("Summary");
        var header = summary.Cell("A1");
        header.Value = "Contrôle";
        header.Style = header.Style with { Bold = true, FontSize = 14, FontColor = "#1F4E79", FillColor = "DDEBF7", Border = true };
        summary.Cell(2, 1).Value = 1234.5;
        summary.Cell(2, 1).Style = XlsxStyle.Default with { NumberFormat = "#,##0.00" };
        summary.Cell(2, 2).Value = new DateOnly(2026, 10, 6);
        summary.Cell(2, 3).Value = true;
        summary.Cell(2, 4).Value = 42;
        summary.Cell(2, 4).Formula = "SUM(A2:A3)";
        summary.Cell(2, 5).Value = new XlsxError("#DIV/0!");
        summary.Cell(3, 1).Value = "  leading and trailing  ";
        summary.Cell(3, 2).Value = "line\nbreak\ttab\u0001ctrl _x0041_";
        summary.Cell(3, 3).Value = "=HYPERLINK(\"evil\")";
        summary.Cell(4, 1).Style = XlsxStyle.Default with { WrapText = true, HorizontalAlignment = XlsxHorizontalAlignment.Center };
        summary.SetColumnWidth(1, 30);
        summary.FreezePanes(1, 1);
        summary.Merge(XlsxRange.Parse("B5:C6"));
        summary.AutoFilter = XlsxRange.Parse("A1:E3");
        workbook.AddWorksheet("Données").Cell(1, 1).Value = "second";

        var loaded = XlsxWorkbook.Load(workbook.ToArray());

        Assert.Equal("Audit", loaded.Title);
        Assert.Equal("OE", loaded.Author);
        Assert.Equal(["Summary", "Données"], loaded.Worksheets.Select(w => w.Name));
        var sheet = loaded.Worksheet("summary");
        var a1 = sheet.Cell("A1");
        Assert.Equal("Contrôle", a1.Value);
        Assert.True(a1.Style.Bold);
        Assert.Equal(14, a1.Style.FontSize);
        Assert.Equal("1F4E79", a1.Style.FontColor);
        Assert.Equal("DDEBF7", a1.Style.FillColor);
        Assert.True(a1.Style.Border);
        Assert.Equal("1,234.50", sheet.Cell(2, 1).FormattedText);
        Assert.Equal(new DateTime(2026, 10, 6), sheet.Cell(2, 2).Value);
        Assert.Equal("2026-10-06", sheet.Cell(2, 2).FormattedText);
        Assert.Equal(true, sheet.Cell(2, 3).Value);
        Assert.Equal("SUM(A2:A3)", sheet.Cell(2, 4).Formula);
        Assert.Equal(42d, sheet.Cell(2, 4).Value);
        Assert.Equal(new XlsxError("#DIV/0!"), sheet.Cell(2, 5).Value);
        Assert.Equal("  leading and trailing  ", sheet.Cell(3, 1).Value);
        Assert.Equal("line\nbreak\ttab\u0001ctrl _x0041_", sheet.Cell(3, 2).Value);
        Assert.True(sheet.Cell(3, 3).Style.QuotePrefix);
        Assert.True(sheet.Cell(4, 1).Style.WrapText);
        Assert.Equal(XlsxHorizontalAlignment.Center, sheet.Cell(4, 1).Style.HorizontalAlignment);
        Assert.Equal(30, sheet.ColumnWidths[1]);
        Assert.Equal((1, 1), (sheet.FrozenRows, sheet.FrozenColumns));
        Assert.Equal([XlsxRange.Parse("B5:C6")], sheet.MergedRanges);
        Assert.Equal(XlsxRange.Parse("A1:E3"), sheet.AutoFilter);
        Assert.Equal(new XlsxRange(1, 1, 3, 5), sheet.UsedRange);
    }

    [Fact]
    public void Output_is_a_well_formed_package_and_is_deterministic()
    {
        static byte[] Build()
        {
            var workbook = new XlsxWorkbook();
            workbook.AddWorksheet("S").Cell(1, 1).Value = "x";
            return workbook.ToArray();
        }

        var bytes = Build();
        Assert.Equal(bytes, Build());
        using var zip = new ZipArchive(new MemoryStream(bytes));
        Assert.Equal(
            ["[Content_Types].xml", "_rels/.rels", "docProps/app.xml", "docProps/core.xml", "xl/_rels/workbook.xml.rels", "xl/sharedStrings.xml", "xl/styles.xml", "xl/workbook.xml", "xl/worksheets/sheet1.xml"],
            zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal));
        foreach (var entry in zip.Entries)
        {
            using var stream = entry.Open();
            Assert.NotNull(XDocument.Load(stream).Root);
        }
    }

    [Fact]
    public void Reads_a_workbook_written_by_another_producer()
    {
        var bytes = Zip(new()
        {
            ["[Content_Types].xml"] = "<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'/>",
            ["_rels/.rels"] = "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='r1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument' Target='/xl/workbook.xml'/></Relationships>",
            ["xl/workbook.xml"] = "<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Data' sheetId='7' r:id='rS'/></sheets></workbook>",
            ["xl/_rels/workbook.xml.rels"] = "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='rS' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet' Target='sheets/data.xml'/><Relationship Id='rT' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings' Target='strings.xml'/><Relationship Id='rY' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles' Target='styles.xml'/></Relationships>",
            ["xl/strings.xml"] = "<sst xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><si><r><t>Rich </t></r><r><rPr><b/></rPr><t>text</t></r><rPh><t>ignored</t></rPh></si><si><t>plain</t></si></sst>",
            ["xl/styles.xml"] = "<styleSheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><fonts><font><sz val='11'/></font></fonts><cellXfs><xf numFmtId='0'/><xf numFmtId='14'/><xf numFmtId='4'/></cellXfs></styleSheet>",
            ["xl/sheets/data.xml"] = "<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData>"
                + "<row r='2'><c t='s'><v>0</v></c><c t='s'><v>1</v></c><c t='inlineStr'><is><t>inline</t></is></c></row>"
                + "<row><c s='1'><v>46301</v></c><c s='2'><v>11700000</v></c><c t='d'><v>2026-10-06T00:00:00</v></c><c><f>1/0</f><v>#DIV/0!</v></c></row>"
                + "</sheetData></worksheet>",
        });

        var sheet = XlsxWorkbook.Load(bytes).Worksheet("Data");

        Assert.Equal("Rich text", sheet.GetFormattedText(2, 1));
        Assert.Equal("plain", sheet.GetFormattedText(2, 2));
        Assert.Equal("inline", sheet.GetFormattedText(2, 3));
        Assert.Equal("10/6/2026", sheet.GetFormattedText(3, 1));
        Assert.Equal("11,700,000.00", sheet.GetFormattedText(3, 2));
        Assert.Equal(new DateTime(2026, 10, 6), sheet.Cell(3, 3).Value);
        Assert.Equal("1/0", sheet.Cell(3, 4).Formula);
        Assert.Equal(new XlsxRange(2, 1, 3, 4), sheet.UsedRange);
    }

    [Fact]
    public void Refuses_parts_beyond_the_limits_and_non_packages()
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("Big");
        for (var row = 1; row <= 2000; row++)
        {
            sheet.Cell(row, 1).Value = row;
        }

        var bytes = workbook.ToArray();

        Assert.Throws<DocumentFormatException>(() => XlsxWorkbook.Load(bytes, new PackageLimits { MaxPartSize = 4096 }));
        Assert.Throws<DocumentFormatException>(() => XlsxWorkbook.Load(Encoding.UTF8.GetBytes("not a zip")));
        Assert.Throws<DocumentFormatException>(() => XlsxWorkbook.Load(Zip(new() { ["a.txt"] = "x" })));
    }

    [Fact]
    public void Converts_between_csv_and_worksheets()
    {
        using var csv = CsvReader.Open(new StringReader("Name;Amount\nPomme;1234,5\nPoire;abc\n"));

        var workbook = XlsxCsvConverter.FromCsv(csv, "Import", System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));
        var sheet = workbook.Worksheet("Import");

        Assert.Equal(1234.5, sheet.Cell(2, 2).Value);
        Assert.Equal("abc", sheet.Cell(3, 2).Value);
        Assert.True(sheet.Cell(1, 1).Style.Bold);
        Assert.Equal(XlsxRange.Parse("A1:B3"), sheet.AutoFilter);
        Assert.Equal("Name,Amount\r\nPomme,1234.5\r\nPoire,abc\r\n", XlsxCsvConverter.ToCsv(XlsxWorkbook.Load(workbook.ToArray()).Worksheets[0]));
    }

    [Fact]
    public void Validates_names_and_values()
    {
        var workbook = new XlsxWorkbook();
        Assert.Throws<ArgumentException>(() => workbook.AddWorksheet("bad/name"));
        Assert.Throws<ArgumentException>(() => workbook.AddWorksheet(new string('x', 32)));
        var sheet = workbook.AddWorksheet("ok");
        Assert.Throws<ArgumentException>(() => workbook.AddWorksheet("OK"));
        Assert.Throws<ArgumentException>(() => sheet.Cell(1, 1).Value = new object());
        sheet.Cell(1, 1).Value = double.NaN;
        Assert.Equal(XlsxValueType.Error, sheet.Cell(1, 1).ValueType);
        sheet.Merge(XlsxRange.Parse("A1:B2"));
        Assert.Throws<ArgumentException>(() => sheet.Merge(XlsxRange.Parse("B2:C3")));
        Assert.Throws<InvalidOperationException>(() => new XlsxWorkbook().ToArray());
        Assert.Equal("XFD", CellReference.ColumnName(16384));
        Assert.Equal("AA", CellReference.ColumnName(27));
    }

    private static byte[] Zip(Dictionary<string, string> parts)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in parts)
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        return stream.ToArray();
    }
}
