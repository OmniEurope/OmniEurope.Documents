// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging.Jbig2;
using static OmniEurope.Documents.Tests.Imaging.Jbig2.SegmentWriter;

namespace OmniEurope.Documents.Tests.Imaging.Jbig2;

/// <summary>
/// Symbol dictionaries and text regions, arithmetic coded, on bitstreams encoded by the test writer. Each text region
/// lists its values in the order T.88 6.4.5 reads them; the expected page places the symbols by their top-left
/// corners, worked out by hand from the reference corner.
/// </summary>
public sealed class Jbig2SymbolTests
{
    private const int SymbolDictionary = 0;
    private const int Text = 6;
    private static readonly (int X, int Y)[] Standard = [(3, -1), (-3, -1), (2, -2), (-2, -2)];

    private static readonly Jbig2Bitmap A = Jbig2Images.Glyph(3, 7, 0);
    private static readonly Jbig2Bitmap B = Jbig2Images.Glyph(5, 7, 1);
    private static readonly Jbig2Bitmap C = Jbig2Images.Glyph(6, 9, 2);

    [Fact]
    public void A_text_region_places_dictionary_symbols_strip_by_strip()
    {
        // Strip at T 2: A at S 1, B two pixels after the last column of A (S 1 + 3 - 1 + 2); strip at T 12: C at S 3.
        var text = new ArithmeticWriter();
        text.Integer("IADT", 0);
        text.Integer("IADT", 2);
        text.Integer("IAFS", 1);
        text.SymbolId(0, 2);
        text.Integer("IADS", 2);
        text.SymbolId(1, 2);
        text.Integer("IADS", null);
        text.Integer("IADT", 10);
        text.Integer("IAFS", 2);
        text.SymbolId(2, 2);
        text.Integer("IADS", null);

        var page = Decode(30, 24, Dictionary([A, B, C]), TextSegment(30, 24, 0x0010, 3, text));

        Jbig2Images.AssertSame(Jbig2Images.Compose(30, 24, (A, 1, 2), (B, 5, 2), (C, 3, 12)), page);
    }

    [Fact]
    public void Strips_corners_transposition_and_the_s_offset_place_instances()
    {
        // Two-row strips (CURT read), bottom-right corner, transposed: S runs down, T across; DS offset -1.
        var text = new ArithmeticWriter();
        text.Integer("IADT", 0);
        text.Integer("IADT", 2);
        text.Integer("IAFS", 4);
        text.Integer("IAIT", 1);
        text.SymbolId(0, 2);
        text.Integer("IADS", 3);
        text.Integer("IAIT", 0);
        text.SymbolId(1, 2);
        text.Integer("IADS", null);

        // flags: LOGSBSTRIPS 1, REFCORNER bottom-right (2), TRANSPOSED, SBDSOFFSET -1 (31).
        var flags = (1 << 2) | (2 << 4) | 0x40 | (31 << 10);
        var page = Decode(30, 30, Dictionary([A, B, C]), TextSegment(30, 30, flags, 2, text));

        // Strip T = 2 * 2 = 4. A: T = 5 is its right column, S = 4 its top row (S moves down by its height - 1 to its
        // bottom first): top-left (3, 4). B: S = 10 + 3 - 1 = 12 its top row, T = 4 its right column: top-left (0, 12).
        Jbig2Images.AssertSame(Jbig2Images.Compose(30, 30, (A, 3, 4), (B, 0, 12)), page);
    }

