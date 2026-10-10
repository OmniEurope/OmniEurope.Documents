// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// Word to Markdown. Expected texts follow CommonMark and its GitHub extensions (pipe tables, strikethrough,
/// footnotes); the round trip reads the Markdown back with the package's own Markdown to Word conversion and
/// compares text and structure (heading levels, list depths, table cells, emphasis).
/// </summary>
public sealed class WordToMarkdownTests
{
    private static readonly WordRunProperties Bold = new() { Bold = true };
    private static readonly WordRunProperties Italic = new() { Italic = true };

    [Fact]
    public void Writes_headings_paragraphs_emphasis_and_hard_breaks()
    {
        var result = WordToMarkdown.Convert(Formatted());

        Assert.Equal(
            "# Titre\n\n## Sous-titre\n\nTexte **gras** et *italique*, ***les deux*** puis ~~barré~~ fin.\\\nLigne suivante\n",
            result.Markdown);
        Assert.Empty(result.Images);
    }

    [Fact]
    public void Gives_the_same_markdown_for_the_saved_package()
    {
        Assert.Equal(WordToMarkdown.Convert(Formatted()).Markdown, WordToMarkdown.Convert(Formatted().ToArray()).Markdown);
    }

    [Fact]
    public void Writes_nested_bulleted_and_numbered_lists()
    {
        var document = new WordDocument();
        var bullets = document.Numbering.AddBulletList();
        document.AddParagraph("un").AsListItem(bullets);
        document.AddParagraph("deux").AsListItem(bullets);
        document.AddParagraph("deux a").AsListItem(bullets, 1);
        document.AddParagraph().AsListItem(bullets, 1);
        document.AddParagraph("trois").AsListItem(bullets);
        document.AddParagraph("Entre les listes");
        var numbers = document.Numbering.AddNumberedList();
        document.AddParagraph("premier").AsListItem(numbers);
        document.AddParagraph("second").AsListItem(numbers);
        document.AddParagraph("second a").AsListItem(numbers, 1);
        document.AddParagraph("second b").AsListItem(numbers, 1);
        document.AddParagraph("troisième").AsListItem(numbers);
        document.AddParagraph("profond").AsListItem(numbers, 3);
        document.AddParagraph("ligne").AsListItem(numbers).Add(new WordBreak(WordBreakKind.Line)).AddText("suite");

        var result = WordToMarkdown.Convert(document);

        Assert.Equal(
            "- un\n- deux\n  - deux a\n- trois\n\nEntre les listes\n\n1. premier\n2. second\n   1. second a\n   2. second b\n3. troisième\n   1. profond\n4. ligne\\\n   suite\n",
            result.Markdown);
        Assert.Contains("list numbers in letters, roman numerals or words are written as numbers", result.Gaps);
        Assert.Contains("empty paragraphs are left out", result.Gaps);
    }

    [Fact]
    public void Keeps_the_label_of_a_numbered_heading_and_writes_a_heading_on_one_line()
    {
        var document = new WordDocument();
        var numbers = document.Numbering.AddNumberedList();
        document.AddHeading("Introduction").AsListItem(numbers);
        document.AddHeading("Suite").AsListItem(numbers).Add(new WordBreak(WordBreakKind.Line)).AddText("du titre");

        Assert.Equal("# 1\\. Introduction\n\n# 2\\. Suite du titre\n", WordToMarkdown.Convert(document).Markdown);
    }

