// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Layout;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>Words and lines across font size and baseline changes: superscripts, subscripts, small capitals.</summary>
public sealed class PdfLayoutWordTests
{
    private const double PageHeight = 841.89;

    [Fact]
    public void A_raised_footnote_reference_is_its_own_word_on_the_line_of_its_text()
    {
        var page = Page(canvas =>
        {
            var x = 72 + canvas.DrawText("Whereas the water must be assessed", 72, 100, PdfFont.Serif, 12);
            x += canvas.DrawText("1", x, 100 - 4.6, PdfFont.Serif, 8);
            canvas.DrawText(". The next sentence follows.", x, 100, PdfFont.Serif, 12);
        });

        var words = PdfLayoutAnalyzer.Words(page).Select(w => w.Text).ToList();
        var layout = PdfLayoutAnalyzer.AnalyzePage(page);

        Assert.Equal(["assessed", "1", "."], words.Skip(5).Take(3));
        Assert.DoesNotContain("assessed1", words);
        var line = Assert.Single(Assert.Single(layout.Blocks).Lines);
        Assert.Equal("Whereas the water must be assessed 1 . The next sentence follows.", line.Text);
        Assert.Equal(PageHeight - 100, line.Baseline, 1);
        Assert.Equal(12, line.FontSize);
        Assert.Equal("Whereas the water must be assessed 1 . The next sentence follows.", page.Text);
    }

    [Fact]
    public void A_footnote_number_raised_at_the_start_of_a_line_does_not_move_its_baseline()
    {
        var page = Page(canvas =>
        {
            var x = 72 + canvas.DrawText("2", 72, 100 - 4, PdfFont.Serif, 8);
            canvas.DrawText(" Directive of the Council.", x, 100, PdfFont.Serif, 12);
            canvas.DrawText("Second line of the note.", 72, 114, PdfFont.Serif, 12);
        });

        var layout = PdfLayoutAnalyzer.AnalyzePage(page);

        var block = Assert.Single(layout.Blocks);
        Assert.Equal(["2 Directive of the Council.", "Second line of the note."], block.Lines.Select(l => l.Text));
        Assert.Equal(PageHeight - 100, block.Lines[0].Baseline, 1);
    }

    [Fact]
    public void A_lowered_subscript_is_its_own_word_on_the_line()
    {
        var page = Page(canvas =>
        {
            var x = 72 + canvas.DrawText("Water is H", 72, 100, PdfFont.Sans, 12);
            x += canvas.DrawText("2", x, 100 + 2.5, PdfFont.Sans, 8);
            canvas.DrawText("O and salt.", x, 100, PdfFont.Sans, 12);
        });

        var lines = PdfLayoutAnalyzer.AnalyzePage(page).Blocks.SelectMany(b => b.Lines).ToList();

        var line = Assert.Single(lines);
        Assert.Equal(["Water", "is", "H", "2", "O", "and", "salt."], line.Words.Select(w => w.Text));
        Assert.Equal("Water is H 2 O and salt.", page.Text);
    }

    [Fact]
    public void Small_capitals_stay_one_word()
    {
        var page = Page(canvas =>
        {
            var x = 72 + canvas.DrawText("C", 72, 100, PdfFont.Sans.AsBold(), 12);
            x += canvas.DrawText("OV", x, 100, PdfFont.Sans.AsBold(), 9.6);
            canvas.DrawText(" DESCRIPTION", x, 100, PdfFont.Sans.AsBold(), 9.6);
        });

        Assert.Equal(["COV", "DESCRIPTION"], PdfLayoutAnalyzer.Words(page).Select(w => w.Text));
        Assert.Equal("COV DESCRIPTION", page.Text);
    }

    [Fact]
    public void Small_capitals_stay_one_word_beside_a_column_on_a_slightly_higher_baseline()
    {
        var page = Page(canvas =>
        {
            canvas.DrawText("Left column text", 72, 100, PdfFont.Sans, 11.5);
            var x = 300 + canvas.DrawText("C", 300, 100 + 3.7, PdfFont.Sans.AsBold(), 12);
            x += canvas.DrawText("ONTEXT", x, 100 + 3.7, PdfFont.Sans.AsBold(), 9.5);
            canvas.DrawText(": body", x, 100 + 3.7, PdfFont.Sans, 11);
        });

        var words = PdfLayoutAnalyzer.Words(page).Select(w => w.Text).ToList();

        Assert.Contains("CONTEXT:", words);
        Assert.DoesNotContain("C", words);
        Assert.Contains("Left column text", PdfLayoutAnalyzer.AnalyzePage(page).Blocks.Select(b => b.Text));
    }

