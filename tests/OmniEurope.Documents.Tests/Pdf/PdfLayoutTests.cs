// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Layout;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Tests.Pdf;

public sealed class PdfLayoutTests
{
    [Fact]
    public void Reads_browser_columns_one_after_the_other()
    {
        var document = PdfDocument.Open(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Pdf", "browser.pdf")));

        var text = PdfLayoutAnalyzer.AnalyzePage(document.GetPage(1)).Text;

        Assert.StartsWith("Rapport de test", text);
        var left = text.IndexOf("Colonne gauche", StringComparison.Ordinal);
        var leftEnd = text.IndexOf("dix-neuf vingt.", StringComparison.Ordinal);
        var right = text.IndexOf("Colonne droite", StringComparison.Ordinal);
        Assert.True(left >= 0 && leftEnd > left && right > leftEnd, text);
    }

    [Fact]
    public void Orders_two_columns_and_flags_running_headers_and_footers()
    {
        var document = PdfDocument.Open(TwoColumns(pages: 3));

        var layouts = PdfLayoutAnalyzer.Analyze(document);

        Assert.Equal(3, layouts.Count);
        foreach (var layout in layouts)
        {
            var decorations = layout.Blocks.Where(b => b.IsDecoration).Select(b => b.Text).ToList();
            Assert.Contains("Rapport annuel", decorations);
            Assert.Contains($"Page {layout.Page.Number} / 3", decorations);
            var text = layout.Text;
            Assert.DoesNotContain("Rapport annuel", text);
            Assert.True(text.IndexOf("gauche fin", StringComparison.Ordinal) < text.IndexOf("droite début", StringComparison.Ordinal), text);
        }

        Assert.Equal(0, layouts[0].Blocks.First(b => b.Text.StartsWith("Titre", StringComparison.Ordinal)).ReadingOrder - layouts[0].Blocks.Count(b => b.IsDecoration && b.BoundingBox.Top > 700));
    }

    [Fact]
    public void Splits_words_and_reports_their_style()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage();
        page.DrawText("Bonjour", 72, 100, PdfFont.Sans.AsBold(), 12);
        page.DrawText("le monde", 140, 100, PdfFont.Serif.AsItalic(), 12);
        var document = PdfDocument.Open(builder.ToArray());

        var words = PdfLayoutAnalyzer.Words(document.GetPage(1));

        Assert.Equal(["Bonjour", "le", "monde"], words.Select(w => w.Text));
        Assert.True(words[0].IsBold);
        Assert.True(words[2].IsItalic);
        Assert.Equal(12, words[1].FontSize);
        Assert.True(words[0].BoundingBox.Right < words[1].BoundingBox.Left);
    }

    [Fact]
    public void Detects_scanned_pages()
    {
        var builder = new PdfDocumentBuilder();
        var scan = new RasterImage(200, 280, ImageColorType.Gray);
        builder.AddPage().DrawImage(builder.AddImage(scan), 0, 0, 595.28, 841.89);
        builder.AddPage().DrawText(string.Join(' ', Enumerable.Repeat("texte réel", 10)), 72, 100, PdfFont.Sans, 12);
        var document = PdfDocument.Open(builder.ToArray());

        Assert.True(PdfLayoutAnalyzer.IsScanned(document.GetPage(1)));
        Assert.False(PdfLayoutAnalyzer.IsScanned(document.GetPage(2)));
    }

    private static byte[] TwoColumns(int pages)
    {
        var builder = new PdfDocumentBuilder();
        for (var n = 1; n <= pages; n++)
        {
            var page = builder.AddPage();
            page.DrawText("Rapport annuel", 72, 40, PdfFont.Sans, 9);
            page.DrawTextInBox($"Page {n} / {pages}", 0, 810, page.Width, 0, PdfFont.Sans, 9, alignment: PdfTextAlignment.Center);
            if (n == 1)
            {
                page.DrawText("Titre du rapport", 72, 100, PdfFont.Sans.AsBold(), 18);
            }

            Column(page, 72, n == 1 ? 140 : 100, "gauche");
            Column(page, 320, n == 1 ? 140 : 100, "droite");
        }

        return builder.ToArray();
    }

    private static void Column(PdfCanvas page, double x, double top, string name)
    {
        page.DrawText($"Colonne {name} début", x, top, PdfFont.Serif, 11);
        for (var i = 1; i <= 8; i++)
        {
            page.DrawText($"ligne {i} de la colonne {name}", x, top + (i * 14), PdfFont.Serif, 11);
        }

        page.DrawText($"Colonne {name} fin", x, top + (9 * 14), PdfFont.Serif, 11);
    }
}
