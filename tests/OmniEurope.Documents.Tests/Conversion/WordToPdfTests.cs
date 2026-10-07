// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

public sealed class WordToPdfTests
{
    private const double Line = 20;

    [Fact]
    public void Page_fields_headers_and_first_page_footer_follow_the_pages()
    {
        var document = new WordDocument { Information = new WordInformation { Title = "Champs" } };
        var section = document.Sections[0];
        section.Page = section.Page with { TitlePage = true };
        section.Headers[WordHeaderFooterKind.Default] = new WordHeaderFooter(new WordParagraph("En-tête courant"));
        section.Footers[WordHeaderFooterKind.Default] = new WordHeaderFooter(new WordParagraph("Page ").Add(WordField.Page()).AddText(" sur ").Add(WordField.NumPages()));
        section.Footers[WordHeaderFooterKind.First] = new WordHeaderFooter(new WordParagraph("Couverture"));
        document.AddParagraph("Un");
        document.AddPageBreak();
        document.AddParagraph("Deux");
        document.AddPageBreak();
        document.AddParagraph("Trois");

        var result = WordToPdf.Convert(document);

        Assert.Equal(3, result.PageCount);
        var pdf = PdfDocument.Open(result.Pdf);
        Assert.Equal("Champs", pdf.Information.Title);
        Assert.Contains("Couverture", pdf.GetPage(1).Text);
        Assert.DoesNotContain("En-tête courant", pdf.GetPage(1).Text);
        Assert.Contains("Page 2 sur 3", pdf.GetPage(2).Text);
        Assert.Contains("En-tête courant", pdf.GetPage(3).Text);
        Assert.Contains("Trois", pdf.GetPage(3).Text);
        Assert.Empty(result.Gaps);
    }

    [Theory]
    [InlineData(true, new[] { 4, 2 })]
    [InlineData(false, new[] { 5, 1 })]
    public void Widow_control_moves_a_line_so_none_stands_alone(bool widowControl, int[] expected)
    {
        var document = Exact(pageLines: 5);
        document.Body.Add(new WordParagraph(string.Join('\n', Enumerable.Range(1, 6).Select(i => "L" + i))) { Properties = new WordParagraphProperties { WidowControl = widowControl } });

        var pdf = PdfDocument.Open(WordToPdf.Convert(document).Pdf);

        Assert.Equal(expected, pdf.Pages.Select(p => p.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length));
    }

    [Fact]
    public void Keep_with_next_and_keep_lines_carry_paragraphs_to_the_next_page()
    {
        var document = Exact(pageLines: 5);
        foreach (var i in Enumerable.Range(1, 4))
        {
            document.AddParagraph("Remplissage " + i);
        }

        document.Body.Add(new WordParagraph("Titre lié") { Properties = new WordParagraphProperties { KeepNext = true } });
        document.Body.Add(new WordParagraph("Bloc A\nBloc B") { Properties = new WordParagraphProperties { KeepLines = true } });

        var pdf = PdfDocument.Open(WordToPdf.Convert(document).Pdf);

        Assert.Equal(2, pdf.PageCount);
        Assert.DoesNotContain("Titre lié", pdf.GetPage(1).Text);
        Assert.StartsWith("Titre lié", pdf.GetPage(2).Text);
        Assert.Contains("Bloc B", pdf.GetPage(2).Text);
    }

    [Fact]
    public void Footnotes_sit_at_the_bottom_of_the_page_of_their_reference()
    {
        var document = Exact(pageLines: 6);
        document.AddParagraph("Sans note");
        document.AddPageBreak();
        document.AddParagraph("Avec note").Add(document.AddFootnote("Texte de la note"));
        document.AddParagraph("Après");

        var pdf = PdfDocument.Open(WordToPdf.Convert(document).Pdf);

        Assert.DoesNotContain("Texte de la note", pdf.GetPage(1).Text);
        var page = pdf.GetPage(2);
        Assert.Contains("Texte de la note", page.Text);
        var note = page.Letters.First(l => l.Value == "T");
        var body = page.Letters.First(l => l.Value == "A");
        Assert.True(note.Y < body.Y, "the note is below the body text");
    }

