// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml.Linq;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Excel.Editing;
using static OmniEurope.Documents.Tests.Word.DocxFactory;

namespace OmniEurope.Documents.Tests.Excel;

/// <summary>
/// Editing a workbook in place (ECMA-376 part 1, §18.3): the parts a cell edit does not concern are copied byte
/// for byte, the worksheet keeps every element around the cell, and Excel is asked to recalculate on opening.
/// The workbook is written by hand in <see cref="XlsxFactory"/>.
/// </summary>
public sealed class XlsxEditorTests
{
    private static readonly XNamespace S = XlsxFactory.Main;

    [Fact]
    public void A_cell_edit_rewrites_only_its_sheet_and_the_calculation_flag()
    {
        var original = XlsxFactory.Rich();
        var editor = XlsxEditor.Open(original);

        editor.Sheet("Données").SetValue("B1", 15);
        var edited = editor.ToArray();

        Assert.Equal(["xl/workbook.xml", "xl/worksheets/sheet1.xml"], editor.ChangedParts.Order(StringComparer.Ordinal));
        var before = Entries(original);
        var after = Entries(edited);
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        foreach (var name in before.Keys.Except(["xl/workbook.xml", "xl/worksheets/sheet1.xml"]))
        {
            Assert.True(before[name].AsSpan().SequenceEqual(after[name]), $"{name} changed");
        }

        // The worksheet keeps its conditional format, validation, drawing, comments and table around the cell.
        var sheet = XDocument.Parse(Part(edited, "xl/worksheets/sheet1.xml")).Root!;
        Assert.Equal(
            ["dimension", "cols", "sheetData", "conditionalFormatting", "dataValidations", "drawing", "legacyDrawing", "tableParts"],
            sheet.Elements().Select(e => e.Name.LocalName));
        Assert.Equal("15", sheet.Descendants(S + "c").Single(c => (string?)c.Attribute("r") == "B1").Element(S + "v")!.Value);
        Assert.Equal("1", (string?)XDocument.Parse(Part(edited, "xl/workbook.xml")).Root!.Element(S + "calcPr")!.Attribute("fullCalcOnLoad"));
        Assert.Equal(["sheets", "calcPr", "pivotCaches"], XDocument.Parse(Part(edited, "xl/workbook.xml")).Root!.Elements().Skip(2).Select(e => e.Name.LocalName));

        var reread = XlsxWorkbook.Load(edited).Worksheet("Données");
        Assert.Equal(15.0, reread.Cell("B1").Value);
        Assert.Equal("Titre", reread.Cell("A1").Value);
        Assert.Equal("B1*2", reread.Cell("C1").Formula);
    }

    [Fact]
    public void Values_are_read_as_the_file_holds_them()
    {
        var sheet = XlsxEditor.Open(XlsxFactory.Rich()).Sheet("données");

        Assert.Equal("Titre", sheet.GetValue("A1"));
        Assert.Equal("Nom complet", sheet.GetValue("E1"));
        Assert.Equal(10.0, sheet.GetValue("B1"));
        Assert.Equal(20.0, sheet.GetValue("$C$1"));
        Assert.Equal("B1*2", sheet.GetFormula("C1"));
        Assert.Equal(true, sheet.GetValue("A3"));
        Assert.Equal("Durand", sheet.GetValue("E3"));
        Assert.Null(sheet.GetValue("B2"));
        Assert.Null(sheet.GetFormula("B1"));
        Assert.Equal("Données", sheet.Name);
    }

    [Fact]
    public void A_new_cell_finds_its_place_and_the_format_of_its_column()
    {
        var editor = XlsxEditor.Open(XlsxFactory.Rich());
        var sheet = editor.Sheet("Données");

        sheet.SetValue("D5", 3.25);
        sheet.SetValue("D2", "milieu");
        sheet.SetValue("A1", null);
        sheet.SetValue("B3", false);
        var edited = editor.ToArray();

        var root = XDocument.Parse(Part(edited, "xl/worksheets/sheet1.xml")).Root!;
        Assert.Equal(["1", "2", "3", "5"], root.Descendants(S + "row").Select(r => (string)r.Attribute("r")!));
        Assert.Equal(["A3", "B3", "E3", "F3"], root.Descendants(S + "row").ElementAt(2).Elements(S + "c").Select(c => (string)c.Attribute("r")!));
        Assert.Equal("A1:F5", (string?)root.Element(S + "dimension")!.Attribute("ref"));
        Assert.All(root.Descendants(S + "row").Where(r => (string)r.Attribute("r")! != "1"), r => Assert.Null(r.Attribute("spans")));
        var d5 = root.Descendants(S + "c").Single(c => (string?)c.Attribute("r") == "D5");
        Assert.Equal("1", (string?)d5.Attribute("s"));

        var reread = XlsxWorkbook.Load(edited).Worksheet("Données");
        Assert.Equal("3.25", reread.Cell("D5").FormatValue(CultureInfo.InvariantCulture));
        Assert.Equal("milieu", reread.Cell("D2").Value);
        Assert.Equal(XlsxValueType.Empty, reread.Cell("A1").ValueType);
        Assert.Equal(false, reread.Cell("B3").Value);
    }