    [Fact]
    public void Writes_tables_as_pipe_tables_with_merged_cells_spread()
    {
        var document = new WordDocument();
        var table = new WordTable();
        table.AddRow("Nom", "Valeur", "Note");
        table.AddRow("a | b", "1", "*x*");
        var merged = new WordTableRow();
        merged.Cells.Add(new WordTableCell("fusion") { Properties = new WordTableCellProperties { GridSpan = 2, VerticalMerge = WordVerticalMerge.Restart } });
        merged.Cells.Add(new WordTableCell("z"));
        table.Rows.Add(merged);
        var below = new WordTableRow();
        below.Cells.Add(new WordTableCell("caché") { Properties = new WordTableCellProperties { GridSpan = 2, VerticalMerge = WordVerticalMerge.Continue } });
        var two = new WordTableCell("un");
        two.Blocks.Add(new WordParagraph("deux"));
        below.Cells.Add(two);
        table.Rows.Add(below);
        var nested = new WordTableCell();
        var inner = new WordTable();
        inner.AddRow("x", "y");
        nested.Blocks.Add(inner);
        table.AddRow("court").Cells.Add(nested);
        document.AddTable(table);
        document.AddTable(new WordTable());

        var result = WordToMarkdown.Convert(document);

        Assert.Equal(
            "| Nom | Valeur | Note |\n| --- | --- | --- |\n| a \\| b | 1 | \\*x\\* |\n| fusion |  | z |\n|  |  | un deux |\n| court | x y |  |\n",
            result.Markdown);
        Assert.Contains("merged table cells keep their text in the first cell, the cells they cover are empty", result.Gaps);
        Assert.Contains("several paragraphs in a table cell or a note are joined on one line", result.Gaps);
        Assert.Contains("tables inside table cells are written as their text", result.Gaps);
    }

    [Fact]
    public void Keeps_safe_links_only()
    {
        var document = new WordDocument();
        var paragraph = document.AddParagraph("Voir ");
        paragraph.Add(new WordHyperlink("https://example.org/é", "le site"));
        paragraph.AddText(", ");
        paragraph.Add(new WordHyperlink("mailto:x@example.org", "écrire"));
        paragraph.AddText(", ");
        paragraph.Add(new WordHyperlink("javascript:alert(1)", "piège"));
        paragraph.AddText(", ");
        paragraph.Add(new WordHyperlink(null, "plus haut", "signet"));
        paragraph.AddText(" et ");
        paragraph.Add(new WordHyperlink("https://example.org/f(x)", "**fonction** "));
        paragraph.AddText("puis ");
        paragraph.Add(new WordHyperlink("https://example.org/", string.Empty));

        var result = WordToMarkdown.Convert(document);

        Assert.Equal(
            "Voir [le site](https://example.org/%C3%A9), [écrire](mailto:x@example.org), piège, plus haut et [\\*\\*fonction\\*\\*](<https://example.org/f(x)>) puis [https://example.org/](https://example.org/)\n",
            result.Markdown);
        Assert.Contains("links with an address other than http, https or mailto keep their text only", result.Gaps);
        Assert.Contains("links to bookmarks keep their text only", result.Gaps);
        Assert.DoesNotContain("underline, superscript and subscript are written as plain text", result.Gaps);
    }

    [Fact]
    public void Writes_pictures_as_reference_images_and_returns_their_files()
    {
        var document = new WordDocument();
        var png = new WordImage([1, 2, 3], "image/png");
        var gif = new WordImage([4, 5], "image/gif");
        document.AddParagraph("Logo : ").Add(new WordPicture(png, 40, 40) { Description = "Logo [officiel]" });
        var second = document.AddParagraph();
        second.Add(new WordPicture(png, 20, 20));
        second.AddText(" ");
        second.Add(new WordPicture(gif, 20, 20) { Description = "anim", Floating = new WordFloatingPosition() });
        document.AddParagraph().Add(new WordPicture(new WordImage([6], "application/octet-stream"), 5, 5));

        var result = WordToMarkdown.Convert(document, new WordMarkdownOptions { ImageFolder = "media/" });

        Assert.Equal(
            "Logo : ![Logo \\[officiel\\]][image1]\n\n![][image1] ![anim][image2]\n\n![][image3]\n\n[image1]: media/image1.png\n[image2]: media/image2.gif\n[image3]: media/image3.bin\n",
            result.Markdown);
        Assert.Equal(["media/image1.png", "media/image2.gif", "media/image3.bin"], result.Images.Select(i => i.Path));
        Assert.Same(png.Data, result.Images[0].Data);
        Assert.Equal("image/gif", result.Images[1].ContentType);
        Assert.Contains("floating pictures are written in the text flow", result.Gaps);
        Assert.Equal("![][image1]\n\n[image1]: image1.png\n", WordToMarkdown.Convert(OnePicture(), new WordMarkdownOptions { ImageFolder = string.Empty }).Markdown);
    }

