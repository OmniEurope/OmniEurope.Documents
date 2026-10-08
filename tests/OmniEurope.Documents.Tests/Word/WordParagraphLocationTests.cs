// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word.Editing;
using static OmniEurope.Documents.Tests.Word.DocxFactory;
using Step = OmniEurope.Documents.Word.Editing.WordPathStep;

namespace OmniEurope.Documents.Tests.Word;

/// <summary>Where <see cref="WordEditor.Paragraphs"/> finds each paragraph: part, note, comment and block path.</summary>
public sealed class WordParagraphLocationTests
{
    private const string Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
    private const string Wps = "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";

    private static readonly string TextBoxes =
        "<w:p><w:r><w:t>Anchor</w:t></w:r><w:r><mc:AlternateContent><mc:Choice Requires=\"wps\"><w:drawing>"
        + "<wp:anchor xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\"><a:graphic xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\">"
        + "<a:graphicData uri=\"" + Wps + "\"><wps:wsp xmlns:wps=\"" + Wps + "\"><wps:txbx><w:txbxContent>"
        + "<w:p><w:r><w:t>Box</w:t></w:r><w:r><w:pict><v:shape xmlns:v=\"urn:schemas-microsoft-com:vml\"><v:textbox><w:txbxContent><w:p><w:r><w:t>Inner</w:t></w:r></w:p></w:txbxContent></v:textbox></v:shape></w:pict></w:r></w:p>"
        + "</w:txbxContent></wps:txbx></wps:wsp></a:graphicData></a:graphic></wp:anchor></w:drawing></mc:Choice>"
        + "<mc:Fallback><w:pict><v:shape xmlns:v=\"urn:schemas-microsoft-com:vml\"><v:textbox><w:txbxContent><w:p><w:r><w:t>Box copy</w:t></w:r></w:p></w:txbxContent></v:textbox></v:shape></w:pict></mc:Fallback>"
        + "</mc:AlternateContent></w:r>"
        + "<w:r><w:pict><v:shape xmlns:v=\"urn:schemas-microsoft-com:vml\"><v:textbox><w:txbxContent><w:p><w:r><w:t>Box2</w:t></w:r></w:p></w:txbxContent></v:textbox></v:shape></w:pict></w:r></w:p>";

    private static readonly string Body =
        "<w:p><w:r><w:t>Intro</w:t></w:r></w:p><w:bookmarkStart w:id=\"0\" w:name=\"b\"/><w:bookmarkEnd w:id=\"0\"/>"
        + "<w:tbl><w:tblPr><w:tblStyle w:val=\"Grid\"/></w:tblPr><w:tblGrid/>"
        + "<w:tr><w:tc><w:p><w:r><w:t>A1</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>B1</w:t></w:r></w:p><w:p><w:r><w:t>B1b</w:t></w:r></w:p></w:tc></w:tr>"
        + "<w:sdt><w:sdtContent><w:tr><w:tc><w:p><w:r><w:t>A2</w:t></w:r></w:p></w:tc><w:sdt><w:sdtContent><w:tc>"
        + "<w:tbl><w:tr><w:tc><w:p><w:r><w:t>Nested</w:t></w:r></w:p></w:tc></w:tr></w:tbl><w:p/></w:tc></w:sdtContent></w:sdt></w:tr></w:sdtContent></w:sdt>"
        + "</w:tbl>"
        + "<w:sdt><w:sdtPr/><w:sdtContent><w:p><w:r><w:t>InControl</w:t></w:r></w:p></w:sdtContent></w:sdt>"
        + "<w:customXml w:element=\"x\"><w:p><w:r><w:t>InCustom</w:t></w:r></w:p></w:customXml>"
        + TextBoxes
        + "<w:p><w:r><w:t>After</w:t></w:r></w:p><w:sectPr/>";

    private static readonly Dictionary<string, string> Parts = new()
    {
        ["word/header1.xml"] = $"<w:hdr xmlns:w=\"{W}\"><w:p><w:r><w:t>Head</w:t></w:r></w:p></w:hdr>",
        ["word/footer1.xml"] = $"<w:ftr xmlns:w=\"{W}\"><w:p><w:r><w:t>Foot</w:t></w:r></w:p></w:ftr>",
        ["word/footnotes.xml"] = $"<w:footnotes xmlns:w=\"{W}\"><w:footnote w:type=\"separator\" w:id=\"-1\"><w:p><w:r><w:separator/></w:r></w:p></w:footnote>"
            + "<w:footnote w:type=\"continuationSeparator\" w:id=\"0\"><w:p><w:r><w:continuationSeparator/></w:r></w:p></w:footnote>"
            + "<w:footnote w:id=\"1\"><w:p><w:r><w:footnoteRef/></w:r><w:r><w:t xml:space=\"preserve\"> A note.</w:t></w:r></w:p></w:footnote></w:footnotes>",
        ["word/endnotes.xml"] = $"<w:endnotes xmlns:w=\"{W}\"><w:endnote w:id=\"0\"><w:p/></w:endnote><w:endnote w:id=\"2\" w:type=\"normal\"><w:p><w:r><w:t>End</w:t></w:r></w:p></w:endnote></w:endnotes>",
        ["word/comments.xml"] = $"<w:comments xmlns:w=\"{W}\"><w:comment w:id=\"3\" w:author=\"A\"><w:p><w:r><w:t>Remark</w:t></w:r></w:p></w:comment></w:comments>",
    };

