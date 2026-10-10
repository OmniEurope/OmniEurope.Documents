// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Conversion.WordLayout;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Text;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// Text wrapping around floating shapes (ECMA-376 part 1, §20.4.2.3 anchor, §20.4.2.10 and .11 positions, §20.4.3
/// wrapping). Each document is written to a .docx package, read back and converted; the lines are read from the PDF
/// by baseline. The text is 10 pt Liberation Sans on exact 12 pt lines, so a line's baseline lies about 9.9 pt below
/// its top; the page is A4 with 70.9 pt margins (text from 70.9 to 524.4 pt).
/// </summary>
public sealed class WordFloatWrapTests
{
    private const double Margin = 70.9;
    private const double TextRight = 524.4;
    private const string Words = "lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor incididunt ut labore et dolore magna aliqua ut enim ad minim veniam quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur excepteur sint occaecat cupidatat non proident sunt in culpa qui officia deserunt mollit anim id est laborum";

    [Fact]
    public void Lines_beside_a_square_wrapped_picture_are_shortened_and_resume_full_width_below_it()
    {
        // A 100 x 60 picture at the left of the column, from the paragraph top, with 9 pt kept free on its right.
        var picture = Picture(100, 60, new WordFloatingPosition(0, "column", 0, "paragraph", WordWrap.Square) { DistanceRight = 9 });
        var page = Convert(Document(new WordParagraph().Add(picture).AddText(Words)));

        var lines = Lines(page);
        var beside = lines.Where(l => l.Baseline < Margin + 60).ToList();
        var below = lines.Where(l => l.Baseline > Margin + 60 + 1).ToList();

        // The lines whose top is above the picture's bottom (five 12 pt lines) start after the picture and its distance.
        Assert.Equal(5, beside.Count);
        Assert.All(beside, l => Assert.InRange(l.Left, Margin + 109 - 0.5, Margin + 109 + 0.5));
        Assert.All(beside, l => Assert.True(l.Right <= TextRight + 0.5, $"{l.Text} ends at {l.Right}"));
        Assert.NotEmpty(below);
        Assert.All(below, l => Assert.Equal(Margin, l.Left, 0.5));
        // The picture is drawn where it was placed.
        var image = Assert.Single(page.Images).Bounds;
        Assert.Equal(Margin, image.Left, 0.5);
        Assert.Equal(page.Height - Margin, image.Top, 0.5);
    }

