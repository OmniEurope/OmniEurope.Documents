// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Word;

public sealed class WordDocumentTests
{
    [Fact]
    public void Written_document_reads_back_with_structure_and_formatting()
    {
        var document = SampleDocuments.Report();

        var reread = WordDocument.Load(document.ToArray());

        Assert.Empty(reread.Gaps);
        Assert.Equal(2, reread.Sections.Count);
        Assert.True(reread.Sections[1].Page.Landscape);
        Assert.Equal(841.9, reread.Sections[1].Page.Width, 1);
        Assert.Equal("Rapport", reread.Information.Title);
        Assert.Equal("fr-FR", reread.Styles.DefaultRunProperties.Language);

        var blocks = reread.Blocks.ToList();
        var heading = Assert.IsType<WordParagraph>(blocks[0]);
        Assert.Equal("Heading1", heading.StyleId);
        Assert.Equal("Introduction", heading.Text);
        var resolved = reread.Styles.ResolveRun(reread.Styles.ResolveParagraph(heading.Properties), heading.Inlines[0].Properties);
        Assert.True(resolved.Bold);
        Assert.Equal(16, resolved.FontSize);
        Assert.Equal("Calibri", resolved.Font);

        var body = Assert.IsType<WordParagraph>(blocks[1]);
        Assert.Equal("Texte en gras\tet italique, lien.", body.Text);
        var bold = body.Inlines.OfType<WordText>().First(t => t.Value == "gras");
        Assert.True(bold.Properties.Bold);
        Assert.Equal("FF0000", bold.Properties.Color);
        Assert.Equal("en-GB", bold.Properties.Language);
        var link = Assert.Single(body.Inlines.OfType<WordHyperlink>());
        Assert.Equal("https://example.org/", link.Target);
        var note = Assert.Single(body.Inlines.OfType<WordNoteReference>());
        Assert.Equal("Source de la note.", reread.Footnotes[note.Id].Text.Trim());
        Assert.Equal(WordAlignment.Justify, body.Properties.Alignment);
        Assert.Equal(-18, body.Properties.FirstLineIndent);
        Assert.Equal(1.5, body.Properties.LineSpacing);
    }

    [Fact]
    public void Tables_keep_spans_merges_and_header_rows()
    {
        var table = Assert.IsType<WordTable>(WordDocument.Load(SampleDocuments.Report().ToArray()).Blocks.ElementAt(3));

        Assert.Equal([100, 150, 200], table.Columns.Select(c => Math.Round(c, 2)));
        Assert.True(table.Rows[0].Properties.IsHeader);
        Assert.Equal(2, table.Rows[1].Cells[0].Properties.GridSpan);
        Assert.Equal(WordVerticalMerge.Restart, table.Rows[1].Cells[1].Properties.VerticalMerge);
        Assert.Equal(WordVerticalMerge.Continue, table.Rows[2].Cells[2].Properties.VerticalMerge);
        Assert.Equal("D9E2F3", table.Rows[0].Cells[0].Properties.Shading);
        Assert.Equal("TableGrid", table.Properties.StyleId);
        Assert.Equal("A\tB\tC\nFusion\tHaut\n1\t2\t", table.Text);
        Assert.Equal(0.5, Assert.IsType<WordTableBorders>(WordDocument.Load(SampleDocuments.Report().ToArray()).Styles.ResolveTable(table.Properties).Borders).Top!.Width);
    }

    [Fact]
    public void Lists_number_through_the_counter()
    {
        var reread = WordDocument.Load(SampleDocuments.Report().ToArray());
        var counter = new WordListCounter(reread.Numbering);

        var labels = reread.Blocks.OfType<WordParagraph>()
            .Where(p => p.Properties.NumberingId is not null)
            .Select(p => counter.Next(p.Properties.NumberingId!.Value, p.Properties.NumberingLevel ?? 0)!.Value.Label)
            .ToList();

        Assert.Equal(["1.", "2.", "a.", "b.", "3.", "•"], labels);
    }

