// SPDX-License-Identifier: EUPL-1.2
using System.IO.Compression;
using System.Text;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Excel.Editing;

namespace OmniEurope.Documents.Tests.Excel;

/// <summary>Text that looks like an Office escape (<c>_xHHHH_</c>) keeps its value through the editor, and an OOXML Strict workbook is named, not mistaken for an empty one.</summary>
public sealed class XlsxEditorTextTests
{
    [Theory]
    [InlineData("_x0042_")]
    [InlineData("_x005F_x0041_")]
    [InlineData("_x0041_")]
    [InlineData("plain _x text")]
    public void A_text_that_looks_like_an_escape_is_the_same_through_the_model_the_editor_and_the_file(string text)
    {
        var editor = XlsxEditor.Open(XlsxFactory.Rich());
        editor.Sheet("Données").SetValue("D2", text);

        Assert.Equal(text, editor.Sheet("Données").GetValue("D2"));
        var saved = editor.ToArray();
        Assert.Equal(text, XlsxEditor.Open(saved).Sheet("Données").GetValue("D2"));
        Assert.Equal(text, XlsxWorkbook.Load(saved).Worksheet("Données").Cell("D2").Value);
    }

    [Fact]
    public void The_escapes_of_an_external_file_are_decoded_once()
    {
        const string rows = """<row r="1"><c r="A1" t="inlineStr"><is><t>_x0042_</t></is></c><c r="B1" t="inlineStr"><is><r><t>_x005F_x0041_</t></r></is></c><c r="C1" t="str"><f>1</f><v>_x0043_</v></c></row>""";
        var xlsx = XlsxFactory.Rich(rows);

        var sheet = XlsxEditor.Open(xlsx).Sheet("Données");
        var model = XlsxWorkbook.Load(xlsx).Worksheet("Données");

        Assert.Equal(["B", "_x0041_", "C"], new[] { "A1", "B1", "C1" }.Select(r => sheet.GetValue(r)));
        Assert.Equal(["B", "_x0041_", "C"], new[] { "A1", "B1", "C1" }.Select(r => model.Cell(r).Value));
    }

    [Fact]
    public void A_strict_workbook_is_refused_by_name_and_not_as_an_empty_one()
    {
        var strict = Strict();

        var load = Assert.Throws<DocumentFormatException>(() => XlsxWorkbook.Load(strict));
        var edit = Assert.Throws<DocumentFormatException>(() => XlsxEditor.Open(strict));
        Assert.Contains("Strict", load.Message, StringComparison.Ordinal);
        Assert.Contains("Strict", edit.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("no worksheet", load.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("no worksheet", edit.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_transitional_workbook_still_loads_and_opens()
    {
        var xlsx = XlsxFactory.Rich();

        Assert.Equal(10.0, XlsxWorkbook.Load(xlsx).Worksheet("Données").Cell("B1").Value);
        Assert.Equal(10.0, XlsxEditor.Open(xlsx).Sheet("Données").GetValue("B1"));
    }

    // A workbook of one sheet in the Strict namespaces (ISO/IEC 29500 strict).
    private static byte[] Strict()
    {
        const string main = "http://purl.oclc.org/ooxml/spreadsheetml/main";
        const string rel = "http://purl.oclc.org/ooxml/officeDocument/relationships";
        const string pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
        var parts = new Dictionary<string, string>
        {
            ["[Content_Types].xml"] = """<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>""",
            ["_rels/.rels"] = $"""<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="{pkg}"><Relationship Id="rId1" Type="{rel}/officeDocument" Target="xl/workbook.xml"/></Relationships>""",
            ["xl/workbook.xml"] = $"""<?xml version="1.0" encoding="UTF-8"?><workbook xmlns="{main}" xmlns:r="{rel}"><sheets><sheet name="Feuil1" sheetId="1" r:id="rId1"/></sheets></workbook>""",
            ["xl/_rels/workbook.xml.rels"] = $"""<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="{pkg}"><Relationship Id="rId1" Type="{rel}/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""",
            ["xl/worksheets/sheet1.xml"] = $"""<?xml version="1.0" encoding="UTF-8"?><worksheet xmlns="{main}"><sheetData><row r="1"><c r="A1"><v>1</v></c></row></sheetData></worksheet>""",
        };
        using var stream = new MemoryStream();
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