    [Fact]
    public void A_picture_in_the_middle_leaves_text_on_both_sides()
    {
        // The picture spans 220.9 to 320.9 pt; with 9 pt on each side the text keeps out of 211.9 to 329.9 pt.
        var picture = Picture(100, 60, new WordFloatingPosition(150, "column", 0, "paragraph", WordWrap.Square) { DistanceLeft = 9, DistanceRight = 9 });
        var page = Convert(Document(new WordParagraph().Add(picture).AddText(Words)));

        var beside = Lines(page).Where(l => l.Baseline < Margin + 60).ToList();

        Assert.Equal(5, beside.Count);
        Assert.All(beside, l => Assert.DoesNotContain(l.Letters, c => c.X + c.Width > 211.9 + 0.5 && c.X < 329.9 - 0.5));
        Assert.All(beside, l => Assert.Contains(l.Letters, c => c.X < 211.9));
        Assert.All(beside, l => Assert.Contains(l.Letters, c => c.X > 329.9 - 0.5));
        // The left span is filled before the right one: the text reads on across the picture.
        var first = beside[0];
        var left = string.Concat(first.Letters.Where(c => c.X < 211.9).OrderBy(c => c.X).Select(c => c.Value));
        var right = string.Concat(first.Letters.Where(c => c.X > 329.9 - 0.5).OrderBy(c => c.X).Select(c => c.Value));
        Assert.StartsWith(left + right, Words.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(WordWrapSide.Left, 150.0, true, false)]
    [InlineData(WordWrapSide.Right, 150.0, false, true)]
    // Largest: the picture stands 50 pt from the left, the room is on its right.
    [InlineData(WordWrapSide.Largest, 50.0, false, true)]
    public void The_wrap_side_keeps_text_on_one_side(WordWrapSide side, double offset, bool leftText, bool rightText)
    {
        var picture = Picture(100, 60, new WordFloatingPosition(offset, "column", 0, "paragraph", WordWrap.Square) { DistanceLeft = 9, DistanceRight = 9, WrapSide = side });
        var page = Convert(Document(new WordParagraph().Add(picture).AddText(Words)));
        var (start, end) = (Margin + offset - 9, Margin + offset + 109);

        var beside = Lines(page).Where(l => l.Baseline < Margin + 60).ToList();

        Assert.NotEmpty(beside);
        Assert.All(beside, l => Assert.Equal(leftText, l.Letters.Exists(c => c.X + c.Width <= start + 0.5)));
        Assert.All(beside, l => Assert.Equal(rightText, l.Letters.Exists(c => c.X >= end - 0.5)));
    }

    [Fact]
    public void Text_resumes_below_a_top_and_bottom_shape()
    {
        // A 200 x 60 picture with 6 pt kept free below it, at the top of the second paragraph: that paragraph's text
        // starts below it, at the full width.
        var picture = Picture(200, 60, new WordFloatingPosition(100, "column", 0, "paragraph", WordWrap.TopAndBottom) { DistanceBottom = 6 });
        var document = Document(new WordParagraph().AddText("Avant"), new WordParagraph().Add(picture).AddText(Words));
        var page = Convert(document);

        var lines = Lines(page);
        var before = lines.Single(l => l.Text == "Avant");
        var after = lines.Where(l => l.Text != "Avant").ToList();

        // The second paragraph's top is one line down (82.9); its first line moves to the picture's bottom plus 6 pt.
        Assert.Equal(Margin + 9.9, before.Baseline, 0.5);
        Assert.Equal(Margin + 12 + 60 + 6 + 9.9, after[0].Baseline, 0.5);
        Assert.All(after, l => Assert.Equal(Margin, l.Left, 0.5));
    }

    [Fact]
    public void A_shape_without_wrapping_leaves_the_text_where_it_is()
    {
        var picture = Picture(100, 60, new WordFloatingPosition(0, "column", 0, "paragraph", WordWrap.None));
        var page = Convert(Document(new WordParagraph().Add(picture).AddText(Words)));

        var lines = Lines(page);

        Assert.All(lines, l => Assert.Equal(Margin, l.Left, 0.5));
        Assert.Equal(Margin + 9.9, lines[0].Baseline, 0.5);
    }

    [Theory]
    [InlineData("margin", "center", "margin", "top", Margin + ((TextRight - Margin - 100) / 2), Margin)]
    [InlineData("page", "right", "page", "bottom", 595.3 - 100, 841.9 - 60)]
    [InlineData("rightMargin", "left", "bottomMargin", "top", TextRight, 841.9 - Margin)]
    [InlineData("column", "right", "margin", "center", TextRight - 100, Margin + ((841.9 - (2 * Margin) - 60) / 2))]
    public void Alignments_place_a_shape_in_its_reference(string horizontal, string alignX, string vertical, string alignY, double left, double top)
    {
        var position = new WordFloatingPosition(0, horizontal, 0, vertical, WordWrap.None) { HorizontalAlignment = alignX, VerticalAlignment = alignY };
        var page = Convert(Document(new WordParagraph().Add(Picture(100, 60, position)).AddText("Texte")));

        var bounds = Assert.Single(page.Images).Bounds;

        Assert.Equal(left, bounds.Left, 0.5);
        Assert.Equal(page.Height - top, bounds.Top, 0.5);
    }

    [Fact]
    public void A_text_box_fitting_its_text_takes_the_room_its_text_needs()
    {
        // A text box stated 20 pt high holding five lines grows to 5 x 12 + 2 x 3.6 = 67.2 pt: the text below it starts
        // after that, not after 20 pt.
        var box = new WordTextBox(200, 20) { FitsText = true, Floating = new WordFloatingPosition(0, "column", 0, "paragraph", WordWrap.TopAndBottom) };
        foreach (var n in Enumerable.Range(1, 5))
        {
            box.Blocks.Add(new WordParagraph($"Ligne {n}"));
        }

        var page = Convert(Document(new WordParagraph().Add(box).AddText("Après la boîte")));

        var after = Lines(page).Single(l => l.Text.StartsWith("Aprèsla", StringComparison.Ordinal));
        Assert.Equal(Margin + 67.2 + 9.9, after.Baseline, 0.5);
    }

    [Fact]
    public void A_table_row_goes_below_a_wrapping_shape_it_would_overlap()
    {
        var picture = Picture(100, 60, new WordFloatingPosition(0, "column", 0, "paragraph", WordWrap.Square));
        var document = Document(new WordParagraph().Add(picture).AddText("Ancre"));
        var table = new WordTable(200);
        table.AddRow("Cellule");
        document.AddTable(table);

        var page = Convert(document);

        var cell = Lines(page).Single(l => l.Text == "Cellule");
        Assert.True(cell.Baseline > Margin + 60, $"the row starts at {cell.Baseline}, inside the picture");
    }

    [Fact]
    public void A_shape_positioned_from_its_paragraph_moves_below_a_wrapping_shape_it_would_overlap()
    {
        // The second picture, 40 pt above the top of its (second) paragraph, would overlap the first one: it goes below it.
        var first = Picture(200, 60, new WordFloatingPosition(0, "column", 0, "paragraph", WordWrap.TopAndBottom));
        var second = Picture(200, 40, new WordFloatingPosition(0, "column", -40, "paragraph", WordWrap.TopAndBottom));
        var page = Convert(Document(new WordParagraph().Add(first).AddText("Un"), new WordParagraph().Add(second).AddText("Deux")));

        var images = page.Images.Select(i => i.Bounds).OrderByDescending(b => b.Top).ToList();

        Assert.Equal(2, images.Count);
        Assert.True(images[1].Top <= images[0].Bottom + 0.5, $"second picture top {images[1].Top}, first bottom {images[0].Bottom}");
    }

    [Fact]
    public void A_shape_moves_to_the_next_page_with_its_paragraph()
    {
        // Fifty-eight lines fill the first page (700.1 pt of text room on 12 pt lines): the anchoring paragraph
        // and its picture land on the second page.
        var paragraphs = Enumerable.Range(1, 58).Select(n => new WordParagraph($"Ligne {n}")).ToList();
        var picture = Picture(100, 60, new WordFloatingPosition(0, "column", 0, "paragraph", WordWrap.Square) { DistanceRight = 9 });
        paragraphs.Add(new WordParagraph().Add(picture).AddText(Words));
        var pdf = PdfDocument.Open(WordToPdf.Convert(WordDocument.Load(Document([.. paragraphs]).ToArray())).Pdf);

        Assert.Empty(pdf.GetPage(1).Images);
        var image = Assert.Single(pdf.GetPage(2).Images).Bounds;
        Assert.Equal(pdf.GetPage(2).Height - Margin, image.Top, 0.5);
        Assert.All(Lines(pdf.GetPage(2)).Where(l => l.Baseline < Margin + 60), l => Assert.Equal(Margin + 109, l.Left, 0.5));
    }

    [Fact]
    public void A_line_with_no_room_on_a_page_covered_by_a_shape_runs_past_the_page_once_and_is_reported()
    {
        // A page-high picture positioned from the page covers the whole text area of every page its paragraph lands on.
        var picture = Picture(453.5, 700, new WordFloatingPosition(0, "margin", 70, "page", WordWrap.TopAndBottom));
        var result = WordToPdf.Convert(WordDocument.Load(Document(new WordParagraph().Add(picture).AddText("Texte"), new WordParagraph("Suite")).ToArray()));

        Assert.Contains("content taller than a page runs past its bottom", result.Gaps);
        Assert.Contains("Suite", string.Concat(Enumerable.Range(1, result.PageCount).Select(p => PdfDocument.Open(result.Pdf).GetPage(p).Text)), StringComparison.Ordinal);
    }

    [Fact]
    public void Wrapping_shapes_in_headers_and_tables_are_reported()
    {
        var document = Document(new WordParagraph("Corps"));
        var header = new WordHeaderFooter();
        header.Blocks.Add(new WordParagraph().Add(Picture(50, 20, new WordFloatingPosition(0, "column", 0, "paragraph", WordWrap.Square))));
        document.Sections[0].Headers[WordHeaderFooterKind.Default] = header;
        var table = new WordTable(200);
        table.AddRow("x").Cells[0].Blocks.OfType<WordParagraph>().First().Add(Picture(20, 20, new WordFloatingPosition(0, "column", 0, "paragraph", WordWrap.Square)));
        document.AddTable(table);

        var gaps = WordToPdf.Convert(document).Gaps;

        Assert.Contains("text does not flow around floating shapes in tables, headers and footers", gaps);
    }

    [Theory]
    [InlineData("page", null, 10.0, 10.0)]
    [InlineData("margin", null, 10.0, 60.0)]
    [InlineData("column", null, 10.0, 210.0)]
    [InlineData("character", null, 10.0, 260.0)]
    [InlineData("leftMargin", "center", 0.0, 15.0)]
    [InlineData("rightMargin", "right", 0.0, 480.0)]
    [InlineData("insideMargin", "inside", 0.0, 0.0)]
    [InlineData("outsideMargin", "outside", 0.0, 480.0)]
    [InlineData("insideMargin", "outside", 0.0, 30.0)]
    [InlineData("outsideMargin", "inside", 0.0, 450.0)]
    internal void Horizontal_references_and_alignments_follow_the_specification(string relativeTo, string? alignment, double offset, double expected)
    {
        // Page 500 wide, margins 50 to 450, column 200 to 300, anchor character at 250; a 20 pt wide shape on an odd page.
        var frames = new FloatFrames(500, 800, (50, 60, 400, 680), (200, 100), 250, 300, 320, OddPage: true);
        var position = new WordFloatingPosition(offset, relativeTo) { HorizontalAlignment = alignment };

        Assert.Equal(expected, FloatGeometry.Place(position, 20, 10, frames).X, 3);
    }

    [Theory]
    [InlineData("page", null, 5.0, 5.0)]
    [InlineData("margin", "bottom", 0.0, 730.0)]
    [InlineData("paragraph", null, 5.0, 305.0)]
    [InlineData("line", null, 5.0, 325.0)]
    [InlineData("topMargin", "center", 0.0, 25.0)]
    [InlineData("bottomMargin", "top", 0.0, 740.0)]
    [InlineData("insideMargin", "top", 0.0, 0.0)]
    [InlineData("outsideMargin", "bottom", 0.0, 790.0)]
    [InlineData("page", "inside", 0.0, 0.0)]
    [InlineData("page", "outside", 0.0, 790.0)]
    internal void Vertical_references_and_alignments_follow_the_specification(string relativeTo, string? alignment, double offset, double expected)
    {
        // Page 800 high, margins 60 to 740, paragraph at 300, line at 320; a 10 pt high shape on an odd page.
        var frames = new FloatFrames(500, 800, (50, 60, 400, 680), (200, 100), 250, 300, 320, OddPage: true);
        var position = new WordFloatingPosition(0, "page", offset, relativeTo) { VerticalAlignment = alignment };

        Assert.Equal(expected, FloatGeometry.Place(position, 20, 10, frames).Y, 3);
    }

    [Fact]
    public void Inside_and_outside_swap_on_even_pages()
    {
        var even = new FloatFrames(500, 800, (50, 60, 400, 680), (200, 100), 250, 300, 320, OddPage: false);

        Assert.Equal(450, FloatGeometry.Place(new WordFloatingPosition(0, "insideMargin"), 20, 10, even).X, 3);
        Assert.Equal(0, FloatGeometry.Place(new WordFloatingPosition(0, "outsideMargin"), 20, 10, even).X, 3);
        Assert.Equal(740, FloatGeometry.Place(new WordFloatingPosition(0, "page", 0, "insideMargin"), 20, 10, even).Y, 3);
        Assert.Equal(480, FloatGeometry.Place(new WordFloatingPosition(0, "page") { HorizontalAlignment = "inside" }, 20, 10, even).X, 3);
        Assert.Equal(0, FloatGeometry.Place(new WordFloatingPosition(0, "page") { HorizontalAlignment = "outside" }, 20, 10, even).X, 3);
    }

    internal static WordDocument Document(params WordParagraph[] paragraphs)
    {
        var document = new WordDocument(language: null);
        document.Styles.DefaultRunProperties = new WordRunProperties { Font = "Liberation Sans", FontComplex = "Liberation Sans", FontSize = 10, FontSizeComplex = 10 };
        document.Styles.DefaultParagraphProperties = new WordParagraphProperties { SpacingBefore = 0, SpacingAfter = 0, LineSpacing = 12, LineSpacingRule = WordLineSpacingRule.Exact };
        document.Body.AddRange(paragraphs);
        return document;
    }

    internal static PdfPage Convert(WordDocument document) =>
        PdfDocument.Open(WordToPdf.Convert(WordDocument.Load(document.ToArray())).Pdf).GetPage(1);

    /// <summary>The lines of a page by baseline (distance from the page top), with their left and right ends.</summary>
    internal static List<TextLine> Lines(PdfPage page) => page.Letters.Where(l => l.Value.Trim().Length > 0)
        .GroupBy(l => Math.Round(page.Height - l.Y, 1))
        .OrderBy(g => g.Key)
        .Select(g => new TextLine(g.Key, g.Min(l => l.X), g.Max(l => l.X + l.Width), string.Concat(g.OrderBy(l => l.X).Select(l => l.Value)), [.. g]))
        .ToList();

    private static WordPicture Picture(double width, double height, WordFloatingPosition position) =>
        new(WordImage.FromBytes(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "rgb.png"))), width, height) { Floating = position };

    internal sealed record TextLine(double Baseline, double Left, double Right, string Text, List<PdfLetter> Letters);
}