    [Fact]
    public void Headers_footers_fields_and_pictures_survive()
    {
        var reread = WordDocument.Load(SampleDocuments.Report().ToArray());

        var footer = reread.Sections[0].Footers[WordHeaderFooterKind.Default];
        Assert.Same(footer, reread.Sections[1].Footers[WordHeaderFooterKind.Default]);
        var fields = footer.Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordField>().Select(f => f.Kind).ToList();
        Assert.Equal(["PAGE", "NUMPAGES"], fields);
        Assert.Equal("Page 1 / 1", footer.Text);
        Assert.Equal("Rapport confidentiel", reread.Sections[0].Headers[WordHeaderFooterKind.Default].Text);
        Assert.Equal("Première page", reread.Sections[0].Headers[WordHeaderFooterKind.First].Text);

        var picture = reread.Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordPicture>().Single();
        Assert.Equal(SampleDocuments.Png, picture.Image.Data);
        Assert.Equal("image/png", picture.Image.ContentType);
        Assert.Equal((96, 72), (Math.Round(picture.Width), Math.Round(picture.Height)));
        Assert.Equal("Dégradé", picture.Description);
        var box = reread.Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordTextBox>().Single();
        Assert.Equal("Encadré", box.Blocks.Single().Text);
        Assert.Equal(WordWrap.Square, box.Floating!.Wrap);
    }

    [Fact]
    public void Revisions_are_kept_but_deleted_text_is_not_visible()
    {
        var document = new WordDocument();
        var paragraph = document.AddParagraph("Avant ");
        paragraph.Add(new WordText("ajouté") { Revision = new WordRevision(WordRevisionKind.Inserted, "Relecteur", new DateTimeOffset(2026, 10, 6, 8, 0, 0, TimeSpan.Zero)) });
        paragraph.Add(new WordText("retiré") { Revision = new WordRevision(WordRevisionKind.Deleted, "Relecteur") });

        var reread = WordDocument.Load(document.ToArray()).Blocks.OfType<WordParagraph>().Single();

        Assert.Equal("Avant ajouté", reread.Text);
        Assert.Equal(WordRevisionKind.Deleted, reread.Inlines.OfType<WordText>().Single(t => t.Value == "retiré").Revision!.Kind);
        Assert.Equal("Relecteur", reread.Inlines[1].Revision!.Author);
    }

    [Fact]
    public void Saving_is_deterministic_and_identical_images_share_one_part()
    {
        var document = SampleDocuments.Report();
        var image = WordImage.FromBytes(SampleDocuments.Png);
        document.AddParagraph().Add(new WordPicture(image, 10, 10)).Add(new WordPicture(WordImage.FromBytes(SampleDocuments.Png.ToArray()), 20, 20));

        var first = document.ToArray();

        Assert.Equal(first, document.ToArray());
        Assert.Single(DocxFactory.Entries(first).Keys, k => k.StartsWith("word/media/", StringComparison.Ordinal));
    }

    [Fact]
    public void Complex_fields_nested_fields_and_fields_across_paragraphs_are_rebuilt()
    {
        var body = """
            <w:p><w:r><w:t xml:space="preserve">Page </w:t></w:r><w:r><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:instrText xml:space="preserve"> PAGE </w:instrText></w:r><w:r><w:fldChar w:fldCharType="separate"/></w:r><w:r><w:t>4</w:t></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r></w:p>
            <w:p><w:r><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:instrText xml:space="preserve"> IF </w:instrText></w:r><w:r><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:instrText>MERGEFIELD x</w:instrText></w:r><w:r><w:fldChar w:fldCharType="separate"/></w:r><w:r><w:t>1</w:t></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r><w:r><w:instrText xml:space="preserve"> = 1 "oui" "non"</w:instrText></w:r><w:r><w:fldChar w:fldCharType="separate"/></w:r><w:r><w:t>oui</w:t></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r></w:p>
            <w:p><w:r><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:instrText>TOC \o "1-3"</w:instrText></w:r><w:r><w:fldChar w:fldCharType="separate"/></w:r><w:r><w:t>Entrée 1</w:t></w:r></w:p>
            <w:p><w:r><w:t>Entrée 2</w:t></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r></w:p>
            <w:p><w:fldSimple w:instr=" NUMPAGES "><w:r><w:t>9</w:t></w:r></w:fldSimple><w:r><w:noBreakHyphen/><w:sym w:font="Wingdings" w:char="F0E0"/></w:r></w:p>
            """;

        var paragraphs = WordDocument.Load(DocxFactory.Build(body)).Blocks.OfType<WordParagraph>().ToList();

        Assert.Equal("Page 4", paragraphs[0].Text);
        Assert.Equal("PAGE", paragraphs[0].Inlines.OfType<WordField>().Single().Kind);
        var conditional = paragraphs[1].Inlines.OfType<WordField>().Single();
        Assert.Equal("IF 1 = 1 \"oui\" \"non\"", conditional.Instruction);
        Assert.Equal("oui", paragraphs[1].Text);
        Assert.Equal("TOC", paragraphs[2].Inlines.OfType<WordField>().Single().Kind);
        Assert.Equal("Entrée 1", paragraphs[2].Text);
        Assert.Equal("Entrée 2", paragraphs[3].Text);
        Assert.Equal("9\u2011\uF0E0", paragraphs[4].Text);
    }

