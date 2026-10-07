// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;
using static OmniEurope.Documents.Tests.Word.DocxFactory;

namespace OmniEurope.Documents.Tests.Word;

/// <summary>
/// Reading WordprocessingML written by hand (ECMA-376 part 1): content controls and custom XML around table
/// rows, deleted rows, alternate content, equations, foreign run content, break types and image parts that
/// are shared or missing.
/// </summary>
public sealed class WordReadingTests
{
    private const string Vml = "xmlns:v=\"urn:schemas-microsoft-com:vml\"";

    [Fact]
    public void Rows_inside_content_controls_and_custom_xml_belong_to_the_table()
    {
        var document = WordDocument.Load(Build(
            "<w:tbl><w:tr><w:tc><w:p><w:r><w:t>un</w:t></w:r></w:p></w:tc></w:tr>"
            + "<w:sdt><w:sdtContent><w:tr><w:tc><w:p><w:r><w:t>deux</w:t></w:r></w:p></w:tc></w:tr></w:sdtContent></w:sdt>"
            + "<w:customXml w:element=\"ligne\"><w:tr><w:trPr><w:del w:id=\"1\" w:author=\"Relecteur\"/></w:trPr><w:tc><w:p><w:r><w:t>trois</w:t></w:r></w:p></w:tc></w:tr></w:customXml></w:tbl>"));

        var table = Assert.Single(document.Blocks.OfType<WordTable>());
        Assert.Equal(["un", "deux", "trois"], table.Rows.Select(r => r.Cells[0].Text));
        Assert.Equal((WordRevisionKind.Deleted, "Relecteur"), (table.Rows[2].Revision!.Kind, table.Rows[2].Revision!.Author));
    }

    [Fact]
    public void Alternate_content_takes_the_choice_and_equations_their_text()
    {
        var document = WordDocument.Load(Build(
            "<w:p><mc:AlternateContent><mc:Choice Requires=\"w14\"><w:r><w:t>choix</w:t></w:r></mc:Choice><mc:Fallback><w:r><w:t>repli</w:t></w:r></mc:Fallback></mc:AlternateContent></w:p>"
            + "<w:p><m:oMath xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\"><m:r><m:t>x=</m:t></m:r><m:r><m:t>1</m:t></m:r></m:oMath></w:p>"
            + "<w:p><w:r><x:extra xmlns:x=\"urn:example\"/><w:t>simple</w:t></w:r></w:p>"));

        Assert.Equal(["choix", "x=1", "simple"], document.Blocks.Select(b => b.Text));
        Assert.Contains("equations read as plain text", document.Gaps);
    }

    [Fact]
    public void Break_types_are_read()
    {
        var document = WordDocument.Load(Build("<w:p><w:r><w:br w:type=\"page\"/><w:br w:type=\"column\"/><w:br/><w:br w:type=\"textWrapping\"/></w:r></w:p>"));

        var kinds = document.Blocks.OfType<WordParagraph>().First().Inlines.OfType<WordBreak>().Select(b => b.Kind);
        Assert.Equal([WordBreakKind.Page, WordBreakKind.Column, WordBreakKind.Line, WordBreakKind.Line], kinds);
    }

