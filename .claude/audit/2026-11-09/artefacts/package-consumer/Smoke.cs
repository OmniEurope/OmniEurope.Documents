using System.Text;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Csv;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Validation;

internal static class Smoke
{
    private static int _assertions;
    private static void Assert(bool result, string message)
    {
        Console.WriteLine($"ASSERT {++_assertions}: {(result ? "PASS" : "FAIL")} {message}");
        if (!result) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("name,value\r\nalpha,42\r\n"));
        XlsxWorkbook workbook;
        using (var csv = CsvReader.Open(source, leaveOpen: true)) workbook = XlsxCsvConverter.FromCsv(csv);
        Assert(source.CanRead, "Flux CSV laisse ouvert");
        Assert((string?)workbook.Worksheets[0].Cell("B2").Value == "42", "Import CSV conserve le texte par defaut");
        using var xlsx = new MemoryStream();
        workbook.Save(xlsx);
        Assert(xlsx.CanWrite, "Flux sauvegarde Excel laisse ouvert");
        xlsx.Position = 0;
        var reread = XlsxWorkbook.Load(xlsx);
        Assert(xlsx.CanRead && reread.Worksheets[0].Cell("A2").FormattedText == "alpha", "Relecture Excel reelle et flux laisse ouvert");
        var excelPdf = ExcelToPdf.Convert(reread);
        using var excelPdfStream = new MemoryStream(excelPdf.Pdf);
        var pdf = PdfDocument.Open(excelPdfStream);
        Assert(excelPdfStream.CanRead && pdf.PageCount > 0 && pdf.GetPage(1).Text.Contains("alpha", StringComparison.Ordinal), "Excel vers PDF relu et texte present");

        var word = MarkdownToWord.Convert("# Audit\n\nBonjour **monde**.\n");
        using var docx = new MemoryStream();
        word.Save(docx);
        Assert(docx.CanWrite, "Flux sauvegarde Word laisse ouvert");
        docx.Position = 0;
        var wordRead = WordDocument.Load(docx);
        Assert(docx.CanRead && wordRead.Text.Contains("Bonjour monde.", StringComparison.Ordinal), "Relecture Word conserve le texte et flux laisse ouvert");
        var report = WordSchemaValidator.Validate(docx.ToArray());
        Assert(report.Errors.Count == 0, "Word produit valide avec schemas embarques");
        var wordPdf = WordToPdf.Convert(wordRead);
        Assert(PdfDocument.Open(wordPdf.Pdf).GetPage(1).Text.Contains("Bonjour", StringComparison.Ordinal), "Markdown vers Word vers PDF relu");
        var html = WordToHtml.Convert(wordRead);
        Assert(html.Html.Contains("Bonjour", StringComparison.Ordinal) && html.Html.Contains("data-address", StringComparison.Ordinal), "Word vers HTML contient texte et adresse");
        Assert(HtmlToWord.Convert(html.Html).Text.Contains("Bonjour monde.", StringComparison.Ordinal), "HTML relu vers modele Word");
        Assert(word.ToArray().SequenceEqual(word.ToArray()), "Sauvegardes Word repetees deterministes");
        Assert(workbook.ToArray().SequenceEqual(workbook.ToArray()), "Sauvegardes Excel repetees deterministes");
        using var output = new MemoryStream();
        using (var writer = CsvWriter.Create(output, leaveOpen: true)) writer.WriteRecord("alpha", "42");
        Assert(output.CanWrite && Encoding.UTF8.GetString(output.ToArray()) == "alpha,42\r\n", "Ecriture CSV exacte et flux laisse ouvert");
        Console.WriteLine($"ASSERTIONS={_assertions}");
    }
}
