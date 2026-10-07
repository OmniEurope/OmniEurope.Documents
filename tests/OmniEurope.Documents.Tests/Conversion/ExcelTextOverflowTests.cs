// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Pdf.Text;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// Cells printed as Excel shows them. Text that does not wrap stays on one line, runs on into empty
/// neighbouring cells (right of left-aligned text, left of right-aligned text, both sides of centred text, over
/// fills and borders) and is clipped at the first cell holding a value or a formula; past the edge of the
/// printed range it wraps instead. Numbers never wrap: a column of width w holds w digits of the default font
/// (7 pixels each, plus 5 pixels of padding), beyond which a number is shortened or filled with '#'. Each letter
/// of the PDF is checked in the rendering: drawn where the text may go, absent past the clip.
/// </summary>
public sealed class ExcelTextOverflowTests
{
    private const string Long = "Libellé beaucoup plus long que sa colonne";

    // Drawn wider than two default columns, narrower than three.
    private const string Fits = "Libellé plus long que sa colonne";

    private const string Wrapped = "Texte renvoyé à la ligne dans sa cellule";

    // A default column: 8.43 characters of 7 pixels plus 5 pixels of padding, at 0.75 point per pixel.
    private const double Column = ((8.43 * 7) + 5) * 0.75;

    [Fact]
    public void Left_aligned_text_runs_on_to_the_right_until_a_value()
    {
        var sheet = Sheet();
        sheet.Cell("A1").Value = Fits;
        sheet.Cell("E1").Value = "fin";

        var row = Row(sheet, 0);

        Assert.All(row.Cells, c => Assert.Null(c.Properties.GridSpan));
        var overflow = Assert.IsType<WordCellOverflow>(row.Cells[0].Properties.Overflow);
        Assert.Equal(0, overflow.ExtendLeft);
        Assert.Equal(2 * Column, overflow.ExtendRight, 4);

        // No grid line under the text: A loses its right side, B both, C its left one; D and E keep theirs.
        Assert.True(row.Cells[0].Properties.Borders!.Right!.IsNone);
        Assert.True(row.Cells[1].Properties.Borders!.Left!.IsNone && row.Cells[1].Properties.Borders!.Right!.IsNone);
        Assert.True(row.Cells[2].Properties.Borders!.Left!.IsNone);
        Assert.Null(row.Cells[2].Properties.Borders!.Right);
        Assert.All(row.Cells.Skip(3), c => Assert.Null(c.Properties.Borders));

        var page = Pdf(sheet, out var image);
        Assert.StartsWith(Fits + " fin", page.Text, StringComparison.Ordinal);
        Assert.All(Letters(page, Fits), l => Assert.True(Drawn(page, image, l), $"'{l.Value}' at {l.X:N1} is missing"));
    }

    [Fact]
    public void Text_meeting_a_value_is_clipped_at_its_own_cell()
    {
        var sheet = Sheet();
        sheet.Cell("A1").Value = Long;
        sheet.Cell("B1").Value = 7;

        var row = Row(sheet, 0);

        var overflow = Assert.IsType<WordCellOverflow>(row.Cells[0].Properties.Overflow);
        Assert.Equal((WordAlignment.Left, 0, 0), (overflow.Alignment, overflow.ExtendLeft, overflow.ExtendRight));
        var page = Pdf(sheet, out var image);
        AssertClippedAt(page, image, Long, CellRight(page, "7") - Column, "7");
    }

    [Fact]
    public void A_formula_without_value_stops_the_text_but_a_fill_and_a_border_do_not()
    {
        var sheet = Sheet();
        sheet.Cell("A1").Value = Fits;
        sheet.Cell("B1").Style = XlsxStyle.Default with { FillColor = "FFFF00" };
        sheet.Cell("C1").Style = XlsxStyle.Default with { Border = true };
        sheet.Cell("A2").Value = Long;
        sheet.Cell("B2").Formula = "C9*2";
        sheet.Cell("C2").Value = 5;

        var filled = Row(sheet, 0);
        Assert.Equal(2 * Column, filled.Cells[0].Properties.Overflow!.ExtendRight, 4);
        Assert.Equal("FFFF00", filled.Cells[1].Properties.Shading);
        Assert.Equal(0, Row(sheet, 1).Cells[0].Properties.Overflow!.ExtendRight);

        var page = Pdf(sheet, out var image);
        Assert.All(Letters(page, Fits), l => Assert.True(Drawn(page, image, l), $"'{l.Value}' at {l.X:N1} is missing over the fill"));
        Assert.True(Count(image, (255, 255, 0)) > 100);

        // Row 2: B2 holds a formula, so the text stops at the edge of A2 (one column left of C2's number).
        AssertClippedAt(page, image, Long, CellRight(page, "5") - (2 * Column), "5");
    }

    [Fact]
    public void Right_aligned_text_runs_on_to_the_left_and_centred_text_both_ways()
    {
        var sheet = Sheet();
        sheet.Cell("D1").Value = Long;
        sheet.Cell("D1").Style = XlsxStyle.Default with { HorizontalAlignment = XlsxHorizontalAlignment.Right };
        sheet.Cell("C2").Value = "Titre centré assez long";
        sheet.Cell("C2").Style = XlsxStyle.Default with { HorizontalAlignment = XlsxHorizontalAlignment.Center };

        var right = Row(sheet, 0).Cells[3].Properties.Overflow!;
        var centred = Row(sheet, 1).Cells[2].Properties.Overflow!;

        Assert.Equal(3 * Column, right.ExtendLeft, 4);
        Assert.Equal(0, right.ExtendRight);
        Assert.Equal(Column, centred.ExtendLeft, 4);
        Assert.Equal(Column, centred.ExtendRight, 4);
        var page = Pdf(sheet, out var image);
        Assert.All(Letters(page, Long).Concat(Letters(page, "Titre centré assez long")), l => Assert.True(Drawn(page, image, l), $"'{l.Value}' is missing"));
    }

