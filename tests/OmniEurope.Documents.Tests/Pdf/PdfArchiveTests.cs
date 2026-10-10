// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;
using System.Text;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Tests.Pdf;

public sealed class PdfArchiveTests
{
    // A character no bundled font has a glyph for (CJK ideograph U+4E2D).
    private static readonly string Missing = ((char)0x4E2D).ToString();

    private static byte[] Rich(PdfConformance conformance, bool cmykFirst = false)
    {
        var builder = new PdfDocumentBuilder
        {
            Title = "Rapport <annuel> & bilan",
            Author = "Zoë Dupont",
            Subject = "Archivage",
            Keywords = "pdf, archive",
            Creator = "Tests",
            CreationDate = new DateTimeOffset(2026, 10, 10, 9, 30, 0, TimeSpan.FromHours(2)),
        };
        var cmyk = cmykFirst ? builder.AddImage(Fixture("cmyk-plain.jpg")) : null;
        builder.Conformance = conformance;
        var page = builder.AddPage();
        page.DrawText("Déjà vu, 10 € " + Missing + " fin", 50, 100, PdfFont.Sans, 12);
        page.DrawText("Translucide", 50, 130, PdfFont.Serif.AsBold(), 14, PdfColor.Blue, opacity: 0.5);
        page.AddLink(50, 140, 100, 20, "https://example.org/");
        var alpha = new RasterImage(4, 4, ImageColorType.Rgba, [.. Enumerable.Repeat(new byte[] { 200, 30, 30, 128 }, 16).SelectMany(p => p)]);
        page.DrawImage(builder.AddImage(alpha), 50, 200, 40, 40);
        page.DrawImage(cmyk ?? builder.AddImage(Fixture("cmyk-plain.jpg")), 100, 200, 40, 40);
        page.DrawImage(builder.AddImage(Fixture("baseline.jpg")), 150, 200, 40, 40);
        builder.AddBookmark("Début", page, 100);
        builder.AddPage().DrawText("Page 2", 50, 50, PdfFont.Mono, 10);
        return builder.ToArray();
    }

    private static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", name));

    [Theory]
    [InlineData(PdfConformance.PdfA2b, 'B')]
    [InlineData(PdfConformance.PdfA2u, 'U')]
    public void A_written_document_meets_every_clause_checked(PdfConformance conformance, char level)
    {
        var pdf = Rich(conformance);

        Assert.Empty(PdfAChecker.Check(pdf, level));
    }

    [Fact]
    public void The_checker_finds_what_a_plain_pdf_lacks()
    {
        var violations = PdfAChecker.Check(Rich(PdfConformance.None), 'U');

        Assert.All(violations, v => Assert.Contains(v[..v.IndexOf(':', StringComparison.Ordinal)], PdfAChecker.Clauses));

        foreach (var clause in new[] { "6.2.3:", "6.6.2.1:", "6.3.2:", "6.2.4.3:", "6.2.11.8:" })
        {
            Assert.Contains(violations, v => v.StartsWith(clause, StringComparison.Ordinal));
        }

        var encrypted = new PdfDocumentBuilder { Encryption = new PdfEncryption(string.Empty, "owner") };
        encrypted.AddPage();
        Assert.Contains(PdfAChecker.Check(encrypted.ToArray(), 'B'), v => v.StartsWith("6.1.3:", StringComparison.Ordinal));
    }

    [Fact]
    public void Characters_without_a_glyph_are_left_out_without_moving_the_text_after_them()
    {
        var builder = new PdfDocumentBuilder { Conformance = PdfConformance.PdfA2b };
        builder.AddPage(300, 100).DrawText("a" + Missing + "b", 10, 50, PdfFont.Sans, 20);
        var plain = new PdfDocumentBuilder();
        plain.AddPage(300, 100).DrawText("a" + Missing + "b", 10, 50, PdfFont.Sans, 20);

        var archived = PdfDocument.Open(builder.ToArray()).GetPage(1).Letters;
        var reference = PdfDocument.Open(plain.ToArray()).GetPage(1).Letters;

        Assert.Equal(1, builder.OmittedCharacters);
        Assert.Equal(["a", "b"], archived.Select(l => l.Value));
        Assert.Equal(reference[^1].X, archived[^1].X, 3);
    }

