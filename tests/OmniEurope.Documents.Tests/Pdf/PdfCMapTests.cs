// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>CMaps and glyph names, written from the PDF specification (§9.7.5, §9.10.3) and the Adobe Glyph List rules.</summary>
public sealed class PdfCMapTests
{
    [Fact]
    public void Reads_every_kind_of_cmap_section()
    {
        const string source = """
            /CIDInit /ProcSet findresource begin
            1 begincodespacerange <0000> <FFFF> endcodespacerange
            1 beginbfchar <0001> <0041> endbfchar
            2 beginbfrange <0010> <0012> [<0061> <0062> <0063>] <00FE> <0101> <00FE> endbfrange
            1 begincidchar <0020> 500 endcidchar
            1 begincidrange <0030> <0032> 700 endcidrange
            endcmap
            """;

        var cmap = PdfCMap.Parse(Encoding.ASCII.GetBytes(source));

        Assert.Equal("A", cmap.ToUnicode(0x01));
        Assert.Equal(["a", "b", "c"], new uint[] { 0x10, 0x11, 0x12 }.Select(cmap.ToUnicode));
        Assert.Equal(["þ", "ÿ", "Ā", "ā"], new uint[] { 0xFE, 0xFF, 0x100, 0x101 }.Select(cmap.ToUnicode));
        Assert.Equal((500, 700, 702), (cmap.ToCid(0x20), cmap.ToCid(0x30), cmap.ToCid(0x32)));
        Assert.Equal([(0x0001u, 2), (0x0010u, 2)], cmap.Codes([0x00, 0x01, 0x00, 0x10]));
    }

    [Fact]
    public void Usecmap_identity_adds_two_byte_codes()
    {
        var cmap = PdfCMap.Parse(Encoding.ASCII.GetBytes("/Identity-H usecmap 1 beginbfchar <0005> <0045> endbfchar"));

        Assert.Equal([(0x0005u, 2)], cmap.Codes([0x00, 0x05]));
        Assert.Equal("E", cmap.ToUnicode(5));
    }

    [Theory]
    [InlineData("1 begincodespacerange <> <> endcodespacerange", 1)]
    [InlineData("2 begincodespacerange <> <> <0000> <FFFF> endcodespacerange", 2)]
    [InlineData("1 begincodespacerange <0000000000> <FFFFFFFFFF> endcodespacerange", 1)]
    public void Code_spaces_outside_one_to_four_bytes_are_ignored(string ranges, int length)
    {
        // An empty range would match codes of no byte, and splitting the text would never advance.
        var cmap = PdfCMap.Parse(Encoding.ASCII.GetBytes(ranges));

        var codes = cmap.Codes([0x00, 0x41, 0x00, 0x42]).Take(10).ToList();

        Assert.Equal(4 / length, codes.Count);
        Assert.All(codes, c => Assert.Equal(length, c.Length));
    }

    [Theory]
    [InlineData("A", "A")]
    [InlineData("uni00410042", "AB")]
    [InlineData("uni004", null)]
    [InlineData("u1F600", "\U0001F600")]
    [InlineData("uD800", null)]
    [InlineData("u110000", null)]
    [InlineData("afii10017", "А")]
    [InlineData("afii99999", null)]
    [InlineData("", null)]
    [InlineData("f_f_i", "ffi")]
    [InlineData("f_f_qzx", null)]
    [InlineData("Ccedilla", "Ç")]
    [InlineData("Gcommaaccent", "Ģ")]
    public void Glyph_names_follow_the_glyph_list_rules(string name, string? text)
    {
        Assert.Equal(text, GlyphNames.ToUnicode(name));
    }

    [Fact]
    public void Codes_follow_the_code_space_or_fall_back_to_its_shortest_length()
    {
        // Without a code space, single bytes; with a two-byte space, a final lone byte still takes the shortest length.
        var none = PdfCMap.Parse(Encoding.ASCII.GetBytes("1 beginbfchar <41> <0042> endbfchar"));
        var twoBytes = PdfCMap.Parse(Encoding.ASCII.GetBytes("1 begincodespacerange <0000> <FFFF> endcodespacerange"));

        Assert.Equal([(0x41u, 1), (0x42u, 1)], none.Codes([0x41, 0x42]));
        Assert.Equal("B", none.ToUnicode(0x41));
        Assert.Equal([(0x0102u, 2), (0x03u, 2)], twoBytes.Codes([0x01, 0x02, 0x03]));
    }

    [Fact]
    public void Ranges_with_a_bound_that_is_not_a_string_are_skipped()
    {
        var cmap = PdfCMap.Parse(Encoding.ASCII.GetBytes("2 beginbfrange 5 <0002> <0041> <0003> <0003> <0043> endbfrange"));

        Assert.Equal((null, "C"), (cmap.ToUnicode(2), cmap.ToUnicode(3)));
    }
}