    [Fact]
    public void A_formula_change_drops_the_calculation_chain_and_its_references()
    {
        var editor = XlsxEditor.Open(XlsxFactory.Rich());
        var sheet = editor.Sheet("Données");

        sheet.SetFormula("B3", "=SUM(B1:B2)");
        sheet.SetValue("C1", "plus de formule");
        var edited = editor.ToArray();

        var entries = Entries(edited);
        Assert.DoesNotContain("xl/calcChain.xml", entries.Keys);
        Assert.DoesNotContain("calcChain", Part(edited, "xl/_rels/workbook.xml.rels"), StringComparison.Ordinal);
        Assert.DoesNotContain("calcChain", Part(edited, "[Content_Types].xml"), StringComparison.Ordinal);
        var reread = XlsxWorkbook.Load(edited).Worksheet("Données");
        Assert.Equal("SUM(B1:B2)", reread.Cell("B3").Formula);
        Assert.Equal("plus de formule", reread.Cell("C1").Value);
        Assert.Null(reread.Cell("C1").Formula);
        Assert.Equal("SUM(B1:B2)", XlsxEditor.Open(edited).Sheet("Données").GetFormula("B3"));
    }

    [Fact]
    public void Dates_are_written_as_serials_of_the_workbook_date_system()
    {
        var date = new DateTime(2026, 10, 8);
        var editor = XlsxEditor.Open(XlsxFactory.Rich(date1904: true));

        editor.Sheet("Données").SetValue("B1", date);

        Assert.True(editor.Date1904);
        Assert.Equal(ExcelDate.ToSerial(date, date1904: true), editor.Sheet("Données").GetValue("B1"));
        Assert.Throws<ArgumentException>(() => editor.Sheet("Données").SetValue("B1", new DateTime(1900, 1, 1)));
    }

    [Fact]
    public void Implicit_row_and_cell_positions_are_written_out_before_an_edit()
    {
        // Rows and cells without r follow the previous one (ECMA-376 part 1, §18.3.1.73 and §18.3.1.4).
        var editor = XlsxEditor.Open(XlsxFactory.Rich("""<row><c><v>1</v></c><c><v>2</v></c></row><row><c><v>3</v></c></row><row r="5"><c r="C5"><v>4</v></c><c><v>5</v></c></row>"""));
        var sheet = editor.Sheet("Données");

        Assert.Equal(2.0, sheet.GetValue("B1"));
        Assert.Equal(5.0, sheet.GetValue("D5"));
        sheet.SetValue("B2", 9);

        var reread = XlsxWorkbook.Load(editor.ToArray()).Worksheet("Données");
        Assert.Equal([1.0, 2.0, 3.0, 9.0, 4.0, 5.0], new[] { "A1", "B1", "A2", "B2", "C5", "D5" }.Select(r => (double)reread.Cell(r).Value!));
    }

    [Fact]
    public void Edits_that_would_break_a_formula_group_or_a_table_are_refused()
    {
        var shared = """<row r="1"><c r="A1"><f t="shared" ref="A1:A3" si="0">B1</f><v>1</v></c></row><row r="2"><c r="A2"><f t="shared" si="0"/><v>1</v></c></row>"""
            + """<row r="4"><c r="B4"><f t="array" ref="B4:B5">C4:C5*2</f><v>0</v></c></row>""";
        var sheet = XlsxEditor.Open(XlsxFactory.Rich(shared)).Sheet("Données");

        Assert.Throws<NotSupportedException>(() => sheet.SetValue("A1", 1));
        Assert.Throws<NotSupportedException>(() => sheet.SetValue("B5", 1));
        Assert.Throws<NotSupportedException>(() => sheet.SetValue("F1", "Autre nom"));
        sheet.SetValue("A2", 7);
        sheet.SetValue("E2", "donnée de tableau");
        Assert.Equal(7.0, sheet.GetValue("A2"));
    }

    [Fact]
    public void Values_and_references_it_cannot_write_are_refused()
    {
        var editor = XlsxEditor.Open(XlsxFactory.Rich());
        var sheet = editor.Sheet("Données");

        Assert.Throws<ArgumentException>(() => sheet.SetValue("1A", 1));
        Assert.Throws<ArgumentException>(() => sheet.SetValue("A1", "a\u0001b"));
        Assert.Throws<ArgumentException>(() => sheet.SetValue("A1", new string('x', 32768)));
        Assert.Throws<ArgumentException>(() => sheet.SetValue("A1", double.NaN));
        Assert.Throws<ArgumentException>(() => sheet.SetValue("A1", new object()));
        Assert.Throws<ArgumentException>(() => sheet.SetFormula("A1", "="));
        Assert.Throws<ArgumentException>(() => editor.Sheet("Absente"));
        Assert.Empty(editor.ChangedParts);
        Assert.Equal(["Données", "Autre"], editor.SheetNames);
    }
}
