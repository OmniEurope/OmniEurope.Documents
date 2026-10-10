// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Word;
using static OmniEurope.Documents.Tests.Conversion.WordFloatWrapTests;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// Unequal columns (ECMA-376 part 1, §17.6.4 cols and §17.6.3 col: <c>w:equalWidth="0"</c>, each column's width and
/// space after it), the line between columns (<c>w:sep</c>), column breaks and the balancing of columns before a
/// continuous section break, and the page-end rules measured against Word. Positions are read back from the PDF; the
/// text is 10 pt Liberation Sans on exact 12 pt lines, the left margin is at 70.9 pt.
/// </summary>
public sealed class WordColumnLayoutTests
{
    private const double Margin = 70.9;
    private const string Words = "lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor incididunt ut labore et dolore magna aliqua ut enim ad minim veniam quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat";

    [Fact]
    public void Each_unequal_column_breaks_its_lines_at_its_own_width()
    {
        // A 100 pt column, 20 pt of space, a 300 pt column: the second column's text is broken at 300 pt, not at the
        // first column's width.
        var document = Document(new WordParagraph().AddText(Words).Add(new WordBreak(WordBreakKind.Column)).AddText(Words));
        document.Sections[0].Page = WordPageSetup.A4 with { Columns = 2, ColumnWidths = [100, 300], ColumnSpacings = [20, 0] };

        var page = Convert(document);
        var first = Column(page, 0, Margin + 110);
        var second = Column(page, Margin + 110, page.Width);

        Assert.All(first, l => Assert.True(l.Right <= Margin + 100 + 0.5, $"{l.Text} ends at {l.Right}"));
        Assert.All(second, l => Assert.Equal(Margin + 120, l.Left, 0.5));
        Assert.All(second, l => Assert.True(l.Right <= Margin + 420 + 0.5, $"{l.Text} ends at {l.Right}"));
        Assert.Contains(second, l => l.Right > Margin + 120 + 250);
        Assert.True(second.Count < first.Count, "the wider column needs fewer lines for the same text");
    }

