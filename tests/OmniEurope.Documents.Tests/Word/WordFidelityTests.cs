// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Editing;

namespace OmniEurope.Documents.Tests.Word;

public sealed class WordFidelityTests
{
    private static readonly byte[] Png = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "rgb.png"));

    [Fact]
    public void Reads_vml_pictures_text_boxes_and_wrapped_blocks()
    {
        var body = """
            <w:sdt><w:sdtContent><w:p><w:r><w:t>Dans un contrôle</w:t></w:r></w:p></w:sdtContent></w:sdt>
            <w:customXml><w:p><w:r><w:t>Dans du XML</w:t></w:r></w:p></w:customXml>
            <mc:AlternateContent><mc:Choice Requires="w14"><w:p><w:r><w:t>Choix</w:t></w:r></w:p></mc:Choice><mc:Fallback><w:p><w:r><w:t>Repli</w:t></w:r></w:p></mc:Fallback></mc:AlternateContent>
            <w:p><w:r><w:pict xmlns:v="urn:schemas-microsoft-com:vml"><v:shape style="width:72pt;height:0.5in"><v:imagedata r:id="rId1"/></v:shape></w:pict></w:r></w:p>
            <w:p><w:r><w:pict xmlns:v="urn:schemas-microsoft-com:vml"><v:rect style="width:200px;height:3cm"><v:textbox><w:txbxContent><w:p><w:r><w:t>Boîte VML</w:t></w:r></w:p></w:txbxContent></v:textbox></v:rect></w:pict></w:r></w:p>
            <w:p><w:smartTag><w:r><w:t>étiquette</w:t></w:r></w:smartTag><w:moveTo><w:r><w:t> déplacé</w:t></w:r></w:moveTo><w:moveFrom><w:r><w:delText>ancien</w:delText></w:r></w:moveFrom></w:p>
            """;
        var package = DocxFactory.Build(
            body,
            documentRelationships: """<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/image1.png"/>""",
            binaryParts: new Dictionary<string, byte[]> { ["word/media/image1.png"] = Png });

        var blocks = WordDocument.Load(package).Blocks.OfType<WordParagraph>().ToList();

        Assert.Equal(["Dans un contrôle", "Dans du XML", "Choix", string.Empty, string.Empty, "étiquette déplacé"], blocks.Select(b => b.Text));
        var picture = Assert.IsType<WordPicture>(blocks[3].Inlines.Single());
        Assert.Equal((72, 36), (picture.Width, picture.Height));
        Assert.Equal(Png, picture.Image.Data);
        var box = Assert.IsType<WordTextBox>(blocks[4].Inlines.Single());
        Assert.Equal("Boîte VML", box.Blocks.Single().Text);
        Assert.Equal(150, box.Width);
        Assert.Equal(85.04, box.Height, 2);
        Assert.Equal(WordRevisionKind.Deleted, blocks[5].Inlines.Last().Revision!.Kind);
    }

    [Fact]
    public void Comments_tabs_underlines_and_table_styles_round_trip()
    {
        var document = new WordDocument();
        document.Comments.Add(new WordComment(7) { Author = "Relecteur", Initials = "R", Date = new DateTimeOffset(2026, 10, 6, 9, 30, 0, TimeSpan.Zero) });
        document.Comments[0].Blocks.Add(new WordParagraph("À vérifier"));
        var tabs = new WordTabStop[]
        {
            new(72, WordTabAlignment.Left, WordTabLeader.Dot),
            new(144, WordTabAlignment.Center, WordTabLeader.Hyphen),
            new(216, WordTabAlignment.Right, WordTabLeader.Underscore),
            new(288, WordTabAlignment.Decimal, WordTabLeader.MiddleDot),
            new(360, WordTabAlignment.Bar, WordTabLeader.Heavy),
        };
        var paragraph = new WordParagraph { Properties = new WordParagraphProperties { Tabs = tabs, Borders = new WordParagraphBorders(Bottom: new WordBorder("double", 1.5, 1, "FF0000")) } };
        foreach (var underline in Enum.GetValues<WordUnderline>())
        {
            paragraph.AddText(underline.ToString(), new WordRunProperties { Underline = underline, Caps = true, Strike = false, Position = 3, CharacterSpacing = 1 });
        }

        paragraph.Add(new WordCommentReference(7)).Add(new WordTab { Alignment = WordTabAlignment.Right, Leader = WordTabLeader.Dot });
        document.Body.Add(paragraph);
        document.Styles.Add(new WordStyle("Grille", WordStyleType.Table)
        {
            TableProperties = new WordTableProperties { Look = WordTableLook.FirstRow | WordTableLook.NoVerticalBanding, CellMargins = new WordCellMargins(1, 2, 3, 4) },
            ConditionalFormats = Enum.GetValues<WordTableRegion>().Select(r => new WordTableConditionalFormat(r, RunProperties: new WordRunProperties { Bold = true }, CellProperties: new WordTableCellProperties { Shading = "EEEEEE" })).ToList(),
        });

        var reread = WordDocument.Load(document.ToArray());

        var comment = reread.Comments.Single();
        Assert.Equal((7, "Relecteur", "R", "À vérifier"), (comment.Id, comment.Author, comment.Initials, comment.Text));
        Assert.Equal(document.Comments[0].Date, comment.Date);
        var read = reread.Blocks.OfType<WordParagraph>().Single();
        Assert.Equal(tabs, read.Properties.Tabs);
        Assert.Equal(new WordBorder("double", 1.5, 1, "FF0000"), read.Properties.Borders!.Bottom);
        Assert.Equal(Enum.GetValues<WordUnderline>(), read.Inlines.OfType<WordText>().Select(t => t.Properties.Underline!.Value));
        Assert.Equal(7, read.Inlines.OfType<WordCommentReference>().Single().Id);
        Assert.Equal(WordTabAlignment.Right, read.Inlines.OfType<WordTab>().Single().Alignment);
        var style = reread.Styles.Get("Grille")!;
        Assert.Equal(WordTableLook.FirstRow | WordTableLook.NoVerticalBanding, style.TableProperties!.Look);
        Assert.Equal(Enum.GetValues<WordTableRegion>(), style.ConditionalFormats.Select(f => f.Region));
        Assert.All(style.ConditionalFormats, f => Assert.Equal("EEEEEE", f.CellProperties!.Shading));
    }

    [Fact]
    public void Legacy_table_look_and_number_formats_are_read()
    {
        var body = """<w:tbl><w:tblPr><w:tblLook w:val="04A0"/></w:tblPr><w:tblGrid><w:gridCol w:w="2000"/></w:tblGrid><w:tr><w:tc><w:p/></w:tc></w:tr></w:tbl><w:sectPr><w:pgNumType w:fmt="upperRoman" w:start="3"/><w:cols w:num="2" w:equalWidth="0" w:sep="1"><w:col w:w="3000"/><w:col w:w="5000"/></w:cols><w:type w:val="oddPage"/></w:sectPr>""";

        var document = WordDocument.Load(DocxFactory.Build(body));

        Assert.Equal(WordTableLook.FirstRow | WordTableLook.FirstColumn | WordTableLook.NoVerticalBanding, document.Blocks.OfType<WordTable>().Single().Properties.Look);
        var page = document.Sections.Single().Page;
        Assert.Equal((WordNumberFormat.UpperRoman, 3), (page.PageNumberFormat, page.PageNumberStart));
        Assert.Equal([150, 250], page.ColumnWidths!);
        Assert.True(page.ColumnSeparator);
        Assert.Equal(WordSectionStart.OddPage, page.Start);
        var reread = WordDocument.Load(document.ToArray()).Sections.Single().Page;
        Assert.Equal(page.ColumnWidths, reread.ColumnWidths);
        Assert.Equal((WordNumberFormat.UpperRoman, 3, WordSectionStart.OddPage), (reread.PageNumberFormat, reread.PageNumberStart, reread.Start));
    }

    [Fact]
    public void Numbering_overrides_and_row_properties_round_trip()
    {
        var document = new WordDocument();
        var list = document.Numbering.AddNumberedList();
        var restarted = document.Numbering.Restart(list, start: 5);
        document.Numbering.Instances[restarted].LevelOverrides[1] = new WordNumberingLevel(1)
        {
            Format = WordNumberFormat.UpperLetter,
            Text = "(%2)",
            Suffix = WordLabelSuffix.Space,
            RestartAfter = 1,
            StyleId = "ListParagraph",
            Alignment = WordAlignment.Right,
        };
        document.AddParagraph("Un").AsListItem(list);
        document.AddParagraph("Deux").AsListItem(list);
        document.AddParagraph("Cinq").AsListItem(restarted);
        document.AddParagraph("Cinq A").AsListItem(restarted, 1);
        var table = new WordTable(100);
        var row = table.AddRow("x");
        row.Properties = new WordTableRowProperties { Height = 20, HeightRule = WordRowHeightRule.Exact, GridBefore = 1, GridAfter = 2, CantSplit = false };
        row.Revision = new WordRevision(WordRevisionKind.Inserted, "R");
        document.AddTable(table);

        var reread = WordDocument.Load(document.ToArray());

        var counter = new WordListCounter(reread.Numbering);
        var labels = reread.Blocks.OfType<WordParagraph>().Select(p => counter.Next(p.Properties.NumberingId!.Value, p.Properties.NumberingLevel ?? 0)!.Value).ToList();
        Assert.Equal(["1.", "2.", "5.", "(A)"], labels.Select(l => l.Label));
        Assert.Equal((WordLabelSuffix.Space, WordAlignment.Right, "ListParagraph", 1), (labels[3].Level.Suffix, labels[3].Level.Alignment, labels[3].Level.StyleId, labels[3].Level.RestartAfter));
        var readRow = reread.Blocks.OfType<WordTable>().Single().Rows.Single();
        Assert.Equal(row.Properties, readRow.Properties);
        Assert.Equal((WordRevisionKind.Inserted, "R"), (readRow.Revision!.Kind, readRow.Revision.Author));
    }

    [Fact]
    public void Identifies_metafiles_and_rejects_unknown_images()
    {
        var emf = new byte[60];
        emf[0] = 1;
        " EMF"u8.CopyTo(emf.AsSpan(40));
        byte[] wmf = [0xD7, 0xCD, 0xC6, 0x9A, 0, 0];

        Assert.Equal("image/x-emf", WordImage.FromBytes(emf).ContentType);
        Assert.Equal("image/x-wmf", WordImage.FromBytes(wmf).ContentType);
        Assert.Throws<ArgumentException>(() => WordImage.FromBytes([1, 2, 3]));
    }

    [Fact]
    public void Characters_xml_cannot_hold_are_dropped()
    {
        var document = new WordDocument { Information = new WordInformation { Title = "Titre\u0001" } };
        document.AddParagraph("a\u0001b\uD800c😀");

        var reread = WordDocument.Load(document.ToArray());

        Assert.Equal("abc😀", reread.Text);
        Assert.Equal("Titre", reread.Information.Title);
    }

    [Fact]
    public void Editor_creates_and_updates_core_dates()
    {
        var plain = new WordDocument();
        plain.AddParagraph("x");
        var editor = WordEditor.Open(plain.ToArray());
        var created = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        editor.Information = new WordInformation { Title = "T", Created = created, Modified = created };
        editor.Information = editor.Information with { Modified = created.AddDays(1), Title = null };

        var information = WordDocument.Load(editor.ToArray()).Information;
        Assert.Equal(created, information.Created);
        Assert.Equal(created.AddDays(1), information.Modified);
        Assert.Null(information.Title);
    }
}
