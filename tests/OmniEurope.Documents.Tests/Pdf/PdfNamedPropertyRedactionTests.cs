// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Editing;
using OmniEurope.Documents.Pdf.Objects;

namespace OmniEurope.Documents.Tests.Pdf;

public sealed class PdfNamedPropertyRedactionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Named_replacement_text_is_removed_from_all_reachable_resources(bool form, bool shared)
    {
        const string text = "BT /F1 12 Tf 10 50 Td /Span /P1 BDC (VISIBLESECRET) Tj EMC ET";
        const string resources = "/Font << /F1 5 0 R >> /Properties << /P1 6 0 R /Alias 6 0 R >>";
        var publicText = shared ? "BT /F1 12 Tf 10 150 Td /Span /Alias BDC (PUBLIC) Tj EMC ET " : "";
        var source = RawPdf.Page(publicText + (form ? "/Fm Do" : text), resources + (form ? " /XObject << /Fm 7 0 R >>" : ""), "",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            "<< /ActualText (ALTERNATESECRET) /Alt (ALTSECRET) /E (EXPANSIONSECRET) /MCID 7 >>",
            RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 200 200]", RawPdf.Ascii(text)));
        var document = PdfDocument.Open(source);

        var result = PdfRedactor.Redact(document, [new PdfRedaction(1, new PdfRectangle(0, 30, 200, 70))]);
        var reopened = PdfDocument.Open(result.Pdf);
        var bytes = AllContents(reopened, result.Pdf);

        Assert.Equal("VISIBLESECRET", result.RemovedText);
        Assert.DoesNotContain("SECRET", bytes, StringComparison.Ordinal);
        Assert.Contains("ALTERNATESECRET", AllContents(document, source), StringComparison.Ordinal);
        var originalProperties = document.Store.Get<PdfDictionary>(document.Store.Get<PdfDictionary>(document.GetPage(1).Resources, "Properties"), "P1")!;
        Assert.Equal("ALTERNATESECRET", Assert.IsType<PdfString>(originalProperties["ActualText"]).ToText());
        if (shared) Assert.Contains("PUBLIC", reopened.GetPage(1).Text, StringComparison.Ordinal);
        Assert.Equal(7, reopened.Store.Number(reopened.Store.Get<PdfDictionary>(reopened.Store.Get<PdfDictionary>(reopened.GetPage(1).Resources, "Properties"), "P1"), "MCID"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Properties_shared_across_pages_are_cleaned_independently_of_copy_order(bool publicFirst)
    {
        var source = RawPdf.Objects(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources 9 0 R /Contents {(publicFirst ? 5 : 6)} 0 R >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources 9 0 R /Contents {(publicFirst ? 6 : 5)} 0 R >>",
            RawPdf.Stream("", RawPdf.Ascii("BT /F1 12 Tf 10 150 Td /Span /P1 BDC (PUBLIC) Tj EMC ET")),
            RawPdf.Stream("", RawPdf.Ascii("BT /F1 12 Tf 10 50 Td /Span /P1 BDC (VISIBLESECRET) Tj EMC ET")),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            "<< /ActualText (ALTERNATESECRET) /Alt (ALTSECRET) /E (EXPANSIONSECRET) >>",
            "<< /Font << /F1 7 0 R >> /Properties << /P1 8 0 R >> >>");
        var document = PdfDocument.Open(source);

        var result = PdfRedactor.Redact(document, [new PdfRedaction(publicFirst ? 2 : 1, new PdfRectangle(0, 30, 200, 70))]);

        Assert.DoesNotContain("SECRET", AllContents(PdfDocument.Open(result.Pdf), result.Pdf), StringComparison.Ordinal);
        Assert.Contains("PUBLIC", PdfDocument.Open(result.Pdf).GetPage(publicFirst ? 1 : 2).Text, StringComparison.Ordinal);
        Assert.Contains("ALTERNATESECRET", AllContents(document, source), StringComparison.Ordinal);
    }

    private static string AllContents(PdfDocument document, byte[] bytes)
    {
        var output = new StringBuilder(Encoding.Latin1.GetString(bytes));
        foreach (var number in document.Store.ObjectNumbers)
        {
            if (document.Store.Resolve(new PdfReference(number, 0)) is PdfStream stream)
                output.Append(Encoding.Latin1.GetString(document.Store.DecodeBytes(stream)));
        }
        return output.ToString();
    }
}
