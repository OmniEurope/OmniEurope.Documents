// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Objects;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>
/// File structure (ISO 32000-1 §7.5): cross-reference streams with filter arrays, hybrid files, object streams,
/// and the damage real files show: broken tables, wrong offsets, a stream length that refers to itself.
/// </summary>
public sealed class PdfStructureTests
{
    private const string Content = "BT /F1 10 Tf 10 10 Td (Hi) Tj ET";

    [Fact]
    public void Cross_reference_streams_may_list_their_filters_in_arrays()
    {
        var document = PdfDocument.Open(XrefStreamFile(PageObjects(), hybrid: false));

        Assert.False(document.WasRepaired);
        Assert.Equal("Hi", document.GetPage(1).Text);
    }

    [Fact]
    public void Hybrid_files_add_the_entries_of_their_stream_to_the_table()
    {
        // The classic table lists objects 1 to 4; the font (object 5) is only in the cross-reference stream.
        var document = PdfDocument.Open(XrefStreamFile(PageObjects(), hybrid: true));

        Assert.False(document.WasRepaired);
        Assert.Equal("Hi", document.GetPage(1).Text);
    }

    [Fact]
    public void Objects_in_object_streams_need_a_stream_that_holds_them()
    {
        // Object 9 lives in object stream 8; 6 points into object 5, which is not a stream; 7 into stream 8 at an
        // index stream 8 does not hold.
        var objects = PageObjects();
        objects.Add("0");
        objects.Add("0");
        objects.Add(RawPdf.Stream("/Type /ObjStm /N 1 /First 4", RawPdf.Ascii("9 0 (in a stream)")));
        var rows = new Dictionary<int, (int, int, int)> { [6] = (2, 5, 0), [7] = (2, 8, 3), [9] = (2, 8, 0) };

        var store = PdfDocument.Open(XrefStreamFile(objects, hybrid: false, rows)).Store;

        Assert.Null(store.Resolve(new PdfReference(6, 0)));
        Assert.Null(store.Resolve(new PdfReference(7, 0)));
        Assert.Equal("in a stream", Encoding.ASCII.GetString(((PdfString)store.Resolve(new PdfReference(9, 0))!).Bytes));
    }

    [Theory]
    [InlineData("xref\n0 6\n", "xref\nx 6\n")]
    [InlineData("0000000000 65535 f \n", "0000000000 65535 f \nfoo")]
    public void Broken_tables_are_rebuilt_from_the_objects(string original, string broken)
    {
        var text = Encoding.Latin1.GetString(RawPdf.Page(Content, "/Font << /F1 5 0 R >>", string.Empty, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));

        var document = PdfDocument.Open(Encoding.Latin1.GetBytes(text.Replace(original, broken, StringComparison.Ordinal)));

        Assert.True(document.WasRepaired);
        Assert.Equal("Hi", document.GetPage(1).Text);
    }

    [Fact]
    public void A_wrong_offset_is_corrected_by_finding_the_object_header()
    {
        // Twelve objects, so "11 0 obj" also ends with "1 0 obj"; the catalog's offset (9) is replaced by 3.
        var fillers = Enumerable.Range(6, 7).Select(i => (object)i.ToString(CultureInfo.InvariantCulture)).ToArray();
        var text = Encoding.Latin1.GetString(RawPdf.Page(Content, "/Font << /F1 5 0 R >>", string.Empty, ["<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", .. fillers]));
        var wrong = text.Replace("0000000009 00000 n", "0000000003 00000 n", StringComparison.Ordinal);

        var document = PdfDocument.Open(Encoding.Latin1.GetBytes(wrong));

        Assert.False(document.WasRepaired);
        Assert.Equal("Hi", document.GetPage(1).Text);
    }

