// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using System.Text.RegularExpressions;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Conversion.HtmlLayout;
using OmniEurope.Documents.Html;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// HTML to Word reads the single-class rules of the page's own style elements, so the class-based page of Word
/// to HTML converts back like the page with <c>style</c> attributes it replaced.
/// </summary>
public sealed partial class HtmlToWordClassRulesTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Word");

    public static TheoryData<string> Documents => ["rent-receipt-template.docx", "external.docx", "report"];

    [Theory]
    [MemberData(nameof(Documents))]
    public void A_class_based_page_converts_back_like_the_same_page_with_style_attributes(string name)
    {
        var docx = name == "report" ? SampleDocuments.Report().ToArray() : File.ReadAllBytes(Path.Combine(Fixtures, name));
        var result = WordToHtml.Convert(docx);

        // The page as it was written before classes: each generated class back in a style attribute, converted
        // the way style attributes always were (the rules of the style element are not read by Append).
        var legacy = new WordDocument();
        HtmlToWord.Append(HtmlParser.ParseDocument(Inlined(result.Html, result.StyleSheet)).Body, legacy);
        var current = HtmlToWord.Convert(result.Html);
        var withoutRules = new WordDocument();
        HtmlToWord.Append(HtmlParser.ParseDocument(result.Html).Body, withoutRules);

        Assert.Equal(Signature(legacy.Body), Signature(current.Body));
        Assert.NotEqual(Signature(withoutRules.Body), Signature(current.Body));
    }

    [Fact]
    public void Class_rules_apply_in_sheet_order_and_the_style_attribute_wins()
    {
        const string Html = "<html><head><style>/* x{color:#00FF00} */ .x{color:#FF0000;font-size:20pt} @media print{.x{color:#0000FF}} p.y,.z .x{font-style:italic}"
            + " .a, .b {text-align:center} .later{font-size:9pt} @import url(a.css); .big{font-size:30pt}</style></head><body>"
            + "<p class=\"x later\" style=\"font-weight:bold;font-size:12pt\">un</p><p class=\"x later\">deux</p><p class=\"b big y\">trois</p>"
            + "<style>.big{font-size:31pt}</style></body></html>";

        var paragraphs = HtmlToWord.Convert(Html).Body.OfType<WordParagraph>().ToList();

        var one = ((WordText)paragraphs[0].Inlines[0]).Properties;
        Assert.Equal("FF0000", one.Color);
        Assert.True(one.Bold);
        Assert.Equal(12, one.FontSize);
        Assert.Null(one.Italic);
        var two = ((WordText)paragraphs[1].Inlines[0]).Properties;
        Assert.Equal("FF0000", two.Color);
        Assert.Equal(9, two.FontSize);
        Assert.Null(two.Bold);
        Assert.Equal(WordAlignment.Center, paragraphs[2].Properties.Alignment);
        var three = ((WordText)paragraphs[2].Inlines[0]).Properties;
        Assert.Equal(31, three.FontSize);
        Assert.Null(three.Italic);
        Assert.Null(three.Color);
    }

    [Fact]
    public void Base_rules_are_read_too_so_the_highlighted_paragraph_keeps_its_background()
    {
        var document = new WordDocument();
        document.AddParagraph("surligné");
        document.AddParagraph("normal");
        var docx = document.ToArray();
        var address = HtmlParser.ParseDocument(WordToHtml.Convert(docx).Html).Body.QuerySelector("p")!.GetAttribute("data-address");

        var back = HtmlToWord.Convert(WordToHtml.Convert(docx, new WordHtmlOptions { HighlightAddress = address }).Html);
        var runs = back.Body.OfType<WordParagraph>().Select(p => ((WordText)p.Inlines[0]).Properties.Shading).ToList();

        Assert.Equal(["FFF2A8", null], runs);
    }

    [Fact]
    public void A_linked_style_sheet_is_reported_and_never_read()
    {
        var linked = HtmlToWord.Convert("<html><head><link rel=\"preload stylesheet\" href=\"page.css\"></head><body><p class=\"x\">texte</p></body></html>");
        var plain = HtmlToWord.Convert("<html><head><link rel=\"icon\" href=\"i.png\"></head><body><p>texte</p></body></html>");

        Assert.Contains(HtmlToWord.LinkedStyleSheetsGap, linked.Gaps);
        Assert.DoesNotContain(HtmlToWord.LinkedStyleSheetsGap, plain.Gaps);
        Assert.Equal("texte", linked.Text.Trim());
    }

    [Fact]
    public void An_unclosed_block_ends_with_the_sheet_and_no_rules_leave_the_element_as_is()
    {
        var page = HtmlParser.ParseDocument("<style>.ok{color:#112233} }.stray{color:#445566} .open{color:#FF0000</style><p class=\"ok stray open\">x</p>");
        var element = page.QuerySelector("p")!;

        Assert.Equal("#FF0000", CssClassRules.From(page).Of(element)["color"]);
        Assert.Empty(CssClassRules.None.Of(element));
        Assert.Equal("#112233", CssClassRules.From(HtmlParser.ParseDocument("<style>.ok{color:#112233}</style>")).Of(element)["color"]);
    }

    private static string Inlined(string html, string css)
    {
        var rules = HtmlStyleRules.Rules(css);
        return ClassAttribute().Replace(html, match =>
        {
            var classes = match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var generated = classes.Where(rules.ContainsKey).ToList();
            var named = string.Join(' ', classes.Except(generated));
            return (named.Length > 0 ? "class=\"" + named + "\"" : string.Empty) + (generated.Count == 1 ? " style=\"" + rules[generated[0]] + "\"" : string.Empty);
        });
    }

    // Every paragraph's properties and every inline's type, properties, text and size, tables cell by cell.
    private static string Signature(IEnumerable<WordBlock> blocks)
    {
        var text = new StringBuilder();
        foreach (var block in blocks)
        {
            switch (block)
            {
                case WordParagraph paragraph:
                    text.Append("P ").Append(paragraph.StyleId).Append(' ').Append(paragraph.Properties).Append('\n');
                    Inlines(paragraph.Inlines, text);
                    break;
                case WordTable table:
                    text.Append("T ").Append(table.Properties).Append('\n');
                    foreach (var cell in table.Rows.SelectMany(r => r.Cells))
                    {
                        text.Append("C ").Append(cell.Properties).Append('\n').Append(Signature(cell.Blocks));
                    }

                    break;
                default:
                    text.Append(block.GetType().Name).Append('\n');
                    break;
            }
        }

        return text.ToString();
    }

    private static void Inlines(IEnumerable<WordInline> inlines, StringBuilder text)
    {
        foreach (var inline in inlines)
        {
            text.Append("  ").Append(inline.GetType().Name).Append(' ').Append(inline.Properties);
            _ = inline switch
            {
                WordText run => text.Append(" \"").Append(run.Value).Append('"'),
                WordPicture picture => text.Append(' ').Append(picture.Width).Append('x').Append(picture.Height),
                _ => text,
            };
            text.Append('\n');
            if (inline is WordHyperlink link)
            {
                Inlines(link.Inlines, text);
            }
        }
    }

    [GeneratedRegex("class=\"([^\"]*)\"")]
    private static partial Regex ClassAttribute();
}
