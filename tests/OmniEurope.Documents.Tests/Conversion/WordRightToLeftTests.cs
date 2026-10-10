// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Text;
using OmniEurope.Documents.Word;
using static OmniEurope.Documents.Tests.Conversion.WordFloatWrapTests;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// Right-to-left paragraphs (<c>w:bidi</c>, ECMA-376 part 1 §17.3.1.6), right-to-left runs (<c>w:rtl</c>, §17.3.2.30)
/// and mixed text ordered by the Unicode Bidirectional Algorithm, read back from the PDF: the drawing order of the
/// letters is their order by x. Hebrew letters stand for right-to-left text (the bundled Liberation fonts draw them).
/// The text column runs from 70.9 to 524.4 pt.
/// </summary>
public sealed class WordRightToLeftTests
{
    private const double Margin = 70.9;
    private const double TextRight = 524.4;
    private const string Alef = "א";
    private const string Bet = "ב";
    private const string Gimel = "ג";
    private const string Dalet = "ד";
    private const string He = "ה";
    private const string Vav = "ו";

    [Fact]
    public void A_right_to_left_paragraph_starts_at_the_right_margin_and_reads_leftwards()
    {
        var page = Convert(Document(Rtl(Alef + Bet + Gimel)));

        // The first letter in reading order is the rightmost, against the right margin.
        Assert.Equal(TextRight, Right(page, Alef), 0.5);
        Assert.True(X(page, Alef) > X(page, Bet) && X(page, Bet) > X(page, Gimel));
    }

    [Fact]
    public void Mixed_text_keeps_latin_words_and_numbers_left_to_right_inside_a_right_to_left_line()
    {
        // UAX #9: the Latin word and the number after it are an L run at level 2 (W7 makes the number L after Latin
        // text), the Hebrew words R runs at level 1; drawn from the left: the second Hebrew word reversed, "abc 123",
        // the first Hebrew word reversed.
        var page = Convert(Document(Rtl(Alef + Bet + Gimel + " abc 123 " + Dalet + He + Vav)));

        string[] leftToRight = [Vav, He, Dalet, "a", "b", "c", "1", "2", "3", Gimel, Bet, Alef];
        var xs = leftToRight.Select(v => X(page, v)).ToList();
        Assert.Equal(xs.Order().ToList(), xs);
        Assert.Equal(TextRight, Right(page, Alef), 0.5);
    }

    [Fact]
    public void Numbers_after_hebrew_stay_left_to_right_and_follow_it_leftwards()
    {
        // "AB 123" in a right-to-left paragraph: the number (EN after R, W7 does not apply) is at level 2, left of the
        // Hebrew; its digits read left to right.
        var page = Convert(Document(Rtl(Alef + Bet + " 123")));

        string[] leftToRight = ["1", "2", "3", Bet, Alef];
        var xs = leftToRight.Select(v => X(page, v)).ToList();
        Assert.Equal(xs.Order().ToList(), xs);
    }

    [Fact]
    public void A_hebrew_word_in_left_to_right_text_is_reversed_in_place()
    {
        var page = Convert(Document(new WordParagraph("abc " + Alef + Bet + Gimel + " def")));

        string[] leftToRight = ["a", "b", "c", Gimel, Bet, Alef, "d", "e", "f"];
        var xs = leftToRight.Select(v => X(page, v)).ToList();
        Assert.Equal(xs.Order().ToList(), xs);
        Assert.Equal(Margin, X(page, "a"), 0.5);
    }

    [Fact]
    public void Brackets_resolved_right_to_left_are_drawn_mirrored()
    {
        // In "ABC (abc)" right to left, the parentheses take the paragraph direction (N0) and are reversed with the line:
        // the closing one, drawn mirrored, stands left of "abc" as an opening one.
        var page = Convert(Document(Rtl(Alef + Bet + Gimel + " (abc)")));

        var letters = page.Letters.Where(l => l.Value.Trim().Length > 0).OrderBy(l => l.X).Select(l => l.Value).ToList();
        Assert.Equal(["(", "a", "b", "c", ")", Gimel, Bet, Alef], letters);
    }