    [Fact]
    public void Text_reaching_the_edge_of_the_printed_range_wraps_instead_of_being_cut()
    {
        var sheet = Sheet();
        sheet.Cell("E1").Value = Long;
        sheet.Cell("A2").Value = Wrapped;
        sheet.Cell("A2").Style = XlsxStyle.Default with { WrapText = true };

        Assert.Null(Row(sheet, 0).Cells[4].Properties.Overflow);
        Assert.Null(Row(sheet, 1).Cells[0].Properties.Overflow);

        var page = Pdf(sheet, out var image);
        foreach (var text in new[] { Long, Wrapped })
        {
            var letters = Letters(page, text);
            Assert.True(letters.Select(l => Math.Round(l.Y)).Distinct().Count() >= 3, $"'{text}' is not wrapped");
            Assert.All(letters, l => Assert.True(Drawn(page, image, l), $"'{l.Value}' of '{text}' is missing"));
        }
    }

    [Theory]
    [InlineData(12345678d, "General", "12345678")]
    [InlineData(123456789d, "General", "1.23E+08")]
    [InlineData(12345.67, "#,##0.00", "12,345.67")]
    [InlineData(123456.78, "#,##0.00", "########")]
    [InlineData(46219d, "dd/mm/yyyy", "########")]
    public void A_default_column_holds_eight_digits(double value, string format, string expected)
    {
        var sheet = Sheet();
        sheet.Cell("B1").Value = value;
        sheet.Cell("B1").Style = XlsxStyle.Default with { NumberFormat = format };

        var cell = Row(sheet, 0).Cells[1];

        Assert.Equal(expected, cell.Blocks.OfType<WordParagraph>().Single().Text);
        Assert.NotNull(cell.Properties.Overflow);
        var page = Pdf(sheet, out var image);
        Assert.All(Letters(page, expected), l => Assert.True(Drawn(page, image, l), $"'{l.Value}' is missing"));
    }

    // Five default columns, A to E, held by the used range.
    private static XlsxWorksheet Sheet()
    {
        var sheet = new XlsxWorkbook().AddWorksheet("Feuille");
        sheet.Cell("A3").Value = "·";
        sheet.Cell("E3").Value = "·";
        return sheet;
    }

    private static WordTableRow Row(XlsxWorksheet sheet, int index) =>
        ExcelToPdf.ToWord(sheet.Workbook, new ExcelPdfOptions { SheetTitles = false }).Blocks.OfType<WordTable>().Single().Rows[index];

    private static PdfPage Pdf(XlsxWorksheet sheet, out RasterImage image)
    {
        var page = PdfDocument.Open(ExcelToPdf.Convert(sheet.Workbook, new ExcelPdfOptions { SheetTitles = false }).Pdf).GetPage(1);
        image = PdfRenderer.Render(page, new PdfRenderOptions { Dpi = 72 }).Image;
        return page;
    }

    // Letters left of the edge are drawn, letters right of it are not (those under the next cell's value aside).
    private static void AssertClippedAt(PdfPage page, RasterImage image, string text, double edge, string value)
    {
        var letters = Letters(page, text);
        var other = page.Letters.Last(l => l.Value == value).BoundingBox;
        Assert.All(letters.Where(l => l.BoundingBox.Right < edge - 1), l => Assert.True(Drawn(page, image, l), $"'{l.Value}' is missing"));
        var hidden = letters.Where(l => l.BoundingBox.Left > edge + 1 && (l.BoundingBox.Right < other.Left || l.BoundingBox.Left > other.Right)).ToList();
        Assert.True(hidden.Count >= 10, $"{hidden.Count} letters checked");
        Assert.All(hidden, l => Assert.False(Drawn(page, image, l), $"'{l.Value}' passes the cell"));
    }

    // The letters of the first occurrence of the text, in drawing order.
    private static List<PdfLetter> Letters(PdfPage page, string text)
    {
        var letters = page.Letters.Where(l => !string.IsNullOrWhiteSpace(l.Value)).ToList();
        var compact = text.Replace(" ", string.Empty, StringComparison.Ordinal);
        for (var i = 0; i + compact.Length <= letters.Count; i++)
        {
            if (string.Concat(letters.Skip(i).Take(compact.Length).Select(l => l.Value)) == compact)
            {
                return letters.GetRange(i, compact.Length);
            }
        }

        throw new InvalidOperationException($"'{text}' is not on the page.");
    }

    // The right edge of the cell holding the right-aligned number: its last digit plus the right margin.
    private static double CellRight(PdfPage page, string number) => page.Letters.Last(l => l.Value == number).BoundingBox.Right + 2.5;

    // True when dark pixels lie inside the letter's box (72 dpi, one pixel per point).
    private static bool Drawn(PdfPage page, RasterImage image, PdfLetter letter)
    {
        var box = letter.BoundingBox;
        for (var y = (int)(page.Height - box.Top) + 1; y < (int)(page.Height - box.Bottom); y++)
        {
            for (var x = (int)Math.Floor(box.Left); x < (int)Math.Ceiling(box.Right); x++)
            {
                // Darker than the light grey grid lines (BFBFBF).
                if (image.GetRgba(x, y).R < 160)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int Count(RasterImage image, (byte R, byte G, byte B) colour)
    {
        var count = 0;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var p = image.GetRgba(x, y);
                count += (p.R, p.G, p.B) == colour ? 1 : 0;
            }
        }

        return count;
    }
}
