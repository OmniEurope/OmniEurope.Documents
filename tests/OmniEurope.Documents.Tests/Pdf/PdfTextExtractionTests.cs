// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>
/// Text operators and font encodings (ISO 32000-1 §9.4, §9.6, §9.7): TJ adjustments, the ' and " operators,
/// missing fonts, CID widths, UCS-2 encodings, named and built-in encodings. Helvetica letters advance by the
/// Helvetica metrics (A and B: 667/1000 em).
/// </summary>
public sealed class PdfTextExtractionTests
{
    private const string Helvetica = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>";

    [Fact]
    public void Tj_array_numbers_move_the_next_glyph_back_by_thousandths_of_the_size()
    {
        // At 10 points A advances 6.67; -1000 moves B 10 further right.
        var letters = Letters("BT /F1 10 Tf 0 0 Td [(A) -1000 (B)] TJ ET", Helvetica);

        Assert.Equal(["A", "B"], letters.Select(l => l.Value));
        Assert.Equal([0.0, 16.67], letters.Select(l => Math.Round(l.X, 2)));
    }

    [Fact]
    public void Quote_operators_move_to_the_next_line_and_set_spacing()
    {
        // Leading 12 from y 100: ' shows B at y 88; " sets word spacing 2 and character spacing 1, then shows CD at y 76.
        var letters = Letters("BT /F1 10 Tf 12 TL 0 100 Td (A) Tj (B) ' 2 1 (CD) \" ET", Helvetica);

        Assert.Equal(["A", "B", "C", "D"], letters.Select(l => l.Value));
        Assert.Equal([100.0, 88, 76, 76], letters.Select(l => Math.Round(l.Y, 2)));
        // C advances 7.22 plus the character spacing.
        Assert.Equal(8.22, Math.Round(letters[3].X - letters[2].X, 2));
    }

    [Fact]
    public void Text_without_a_known_font_is_not_extracted()
    {
        var letters = Letters("BT (A) Tj /F9 10 Tf (B) Tj /F1 10 Tf (C) Tj ET", Helvetica);

        Assert.Equal(["C"], letters.Select(l => l.Value));
    }

    [Fact]
    public void Cid_widths_accept_lists_ranges_and_skip_malformed_entries()
    {
        // W: /Bad (skipped), 1 [600 700] (CIDs 1 and 2), 3 4 900 (CIDs 3 and 4), then a lone 7.
        var font = "<< /Type /Font /Subtype /Type0 /BaseFont /Test /Encoding /Identity-H /DescendantFonts [6 0 R] >>";
        var descendant = "<< /Type /Font /Subtype /CIDFontType2 /BaseFont /Test /DW 300 /W [/Bad 1 [600 700] 3 4 900 7] /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> >>";
        var letters = Letters("BT /F1 10 Tf <00010002000300040005> Tj ET", font, descendant);

        Assert.Equal([6.0, 7, 9, 9, 3], letters.Select(l => Math.Round(l.Width, 2)));
    }

    [Fact]
    public void Ucs2_encodings_use_the_code_as_the_character()
    {
        var font = "<< /Type /Font /Subtype /Type0 /BaseFont /Test /Encoding /UniGB-UCS2-H /DescendantFonts [6 0 R] >>";
        var descendant = "<< /Type /Font /Subtype /CIDFontType0 /BaseFont /Test /CIDSystemInfo << /Registry (Adobe) /Ordering (GB1) /Supplement 2 >> >>";

        var letters = Letters("BT /F1 10 Tf <4E2D6587> Tj ET", font, descendant);

        Assert.Equal("中文", string.Concat(letters.Select(l => l.Value)));
    }

    [Fact]
    public void Named_and_built_in_encodings_map_codes_to_text()
    {
        // MacRoman 0x8E is é; the embedded Type 1 program maps code 65 to "B" in its own encoding.
        var mac = Letters("BT /F1 10 Tf <8E> Tj ET", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /MacRomanEncoding >>");
        var program = "%!PS-AdobeFont-1.0: Test\n/Encoding 256 array\ndup 65 /B put\nreadonly def\ncurrentfile eexec\n";
        var builtIn = Letters(
            "BT /F1 10 Tf (A) Tj ET",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Test /FirstChar 65 /LastChar 65 /Widths [500] /FontDescriptor 6 0 R >>",
            "<< /Type /FontDescriptor /FontName /Test /Flags 32 /FontFile 7 0 R >>",
            RawPdf.Stream($"/Length1 {program.Length} /Length2 0 /Length3 0", Encoding.ASCII.GetBytes(program)));

        Assert.Equal("é", Assert.Single(mac).Value);
        Assert.Equal("B", Assert.Single(builtIn).Value);
    }

    [Fact]
    public void An_unreadable_embedded_program_falls_back_to_the_widths()
    {
        var letters = Letters(
            "BT /F1 10 Tf (A) Tj ET",
            "<< /Type /Font /Subtype /TrueType /BaseFont /Test /FirstChar 65 /LastChar 65 /Widths [400] /FontDescriptor 6 0 R >>",
            "<< /Type /FontDescriptor /FontName /Test /Flags 32 /FontFile2 7 0 R >>",
            RawPdf.Stream(string.Empty, [1, 2, 3, 4, 5, 6, 7, 8]));

        Assert.Equal(("A", 4.0), (Assert.Single(letters).Value, Math.Round(letters[0].Width, 2)));
    }

    // Object 5 is the font /F1; further objects follow from 6.
    private static IReadOnlyList<PdfLetter> Letters(string content, string font, params object[] more) =>
        PdfDocument.Open(RawPdf.Page(content, "/Font << /F1 5 0 R >>", string.Empty, [font, .. more])).GetPage(1).Letters;
}
