// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Conversion.WordLayout;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Text;
using OmniEurope.Documents.Pdf.Writing;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// A justified line may take one more word than its natural width allows: Word 2013 and later (compatibility
/// mode 15) shrink the spaces of a justified line by up to a quarter of their width instead of pushing the last
/// word to the next line.
/// </summary>
public sealed class WordJustifyShrinkTests
{
    private static readonly LayoutContext Context = new(new WordDocument(), new PdfDocumentBuilder());
    private static readonly TextStyle Style = new(new PdfFont("Liberation Sans"), 10, PdfColor.Black);

    [Fact]
    public void A_justified_line_shrinks_its_spaces_to_take_a_word_that_overflows_by_less_than_a_quarter_of_them()
    {
        // Five words of 16.5 and four spaces of 5: 102.5 wide in a line of 100, 2.5 over out of the 5 the spaces may give.
        var lines = Break(WordAlignment.Justify, Words(16.5, 6));

        Assert.Equal(["a b c d e", "f"], Texts(lines));
        var last = lines[0].Items.Last(i => i.Token is TextToken { IsSpace: false });
        Assert.Equal(100, last.X + last.Width, 2);
        Assert.All(lines[0].Items.Where(i => i.Token is TextToken { IsSpace: true }).Take(4), i => Assert.Equal(4.375, i.Width, 3));
    }

    [Fact]
    public void A_justified_line_does_not_shrink_its_spaces_by_more_than_a_quarter()
    {
        // Five words of 18 and four spaces of 5: 110 wide, 10 over, and the spaces give 5 at most.
        var lines = Break(WordAlignment.Justify, Words(18, 6));

        Assert.Equal(["a b c d", "e f"], Texts(lines));
    }

    [Theory]
    [InlineData(WordAlignment.Left)]
    [InlineData(WordAlignment.Center)]
    [InlineData(WordAlignment.Right)]
    public void Only_justified_text_shrinks_its_spaces(WordAlignment alignment)
    {
        var lines = Break(alignment, Words(16.5, 6));

        Assert.Equal(["a b c d", "e f"], Texts(lines));
    }

    [Fact]
    public void Carlito_text_of_a_synthetic_paragraph_breaks_where_the_shrunk_line_fits()
    {
        // The line is 1.2 pt narrower than the natural width of its first six words, which the spaces can absorb.
        var face = FontLibrary.Default.Resolve("Calibri");
        string[] words = ["paiement", "du", "loyer", "et", "des", "charges", "pour", "la", "période", "de", "location"];
        var line = string.Join(' ', words[..6]);
        var width = face.MeasureText(line, 11) - 1.2;
        var document = new WordDocument();
        document.Sections[0].Page = WordPageSetup.A4 with { MarginLeft = 72, MarginRight = WordPageSetup.A4.Width - 72 - width };
        var run = new WordRunProperties { Font = "Calibri", FontSize = 11 };
        var paragraph = new WordParagraph { Properties = new WordParagraphProperties { Alignment = WordAlignment.Justify } };
        paragraph.Inlines.Add(new WordText(string.Join(' ', words), run));
        document.Body.Add(paragraph);

        var page = PdfDocument.Open(WordToPdf.Convert(document).Pdf).GetPage(1);

        var rows = Rows(page);
        Assert.Equal(line, rows[0]);
        Assert.Equal(string.Join(' ', words[6..]), rows[1]);
        var right = page.Letters.Where(l => l.Value.Trim().Length > 0 && Math.Abs(l.Y - page.Letters.Where(m => m.Value.Trim().Length > 0).Max(m => m.Y)) < 1).Max(l => l.X + l.Width);
        Assert.Equal(72 + width, right, 1);
    }

    [Fact]
    public void Every_line_of_the_rent_receipt_template_breaks_where_Word_365_breaks_it()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Word");
        var ours = PdfDocument.Open(WordToPdf.Convert(File.ReadAllBytes(Path.Combine(folder, "rent-receipt-template.docx"))).Pdf).GetPage(1);
        var word = PdfDocument.Open(File.ReadAllBytes(Path.Combine(folder, "rent-receipt-template.word365.pdf"))).GetPage(1);

        Assert.Equal(Rows(word), Rows(ours));
    }

    private static string[] Rows(PdfPage page) =>
        page.Letters.Where(l => l.FontSize > 5).GroupBy(l => Math.Round(l.Y)).OrderByDescending(g => g.Key)
            .Select(g => string.Concat(g.OrderBy(l => l.X).Select(l => l.Value)).Trim()).Where(t => t.Length > 0).ToArray();

    // Words "a", "b"... of the given width separated by spaces of 5.
    private static List<Token> Words(double width, int count)
    {
        var tokens = new List<Token>();
        for (var i = 0; i < count; i++)
        {
            tokens.Add(new TextToken(((char)('a' + i)).ToString(), Style) { Width = width, BreakBefore = true });
            if (i < count - 1)
            {
                tokens.Add(new TextToken(" ", Style) { Width = 5 });
            }
        }

        return tokens;
    }

    private static List<Line> Break(WordAlignment alignment, List<Token> tokens) =>
        new LineBreaker(Context, new LineGeometry(100, 0, 100, 0, [], 36, alignment, null, WordLineSpacingRule.Multiple, Style)).Break(tokens);

    private static string[] Texts(List<Line> lines) =>
        lines.Select(l => string.Concat(l.Items.Select(i => i.Token is TextToken t ? t.Text : string.Empty)).TrimEnd()).ToArray();
}
