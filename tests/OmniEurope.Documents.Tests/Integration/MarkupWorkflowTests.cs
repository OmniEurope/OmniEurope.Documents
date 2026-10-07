// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Html;
using OmniEurope.Documents.Markdown;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Layout;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Integration;

/// <summary>
/// Markdown and HTML through their whole life: written, parsed, rendered to HTML, cleaned, converted to Word
/// and PDF, and read back; hostile input stays inert at every step.
/// </summary>
public sealed class MarkupWorkflowTests
{
    private const string Tricky = "Prix *hors* taxes & <b>remise</b> [1] `x` # 5 | 3";

    [Fact]
    public void Written_markdown_parses_back_to_the_same_text()
    {
        var markdown = Written();

        var document = MarkdownParser.Parse(markdown, MarkdownOptions.GitHub);

        var blocks = document.Children;
        var heading = Assert.IsType<MarkdownHeading>(blocks[0]);
        Assert.Equal((1, "Compte rendu"), (heading.Level, Plain(heading.Inlines)));
        Assert.Equal(Tricky, Plain(Assert.IsType<MarkdownParagraph>(blocks[1]).Inlines));
        var bullets = Assert.IsType<MarkdownList>(blocks[2]);
        Assert.False(bullets.IsOrdered);
        Assert.Equal(["Premier *point*", "1. pas une liste"], bullets.Children.Select(Item));
        var steps = Assert.IsType<MarkdownList>(blocks[3]);
        Assert.True(steps.IsOrdered);
        Assert.Equal(["Ouvrir", "Signer"], steps.Children.Select(Item));
        var table = Assert.IsType<MarkdownTable>(blocks[4]);
        Assert.Equal(["Poste", "Montant"], table.Header.Select(Plain));
        Assert.Equal([MarkdownTableAlignment.Left, MarkdownTableAlignment.Right], table.Alignments);
        Assert.Equal(["a | b", "12,50"], table.Rows[0].Select(Plain));
        Assert.Equal(["", "3"], table.Rows[1].Select(Plain));
    }

    [Fact]
    public void Markdown_goes_to_html_word_and_pdf_with_its_structure()
    {
        var markdown = Written();

        var html = HtmlParser.ParseDocument(MarkdownRenderer.ToHtml(markdown, MarkdownOptions.GitHub));
        Assert.Equal("Compte rendu", html.QuerySelector("h1")?.TextContent);
        Assert.Equal(Tricky, html.QuerySelector("p")?.TextContent);
        Assert.Equal(4, html.QuerySelectorAll("td").Count);
        Assert.Equal("right", html.QuerySelector("td:last-child")?.GetAttribute("align") ?? Style(html.QuerySelector("td:last-child")));

        var word = WordDocument.Load(MarkdownToWord.Convert(markdown).ToArray());
        var paragraphs = word.Blocks.OfType<WordParagraph>().ToList();
        Assert.Equal("Heading1", paragraphs[0].StyleId);
        Assert.Equal(Tricky, paragraphs[1].Text);
        Assert.Equal(4, paragraphs.Count(p => p.Properties.NumberingId is not null));
        var table = Assert.Single(word.Blocks.OfType<WordTable>());
        Assert.Equal("Poste\tMontant\na | b\t12,50\n\t3", table.Text);

        var conversion = MarkdownToPdf.Convert(markdown);
        var pdf = PdfDocument.Open(conversion.Pdf);
        Samples.InOrder(Samples.AllText(pdf), "Compte rendu", Tricky, "Premier *point*", "1. pas une liste", "Ouvrir", "Signer", "Poste", "a | b", "12,50");
        var lines = PdfLayoutAnalyzer.AnalyzePage(pdf.GetPage(1)).Blocks.SelectMany(b => b.Lines).ToList();
        Assert.True(lines.Single(l => l.Text == "Compte rendu").FontSize > lines.First(l => l.Text.StartsWith("Prix", StringComparison.Ordinal)).FontSize);
    }