    [Fact]
    public void Each_column_is_followed_by_its_own_space()
    {
        var document = Document(new WordParagraph().AddText("Un").Add(new WordBreak(WordBreakKind.Column)).AddText("Deux").Add(new WordBreak(WordBreakKind.Column)).AddText("Trois"));
        document.Sections[0].Page = WordPageSetup.A4 with { Columns = 3, ColumnWidths = [100, 100, 100], ColumnSpacings = [10, 30, 0] };

        var page = Convert(document);

        Assert.Equal(Margin + 110, page.Letters.First(l => l.Value == "D").X, 0.5);
        Assert.Equal(Margin + 240, page.Letters.First(l => l.Value == "T").X, 0.5);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_line_is_drawn_between_columns_when_the_section_asks_for_it(bool separator)
    {
        var document = Document(new WordParagraph().AddText(Words).Add(new WordBreak(WordBreakKind.Column)).AddText(Words));
        document.Sections[0].Page = WordPageSetup.A4 with { Columns = 2, ColumnWidths = [100, 300], ColumnSpacings = [20, 0], ColumnSeparator = separator };

        var page = Convert(document);
        var image = PdfRenderer.Render(page, new PdfRenderOptions { Dpi = 72 }).Image;

        // The middle of the gap, 10 pt below the top of the columns.
        var (r, g, b, _) = image.GetRgba((int)Math.Round(Margin + 110), (int)Math.Round(Margin + 10));
        Assert.Equal(separator, r + g + b < 600);
    }

    [Fact]
    public void A_table_reaching_a_wider_column_is_laid_out_at_its_width()
    {
        // A table at full width (100 %) after a column break: in the 300 pt column its cell is 300 pt wide.
        var document = Document(new WordParagraph().AddText("Avant").Add(new WordBreak(WordBreakKind.Column)));
        var table = new WordTable(100) { Properties = new WordTableProperties { Width = WordWidth.Percent(100) } };
        table.AddRow(Words);
        document.AddTable(table);
        document.Sections[0].Page = WordPageSetup.A4 with { Columns = 2, ColumnWidths = [100, 300], ColumnSpacings = [20, 0] };

        var page = Convert(document);
        var lines = Column(page, Margin + 110, page.Width);

        Assert.All(lines, l => Assert.Equal(Margin + 120 + 5.4, l.Left, 0.5));
        Assert.Contains(lines, l => l.Right > Margin + 120 + 250);
    }

    [Fact]
    public void Unequal_columns_are_balanced_before_a_continuous_section_break()
    {
        // Thirty short paragraphs in a 150 pt and a 250 pt column, then a continuous section: the two columns end
        // within a line of each other and the next section starts below the longer one.
        var paragraphs = Enumerable.Range(1, 30).Select(n => new WordParagraph($"Ligne {n} {Words[..60]}")).ToArray();
        var document = Document(paragraphs);
        document.Sections[0].Page = WordPageSetup.A4 with { Columns = 2, ColumnWidths = [150, 250], ColumnSpacings = [20, 0] };
        document.AddSection(WordPageSetup.A4 with { Start = WordSectionStart.Continuous });
        document.AddParagraph("Suite");

        var page = Convert(document);
        var suite = Lines(page).Single(l => l.Text == "Suite");
        var first = Column(page, 0, Margin + 160).Where(l => l.Text != "Suite").Max(l => l.Baseline);
        var second = Column(page, Margin + 160, page.Width).Max(l => l.Baseline);

        Assert.True(Math.Abs(first - second) <= 12 + 0.5, $"columns end at {first} and {second}");
        Assert.True(suite.Baseline > Math.Max(first, second), "the next section starts below the columns");
        Assert.True(Math.Max(first, second) < 700, "the columns were balanced, not filled to the page bottom (771 pt)");
    }

    [Fact]
    public void A_tab_does_not_make_its_line_taller()
    {
        // As in a table of contents: entries in 9 pt with the tab before the page number in 11 pt keep the 9 pt line.
        var document = new WordDocument(language: null);
        document.Styles.DefaultRunProperties = new WordRunProperties { Font = "Liberation Sans", FontSize = 9 };
        document.Styles.DefaultParagraphProperties = new WordParagraphProperties { SpacingBefore = 0, SpacingAfter = 0 };
        foreach (var entry in new[] { "Un", "Deux", "Trois" })
        {
            document.Body.Add(new WordParagraph().AddText(entry).Add(new WordTab { Properties = new WordRunProperties { FontSize = 11 } }).AddText("12"));
        }

        document.Body.Add(new WordParagraph("Neuf points"));
        document.Body.Add(new WordParagraph("Dix"));
        var lines = Lines(Convert(document));

        var entries = lines[1].Baseline - lines[0].Baseline;
        var plain = lines[4].Baseline - lines[3].Baseline;
        Assert.Equal(plain, entries, 0.25);
    }

    [Fact]
    public void A_page_break_at_the_end_of_a_full_page_does_not_leave_a_blank_page()
    {
        // Fifty-eight 12 pt lines fill the page; the paragraph holding the page break stays at its end.
        var paragraphs = Enumerable.Range(1, 58).Select(n => new WordParagraph($"Ligne {n}")).ToList();
        paragraphs.Add(new WordParagraph().Add(new WordBreak(WordBreakKind.Page)));
        paragraphs.Add(new WordParagraph("Suite"));
        var result = WordToPdf.Convert(WordDocument.Load(Document([.. paragraphs]).ToArray()));

        Assert.Equal(2, result.PageCount);
        Assert.Contains("Suite", PdfDocument.Open(result.Pdf).GetPage(2).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_empty_paragraph_ending_a_full_page_before_a_new_page_section_does_not_leave_a_blank_page()
    {
        var paragraphs = Enumerable.Range(1, 58).Select(n => new WordParagraph($"Ligne {n}")).ToList();
        paragraphs.Add(new WordParagraph());
        var document = Document([.. paragraphs]);
        document.AddSection(WordPageSetup.A4);
        document.AddParagraph("Section deux");

        var result = WordToPdf.Convert(WordDocument.Load(document.ToArray()));

        Assert.Equal(2, result.PageCount);
        Assert.Contains("Section deux", PdfDocument.Open(result.Pdf).GetPage(2).Text, StringComparison.Ordinal);
    }

    // The lines of the letters between two x positions (one column), by baseline.
    private static List<TextLine> Column(PdfPage page, double from, double to) => page.Letters.Where(l => l.Value.Trim().Length > 0 && l.X >= from && l.X < to)
        .GroupBy(l => Math.Round(page.Height - l.Y, 1))
        .OrderBy(g => g.Key)
        .Select(g => new TextLine(g.Key, g.Min(l => l.X), g.Max(l => l.X + l.Width), string.Concat(g.OrderBy(l => l.X).Select(l => l.Value)), [.. g]))
        .ToList();
}