    [Theory]
    [InlineData(0, false, 10, 4)]
    [InlineData(1, false, 10, 12)]
    [InlineData(2, false, 10, 4)]
    [InlineData(3, false, 10, 12)]
    [InlineData(0, true, 12, 10)]
    [InlineData(1, true, 12, 10)]
    [InlineData(2, true, 7, 10)]
    [InlineData(3, true, 7, 10)]
    public void Every_reference_corner_places_a_single_instance(int corner, bool transposed, int left, int top)
    {
        // C (6 by 9) given at S 10, T 12. S first moves to the trailing edge along the strip, so along S the symbol
        // starts at 10 whatever the corner; across, a bottom corner (right corner, transposed) puts T on its last row
        // (column): top 12 - 9 + 1 = 4, left 12 - 6 + 1 = 7.
        var text = new ArithmeticWriter();
        text.Integer("IADT", 0);
        text.Integer("IADT", 12);
        text.Integer("IAFS", 10);
        text.SymbolId(2, 2);
        text.Integer("IADS", null);
        var flags = (corner << 4) | (transposed ? 0x40 : 0);

        var page = Decode(40, 40, Dictionary([A, B, C]), TextSegment(40, 40, flags, 1, text));

        Jbig2Images.AssertSame(Jbig2Images.Compose(40, 40, (C, left, top)), page);
    }

    [Fact]
    public void Text_regions_refine_instances_and_combine_with_their_operator_on_a_black_background()
    {
        var refined = Jbig2Images.Glyph(4, 8, 0);
        refined[2, 3] ^= 1;
        var text = new ArithmeticWriter();
        text.Integer("IADT", 0);
        text.Integer("IADT", 1);
        text.Integer("IAFS", 2);
        text.SymbolId(0, 2);
        text.Integer("IARI", 1);
        text.Integer("IARDW", 1);
        text.Integer("IARDH", 1);
        text.Integer("IARDX", 0);
        text.Integer("IARDY", 0);
        text.Refinement("GR", refined, A, 0, 0, 0, [(-1, -1), (-1, -1)], false);
        text.Integer("IADS", 2);
        text.SymbolId(1, 2);
        text.Integer("IARI", 0);
        text.Integer("IADS", null);

        // SBREFINE, top-left, XOR (2), default pixel 1, refinement template 0 with its adaptive pixels.
        var flags = 2 | (1 << 4) | (2 << 7) | (1 << 9);
        var page = Decode(20, 12, Dictionary([A, B, C]), TextSegment(20, 12, flags, 2, text, Adaptive((-1, -1), (-1, -1))));

        var expected = Jbig2Images.Compose(20, 12, (refined, 2, 1), (B, 7, 1));
        for (var i = 0; i < expected.Pixels.Length; i++)
        {
            expected.Pixels[i] ^= 1;
        }

        Jbig2Images.AssertSame(expected, page);
    }

    [Fact]
    public void A_dictionary_refines_and_aggregates_the_symbols_it_knows()
    {
        // Dictionary 1: A and B. Dictionary 2 (refinement and aggregation): D, a refinement of A one pixel wider, and
        // E, B then A side by side; it exports E and A only.
        var d = Jbig2Images.Glyph(4, 7, 0);
        d[3, 3] ^= 1;
        var e = Jbig2Images.Compose(8, 7, (B, 0, 0), (A, 5, 0));
        var writer = new ArithmeticWriter();
        writer.Integer("IADH", 7);
        writer.Integer("IADW", 4);
        writer.Integer("IAAI", 1);
        writer.SymbolId(0, 2);
        writer.Integer("IARDX", 0);
        writer.Integer("IARDY", 0);
        writer.Refinement("GR", d, A, 0, 0, 1, [], false);
        writer.Integer("IADW", 4);
        writer.Integer("IAAI", 2);
        writer.Integer("IADT", 0);
        writer.Integer("IADT", 0);
        writer.Integer("IAFS", 0);
        writer.SymbolId(1, 2);
        writer.Integer("IARI", 0);
        writer.Integer("IADS", 1);
        writer.SymbolId(0, 2);
        writer.Integer("IARI", 0);
        writer.Integer("IADS", null);
        writer.Integer("IADW", null);
        writer.Integer("IAEX", 0);
        writer.Integer("IAEX", 1);
        writer.Integer("IAEX", 2);
        writer.Integer("IAEX", 1);

        // SDHUFF 0, SDREFAGG, SDTEMPLATE 0, SDRTEMPLATE 1.
        byte[] aggregate = [.. Be16(2 | (1 << 12)), .. Adaptive(Standard), .. Be(2), .. Be(2), .. writer.Mq.Finish()];
        var text = new ArithmeticWriter();
        text.Integer("IADT", 0);
        text.Integer("IADT", 0);
        text.Integer("IAFS", 0);
        text.SymbolId(1, 1);
        text.Integer("IADS", 1);
        text.SymbolId(0, 1);
        text.Integer("IADS", null);

        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(20, 8));
        var first = segments.Add(SymbolDictionary, Dictionary([A, B]));
        var second = segments.Add(SymbolDictionary, aggregate, referred: [first]);
        segments.Add(Text, TextSegment(20, 8, 0x0010, 2, text), referred: [second]);
        var page = Jbig2Decoder.Decode(segments.ToArray());