    [Fact]
    public void Long_tables_repeat_their_header_rows_and_split_between_rows()
    {
        var document = new WordDocument();
        var table = new WordTable(200, 200) { Properties = new WordTableProperties { StyleId = "TableGrid" } };
        table.AddRow("Entête gauche", "Entête droite").Properties = new WordTableRowProperties { IsHeader = true };
        foreach (var i in Enumerable.Range(1, 80))
        {
            table.AddRow("Ligne " + i, "Valeur " + i);
        }

        document.AddTable(table);

        var pdf = PdfDocument.Open(WordToPdf.Convert(document).Pdf);

        Assert.True(pdf.PageCount >= 2);
        Assert.All(pdf.Pages, p => Assert.StartsWith("Entête gauche", p.Text));
        var all = string.Join('\n', pdf.Pages.Select(p => p.Text));
        Assert.All(Enumerable.Range(1, 80), i => Assert.Contains("Ligne " + i + " Valeur " + i, all));
    }

    [Fact]
    public void Columns_are_balanced_before_a_continuous_section()
    {
        var document = new WordDocument();
        document.Sections[0].Page = WordPageSetup.A4 with { Columns = 2 };
        foreach (var name in new[] { "Alpha", "Bravo", "Charlie", "Delta" })
        {
            document.AddParagraph(name);
        }

        document.AddSection(WordPageSetup.A4 with { Start = WordSectionStart.Continuous });
        document.AddParagraph("Pleine largeur");

        var page = PdfDocument.Open(WordToPdf.Convert(document).Pdf).GetPage(1);

        var middle = page.Width / 2;
        Assert.True(First(page, "A").X < middle && First(page, "B").X < middle);
        Assert.True(First(page, "C").X > middle && First(page, "D").X > middle);
        Assert.True(First(page, "P").Y < First(page, "B").Y, "the next section starts below the balanced columns");
    }

    [Fact]
    public void Justified_lines_end_on_the_right_margin_and_right_tabs_on_their_stop()
    {
        var document = new WordDocument();
        var words = string.Join(' ', Enumerable.Range(1, 60).Select(i => "mot" + i));
        document.Body.Add(new WordParagraph(words) { Properties = new WordParagraphProperties { Alignment = WordAlignment.Justify } });
        document.Body.Add(new WordParagraph("Total\t99") { Properties = new WordParagraphProperties { Tabs = [new WordTabStop(300, WordTabAlignment.Right, WordTabLeader.Dot)] } });

        var page = PdfDocument.Open(WordToPdf.Convert(document).Pdf).GetPage(1);

        var left = WordPageSetup.A4.MarginLeft;
        var right = WordPageSetup.A4.Width - WordPageSetup.A4.MarginRight;
        var lines = page.Letters.Where(l => l.Value.Trim().Length > 0 && l.FontSize > 10).GroupBy(l => Math.Round(l.Y)).OrderByDescending(g => g.Key).ToList();
        Assert.True(lines.Count >= 4);
        foreach (var line in lines.Take(lines.Count - 3))
        {
            Assert.Equal(right, line.Max(l => l.X + l.Width), 1);
        }

        var nine = page.Letters.Last(l => l.Value == "9");
        Assert.Equal(left + 300, nine.X + nine.Width, 1);
        Assert.Contains(page.Letters, l => l.Value == "." && l.X > left + 40 && l.X < left + 290);
    }

