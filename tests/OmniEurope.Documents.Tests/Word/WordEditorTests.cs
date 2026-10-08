// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Editing;

namespace OmniEurope.Documents.Tests.Word;

public sealed class WordEditorTests
{
    private static readonly byte[] External = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Word", "external.docx"));

    [Fact]
    public void Saving_without_changes_keeps_every_part_identical()
    {
        foreach (var original in new[] { External, SampleDocuments.Report().ToArray() })
        {
            var editor = WordEditor.Open(original);
            _ = editor.Paragraphs().Select(p => p.Text).ToList();
            _ = editor.ToDocument();

            var saved = editor.ToArray();

            Assert.Empty(editor.ChangedParts);
            var before = DocxFactory.Entries(original);
            var after = DocxFactory.Entries(saved);
            Assert.Equal(before.Keys, after.Keys);
            Assert.All(before, entry => Assert.Equal(entry.Value, after[entry.Key]));
        }
    }

    [Fact]
    public void Replaces_text_split_across_runs_and_rewrites_only_that_part()
    {
        var body = """
            <w:p><w:r><w:rPr><w:b/></w:rPr><w:t>Bonjour {{pr</w:t></w:r><w:proofErr w:type="spellStart"/><w:r><w:rPr><w:i/></w:rPr><w:t>enom</w:t></w:r><w:r><w:t xml:space="preserve">}}, </w:t></w:r><w:r><w:t>{{prenom}}!</w:t></w:r></w:p>
            <w:p><w:r><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:instrText>MERGEFIELD {{prenom}}</w:instrText></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r></w:p>
            """;
        var editor = WordEditor.Open(DocxFactory.Build(body));

        var count = editor.ReplaceText(new Dictionary<string, string> { ["{{prenom}}"] = "Alice" });

        Assert.Equal(2, count);
        Assert.Equal(["word/document.xml"], editor.ChangedParts);
        var paragraph = WordDocument.Load(editor.ToArray()).Blocks.OfType<WordParagraph>().First();
        Assert.Equal("Bonjour Alice, Alice!", paragraph.Text);
        Assert.True(paragraph.Inlines.OfType<WordText>().Single(t => t.Value.Contains("Alice", StringComparison.Ordinal) && t.Value.StartsWith("Bonjour", StringComparison.Ordinal)).Properties.Bold);
        Assert.Contains("MERGEFIELD {{prenom}}", DocxFactory.Part(editor.ToArray(), "word/document.xml"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<w:r><w:t>a</w:t><w:tab/><w:t>b</w:t></w:r>")]
    [InlineData("<w:r><w:t>a</w:t><w:br/><w:t>b</w:t></w:r>")]
    [InlineData("<w:r><w:t>a</w:t></w:r><w:r><w:footnoteReference w:id=\"1\"/></w:r><w:r><w:t>b</w:t></w:r>")]
    [InlineData("<w:r><w:t>a</w:t></w:r><w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText>PAGE</w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r><w:r><w:t>b</w:t></w:r>")]
    public void A_search_does_not_span_what_separates_the_text(string runs)
    {
        // The visible text holds a tab, a break, a note or a field between a and b: "ab" is not in it.
        var editor = WordEditor.Open(DocxFactory.Build($"<w:p>{runs}</w:p><w:p><w:r><w:t>a</w:t></w:r><w:r><w:rPr><w:b/></w:rPr><w:t>b</w:t></w:r></w:p>"));

        Assert.Equal(1, editor.ReplaceText("ab", "X"));

        var paragraphs = editor.ToDocument().Blocks.OfType<WordParagraph>().ToList();
        Assert.DoesNotContain("X", paragraphs[0].Text, StringComparison.Ordinal);
        Assert.Equal("X", paragraphs[1].Text);
    }

    [Fact]
    public void Replacement_reaches_headers_footers_and_notes()
    {
        var document = new WordDocument();
        document.Sections[0].Headers[WordHeaderFooterKind.Default] = new WordHeaderFooter(new WordParagraph("En-tête [CLIENT]"));
        document.AddParagraph("Corps [CLIENT]").Add(document.AddFootnote("Note [CLIENT]"));
        var editor = WordEditor.Open(document.ToArray());

        Assert.Equal(3, editor.ReplaceText("[client]", "ACME", StringComparison.OrdinalIgnoreCase));

        var reread = editor.ToDocument();
        Assert.Equal("En-tête ACME", reread.Sections[0].Headers[WordHeaderFooterKind.Default].Text);
        Assert.Equal("Corps ACME", reread.Blocks.First().Text);
        Assert.Equal("Note ACME", reread.Footnotes.Values.Single().Text.Trim());
    }

    [Fact]
    public void Setting_text_keeps_fields_and_notes_and_sets_the_language()
    {
        var document = new WordDocument();
        var source = document.AddParagraph("Texte à traduire ", "Heading2");
        source.Inlines[0].Properties = new WordRunProperties { Italic = true, Color = "112233" };
        source.Add(document.AddFootnote("Note")).Add(WordField.Page()).AddText(" suite");
        var editor = WordEditor.Open(document.ToArray());
        var paragraph = editor.Paragraphs().First(p => p.PartName == "word/document.xml");
        Assert.Equal("Texte à traduire  suite", paragraph.EditableText);

        paragraph.SetText([new WordTextPiece("Text to "), new WordTextPiece("translate", new WordRunProperties { Bold = true })], "en-GB");

        var reread = editor.ToDocument().Blocks.OfType<WordParagraph>().First();
        Assert.Equal("Text to translate1", reread.Text);
        Assert.Equal("Heading2", reread.StyleId);
        var texts = reread.Inlines.OfType<WordText>().ToList();
        Assert.All(texts, t => Assert.Equal("en-GB", t.Properties.Language));
        Assert.All(texts, t => Assert.Equal("112233", t.Properties.Color));
        Assert.True(texts[1].Properties.Bold);
        Assert.True(texts[0].Properties.Italic);
        Assert.Single(reread.Inlines.OfType<WordNoteReference>());
        Assert.Single(reread.Inlines.OfType<WordField>());
    }

    [Fact]
    public void Unchanged_text_leaves_the_package_untouched()
    {
        var editor = WordEditor.Open(External);
        var paragraph = editor.FindParagraph("word/document.xml#0")!;

        paragraph.SetText("Rapport de test");

        Assert.Equal("Rapport de test", paragraph.Text);
        Assert.Empty(editor.ChangedParts);
    }

    [Fact]
    public void Core_properties_are_updated_in_place()
    {
        var editor = WordEditor.Open(External);

        editor.Information = editor.Information with { Title = "Nouveau titre", Subject = "Sujet" };

        var core = DocxFactory.Part(editor.ToArray(), "docProps/core.xml");
        Assert.Contains("<dc:title>Nouveau titre</dc:title>", core, StringComparison.Ordinal);
        Assert.Contains("<cp:revision>1</cp:revision>", core, StringComparison.Ordinal);
        Assert.Equal(["docProps/core.xml"], editor.ChangedParts);
        Assert.Equal("Sujet", WordDocument.Load(editor.ToArray()).Information.Subject);
    }

    [Fact]
    public void Merging_carries_pictures_notes_lists_styles_and_sections()
    {
        var first = SampleDocuments.Report().ToArray();
        var second = new WordDocument();
        second.Styles.Add(new WordStyle("Citation", WordStyleType.Paragraph) { Name = "Citation", RunProperties = new WordRunProperties { Italic = true } });
        var list = second.Numbering.AddNumberedList(WordNumberFormat.UpperRoman);
        second.AddParagraph("Annexe", "Citation").Add(second.AddFootnote("Note de l'annexe"));
        second.AddParagraph("Point").AsListItem(list);
        second.AddParagraph().Add(new WordPicture(WordImage.FromBytes(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "gray.png"))), 30, 30));

        var merged = WordDocument.Load(WordEditor.Merge(first, second.ToArray(), External));

        Assert.Equal(4, merged.Sections.Count);
        Assert.Contains("Introduction", merged.Text);
        Assert.Contains("Annexe", merged.Text);
        Assert.Contains("Rapport de test", merged.Text);
        Assert.Equal(["Source de la note.", "Note de l'annexe"], merged.Footnotes.Values.OrderBy(n => n.Id).Select(n => n.Text.Trim()));
        var annex = merged.Blocks.OfType<WordParagraph>().First(p => p.Text == "Annexe");
        Assert.Equal("Note de l'annexe", merged.Footnotes[annex.Inlines.OfType<WordNoteReference>().Single().Id].Text.Trim());
        Assert.True(merged.Styles.ResolveRun(merged.Styles.ResolveParagraph(annex.Properties), WordRunProperties.Empty).Italic);
        var point = merged.Blocks.OfType<WordParagraph>().First(p => p.Text == "Point");
        Assert.Equal("I.", new WordListCounter(merged.Numbering).Next(point.Properties.NumberingId!.Value, 0)!.Value.Label);
        Assert.Equal(3, merged.Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordPicture>().Select(p => p.Image.PartName).Distinct().Count());
        Assert.Empty(merged.Gaps);
    }

    [Fact]
    public void Merging_into_a_document_without_notes_or_lists_creates_those_parts()
    {
        var plain = new WordDocument();
        plain.AddParagraph("Couverture");
        var annex = new WordDocument();
        annex.AddParagraph("Annexe").Add(annex.AddEndnote("Fin")).Add(annex.AddFootnote("Bas"));
        annex.AddParagraph("Point").AsListItem(annex.Numbering.AddBulletList());
        var target = WordEditor.Open(plain.ToArray());

        target.Append(WordEditor.Open(annex.ToArray()), startOnNewPage: false);

        var merged = target.ToDocument();
        Assert.Equal(["Couverture", "Annexe", "Point"], merged.Blocks.Select(b => b.Text));
        Assert.Equal(WordSectionStart.Continuous, merged.Sections[1].Page.Start);
        Assert.Equal("Bas", merged.Footnotes.Values.Single().Text.Trim());
        Assert.Equal("Fin", merged.Endnotes.Values.Single().Text.Trim());
        var point = merged.Blocks.OfType<WordParagraph>().Last();
        Assert.Equal("•", new WordListCounter(merged.Numbering).Next(point.Properties.NumberingId!.Value, 0)!.Value.Label);
        Assert.Contains("word/numbering.xml", target.ChangedParts);
    }
}
