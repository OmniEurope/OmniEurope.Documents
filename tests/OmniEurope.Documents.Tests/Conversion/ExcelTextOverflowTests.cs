// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Pdf.Text;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// Text that does not wrap, printed as Excel shows it: on one line, running on into empty neighbouring cells
/// (right of left-aligned text, left of right-aligned text, both sides of centred text) and clipped at the
/// first cell holding a value, a fill or a border. Each letter of the PDF is checked in the rendering: drawn
/// when it lies in the cells the text may use, absent when it lies past them.
/// </summary>
public sealed class ExcelTextOverflowTests
{
    private const string Long = "Libellé beaucoup plus long que sa colonne";

    // Wider than one default column, narrower than four.
    private const string Fits = "Libellé plus long que sa colonne";

    private const string Wrapped = "Texte renvoyé à la ligne dans sa cellule";

    [Fact]
    public void Left_aligned_text_runs_on_to_the_right_until_a_value()
    {
        var sheet = Sheet();
        sheet.Cell("A1").Value = Fits;
        sheet.Cell("E1").Value = "fin";

        var row = Row(sheet, 0);

        Assert.Equal([4, 1], row.Cells.Select(c => c.Properties.GridSpan ?? 1));
        Assert.Null(row.Cells[0].Properties.Overflow);
        var page = Pdf(sheet);
        Assert.StartsWith(Fits + " fin", page.Text, StringComparison.Ordinal);
        Assert.All(Letters(page, Fits), l => Assert.True(Drawn(page, l), $"'{l.Value}' at {l.X:N1} is missing"));
    }

    [Fact]
    public void Text_meeting_a_value_is_clipped_at_its_own_cell()
    {
        var sheet = Sheet();
        sheet.Cell("A1").Value = Long;
        sheet.Cell("B1").Value = 7;

        var row = Row(sheet, 0);

        Assert.Equal([1, 1, 1, 1, 1], row.Cells.Select(c => c.Properties.GridSpan ?? 1));
        Assert.Equal(WordAlignment.Left, row.Cells[0].Properties.Overflow?.Alignment);
        var page = Pdf(sheet);
        var edge = CellRight(page, "7") - Column;
        var letters = Letters(page, Long);
        Assert.Contains(letters, l => l.BoundingBox.Right < edge);
        Assert.Contains(letters, l => l.BoundingBox.Left > edge);
        Assert.All(letters.Where(l => l.BoundingBox.Right < edge - 1), l => Assert.True(Drawn(page, l), $"'{l.Value}' is missing"));
        // Past the edge nothing of the text is drawn (the 7 of the next cell is left out of the check).
        var seven = page.Letters.Last(l => l.Value == "7").BoundingBox;
        var hidden = letters.Where(l => l.BoundingBox.Left > edge + 1 && (l.BoundingBox.Right < seven.Left || l.BoundingBox.Left > seven.Right)).ToList();
        Assert.True(hidden.Count >= 10, $"{hidden.Count} letters checked");
        Assert.All(hidden, l => Assert.False(Drawn(page, l), $"'{l.Value}' passes the cell"));
    }

    [Fact]
    public void Right_aligned_text_runs_on_to_the_left_and_centred_text_both_ways()
    {
        var sheet = Sheet();
        sheet.Cell("D1").Value = Long;
        sheet.Cell("D1").Style = XlsxStyle.Default with { HorizontalAlignment = XlsxHorizontalAlignment.Right };
        sheet.Cell("C2").Value = "Titre centré assez long";
        sheet.Cell("C2").Style = XlsxStyle.Default with { HorizontalAlignment = XlsxHorizontalAlignment.Center };

        var right = Row(sheet, 0);
        var centred = Row(sheet, 1);

        Assert.Equal([4, 1], right.Cells.Select(c => c.Properties.GridSpan ?? 1));
        Assert.Equal([1, 3, 1], centred.Cells.Select(c => c.Properties.GridSpan ?? 1));
        var page = Pdf(sheet);
        Assert.All(Letters(page, Long).Concat(Letters(page, "Titre centré assez long")), l => Assert.True(Drawn(page, l), $"'{l.Value}' is missing"));
    }

    [Fact]
    public void A_fill_stops_the_text_and_wrapped_text_keeps_wrapping()
    {
        var sheet = Sheet();
        sheet.Cell("A1").Value = Long;
        sheet.Cell("B1").Style = XlsxStyle.Default with { FillColor = "FFFF00" };
        sheet.Cell("A2").Value = Wrapped;
        sheet.Cell("A2").Style = XlsxStyle.Default with { WrapText = true };

        Assert.Equal(1, Row(sheet, 0).Cells[0].Properties.GridSpan ?? 1);
        Assert.NotNull(Row(sheet, 0).Cells[0].Properties.Overflow);
        Assert.Null(Row(sheet, 1).Cells[0].Properties.Overflow);

        // The wrapped copy takes several lines: more distinct baselines than the single-line one.
        var page = Pdf(sheet);
        var lines = Letters(page, Wrapped).Select(l => Math.Round(l.Y)).Distinct().Count();
        Assert.True(lines >= 3, $"{lines} baselines");
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

    private static PdfPage Pdf(XlsxWorksheet sheet) =>
        PdfDocument.Open(ExcelToPdf.Convert(sheet.Workbook, new ExcelPdfOptions { SheetTitles = false }).Pdf).GetPage(1);

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

    // A default column: 8.43 characters of 7 pixels plus 5 pixels of padding, at 0.75 point per pixel.
    private const double Column = ((8.43 * 7) + 5) * 0.75;

    // True when dark pixels lie inside the letter's box (72 dpi, one pixel per point).
    private static bool Drawn(PdfPage page, PdfLetter letter)
    {
        var image = PdfRenderer.Render(page, new PdfRenderOptions { Dpi = 72 }).Image;
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
}
