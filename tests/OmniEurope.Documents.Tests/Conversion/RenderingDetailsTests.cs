// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Conversion.HtmlLayout;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Tests.Word;
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Editing;

namespace OmniEurope.Documents.Tests.Conversion;

public sealed class RenderingDetailsTests
{
    [Fact]
    public void Inline_styles_and_presentational_tags_map_to_run_formatting()
    {
        var html = """
            <p><span style="color: rgb(10, 20, 30); background-color: #abc; font-weight: 700; font-style: italic; text-decoration: underline line-through; font-size: 1.5em; font-family: 'Times New Roman', serif">styled</span>
            <font color="navy" face="Arial, sans-serif">font</font> <small>small</small> <big>big</big> <sub>sub</sub> <mark>mark</mark>
            <span style="color: #zz; font-size: 3vw">ignored</span></p>
            <dl><dt>Terme</dt><dd>Définition</dd></dl>
            <figure><figcaption>Légende</figcaption></figure><center>Centré</center>
            """;

        var paragraphs = HtmlToWord.Convert(html).Blocks.OfType<WordParagraph>().ToList();

        var texts = paragraphs[0].Inlines.OfType<WordText>().Where(t => t.Value.Trim().Length > 0).ToDictionary(t => t.Value.Trim(), t => t.Properties);
        var styled = texts["styled"];
        Assert.Equal(("0A141E", "AABBCC", true, true, WordUnderline.Single, true, 16.5, "Times New Roman"),
            (styled.Color, styled.Shading, styled.Bold, styled.Italic, styled.Underline, styled.Strike, styled.FontSize, styled.Font));
        Assert.Equal(("000080", "Arial"), (texts["font"].Color, texts["font"].Font));
        Assert.True(texts["small"].FontSize < 11 && texts["big"].FontSize > 11);
        Assert.Equal(WordVerticalPosition.Subscript, texts["sub"].VerticalPosition);
        Assert.Equal("yellow", texts["mark"].Highlight);
        Assert.Null(texts["ignored"].Color);
        Assert.Null(texts["ignored"].FontSize);
        Assert.True(paragraphs[1].Inlines.OfType<WordText>().Single().Properties.Bold);
        Assert.Equal(36, paragraphs[2].Properties.IndentLeft);
        Assert.Equal("Caption", paragraphs[3].StyleId);
        Assert.Equal(WordAlignment.Center, paragraphs[4].Properties.Alignment);
    }

    [Theory]
    [InlineData("12pt", 12)]
    [InlineData("16px", 12)]
    [InlineData("2.54cm", 72)]
    [InlineData("25.4mm", 72)]
    [InlineData("1in", 72)]
    [InlineData("150%", 15)]
    [InlineData("2rem", 20)]
    public void Css_lengths_convert_to_points(string value, double expected)
    {
        Assert.Equal(expected, CssStyle.Length(value, 10)!.Value, 6);
    }

    [Theory]
    [InlineData("#f0a", "FF00AA")]
    [InlineData("rgb(1, 2, 300)", "0102FF")]
    [InlineData("Teal", "008080")]
    [InlineData("rgb(1, 2)", null)]
    [InlineData("hsl(0, 0%, 0%)", null)]
    public void Css_colours_convert_to_hex(string value, string? expected)
    {
        Assert.Equal(expected, CssStyle.Color(value));
    }

