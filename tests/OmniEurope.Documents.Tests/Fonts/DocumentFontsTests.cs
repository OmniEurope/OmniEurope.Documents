// SPDX-License-Identifier: EUPL-1.2
using System.Security.Cryptography;
using System.Text;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Fonts;

/// <summary>The extra fonts assembly: its faces, the substitutions it adds and the per-document font report.</summary>
public sealed class DocumentFontsTests
{
    private static readonly Lazy<FontLibrary> Extra = new(DocumentFonts.CreateLibrary);

    private static readonly (string Family, bool Bold, bool Italic, string Style)[] FaceList =
    [
        .. new[] { "Gelasio", "Noto Sans", "Noto Serif", "Roboto", "Open Sans", "Lato" }.SelectMany(family => new[]
        {
            (family, false, false, "Regular"), (family, true, false, "Bold"), (family, false, true, "Italic"), (family, true, true, "Bold Italic"),
        }),
        ("Selawik", false, false, "Regular"), ("Selawik", true, false, "Bold"), ("Selawik Light", false, false, "Light"),
        ("Selawik Semilight", false, false, "Semilight"), ("Selawik Semibold", false, false, "Semibold"),
    ];

    public static TheoryData<string, bool, bool, string> Faces()
    {
        var data = new TheoryData<string, bool, bool, string>();
        foreach (var (family, bold, italic, style) in FaceList)
        {
            data.Add(family, bold, italic, style);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public void Each_extra_face_parses_and_draws_a_known_glyph(string family, bool bold, bool italic, string style)
    {
        var face = Extra.Value.Resolve(family, bold, italic);

        Assert.StartsWith(family.Split(' ')[0], face.Names.Family, StringComparison.Ordinal);
        Assert.Equal(style, face.Names.Subfamily);
        Assert.True(face.EmbeddingAllowed);
        Assert.NotEmpty(face.Glyphs!.GetOutline(face.GetGlyphIndex('A')).Contours);
        Assert.True(face.MeasureText("Hamburg", 10) > 0);

        var document = new WordDocument();
        document.Body.Add(new WordParagraph { Inlines = { new WordText("Hamburg", new WordRunProperties { Font = family, Bold = bold, Italic = italic }) } });
        var result = WordToPdf.Convert(document, new WordPdfOptions { Fonts = Extra.Value });

        Assert.Contains("Hamburg", PdfDocument.Open(result.Pdf).GetPage(1).Text);
        Assert.Contains("+" + face.Names.PostScriptName, Encoding.Latin1.GetString(result.Pdf), StringComparison.Ordinal);
    }

    [Fact]
    public void Segoe_UI_and_Georgia_are_drawn_with_Selawik_and_Gelasio_once_registered()
    {
        Assert.Equal("Selawik", Extra.Value.Resolve("Segoe UI").Names.Family);
        Assert.Equal("Bold", Extra.Value.Resolve("Segoe UI", bold: true).Names.Subfamily);
        Assert.Equal("Light", Extra.Value.Resolve("Segoe UI Light").Names.Subfamily);
        Assert.Equal("Gelasio", Extra.Value.Resolve("Georgia", true, true).Names.Family);
        Assert.Equal("Bold Italic", Extra.Value.Resolve("Georgia", true, true).Names.Subfamily);

        var plain = new FontLibrary();
        Assert.Equal("Liberation Sans", plain.Resolve("Segoe UI").Names.Family);
        Assert.Equal("Liberation Serif", plain.Resolve("Georgia").Names.Family);
        Assert.Equal("Liberation Sans", FontLibrary.Default.Resolve("Segoe UI").Names.Family);
    }

    [Fact]
    public void Unknown_families_fall_back_to_Noto_once_registered_and_to_Liberation_otherwise()
    {
        Assert.Equal("Noto Sans", Extra.Value.Resolve("Police Sans Inconnue").Names.Family);
        Assert.Equal("Noto Serif", Extra.Value.Resolve("Police Imaginaire").Names.Family);
        Assert.Equal("Liberation Mono", Extra.Value.Resolve("Mono Inconnue").Names.Family);
        Assert.Equal("Liberation Sans", Extra.Value.Resolve("Arial").Names.Family);
        Assert.Equal("Carlito", Extra.Value.Resolve("Calibri Light").Names.Family);

        var plain = new FontLibrary();
        Assert.Equal("Liberation Sans", plain.Resolve("Police Sans Inconnue").Names.Family);
        Assert.Equal("Liberation Serif", plain.Resolve("Police Imaginaire").Names.Family);
    }

    [Fact]
    public void Characters_a_face_lacks_come_from_Noto_once_registered()
    {
        // Gelasio and Lato (1.1) have no Cyrillic; Lato has no Hungarian double acute either.
        Assert.Equal("Noto Serif", Extra.Value.ResolveForCharacter("Georgia", false, false, 'Ж').Names.Family);
        Assert.Equal("Noto Sans", Extra.Value.ResolveForCharacter("Lato", false, false, 'ő').Names.Family);
        Assert.Equal("Lato", Extra.Value.ResolveForCharacter("Lato", false, false, 'é').Names.Family);
        Assert.Equal("Liberation Serif", new FontLibrary().ResolveForCharacter("Caladea", false, false, 'Ж').Names.Family);
    }

    [Fact]
    public void Roboto_Open_Sans_and_Lato_are_used_as_is()
    {
        var result = Convert(Extra.Value, "Roboto", "Open Sans", "Lato");

        Assert.All(result.Fonts, f => Assert.True(f.Exact, f.Requested));
        Assert.Equal(["Roboto", "Open Sans", "Lato"], result.Fonts.Select(f => f.Used));
        Assert.DoesNotContain(result.Gaps, g => g.StartsWith("font ", StringComparison.Ordinal));
    }

    [Fact]
    public void Aptos_and_Segoe_UI_substitutions_are_reported()
    {
        var result = Convert(Extra.Value, "Aptos", "Segoe UI", "Georgia", "Police Imaginaire");

        Assert.Contains("font \"Aptos\" replaced by Roboto", result.Gaps);
        Assert.Contains("font \"Segoe UI\" replaced by Selawik", result.Gaps);
        Assert.Contains("font \"Georgia\" replaced by Gelasio", result.Gaps);
        Assert.Contains("font \"Police Imaginaire\" replaced by Noto Serif", result.Gaps);
        var aptos = result.Fonts.Single(f => f.Requested == "Aptos");
        Assert.False(aptos.Exact);
        Assert.Equal("Roboto", aptos.Used);
        Assert.Contains("no free equivalent", aptos.Note, StringComparison.Ordinal);
        Assert.Equal("Noto Serif", result.Fonts.Single(f => f.Requested == "Police Imaginaire").Used);

        var plain = Convert(new FontLibrary(), "Aptos", "Segoe UI");
        Assert.Contains("font \"Aptos\" replaced by LiberationSans", plain.Gaps);
        Assert.Contains("font \"Segoe UI\" replaced by LiberationSans", plain.Gaps);
        Assert.All(plain.Fonts, f => Assert.Equal("Liberation Sans", f.Used));
    }

    [Fact]
    public void The_result_lists_each_requested_family_with_the_font_drawn()
    {
        var result = Convert(null, "Calibri", "Calibri Light", "Liberation Sans", "Calibri");

        Assert.Equal(["Calibri", "Calibri Light", "Liberation Sans"], result.Fonts.Select(f => f.Requested));
        Assert.Equal(new FontUse("Calibri", "Carlito", false, "metric-compatible look-alike (same widths, other shapes)"), result.Fonts[0]);
        Assert.Equal("Carlito", result.Fonts[1].Used);
        Assert.False(result.Fonts[1].Exact);
        Assert.Contains("no light weight", result.Fonts[1].Note, StringComparison.Ordinal);
        Assert.Equal(new FontUse("Liberation Sans", "Liberation Sans", true, null), result.Fonts[2]);
    }

    [Fact]
    public void The_rent_receipt_template_lists_Calibri_and_Calibri_Light_drawn_with_Carlito()
    {
        var docx = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Word", "rent-receipt-template.docx"));

        var fonts = WordToPdf.Convert(docx).Fonts;

        var calibri = fonts.Single(f => f.Requested == "Calibri");
        Assert.Equal("Carlito", calibri.Used);
        Assert.False(calibri.Exact);
        var light = fonts.Single(f => f.Requested == "Calibri Light");
        Assert.Equal("Carlito", light.Used);
        Assert.Contains("Carlito Regular", light.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void Licences_and_the_notice_are_embedded_and_the_notice_records_each_face_hash()
    {
        foreach (var family in DocumentFonts.Families)
        {
            var licence = DocumentFonts.Licence(family);
            // Only SIL OFL 1.1 fonts ship with the package.
            Assert.Contains("SIL OPEN FONT LICENSE Version 1.1", licence, StringComparison.Ordinal);
            Assert.DoesNotContain("Apache License", licence, StringComparison.Ordinal);
            Assert.Contains(System.Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(licence))), DocumentFonts.Notice(), StringComparison.Ordinal);
        }

        Assert.Contains("The Roboto Project Authors", DocumentFonts.Licence("Roboto"), StringComparison.Ordinal);
        Assert.Contains("The Open Sans Project Authors", DocumentFonts.Licence("Open Sans"), StringComparison.Ordinal);
        Assert.Contains("Selawik", DocumentFonts.Licence("Selawik"), StringComparison.Ordinal);        Assert.Throws<KeyNotFoundException>(() => DocumentFonts.Licence("Arial"));
        var notice = DocumentFonts.Notice();
        Assert.Equal(29, FaceList.Length);
        foreach (var (family, bold, italic, _) in FaceList)
        {
            var hash = System.Convert.ToHexStringLower(SHA256.HashData(Extra.Value.Resolve(family, bold, italic).Data));
            Assert.Contains(hash, notice, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_core_assembly_does_not_reference_the_extra_fonts_assembly()
    {
        var core = typeof(FontLibrary).Assembly;

        Assert.DoesNotContain(core.GetReferencedAssemblies(), a => a.Name == typeof(DocumentFonts).Assembly.GetName().Name);
        Assert.Contains(typeof(DocumentFonts).Assembly.GetReferencedAssemblies(), a => a.Name == core.GetName().Name);
    }

    [Fact]
    public void A_font_registered_under_a_substituted_name_still_wins()
    {
        var fonts = new FontLibrary();
        fonts.Register(FontLibrary.Bundled("Carlito", false, false).Data, "Segoe UI");
        DocumentFonts.Register(fonts);

        Assert.Equal("Carlito", fonts.Resolve("Segoe UI").Names.Family);
        Assert.True(fonts.Describe("Segoe UI").Exact);
    }

    private static WordPdfResult Convert(FontLibrary? fonts, params string[] families)
    {
        var document = new WordDocument();
        foreach (var family in families)
        {
            document.Body.Add(new WordParagraph { Inlines = { new WordText("Texte " + family, new WordRunProperties { Font = family, FontSize = 11 }) } });
        }

        return WordToPdf.Convert(document, new WordPdfOptions { Fonts = fonts });
    }
}
