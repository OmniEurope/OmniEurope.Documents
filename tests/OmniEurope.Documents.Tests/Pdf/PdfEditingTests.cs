// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Editing;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Tests.Pdf;

public sealed class PdfEditingTests
{
    [Fact]
    public void Merges_documents_in_order()
    {
        var merged = PdfDocument.Open(PdfEditor.Merge(Open(Pages("A1", "A2")), Open(Pages("B1")), Browser()));

        Assert.Equal(5, merged.PageCount);
        Assert.Equal(["A1", "A2", "B1"], merged.Pages.Take(3).Select(p => p.Text));
        Assert.Contains("Rapport de test", merged.GetPage(4).Text);
        Assert.Equal("Deuxième page du document.", merged.GetPage(5).Text);
    }

    [Fact]
    public void Extracts_reorders_removes_and_rotates_pages()
    {
        var source = Open(Pages("P1", "P2", "P3", "P4"));

        Assert.Equal(["P3", "P1"], Texts(PdfEditor.ExtractPages(source, [3, 1])));
        Assert.Equal(["P2", "P3", "P4"], Texts(PdfEditor.ExtractPages(source, "2-")));
        Assert.Equal(["P1", "P4"], Texts(PdfEditor.RemovePages(source, [2, 3])));
        Assert.Equal(["P2", "P2"], Texts(PdfEditor.ExtractPages(source, [2, 2])));
        var rotated = PdfDocument.Open(PdfEditor.RotatePages(source, [2], 90));
        Assert.Equal([0, 90, 0, 0], rotated.Pages.Select(p => p.Rotation));
        Assert.Equal((841.89, 595.28), (Math.Round(rotated.GetPage(2).Width, 2), Math.Round(rotated.GetPage(2).Height, 2)));
        Assert.Throws<ArgumentException>(() => PdfEditor.RemovePages(source, [1, 2, 3, 4]));
    }

    [Fact]
    public void Splits_by_size_and_by_ranges()
    {
        var source = Open(Pages("P1", "P2", "P3"));

        var singles = PdfEditor.Split(source);
        var parts = PdfEditor.SplitByRanges(source, "1-2", "3");

        Assert.Equal(["P1", "P2", "P3"], singles.Select(b => Texts(b).Single()));
        Assert.Equal(["P1", "P2"], Texts(parts[0]));
        Assert.Equal(["P3"], Texts(parts[1]));
    }

    [Theory]
    [InlineData("1-3,5", 6, new[] { 1, 2, 3, 5 })]
    [InlineData("4-", 6, new[] { 4, 5, 6 })]
    [InlineData("-2", 6, new[] { 1, 2 })]
    [InlineData("2, 2", 3, new[] { 2, 2 })]
    public void Parses_page_ranges(string expression, int count, int[] expected)
    {
        Assert.Equal(expected, PageRanges.Parse(expression, count));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("7")]
    [InlineData("3-1")]
    [InlineData("a")]
    [InlineData("")]
    public void Rejects_bad_page_ranges(string expression)
    {
        Assert.Throws<FormatException>(() => PageRanges.Parse(expression, 6));
    }

    [Fact]
    public void Stamping_appends_an_update_and_keeps_the_original_bytes()
    {
        var original = Pages("Contenu 1", "Contenu 2");

        var stamped = PdfStamper.StampText(original, "Partagé le 06/10/2026 — Ωμέγα", new PdfStampOptions { Opacity = 0.3 });

        Assert.Equal(original, stamped[..original.Length]);
        var document = PdfDocument.Open(stamped);
        Assert.False(document.WasRepaired);
        Assert.Equal(2, document.PageCount);
        Assert.Contains("Contenu 2", document.GetPage(2).Text);
        Assert.Contains("Partagé le 06/10/2026 — Ωμέγα", document.GetPage(2).Text);
        var footer = document.GetPage(1).Letters.First(l => l.Value == "P");
        Assert.Equal(36, footer.X, 3);
        Assert.Equal(14, footer.Y, 3);
    }

