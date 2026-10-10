// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Editing;
using OmniEurope.Documents.Word.Validation;

namespace OmniEurope.Documents.Tests.Word;

public sealed class WordTrackedChangeTests
{
    private const string By = "w:author=\"Ann\" w:date=\"2026-01-02T03:04:05Z\"";

    private static readonly string Content = $"""
        <w:p><w:r><w:t xml:space="preserve">Keep </w:t></w:r><w:ins w:id="1" {By}><w:r><w:t>new</w:t></w:r></w:ins><w:del w:id="2" {By}><w:r><w:delText>old</w:delText></w:r></w:del></w:p>
        """;

    private static readonly string Move = $"""
        <w:p><w:moveFromRangeStart w:id="10" w:name="move1" {By}/><w:moveFrom w:id="11" {By}><w:r><w:t xml:space="preserve">Moved </w:t></w:r></w:moveFrom><w:moveFromRangeEnd w:id="10"/><w:r><w:t>stays</w:t></w:r></w:p>
        <w:p><w:r><w:t xml:space="preserve">Here: </w:t></w:r><w:moveToRangeStart w:id="12" w:name="move1" {By}/><w:moveTo w:id="13" {By}><w:r><w:t xml:space="preserve">Moved </w:t></w:r></w:moveTo><w:moveToRangeEnd w:id="12"/></w:p>
        """;

    private static readonly string Marks = $"""
        <w:p><w:pPr><w:rPr><w:del w:id="20" {By}/></w:rPr></w:pPr><w:r><w:t>First</w:t></w:r></w:p>
        <w:p><w:pPr><w:jc w:val="center"/><w:rPr><w:ins w:id="21" {By}/></w:rPr></w:pPr><w:r><w:t xml:space="preserve"> second</w:t></w:r></w:p>
        <w:p><w:r><w:t xml:space="preserve"> third</w:t></w:r></w:p>
        """;

    private static readonly string Formatting = $"""
        <w:p><w:pPr><w:jc w:val="right"/><w:pPrChange w:id="31" {By}><w:pPr><w:jc w:val="left"/></w:pPr></w:pPrChange></w:pPr><w:r><w:rPr><w:b/><w:rPrChange w:id="30" {By}><w:rPr><w:i/></w:rPr></w:rPrChange></w:rPr><w:t>Styled</w:t></w:r></w:p>
        <w:p><w:pPr><w:numPr><w:ilvl w:val="0"/><w:numId w:val="1"/><w:ins w:id="32" {By}/></w:numPr><w:numberingChange w:id="33" {By} w:original="1."/></w:pPr><w:r><w:t>Listed</w:t></w:r></w:p>
        """;

    private static readonly string Table = $"""
        <w:tbl><w:tblPr><w:tblW w:w="0" w:type="auto"/><w:tblPrChange w:id="40" {By}><w:tblPr><w:tblW w:w="5000" w:type="pct"/></w:tblPr></w:tblPrChange></w:tblPr>
        <w:tblGrid><w:gridCol w:w="2000"/><w:gridCol w:w="2000"/><w:tblGridChange w:id="47"><w:tblGrid><w:gridCol w:w="3000"/><w:gridCol w:w="1000"/></w:tblGrid></w:tblGridChange></w:tblGrid>
        <w:tr><w:trPr><w:ins w:id="41" {By}/></w:trPr><w:tc><w:p><w:r><w:t>InsertedRow</w:t></w:r></w:p></w:tc><w:tc><w:p/></w:tc></w:tr>
        <w:tr><w:trPr><w:del w:id="42" {By}/></w:trPr><w:tc><w:p><w:r><w:t>DeletedRow</w:t></w:r></w:p></w:tc><w:tc><w:p/></w:tc></w:tr>
        <w:tr><w:trPr><w:cantSplit/><w:trPrChange w:id="48" {By}><w:trPr><w:tblHeader/></w:trPr></w:trPrChange></w:trPr><w:tc><w:tcPr><w:tcW w:w="2000" w:type="dxa"/><w:tcPrChange w:id="43" {By}><w:tcPr><w:tcW w:w="1000" w:type="dxa"/></w:tcPr></w:tcPrChange></w:tcPr><w:p><w:r><w:t>A</w:t></w:r></w:p></w:tc><w:tc><w:tcPr><w:cellIns w:id="44" {By}/></w:tcPr><w:p><w:r><w:t>NewCell</w:t></w:r></w:p></w:tc></w:tr>
        <w:tr><w:tc><w:tcPr><w:cellMerge w:id="46" {By} w:vMerge="rest"/></w:tcPr><w:p><w:r><w:t>B</w:t></w:r></w:p></w:tc><w:tc><w:tcPr><w:cellDel w:id="45" {By}/></w:tcPr><w:p><w:r><w:t>OldCell</w:t></w:r></w:p></w:tc></w:tr>
        </w:tbl><w:p/>
        <w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:sectPrChange w:id="50" {By}><w:sectPr><w:pgSz w:w="16838" w:h="11906" w:orient="landscape"/></w:sectPr></w:sectPrChange></w:sectPr>
        """;