    private static readonly string Relationships =
        $"<Relationship Id=\"rId5\" Type=\"{Rel}footnotes\" Target=\"footnotes.xml\"/><Relationship Id=\"rId6\" Type=\"{Rel}endnotes\" Target=\"endnotes.xml\"/>"
        + $"<Relationship Id=\"rId7\" Type=\"{Rel}header\" Target=\"header1.xml\"/><Relationship Id=\"rId8\" Type=\"{Rel}footer\" Target=\"footer1.xml\"/>"
        + $"<Relationship Id=\"rId9\" Type=\"{Rel}comments\" Target=\"comments.xml\"/><Relationship Id=\"rId11\" Type=\"{Rel}header\" Target=\"header1.xml\"/>";

    internal static byte[] Document() => Build(Body, extraParts: Parts, documentRelationships: Relationships);

    [Fact]
    public void Paragraphs_come_in_document_order_with_their_block_paths()
    {
        var body = WordEditor.Open(Document()).Paragraphs().Where(p => p.Location.PartKind == WordPartKind.Body).ToList();

        Assert.Equal(["Intro", "A1", "B1", "B1b", "A2", "Nested", "", "InControl", "InCustom", "Anchor", "Box", "Inner", "Box2", "After"], body.Select(p => p.EditableText));
        Assert.Equal(
            [
                [Step.Block(0)],
                [Step.Block(1), Step.Row(0), Step.Cell(0), Step.Block(0)],
                [Step.Block(1), Step.Row(0), Step.Cell(1), Step.Block(0)],
                [Step.Block(1), Step.Row(0), Step.Cell(1), Step.Block(1)],
                [Step.Block(1), Step.Row(1), Step.Cell(0), Step.Block(0)],
                [Step.Block(1), Step.Row(1), Step.Cell(1), Step.Block(0), Step.Row(0), Step.Cell(0), Step.Block(0)],
                [Step.Block(1), Step.Row(1), Step.Cell(1), Step.Block(1)],
                [Step.Block(2), Step.ContentControl, Step.Block(0)],
                [Step.Block(3), Step.CustomXml, Step.Block(0)],
                [Step.Block(4)],
                [Step.Block(4), Step.TextBox(0), Step.Block(0)],
                [Step.Block(4), Step.TextBox(0), Step.Block(0), Step.TextBox(0), Step.Block(0)],
                [Step.Block(4), Step.TextBox(1), Step.Block(0)],
                new[] { Step.Block(5) },
            ],
            body.Select(p => p.Location.Path.ToArray()));
        Assert.All(body, p => Assert.Equal("word/document.xml", p.Location.PartName));
        Assert.All(body, p => Assert.Null(p.Location.RelationshipId ?? (object?)p.Location.NoteId ?? p.Location.CommentId));
        Assert.Equal("word/document.xml/Block(1)/Row(1)/Cell(1)/Block(0)/Row(0)/Cell(0)/Block(0)", body[5].Location.ToString());
        Assert.Equal("word/document.xml/Block(2)/ContentControl/Block(0)", body[7].Location.ToString());
    }

    [Fact]
    public void Headers_footers_notes_and_comments_carry_their_ids()
    {
        var editor = WordEditor.Open(Document());

        var others = editor.Paragraphs().Where(p => p.Location.PartKind != WordPartKind.Body).Select(p => p.Location).ToList();

        Assert.Equal(["word/document.xml", "word/header1.xml", "word/footer1.xml", "word/footnotes.xml", "word/endnotes.xml", "word/comments.xml"], editor.TextParts);
        Assert.Equal(
            [WordPartKind.Header, WordPartKind.Footer, WordPartKind.Footnote, WordPartKind.Footnote, WordPartKind.Footnote, WordPartKind.Endnote, WordPartKind.Endnote, WordPartKind.Comment],
            others.Select(l => l.PartKind));
        Assert.Equal(["rId7", "rId8", null, null, null, null, null, null], others.Select(l => l.RelationshipId));
        Assert.Equal([null, null, -1, 0, 1, 0, 2, null], others.Select(l => l.NoteId));
        Assert.Equal([false, false, true, true, false, true, false, false], others.Select(l => l.IsSeparatorNote));
        Assert.Equal([null, null, null, null, null, null, null, 3], others.Select(l => l.CommentId));
        Assert.All(others, l => Assert.Equal([Step.Block(0)], l.Path));
        Assert.Equal("word/footnotes.xml/Note(1)/Block(0)", others[4].ToString());
        Assert.Equal("word/comments.xml/Comment(3)/Block(0)", others[7].ToString());
        Assert.Equal("word/header1.xml/Block(0)", others[0].ToString());
    }