    [Fact]
    public void Words_of_different_sizes_separated_by_a_space_stay_two_words()
    {
        var page = Page(canvas =>
        {
            var x = 72 + canvas.DrawText("Big ", 72, 100, PdfFont.Sans, 20);
            canvas.DrawText("small", x, 100, PdfFont.Sans, 9);
            var y = 72 + canvas.DrawText("Large", 72, 140, PdfFont.Sans, 20);
            canvas.DrawText("tiny", y + canvas.MeasureText(" ", PdfFont.Sans, 9), 140, PdfFont.Sans, 9);
        });

        Assert.Equal(["Big", "small", "Large", "tiny"], PdfLayoutAnalyzer.Words(page).Select(w => w.Text));
    }

    [Fact]
    public void A_raised_run_far_from_the_text_is_a_line_of_its_own()
    {
        var page = Page(canvas =>
        {
            canvas.DrawText("Body text of the cell", 72, 100, PdfFont.Sans, 12);
            canvas.DrawText("note", 72 + canvas.MeasureText("Body text of the cell", PdfFont.Sans, 12) + 8, 100 - 4.5, PdfFont.Sans, 8);
        });

        var lines = PdfLayoutAnalyzer.Lines(page.Letters);

        Assert.Equal(["note", "Body text of the cell"], lines.Select(l => l.Text));
    }

    [Fact]
    public void Lines_printed_over_each_other_stay_apart()
    {
        var page = Page(canvas =>
        {
            canvas.DrawText("<documentRef href=first>", 72, 100, PdfFont.Mono, 5);
            canvas.DrawText("<attachment second>", 74, 101.2, PdfFont.Mono, 5);
        });

        var lines = PdfLayoutAnalyzer.Lines(page.Letters).Select(l => l.Text).ToList();

        Assert.Equal(["<documentRef href=first>", "<attachment second>"], lines);
        Assert.Equal("<documentRef href=first>\n<attachment second>", page.Text);
    }

    [Fact]
    public void A_stray_blank_on_its_own_baseline_does_not_split_a_word()
    {
        var page = Page(canvas =>
        {
            canvas.DrawText("Or the statement", 72, 100, PdfFont.Mono, 8);
            canvas.DrawText(" ", 72 + 3, 100 - 3.4, PdfFont.Sans, 11);
        });

        Assert.Equal(["Or", "the", "statement"], PdfLayoutAnalyzer.Words(page).Select(w => w.Text));
    }

    [Fact]
    public void A_glyph_raised_by_a_tenth_of_its_size_stays_in_its_word()
    {
        var page = Page(canvas =>
        {
            var x = 72 + canvas.DrawText("art", 72, 100, PdfFont.Sans, 7);
            x += canvas.DrawText("_", x, 100 - 0.7, PdfFont.Sans, 7);
            canvas.DrawText("1.par", x, 100, PdfFont.Sans, 7);
        });

        Assert.Equal(["art_1.par"], PdfLayoutAnalyzer.Words(page).Select(w => w.Text));
    }

    [Fact]
    public void Punctuation_drawn_higher_does_not_set_the_line_of_the_text_below_it()
    {
        var page = Page(canvas =>
        {
            canvas.DrawText("....", 300, 100 - 0.7, PdfFont.Sans, 7);
            canvas.DrawText("left words", 72, 100, PdfFont.Sans, 7);
            canvas.DrawText("right words", 200, 100 + 1.6, PdfFont.Sans, 6.3);
        });

        var lines = PdfLayoutAnalyzer.Lines(page.Letters).Select(l => l.Text).ToList();

        Assert.Equal(["left words", "right words", "...."], lines);
    }

    [Fact]
    public void A_line_of_punctuation_alone_is_a_line()
    {
        var page = Page(canvas =>
        {
            canvas.DrawText("....", 72, 100, PdfFont.Sans, 7);
            canvas.DrawText("----", 72, 120, PdfFont.Sans, 7);
        });

        Assert.Equal(["....", "----"], PdfLayoutAnalyzer.Lines(page.Letters).Select(l => l.Text));
    }

