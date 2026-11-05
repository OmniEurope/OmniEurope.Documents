// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Conversion.WordHtml;
using OmniEurope.Documents.Html;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// Word to HTML under a strict Content-Security-Policy (<c>style-src 'self'</c> or a nonce, <c>img-src 'self'</c>):
/// no <c>style</c> attribute, the formatting of each element as a shared class of one style sheet, the sheet
/// embedded with an optional checked nonce or linked, pictures embedded or served apart.
/// </summary>
public sealed class WordToHtmlCspTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Word");

    public static TheoryData<string> Documents => ["rent-receipt-template.docx", "external.docx", "report"];

    [Theory]
    [MemberData(nameof(Documents))]
    public void No_element_carries_a_style_attribute_and_every_generated_class_has_its_rule(string name)
    {
        var docx = name == "report" ? SampleDocuments.Report().ToArray() : File.ReadAllBytes(Path.Combine(Fixtures, name));
        var options = new WordHtmlOptions { MarkContentControls = true, Revisions = WordHtmlRevisions.Marked, HighlightAddress = "word/document.xml#0" };

        foreach (var result in new[] { WordToHtml.Convert(docx), WordToHtml.Convert(docx, options) })
        {
            var page = HtmlParser.ParseDocument(result.Html);
            Assert.DoesNotContain(" style=", result.Html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(page.DescendantElements(), e => e.GetAttribute("style") is not null);
            var rules = HtmlStyleRules.Rules(result.StyleSheet);
            var used = page.Body.DescendantElements().SelectMany(e => e.ClassList).Where(c => c.StartsWith(HtmlStyleClasses.Prefix, StringComparison.Ordinal) && char.IsAsciiDigit(c[^1])).ToHashSet();
            Assert.NotEmpty(used);
            Assert.Subset(rules.Keys.ToHashSet(), used);
            Assert.Equal(result.StyleSheet, page.Head.QuerySelector("style")!.TextContent);
        }
    }

    [Fact]
    public void Identical_formatting_shares_one_class_and_each_rule_is_written_once()
    {
        var document = new WordDocument();
        var centred = new WordParagraphProperties { Alignment = WordAlignment.Center, SpacingAfter = 6 };
        document.Body.Add(new WordParagraph("un") { Properties = centred });
        document.Body.Add(new WordParagraph("deux") { Properties = centred });
        document.Body.Add(new WordParagraph("trois") { Properties = centred with { Alignment = WordAlignment.Right } });
        var red = new WordRunProperties { Color = "FF0000" };
        document.AddParagraph().AddText("a", red).AddText(" ").AddText("b", red with { Bold = true });

        var result = WordToHtml.Convert(document);
        var page = HtmlParser.ParseDocument(result.Html);
        var paragraphs = page.Body.QuerySelectorAll(".omni-section > p");

        Assert.Equal(HtmlStyleRules.Of(paragraphs[0]), HtmlStyleRules.Of(paragraphs[1]));
        Assert.Equal(paragraphs[0].GetAttribute("class"), paragraphs[1].GetAttribute("class"));
        Assert.NotEqual(paragraphs[0].GetAttribute("class"), paragraphs[2].GetAttribute("class"));
        Assert.StartsWith("text-align:center;", HtmlStyleRules.Of(paragraphs[0]), StringComparison.Ordinal);
        Assert.StartsWith("text-align:right;", HtmlStyleRules.Of(paragraphs[2]), StringComparison.Ordinal);
        var spans = paragraphs[3].QuerySelectorAll("span");
        Assert.Equal(2, spans.Count);
        Assert.Equal(spans[0].GetAttribute("class"), spans[1].GetAttribute("class"));
        Assert.Equal("color:#FF0000", HtmlStyleRules.Of(spans[0]));
        var rules = HtmlStyleRules.Rules(result.StyleSheet);
        Assert.Equal(rules.Count, rules.Values.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(page.Body.DescendantElements().Select(HtmlStyleRules.Of).OfType<string>().Distinct(StringComparer.Ordinal).Count(), rules.Count);
    }

    [Fact]
    public void The_embedded_sheet_carries_a_nonce_and_a_linked_sheet_replaces_it()
    {
        var docx = File.ReadAllBytes(Path.Combine(Fixtures, "rent-receipt-template.docx"));

        var embedded = WordToHtml.Convert(docx, new WordHtmlOptions { StyleSheetNonce = "r4nd0m+/-_A==" });
        var style = Assert.Single(HtmlParser.ParseDocument(embedded.Html).Head.QuerySelectorAll("style"));
        Assert.Equal("r4nd0m+/-_A==", style.GetAttribute("nonce"));
        Assert.Contains("<style nonce=\"r4nd0m+/-_A==\">", embedded.Html, StringComparison.Ordinal);

        var linked = WordToHtml.Convert(docx, new WordHtmlOptions { StyleSheetHref = "/documents/7/page.css?v=2&x=1", StyleSheetNonce = "abc" });
        var head = HtmlParser.ParseDocument(linked.Html).Head;
        Assert.Empty(head.QuerySelectorAll("style"));
        var link = Assert.Single(head.QuerySelectorAll("link"));
        Assert.Equal(("stylesheet", "/documents/7/page.css?v=2&x=1", "abc"), (link.GetAttribute("rel"), link.GetAttribute("href"), link.GetAttribute("nonce")));
        Assert.Contains("href=\"/documents/7/page.css?v=2&amp;x=1\"", linked.Html, StringComparison.Ordinal);
        Assert.Equal(embedded.StyleSheet, linked.StyleSheet);
        Assert.StartsWith("html{background:#ffffff}", linked.StyleSheet, StringComparison.Ordinal);
        Assert.Contains(".omni-highlight{", linked.StyleSheet, StringComparison.Ordinal);
        Assert.Subset(HtmlStyleRules.Rules(linked.StyleSheet).Keys.ToHashSet(), HtmlParser.ParseDocument(linked.Html).Body.DescendantElements().SelectMany(e => e.ClassList).Where(c => c.StartsWith(HtmlStyleClasses.Prefix, StringComparison.Ordinal) && char.IsAsciiDigit(c[^1])).ToHashSet());
        Assert.DoesNotContain(" style=", linked.Html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"><script>alert(1)</script>")]
    [InlineData("")]
    [InlineData("===")]
    [InlineData("abc===")]
    [InlineData("ab=c")]
    [InlineData("a b")]
    [InlineData("é")]
    public void A_nonce_outside_the_csp_base64_grammar_is_refused(string nonce)
    {
        Assert.Throws<ArgumentException>(() => new WordHtmlOptions { StyleSheetNonce = nonce });
        Assert.Throws<ArgumentException>(() => new ExcelHtmlOptions { StyleSheetNonce = nonce });
    }

    [Fact]
    public void Pictures_can_be_served_apart_under_a_prefix()
    {
        var png = SampleDocuments.Png;
        var jpeg = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "plain.jpg"));
        var tiff = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "rgb-none.tif"));
        var document = new WordDocument();
        var shared = WordImage.FromBytes(png);
        document.AddParagraph().Add(new WordPicture(shared, 30, 20) { Description = "un" });
        document.AddParagraph().Add(new WordPicture(WordImage.FromBytes(jpeg), 30, 20));
        document.AddParagraph().Add(new WordPicture(shared, 15, 10));
        document.AddParagraph().Add(new WordPicture(WordImage.FromBytes(tiff), 40, 40));

        var result = WordToHtml.Convert(document, new WordHtmlOptions { ImageUrlPrefix = "/documents/7/images/" });
        var images = HtmlParser.ParseDocument(result.Html).Body.QuerySelectorAll("img");

        Assert.DoesNotContain("data:", result.Html, StringComparison.Ordinal);
        Assert.Equal(
            ["/documents/7/images/image1.png", "/documents/7/images/image2.jpg", "/documents/7/images/image1.png", "/documents/7/images/image3.png"],
            images.Select(i => i.GetAttribute("src")));
        Assert.Equal(["image1.png", "image2.jpg", "image3.png"], result.Images.Select(i => i.Name));
        Assert.Equal(["image/png", "image/jpeg", "image/png"], result.Images.Select(i => i.MediaType));
        Assert.Equal(png, result.Images[0].Data);
        Assert.NotSame(png, result.Images[0].Data);
        Assert.Equal(jpeg, result.Images[1].Data);
        var (fromTiff, source) = (PngCodec.Decode(result.Images[2].Data), ImageDecoder.Decode(tiff));
        Assert.Equal((source.Width, source.Height), (fromTiff.Width, fromTiff.Height));
        Assert.Equal("width:15pt;height:10pt", HtmlStyleRules.Of(images[2]));

        var embedded = WordToHtml.Convert(document);
        Assert.Empty(embedded.Images);
        Assert.All(HtmlParser.ParseDocument(embedded.Html).Body.QuerySelectorAll("img"), i => Assert.StartsWith("data:image/", i.GetAttribute("src"), StringComparison.Ordinal));
    }

    [Fact]
    public void Document_values_cannot_reach_the_style_sheet_as_markup_or_rules()
    {
        var document = new WordDocument();
        var styled = new WordRunProperties { Font = "x'}</style><script>alert(2)</script>{", Color = "red;}p{x", Shading = "\"><style>" };
        document.AddParagraph().AddText("police", styled);

        var result = WordToHtml.Convert(document);

        Assert.DoesNotContain("<", result.StyleSheet, StringComparison.Ordinal);
        Assert.DoesNotContain("red", result.StyleSheet, StringComparison.Ordinal);
        Assert.Contains("{font-family:'xstylescriptalert2script',sans-serif}", result.StyleSheet, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => new HtmlStyleClasses().Of("color:red}p{color:blue"));
        Assert.Throws<InvalidOperationException>(() => new HtmlStyleClasses().Of("x</style>"));
    }
}