    [Fact]
    public void Images_take_their_size_from_one_dimension_or_their_resolution()
    {
        var png = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "rgb.png"));
        var source = "data:image/png;base64," + Convert.ToBase64String(png);
        Assert.True(OmniEurope.Documents.Imaging.ImageInfo.TryIdentify(png, out var info));

        var pictures = HtmlToWord.Convert($"""<p><img src="{source}" style="height: 30px"><img src="{source}"><img src="data:image/png;base64,!!"></p>""")
            .Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordPicture>().ToList();

        Assert.Equal(2, pictures.Count);
        Assert.Equal(22.5, pictures[0].Height, 6);
        Assert.Equal(22.5 * info.Width / info.Height, pictures[0].Width, 6);
        Assert.Equal(info.Width * 72 / (info.DpiX > 0 ? info.DpiX : 96), pictures[1].Width, 6);
    }

    [Fact]
    public void Text_decorations_capitals_links_and_highlights_are_drawn()
    {
        var document = new WordDocument();
        var paragraph = document.AddParagraph();
        foreach (var underline in new[] { WordUnderline.Double, WordUnderline.Thick, WordUnderline.Dotted, WordUnderline.Dash, WordUnderline.Wave, WordUnderline.Words })
        {
            paragraph.AddText(underline + " souligné ", new WordRunProperties { Underline = underline });
        }

        paragraph.AddText("double barré ", new WordRunProperties { DoubleStrike = true });
        paragraph.AddText("barré ", new WordRunProperties { Strike = true });
        paragraph.AddText("capitales ", new WordRunProperties { Caps = true });
        paragraph.AddText("Petites Capitales ", new WordRunProperties { SmallCaps = true });
        paragraph.AddText("surligné", new WordRunProperties { Highlight = "green" });
        paragraph.Add(new WordHyperlink("https://example.org/", "lien"));

        var page = PdfDocument.Open(WordToPdf.Convert(document).Pdf).GetPage(1);

        Assert.Contains("CAPITALES", page.Text);
        Assert.Contains("PETITES CAPITALES", page.Text);
        var sizes = page.Letters.Where(l => l.Value is "P" or "E").Select(l => Math.Round(l.FontSize, 1)).Distinct().ToList();
        Assert.True(sizes.Count >= 2, "small capitals are drawn smaller");
        var content = System.Text.Encoding.Latin1.GetString(page.ContentBytes());
        Assert.Contains("] 0 d", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Vertically_merged_cells_grow_the_last_row_of_the_merge()
    {
        var document = new WordDocument();
        var table = new WordTable(100, 100) { Properties = new WordTableProperties { StyleId = "TableGrid" } };
        var first = table.AddRow(string.Join('\n', Enumerable.Range(1, 6).Select(i => "Fusion " + i)), "R1");
        first.Cells[0].Properties = new WordTableCellProperties { VerticalMerge = WordVerticalMerge.Restart, Shading = "FFEEAA" };
        var second = table.AddRow(string.Empty, "R2");
        second.Cells[0].Properties = new WordTableCellProperties { VerticalMerge = WordVerticalMerge.Continue };
        document.AddTable(table);
        document.AddParagraph("Après");

        var page = PdfDocument.Open(WordToPdf.Convert(document).Pdf).GetPage(1);

        var lastMerged = page.Letters.Where(l => l.Value == "6").Min(l => l.Y);
        var after = page.Letters.First(l => l.Value == "A").Y;
        Assert.True(after < lastMerged, "the paragraph after the table is below the whole merged cell");
    }

    [Fact]
    public void Rewriting_text_inside_links_and_insertions_removes_emptied_wrappers()
    {
        var body = """<w:p><w:hyperlink r:id="rId9"><w:r><w:t>lien</w:t></w:r></w:hyperlink><w:ins w:id="1" w:author="A"><w:r><w:t> ajouté</w:t></w:r></w:ins></w:p>""";
        var editor = WordEditor.Open(DocxFactory.Build(body, documentRelationships: """<Relationship Id="rId9" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink" Target="https://example.org" TargetMode="External"/>"""));

        editor.Paragraphs()[0].SetText("nouveau");

        var xml = DocxFactory.Part(editor.ToArray(), "word/document.xml");
        Assert.DoesNotContain("w:ins", xml, StringComparison.Ordinal);
        Assert.Contains("nouveau", xml, StringComparison.Ordinal);
        Assert.Equal("nouveau", editor.ToDocument().Text);
    }

    [Fact]
    public void Numbering_definitions_linked_through_a_style_borrow_its_levels()
    {
        var numbering = """
            <w:numbering xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <w:abstractNum w:abstractNumId="0"><w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="upperLetter"/><w:lvlText w:val="%1)"/></w:lvl></w:abstractNum>
              <w:abstractNum w:abstractNumId="1"><w:numStyleLink w:val="ListeLettres"/></w:abstractNum>
              <w:num w:numId="1"><w:abstractNumId w:val="0"/></w:num>
              <w:num w:numId="2"><w:abstractNumId w:val="1"/></w:num>
            </w:numbering>
            """;
        var styles = """
            <w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <w:style w:type="numbering" w:styleId="ListeLettres"><w:name w:val="Liste lettres"/><w:pPr><w:numPr><w:numId w:val="1"/></w:numPr></w:pPr></w:style>
            </w:styles>
            """;
        var package = DocxFactory.Build(
            """<w:p><w:pPr><w:numPr><w:ilvl w:val="0"/><w:numId w:val="2"/></w:numPr></w:pPr><w:r><w:t>Point</w:t></w:r></w:p>""",
            extraParts: new Dictionary<string, string> { ["word/numbering.xml"] = numbering, ["word/styles.xml"] = styles },
            documentRelationships: """<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering" Target="numbering.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>""");

        var document = WordDocument.Load(package);

        Assert.Equal("A)", new WordListCounter(document.Numbering).Next(2, 0)!.Value.Label);
    }
}
