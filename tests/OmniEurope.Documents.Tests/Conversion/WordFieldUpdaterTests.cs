// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Tests.Word;
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Editing;
using OmniEurope.Documents.Word.Validation;

namespace OmniEurope.Documents.Tests.Conversion;

public sealed class WordFieldUpdaterTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 4, 15, 6, 7, TimeSpan.Zero);

    private static byte[] Report()
    {
        var document = new WordDocument();
        document.AddParagraph("Contents");
        document.AddParagraph().Add(new WordField("TOC \\o \"1-2\" \\h \\z \\u", "Right-click to update"));
        document.AddPageBreak();
        document.AddHeading("Introduction", 1);
        document.AddParagraph("Opening words.");
        document.AddHeading("Scope", 2);
        document.AddHeading("Deep level left out", 3);
        document.AddPageBreak();
        document.AddHeading("Method", 1);
        foreach (var i in Enumerable.Range(1, 60))
        {
            document.AddParagraph("Line " + i.ToString(CultureInfo.InvariantCulture) + " of a long method description that fills the page.");
        }

        document.AddHeading("Results", 2);
        document.AddPageBreak();
        document.AddHeading("Annex", 1);
        return document.ToArray();
    }

    private static void AssertValid(byte[] package)
    {
        var report = WordSchemaValidator.Validate(package);
        Assert.True(report.IsValid, string.Join("\n", report.Errors.Select(e => e.ToString())));
    }

    [Fact]
    public void The_table_of_contents_lists_the_headings_with_the_pages_of_the_pdf()
    {
        var editor = WordEditor.Open(Report());

        var update = WordFieldUpdater.Update(editor, new WordFieldOptions { Now = Now });

        var bytes = editor.ToArray();
        AssertValid(bytes);
        Assert.Equal(5, update.TableOfContentsEntries);
        var model = WordDocument.Load(bytes);
        var entries = model.Blocks.OfType<WordParagraph>().Where(p => p.StyleId?.StartsWith("TOC", StringComparison.Ordinal) == true).ToList();
        Assert.Equal(["Introduction", "Scope", "Method", "Results", "Annex"], entries.Select(e => e.Text.Split('\t')[0]));
        Assert.Equal(["TOC1", "TOC2", "TOC1", "TOC2", "TOC1"], entries.Select(e => e.StyleId));

        var pdf = PdfDocument.Open(WordToPdf.Convert(bytes).Pdf);
        Assert.Equal(update.PageCount, pdf.PageCount);
        foreach (var entry in entries)
        {
            var (title, page) = (entry.Text.Split('\t')[0], int.Parse(entry.Text.Split('\t')[1], CultureInfo.InvariantCulture));
            Assert.InRange(page, 2, pdf.PageCount);
            Assert.Contains(title, pdf.GetPage(page).Text, StringComparison.Ordinal);
            if (page > 2)
            {
                // The page before is not the table of contents, so the heading must not already stand there.
                Assert.DoesNotContain(title, pdf.GetPage(page - 1).Text, StringComparison.Ordinal);
            }
        }

        var pages = entries.Select(e => int.Parse(e.Text.Split('\t')[1], CultureInfo.InvariantCulture)).ToList();
        Assert.Equal((2, pdf.PageCount), (pages[0], pages[^1]));
        Assert.Equal(pages.Order(), pages);
    }

    [Fact]
    public void Updating_again_changes_nothing_and_reuses_the_bookmarks()
    {
        var editor = WordEditor.Open(Report());
        WordFieldUpdater.Update(editor);
        var first = editor.ToArray();

        var again = WordEditor.Open(first);
        WordFieldUpdater.Update(again);

        Assert.Equal(DocxFactory.Part(first, "word/document.xml"), DocxFactory.Part(again.ToArray(), "word/document.xml"));
        Assert.Equal(5, DocxFactory.Part(first, "word/document.xml").Split("w:name=\"_Toc").Length - 1);
    }

    [Fact]
    public void Page_references_bookmark_text_page_counts_and_dates_are_computed()
    {
        var body = """
            <w:p><w:r><w:t xml:space="preserve">See page </w:t></w:r><w:r><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:instrText xml:space="preserve"> PAGEREF target \h </w:instrText></w:r><w:r><w:fldChar w:fldCharType="separate"/></w:r><w:r><w:rPr><w:b/></w:rPr><w:t>99</w:t></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r><w:r><w:t xml:space="preserve"> for </w:t></w:r><w:fldSimple w:instr=" REF target "><w:r><w:t>old</w:t></w:r></w:fldSimple></w:p>
            <w:p><w:r><w:br w:type="page"/></w:r></w:p>
            <w:p><w:bookmarkStart w:id="0" w:name="target"/><w:r><w:t xml:space="preserve">Target </w:t></w:r><w:r><w:t>text</w:t></w:r><w:bookmarkEnd w:id="0"/></w:p>
            <w:p><w:fldSimple w:instr="NUMPAGES \* roman"/><w:r><w:t xml:space="preserve"> </w:t></w:r><w:r><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:instrText>PAGE</w:instrText></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r></w:p>
            <w:p><w:fldSimple w:instr="DATE \@ &quot;dddd d MMMM yyyy 'à' HH:mm&quot;"/><w:fldSimple w:instr="TIME \@ &quot;h:mm am/pm&quot;"/><w:fldSimple w:instr="TIME"/></w:p>
            <w:p><w:fldSimple w:instr="DATE \@ &quot;ddd dd MMM yy, d/M/yyyy hh:mm:ss s, H AM/PM&quot;"/><w:fldSimple w:instr="SAVEDATE"/><w:fldSimple w:instr="SECTIONPAGES \* ALPHABETIC"/><w:fldSimple w:instr="NUMPAGES \* alphabetic"/><w:fldSimple w:instr="NUMPAGES \* Roman"/></w:p>
            <w:p><w:fldSimple w:instr="MERGEFIELD Name"><w:r><w:t>«Name»</w:t></w:r></w:fldSimple><w:fldSimple w:instr="PAGEREF missing"><w:r><w:t>?</w:t></w:r></w:fldSimple></w:p>
            <w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="708" w:footer="708" w:gutter="0"/><w:pgNumType w:fmt="upperRoman" w:start="3"/></w:sectPr>
            """;
        var editor = WordEditor.Open(DocxFactory.Build(body));

        var update = WordFieldUpdater.Update(editor, new WordFieldOptions { Now = Now, Culture = CultureInfo.GetCultureInfo("fr-FR") });

        var bytes = editor.ToArray();
        AssertValid(bytes);
        var paragraphs = WordDocument.Load(bytes).Blocks.OfType<WordParagraph>().Select(p => p.Text).ToList();
        Assert.Equal("See page IV for Target text", paragraphs[0]);
        Assert.Equal("ii IV", paragraphs[3]);
        Assert.Equal("mercredi 4 mars 2026 à 15:063:06 pm15:06", paragraphs[4]);
        Assert.Equal("mer. 04 mars 26, 4/3/2026 03:06:07 7, 15 PMBbII", paragraphs[5]);
        Assert.Equal("«Name»?", paragraphs[6]);
        Assert.Contains("<w:b />", DocxFactory.Part(bytes, "word/document.xml"), StringComparison.Ordinal);
        Assert.Equal((11, 2), (update.Fields, update.PageCount));
        Assert.Equal(["MERGEFIELD fields not updated", "PAGEREF to a missing bookmark not updated", "SAVEDATE without the date in the core properties not updated"], update.Gaps);
    }

    [Fact]
    public void Unsupported_contents_switches_and_header_page_fields_are_left_as_they_are()
    {
        var header = $"""
            <?xml version="1.0" encoding="UTF-8"?><w:hdr xmlns:w="{DocxFactory.W}"><w:p><w:fldSimple w:instr="PAGE"><w:r><w:t>1</w:t></w:r></w:fldSimple><w:fldSimple w:instr="CREATEDATE"><w:r><w:t>x</w:t></w:r></w:fldSimple></w:p></w:hdr>
            """;
        var body = """
            <w:p><w:r><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:instrText xml:space="preserve"> TOC \c "Figure" </w:instrText></w:r><w:r><w:fldChar w:fldCharType="separate"/></w:r><w:r><w:t>figures</w:t></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r></w:p>
            <w:p><w:r><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:instrText xml:space="preserve"> TOC \t "Custom,1" </w:instrText></w:r><w:r><w:fldChar w:fldCharType="separate"/></w:r><w:r><w:t>old</w:t></w:r></w:p>
            <w:p><w:r><w:t>still old</w:t></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r><w:r><w:t>after</w:t></w:r></w:p>
            <w:sectPr><w:headerReference w:type="default" r:id="rIdH"/></w:sectPr>
            """;
        var editor = WordEditor.Open(DocxFactory.Build(body, extraParts: new Dictionary<string, string> { ["word/header1.xml"] = header },
            documentRelationships: """<Relationship Id="rIdH" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/header" Target="header1.xml"/>"""));

        var update = WordFieldUpdater.Update(editor);

        var bytes = editor.ToArray();
        AssertValid(bytes);
        Assert.Equal(["CREATEDATE without the date in the core properties not updated", "table of contents with the \\c switch left as it was"], update.Gaps);
        var texts = WordDocument.Load(bytes).Blocks.OfType<WordParagraph>().Select(p => p.Text).ToList();
        Assert.Equal(["figures", "No table of contents entries found.", "after"], texts);
        Assert.Contains("<w:t>1</w:t>", DocxFactory.Part(bytes, "word/header1.xml"), StringComparison.Ordinal);
    }

    [Fact]
    public void Style_lists_and_page_number_omission_follow_their_switches()
    {
        var document = new WordDocument();
        document.Styles.Add(new WordStyle("Custom", WordStyleType.Paragraph) { Name = "My Custom" });
        document.AddParagraph().Add(new WordField("TOC \\t \"My Custom,2\" \\n \"2-2\"", string.Empty));
        document.AddParagraph("Picked", "Custom");
        document.AddHeading("Not listed", 1);

        var editor = WordEditor.Open(document.ToArray());
        var update = WordFieldUpdater.Update(editor);

        var bytes = editor.ToArray();
        AssertValid(bytes);
        var entry = Assert.Single(WordDocument.Load(bytes).Blocks.OfType<WordParagraph>(), p => p.StyleId == "TOC2");
        Assert.Equal("Picked", entry.Text);
        Assert.Equal(1, update.TableOfContentsEntries);
    }
}
