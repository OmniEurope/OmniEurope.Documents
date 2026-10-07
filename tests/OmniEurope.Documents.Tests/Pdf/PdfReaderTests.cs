// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Editing;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Tests.Pdf;

public sealed class PdfReaderTests
{
    private const string Multilingual = "Façade Ωμέγα Жук Zażółć Țară €";

    [Fact]
    public void Reads_back_the_text_written_by_the_builder_with_positions()
    {
        var document = PdfDocument.Open(Written());

        var page = document.GetPage(1);

        Assert.Equal(2, document.PageCount);
        Assert.Contains(Multilingual, page.Text);
        Assert.Contains("Seconde ligne justifiée", page.Text);
        var first = page.Letters[0];
        Assert.Equal("F", first.Value);
        Assert.Equal(50, first.X, 3);
        Assert.Equal(841.89 - 100, first.Y, 3);
        Assert.Equal(12, first.FontSize, 3);
        Assert.StartsWith("LiberationSans", first.FontName);
        Assert.True(first.BoundingBox.Top > first.Y && first.BoundingBox.Bottom < first.Y);
        Assert.Equal("Page deux", document.GetPage(2).Text);
        Assert.Equal((842, 595), (document.GetPage(2).Width, document.GetPage(2).Height));
    }

    [Fact]
    public void Reads_metadata()
    {
        var information = PdfDocument.Open(Written()).Information;

        Assert.Equal("Titre — Ωμέγα", information.Title);
        Assert.Equal("Auteur", information.Author);
        Assert.Equal("OmniEurope.Documents", information.Producer);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 14, 30, 0, TimeSpan.FromHours(2)), information.CreationDate);
    }

    [Fact]
    public void Decodes_embedded_images_back_to_their_pixels()
    {
        var png = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "rgba.png"));
        var jpeg = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "baseline.jpg"));
        var builder = new PdfDocumentBuilder();
        var canvas = builder.AddPage();
        canvas.DrawImage(builder.AddImage(png), 10, 10, 74, 46);
        canvas.DrawImage(builder.AddImage(jpeg), 100, 10, 37, 23);

        var images = PdfDocument.Open(builder.ToArray()).GetPage(1).Images;

        Assert.Equal(2, images.Count);
        Assert.Equal(PngCodec.Decode(png).Pixels, images[0].Decode()!.Pixels);
        Assert.Equal(new PdfRectangle(10, 841.89 - 56, 84, 841.89 - 10), Round(images[0].Bounds));
        Assert.Equal(["DCTDecode"], images[1].Filters);
        Assert.Equal(JpegDecoder.Decode(jpeg).Pixels, images[1].Decode()!.Pixels);
        Assert.True(PngCodec.IsPng(images[1].ToPng()));
    }

    [Fact]
    public void Fax_images_are_decoded_and_unsupported_or_empty_images_give_no_pixels()
    {
        // Group 4, 8 columns: a white row (V0), then two black pixels (horizontal mode: white 0, black 2) and V0.
        var pdf = RawPdf.Page(
            "q 8 0 0 2 0 0 cm /Im1 Do Q q 8 0 0 2 0 50 cm /Im2 Do Q q 8 0 0 2 0 100 cm /Im3 Do Q",
            "/XObject << /Im1 5 0 R /Im2 6 0 R /Im3 7 0 R >>",
            string.Empty,
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 8 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 1 /Filter /CCITTFaxDecode /DecodeParms << /K -1 /Columns 8 >>", [0x93, 0x5E]),
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 8 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 1 /Filter /JBIG2Decode", [0, 0]),
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 0 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 8", [0, 0]));

        var images = PdfDocument.Open(pdf).GetPage(1).Images;

        Assert.Equal(3, images.Count);
        Assert.Equal(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 0, 0, 255, 255, 255, 255, 255, 255 }, images[0].Decode()!.Pixels);
        Assert.Null(images[1].Decode());
        Assert.Null(images[2].Decode());
    }

    [Fact]
    public void Reads_a_pdf_printed_by_a_browser()
    {
        var document = PdfDocument.Open(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Pdf", "browser.pdf")));

        var text = document.GetPage(1).Text;

        Assert.Equal(2, document.PageCount);
        Assert.Equal("Fixture navigateur", document.Information.Title);
        Assert.Contains("Rapport de test", text);
        Assert.Contains("Première ligne avec accents : é è à ç ô ù.", text);
        Assert.Contains("Ωμέγα", text);
        Assert.Contains("Жук", text);
        Assert.Contains("Zażółć", text);
        Assert.Contains("Bêta 3,14", text);
        Assert.Equal("Deuxième page du document.", document.GetPage(2).Text);
        var image = Assert.Single(document.GetPage(1).Images);
        Assert.Equal((2, 2), (image.PixelWidth, image.PixelHeight));
        Assert.NotNull(image.Decode());
    }

    [Fact]
    public void Repairs_a_compact_file_by_indexing_its_object_streams()
    {
        var compact = PdfCompressor.Compress(PdfDocument.Open(Written()), new PdfCompressionOptions());
        var text = Encoding.Latin1.GetString(compact);
        Assert.Contains("/ObjStm", text, StringComparison.Ordinal);
        var at = text.LastIndexOf("startxref\n", StringComparison.Ordinal) + 10;
        var broken = Encoding.Latin1.GetBytes(text[..at] + "12345" + text[text.IndexOf('\n', at)..]);

        var document = PdfDocument.Open(broken);

        Assert.True(document.WasRepaired);
        Assert.Contains(Multilingual, document.GetPage(1).Text);
    }

    [Fact]
    public void Cid_fonts_take_widths_from_lists_ranges_and_the_default()
    {
        var toUnicode = "1 begincodespacerange <0000> <FFFF> endcodespacerange 4 beginbfchar <0001> <0041> <0002> <0042> <000B> <0043> <0014> <0044> endbfchar";
        var pdf = RawPdf.Page(
            "BT /F1 10 Tf 0 100 Td <00010002000B0014> Tj ET",
            "/Font << /F1 5 0 R >>",
            string.Empty,
            "<< /Type /Font /Subtype /Type0 /BaseFont /Test /Encoding /Identity-H /DescendantFonts [6 0 R] /ToUnicode 7 0 R >>",
            "<< /Type /Font /Subtype /CIDFontType2 /BaseFont /Test /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /DW 1000 /W [1 [500 600] 10 12 700 20] >>",
            RawPdf.Stream(string.Empty, RawPdf.Ascii(toUnicode)));

        var letters = PdfDocument.Open(pdf).GetPage(1).Letters;

        Assert.Equal(["A", "B", "C", "D"], letters.Select(l => l.Value));
        Assert.Equal([5.0, 6.0, 7.0, 10.0], letters.Select(l => Math.Round(l.Width, 3)));
    }

    [Fact]
    public void Literal_strings_decode_every_escape()
    {
        var source = Encoding.Latin1.GetBytes("(a\\n\\t\\r\\b\\f\\(\\)\\\\\\101\\60\\7x\\q\\\r\nc\\\nd(e\r\nf))");

        var value = Assert.IsType<OmniEurope.Documents.Pdf.Objects.PdfString>(new PdfLexer(source).Next().Value);

        Assert.Equal("a\n\t\r\b\f()\\A0\u0007xqcd(e\nf)", Encoding.Latin1.GetString(value.Bytes));
    }

    [Theory]
    [InlineData("/Length 99 ", "\n")]
    [InlineData("/Length 12 ", "\r\n")]
    public void Finds_the_end_of_a_stream_whose_length_is_wrong(string length, string beforeEnd)
    {
        const string content = "BT /F1 12 Tf 10 100 Td (Hello) Tj ET";
        var pdf = Encoding.Latin1.GetString(RawPdf.Page(content, "/Font << /F1 5 0 R >>", string.Empty, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"))
            .Replace("/Length 36 ", length, StringComparison.Ordinal)
            .Replace(content + "\nendstream", content + beforeEnd + "endstream", StringComparison.Ordinal);

        Assert.Equal("Hello", PdfDocument.Open(Encoding.Latin1.GetBytes(pdf)).GetPage(1).Text);
    }

    [Fact]
    public void Repairs_a_file_whose_cross_reference_offset_is_wrong()
    {
        var bytes = Written();
        var text = Encoding.Latin1.GetString(bytes);
        var at = text.LastIndexOf("startxref\n", StringComparison.Ordinal) + 10;
        var end = text.IndexOf('\n', at);
        var broken = Encoding.Latin1.GetBytes(text[..at] + "12345" + text[end..]);

        var document = PdfDocument.Open(broken);

        Assert.True(document.WasRepaired);
        Assert.Contains(Multilingual, document.GetPage(1).Text);
    }

    [Fact]
    public void Follows_incremental_updates()
    {
        var original = Written();
        var text = Encoding.Latin1.GetString(original);
        var previous = text[(text.LastIndexOf("startxref\n", StringComparison.Ordinal) + 10)..].Split('\n')[0];
        var size = int.Parse(System.Text.RegularExpressions.Regex.Match(text, @"/Size (\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
        var root = System.Text.RegularExpressions.Regex.Match(text, @"/Root (\d+ 0 R)").Groups[1].Value;
        var update = new StringBuilder();
        var objectOffset = original.Length;
        update.Append(CultureInfo.InvariantCulture, $"{size} 0 obj\n<</Title (Nouveau titre)>>\nendobj\n");
        var xref = objectOffset + update.Length;
        update.Append(CultureInfo.InvariantCulture, $"xref\n{size} 1\n{objectOffset:D10} 00000 n \ntrailer\n<</Size {size + 1} /Root {root} /Info {size} 0 R /Prev {previous}>>\nstartxref\n{xref}\n%%EOF\n");

        var document = PdfDocument.Open([.. original, .. Encoding.ASCII.GetBytes(update.ToString())]);

        Assert.False(document.WasRepaired);
        Assert.Equal("Nouveau titre", document.Information.Title);
        Assert.Contains(Multilingual, document.GetPage(1).Text);
    }

    [Theory]
    [InlineData("ASCIIHexDecode", "48656C6C6F>", "Hello")]
    [InlineData("ASCII85Decode", "9jqo^~>", "Man ")]
    [InlineData("ASCII85Decode", "z~>", "\0\0\0\0")]
    public void Decodes_text_filters(string filter, string encoded, string expected)
    {
        var (data, _, _) = PdfFilters.Decode(Encoding.ASCII.GetBytes(encoded), [filter], []);

        Assert.Equal(expected, Encoding.Latin1.GetString(data));
    }

    [Fact]
    public void Decodes_lzw_and_run_length()
    {
        // The LZW example of ISO 32000-1 §7.4.4.2.
        byte[] lzw = [0x80, 0x0B, 0x60, 0x50, 0x22, 0x0C, 0x0C, 0x85, 0x01];
        Assert.Equal("-----A---B", Encoding.ASCII.GetString(PdfFilters.Decode(lzw, ["LZWDecode"], []).Data));
        byte[] runLength = [2, (byte)'a', (byte)'b', (byte)'c', 253, (byte)'x', 128];
        Assert.Equal("abcxxxx", Encoding.ASCII.GetString(PdfFilters.Decode(runLength, ["RunLengthDecode"], []).Data));
    }

    [Fact]
    public void Rc4_matches_its_reference_vector()
    {
        Assert.Equal(Convert.FromHexString("BBF316E8D940AF0AD3"), Rc4.Apply("Key"u8.ToArray(), "Plaintext"u8.ToArray()));
    }

    [Fact]
    public void Rejects_what_is_not_a_pdf()
    {
        Assert.Throws<InvalidDataException>(() => PdfDocument.Open("hello"u8.ToArray()));
        Assert.Throws<InvalidDataException>(() => PdfDocument.Open("%PDF-1.4\nnothing else"u8.ToArray()));
    }

    private static byte[] Written()
    {
        var builder = new PdfDocumentBuilder { Title = "Titre — Ωμέγα", Author = "Auteur", CreationDate = new DateTimeOffset(2026, 10, 6, 14, 30, 0, TimeSpan.FromHours(2)) };
        var flow = new PdfFlowLayout(builder);
        flow.Page.DrawText(Multilingual, 50, 100, PdfFont.Sans, 12);
        flow.Y = 130;
        flow.AddParagraph("Seconde ligne justifiée avec plusieurs mots pour remplir la largeur de la page entière. Encore une phrase.", PdfFont.Serif, 11, alignment: PdfTextAlignment.Justify);
        builder.AddPage(842, 595).DrawText("Page deux", 50, 60, PdfFont.Sans, 12);
        return builder.ToArray();
    }

    private static PdfRectangle Round(PdfRectangle r) => new(Math.Round(r.Left, 2), Math.Round(r.Bottom, 2), Math.Round(r.Right, 2), Math.Round(r.Top, 2));
}