    [Fact]
    public void Text_of_a_document_from_another_application_is_found_in_the_pdf()
    {
        var docx = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Word", "external.docx"));

        var result = WordToPdf.Convert(docx);

        var text = string.Join('\n', PdfDocument.Open(result.Pdf).Pages.Select(p => p.Text));
        foreach (var expected in new[] { "Rapport de test", "Premier paragraphe avec du gras", "Élément deux", "Étape B", "Fusion sur deux colonnes", "Ωμέγα, Привет, Ţară." })
        {
            Assert.Contains(expected, text);
        }

        Assert.DoesNotContain(result.Gaps, g => g.Contains("Symbol", StringComparison.Ordinal));
        Assert.Equal(result.Pdf, WordToPdf.Convert(docx).Pdf);
    }

    [Fact]
    public void Draws_emf_pictures_as_vector_graphics()
    {
        var document = new WordDocument();
        document.AddParagraph().Add(new WordPicture(WordImage.FromBytes(new EmfFixture().Brush(1, 0x00C0FF).Select(1).Ints(43, 10, 10, 390, 190).Text("EMF", 40, 80).ToArray()), 200, 100));

        var result = WordToPdf.Convert(document);

        Assert.DoesNotContain(result.Gaps, g => g.Contains("empty frame", StringComparison.Ordinal));
        var page = PdfDocument.Open(result.Pdf).GetPage(1);
        Assert.Contains("EMF", page.Text);
        var letter = page.Letters.First(l => l.Value == "E");
        Assert.InRange(letter.X, WordPageSetup.A4.MarginLeft, WordPageSetup.A4.MarginLeft + 200);
    }

    [Fact]
    public void Emf_paths_curves_ellipses_bitmaps_and_fonts_are_drawn()
    {
        var emf = new EmfFixture()
            .Ints(33)
            .Ints(17, 8).Ints(9, 400, 200).Ints(11, 400, 200)
            .Pen(1, 3, 0x0000FF).Select(1).Brush(2, 0x00FF00).Select(2)
            .PolyPolygon16([(10, 10), (100, 10), (100, 100), (10, 100)], [(30, 30), (60, 30), (60, 60), (30, 60)])
            .Points16(85, (120, 20), (150, 0), (180, 60), (210, 20))
            .Ints(27, 220, 20).Ints(54, 300, 90)
            .Ints(59).Points16(86, (220, 120), (300, 120), (260, 180)).Ints(60).Ints(63)
            .Ints(42, 310, 110, 390, 190)
            .Ints(59).Ints(27, 10, 120).Ints(54, 100, 190).Ints(54, 10, 190).Ints(27, 150, 150).Ints(54, 200, 150).Ints(60).Ints(64)
            .Ints(34, -1)
            .Bitmap(320, 20, 60, 60)
            .Font(3, -24, 700, "Arial").Select(3).Ints(22, 24).Ints(24, 0x000080)
            .Text("Gras", 20, 150);
        var document = new WordDocument();
        document.AddParagraph().Add(new WordPicture(WordImage.FromBytes(emf.ToArray()), 300, 150));

        var result = WordToPdf.Convert(document);

        Assert.Empty(result.Gaps);
        var page = PdfDocument.Open(result.Pdf).GetPage(1);
        Assert.Single(page.Images);
        var letter = First(page, "G");
        Assert.Contains("Bold", letter.FontName, StringComparison.Ordinal);
        Assert.InRange(letter.FontSize, 15, 21);
        var content = System.Text.Encoding.Latin1.GetString(page.ContentBytes());
        Assert.Contains("\nB*\n", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Column_breaks_border_styles_and_oversized_content_are_handled()
    {
        var document = new WordDocument();
        document.Sections[0].Page = WordPageSetup.A4 with { Columns = 2 };
        document.AddParagraph("Gauche").Add(new WordBreak(WordBreakKind.Column)).AddText("Droite");
        document.Body.Add(new WordParagraph("Encadré")
        {
            Properties = new WordParagraphProperties { Borders = new WordParagraphBorders(new WordBorder("double"), new WordBorder("dotted"), new WordBorder("dashed"), new WordBorder("single", 1)), Shading = "EEEEEE" },
        });
        document.AddParagraph().Add(new WordPicture(WordImage.FromBytes(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "rgb.png"))), 100, 2000));

        var result = WordToPdf.Convert(document);

        var page = PdfDocument.Open(result.Pdf).GetPage(1);
        Assert.True(First(page, "D").X > page.Width / 2);
        Assert.True(First(page, "G").X < page.Width / 2);
        var content = System.Text.Encoding.Latin1.GetString(page.ContentBytes());
        Assert.Contains("] 0 d", content, StringComparison.Ordinal);
        Assert.Contains("content taller than a page runs past its bottom", result.Gaps);
    }

