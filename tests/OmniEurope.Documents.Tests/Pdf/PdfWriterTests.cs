// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Tests.Pdf;

public sealed class PdfWriterTests
{
    [Fact]
    public void Writes_a_structurally_valid_file_with_correct_cross_references()
    {
        var bytes = Sample();
        var text = Encoding.Latin1.GetString(bytes);

        Assert.StartsWith("%PDF-1.7\n", text);
        Assert.EndsWith("%%EOF\n", text);
        var startXref = int.Parse(Regex.Match(text, @"startxref\n(\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.StartsWith("xref\n0 ", text[startXref..]);
        var entries = Regex.Matches(text[startXref..], @"(\d{10}) 00000 n ").Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
        Assert.NotEmpty(entries);
        for (var i = 0; i < entries.Count; i++)
        {
            Assert.StartsWith($"{i + 1} 0 obj\n", text[entries[i]..]);
        }

        Assert.Matches(@"/Size " + (entries.Count + 1), text);
    }

    [Fact]
    public void Same_content_gives_the_same_bytes()
    {
        Assert.Equal(Sample(), Sample());
    }

    [Fact]
    public void Embeds_subset_fonts_with_unicode_maps_and_metadata()
    {
        var text = Encoding.Latin1.GetString(Sample());

        Assert.Matches(@"/BaseFont /[A-Z]{6}\+LiberationSans", text);
        Assert.Contains("/Encoding /Identity-H", text);
        Assert.Contains("/Subtype /CIDFontType2", text);
        Assert.Contains("/FontFile2", text);
        Assert.Contains("/ToUnicode", text);
        Assert.Contains("/Producer (OmniEurope.Documents)", text);
        Assert.Contains("/Title <FEFF", text);
        var toUnicode = Inflated(Sample()).First(s => s.Contains("beginbfchar", StringComparison.Ordinal));
        Assert.Contains("<03A9>", toUnicode);
    }

    [Fact]
    public void Measures_text_with_fallback_faces()
    {
        var document = new PdfDocumentBuilder();

        var latin = document.MeasureText("Hello", new PdfFont("Arial"), 12);
        var greek = document.MeasureText("Ω", new PdfFont("Cambria"), 12);

        Assert.Equal((1479 + 1139 + 455 + 455 + 1139) * 12 / 2048.0, latin, 6);
        Assert.True(greek > 0);
        Assert.Equal(document.MeasureText("a b", PdfFont.Sans, 10), document.MeasureText("a\tb", PdfFont.Sans, 10), 6);
    }

    [Fact]
    public void Wraps_at_spaces_and_hyphens_and_breaks_long_words()
    {
        var document = new PdfDocumentBuilder();
        var font = PdfFont.Sans;
        var width = document.MeasureText("aaaa bbbb", font, 10) + 0.01;

        var lines = TextWrapper.Wrap(document, "aaaa bbbb cccc-dddd\nnext " + new string('x', 40), font, 10, width);

        Assert.Equal("aaaa bbbb", lines[0].Text);
        Assert.Equal("cccc-dddd", lines[1].Text);
        Assert.True(lines[1].EndsParagraph);
        Assert.Equal("next", lines[2].Text);
        Assert.All(lines, l => Assert.True(l.Width <= width + 0.001));
        Assert.Equal(new string('x', 40), string.Concat(lines.Skip(3).Select(l => l.Text)));
    }

    [Fact]
    public void Flow_layout_breaks_pages_repeats_table_headers_and_numbers_footers()
    {
        var document = new PdfDocumentBuilder();
        var footers = new List<string>();
        var flow = new PdfFlowLayout(document, new PdfFlowOptions { Footer = (_, n, total) => footers.Add($"{n}/{total}") });
        flow.AddParagraph(string.Join(' ', Enumerable.Repeat("Lorem ipsum dolor sit amet.", 120)), PdfFont.Serif, 11, alignment: PdfTextAlignment.Justify);
        var table = new PdfTable(1, 3, 1).Header("#", "Contrôle", "Statut");
        for (var i = 0; i < 80; i++)
        {
            table.Row(i.ToString(CultureInfo.InvariantCulture), $"Contrôle numéro {i} avec un libellé assez long pour passer à la ligne dans la cellule", new PdfTableCell("OK") { Fill = PdfColor.FromHex("C6EFCE") });
        }

        flow.AddTable(table);
        flow.Finish();

        Assert.True(document.Pages.Count >= 3);
        Assert.Equal(Enumerable.Range(1, document.Pages.Count).Select(n => $"{n}/{document.Pages.Count}"), footers);
        Assert.True(document.ToArray().Length > 1000);
    }

    [Fact]
    public void Flow_layout_aligns_images_and_shrinks_wide_ones()
    {
        var document = new PdfDocumentBuilder();
        var flow = new PdfFlowLayout(document);
        var image = document.AddImage(new RasterImage(2, 2, ImageColorType.Rgb, new byte[12]));
        flow.AddImage(image, 100, 50, PdfTextAlignment.Center);
        flow.AddImage(image, 100, 50, PdfTextAlignment.Right);
        flow.AddImage(image, 5000, 1000);
        flow.Finish();

        var bounds = PdfDocument.Open(document.ToArray()).GetPage(1).Images.Select(i => i.Bounds).ToList();

        Assert.Equal(3, bounds.Count);
        Assert.Equal(flow.Left + ((flow.ContentWidth - 100) / 2), bounds[0].Left, 2);
        Assert.Equal(flow.Left + flow.ContentWidth, bounds[1].Right, 2);
        Assert.Equal(flow.Left, bounds[2].Left, 2);
        Assert.Equal(flow.ContentWidth, bounds[2].Right - bounds[2].Left, 2);
        Assert.Equal(flow.ContentWidth / 5, bounds[2].Top - bounds[2].Bottom, 2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Cmyk_jpegs_with_the_adobe_marker_get_an_inverted_decode_array(bool adobe)
    {
        // SOI, an optional APP14 "Adobe" segment, a baseline frame header with four components, EOI.
        byte[] app14 = [0xFF, 0xEE, 0x00, 0x0E, (byte)'A', (byte)'d', (byte)'o', (byte)'b', (byte)'e', 0, 100, 0, 0, 0, 0, 2];
        byte[] frame = [0xFF, 0xC0, 0x00, 0x14, 8, 0, 1, 0, 1, 4, 1, 0x11, 0, 2, 0x11, 0, 3, 0x11, 0, 4, 0x11, 0];
        byte[] jpeg = [0xFF, 0xD8, .. adobe ? app14 : [], .. frame, 0xFF, 0xD9];
        var document = new PdfDocumentBuilder();
        document.AddPage(100, 100).DrawImage(document.AddImage(jpeg), 0, 0, 10, 10);

        var text = Encoding.Latin1.GetString(document.ToArray());

        Assert.Contains("/DeviceCMYK", text, StringComparison.Ordinal);
        Assert.Equal(adobe, text.Contains("/Decode [1 0 1 0 1 0 1 0]", StringComparison.Ordinal));
    }

    [Fact]
    public void Rejects_invalid_use()
    {
        var document = new PdfDocumentBuilder();
        Assert.Throws<InvalidOperationException>(() => document.ToArray());
        var page = document.AddPage();
        Assert.Throws<InvalidOperationException>(() => page.RestoreState());
        _ = document.ToArray();
        Assert.Throws<InvalidOperationException>(() => document.AddPage());
        Assert.Throws<FormatException>(() => PdfColor.FromHex("nope"));
        Assert.Equal(PdfColor.White, PdfColor.FromHex("#123").ReadableForeground());
    }

    private static byte[] Sample()
    {
        var document = new PdfDocumentBuilder { Title = "Échantillon", Author = "OE" };
        var page = document.AddPage();
        page.DrawText("Hello Ω", 50, 100, PdfFont.Sans, 12);
        page.FillRectangle(50, 120, 100, 20, PdfColor.Orange, opacity: 0.4);
        return document.ToArray();
    }

    private static List<string> Inflated(byte[] pdf)
    {
        var result = new List<string>();
        var text = Encoding.Latin1.GetString(pdf);
        foreach (Match match in Regex.Matches(text, "(?<!end)stream\n"))
        {
            var start = match.Index + match.Length;
            var end = text.IndexOf("\nendstream", start, StringComparison.Ordinal);
            try
            {
                using var zlib = new ZLibStream(new MemoryStream(pdf, start, end - start), CompressionMode.Decompress);
                using var reader = new StreamReader(zlib, Encoding.Latin1);
                result.Add(reader.ReadToEnd());
            }
            catch (InvalidDataException)
            {
                // Not every stream is Flate data.
            }
        }

        return result;
    }
}
