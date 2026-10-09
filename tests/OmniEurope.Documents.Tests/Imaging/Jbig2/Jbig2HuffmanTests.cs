// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging.Jbig2;
using static OmniEurope.Documents.Tests.Imaging.Jbig2.SegmentWriter;

namespace OmniEurope.Documents.Tests.Imaging.Jbig2;

/// <summary>
/// Huffman coded symbol dictionaries and text regions (T.88 annex B tables, 6.5.9 collective bitmaps, 7.4.3.1.7
/// symbol ID codes, 7.4.13 table segments), on bitstreams encoded by the test writer.
/// </summary>
public sealed class Jbig2HuffmanTests
{
    private const int SymbolDictionary = 0;
    private const int Text = 6;
    private const int Tables = 53;

    [Fact]
    public void A_huffman_dictionary_cuts_an_uncompressed_collective_bitmap_and_a_text_region_reads_its_symbol_codes()
    {
        // 23 symbols one pixel wide and three high: column i holds the bits of i % 7 + 1.
        var symbols = Enumerable.Range(0, 23).Select(Column).ToArray();
        var dictionary = new BitWriter();
        HuffmanWriter.B4.Write(dictionary, 3);
        HuffmanWriter.B2.Write(dictionary, 1);
        for (var i = 1; i < 23; i++)
        {
            HuffmanWriter.B2.Write(dictionary, 0);
        }

        HuffmanWriter.B2.Write(dictionary, null);
        HuffmanWriter.B1.Write(dictionary, 0);
        dictionary.Bytes([.. Enumerable.Range(0, 3).SelectMany(row => Packed(symbols.Select(s => s[0, row]).ToArray()))]);
        HuffmanWriter.B1.Write(dictionary, 0);
        HuffmanWriter.B1.Write(dictionary, 23);

        // Symbol code lengths 1, 0 0 0, 2, 3, 4, 5, eleven 0, 7 7 7 7: run codes 1, 33 (3 zeros), 2, 3, 4, 5, 34 (11 zeros),
        // 7, 32 (the previous length 3 more times); each run code 4 bits long.
        int[] runCodes = [1, 2, 3, 4, 5, 7, 32, 33, 34];
        var runLengths = Enumerable.Range(0, 35).Select(i => runCodes.Contains(i) ? 4 : 0).ToArray();
        var run = HuffmanWriter.Codes(runLengths);
        int[] lengths = [1, 0, 0, 0, 2, 3, 4, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 7, 7, 7, 7];
        var codes = HuffmanWriter.Codes(lengths);
        var text = new BitWriter();
        foreach (var length in runLengths)
        {
            text.Write(length, 4);
        }

        text.Write(run[1] + run[33] + "000" + run[2] + run[3] + run[4] + run[5] + run[34] + "0000000" + run[7] + run[32] + "00");
        text.Align();

        // Strip at T 2 (B.11: 1 then 3 strips of one row, from -1); symbols 0, 22, 4 at S 1, then 1 + 2, then 3 + 1.
        HuffmanWriter.B11.Write(text, 1);
        HuffmanWriter.B11.Write(text, 3);
        HuffmanWriter.B6.Write(text, 1);
        text.Write(codes[0]);
        HuffmanWriter.B8.Write(text, 2);
        text.Write(codes[22]);
        HuffmanWriter.B8.Write(text, 1);
        text.Write(codes[4]);
        HuffmanWriter.B8.Write(text, null);

        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(10, 6));
        var d = segments.Add(SymbolDictionary, [.. Be16(1), .. Be(23), .. Be(23), .. dictionary.ToArray()]);
        segments.Add(Text, [.. Region(10, 6), .. Be16(1 | (1 << 4)), .. Be16(0), .. Be(3), .. text.ToArray()], referred: [d]);

        var page = Jbig2Decoder.Decode(segments.ToArray());