    [Fact]
    public void Writes_footnotes_and_endnotes_as_footnotes_defined_at_the_end()
    {
        var document = new WordDocument();
        var footnote = document.AddFootnote("Première note.");
        var paragraph = document.AddParagraph("Texte");
        paragraph.Add(footnote);
        paragraph.AddText(" suite");
        paragraph.Add(document.AddEndnote("Note de fin."));
        paragraph.AddText(" et encore");
        paragraph.Add(new WordNoteReference(WordNoteKind.Footnote, footnote.Id));
        paragraph.Add(new WordNoteReference(WordNoteKind.Footnote, 99));
        var inner = document.AddFootnote("Note dans la note.");
        document.Footnotes[footnote.Id].Blocks.Add(new WordParagraph("Deuxième paragraphe").Add(inner));

        var result = WordToMarkdown.Convert(document);

        Assert.Equal(
            "Texte[^1] suite[^e1] et encore[^1]\n\n[^1]: Première note. Deuxième paragraphe[^2]\n\n[^e1]: Note de fin.\n\n[^2]: Note dans la note.\n",
            result.Markdown);
        Assert.Contains("endnotes are written as footnotes", result.Gaps);
        Assert.Contains("references to notes the document does not hold are left out", result.Gaps);
    }

    [Fact]
    public void Escapes_text_so_that_it_reads_back_as_the_same_literal_text()
    {
        string[] texts =
        [
            "# pas un titre", "1. pas une liste", "- pas une puce", "+ plus", "*pas en italique* _ni_ `code`",
            "<b>html</b> & [lien](x) ~barré~ | tube", "a\\b", "=",
        ];
        var document = new WordDocument();
        foreach (var text in texts)
        {
            document.AddParagraph(text);
        }

        document.AddParagraph("Avant").Add(new WordBreak(WordBreakKind.Line)).AddText("# encore");
        var markdown = WordToMarkdown.Convert(document).Markdown;
        var back = MarkdownToWord.Convert(markdown).Body.OfType<WordParagraph>().Select(p => p.Text).ToList();

        Assert.Equal([.. texts, "Avant\n# encore"], back);
    }

    [Fact]
    public void Reads_back_with_the_same_text_and_structure()
    {
        var document = Formatted();
        var bullets = document.Numbering.AddBulletList();
        document.AddParagraph("puce").AsListItem(bullets);
        document.AddParagraph("sous-puce").AsListItem(bullets, 1);
        var numbers = document.Numbering.AddNumberedList();
        document.AddParagraph("étape").AsListItem(numbers);
        document.AddParagraph("autre étape").AsListItem(numbers);
        document.AddHeading("Tableau", 3);
        var table = new WordTable();
        table.AddRow("Colonne", "Montant");
        table.AddRow("Total", "42");
        document.AddTable(table);

        var markdown = WordToMarkdown.Convert(document).Markdown;
        var back = MarkdownToWord.Convert(markdown);

        Assert.Equal(Structure(document), Structure(back));
        var emphasis = back.Body.OfType<WordParagraph>().ElementAt(2).Inlines.OfType<WordText>().Where(t => t.Properties.Bold == true || t.Properties.Italic == true || t.Properties.Strike == true);
        Assert.Equal(["gras", "italique", "les deux", "barré"], emphasis.Select(t => t.Value));
    }