    [Fact]
    public void Information_and_xmp_say_the_same_with_markup_escaped_and_control_characters_left_out()
    {
        var builder = new PdfDocumentBuilder { Conformance = PdfConformance.PdfA2u, Title = "A <b> & " + (char)7 + "c" };
        builder.AddPage().DrawText("x", 10, 10, PdfFont.Sans, 10);
        var pdf = builder.ToArray();
        var document = PdfDocument.Open(pdf);
        var metadata = Encoding.UTF8.GetString(((PdfStream)document.Store.Get(document.Store.Catalog, "Metadata")!).Data);

        Assert.Equal("A <b> & c", document.Information.Title);
        Assert.Contains("<rdf:li xml:lang=\"x-default\">A &lt;b&gt; &amp; c</rdf:li>", metadata, StringComparison.Ordinal);
        Assert.Contains("<pdfaid:conformance>U</pdfaid:conformance>", metadata, StringComparison.Ordinal);
        Assert.Empty(PdfAChecker.Check(pdf, 'U'));
    }

    [Fact]
    public void Encryption_and_cmyk_images_added_before_the_conformance_are_refused()
    {
        var encrypted = new PdfDocumentBuilder { Conformance = PdfConformance.PdfA2b, Encryption = new PdfEncryption(string.Empty, "owner") };
        encrypted.AddPage();

        Assert.Throws<InvalidOperationException>(() => encrypted.ToArray());
        Assert.Throws<InvalidOperationException>(() => Rich(PdfConformance.PdfA2b, cmykFirst: true));
    }

    [Fact]
    public void Cmyk_images_are_converted_to_rgb()
    {
        var store = PdfDocument.Open(Rich(PdfConformance.PdfA2b)).Store;
        var spaces = store.ObjectNumbers.Select(n => store.Resolve(new PdfReference(n, 0))).OfType<PdfStream>()
            .Where(s => s["Subtype"] is PdfName { Value: "Image" }).Select(s => ((PdfName)s["ColorSpace"]!).Value).ToList();

        Assert.DoesNotContain("DeviceCMYK", spaces);
        Assert.Contains("DeviceRGB", spaces);
    }

    [Fact]
    public void Word_excel_markdown_and_image_conversions_produce_conforming_files()
    {
        var word = WordToPdf.Convert(SampleDocuments.Report(), new WordPdfOptions { Conformance = PdfConformance.PdfA2u });
        var plain = WordToPdf.Convert(SampleDocuments.Report());
        var excel = ExcelToPdf.Convert(SampleDocuments.Workbook(), new ExcelPdfOptions { Pdf = new WordPdfOptions { Conformance = PdfConformance.PdfA2b } });
        var markdown = MarkdownToPdf.Convert("# Titre\n\nTexte **gras** et [lien](https://example.org).", options: new WordPdfOptions { Conformance = PdfConformance.PdfA2u });
        var images = ImagesToPdf.Convert([Fixture("cmyk-plain.jpg"), SampleDocuments.Png], new ImagePdfOptions { Conformance = PdfConformance.PdfA2b });

        Assert.Empty(PdfAChecker.Check(word.Pdf, 'U'));
        Assert.Equal(Samples.AllText(PdfDocument.Open(plain.Pdf)), Samples.AllText(PdfDocument.Open(word.Pdf)));
        Assert.Empty(PdfAChecker.Check(excel.Pdf, 'B'));
        Assert.Empty(PdfAChecker.Check(markdown.Pdf, 'U'));
        Assert.Empty(PdfAChecker.Check(images, 'B'));
    }