        // Exported in order: A (input 0) then E (new 1).
        Jbig2Images.AssertSame(Jbig2Images.Compose(20, 8, (e, 0, 0), (A, 8, 0)), page);
    }

    [Fact]
    public void Dictionaries_reuse_the_contexts_a_dictionary_retained_each_from_its_retained_state()
    {
        // One coder runs on: the first dictionary's symbol, then each of the next two continues from the contexts as
        // the first left them (the second's own changes are not seen by the third).
        var first = new ArithmeticWriter();
        first.Integer("IADH", 7);
        first.Integer("IADW", A.Width);
        first.Generic("GB", A, 0, Standard, false);
        first.Integer("IADW", null);
        first.Integer("IAEX", 0);
        first.Integer("IAEX", 1);
        var retained = (byte[])first.Contexts("GB", 1 << 16).Clone();
        byte[] Next(Jbig2Bitmap symbol)
        {
            var writer = new ArithmeticWriter();
            Array.Copy(retained, writer.Contexts("GB", 1 << 16), retained.Length);
            writer.Integer("IADH", symbol.Height);
            writer.Integer("IADW", symbol.Width);
            writer.Generic("GB", symbol, 0, Standard, false);
            writer.Integer("IADW", null);
            writer.Integer("IAEX", 1);
            writer.Integer("IAEX", 1);
            return [.. Be16(0x100), .. Adaptive(Standard), .. Be(1), .. Be(1), .. writer.Mq.Finish()];
        }

        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(20, 10));
        var d1 = segments.Add(SymbolDictionary, [.. Be16(0x200), .. Adaptive(Standard), .. Be(1), .. Be(1), .. first.Mq.Finish()]);
        segments.Add(SymbolDictionary, Next(B), referred: [d1]);
        var d3 = segments.Add(SymbolDictionary, Next(C), referred: [d1]);
        var text = new ArithmeticWriter();
        text.Integer("IADT", 0);
        text.Integer("IADT", 0);
        text.Integer("IAFS", 0);
        text.SymbolId(0, 0);
        text.Integer("IADS", null);
        segments.Add(Text, TextSegment(20, 10, 0x0010, 1, text), referred: [d3]);

        var page = Jbig2Decoder.Decode(segments.ToArray());

        Jbig2Images.AssertSame(Jbig2Images.Compose(20, 10, (C, 0, 0)), page);
    }

    [Fact]
    public void Global_segments_come_before_the_page_and_long_reference_lists_are_read()
    {
        var globals = new SegmentWriter();
        var dictionary = globals.Add(SymbolDictionary, Dictionary([A, B, C]), page: 0);
        var text = new ArithmeticWriter();
        text.Integer("IADT", 0);
        text.Integer("IADT", 0);
        text.Integer("IAFS", 0);
        text.SymbolId(2, 2);
        text.Integer("IADS", null);
        var page = new SegmentWriter(70000);
        page.Add(48, PageInfo(8, 9), page: 300);
        page.Add(Text, TextSegment(8, 9, 0x0010, 1, text), page: 300, referred: [dictionary, 1, 2, 3, 4, 5]);

        var result = Jbig2Decoder.Decode(page.ToArray(), globals.ToArray());

        Jbig2Images.AssertSame(Jbig2Images.Compose(8, 9, (C, 0, 0)), result);
    }

    [Fact]
    public void Broken_dictionaries_and_text_regions_are_refused()
    {
        var overrun = new ArithmeticWriter();
        overrun.Integer("IADH", 7);
        overrun.Integer("IADW", 5);
        overrun.Generic("GB", A, 0, Standard, false);
        overrun.Integer("IADW", 1);
        var tooMany = Wrap([.. Be16(0), .. Adaptive(Standard), .. Be(1), .. Be(1), .. overrun.Mq.Finish()]);
        var badExport = new ArithmeticWriter();
        badExport.Integer("IADH", 7);
        badExport.Integer("IADW", 5);
        badExport.Generic("GB", A, 0, Standard, false);
        badExport.Integer("IADW", null);
        badExport.Integer("IAEX", 5);
        var exports = Wrap([.. Be16(0), .. Adaptive(Standard), .. Be(1), .. Be(1), .. badExport.Mq.Finish()]);
        var negative = Wrap([.. Be16(0), .. Adaptive(Standard), .. Be(1), .. Be(-1)]);
        var unknown = new ArithmeticWriter();
        unknown.Integer("IADT", 0);
        unknown.Integer("IADT", 0);
        unknown.Integer("IAFS", 0);
        unknown.SymbolId(3, 2);
        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(8, 8));
        var d = segments.Add(SymbolDictionary, Dictionary([A, B, C]));
        segments.Add(Text, TextSegment(8, 8, 0x0010, 1, unknown), referred: [d]);

        Assert.Throws<InvalidDataException>(() => Jbig2Decoder.Decode(tooMany));
        Assert.Throws<InvalidDataException>(() => Jbig2Decoder.Decode(exports));
        Assert.Throws<InvalidDataException>(() => Jbig2Decoder.Decode(negative));
        Assert.Throws<InvalidDataException>(() => Jbig2Decoder.Decode(segments.ToArray()));
        Assert.Throws<InvalidDataException>(() => Decode(8, 8, Dictionary([A]), TextSegment(8, 8, 0x0010, 1_000_000, new ArithmeticWriter())));
    }

    /// <summary>A dictionary segment holding <paramref name="symbols"/>, one height class per height, all exported.</summary>
    internal static byte[] Dictionary(Jbig2Bitmap[] symbols)
    {
        var writer = new ArithmeticWriter();
        var height = 0;
        foreach (var group in symbols.GroupBy(s => s.Height).OrderBy(g => g.Key))
        {
            writer.Integer("IADH", group.Key - height);
            height = group.Key;
            var width = 0;
            foreach (var symbol in group.OrderBy(s => s.Width))
            {
                writer.Integer("IADW", symbol.Width - width);
                width = symbol.Width;
                writer.Generic("GB", symbol, 0, Standard, false);
            }

            writer.Integer("IADW", null);
        }

        writer.Integer("IAEX", 0);
        writer.Integer("IAEX", symbols.Length);
        Assert.Equal(symbols, symbols.OrderBy(s => s.Height).ThenBy(s => s.Width));
        return [.. Be16(0), .. Adaptive(Standard), .. Be(symbols.Length), .. Be(symbols.Length), .. writer.Mq.Finish()];
    }

    internal static byte[] TextSegment(int width, int height, int flags, int instances, ArithmeticWriter writer, byte[]? adaptive = null) =>
        [.. Region(width, height), .. Be16(flags), .. adaptive ?? [], .. Be(instances), .. writer.Mq.Finish()];

    private static Jbig2Bitmap Decode(int width, int height, byte[] dictionary, byte[] text)
    {
        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(width, height));
        var d = segments.Add(SymbolDictionary, dictionary);
        segments.Add(Text, text, referred: [d]);
        segments.Add(49, []);
        return Jbig2Decoder.Decode(segments.ToArray());
    }

    private static byte[] Wrap(byte[] dictionary)
    {
        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(8, 8));
        segments.Add(SymbolDictionary, dictionary);
        return segments.ToArray();
    }
}
