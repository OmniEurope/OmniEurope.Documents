// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Pdf.Text;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// Word page layout (ECMA-376 part 1, §17.4 tables, §17.6 sections, §17.11 notes): table styles by region,
/// table alignment and widths, vertical merges, explicit column widths, odd and even section starts,
/// endnotes, text boxes and pictures that cannot be drawn. Positions are read back from the PDF.
/// </summary>
public sealed class WordLayoutTests
{
    private static readonly WordPageSetup Page = WordPageSetup.A4;

    [Fact]
    public void Table_style_regions_shade_the_cells_they_name()
    {
        // Three columns, three rows, header, total row and first column on, column bands on: B is a band column,
        // C is the last column; the corners of the total row have their own shading.
        var style = new WordStyle("Grid", WordStyleType.Table)
        {
            ConditionalFormats =
            [
                new WordTableConditionalFormat(WordTableRegion.WholeTable, CellProperties: new WordTableCellProperties { Shading = "EEEEEE" }),
                new WordTableConditionalFormat(WordTableRegion.OddColumns, CellProperties: new WordTableCellProperties { Shading = "00FF00" }),
                new WordTableConditionalFormat(WordTableRegion.BottomLeftCell, CellProperties: new WordTableCellProperties { Shading = "FF0000" }),
                new WordTableConditionalFormat(WordTableRegion.BottomRightCell, CellProperties: new WordTableCellProperties { Shading = "0000FF" }, RunProperties: new WordRunProperties { Bold = true }),
            ],
        };
        var document = new WordDocument();
        document.Styles.Add(style);
        var table = new WordTable(100, 100, 100) { Properties = new WordTableProperties { StyleId = "Grid", Look = WordTableLook.FirstRow | WordTableLook.LastRow | WordTableLook.FirstColumn | WordTableLook.NoHorizontalBanding } };
        table.AddRow("a1", "b1", "c1");
        table.AddRow("a2", "b2", "c2");
        table.AddRow("a3", "b3", "c3");
        document.AddTable(table);

        var (page, image) = Render(document);

        Assert.Equal((0, 255, 0), Behind(page, image, "b2"));
        Assert.Equal((238, 238, 238), Behind(page, image, "c2"));
        Assert.Equal((255, 0, 0), Behind(page, image, "a3"));
        Assert.Equal((0, 0, 255), Behind(page, image, "c3"));
        Assert.Contains("Bold", page.Letters.First(l => l.Value == "c" && Word(page, l) == "c3").FontName, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(WordAlignment.Center, 0.5)]
    [InlineData(WordAlignment.Right, 1.0)]
    public void Tables_narrower_than_the_column_are_aligned(WordAlignment alignment, double share)
    {
        var document = new WordDocument();
        var table = new WordTable(200) { Properties = new WordTableProperties { Alignment = alignment } };
        table.AddRow("X");
        document.AddTable(table);

        var letter = PdfDocument.Open(WordToPdf.Convert(document).Pdf).GetPage(1).Letters[0];

        // The cell text starts after the default cell margin of 5.4 points.
        Assert.Equal(Math.Round(Page.MarginLeft + ((Page.ContentWidth - 200) * share) + 5.4, 1), Math.Round(letter.X, 1));
    }

    [Theory]
    [InlineData(400, 400, null, 0.5)]
    [InlineData(100, 100, 50.0, 0.25)]
    public void Column_widths_are_scaled_to_the_table_width(double first, double second, double? percent, double secondStart)
    {
        // Too wide for the page, or a percentage of the text width: both columns keep their proportions.
        var document = new WordDocument();
        var table = new WordTable(first, second) { Properties = new WordTableProperties { Width = percent is { } p ? WordWidth.Percent(p) : null } };
        table.AddRow("A", "B");
        document.AddTable(table);

        var letter = PdfDocument.Open(WordToPdf.Convert(document).Pdf).GetPage(1).Letters.First(l => l.Value == "B");

        Assert.Equal(Math.Round(Page.MarginLeft + (Page.ContentWidth * secondStart) + 5.4, 1), Math.Round(letter.X, 1));
    }

    [Fact]
    public void A_vertically_merged_cell_taller_than_its_rows_makes_the_last_row_grow()
    {
        var document = new WordDocument();
        var table = new WordTable(150, 150);
        var first = table.AddRow("un\ndeux\ntrois\nquatre", "haut");
        first.Cells[0].Properties = new WordTableCellProperties { VerticalMerge = WordVerticalMerge.Restart };
        var second = table.AddRow(string.Empty, "bas");
        second.Cells[0].Properties = new WordTableCellProperties { VerticalMerge = WordVerticalMerge.Continue };
        var alone = table.AddRow("seul", "x");
        alone.Cells[0].Properties = new WordTableCellProperties { VerticalMerge = WordVerticalMerge.Restart };
        document.AddTable(table);
        document.AddParagraph("Après");

        var page = PdfDocument.Open(WordToPdf.Convert(document).Pdf).GetPage(1);

        var quatre = page.Letters.First(l => l.Value == "q").Y;
        var seul = page.Letters.First(l => Word(page, l) == "se").Y;
        Assert.True(seul < quatre, "the row after the merge starts below its last line");
    }

    [Fact]
    public void Explicit_column_widths_place_each_column()
    {
        var document = new WordDocument();
        document.Sections[0].Page = Page with { Columns = 2, ColumnWidths = [100, 300], ColumnSpacing = 20 };
        document.AddParagraph("Gauche").Add(new WordBreak(WordBreakKind.Column)).AddText("Droite");

        var page = PdfDocument.Open(WordToPdf.Convert(document).Pdf).GetPage(1);

        Assert.Equal(Math.Round(Page.MarginLeft + 120, 1), Math.Round(page.Letters.First(l => l.Value == "D").X, 1));
    }

    [Theory]
    [InlineData(WordSectionStart.OddPage, 3)]
    [InlineData(WordSectionStart.EvenPage, 2)]
    public void Odd_and_even_page_sections_skip_a_page_when_needed(WordSectionStart start, int page)
    {
        var document = new WordDocument();
        document.AddParagraph("Premier");
        document.AddSection(Page with { Start = start });
        document.AddParagraph("Second");

        var pdf = PdfDocument.Open(WordToPdf.Convert(document).Pdf);

        Assert.Equal(page, pdf.PageCount);
        Assert.Equal("Second", pdf.GetPage(page).Text);
    }

    [Fact]
    public void Endnotes_follow_the_text_after_a_separator()
    {
        var document = new WordDocument();
        document.AddParagraph("Corps").Add(document.AddEndnote("Note finale"));
        document.AddParagraph("Fin du texte");

        var page = PdfDocument.Open(WordToPdf.Convert(document).Pdf).GetPage(1);

        Assert.Contains("Note finale", page.Text, StringComparison.Ordinal);
        Assert.True(page.Letters.First(l => l.Value == "N").Y < page.Letters.First(l => l.Value == "F").Y, "the endnote comes after the last paragraph");
    }

    [Fact]
    public void Text_boxes_draw_their_blocks_and_unknown_pictures_leave_a_frame()
    {
        var document = new WordDocument();
        var box = new WordTextBox(200, 60);
        box.Blocks.Add(new WordParagraph("Dans la boîte"));
        document.AddParagraph().Add(box);
        document.AddParagraph().Add(new WordPicture(new WordImage([1, 2, 3], "image/svg+xml"), 50, 50));

        var result = WordToPdf.Convert(document);

        Assert.Contains("Dans la boîte", PdfDocument.Open(result.Pdf).GetPage(1).Text, StringComparison.Ordinal);
        Assert.Contains("picture format image/svg+xml drawn as an empty frame", result.Gaps);
    }

    [Theory]
    [InlineData(WordTabLeader.Hyphen, "-")]
    [InlineData(WordTabLeader.MiddleDot, "·")]
    public void Tab_leaders_fill_the_space_before_the_stop(WordTabLeader leader, string character)
    {
        var document = new WordDocument();
        var paragraph = new WordParagraph { Properties = new WordParagraphProperties { Tabs = [new WordTabStop(300, WordTabAlignment.Right, leader)] } };
        paragraph.AddText("A").Add(new WordTab()).AddText("B");
        document.Body.Add(paragraph);

        var text = PdfDocument.Open(WordToPdf.Convert(document).Pdf).GetPage(1).Text;

        Assert.Contains(character + character + character, text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(WordTabLeader.Underscore)]
    [InlineData(WordTabLeader.Heavy)]
    public void Line_leaders_are_drawn_under_the_tab(WordTabLeader leader)
    {
        var document = new WordDocument();
        var paragraph = new WordParagraph { Properties = new WordParagraphProperties { Tabs = [new WordTabStop(300, WordTabAlignment.Left, leader)] } };
        paragraph.AddText("A").Add(new WordTab()).AddText("B");
        document.Body.Add(paragraph);

        var (page, image) = Render(document);

        // Halfway between A and the stop, at the underline position just below the baseline.
        var a = page.Letters.First(l => l.Value == "A");
        var x = (int)(Page.MarginLeft + 150);
        var ink = Enumerable.Range((int)(page.Height - a.Y), 4).Any(y => image.GetRgba(x, y).R < 128);
        Assert.True(ink);
        Assert.DoesNotContain("_", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Centre_tabs_and_centred_or_distributed_paragraphs()
    {
        var document = new WordDocument();
        var tabbed = new WordParagraph { Properties = new WordParagraphProperties { Tabs = [new WordTabStop(200, WordTabAlignment.Center)] } };
        tabbed.Add(new WordTab()).AddText("MM");
        document.Body.Add(tabbed);
        document.Body.Add(new WordParagraph("NN") { Properties = new WordParagraphProperties { Alignment = WordAlignment.Center } });
        document.Body.Add(new WordParagraph("O P") { Properties = new WordParagraphProperties { Alignment = WordAlignment.Distribute } });

        var letters = PdfDocument.Open(WordToPdf.Convert(document).Pdf).GetPage(1).Letters;

        double Middle(string value) => (letters.First(l => l.Value == value).X + letters.Last(l => l.Value == value).X + letters.Last(l => l.Value == value).Width) / 2;
        Assert.Equal(Math.Round(Page.MarginLeft + 200, 0), Math.Round(Middle("M"), 0));
        Assert.Equal(Math.Round(Page.MarginLeft + (Page.ContentWidth / 2), 0), Math.Round(Middle("N"), 0));
        var p = letters.First(l => l.Value == "P");
        Assert.Equal(Math.Round(Page.MarginLeft + Page.ContentWidth, 0), Math.Round(p.X + p.Width, 0));
    }

    [Fact]
    public void Symbol_fonts_are_drawn_with_look_alikes()
    {
        var document = new WordDocument();
        document.AddParagraph("a", null).Inlines[0].Properties = new WordRunProperties { Font = "Symbol" };
        document.AddParagraph().Add(new WordSymbol("Wingdings", '')).Add(new WordSymbol("Symbol", ''));

        var result = WordToPdf.Convert(document);
        var text = PdfDocument.Open(result.Pdf).GetPage(1).Text;

        // Symbol "a" is alpha; Wingdings 0xFC is a check mark or a bullet; Symbol 0x21 has no look-alike entry.
        Assert.Contains("α", text, StringComparison.Ordinal);
        Assert.Contains("!", text, StringComparison.Ordinal);
        Assert.Contains("symbol font characters approximated", result.Gaps);
    }

    [Fact]
    public void Fields_show_their_result_and_count_section_pages()
    {
        var document = new WordDocument();
        document.AddParagraph().Add(new WordField("REF signet", "résultat"));
        document.AddParagraph("Pages : ").Add(new WordField("SECTIONPAGES", "9"));

        var text = PdfDocument.Open(WordToPdf.Convert(document).Pdf).GetPage(1).Text;

        Assert.Contains("résultat", text, StringComparison.Ordinal);
        Assert.Contains("Pages : 1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Unreadable_pictures_are_reported_and_numbering_restarts_no_longer_are()
    {
        var document = new WordDocument { Settings = new WordSettings { FootnoteRestart = WordNoteRestart.EachPage } };
        document.AddParagraph("Texte").Add(document.AddFootnote("note"));
        document.AddParagraph().Add(new WordPicture(new WordImage([1, 2, 3, 4], "image/png"), 40, 40));

        var gaps = WordToPdf.Convert(document).Gaps;

        Assert.DoesNotContain(gaps, g => g.Contains("footnote", StringComparison.Ordinal));
        Assert.Contains("unreadable picture replaced by a frame", gaps);
    }

    [Theory]
    [InlineData("page", "page", 10.0, 20.0)]
    [InlineData("margin", "topMargin", 10.0, 20.0)]
    [InlineData("page", "margin", 10.0, 20.0)]
    public void Floating_pictures_are_placed_from_their_anchor(string horizontal, string vertical, double dx, double dy)
    {
        var png = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "rgb.png"));
        var document = new WordDocument();
        document.AddParagraph().Add(new WordPicture(WordImage.FromBytes(png), 50, 50) { Floating = new WordFloatingPosition(dx, horizontal, dy, vertical, WordWrap.None) });

        var page = PdfDocument.Open(WordToPdf.Convert(document).Pdf).GetPage(1);
        var bounds = Assert.Single(page.Images).Bounds;

        var left = (horizontal == "page" ? 0 : Page.MarginLeft) + dx;
        var top = (vertical == "margin" ? Page.MarginTop : 0) + dy;
        Assert.Equal(Math.Round(left, 1), Math.Round(bounds.Left, 1));
        Assert.Equal(Math.Round(page.Height - top, 1), Math.Round(bounds.Top, 1));
    }

    private static (PdfPage Page, RasterImage Image) Render(WordDocument document)
    {
        var page = PdfDocument.Open(WordToPdf.Convert(document).Pdf).GetPage(1);
        return (page, PdfRenderer.Render(page, new PdfRenderOptions { Dpi = 72 }).Image);
    }

    // The colour just right of a cell's text, on its baseline's line (image y = page height - PDF y).
    private static (int R, int G, int B) Behind(PdfPage page, RasterImage image, string word)
    {
        var letter = page.Letters.First(l => Word(page, l) == word && l.Value == word[..1]);
        var (r, g, b, _) = image.GetRgba((int)(letter.X + 40), (int)(page.Height - letter.Y - 3));
        return (r, g, b);
    }

    // The two-letter word a letter starts.
    private static string Word(PdfPage page, PdfLetter letter)
    {
        var index = page.Letters.ToList().IndexOf(letter);
        return index + 1 < page.Letters.Count ? letter.Value + page.Letters[index + 1].Value : letter.Value;
    }
}