    [Fact]
    public void Order_addresses_and_locations_survive_a_save_and_an_edit()
    {
        var editor = WordEditor.Open(Document());
        var before = editor.Paragraphs().Select(p => (p.Address, p.Location)).ToList();

        var again = WordEditor.Open(editor.ToArray());
        again.FindParagraph(before[3].Location)!.SetContent([new WordTextPiece("B1 traduit")]);
        var after = WordEditor.Open(again.ToArray()).Paragraphs().Select(p => (p.Address, p.Location)).ToList();

        Assert.Empty(editor.ChangedParts);
        Assert.Equal(before, after);
        Assert.Equal("B1 traduit", WordEditor.Open(again.ToArray()).FindParagraph(before[3].Location)!.EditableText);
    }

    [Fact]
    public void Paragraphs_in_a_cell_a_content_control_and_a_text_box_are_found_by_location_and_rewritten()
    {
        var targets = new[]
        {
            new WordParagraphLocation("word/document.xml", WordPartKind.Body, [Step.Block(1), Step.Row(1), Step.Cell(1), Step.Block(0), Step.Row(0), Step.Cell(0), Step.Block(0)]),
            new WordParagraphLocation("WORD/document.xml", WordPartKind.Body, [Step.Block(2), Step.ContentControl, Step.Block(0)]),
            new WordParagraphLocation("word/document.xml", WordPartKind.Body, [Step.Block(4), Step.TextBox(0), Step.Block(0)]),
            new WordParagraphLocation("word/header1.xml", WordPartKind.Header, [Step.Block(0)]) { RelationshipId = "rId7" },
        };
        var editor = WordEditor.Open(Document());

        foreach (var target in targets)
        {
            var paragraph = editor.FindParagraph(target)!;
            paragraph.SetContent([new WordTextPiece(paragraph.EditableText + " (fr)")], "fr-FR");
        }

        var reread = WordEditor.Open(editor.ToArray());
        Assert.Equal(["Nested (fr)", "InControl (fr)", "Box (fr)", "Head (fr)"], targets.Select(t => reread.FindParagraph(t)!.EditableText));
        Assert.Equal([WordRunKind.Text, WordRunKind.Drawing], reread.FindParagraph(targets[2])!.Runs().Select(r => r.Kind));
        Assert.Equal("Inner", reread.FindParagraph(new WordParagraphLocation("word/document.xml", WordPartKind.Body, [Step.Block(4), Step.TextBox(0), Step.Block(0), Step.TextBox(0), Step.Block(0)]))!.EditableText);
        Assert.Equal("fr-FR", reread.FindParagraph(targets[1])!.Runs().Single().Properties.Language);
        Assert.Equal(["word/document.xml", "word/header1.xml"], editor.ChangedParts.Order(StringComparer.Ordinal));
        Assert.Null(editor.FindParagraph(new WordParagraphLocation("word/document.xml", WordPartKind.Body, [Step.Block(42)])));
    }

    [Fact]
    public void Locations_compare_by_value()
    {
        var path = new List<Step> { Step.Block(1), Step.Row(0) };
        var location = new WordParagraphLocation("word/document.xml", WordPartKind.Body, path);
        path.Add(Step.Cell(0));

        Assert.Equal([Step.Block(1), Step.Row(0)], location.Path);
        Assert.Equal(location, new WordParagraphLocation("Word/Document.xml", WordPartKind.Body, [Step.Block(1), Step.Row(0)]));
        Assert.Equal(location.GetHashCode(), new WordParagraphLocation("Word/Document.xml", WordPartKind.Body, [Step.Block(1), Step.Row(0)]).GetHashCode());
        Assert.NotEqual(location, location with { NoteId = 2 });
        Assert.NotEqual(location, location with { CommentId = 2 });
        Assert.NotEqual(location, location with { RelationshipId = "rId1" });
        Assert.NotEqual(location, location with { IsSeparatorNote = true });
        Assert.NotEqual(location, new WordParagraphLocation("word/document.xml", WordPartKind.Header, [Step.Block(1), Step.Row(0)]));
        Assert.NotEqual(location, new WordParagraphLocation("word/document.xml", WordPartKind.Body, [Step.Block(1)]));
        Assert.NotEqual(location, new WordParagraphLocation("word/header1.xml", WordPartKind.Body, [Step.Block(1), Step.Row(0)]));
        Assert.False(location.Equals(null));
        Assert.Equal("Block(3)", Step.Block(3).ToString());
        Assert.Equal("TextBox(1)", Step.TextBox(1).ToString());
        Assert.Equal("CustomXml", Step.CustomXml.ToString());
        Assert.Equal(new Step(WordPathStepKind.Cell, 2), Step.Cell(2));
        Assert.Throws<ArgumentNullException>(() => new WordParagraphLocation(null!, WordPartKind.Body, []));
        Assert.Throws<ArgumentNullException>(() => new WordParagraphLocation("p", WordPartKind.Body, null!));
        Assert.Throws<ArgumentNullException>(() => WordEditor.Open(Document()).FindParagraph((WordParagraphLocation)null!));
    }
}