    [Fact]
    public void Reports_what_markdown_cannot_carry_and_leaves_out_hidden_and_deleted_text()
    {
        var document = new WordDocument();
        document.Sections[0].Headers[WordHeaderFooterKind.Default] = new WordHeaderFooter(new WordParagraph("En-tête"));
        document.Comments.Add(new WordComment(1));
        var paragraph = document.AddParagraph("a");
        paragraph.AddText("souligné", new WordRunProperties { Underline = WordUnderline.Single });
        paragraph.AddText("2", new WordRunProperties { VerticalPosition = WordVerticalPosition.Superscript });
        paragraph.AddText("caché", new WordRunProperties { Hidden = true });
        paragraph.Add(new WordText("supprimé") { Revision = new WordRevision(WordRevisionKind.Deleted) });
        paragraph.Add(new WordText("ajouté") { Revision = new WordRevision(WordRevisionKind.Inserted) });
        paragraph.Add(new WordTab());
        paragraph.AddText("b");
        paragraph.Add(new WordBreak(WordBreakKind.Page));
        paragraph.Add(new WordBreak(WordBreakKind.Column));
        paragraph.AddText("c");
        paragraph.Add(new WordField("PAGE", "7"));
        paragraph.Add(new WordSymbol("Wingdings", ''));
        paragraph.Add(new WordCommentReference(1));
        var box = new WordTextBox(100, 50);
        box.Blocks.Add(new WordParagraph("dans la boîte"));
        paragraph.Add(box);
        var cell = new WordTableCell();
        var boxed = new WordParagraph("cellule").AsListItem(document.Numbering.AddBulletList());
        boxed.Add(new WordTextBox(10, 10));
        cell.Blocks.Add(boxed);
        var table = new WordTable();
        table.Rows.Add(new WordTableRow { Cells = { cell } });
        table.Rows.Add(new WordTableRow { Revision = new WordRevision(WordRevisionKind.Deleted), Cells = { new WordTableCell("retirée") } });
        document.AddTable(table);

        var result = WordToMarkdown.Convert(document);

        Assert.StartsWith("asouligné2ajouté\tb\\\nc7", result.Markdown, StringComparison.Ordinal);
        Assert.EndsWith("\n\ndans la boîte\n\n| cellule |\n| --- |\n", result.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("caché", result.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("supprimé", result.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("retirée", result.Markdown, StringComparison.Ordinal);
        string[] expected =
        [
            "column breaks are written as line breaks", "comments are left out", "headers and footers are left out",
            "list items inside table cells and notes are written as plain paragraphs", "page breaks are left out",
            "tabs are written as tab characters", "text boxes are written as paragraphs after the paragraph that anchors them",
            "text boxes inside table cells and notes are left out", "underline, superscript and subscript are written as plain text",
        ];
        Assert.All(expected, gap => Assert.Contains(gap, result.Gaps));
    }

    [Fact]
    public void Writes_nothing_for_an_empty_document()
    {
        var result = WordToMarkdown.Convert(new WordDocument());

        Assert.Equal(string.Empty, result.Markdown);
        Assert.Empty(result.Gaps);
    }

    private static WordDocument Formatted()
    {
        var document = new WordDocument();
        document.AddHeading("Titre", 1);
        document.AddHeading("Sous-titre", 2);
        var paragraph = document.AddParagraph("Texte ");
        paragraph.AddText("gras", Bold);
        paragraph.AddText(" et ");
        paragraph.AddText("italique", Italic);
        paragraph.AddText(", ");
        paragraph.AddText("les deux", new WordRunProperties { Bold = true, Italic = true });
        paragraph.AddText(" puis ");
        paragraph.AddText("barré ", new WordRunProperties { Strike = true });
        paragraph.AddText("fin.");
        paragraph.Add(new WordBreak(WordBreakKind.Line));
        paragraph.AddText("Ligne suivante");
        paragraph.Add(new WordBreak(WordBreakKind.Line));
        return document;
    }

    private static WordDocument OnePicture()
    {
        var document = new WordDocument();
        document.AddParagraph().Add(new WordPicture(new WordImage([1], "image/png"), 5, 5));
        return document;
    }

    // Each block as its kind and text: a heading with its level, a list item with its depth, a table's cells.
    private static List<string> Structure(WordDocument document)
    {
        var lines = new List<string>();
        foreach (var block in document.Body)
        {
            if (block is WordTable table)
            {
                lines.Add("table " + string.Join(" / ", table.Rows.Select(r => string.Join(" | ", r.Cells.Select(c => c.Text)))));
                continue;
            }

            var paragraph = (WordParagraph)block;
            var resolved = document.Styles.ResolveParagraph(paragraph.Properties, document.Numbering);
            var kind = resolved.OutlineLevel is { } level and < 6 ? "h" + (level + 1)
                : resolved.NumberingId is > 0 ? "li" + (resolved.NumberingLevel ?? 0)
                : "p";
            lines.Add(kind + " " + paragraph.Text.TrimEnd('\n'));
        }

        return lines;
    }
}
