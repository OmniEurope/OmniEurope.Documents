// SPDX-License-Identifier: EUPL-1.2
// The overloads without a token are the baseline these tests compare against, so they are called on purpose.
#pragma warning disable xUnit1051
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Conversion.WordLayout;
using OmniEurope.Documents.Markdown;
using OmniEurope.Documents.Pdf.Writing;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>The conversions to PDF accept a cancellation token: a cancelled one throws before anything is returned,
/// and the token changes nothing while it is not cancelled.</summary>
public sealed class ConversionCancellationTests
{
    private const string Html = "<h1>Titre</h1><p>Un paragraphe <b>gras</b>.</p><table><tr><td>A</td><td>B</td></tr></table>";
    private const string Markdown = "# Titre\n\nUn paragraphe **gras**.\n\n| A | B |\n|---|---|\n| 1 | 2 |\n";

    private static CancellationToken Cancelled()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        return source.Token;
    }

    [Fact]
    public void Word_to_pdf_throws_when_the_token_is_already_cancelled()
    {
        var document = SampleDocuments.Report();

        Assert.Throws<OperationCanceledException>(() => WordToPdf.Convert(document, null, Cancelled()));
        Assert.Throws<OperationCanceledException>(() => WordToPdf.Convert(document.ToArray(), null, Cancelled()));
    }

    [Fact]
    public void Word_to_pdf_with_a_live_token_gives_the_same_bytes_as_without_one()
    {
        var document = SampleDocuments.Report();
        using var source = new CancellationTokenSource();

        var without = WordToPdf.Convert(document);
        var with = WordToPdf.Convert(document, null, source.Token);
        var fromBytes = WordToPdf.Convert(document.ToArray(), null, source.Token);

        Assert.Equal(without.Pdf, with.Pdf);
        Assert.Equal(without.PageCount, with.PageCount);
        Assert.Equal(WordToPdf.Convert(document.ToArray()).Pdf, fromBytes.Pdf);
    }

    [Fact]
    public void Excel_to_pdf_throws_when_the_token_is_already_cancelled()
    {
        var workbook = SampleDocuments.Workbook();

        Assert.Throws<OperationCanceledException>(() => ExcelToPdf.Convert(workbook, null, Cancelled()));
        Assert.Throws<OperationCanceledException>(() => ExcelToPdf.Convert(workbook.ToArray(), null, Cancelled()));
        Assert.Throws<OperationCanceledException>(() => ExcelToPdf.ToWord(workbook, null, Cancelled()));
    }

    [Fact]
    public void Excel_to_pdf_with_a_live_token_gives_the_same_bytes_as_without_one()
    {
        var workbook = SampleDocuments.Workbook();
        using var source = new CancellationTokenSource();

        var without = ExcelToPdf.Convert(workbook);
        var with = ExcelToPdf.Convert(workbook, null, source.Token);
        var fromBytes = ExcelToPdf.Convert(workbook.ToArray(), null, source.Token);

        Assert.Equal(without.Pdf, with.Pdf);
        Assert.Equal(ExcelToPdf.Convert(workbook.ToArray()).Pdf, fromBytes.Pdf);
    }

    [Fact]
    public void Html_to_pdf_throws_when_the_token_is_already_cancelled() =>
        Assert.Throws<OperationCanceledException>(() => HtmlToPdf.Convert(Html, null, Cancelled()));

    [Fact]
    public void Html_to_pdf_with_a_live_token_gives_the_same_bytes_as_without_one()
    {
        using var source = new CancellationTokenSource();

        Assert.Equal(HtmlToPdf.Convert(Html).Pdf, HtmlToPdf.Convert(Html, null, source.Token).Pdf);
    }

    [Fact]
    public void Markdown_to_pdf_throws_when_the_token_is_already_cancelled() =>
        Assert.Throws<OperationCanceledException>(() => MarkdownToPdf.Convert(Markdown, null, null, Cancelled()));

    [Fact]
    public void Markdown_to_pdf_with_a_live_token_gives_the_same_bytes_as_without_one()
    {
        using var source = new CancellationTokenSource();

        Assert.Equal(MarkdownToPdf.Convert(Markdown).Pdf, MarkdownToPdf.Convert(Markdown, MarkdownOptions.GitHub, null, source.Token).Pdf);
    }

    [Fact]
    public void Each_block_laid_out_checks_the_token()
    {
        using var source = new CancellationTokenSource();
        var context = new LayoutContext(SampleDocuments.Report(), new PdfDocumentBuilder()) { CancellationToken = source.Token };
        var layout = new BlockLayout(context);
        var blocks = context.Document.Sections[0].Blocks;

        Assert.NotEmpty(layout.Layout(blocks, 400));
        source.Cancel();

        Assert.Throws<OperationCanceledException>(() => layout.Layout(blocks, 400));
    }

    [Fact]
    public void Each_page_opened_checks_the_token()
    {
        // A document without blocks lays nothing out, so the only checkpoint left on the way is the opening of the first page.
        using var source = new CancellationTokenSource();
        var context = new LayoutContext(new WordDocument(), new PdfDocumentBuilder()) { CancellationToken = source.Token };
        Assert.Single(new Paginator(context, new BlockLayout(context)).Run());
        source.Cancel();

        Assert.Throws<OperationCanceledException>(() => new Paginator(context, new BlockLayout(context)).Run());
    }

    [Fact]
    public void A_token_cancelled_after_the_pages_are_laid_out_stops_the_painting_before_the_first_page()
    {
        using var source = new CancellationTokenSource();
        var context = new LayoutContext(SampleDocuments.Report(), new PdfDocumentBuilder()) { CancellationToken = source.Token };
        var pages = WordToPdf.Paginate(context);
        Assert.True(pages.Count > 1);
        var cancelled = 0;
        using var registration = source.Token.Register(() => cancelled++);

        source.Cancel();

        Assert.Equal(1, cancelled);
        Assert.Throws<OperationCanceledException>(() => new PagePainter(context, bookmarks: true).Paint(pages));
    }
}
