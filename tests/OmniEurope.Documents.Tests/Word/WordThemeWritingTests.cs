// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Validation;

namespace OmniEurope.Documents.Tests.Word;

/// <summary>A document whose styles name theme fonts keeps a theme when the model is written: without one
/// <c>asciiTheme="minorHAnsi"</c> resolves to nothing and the text is drawn in another face.</summary>
public sealed class WordThemeWritingTests
{
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";

    private const string CustomTheme = """
        <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Custom"><a:themeElements>
        <a:clrScheme name="Custom"><a:dk1><a:sysClr val="windowText" lastClr="000000"/></a:dk1><a:lt1><a:sysClr val="window" lastClr="FFFFFF"/></a:lt1><a:dk2><a:srgbClr val="112233"/></a:dk2><a:lt2><a:srgbClr val="EEEEEE"/></a:lt2><a:accent1><a:srgbClr val="FF00AA"/></a:accent1><a:accent2><a:srgbClr val="00FF11"/></a:accent2><a:accent3><a:srgbClr val="A5A5A5"/></a:accent3><a:accent4><a:srgbClr val="FFC000"/></a:accent4><a:accent5><a:srgbClr val="5B9BD5"/></a:accent5><a:accent6><a:srgbClr val="70AD47"/></a:accent6><a:hlink><a:srgbClr val="0563C1"/></a:hlink><a:folHlink><a:srgbClr val="954F72"/></a:folHlink></a:clrScheme>
        <a:fontScheme name="Custom"><a:majorFont><a:latin typeface="Georgia"/><a:ea typeface=""/><a:cs typeface=""/></a:majorFont><a:minorFont><a:latin typeface="Verdana"/><a:ea typeface=""/><a:cs typeface=""/></a:minorFont></a:fontScheme>
        <a:fmtScheme name="Custom"><a:fillStyleLst><a:solidFill><a:srgbClr val="123456"/></a:solidFill><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:fillStyleLst><a:lnStyleLst><a:ln w="9999"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln><a:ln w="6350"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln><a:ln w="6350"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln></a:lnStyleLst><a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst><a:bgFillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:bgFillStyleLst></a:fmtScheme>
        </a:themeElements></a:theme>
        """;

    private static WordDocument LoadedWithCustomTheme() => WordDocument.Load(DocxFactory.Build(
        "<w:p><w:r><w:t>x</w:t></w:r></w:p>",
        extraParts: new Dictionary<string, string> { ["word/theme/theme1.xml"] = CustomTheme },
        documentRelationships: "<Relationship Id=\"rId9\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme\" Target=\"theme/theme1.xml\"/>",
        contentTypes: new Dictionary<string, string> { ["word/theme/theme1.xml"] = "application/vnd.openxmlformats-officedocument.theme+xml" }));

    private static XElement Written(WordDocument document, string scheme) =>
        XDocument.Parse(DocxFactory.Part(document.ToArray(), "word/theme/theme1.xml")).Descendants(A + scheme).Single();

    [Fact]
    public void A_loaded_theme_keeps_its_colour_and_format_schemes_on_save()
    {
        var document = LoadedWithCustomTheme();
        var original = XDocument.Parse(CustomTheme);

        Assert.True(XNode.DeepEquals(original.Descendants(A + "clrScheme").Single(), Written(document, "clrScheme")));
        Assert.True(XNode.DeepEquals(original.Descendants(A + "fmtScheme").Single(), Written(document, "fmtScheme")));
        Assert.True(XNode.DeepEquals(original.Descendants(A + "fontScheme").Single(), Written(document, "fontScheme")));
    }

    [Fact]
    public void A_font_changed_in_the_model_updates_the_loaded_theme_font_scheme_only()
    {
        var document = LoadedWithCustomTheme();
        document.Styles.MajorFont = "Arial";

        var fonts = Written(document, "fontScheme");
        Assert.Equal("Arial", (string?)fonts.Element(A + "majorFont")!.Element(A + "latin")!.Attribute("typeface"));
        Assert.Equal("Verdana", (string?)fonts.Element(A + "minorFont")!.Element(A + "latin")!.Attribute("typeface"));
        Assert.True(XNode.DeepEquals(XDocument.Parse(CustomTheme).Descendants(A + "clrScheme").Single(), Written(document, "clrScheme")));
        Assert.Equal("Arial", WordDocument.Load(document.ToArray()).Styles.MajorFont);
    }

    [Fact]
    public void A_loaded_document_is_written_with_a_theme_holding_its_fonts()
    {
        var source = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Word", "rent-receipt-template.docx"));
        var document = WordDocument.Load(source);

        var bytes = document.ToArray();

        var theme = XDocument.Parse(DocxFactory.Part(bytes, "word/theme/theme1.xml"));
        Assert.Equal((document.Styles.MajorFont, document.Styles.MinorFont), (
            (string?)theme.Descendants(A + "majorFont").Single().Element(A + "latin")!.Attribute("typeface"),
            (string?)theme.Descendants(A + "minorFont").Single().Element(A + "latin")!.Attribute("typeface")));
        Assert.NotNull(document.Styles.MajorFont);
        Assert.Contains("/word/theme/theme1.xml", DocxFactory.Part(bytes, "[Content_Types].xml"), StringComparison.Ordinal);
        Assert.Contains("relationships/theme", DocxFactory.Part(bytes, "word/_rels/document.xml.rels"), StringComparison.Ordinal);
        Assert.Equal((document.Styles.MajorFont, document.Styles.MinorFont), (WordDocument.Load(bytes).Styles.MajorFont, WordDocument.Load(bytes).Styles.MinorFont));
        var report = WordSchemaValidator.Validate(bytes);
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Errors));
        Assert.Contains("word/theme/theme1.xml", report.CheckedParts);
    }

    [Fact]
    public void A_document_without_theme_fonts_is_written_without_a_theme()
    {
        var document = new WordDocument();
        document.Body.Add(new WordParagraph());

        Assert.DoesNotContain("word/theme/theme1.xml", DocxFactory.Entries(document.ToArray()).Keys);
    }
}