    [Fact]
    public void Missing_glyphs_of_a_conversion_are_reported_in_its_gaps()
    {
        var result = MarkdownToPdf.Convert("x " + Missing, options: new WordPdfOptions { Conformance = PdfConformance.PdfA2b });

        Assert.Contains(result.Gaps, g => g.Contains(".notdef", StringComparison.Ordinal));
        Assert.Empty(PdfAChecker.Check(result.Pdf, 'B'));
    }

    // ICC.1:2001-04: header (§6.1), tag table (§6.2), the tags a three-component matrix display profile requires
    // (§6.3.1.2: desc, wtpt, cprt, rXYZ, gXYZ, bXYZ, rTRC, gTRC, bTRC). The expected D50 colorants are the published
    // Bradford-adapted sRGB matrix (0.4360747 0.3850649 0.1430804 / 0.2225045 0.7168786 0.0606169 / 0.0139322
    // 0.0971045 0.7141733), the curve value at 0.5 the sRGB function's 0.214041.
    [Fact]
    public void The_srgb_profile_is_a_well_formed_version_2_display_profile()
    {
        var icc = SrgbProfile.Create();
        var tags = Enumerable.Range(0, (int)BinaryPrimitives.ReadUInt32BigEndian(icc.AsSpan(128)))
            .Select(i => icc.AsSpan(132 + (i * 12), 12).ToArray())
            .ToDictionary(e => Encoding.ASCII.GetString(e, 0, 4), e => (Offset: (int)BinaryPrimitives.ReadUInt32BigEndian(e.AsSpan(4)), Size: (int)BinaryPrimitives.ReadUInt32BigEndian(e.AsSpan(8))));
        double Fixed(int offset) => BinaryPrimitives.ReadInt32BigEndian(icc.AsSpan(offset)) / 65536.0;
        double[] Xyz(string tag) => [Fixed(tags[tag].Offset + 8), Fixed(tags[tag].Offset + 12), Fixed(tags[tag].Offset + 16)];

        Assert.Equal((uint)icc.Length, BinaryPrimitives.ReadUInt32BigEndian(icc));
        Assert.Equal("mntrRGB XYZ ", Encoding.ASCII.GetString(icc, 12, 12));
        Assert.Equal("acsp", Encoding.ASCII.GetString(icc, 36, 4));
        Assert.Equal(2, icc[8]);
        Assert.Equal(["desc", "cprt", "wtpt", "rXYZ", "gXYZ", "bXYZ", "rTRC", "gTRC", "bTRC"], tags.Keys);
        Assert.All(tags.Values, t => Assert.True(t.Offset % 4 == 0 && t.Offset + t.Size <= icc.Length));
        Assert.Equal([0.9642, 1.0, 0.8249], [Fixed(68), Fixed(72), Fixed(76)], (a, b) => Math.Abs(a - b) < 1e-4);
        Assert.Equal([0.4360747, 0.2225045, 0.0139322], Xyz("rXYZ"), (a, b) => Math.Abs(a - b) < 5e-4);
        Assert.Equal([0.3850649, 0.7168786, 0.0971045], Xyz("gXYZ"), (a, b) => Math.Abs(a - b) < 5e-4);
        Assert.Equal([0.1430804, 0.0606169, 0.7141733], Xyz("bXYZ"), (a, b) => Math.Abs(a - b) < 5e-4);
        var curve = tags["rTRC"].Offset;
        Assert.Equal("curv", Encoding.ASCII.GetString(icc, curve, 4));
        var points = (int)BinaryPrimitives.ReadUInt32BigEndian(icc.AsSpan(curve + 8));
        var middle = BinaryPrimitives.ReadUInt16BigEndian(icc.AsSpan(curve + 12 + ((points - 1) / 2 * 2))) / 65535.0;
        Assert.Equal(SrgbProfile.Linear((points - 1) / 2 / (double)(points - 1)), middle, 4);
        Assert.Equal(0.214041, SrgbProfile.Linear(0.5), 5);
        Assert.Equal(icc, SrgbProfile.Create());
    }
}