    [Fact]
    public void A_shared_image_part_is_read_once_and_a_missing_one_reported()
    {
        const string type = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";
        var document = WordDocument.Load(Build(
            $"<w:p><w:r><w:pict {Vml}><v:shape style=\"width:20pt;height:10pt\"><v:imagedata r:id=\"rId1\"/></v:shape></w:pict></w:r>"
            + $"<w:r><w:pict {Vml}><v:shape style=\"width:20pt;height:10pt\"><v:imagedata r:id=\"rId1\"/></v:shape></w:pict></w:r>"
            + $"<w:r><w:pict {Vml}><v:shape style=\"width:20pt;height:10pt\"><v:imagedata r:id=\"rId2\"/></v:shape></w:pict></w:r></w:p>",
            documentRelationships: $"<Relationship Id=\"rId1\" Type=\"{type}\" Target=\"media/image1.png\"/><Relationship Id=\"rId2\" Type=\"{type}\" Target=\"media/absente.png\"/>",
            binaryParts: new Dictionary<string, byte[]> { ["word/media/image1.png"] = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "gray.png")) }));

        var pictures = document.Blocks.OfType<WordParagraph>().First().Inlines.OfType<WordPicture>().ToList();
        Assert.Equal(2, pictures.Count);
        Assert.Same(pictures[0].Image, pictures[1].Image);
        Assert.Equal((20.0, 10.0), (pictures[0].Width, pictures[0].Height));
        Assert.Contains("missing image part", document.Gaps);
    }

    [Fact]
    public void Section_footnote_numbering_is_read_and_written_back()
    {
        var document = WordDocument.Load(Build(
            "<w:p><w:pPr><w:sectPr><w:footnotePr><w:numFmt w:val=\"upperRoman\"/><w:numStart w:val=\"3\"/><w:numRestart w:val=\"eachSect\"/></w:footnotePr></w:sectPr></w:pPr></w:p>"
            + "<w:p><w:pPr><w:sectPr><w:footnotePr><w:numRestart w:val=\"eachPage\"/></w:footnotePr></w:sectPr></w:pPr></w:p>"
            + "<w:p><w:pPr><w:sectPr><w:footnotePr><w:pos w:val=\"beneathText\"/></w:footnotePr></w:sectPr></w:pPr></w:p>"
            + "<w:sectPr><w:footnotePr><w:numRestart w:val=\"continuous\"/></w:footnotePr></w:sectPr>"));

        var read = document.Sections.Select(s => s.Page.FootnoteNumbering).ToList();

        Assert.Equal(new WordNoteNumbering { Format = WordNumberFormat.UpperRoman, Start = 3, Restart = WordNoteRestart.EachSection }, read[0]);
        Assert.Equal(new WordNoteNumbering { Restart = WordNoteRestart.EachPage }, read[1]);
        Assert.Null(read[2]);
        Assert.Equal(new WordNoteNumbering { Restart = WordNoteRestart.Continuous }, read[3]);
        var saved = document.ToArray();
        Assert.Contains("<w:footnotePr><w:numFmt w:val=\"upperRoman\" /><w:numStart w:val=\"3\" /><w:numRestart w:val=\"eachSect\" /></w:footnotePr>", Part(saved, "word/document.xml"), StringComparison.Ordinal);
        Assert.Equal(read, WordDocument.Load(saved).Sections.Select(s => s.Page.FootnoteNumbering));
    }

    [Fact]
    public void Paragraphs_remember_the_address_the_editor_gives_them()
    {
        var bytes = Build(
            "<w:p><w:r><w:t>Un</w:t></w:r></w:p>"
            + "<w:p><w:r><mc:AlternateContent><mc:Choice Requires=\"wps\"><w:t>choix</w:t></mc:Choice><mc:Fallback><w:pict><w:p><w:r><w:t>repli</w:t></w:r></w:p></w:pict></mc:Fallback></mc:AlternateContent></w:r></w:p>"
            + "<w:tbl><w:tr><w:tc><w:p><w:r><w:t>Cellule</w:t></w:r></w:p></w:tc></w:tr></w:tbl>");

        var document = WordDocument.Load(bytes);
        var editor = Documents.Word.Editing.WordEditor.Open(bytes).Paragraphs();

        var paragraphs = document.Blocks.OfType<WordParagraph>().Concat(document.Blocks.OfType<WordTable>().SelectMany(t => t.Rows[0].Cells[0].Blocks.OfType<WordParagraph>())).ToList();
        Assert.Equal(["word/document.xml#0", "word/document.xml#1", "word/document.xml#2"], paragraphs.Select(p => p.SourceAddress));
        Assert.Equal(editor.Select(p => p.Address), paragraphs.Select(p => p.SourceAddress));
        Assert.Null(new WordParagraph("construit").SourceAddress);
    }
}
