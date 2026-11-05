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
    public void The_screen_highlight_of_a_paragraph_is_not_carried_into_the_document()
    {
        var document = new WordDocument();
        document.AddParagraph("surligné");
        document.AddParagraph("normal");
        var docx = document.ToArray();
        var address = HtmlParser.ParseDocument(WordToHtml.Convert(docx).Html).Body.QuerySelector("p")!.GetAttribute("data-address");

        var back = HtmlToWord.Convert(WordToHtml.Convert(docx, new WordHtmlOptions { HighlightAddress = address }).Html);
        var runs = back.Body.OfType<WordParagraph>().Select(p => ((WordText)p.Inlines[0]).Properties.Shading).ToList();

        Assert.Equal([null, null], runs);
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

    [Fact]
    public void A_style_element_for_another_medium_or_language_is_not_read()
    {
        var page = HtmlParser.ParseDocument(
            "<style media=\"print\">.x{display:none}</style><style type=\"text/less\">.x{color:#FF0000}</style>" +
            "<style media=\"screen\">.x{font-weight:bold}</style><style type=\"text/css\" media=\"all\">.x{font-size:12pt}</style><p class=\"x\">kept</p>");

        var css = CssClassRules.From(page).Of(page.QuerySelector("p")!);

        Assert.False(css.ContainsKey("display"));
        Assert.False(css.ContainsKey("color"));
        Assert.Equal("bold", css["font-weight"]);
        Assert.Equal("12pt", css["font-size"]);
        Assert.Contains("kept", HtmlToWord.Convert(page.DocumentElement.OuterHtml).Body.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordText>().Select(t => t.Value));
    }

    [Fact]
    public void A_hostile_sheet_costs_linear_time()
    {
        // 40,000 rules for one class, each with many declarations, and 40,000 elements carrying it (and a unique
        // second class each): merging rule text per element would parse billions of declarations.
        const int Count = 40_000;
        var html = new StringBuilder("<style>");
        for (var i = 0; i < Count; i++)
        {
            html.Append(".a{color:#").Append((i % 0xFFFFFF).ToString("X6", System.Globalization.CultureInfo.InvariantCulture)).Append(";x-").Append(i).Append(":1;y:2}");
        }

        html.Append(".a{font-weight:bold}</style>");
        for (var i = 0; i < Count; i++)
        {
            html.Append("<p class=\"a u").Append(i).Append("\">t</p>");
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var document = HtmlToWord.Convert(html.ToString());
        watch.Stop();

        Assert.Equal(Count, document.Body.OfType<WordParagraph>().Count());
        Assert.True(document.Body.OfType<WordParagraph>().First().Inlines.OfType<WordText>().First().Properties.Bold == true);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20), $"took {watch.Elapsed}");
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
