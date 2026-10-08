// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Writing;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

public sealed class ConvertersTests
{
    private static readonly byte[] Png = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "rgb.png"));

    [Fact]
    public void Html_becomes_word_structure()
    {
        var html = $"""
            <html><head><title>Note</title><style>p {"{"} color: red {"}"}</style><script>alert(1)</script></head><body>
            <h1>Titre</h1>
            <p style="text-align:center; color:#c00">Paragraphe <b>gras</b>, <i>italique</i>, <code>code</code> et <a href="https://example.org">lien</a>.<br>Seconde ligne</p>
            <ol start="3"><li>Trois<ul><li>Puce imbriquée</li></ul></li><li>Quatre</li></ol>
            <pre>ligne 1
              ligne 2</pre>
            <hr>
            <table><thead><tr><th colspan="2">Entête</th></tr></thead>
            <tr><td rowspan="2">Fusion</td><td>A</td></tr><tr><td>B</td></tr></table>
            <p><img src="data:image/png;base64,{System.Convert.ToBase64String(Png)}" alt="Dégradé" width="40"><img src="https://example.org/x.png"></p>
            </body></html>
            """;

        var document = HtmlToWord.Convert(html);

        var paragraphs = document.Blocks.OfType<WordParagraph>().ToList();
        Assert.Equal("Note", document.Information.Title);
        Assert.Equal("Heading1", paragraphs[0].StyleId);
        var body = paragraphs[1];
        Assert.Equal("Paragraphe gras, italique, code et lien.\nSeconde ligne", body.Text);
        Assert.Equal(WordAlignment.Center, body.Properties.Alignment);
        Assert.True(body.Inlines.OfType<WordText>().Single(t => t.Value == "gras").Properties.Bold);
        Assert.Equal("CC0000", body.Inlines.OfType<WordText>().First().Properties.Color);
        Assert.Equal("Courier New", body.Inlines.OfType<WordText>().Single(t => t.Value == "code").Properties.Font);
        Assert.Equal("https://example.org", body.Inlines.OfType<WordHyperlink>().Single().Target);
        var counter = new WordListCounter(document.Numbering);
        var items = paragraphs.Where(p => p.Properties.NumberingId is not null).Select(p => (p.Text, counter.Next(p.Properties.NumberingId!.Value, p.Properties.NumberingLevel ?? 0)!.Value.Label)).ToList();
        Assert.Equal([("Trois", "3."), ("Puce imbriquée", "◦"), ("Quatre", "4.")], items);
        Assert.Contains(paragraphs, p => p.Text == "ligne 1\n  ligne 2");
        Assert.Contains(paragraphs, p => p.Properties.Borders?.Bottom is not null);
        var table = document.Blocks.OfType<WordTable>().Single();
        Assert.True(table.Rows[0].Properties.IsHeader);
        Assert.Equal(2, table.Rows[0].Cells[0].Properties.GridSpan);
        Assert.Equal(WordVerticalMerge.Restart, table.Rows[1].Cells[0].Properties.VerticalMerge);
        Assert.Equal(WordVerticalMerge.Continue, table.Rows[2].Cells[0].Properties.VerticalMerge);
        Assert.Equal("B", table.Rows[2].Cells[1].Text);
        var picture = paragraphs.SelectMany(p => p.Inlines).OfType<WordPicture>().Single();
        Assert.True(OmniEurope.Documents.Imaging.ImageInfo.TryIdentify(Png, out var info));
        Assert.Equal((30, 30.0 * info.Height / info.Width), (picture.Width, picture.Height));
        Assert.Contains("images that are not data: URIs are not loaded", document.Gaps);
        Assert.DoesNotContain("alert", document.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_and_markdown_reach_pdf_with_their_text()
    {
        var html = HtmlToPdf.Convert("<h2>Rapport HTML</h2><p>Contenu <em>accentué</em> : été, Ωμέγα.</p>");
        var markdown = MarkdownToPdf.Convert("# Rapport Markdown\n\nTexte **gras** et `code`.\n\n| A | B |\n|---|---|\n| 1 | 2 |\n\n- [x] fait\n- [ ] à faire\n\n<script>x</script>");

        var htmlText = PdfDocument.Open(html.Pdf).GetPage(1).Text;
        var markdownText = PdfDocument.Open(markdown.Pdf).GetPage(1).Text;

        Assert.Contains("Rapport HTML", htmlText);
        Assert.Contains("Contenu accentué : été, Ωμέγα.", htmlText);
        Assert.Contains("Rapport Markdown", markdownText);
        Assert.Contains("Texte gras et code.", markdownText);
        Assert.Contains("[x] fait", markdownText);
        Assert.Contains("<script>x</script>", markdownText);
        var document = MarkdownToWord.Convert("| A | B |\n|---|---|\n| 1 | 2 |");
        Assert.Equal("A\tB\n1\t2", document.Blocks.OfType<WordTable>().Single().Text);
    }

    [Fact]
    public void Workbooks_become_tables_with_displayed_values()
    {
        var workbook = new XlsxWorkbook { Title = "Classeur" };
        var sheet = workbook.AddWorksheet("Ventes");
        sheet.Cell("A1").Value = "Produit";
        sheet.Cell("B1").Value = "Montant";
        sheet.Cell("A1").Style = XlsxStyle.Default with { Bold = true, FillColor = "DDEBF7" };
        sheet.Cell("A2").Value = "Pommes";
        sheet.Cell("B2").Value = 1234.5;
        sheet.Cell("B2").Style = XlsxStyle.Default with { NumberFormat = "#,##0.00" };
        sheet.Cell("A3").Value = "Total fusionné";
        sheet.Merge(XlsxRange.Parse("A3:B3"));
        sheet.FreezePanes(1);
        var wide = workbook.AddWorksheet("Large");
        for (var c = 1; c <= 14; c++)
        {
            wide.Cell(1, c).Value = "Col" + c;
        }

        var result = ExcelToPdf.Convert(workbook.ToArray());

        var pdf = PdfDocument.Open(result.Pdf);
        Assert.Equal(2, pdf.PageCount);
        var text = pdf.GetPage(1).Text;
        Assert.Contains("Ventes", text);
        Assert.Contains("1,234.50", text);
        Assert.Contains("Total fusionné", text);
        Assert.True(pdf.GetPage(2).Width > pdf.GetPage(2).Height);
        Assert.Contains("Col14", pdf.GetPage(2).Text);
        var table = ExcelToPdf.ToWord(workbook).Blocks.OfType<WordTable>().First();
        Assert.True(table.Rows[0].Properties.IsHeader);
        Assert.Equal(2, table.Rows[2].Cells[0].Properties.GridSpan);
        Assert.Equal("DDEBF7", table.Rows[0].Cells[0].Properties.Shading);
        Assert.Equal(WordAlignment.Right, ((WordParagraph)table.Rows[1].Cells[1].Blocks[0]).Properties.Alignment);
    }

    [Fact]
    public void A_sparse_giant_sheet_is_refused_before_any_layout()
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("Creuse");
        sheet.Cell("A1").Value = "début";
        sheet.Cell("XFD1048576").Value = "fin";
        var xlsx = workbook.ToArray();

        var error = Assert.Throws<DocumentFormatException>(() => ExcelToPdf.Convert(xlsx));
        Assert.Contains("17,179,869,184 cells", error.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ExcelPdfOptions.MaxCells), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_cell_limit_counts_every_sheet_with_its_empty_cells()
    {
        var workbook = new XlsxWorkbook();
        foreach (var name in new[] { "Un", "Deux" })
        {
            var sheet = workbook.AddWorksheet(name);
            sheet.Cell("B2").Value = 1;
            sheet.Cell("K11").Value = 2;
        }

        Assert.Throws<DocumentFormatException>(() => ExcelToPdf.ToWord(workbook, new ExcelPdfOptions { MaxCells = 199 }));
        Assert.Equal(2, ExcelToPdf.ToWord(workbook, new ExcelPdfOptions { MaxCells = 200 }).Blocks.OfType<WordTable>().Count());
    }

    [Fact]
    public void Images_become_pages()
    {
        var jpeg = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "baseline.jpg"));

        var natural = PdfDocument.Open(ImagesToPdf.Convert([Png, jpeg]));
        var fitted = PdfDocument.Open(ImagesToPdf.Convert([Png], new ImagePdfOptions { PageSize = PdfPageSize.A4, Title = "Images" }));

        Assert.Equal(2, natural.PageCount);
        Assert.Single(natural.GetPage(2).Images);
        var image = Assert.Single(fitted.GetPage(1).Images);
        Assert.True(fitted.GetPage(1).Width > fitted.GetPage(1).Height, "a wide image turns the page");
        Assert.Equal(841.89 - (2 * 28.35), image.Bounds.Width, 1);
        Assert.Equal("Images", fitted.Information.Title);
        Assert.Throws<ArgumentException>(() => ImagesToPdf.Convert([[1, 2, 3]]));
        Assert.Throws<ArgumentException>(() => ImagesToPdf.Convert([]));
    }
}