    [Fact]
    public void Stamping_works_on_files_with_cross_reference_streams_and_selected_pages()
    {
        var compact = PdfCompressor.Compress(Open(Pages("Un", "Deux")));
        Assert.Contains("/Type /XRef", Encoding.Latin1.GetString(compact));

        var stamped = PdfStamper.Stamp(compact, (canvas, page) => canvas.DrawText($"Page {page.Number}", 20, 20, PdfFont.Sans, 9), page => page.Number == 2);

        var document = PdfDocument.Open(stamped);
        Assert.False(document.WasRepaired);
        Assert.Equal("Un", document.GetPage(1).Text);
        Assert.Contains("Page 2", document.GetPage(2).Text);
        Assert.Contains("Deux", document.GetPage(2).Text);
    }

    [Fact]
    public void Stamping_twice_chains_the_updates()
    {
        var once = PdfStamper.StampText(Pages("Corps"), "Premier");

        var twice = PdfStamper.StampText(once, "Second", new PdfStampOptions { Position = PdfStampPosition.Top });

        var text = PdfDocument.Open(twice).GetPage(1).Text;
        Assert.Contains("Premier", text);
        Assert.Contains("Second", text);
        Assert.Contains("Corps", text);
    }

    [Fact]
    public void Compression_keeps_content_and_drops_dead_revisions()
    {
        var stamped = PdfStamper.StampText(PdfStamper.StampText(Pages("Texte"), "A"), "B");
        var browser = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Pdf", "browser.pdf"));

        var compressed = PdfCompressor.Compress(PdfDocument.Open(stamped));
        var compressedBrowser = PdfCompressor.Compress(PdfDocument.Open(browser));

        Assert.True(compressed.Length < stamped.Length);
        Assert.Contains("Texte", PdfDocument.Open(compressed).GetPage(1).Text);
        Assert.True(compressedBrowser.Length < browser.Length, $"{compressedBrowser.Length} >= {browser.Length}");
        var reread = PdfDocument.Open(compressedBrowser);
        Assert.Equal(2, reread.PageCount);
        Assert.Equal(PdfDocument.Open(browser).GetPage(1).Text, reread.GetPage(1).Text);
        Assert.Equal("Fixture navigateur", reread.Information.Title);
    }