    [Fact]
    public void Superscripts_keep_the_reading_order_of_two_columns()
    {
        var page = Page(canvas =>
        {
            for (var i = 0; i < 6; i++)
            {
                var baseline = 100 + (i * 14);
                var x = 72 + canvas.DrawText($"left line {i}", 72, baseline, PdfFont.Serif, 11);
                canvas.DrawText($"{i + 1}", x, baseline - 4, PdfFont.Serif, 7);
                canvas.DrawText($"right line {i}", 320, baseline, PdfFont.Serif, 11);
            }
        });

        var layout = PdfLayoutAnalyzer.AnalyzePage(page);

        Assert.Equal(2, layout.Blocks.Count);
        Assert.Equal("left line 0 1\nleft line 1 2\nleft line 2 3\nleft line 3 4\nleft line 4 5\nleft line 5 6", layout.Blocks[0].Text);
        Assert.StartsWith("right line 0", layout.Blocks[1].Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.3, false)]
    [InlineData(0.3, true)]
    [InlineData(0.6, false)]
    [InlineData(0.22, true)]
    public void Letter_spaced_text_forms_its_words_against_its_own_spacing(double tracking, bool spaceGlyphs)
    {
        const string head = "Official Journal of the European Union";
        var page = Page(canvas => Spaced(canvas, head, 72, 60, 9, tracking * 9, spaceGlyphs));

        Assert.Equal(head.Split(' '), PdfLayoutAnalyzer.Words(page).Select(w => w.Text));
        Assert.Equal(head, Assert.Single(PdfLayoutAnalyzer.Lines(page.Letters)).Text);
        Assert.Equal(head, page.Text);
    }

    [Fact]
    public void A_spaced_out_title_is_one_word_and_body_text_beside_it_is_unchanged()
    {
        var page = Page(canvas =>
        {
            Spaced(canvas, "REGULATION", 72, 60, 14, 5, spaceGlyphs: false);
            Spaced(canvas, "to be or not", 72, 100, 11, 0, spaceGlyphs: false);
            canvas.DrawText("a b c d e", 72, 130, PdfFont.Sans, 11);
        });

        Assert.Equal(["REGULATION", "to", "be", "or", "not", "a", "b", "c", "d", "e"], PdfLayoutAnalyzer.Words(page).Select(w => w.Text));
        Assert.Equal("REGULATION\nto be or not\na b c d e", page.Text);
    }

    [Fact]
    public void Character_spacing_keeps_words_whole()
    {
        var page = Page(canvas => canvas.DrawText("Spaced heading of the page", 72, 60, PdfFont.Sans, 10, characterSpacing: 3));

        Assert.Equal(["Spaced", "heading", "of", "the", "page"], PdfLayoutAnalyzer.Words(page).Select(w => w.Text));
        Assert.Equal("Spaced heading of the page", page.Text);
    }

    [Fact]
    public void A_row_of_spaces_printed_under_the_text_does_not_split_its_words()
    {
        var page = Page(canvas =>
        {
            canvas.DrawText("L 12/2", 40, 60, PdfFont.Sans, 9);
            canvas.DrawText(new string(' ', 120), 72, 60 + 0.17, PdfFont.Sans, 9);
            canvas.DrawText("EN", 110, 60 - 0.5, PdfFont.Sans, 8.5);
            canvas.DrawText("Official Journal of the Union", 200, 60, PdfFont.Sans, 9);
        });

        Assert.Equal(["L", "12/2", "EN", "Official", "Journal", "of", "the", "Union"], PdfLayoutAnalyzer.Words(page).Select(w => w.Text));
        Assert.Equal(["L", "12/2", "EN", "Official", "Journal", "of", "the", "Union"], page.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void A_space_whose_end_lies_under_the_next_cell_still_ends_its_word()
    {
        var page = Page(canvas =>
        {
            // Table cells: each one starts before the advance of the space ending the cell before it is over.
            var space = canvas.MeasureText(" ", PdfFont.Serif, 12);
            var sites = canvas.DrawText("Sites ", 72, 60, PdfFont.Serif, 12);
            canvas.DrawText("Ports", 72 + sites - (space * 0.7), 60, PdfFont.Serif, 12);
            var period = canvas.DrawText("period ", 72, 90, PdfFont.Serif, 12);
            canvas.DrawText("4", 72 + period - (space * 0.6), 90, PdfFont.Serif, 12);
        });

        Assert.Equal(["Sites", "Ports", "period", "4"], PdfLayoutAnalyzer.Words(page).Select(w => w.Text));
        Assert.Equal("Sites Ports\nperiod 4", page.Text);
    }

    // Draws text letter by letter, each letter followed by its advance and the tracking; spaces are drawn as glyphs
    // or only skipped.
    private static void Spaced(PdfCanvas canvas, string text, double x, double baseline, double size, double tracking, bool spaceGlyphs)
    {
        foreach (var letter in text.Select(c => c.ToString()))
        {
            if (letter != " " || spaceGlyphs)
            {
                canvas.DrawText(letter, x, baseline, PdfFont.Sans, size);
            }

            x += canvas.MeasureText(letter, PdfFont.Sans, size) + tracking;
        }
    }

    private static PdfPage Page(Action<PdfCanvas> draw)
    {
        var builder = new PdfDocumentBuilder();
        draw(builder.AddPage());
        return PdfDocument.Open(builder.ToArray()).GetPage(1);
    }
}
