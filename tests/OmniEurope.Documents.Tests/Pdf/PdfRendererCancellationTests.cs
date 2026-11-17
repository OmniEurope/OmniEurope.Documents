// SPDX-License-Identifier: EUPL-1.2
// The overloads without a token are the baseline these tests compare against, so they are called on purpose.
#pragma warning disable xUnit1051
using System.Text;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>Page rendering accepts a cancellation token: a cancelled one throws and returns no image, and the token
/// changes nothing while it is not cancelled.</summary>
public sealed class PdfRendererCancellationTests
{
    private static readonly PdfRenderOptions Options = new() { Dpi = 72 };

    private static CancellationToken Cancelled()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        return source.Token;
    }

    [Fact]
    public void Rendering_throws_when_the_token_is_already_cancelled()
    {
        var document = PdfDocument.Open(RawPdf.Page("1 0 0 rg 20 20 60 60 re f"));

        Assert.Throws<OperationCanceledException>(() => PdfRenderer.Render(document.GetPage(1), Options, Cancelled()));
        Assert.Throws<OperationCanceledException>(() => PdfRenderer.RenderToPng(document, 1, Options, Cancelled()));
    }

    [Fact]
    public void Rendering_with_a_live_token_gives_the_same_pixels_as_without_one()
    {
        var document = PdfDocument.Open(RawPdf.Page("1 0 0 rg 20 20 60 60 re f 0 0 1 RG 4 w 20 100 m 180 100 l S"));
        using var source = new CancellationTokenSource();

        var without = PdfRenderer.Render(document.GetPage(1), Options);
        var with = PdfRenderer.Render(document.GetPage(1), Options, source.Token);

        Assert.Equal(without.ToPng(), with.ToPng());
        Assert.Equal(PdfRenderer.RenderToPng(document, 1, Options), PdfRenderer.RenderToPng(document, 1, Options, source.Token));
    }

    [Fact]
    public void A_content_stream_checks_the_token_at_each_batch_of_operators_and_not_before()
    {
        Assert.Throws<OperationCanceledException>(() => RunWithCancelledToken(PageRenderer.CancellationBatch));
        RunWithCancelledToken(PageRenderer.CancellationBatch - 1);
    }

    // Runs that many operators on a renderer whose token is already cancelled, so the first checkpoint reached throws.
    private static void RunWithCancelledToken(int operators)
    {
        var page = PdfDocument.Open(RawPdf.Page("n")).GetPage(1);
        var context = new RenderContext(FontLibrary.Default) { CancellationToken = Cancelled() };
        var renderer = new PageRenderer(page.Store, new Surface(10, 10), Matrix.Identity, context);

        renderer.Run(Encoding.ASCII.GetBytes(string.Join(' ', Enumerable.Repeat("n", operators))), null);
    }
}
