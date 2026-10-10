// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Conversion.WordLayout;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Text;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// Symbol and Wingdings text in a Word document laid out to PDF: Symbol characters advance by the widths of
/// Adobe's Symbol metrics (thousandths of an em times the size), Wingdings characters are drawn with Noto Sans
/// Symbols 2 on a Liberation Sans line. Positions, fonts and text are read back from the PDF.
/// </summary>
public sealed class WordSymbolFontTests
{
    private const double Size = 12;

    [Fact]
    public void A_symbol_bullet_advances_by_the_symbol_font_width()
    {
        var letters = Letters(BulletDocument("Symbol", ''));

        var bullet = letters.Single(l => l.Value == "•");
        var x = letters.Single(l => l.Value == "x");

        // Symbol 0xB7 (bullet) is 460/1000 em wide; Liberation Sans' own bullet is narrower.
        Assert.Equal(460 * Size / 1000, x.X - bullet.X, 2);
        var sans = FontLibrary.Bundled("LiberationSans", false, false);
        Assert.NotEqual(sans.GetAdvanceWidth(sans.GetGlyphIndex('•')) * Size / sans.UnitsPerEm, x.X - bullet.X, 2);
        Assert.Contains("LiberationSans", bullet.FontName, StringComparison.Ordinal);
    }

    [Fact]
    public void Underlined_symbol_text_advances_letter_by_letter_by_the_symbol_widths()
    {
        var document = new WordDocument();
        var paragraph = document.AddParagraph();
        paragraph.Add(new WordText("ab\uF0B8") { Properties = new WordRunProperties { Font = "Symbol", FontSize = Size, Underline = WordUnderline.Single, Strike = true } });
        paragraph.Add(new WordText("x") { Properties = new WordRunProperties { Font = "Arial", FontSize = Size } });

        var letters = Letters(document);

        var alpha = letters.Single(l => l.Value == "α");
        var beta = letters.Single(l => l.Value == "β");
        var divide = letters.Single(l => l.Value == "÷");
        var x = letters.Single(l => l.Value == "x");
        Assert.Equal(631 * Size / 1000, beta.X - alpha.X, 2);
        Assert.Equal(549 * Size / 1000, divide.X - beta.X, 2);

        Assert.Equal(549 * Size / 1000, x.X - divide.X, 2);
    }

    [Fact]
    public void A_wingdings_bullet_is_drawn_with_noto_sans_symbols_2_on_a_text_line()
    {
        var bulleted = BulletDocument("Wingdings", '');
        var plain = new WordDocument();
        plain.AddParagraph().Add(new WordText("x") { Properties = new WordRunProperties { Font = "Arial", FontSize = Size } });

        var result = WordToPdf.Convert(bulleted);
        var page = PdfDocument.Open(result.Pdf).GetPage(1);
        var letters = page.Letters;

        var square = letters.Single(l => l.Value == "▪");
        var x = letters.Single(l => l.Value == "x");
        Assert.Contains("NotoSansSymbols2", square.FontName, StringComparison.Ordinal);
        var noto = FontLibrary.Bundled("NotoSansSymbols2", false, false);
        var glyph = noto.GetGlyphIndex('▪');
        Assert.NotEqual(0, glyph);
        Assert.Equal(noto.GetAdvanceWidth(glyph) * Size / noto.UnitsPerEm, x.X - square.X, 2);
        Assert.Contains("▪x", page.Text, StringComparison.Ordinal);

        // Noto Sans Symbols 2's own line is far taller (1.7 em): the bulleted line keeps the text line.
        Assert.Equal(Letters(plain).Single(l => l.Value == "x").Y, x.Y, 2);
    }

    [Fact]
    public void Every_wingdings_look_alike_has_a_glyph_in_the_bundled_faces()
    {
        var gaps = new HashSet<string>();
        var codes = string.Concat(Enumerable.Range(0xF020, 0xE0).Select(c => (char)c));

        var drawn = Symbols.Map(codes, "Wingdings", gaps).Distinct().ToList();

        Assert.Contains('▪', drawn);
        Assert.All(drawn, c => Assert.True(FontLibrary.Default.ResolveForCharacter(Symbols.DingbatFace, false, false, c).HasGlyph(c), $"U+{(int)c:X4}"));
        Assert.Empty(gaps);
    }

    [Fact]
    public void Symbol_metrics_come_from_the_adobe_file_with_its_licence()
    {
        Assert.Equal(250, SymbolMetrics.Advance(0x20));
        Assert.Equal(460, SymbolMetrics.Advance(0xB7));
        Assert.Null(SymbolMetrics.Advance(0x7F));
        Assert.Contains("may be used, copied, and distributed for any purpose", SymbolMetrics.Licence(), StringComparison.Ordinal);
        Assert.Equal(new Dictionary<int, int> { [65] = 722 }, SymbolMetrics.Parse("StartCharMetrics 2\nC 65 ; WX 722 ; N Alpha ;\nC -1 ; WX 790 ; N apple ;\nC 66 ; N Beta ;\n"));
    }

    [Fact]
    public void Symbol_advances_follow_the_code_each_drawn_character_stands_for()
    {
        Assert.Equal(460, Symbols.SymbolAdvance('•'));
        Assert.Equal(631, Symbols.SymbolAdvance('α'));
        Assert.Equal(500, Symbols.SymbolAdvance('1'));
        Assert.Null(Symbols.SymbolAdvance('€'));
    }

    [Fact]
    public void Noto_sans_symbols_2_resolves_in_every_style_and_falls_back_to_liberation_sans()
    {
        var bold = FontLibrary.Default.Resolve(Symbols.DingbatFace, true, true);

        Assert.Equal("Noto Sans Symbols 2", bold.Names.Family);
        Assert.True(bold.EmbeddingAllowed);
        Assert.Equal("Liberation Sans", FontLibrary.Default.ResolveForCharacter(Symbols.DingbatFace, false, false, 'A').Names.Family);
        Assert.Contains("SIL OPEN FONT LICENSE", FontLibrary.BundledLicence("NotoSansSymbols2"), StringComparison.Ordinal);
        Assert.False(FontLibrary.Default.IsSubstituted(Symbols.DingbatFace));
    }

    // A one-level bullet list whose label is a symbol-font character followed directly by "x" in Arial.
    private static WordDocument BulletDocument(string font, char label)
    {
        var document = new WordDocument();
        var list = document.Numbering.AddBulletList();
        var abstractId = document.Numbering.Instances[list].AbstractId;
        document.Numbering.Abstracts[abstractId].Levels[0] = new WordNumberingLevel(0)
        {
            Format = WordNumberFormat.Bullet,
            Text = label.ToString(),
            Suffix = WordLabelSuffix.Nothing,
            RunProperties = new WordRunProperties { Font = font, FontSize = Size },
        };
        var paragraph = new WordParagraph().AsListItem(list);
        paragraph.Add(new WordText("x") { Properties = new WordRunProperties { Font = "Arial", FontSize = Size } });
        document.Body.Add(paragraph);
        return document;
    }

    private static IReadOnlyList<PdfLetter> Letters(WordDocument document) =>
        PdfDocument.Open(WordToPdf.Convert(document).Pdf).GetPage(1).Letters;
}
