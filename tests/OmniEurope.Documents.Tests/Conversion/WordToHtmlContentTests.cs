// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Conversion.WordHtml;
using OmniEurope.Documents.Html;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Tests.Imaging;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

public sealed class WordToHtmlContentTests
{
    private static readonly string Images = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images");

    [Fact]
    public void Nothing_in_the_document_can_add_markup_or_script()
    {
        var document = new WordDocument { Information = new WordInformation { Title = "</title><script>alert(1)</script>" } };
        document.Styles.DefaultRunProperties = document.Styles.DefaultRunProperties with { Language = "fr\"><script>" };
        document.AddParagraph("<script>alert('x')</script> & \"guillemets\"");
        var styled = new WordRunProperties { Font = "x'</style><script>alert(2)</script>", Color = "red;background:url(javascript:alert(3))", Highlight = "<b>", Shading = "\"><script>" };
        document.AddParagraph().AddText("police", styled);
        var links = document.AddParagraph();
        links.Add(new WordHyperlink("javascript:alert(4)", "piège"));
        links.Add(new WordHyperlink("java\tscript:alert(5)", "piège tabulé"));
        links.Add(new WordHyperlink(" JAVASCRIPT:alert(6)", "piège majuscule"));
        links.Add(new WordHyperlink("data:text/html,<script>alert(7)</script>", "piège data"));
        links.Add(new WordHyperlink(null, "signet", "\"><script>alert(8)</script>"));
        document.AddParagraph().Add(new WordPicture(WordImage.FromBytes(SampleDocuments.Png), 10, 10) { Description = "\" onerror=\"alert(9)" });
        var list = document.Numbering.AddBulletList();
        document.Numbering.Abstracts[document.Numbering.Instances[list].AbstractId].Levels[0] = new WordNumberingLevel(0) { Format = WordNumberFormat.Bullet, Text = "<img src=x onerror=alert(10)>" };
        document.AddParagraph("puce").AsListItem(list);

        var html = WordToHtml.Convert(document).Html;
        var page = HtmlParser.ParseDocument(html);

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror=\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img src=x", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(page.DescendantElements(), e => e.TagName is "script" or "b");
        Assert.Equal(["img"], page.Body.DescendantElements().Where(e => e.TagName == "img").Select(e => e.TagName));
        Assert.Equal("</title><script>alert(1)</script>", page.Title);
        Assert.Contains("<script>alert('x')</script> & \"guillemets\"", page.Body.TextContent, StringComparison.Ordinal);
        Assert.Equal("<img src=x onerror=alert(10)>", page.Body.QuerySelector(".omni-label")!.TextContent);
        Assert.Equal(["#%22%3E%3Cscript%3Ealert(8)%3C/script%3E"], page.Body.QuerySelectorAll("a[href]").Select(a => a.GetAttribute("href")));
        Assert.Equal("frscript", page.DocumentElement.GetAttribute("lang"));
        var font = page.Body.QuerySelectorAll("span[style]").Single(s => s.TextContent == "police");
        Assert.Equal("font-family:'xstylescriptalert2script',sans-serif", font.GetAttribute("style"));
        Assert.Equal("\" onerror=\"alert(9)", page.Body.QuerySelector("img")!.GetAttribute("alt"));
    }

    [Fact]
    public void Only_web_and_mail_links_keep_their_address()
    {
        var document = new WordDocument();
        var paragraph = document.AddParagraph();
        paragraph.Add(new WordHyperlink("https://example.org/a b?q=é", "web"));
        paragraph.Add(new WordHyperlink("mailto:contact@example.org", "courriel"));
        paragraph.Add(new WordHyperlink(null, "interne", "Chapitre_2"));
        paragraph.Add(new WordHyperlink("autre.docx", "relatif"));
        paragraph.Add(new WordHyperlink("file:///c:/secret.txt", "fichier"));

        var result = WordToHtml.Convert(document);
        var anchors = HtmlParser.ParseDocument(result.Html).Body.QuerySelectorAll("a");

        Assert.Equal(["https://example.org/ab?q=%C3%A9", "mailto:contact@example.org", "#Chapitre_2"], anchors.Select(a => a.GetAttribute("href")));
        Assert.Equal(["web", "courriel", "interne"], anchors.Select(a => a.TextContent));
        Assert.Contains(">relatif<", result.Html, StringComparison.Ordinal);
        Assert.Contains("links with an address other than http, https or mailto are left without their address", result.Gaps);
        Assert.Contains("bookmarks are not kept: links to them point to anchors the page does not define", result.Gaps);
    }