    [Fact]
    public void Lossy_compression_turns_cmyk_into_rgb_and_leaves_masks_and_small_images()
    {
        var cmyk = new byte[80 * 80 * 4];
        for (var i = 0; i < cmyk.Length; i++)
        {
            cmyk[i] = (byte)((i / 4 % 80) + (i % 4 * 40));
        }

        var pdf = RawPdf.Page(
            "q 80 0 0 80 0 0 cm /Im1 Do Q q 8 0 0 8 100 0 cm /Im2 Do Q q 80 0 0 80 100 100 cm /Im3 Do Q",
            "/XObject << /Im1 5 0 R /Im2 6 0 R /Im3 7 0 R >>",
            string.Empty,
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 80 /Height 80 /BitsPerComponent 8 /ColorSpace /DeviceCMYK", cmyk),
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 8 /Height 8 /BitsPerComponent 8 /ColorSpace /DeviceGray", new byte[64]),
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 80 /Height 80 /ImageMask true", new byte[800]));

        var compressed = PdfCompressor.Compress(PdfDocument.Open(pdf), new PdfCompressionOptions { ImageQuality = 70 });

        var images = PdfDocument.Open(compressed).GetPage(1).Images;
        Assert.Equal(["DCTDecode"], images[0].Filters);
        Assert.Equal(OmniEurope.Documents.Imaging.ImageColorType.Rgb, images[0].Decode()!.ColorType);
        Assert.DoesNotContain("DCTDecode", images[1].Filters);
        Assert.DoesNotContain("DCTDecode", images[2].Filters);
    }

    [Fact]
    public void Images_with_a_colour_key_or_stencil_mask_keep_their_transparency()
    {
        // A colour-key mask (array) and an explicit mask (stream): re-encoding would lose the transparency.
        var noise = new byte[120 * 80 * 3];
        new Random(3).NextBytes(noise);
        var pdf = RawPdf.Page(
            "q 120 0 0 80 0 0 cm /Im1 Do Q q 120 0 0 80 0 100 cm /Im2 Do Q",
            "/XObject << /Im1 5 0 R /Im2 6 0 R >>",
            string.Empty,
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 120 /Height 80 /BitsPerComponent 8 /ColorSpace /DeviceRGB /Mask [0 10 0 10 0 10]", noise),
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 120 /Height 80 /BitsPerComponent 8 /ColorSpace /DeviceRGB /Mask 7 0 R", noise),
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 120 /Height 80 /ImageMask true", new byte[1200]));

        var compressed = PdfDocument.Open(PdfCompressor.Compress(PdfDocument.Open(pdf), new PdfCompressionOptions { ImageQuality = 60, MaxImageSide = 40 }));

        var images = compressed.GetPage(1).Images;
        Assert.All(images, i => Assert.Equal((120, 80), (i.PixelWidth, i.PixelHeight)));
        Assert.All(images, i => Assert.DoesNotContain("DCTDecode", i.Filters));
        var text = Encoding.Latin1.GetString(PdfCompressor.Compress(PdfDocument.Open(pdf), new PdfCompressionOptions { ImageQuality = 60 }));
        Assert.DoesNotContain("DCTDecode", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_inside_form_xobjects_is_extracted_once_even_when_a_form_draws_itself()
    {
        var pdf = RawPdf.Page(
            "/Fm1 Do /Fm2 Do",
            "/XObject << /Fm1 5 0 R /Fm2 6 0 R >> /Font << /F1 7 0 R >>",
            string.Empty,
            RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 200 200] /Resources << /XObject << /Fm1 5 0 R >> /Font << /F1 7 0 R >> >>", RawPdf.Ascii("BT /F1 12 Tf 10 100 Td (Inside) Tj ET /Fm1 Do")),
            RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 200 200] /Filter /FlateDecode", [1, 2, 3, 4]),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        Assert.Equal("Inside", PdfDocument.Open(pdf).GetPage(1).Text);
    }

    [Fact]
    public void Lossy_compression_reencodes_large_images_as_jpeg()
    {
        var builder = new PdfDocumentBuilder();
        var image = new OmniEurope.Documents.Imaging.RasterImage(400, 300, OmniEurope.Documents.Imaging.ImageColorType.Rgb);
        var random = new Random(7);
        random.NextBytes(image.Pixels);
        builder.AddPage().DrawImage(builder.AddImage(image), 10, 10, 400, 300);
        var original = builder.ToArray();

        var compressed = PdfCompressor.Compress(PdfDocument.Open(original), new PdfCompressionOptions { ImageQuality = 60, MaxImageSide = 200 });

        var reread = Assert.Single(PdfDocument.Open(compressed).GetPage(1).Images);
        Assert.Equal(["DCTDecode"], reread.Filters);
        Assert.Equal((200, 150), (reread.PixelWidth, reread.PixelHeight));
        Assert.True(compressed.Length < original.Length / 3);
    }

    [Fact]
    public void Encrypted_or_damaged_files_are_not_stamped()
    {
        var bytes = Pages("x");
        var text = Encoding.Latin1.GetString(bytes);
        var at = text.LastIndexOf("startxref\n", StringComparison.Ordinal) + 10;
        var broken = Encoding.Latin1.GetBytes(text[..at] + "1" + text[(text.IndexOf('\n', at))..]);

        Assert.Throws<NotSupportedException>(() => PdfStamper.StampText(broken, "x"));
    }

    private static byte[] Pages(params string[] texts)
    {
        var builder = new PdfDocumentBuilder { Title = "Pages" };
        foreach (var text in texts)
        {
            builder.AddPage().DrawText(text, 72, 100, PdfFont.Sans, 14);
        }

        return builder.ToArray();
    }

    private static PdfDocument Open(byte[] bytes) => PdfDocument.Open(bytes);

    private static PdfDocument Browser() => PdfDocument.Open(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Pdf", "browser.pdf")));

    private static List<string> Texts(byte[] pdf) => PdfDocument.Open(pdf).Pages.Select(p => p.Text).ToList();
}