    private static readonly string Numbering = """
        <?xml version="1.0" encoding="UTF-8"?><w:numbering xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:abstractNum w:abstractNumId="0"><w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="decimal"/><w:lvlText w:val="%1."/></w:lvl></w:abstractNum><w:num w:numId="1"><w:abstractNumId w:val="0"/></w:num></w:numbering>
        """;

    private static byte[] Document(string body) => DocxFactory.Build(body, extraParts: new Dictionary<string, string> { ["word/numbering.xml"] = Numbering },
        documentRelationships: """<Relationship Id="rIdN" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering" Target="numbering.xml"/>""",
        contentTypes: new Dictionary<string, string> { ["word/numbering.xml"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml" });

    private static (WordDocument Model, byte[] Bytes) All(string body, bool accept)
    {
        var editor = WordEditor.Open(Document(body));
        var listed = editor.TrackedChanges().Count;

        var applied = accept ? editor.AcceptAllChanges() : editor.RejectAllChanges();

        Assert.Equal(listed, applied);
        Assert.Empty(editor.TrackedChanges());
        var bytes = editor.ToArray();
        var report = WordSchemaValidator.Validate(bytes);
        Assert.True(report.IsValid, string.Join("\n", report.Errors.Select(e => e.ToString())));
        return (WordDocument.Load(bytes), bytes);
    }

    private static List<string> Texts(WordDocument model) => model.Blocks.OfType<WordParagraph>().Select(p => p.Text).ToList();

    [Fact]
    public void Lists_every_kind_with_its_author_date_and_text()
    {
        var editor = WordEditor.Open(Document(Content + Move + Marks + Formatting + Table));

        var changes = editor.TrackedChanges();

        Assert.Equal(
            [
                WordTrackedChangeKind.Insertion, WordTrackedChangeKind.Deletion, WordTrackedChangeKind.Move, WordTrackedChangeKind.ParagraphMarkDeletion,
                WordTrackedChangeKind.ParagraphMarkInsertion, WordTrackedChangeKind.ParagraphFormatting, WordTrackedChangeKind.RunFormatting,
                WordTrackedChangeKind.NumberingInsertion, WordTrackedChangeKind.NumberingChange, WordTrackedChangeKind.TableFormatting,
                WordTrackedChangeKind.TableFormatting, WordTrackedChangeKind.RowInsertion, WordTrackedChangeKind.RowDeletion,
                WordTrackedChangeKind.TableFormatting, WordTrackedChangeKind.TableFormatting, WordTrackedChangeKind.CellInsertion,
                WordTrackedChangeKind.CellMerge, WordTrackedChangeKind.CellDeletion, WordTrackedChangeKind.SectionFormatting,
            ],
            changes.Select(c => c.Kind));
        Assert.Equal(Enumerable.Range(0, changes.Count), changes.Select(c => c.Index));
        Assert.Equal("new", changes[0].Text);
        Assert.Equal("old", changes[1].Text);
        Assert.Equal(("move1", "Moved "), (changes[2].Id, changes[2].Text));
        Assert.Equal("Styled", changes[6].Text);
        Assert.Equal(("Ann", new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)), (changes[0].Author, changes[0].Date));
        Assert.All(changes, c => Assert.Equal("word/document.xml", c.PartName));
    }

    [Fact]
    public void Accepting_keeps_insertions_and_drops_deletions()
    {
        Assert.Equal(["Keep new"], Texts(All(Content, accept: true).Model));
        Assert.Equal(["Keep old"], Texts(All(Content, accept: false).Model));
    }

    [Fact]
    public void A_named_move_is_accepted_or_rejected_at_both_ends()
    {
        Assert.Equal(["stays", "Here: Moved "], Texts(All(Move, accept: true).Model));
        var (model, bytes) = All(Move, accept: false);
        Assert.Equal(["Moved stays", "Here: "], Texts(model));
        Assert.DoesNotContain("moveFromRange", DocxFactory.Part(bytes, "word/document.xml"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_paragraph_mark_that_goes_joins_the_paragraph_with_the_next_one()
    {
        var accepted = All(Marks, accept: true).Model;
        Assert.Equal(["First second", " third"], Texts(accepted));
        Assert.Equal(WordAlignment.Center, accepted.Blocks.OfType<WordParagraph>().First().Properties.Alignment);

        Assert.Equal(["First", " second third"], Texts(All(Marks, accept: false).Model));
    }

    [Fact]
    public void Formatting_changes_keep_the_new_or_restore_the_former_properties()
    {
        var accepted = All(Formatting, accept: true).Model.Blocks.OfType<WordParagraph>().ToList();
        var run = accepted[0].Inlines.OfType<WordText>().Single();
        Assert.Equal((true, (bool?)null, WordAlignment.Right), (run.Properties.Bold, run.Properties.Italic, accepted[0].Properties.Alignment));
        Assert.Equal(1, accepted[1].Properties.NumberingId);

        var rejected = All(Formatting, accept: false).Model.Blocks.OfType<WordParagraph>().ToList();
        run = rejected[0].Inlines.OfType<WordText>().Single();
        Assert.Equal(((bool?)null, true, WordAlignment.Left), (run.Properties.Bold, run.Properties.Italic, rejected[0].Properties.Alignment));
        Assert.Null(rejected[1].Properties.NumberingId);
    }

    [Fact]
    public void Row_cell_table_and_section_changes_follow_the_decision()
    {
        var (accepted, acceptedBytes) = All(Table, accept: true);
        var table = accepted.Blocks.OfType<WordTable>().Single();
        Assert.Equal(["InsertedRow|", "A|NewCell", "B"], table.Rows.Select(r => string.Join('|', r.Cells.Select(c => c.Text))));
        Assert.Equal(WordVerticalMerge.Restart, table.Rows[2].Cells[0].Properties.VerticalMerge);
        Assert.Equal(595.3, accepted.Sections[^1].Page.Width, 1);
        var xml = DocxFactory.Part(acceptedBytes, "word/document.xml");
        Assert.Contains("<w:cantSplit />", xml, StringComparison.Ordinal);
        Assert.Contains("<w:gridCol w:w=\"2000\" />", xml, StringComparison.Ordinal);

        var (rejected, rejectedBytes) = All(Table, accept: false);
        table = rejected.Blocks.OfType<WordTable>().Single();
        Assert.Equal(["DeletedRow|", "A", "B|OldCell"], table.Rows.Select(r => string.Join('|', r.Cells.Select(c => c.Text))));
        Assert.Null(table.Rows[2].Cells[0].Properties.VerticalMerge);
        Assert.Equal(841.9, rejected.Sections[^1].Page.Width, 1);
        xml = DocxFactory.Part(rejectedBytes, "word/document.xml");
        Assert.Contains("<w:tblHeader />", xml, StringComparison.Ordinal);
        Assert.Contains("<w:tblW w:w=\"5000\" w:type=\"pct\" />", xml, StringComparison.Ordinal);
        Assert.Contains("<w:tcW w:w=\"1000\" w:type=\"dxa\" />", xml, StringComparison.Ordinal);
        Assert.Contains("<w:gridCol w:w=\"3000\" />", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void A_table_whose_rows_all_go_is_removed_and_its_cell_keeps_a_paragraph()
    {
        var body = $"""
            <w:tbl><w:tblPr/><w:tblGrid><w:gridCol w:w="2000"/></w:tblGrid><w:tr><w:tc><w:tbl><w:tblPr/><w:tblGrid><w:gridCol w:w="1000"/></w:tblGrid>
            <w:tr><w:trPr><w:ins w:id="1" {By}/></w:trPr><w:tc><w:p><w:r><w:t>gone</w:t></w:r></w:p></w:tc></w:tr></w:tbl></w:tc></w:tr></w:tbl><w:p/>
            """;

        var (model, _) = All(body, accept: false);

        var cell = model.Blocks.OfType<WordTable>().Single().Rows[0].Cells[0];
        Assert.IsType<WordParagraph>(Assert.Single(cell.Blocks));
    }

    [Fact]
    public void Changes_are_accepted_or_rejected_one_by_one()
    {
        var editor = WordEditor.Open(Document(Content));
        var changes = editor.TrackedChanges();

        editor.RejectChange(changes[0]);
        var remaining = Assert.Single(editor.TrackedChanges());
        Assert.Equal(WordTrackedChangeKind.Deletion, remaining.Kind);
        Assert.Throws<ArgumentException>(() => editor.AcceptChange(changes[1]));
        editor.RejectChange(remaining);

        Assert.Equal("Keep old", WordDocument.Load(editor.ToArray()).Text);
        Assert.True(WordSchemaValidator.Validate(editor.ToArray()).IsValid);
    }

    [Fact]
    public void Rejected_deleted_fields_come_back_and_nested_changes_follow_their_container()
    {
        var body = $"""
            <w:p><w:del w:id="1" {By}><w:r><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:delInstrText xml:space="preserve"> PAGE </w:delInstrText></w:r><w:r><w:fldChar w:fldCharType="separate"/></w:r><w:r><w:delText>7</w:delText></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r></w:del>
            <w:ins w:id="2" {By}><w:r><w:t>a</w:t></w:r><w:del w:id="3" {By}><w:r><w:delText>b</w:delText></w:r></w:del></w:ins></w:p>
            """;

        var rejected = All(body, accept: false).Model.Blocks.OfType<WordParagraph>().Single();
        Assert.Equal("PAGE", rejected.Inlines.OfType<WordField>().Single().Instruction);
        Assert.Equal("7", rejected.Text);

        Assert.Equal("a", All(body, accept: true).Model.Text);
    }

    [Fact]
    public void Styles_headers_and_fallbacks_are_covered_by_accept_all()
    {
        var styles = $"""
            <?xml version="1.0" encoding="UTF-8"?><w:styles xmlns:w="{DocxFactory.W}"><w:style w:type="paragraph" w:styleId="Body"><w:name w:val="Body"/><w:rPr><w:b/><w:rPrChange w:id="5" {By}><w:rPr/></w:rPrChange></w:rPr></w:style></w:styles>
            """;
        var header = $"""
            <?xml version="1.0" encoding="UTF-8"?><w:hdr xmlns:w="{DocxFactory.W}"><w:p><w:ins w:id="6" {By}><w:r><w:t>Head</w:t></w:r></w:ins></w:p></w:hdr>
            """;
        var body = $"""
            <w:p><mc:AlternateContent><mc:Choice Requires="w"><w:ins w:id="8" {By}><w:r><w:t>x</w:t></w:r></w:ins></mc:Choice><mc:Fallback><w:ins w:id="9" {By}><w:r><w:t>x</w:t></w:r></w:ins></mc:Fallback></mc:AlternateContent></w:p>
            <w:p><w:ins w:id="7" {By}><w:r><w:t>Body</w:t></w:r></w:ins></w:p>
            <w:sectPr><w:headerReference w:type="default" r:id="rIdH"/></w:sectPr>
            """;
        var package = DocxFactory.Build(body, extraParts: new Dictionary<string, string> { ["word/styles.xml"] = styles, ["word/header1.xml"] = header },
            documentRelationships: """<Relationship Id="rIdS" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/><Relationship Id="rIdH" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/header" Target="header1.xml"/>""");
        var editor = WordEditor.Open(package);

        Assert.Equal(["word/document.xml", "word/document.xml", "word/header1.xml", "word/styles.xml"], editor.TrackedChanges().Select(c => c.PartName));
        Assert.Equal(4, editor.AcceptAllChanges());

        Assert.Empty(editor.TrackedChanges());
        Assert.Equal(["word/document.xml", "word/header1.xml", "word/styles.xml"], editor.ChangedParts.Order(StringComparer.Ordinal));
        Assert.DoesNotContain("rPrChange", DocxFactory.Part(editor.ToArray(), "word/styles.xml"), StringComparison.Ordinal);
        Assert.DoesNotContain("<w:ins", DocxFactory.Part(editor.ToArray(), "word/document.xml"), StringComparison.Ordinal);
        Assert.True(WordSchemaValidator.Validate(editor.ToArray()).IsValid);
    }
}
