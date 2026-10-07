// SPDX-License-Identifier: EUPL-1.2
using System.Diagnostics;
using System.Globalization;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Csv;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Editing;
using OmniEurope.Documents.Pdf.Writing;
using OmniEurope.Documents.Tests;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.StressTests;

/// <summary>
/// Large documents within a time limit, the same output for the same input, and conversions running side by
/// side giving the bytes they give alone.
/// </summary>
public sealed class ScaleTests
{
    [Fact]
    public void A_long_report_converts_to_pdf_in_order()
    {
        var document = new WordDocument();
        for (var chapter = 1; chapter <= 40; chapter++)
        {
            document.AddHeading($"Chapitre {chapter}");
            for (var p = 1; p <= 60; p++)
            {
                document.AddParagraph($"Chapitre {chapter}, paragraphe {p} : texte courant assez long pour occuper toute la largeur de la ligne et passer à la suivante.");
            }

            var table = new WordTable(150, 150, 150);
            for (var r = 0; r < 10; r++)
            {
                table.AddRow($"{chapter}.{r}", "valeur", "commentaire");
            }

            document.AddTable(table);
        }

        var (result, elapsed) = Timed(() => WordToPdf.Convert(document));

        var pdf = PdfDocument.Open(result.Pdf);
        Assert.Equal(result.PageCount, pdf.PageCount);
        Assert.InRange(pdf.PageCount, 100, 300);
        Assert.Contains("Chapitre 40, paragraphe 60", pdf.GetPage(pdf.PageCount).Text + pdf.GetPage(pdf.PageCount - 1).Text, StringComparison.Ordinal);
        Samples.InOrder(Samples.AllText(pdf), "Chapitre 1,", "Chapitre 20,", "40.9");
        Assert.True(elapsed < TimeSpan.FromSeconds(60), $"{elapsed}");
    }

    [Fact]
    public void A_large_sheet_saves_reads_and_prints_with_its_header_on_every_page()
    {
        var workbook = new XlsxWorkbook();
        var sheet = workbook.AddWorksheet("Données");
        for (var c = 1; c <= 8; c++)
        {
            sheet.Cell(1, c).Value = $"Colonne {c}";
        }

        for (var r = 2; r <= 6001; r++)
        {
            for (var c = 1; c <= 8; c++)
            {
                sheet.Cell(r, c).Value = (r * 10) + c;
            }
        }

        sheet.FreezePanes(1);

        var (bytes, saving) = Timed(workbook.ToArray);
        var (reread, reading) = Timed(() => XlsxWorkbook.Load(bytes));
        Assert.Equal(48008, reread.Worksheet("Données").Cells.Count());
        Assert.Equal(60018.0, reread.Worksheet("Données").Cell("H6001").Value);
        Assert.Equal(6001, XlsxCsvConverter.ToCsv(reread.Worksheet("Données")).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);

        var excerpt = new XlsxWorkbook();
        var printed = excerpt.AddWorksheet("Extrait");
        for (var r = 1; r <= 400; r++)
        {
            for (var c = 1; c <= 8; c++)
            {
                printed.Cell(r, c).Value = reread.Worksheet("Données").Cell(r, c).Value;
            }
        }

        printed.FreezePanes(1);
        var pdf = PdfDocument.Open(ExcelToPdf.Convert(excerpt, new ExcelPdfOptions { SheetTitles = false }).Pdf);
        Assert.True(pdf.PageCount > 3);
        Assert.All(pdf.Pages, p => Assert.StartsWith("Colonne 1 Colonne 2 Colonne 3", p.Text, StringComparison.Ordinal));
        Assert.True(saving + reading < TimeSpan.FromSeconds(30), $"{saving} + {reading}");
    }

    [Fact]
    public void A_thousand_pages_open_merge_and_split()
    {
        var builder = new PdfDocumentBuilder();
        for (var i = 1; i <= 1000; i++)
        {
            builder.AddPage().DrawText($"Feuillet {i}", 72, 72, PdfFont.Sans, 12);
        }

        var (pages, elapsed) = Timed(() =>
        {
            var source = PdfDocument.Open(builder.ToArray());
            var doubled = PdfDocument.Open(PdfEditor.Merge(source, source));
            return (Doubled: doubled, Parts: PdfEditor.Split(doubled, 250));
        });

        Assert.Equal(2000, pages.Doubled.PageCount);
        Assert.Equal("Feuillet 1000", pages.Doubled.GetPage(1000).Text.Trim());
        Assert.Equal("Feuillet 1", pages.Doubled.GetPage(1001).Text.Trim());
        Assert.Equal(8, pages.Parts.Count);
        Assert.Equal("Feuillet 751", PdfDocument.Open(pages.Parts[3]).GetPage(1).Text.Trim());
        Assert.True(elapsed < TimeSpan.FromSeconds(60), $"{elapsed}");
    }

    [Fact]
    public void Two_hundred_thousand_csv_records_round_trip()
    {
        var rows = Enumerable.Range(0, 200_000).Select(i => new[] { i.ToString(CultureInfo.InvariantCulture), $"nom \"{i}\"", i % 7 == 0 ? "ligne 1\nligne 2" : "simple", "a;b,c" });

        var (text, writing) = Timed(() => CsvWriter.WriteAll(rows));
        var (read, reading) = Timed(() => CsvReader.ReadAll(text, new CsvReaderOptions { HasHeader = false, Delimiter = ',' }));

        Assert.Equal(200_000, read.Count);
        Assert.Equal(["199999", "nom \"199999\"", "simple", "a;b,c"], read[^1]);
        Assert.Equal("ligne 1\nligne 2", read[700][2]);
        Assert.True(writing + reading < TimeSpan.FromSeconds(30), $"{writing} + {reading}");
    }

    [Fact]
    public async Task Conversions_are_repeatable_and_safe_side_by_side()
    {
        Func<byte[]>[] conversions =
        [
            () => WordToPdf.Convert(SampleDocuments.Report()).Pdf,
            () => ExcelToPdf.Convert(SampleDocuments.Workbook()).Pdf,
            () => MarkdownToPdf.Convert("# Titre\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n- un\n- deux").Pdf,
            () => SampleDocuments.MergedPdf(),
            () => SampleDocuments.Report().ToArray(),
            () => SampleDocuments.Workbook().ToArray(),
        ];

        var expected = conversions.Select(c => c()).ToList();
        for (var i = 0; i < conversions.Length; i++)
        {
            Assert.Equal(expected[i], conversions[i]());
        }

        var parallel = await Task.WhenAll(Enumerable.Range(0, 24).Select(k => Task.Run(() => (Index: k % conversions.Length, Bytes: conversions[k % conversions.Length]()), TestContext.Current.CancellationToken)));

        Assert.All(parallel, r => Assert.Equal(expected[r.Index], r.Bytes));
    }

    private static (T Result, TimeSpan Elapsed) Timed<T>(Func<T> work)
    {
        var watch = Stopwatch.StartNew();
        var result = work();
        TestContext.Current.SendDiagnosticMessage($"{watch.Elapsed.TotalMilliseconds:N0} ms");
        return (result, watch.Elapsed);
    }
}
