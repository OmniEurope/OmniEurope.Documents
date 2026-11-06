// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

public sealed class WordCalibriGapTests
{
    [Fact]
    public void Calibri_Light_drawn_with_Carlito_is_reported_as_a_gap_and_Calibri_is_not()
    {
        var result = WordToPdf.Convert(Document("Calibri", "Calibri Light"));

        Assert.Contains(result.Gaps, g => g.Contains("\"Calibri Light\" replaced by Carlito", StringComparison.Ordinal) && g.Contains("no light weight", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Gaps, g => g.Contains("\"Calibri\"", StringComparison.Ordinal));
    }

    [Fact]
    public void The_rent_receipt_template_reports_its_Calibri_Light_headings()
    {
        var docx = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Word", "rent-receipt-template.docx"));

        var result = WordToPdf.Convert(docx);

        Assert.Contains(result.Gaps, g => g.Contains("\"Calibri Light\" replaced by Carlito", StringComparison.Ordinal));
    }

    [Fact]
    public void A_replaced_font_gap_carries_its_code_arguments_and_a_French_description()
    {
        var details = WordToPdf.Convert(Document("Police Imaginaire", "Calibri Light")).GapDetails;

        var replaced = Assert.Single(details, g => g.Code == "PdfFontReplaced");
        Assert.Equal(["Police Imaginaire", "LiberationSerif"], replaced.Arguments);
        Assert.Equal("font \"Police Imaginaire\" replaced by LiberationSerif", replaced.Message);
        Assert.Equal("police « Police Imaginaire » remplacée par LiberationSerif", replaced.Describe(CultureInfo.GetCultureInfo("fr-FR")));
        var light = Assert.Single(details, g => g.Code == "PdfCalibriLightReplaced");
        Assert.Equal("font \"Calibri Light\" replaced by Carlito Regular (same widths, but no light weight: drawn heavier)", light.Message);
        Assert.Equal("police « Calibri Light » remplacée par Carlito Regular (mêmes largeurs, mais sans graisse légère : dessinée plus grasse)", light.Describe(CultureInfo.GetCultureInfo("fr-FR")));
    }

    [Fact]
    public void A_font_registered_under_the_name_is_not_reported()
    {
        var fonts = new FontLibrary();
        fonts.Register(FontLibrary.Bundled("Carlito", false, false).Data, "Calibri");

        var result = WordToPdf.Convert(Document("Calibri", "Calibri Light"), new WordPdfOptions { Fonts = fonts });

        Assert.DoesNotContain(result.Gaps, g => g.Contains("\"Calibri\"", StringComparison.Ordinal));
        Assert.Contains(result.Gaps, g => g.Contains("\"Calibri Light\"", StringComparison.Ordinal));
    }

    private static WordDocument Document(params string[] fonts)
    {
        var document = new WordDocument();
        foreach (var font in fonts)
        {
            var paragraph = new WordParagraph();
            paragraph.Inlines.Add(new WordText("Texte " + font, new WordRunProperties { Font = font, FontSize = 11 }));
            document.Body.Add(paragraph);
        }

        return document;
    }
}