    [Fact]
    public void Hostile_markdown_and_html_stay_inert_up_to_the_pdf()
    {
        const string markdown = """
            # Titre

            <script>alert('md')</script>

            [clic](javascript:alert(1)) et ![image](data:text/html,<b>x</b>) et <img src=x onerror=alert(2)>
            """;

        var rendered = MarkdownRenderer.ToHtml(markdown, MarkdownOptions.GitHub);
        Assert.DoesNotContain("<script", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img src=\"x\"", rendered, StringComparison.OrdinalIgnoreCase);

        const string page = """
            <html><head><title>Avis</title><style>p{color:red}</style></head><body>
            <h1 onclick="steal()">Avis</h1>
            <p>Texte <a href="javascript:steal()">lien</a> <img src="x" onerror="steal()"> fin.</p>
            <script>document.write('injecté')</script>
            <iframe src="https://example.org/"></iframe>
            </body></html>
            """;
        var clean = new HtmlSanitizer().Sanitize(page);
        var parsed = HtmlParser.ParseFragment(clean);
        Assert.Empty(parsed.QuerySelectorAll("script, iframe, style"));
        Assert.DoesNotContain(parsed.DescendantElements(), e => e.HasAttribute("onclick") || e.HasAttribute("onerror"));
        Assert.DoesNotContain(parsed.DescendantElements(), e => (e.GetAttribute("href") ?? string.Empty).StartsWith("javascript", StringComparison.OrdinalIgnoreCase));

        // No script, style or javascript: link reaches either PDF, not even as a link annotation.
        var fromHtml = HtmlToPdf.Convert(clean).Pdf;
        var fromMarkdown = MarkdownToPdf.Convert(markdown).Pdf;
        foreach (var pdf in new[] { fromHtml, fromMarkdown })
        {
            var raw = System.Text.Encoding.Latin1.GetString(pdf);
            Assert.DoesNotContain("javascript", raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/JS", raw, StringComparison.Ordinal);
        }

        var htmlText = Samples.AllText(PdfDocument.Open(fromHtml));        Samples.InOrder(htmlText, "Avis", "Texte", "lien", "fin.");
        Assert.DoesNotContain("injecté", htmlText, StringComparison.Ordinal);
        Assert.DoesNotContain("steal", htmlText, StringComparison.Ordinal);
        Assert.DoesNotContain("color:red", htmlText, StringComparison.Ordinal);

        // Raw HTML in Markdown is not allowed by default: inline tags show as literal text, never as markup.
        var markdownText = Samples.AllText(PdfDocument.Open(fromMarkdown));
        Samples.InOrder(markdownText, "Titre", "clic", "<img src=x onerror=alert(2)>");
        Assert.Empty(PdfDocument.Open(fromMarkdown).Pages.SelectMany(p => p.Images));
    }

    private static string Written() => new MarkdownWriter()
        .Heading(1, "Compte rendu")
        .Paragraph(Tricky)
        .List(["Premier *point*", "1. pas une liste"])
        .List(["Ouvrir", "Signer"], ordered: true)
        .Table(["Poste", "Montant"], [["a | b", "12,50"], [null, "3"]], [MarkdownTableAlignment.Left, MarkdownTableAlignment.Right])
        .ToString();

    private static string Item(MarkdownBlock item) =>
        Plain(Assert.IsType<MarkdownParagraph>(Assert.Single(Assert.IsType<MarkdownListItem>(item).Children)).Inlines);

    private static string Style(HtmlElement? cell) => cell?.GetAttribute("style")?.Replace("text-align:", string.Empty, StringComparison.Ordinal).Trim(' ', ';') ?? string.Empty;

    // The text a reader sees: literal text and code, containers flattened.
    private static string Plain(IEnumerable<MarkdownInline> inlines) => string.Concat(inlines.Select(i => i switch
    {
        MarkdownText t => t.Text,
        MarkdownCode c => "`" + c.Code + "`",
        MarkdownContainerInline container => Plain(container.Children),
        _ => string.Empty,
    }));
}
