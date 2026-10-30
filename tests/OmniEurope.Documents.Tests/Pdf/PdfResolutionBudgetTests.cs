// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf;

namespace OmniEurope.Documents.Tests.Pdf;

public sealed class PdfResolutionBudgetTests
{
    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    public void Nested_stream_lengths_are_rejected_before_exhausting_the_stack(int count)
    {
        var bytes = LengthChain(count);

        Assert.Throws<InvalidDataException>(() => PdfDocument.Open(bytes).GetPage(1).Text);
    }

    [Fact]
    public void Ordinary_indirect_lengths_and_cycles_still_finish()
    {
        Assert.Single(PdfDocument.Open(LengthChain(8)).Pages);
        var cyclic = RawPdf.Page("", "", "", "6 0 R", "5 0 R");
        var store = PdfDocument.Open(cyclic).Store;

        Assert.Null(store.Resolve(new Documents.Pdf.Objects.PdfReference(5, 0)));
        Assert.Equal(1, PdfDocument.Open(cyclic).PageCount);
    }

    private static byte[] LengthChain(int count)
    {
        var objects = new List<object>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources <<>> /Contents 4 0 R >>",
        };
        for (var i = 4; i < count + 4; i++) objects.Add($"<< /Length {i + 1} 0 R >> stream\nx\nendstream");
        objects.Add("1");
        return RawPdf.Objects(objects.ToArray());
    }
}