    [Fact]
    public void Reads_a_document_produced_by_another_application()
    {
        var document = WordDocument.Load(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Word", "external.docx")));

        var paragraphs = document.Blocks.OfType<WordParagraph>().ToList();
        Assert.Equal("Rapport de test", paragraphs[0].Text);
        Assert.Equal("heading 1", document.Styles.Get(paragraphs[0].StyleId)?.Name, ignoreCase: true);
        Assert.Contains("Premier paragraphe avec du gras, de l'italique et un lien externe.", document.Text);
        Assert.Equal("https://example.org/page", paragraphs[1].Inlines.OfType<WordHyperlink>().Single().Target);
        var resolved = document.Styles.ResolveRun(document.Styles.ResolveParagraph(paragraphs[1].Properties), paragraphs[1].Inlines.OfType<WordText>().Single(t => t.Value == "gras").Properties);
        Assert.True(resolved.Bold);
        var counter = new WordListCounter(document.Numbering);
        var items = paragraphs.Where(p => p.Properties.NumberingId is not null).Select(p => (p.Text, counter.Next(p.Properties.NumberingId!.Value, 0)!.Value.Label)).ToList();
        Assert.Equal(4, items.Count);
        Assert.Equal("Étape B", items[3].Text);
        Assert.Equal("2.", items[3].Label);
        var table = document.Blocks.OfType<WordTable>().Single();
        Assert.Equal("Nom\tValeur\nAlpha\t1\nFusion sur deux colonnes", table.Text);
        Assert.Equal(2, table.Rows[2].Cells[0].Properties.GridSpan);
        var picture = paragraphs.SelectMany(p => p.Inlines).OfType<WordPicture>().Single();
        Assert.Equal(SampleDocuments.Png, picture.Image.Data);
        Assert.Contains("Ωμέγα, Привет, Ţară.", document.Text);
        Assert.Equal("Fixture traitement de texte", document.Information.Title);
    }

    [Theory]
    [InlineData("application/vnd.ms-word.document.macroEnabled.main+xml")]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")]
    public void Refuses_macro_documents_and_other_packages(string contentType)
    {
        Assert.Throws<DocumentFormatException>(() => WordDocument.Load(DocxFactory.Build("<w:p/>", contentType)));
    }

    [Fact]
    public void Refuses_dtds_vba_projects_and_oversized_parts()
    {
        var withDtd = DocxFactory.Build("<w:p><w:r><w:t>&x;</w:t></w:r></w:p>", prolog: "<!DOCTYPE w:document [<!ENTITY x \"boom\">]>");
        var withVba = DocxFactory.Build("<w:p/>", extraParts: new Dictionary<string, string> { ["word/vbaProject.bin"] = "x" });
        var large = DocxFactory.Build("<w:p><w:r><w:t>" + new string('a', 5000) + "</w:t></w:r></w:p>");

        Assert.Throws<DocumentFormatException>(() => WordDocument.Load(withDtd));
        Assert.Throws<DocumentFormatException>(() => WordDocument.Load(withVba));
        Assert.Throws<DocumentFormatException>(() => WordDocument.Load(large, new PackageLimits { MaxPartSize = 1000 }));
        Assert.Throws<DocumentFormatException>(() => WordDocument.Load([1, 2, 3]));
    }

    [Fact]
    public void Unsupported_content_is_reported_as_gaps()
    {
        var body = """<w:p><w:r><w:drawing><wp:inline xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"><wp:extent cx="100" cy="100"/><a:graphic xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/chart"/></a:graphic></wp:inline></w:drawing></w:r></w:p><w:altChunk r:id="rId9"/>""";

        var document = WordDocument.Load(DocxFactory.Build(body));

        Assert.Equal(["chart skipped", "imported content (altChunk) skipped"], document.Gaps);
    }

    [Theory]
    [InlineData(4, WordNumberFormat.LowerRoman, "iv")]
    [InlineData(1994, WordNumberFormat.UpperRoman, "MCMXCIV")]
    [InlineData(28, WordNumberFormat.LowerLetter, "bb")]
    [InlineData(3, WordNumberFormat.DecimalZero, "03")]
    [InlineData(22, WordNumberFormat.Ordinal, "22nd")]
    [InlineData(6, WordNumberFormat.Chicago, "††")]
    public void Formats_numbers(int value, WordNumberFormat format, string expected)
    {
        Assert.Equal(expected, WordNumbering.FormatNumber(value, format));
    }
}