    [Theory]
    // Start (the model's Left) is the right edge of a right-to-left paragraph, end its left edge.
    [InlineData(WordAlignment.Left, TextRight)]
    [InlineData(WordAlignment.Right, Margin + 20)]
    public void Alignment_is_taken_from_the_start_edge(WordAlignment alignment, double edge)
    {
        var paragraph = Rtl(Alef + Bet);
        paragraph.Properties = paragraph.Properties with { Alignment = alignment };
        var page = Convert(Document(paragraph));

        // With the end alignment the word's left end is on the left margin; 20 pt is a generous bound for two letters.
        if (alignment == WordAlignment.Left)
        {
            Assert.Equal(edge, Right(page, Alef), 0.5);
        }
        else
        {
            Assert.Equal(Margin, X(page, Bet), 0.5);
            Assert.True(Right(page, Alef) < edge);
        }
    }

    [Fact]
    public void The_start_indent_of_a_right_to_left_paragraph_is_on_the_right()
    {
        var paragraph = Rtl(Alef + Bet);
        paragraph.Properties = paragraph.Properties with { IndentLeft = 36 };
        var page = Convert(Document(paragraph));

        Assert.Equal(TextRight - 36, Right(page, Alef), 0.5);
    }

    [Fact]
    public void Every_line_of_a_long_right_to_left_paragraph_starts_on_the_right_in_reading_order()
    {
        var words = Enumerable.Range(0, 80).Select(i => (i % 2 == 0 ? Alef + Bet + Gimel : Dalet + He + Vav) + (i % 10).ToString(System.Globalization.CultureInfo.InvariantCulture));
        var page = Convert(Document(Rtl(string.Join(' ', words))));

        var lines = Lines(page);

        Assert.True(lines.Count >= 3, "the text takes several lines");
        Assert.All(lines, l => Assert.Equal(TextRight, l.Right, 0.5));
        // The first word of the paragraph (alef bet gimel 0) is the rightmost word of the first line.
        var first = lines[0].Letters.OrderByDescending(l => l.X).Take(3).Select(l => l.Value);
        Assert.Equal([Alef, Bet, Gimel], first);
    }

    [Fact]
    public void A_right_to_left_run_uses_the_complex_script_size_and_hebrew_text_its_run_complex_size()
    {
        // w:rtl: the run is drawn with its complex script size (20 pt) even for its Latin letters; in a plain run, only
        // the Hebrew letters take the complex script size (16 pt), the Latin ones keep 10 pt.
        var rtl = new WordParagraph().AddText("ab", new WordRunProperties { RightToLeft = true, FontSizeComplex = 20 });
        var mixed = new WordParagraph().AddText("cd " + Alef + Bet, new WordRunProperties { FontSizeComplex = 16 });
        var page = Convert(Document(rtl, mixed));

        Assert.Equal(20, Letter(page, "a").FontSize, 1);
        Assert.Equal(10, Letter(page, "c").FontSize, 1);
        Assert.Equal(16, Letter(page, Alef).FontSize, 1);
    }

    [Fact]
    public void Right_to_left_paragraphs_are_justified_from_the_right()
    {
        var words = string.Join(' ', Enumerable.Range(0, 60).Select(i => i % 2 == 0 ? Alef + Bet + Gimel : Dalet + He));
        var paragraph = Rtl(words);
        paragraph.Properties = paragraph.Properties with { Alignment = WordAlignment.Justify };
        var page = Convert(Document(paragraph));

        var lines = Lines(page);

        // Every line but the last fills the column; the last one stays on the start (right) edge.
        Assert.All(lines.SkipLast(1), l => Assert.Equal(Margin, l.Left, 0.5));
        Assert.All(lines, l => Assert.Equal(TextRight, l.Right, 0.5));
        Assert.True(lines[^1].Left > Margin + 1);
    }

    [Fact]
    public void Arabic_text_is_reported_as_drawn_without_joining_forms()
    {
        var gaps = WordToPdf.Convert(Document(new WordParagraph("بت 12"))).Gaps;

        Assert.Contains("Arabic letters drawn without joining forms", gaps);
    }

    private static WordParagraph Rtl(string text) => new WordParagraph { Properties = new WordParagraphProperties { RightToLeft = true } }.AddText(text);

    private static PdfLetter Letter(PdfPage page, string value) => page.Letters.First(l => l.Value == value);

    private static double X(PdfPage page, string value) => Letter(page, value).X;

    private static double Right(PdfPage page, string value) => Letter(page, value).X + Letter(page, value).Width;
}