    [Theory]
    [InlineData("rgb.png", "image/png")]
    [InlineData("plain.jpg", "image/jpeg")]
    [InlineData("indexed.gif", "image/gif")]
    [InlineData("rgb.bmp", "image/bmp")]
    public void Web_pictures_go_as_they_are_under_the_type_their_bytes_show(string file, string type)
    {
        var bytes = File.ReadAllBytes(Path.Combine(Images, file));
        var document = new WordDocument();
        document.AddParagraph().Add(new WordPicture(new WordImage(bytes, "application/octet-stream"), 30, 20) { Description = "Image" });

        var image = HtmlParser.ParseDocument(WordToHtml.Convert(document).Html).Body.QuerySelector("img")!;

        Assert.Equal("data:" + type + ";base64," + Convert.ToBase64String(bytes), image.GetAttribute("src"));
        Assert.Equal("Image", image.GetAttribute("alt"));
        Assert.Equal("width:30pt;height:20pt", image.GetAttribute("style"));
    }

    [Fact]
    public void Tiff_and_metafile_pictures_become_png_and_others_are_reported()
    {
        var tiff = File.ReadAllBytes(Path.Combine(Images, "rgb-none.tif"));
        var emf = new EmfFixture().Brush(1, 0xFF0000).Select(1).Ints(43, 0, 0, 399, 199).ToArray();
        byte[] wmf = [0xD7, 0xCD, 0xC6, 0x9A, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        var document = new WordDocument();
        document.AddParagraph().Add(new WordPicture(WordImage.FromBytes(tiff), 40, 40));
        document.AddParagraph().Add(new WordPicture(WordImage.FromBytes(emf), 60, 30));
        document.AddParagraph().Add(new WordPicture(WordImage.FromBytes(wmf), 20, 20));
        document.AddParagraph().Add(new WordPicture(new WordImage(Encoding.ASCII.GetBytes("pas une image"), "image/x\"<weird>"), 20, 20));

        var result = WordToHtml.Convert(document);
        var images = HtmlParser.ParseDocument(result.Html).Body.QuerySelectorAll("img");

        Assert.Equal(2, images.Count);
        var fromTiff = PngCodec.Decode(Payload(images[0]));
        var source = ImageDecoder.Decode(tiff);
        Assert.Equal((source.Width, source.Height), (fromTiff.Width, fromTiff.Height));
        Assert.Equal(source.GetRgba(1, 1), fromTiff.GetRgba(1, 1));
        var fromEmf = PngCodec.Decode(Payload(images[1]));
        Assert.Equal((120, 60), (fromEmf.Width, fromEmf.Height));
        var (r, g, b, _) = fromEmf.GetRgba(60, 30);
        Assert.True(b > 200 && r < 60 && g < 60, $"the metafile rectangle is drawn in blue ({r}, {g}, {b})");
        Assert.Contains("picture format image/x-wmf left out", result.Gaps);
        Assert.Contains("picture format image/xweird left out", result.Gaps);
    }

    [Fact]
    public void A_damaged_picture_is_left_out_and_reported()
    {
        // A TIFF whose header reads well but whose compression (99) is unknown: decoding it fails.
        var tiff = TiffWriter.Write(true, new TiffWriter.Page(1, 1, [(258, TiffWriter.Short, [8]), (259, TiffWriter.Short, [99]), (262, TiffWriter.Short, [1])], [0]));
        var document = new WordDocument();
        document.AddParagraph().Add(new WordPicture(new WordImage(tiff, "image/tiff"), 10, 10));

        var result = WordToHtml.Convert(document);

        Assert.DoesNotContain("<img", result.Html, StringComparison.Ordinal);
        Assert.Contains("unreadable picture left out", result.Gaps);
    }

    [Fact]
    public void Text_boxes_follow_their_paragraph_as_blocks_and_breaks_tabs_and_symbols_are_kept()
    {
        var document = new WordDocument();
        var box = new WordTextBox(120, 40);
        box.Blocks.Add(new WordParagraph("Dans la boîte"));
        document.AddParagraph("Avant\tla boîte").Add(box).Add(new WordBreak(WordBreakKind.Column)).AddText("suite");
        document.AddParagraph().Add(new WordSymbol("Symbol", '\uF061')).AddText("\uF062", new WordRunProperties { Font = "Wingdings" });

        var result = WordToHtml.Convert(document);
        var section = HtmlParser.ParseDocument(result.Html).Body.QuerySelector(".omni-section")!;
        var children = section.Children.ToList();

        Assert.Equal(["p", "div", "p"], children.Select(c => c.TagName));
        Assert.Equal("Avant\tla boîtesuite", children[0].TextContent);
        Assert.NotNull(children[0].QuerySelector("br"));
        Assert.Equal("omni-textbox", children[1].GetAttribute("class"));
        Assert.Equal("width:120pt;min-height:40pt", children[1].GetAttribute("style"));
        Assert.Equal("Dans la boîte", children[1].QuerySelector("p")!.TextContent);
        Assert.Equal("α•", children[2].TextContent);
        Assert.Contains("tab stops shown as tab characters of fixed width", result.Gaps);
        Assert.Contains("column breaks shown as line breaks", result.Gaps);
    }

    [Fact]
    public void Contextual_spacing_drops_the_space_between_paragraphs_of_the_same_style()
    {
        var document = new WordDocument();
        var spaced = new WordParagraphProperties { StyleId = "ListParagraph", SpacingBefore = 4, SpacingAfter = 6 };
        document.AddParagraph("Avant");
        document.Body.Add(new WordParagraph("Premier") { Properties = spaced });
        document.Body.Add(new WordParagraph("Second") { Properties = spaced });
        document.AddParagraph("Après");

        var paragraphs = HtmlParser.ParseDocument(WordToHtml.Convert(document).Html).Body.QuerySelectorAll(".omni-section > p");

        Assert.Contains("margin-top:4pt;font", paragraphs[1].GetAttribute("style"), StringComparison.Ordinal);
        Assert.DoesNotContain("margin-bottom", paragraphs[1].GetAttribute("style"), StringComparison.Ordinal);
        Assert.DoesNotContain("margin-top", paragraphs[2].GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("margin-bottom:6pt", paragraphs[2].GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact]
    public void Encoding_drops_what_html_does_not_allow_and_keeps_surrogate_pairs()
    {
        var output = new StringBuilder();

        HtmlOutput.Encode("a\u0001b\u0085c\uD800d\uFFFEe\t\n😀<&>\"'", output);

        Assert.Equal("abcde\t\n😀&lt;&amp;&gt;&quot;'", output.ToString());
    }

    [Fact]
    public void Less_common_formatting_has_its_css_too()
    {
        var document = new WordDocument();
        var paragraph = new WordParagraph
        {
            Properties = new WordParagraphProperties
            {
                Alignment = WordAlignment.Distribute,
                RightToLeft = true,
                Borders = new WordParagraphBorders(new WordBorder("dotted"), new WordBorder("dashed"), new WordBorder("none"), new WordBorder("single", 0.1, 0, "auto")),
            },
        };
        foreach (var (text, run) in new (string, WordRunProperties)[]
        {
            ("pointillé", new() { Underline = WordUnderline.Dotted }),
            ("tirets", new() { Underline = WordUnderline.Dash }),
            ("vague", new() { Underline = WordUnderline.Wave }),
            ("épais", new() { Underline = WordUnderline.Thick }),
            ("mots", new() { Underline = WordUnderline.Words }),
            ("double", new() { DoubleStrike = true }),
            ("petites", new() { SmallCaps = true, Position = 3 }),
            ("symboles", new() { Font = "@#!" }),
            ("ombré", new() { Shading = "ABCDEF", Color = "auto" }),
        })
        {
            paragraph.AddText(text, run);
        }

        document.Body.Add(paragraph);
        document.AddParagraph();
        var table = new WordTable(80);
        table.AddRow("bas").Cells[0].Properties = new WordTableCellProperties { VerticalAlignment = WordCellAlignment.Bottom, NoWrap = true };
        document.AddTable(table);

        var html = WordToHtml.Convert(document).Html;
        var page = HtmlParser.ParseDocument(html);
        var first = page.Body.QuerySelector(".omni-section > p")!;

        Assert.StartsWith("text-align:justify;line-height:1.242;border-top:0.5pt dotted #000000;border-left:0.5pt dashed #000000;border-right:0.25pt solid #000000;direction:rtl;", first.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("<u style=\"text-decoration-style:dotted\">pointillé</u><u style=\"text-decoration-style:dashed\">tirets</u><u style=\"text-decoration-style:wavy\">vague</u><u style=\"text-decoration-thickness:2px\">épais</u><u>mots</u>", html, StringComparison.Ordinal);
        Assert.Contains("<s style=\"text-decoration-style:double\">double</s>", html, StringComparison.Ordinal);
        Assert.Contains("<span style=\"font-variant:small-caps;vertical-align:3pt\">petites</span>", html, StringComparison.Ordinal);
        Assert.Contains(">symboles<", html, StringComparison.Ordinal);
        Assert.Contains("<span style=\"background-color:#ABCDEF\">ombré</span>", html, StringComparison.Ordinal);
        Assert.Equal("<br>", page.Body.QuerySelectorAll(".omni-section > p")[1].InnerHtml);
        Assert.Contains("vertical-align:bottom;white-space:nowrap", page.Body.QuerySelector("td")!.GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_document_without_sections_gives_an_empty_page()
    {
        var document = new WordDocument();
        document.AddParagraph("Seul").Add(document.AddFootnote("note"));
        document.Sections.Clear();

        var page = HtmlParser.ParseDocument(WordToHtml.Convert(document).Html);

        Assert.Empty(page.Body.QuerySelectorAll(".omni-section, .omni-notes, header, footer"));
    }

    private static byte[] Payload(HtmlElement image)
    {
        var source = image.GetAttribute("src")!;
        Assert.StartsWith("data:image/png;base64,", source, StringComparison.Ordinal);
        return Convert.FromBase64String(source[(source.IndexOf(',', StringComparison.Ordinal) + 1)..]);
    }
}