    [Fact]
    public void A_stream_length_referring_to_its_own_object_falls_back_to_endstream()
    {
        // The page's contents are replaced by object 6, whose Length is 6 0 R; its data is hexadecimal (filter array).
        var contents = RawPdf.Ascii("<< /Length 6 0 R /Filter [/ASCIIHexDecode] /DecodeParms [null] >>\nstream\n" + Convert.ToHexString(Encoding.ASCII.GetBytes(Content)) + ">\nendstream");
        var pdf = RawPdf.Page("BT ET", "/Font << /F1 5 0 R >>", "/Contents 6 0 R", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", contents);

        Assert.Equal("Hi", PdfDocument.Open(pdf).GetPage(1).Text);
    }

    [Fact]
    public void An_object_stream_count_beyond_its_header_reads_only_the_pairs_present()
    {
        var objects = PageObjects();
        objects.Add("0");
        objects.Add("0");
        objects.Add(RawPdf.Stream("/Type /ObjStm /N 2147483647 /First 4", RawPdf.Ascii("9 0 (in a stream)")));
        var rows = new Dictionary<int, (int, int, int)> { [9] = (2, 8, 0) };

        var store = PdfDocument.Open(XrefStreamFile(objects, hybrid: false, rows)).Store;

        Assert.Equal("in a stream", Encoding.ASCII.GetString(((PdfString)store.Resolve(new PdfReference(9, 0))!).Bytes));
    }

    [Theory]
    [InlineData("/W [0 0 0] /Index [0 2147483647]")]
    [InlineData("/W [1 4]")]
    [InlineData("/W [1 9 1]")]
    public void Cross_reference_streams_with_forged_widths_are_rebuilt(string widths)
    {
        var text = Encoding.Latin1.GetString(XrefStreamFile(PageObjects(), hybrid: false));

        var document = PdfDocument.Open(Encoding.Latin1.GetBytes(text.Replace("/W [1 4 1]", widths, StringComparison.Ordinal)));

        Assert.True(document.WasRepaired);
        Assert.Equal("Hi", document.GetPage(1).Text);
    }

    // Catalog, pages, page, contents, font.
    private static List<object> PageObjects() =>
    [
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
        RawPdf.Stream(string.Empty, RawPdf.Ascii(Content)),
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
    ];

    // Objects numbered from 1, then a cross-reference stream (W [1 4 1], hexadecimal data) listing them; rows
    // replace or add entries (type, second field, third field). A hybrid file also has a classic table for
    // objects 1 to 4 whose trailer points to the stream with XRefStm.
    private static byte[] XrefStreamFile(List<object> objects, bool hybrid, Dictionary<int, (int Type, int Second, int Third)>? rows = null)
    {
        using var output = new MemoryStream();
        output.Write(RawPdf.Ascii("%PDF-1.7\n"));
        var offsets = new List<long>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Position);
            output.Write(RawPdf.Ascii($"{i + 1} 0 obj\n"));
            output.Write(objects[i] is byte[] bytes ? bytes : RawPdf.Ascii((string)objects[i]));
            output.Write(RawPdf.Ascii("\nendobj\n"));
        }

        var xrefNumber = objects.Count + 1;
        var xrefOffset = output.Position;
        var all = new Dictionary<int, (int Type, int Second, int Third)> { [0] = (0, 0, 255), [xrefNumber] = (1, (int)xrefOffset, 0) };
        for (var i = 0; i < offsets.Count; i++)
        {
            all[i + 1] = (1, (int)offsets[i], 0);
        }

        foreach (var (number, row) in rows ?? [])
        {
            all[number] = row;
        }

        var size = all.Keys.Max() + 1;
        var data = new StringBuilder();
        for (var n = 0; n < size; n++)
        {
            var (type, second, third) = all.TryGetValue(n, out var row) ? row : (0, 0, 0);
            data.Append(CultureInfo.InvariantCulture, $"{type:X2}{second:X8}{third:X2}");
        }

        data.Append('>');
        var hex = RawPdf.Ascii(data.ToString());
        output.Write(RawPdf.Ascii($"{xrefNumber} 0 obj\n"));
        output.Write(RawPdf.Stream($"/Type /XRef /Size {size} /W [1 4 1] /Root 1 0 R /Filter [/ASCIIHexDecode] /DecodeParms [null]", hex));
        output.Write(RawPdf.Ascii("\nendobj\n"));
        var start = xrefOffset;
        if (hybrid)
        {
            start = output.Position;
            output.Write(RawPdf.Ascii("xref\n0 5\n0000000000 65535 f \n"));
            foreach (var offset in offsets.Take(4))
            {
                output.Write(RawPdf.Ascii(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n"));
            }

            output.Write(RawPdf.Ascii($"trailer\n<< /Size 5 /Root 1 0 R /XRefStm {xrefOffset} >>\n"));
        }

        output.Write(RawPdf.Ascii($"startxref\n{start}\n%%EOF\n"));
        return output.ToArray();
    }
}
