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