    [Fact]
    public void A_row_taller_than_the_page_splits_its_cells_and_nested_tables()
    {
        var document = Exact(pageLines: 8);
        var nested = new WordTable(100);
        foreach (var i in Enumerable.Range(1, 12))
        {
            nested.AddRow("Imbriqué " + i);
        }

        var table = new WordTable(150, 150);
        var row = table.AddRow(string.Join('\n', Enumerable.Range(1, 12).Select(i => "Texte " + i)));
        row.Cells.Add(new WordTableCell());
        row.Cells[1].Blocks.Add(nested);
        var kept = table.AddRow("Insécable 1\nInsécable 2\nInsécable 3");
        kept.Properties = new WordTableRowProperties { CantSplit = true };
        kept.Cells.Add(new WordTableCell("fin"));
        document.AddTable(table);

        var pdf = PdfDocument.Open(WordToPdf.Convert(document).Pdf);

        Assert.Equal(2, pdf.PageCount);
        Assert.Contains("Texte 8", pdf.GetPage(1).Text);
        Assert.Contains("Texte 9", pdf.GetPage(2).Text);
        Assert.Contains("Imbriqué 8", pdf.GetPage(1).Text);
        Assert.Contains("Imbriqué 9", pdf.GetPage(2).Text);
        var all = string.Join('\n', pdf.Pages.Select(p => p.Text));
        Assert.All(Enumerable.Range(1, 12), i => Assert.Contains("Texte " + i, all));
        Assert.All(Enumerable.Range(1, 12), i => Assert.Contains("Imbriqué " + i, all));
        var page = pdf.Pages.Single(p => p.Text.Contains("Insécable 1", StringComparison.Ordinal));
        Assert.Contains("Insécable 3", page.Text);
    }

    [Fact]
    public void Approximations_are_reported()
    {
        var document = new WordDocument();
        document.AddParagraph("Police inconnue", null).Inlines[0].Properties = new WordRunProperties { Font = "Police Imaginaire" };
        document.AddParagraph().Add(new WordPicture(WordImage.FromBytes(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "rgb.png"))), 50, 50)
        {
            Floating = new WordFloatingPosition(10, "margin", 10, "paragraph", WordWrap.Square),
        });

        var gaps = WordToPdf.Convert(document).Gaps;

        Assert.Contains("font \"Police Imaginaire\" replaced by LiberationSerif", gaps);
        Assert.Contains("text does not flow around floating shapes", gaps);
    }

    // A page holding exactly the given number of 20 pt lines, without spacing between paragraphs.
    private static WordDocument Exact(int pageLines)
    {
        var document = new WordDocument();
        document.Styles.DefaultParagraphProperties = new WordParagraphProperties { SpacingAfter = 0, SpacingBefore = 0, LineSpacing = Line, LineSpacingRule = WordLineSpacingRule.Exact };
        document.Sections[0].Page = WordPageSetup.A4 with { Height = 40 + (pageLines * Line) + 1, MarginTop = 20, MarginBottom = 20, HeaderDistance = 10, FooterDistance = 10 };
        return document;
    }

    private static OmniEurope.Documents.Pdf.Text.PdfLetter First(PdfPage page, string value) => page.Letters.First(l => l.Value == value);
}