        Jbig2Images.AssertSame(Jbig2Images.Compose(10, 6, (symbols[0], 1, 2), (symbols[22], 3, 2), (symbols[4], 4, 2)), page);
    }

    [Fact]
    public void A_huffman_dictionary_reads_an_mmr_collective_bitmap_and_another_refines_and_aggregates_its_symbols()
    {
        // Dictionary 1: one MMR coded height class, cut into s0 (3 wide) and s1 (4 wide). Dictionary 2: P, s0 refined
        // to 4 by 3, and Q, s0 and s1 side by side; it exports s0, P and Q.
        var collective = Jbig2Images.Parse("##..#.#", "#..#.##");
        var mmr = Jbig2GenericTests.MmrRows(true, "##..#.#", "#..#.##");
        var s0 = Cut(collective, 0, 3);
        var s1 = Cut(collective, 3, 4);
        var first = new BitWriter();
        HuffmanWriter.B4.Write(first, 2);
        HuffmanWriter.B2.Write(first, 3);
        HuffmanWriter.B2.Write(first, 1);
        HuffmanWriter.B2.Write(first, null);
        HuffmanWriter.B1.Write(first, mmr.Length);
        first.Bytes(mmr);
        HuffmanWriter.B1.Write(first, 0);
        HuffmanWriter.B1.Write(first, 2);

        var p = Jbig2Images.Compose(4, 3, (s0, 0, 0));
        p[2, 2] = 1;
        var refinement = new ArithmeticWriter();
        refinement.Refinement("GR", p, s0, 0, 0, 1, [], false);
        var refinementData = refinement.Mq.Finish();
        var q = Jbig2Images.Compose(7, 3, (s0, 0, 0), (s1, 3, 0));
        var second = new BitWriter();
        HuffmanWriter.B4.Write(second, 3);
        HuffmanWriter.B2.Write(second, 4);
        HuffmanWriter.B1.Write(second, 1);
        second.Write(0, 2);
        HuffmanWriter.B15.Write(second, 0);
        HuffmanWriter.B15.Write(second, 0);
        HuffmanWriter.B1.Write(second, refinementData.Length);
        second.Bytes(refinementData);
        HuffmanWriter.B2.Write(second, 3);
        HuffmanWriter.B1.Write(second, 2);
        HuffmanWriter.B11.Write(second, 1);
        HuffmanWriter.B11.Write(second, 1);
        HuffmanWriter.B6.Write(second, 0);
        second.Write("00 0");
        HuffmanWriter.B8.Write(second, 1);
        second.Write("01 0");
        HuffmanWriter.B8.Write(second, null);
        HuffmanWriter.B2.Write(second, null);
        HuffmanWriter.B1.Write(second, 0);
        HuffmanWriter.B1.Write(second, 1);
        HuffmanWriter.B1.Write(second, 1);
        HuffmanWriter.B1.Write(second, 2);

        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(24, 4));
        var d1 = segments.Add(SymbolDictionary, [.. Be16(1), .. Be(2), .. Be(2), .. first.ToArray()]);
        var d2 = segments.Add(SymbolDictionary, [.. Be16(1 | 2 | (1 << 12)), .. Be(3), .. Be(2), .. second.ToArray()], referred: [d1]);
        var text = new ArithmeticWriter();
        text.Integer("IADT", 0);
        text.Integer("IADT", 0);
        text.Integer("IAFS", 0);
        text.SymbolId(0, 2);
        text.Integer("IADS", 3);
        text.SymbolId(1, 2);
        text.Integer("IADS", 2);
        text.SymbolId(2, 2);
        text.Integer("IADS", null);
        segments.Add(Text, Jbig2SymbolTests.TextSegment(24, 4, 0x0010, 3, text), referred: [d2]);

        var page = Jbig2Decoder.Decode(segments.ToArray());

        Jbig2Images.AssertSame(Jbig2Images.Compose(24, 4, (s0, 0, 0), (p, 5, 0), (q, 10, 0)), page);
    }

    [Fact]
    public void Text_regions_read_custom_tables_from_table_segments_and_refine_with_their_own_coded_size()
    {
        // One custom table (0 to 63 in 6 bits after a 1-bit prefix, lower and upper lines, out of band) for every
        // value; the second instance is a refinement of symbol 1 one pixel wider.
        var table = new HuffmanWriter([(0, 1, 6)], (-1, 2), (64, 3), 3);
        var tableBits = new BitWriter();
        tableBits.Write("01 110  10  11  11");
        byte[] tableSegment = [0x23, .. Be(0), .. Be(64), .. tableBits.ToArray()];
        var a = Jbig2Images.Glyph(3, 4, 0);
        var b = Jbig2Images.Glyph(4, 4, 1);
        var refined = Jbig2Images.Glyph(5, 4, 1);
        refined[2, 1] ^= 1;
        var refinement = new ArithmeticWriter();
        refinement.Refinement("GR", refined, b, 0, 0, 1, [], false);
        var refinementData = refinement.Mq.Finish();
        var text = new BitWriter();
        text.Write(Enumerable.Range(0, 35).Select(i => i is 1 ? "0001" : "0000").Aggregate(string.Concat));
        text.Write("0 0");
        text.Align();
        table.Write(text, 0);
        table.Write(text, 1);
        table.Write(text, 2);
        text.Write("0 0");
        table.Write(text, 3);
        text.Write("1 1");
        table.Write(text, 1);
        table.Write(text, 0);
        table.Write(text, 0);
        table.Write(text, 0);
        table.Write(text, refinementData.Length);
        text.Bytes(refinementData);
        table.Write(text, null);

        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(16, 6));
        var t = segments.Add(Tables, tableSegment);
        var d = segments.Add(SymbolDictionary, Jbig2SymbolTests.Dictionary([a, b]));
        var huffmanFlags = 3 | (3 << 2) | (3 << 4) | (3 << 6) | (3 << 8) | (3 << 10) | (3 << 12) | (1 << 14);
        segments.Add(Text, [.. Region(16, 6), .. Be16(1 | 2 | (1 << 4) | (1 << 15)), .. Be16(huffmanFlags), .. Be(2), .. text.ToArray()],
            referred: [d, t, t, t, t, t, t, t, t]);

        var page = Jbig2Decoder.Decode(segments.ToArray());

        // STRIPT: 0 then +1; S 2 for symbol 0 (3 wide), then 2 + 3 - 1 + 3 = 7 for the refinement.
        Jbig2Images.AssertSame(Jbig2Images.Compose(16, 6, (a, 2, 1), (refined, 7, 1)), page);
    }

    [Fact]
    public void Huffman_tables_decode_their_range_lines_and_refuse_codes_they_lack()
    {
        var writer = new BitWriter();
        HuffmanWriter.B15.Write(writer, -100);
        HuffmanWriter.B15.Write(writer, 1000);
        HuffmanWriter.B1.Write(writer, 70000);
        HuffmanWriter.B6.Write(writer, -3000);
        writer.Write("1111111");
        var data = writer.ToArray();
        var reader = new Jbig2BitReader(data, 0, data.Length);
        var custom = new Jbig2HuffmanTable([new Jbig2HuffmanLine(5, 1, 0), new Jbig2HuffmanLine(0, 0, 0)]);

        Assert.Equal(-100, Jbig2HuffmanTable.Standard(15).DecodeValue(reader));
        Assert.Equal(1000, Jbig2HuffmanTable.Standard(15).DecodeValue(reader));
        Assert.Equal(70000, Jbig2HuffmanTable.Standard(1).DecodeValue(reader));
        Assert.Equal(-3000, Jbig2HuffmanTable.Standard(6).DecodeValue(reader));
        Assert.Throws<InvalidDataException>(() => custom.Decode(reader));
        Assert.Throws<InvalidDataException>(() => Jbig2HuffmanTable.Standard(2).DecodeValue(new Jbig2BitReader([0xFC], 0, 1)));
        Assert.Throws<InvalidDataException>(() => Jbig2HuffmanTable.Standard(1).Decode(new Jbig2BitReader([], 0, 0)));
        Assert.Throws<InvalidDataException>(() => Jbig2HuffmanTable.Parse([0, 0], 0, 2));
    }

    [Fact]
    public void Standard_tables_are_built_once_and_codes_are_assigned_canonically()
    {
        for (var number = 1; number <= 15; number++)
        {
            var table = Jbig2HuffmanTable.Standard(number);
            Assert.Same(table, Jbig2HuffmanTable.Standard(number));
        }

        Assert.Equal([0u, 2u, 3u], Jbig2HuffmanTable.AssignCodes([1, 2, 2]));
        Assert.Equal([2u, 0u, 3u, 0u], Jbig2HuffmanTable.AssignCodes([2, 1, 2, 0]));
    }

    [Fact]
    public void Text_regions_refuse_missing_custom_tables_and_impossible_symbol_codes()
    {
        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(8, 8));
        var d = segments.Add(SymbolDictionary, Jbig2SymbolTests.Dictionary([Jbig2Images.Glyph(3, 3, 0)]));
        segments.Add(Text, [.. Region(8, 8), .. Be16(1), .. Be16(3), .. Be(1), 0, 0, 0, 0], referred: [d]);
        var repeat = new BitWriter();
        repeat.Write(Enumerable.Range(0, 35).Select(i => i is 32 ? "0001" : "0000").Aggregate(string.Concat));
        repeat.Write("0 00");
        var noPrevious = new SegmentWriter();
        noPrevious.Add(48, PageInfo(8, 8));
        var d2 = noPrevious.Add(SymbolDictionary, Jbig2SymbolTests.Dictionary([Jbig2Images.Glyph(3, 3, 0)]));
        noPrevious.Add(Text, [.. Region(8, 8), .. Be16(1), .. Be16(0), .. Be(1), .. repeat.ToArray()], referred: [d2]);
        var dictionary = new SegmentWriter();
        dictionary.Add(48, PageInfo(8, 8));
        dictionary.Add(SymbolDictionary, [.. Be16(1 | (3 << 2)), .. Be(1), .. Be(1), 0]);

        Assert.Throws<InvalidDataException>(() => Jbig2Decoder.Decode(segments.ToArray()));
        Assert.Throws<InvalidDataException>(() => Jbig2Decoder.Decode(noPrevious.ToArray()));
        Assert.Throws<InvalidDataException>(() => Jbig2Decoder.Decode(dictionary.ToArray()));
    }

    private static Jbig2Bitmap Column(int index)
    {
        var symbol = new Jbig2Bitmap(1, 3);
        var bits = (index % 7) + 1;
        for (var y = 0; y < 3; y++)
        {
            symbol[0, y] = (bits >> (2 - y)) & 1;
        }

        return symbol;
    }

    private static Jbig2Bitmap Cut(byte[] pixels, int x, int width)
    {
        var bitmap = new Jbig2Bitmap(7, 2);
        pixels.CopyTo(bitmap.Pixels, 0);
        return bitmap.Extract(x, 0, width, 2);
    }

    private static byte[] Packed(int[] pixels)
    {
        var bytes = new byte[(pixels.Length + 7) / 8];
        for (var i = 0; i < pixels.Length; i++)
        {
            bytes[i >> 3] |= (byte)(pixels[i] << (7 - (i & 7)));
        }

        return bytes;
    }
}
