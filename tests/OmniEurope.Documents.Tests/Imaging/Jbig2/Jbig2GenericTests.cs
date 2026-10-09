// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging.Jbig2;
using static OmniEurope.Documents.Tests.Imaging.Jbig2.SegmentWriter;

namespace OmniEurope.Documents.Tests.Imaging.Jbig2;

/// <summary>
/// Generic and refinement regions and the page buffer, on bitstreams encoded by the test writer (<see cref="MqWriter"/>,
/// <see cref="ArithmeticWriter"/>). These round trips cover the decoding paths; conformance itself was checked
/// pixel for pixel against an independent decoder on public JBIG2 test streams and scanned PDFs.
/// </summary>
public sealed class Jbig2GenericTests
{
    private const int Generic = 38;
    private const int IntermediateGeneric = 36;
    private const int Refinement = 42;
    private const int IntermediateRefinement = 40;

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public void Generic_regions_decode_with_every_template_and_typical_prediction(int template, bool typicalPrediction)
    {
        var image = Jbig2Images.Pattern(37, 21, template);
        (int X, int Y)[] adaptive = template == 0 ? [(3, -1), (-3, -1), (2, -2), (-2, -2)] : [(template == 1 ? 3 : 2, -1)];

        var page = Jbig2Images.Decode(PageWith(GenericSegment(image, template, adaptive, typicalPrediction)), 37, 21);

        Jbig2Images.AssertSame(image, page);
    }

    [Fact]
    public void Adaptive_pixels_may_lie_far_from_the_pixel()
    {
        var image = Jbig2Images.Pattern(40, 12, 5);

        var page = Jbig2Images.Decode(PageWith(GenericSegment(image, 0, [(-7, -1), (5, -3), (-12, 0), (9, -2)], true)), 40, 12);

        Jbig2Images.AssertSame(image, page);
    }

    [Fact]
    public void An_adaptive_pixel_on_a_pixel_not_decoded_yet_is_refused()
    {
        var image = Jbig2Images.Pattern(8, 4, 1);
        var data = GenericSegment(image, 1, [(3, -1)], false);
        data[Jbig2RegionInfo.Size + 1] = 2;
        data[Jbig2RegionInfo.Size + 2] = 0;

        Assert.Throws<InvalidDataException>(() => Jbig2Decoder.Decode(PageWith(data)));
    }

    [Fact]
    public void Mmr_generic_regions_decode_group_4_rows()
    {
        // Row 0: horizontal mode, white run 2, black run 4, then vertical 0; row 1: three vertical 0; then the
        // end-of-block code.
        var bits = new BitWriter();
        bits.Write("001 0111 011 1  111  000000000001 000000000001");
        byte[] data = [.. Region(8, 2), 1, .. bits.ToArray()];

        var page = Jbig2Images.Decode(PageWith(data), 8, 2);

        Assert.Equal(Jbig2Images.Parse("..####..", "..####.."), page.Pixels);
    }

    [Fact]
    public void A_generic_region_of_unknown_height_takes_the_row_count_after_its_end_marker()
    {
        var image = Jbig2Images.Pattern(16, 6, 2);
        var writer = new ArithmeticWriter();
        writer.Generic("GB", image, 2, [(2, -1)], false);
        byte[] data = [.. Region(16, -1), 4, 2, 0xFF, .. writer.Mq.Finish(), .. Be(6)];
        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(16, 6));
        segments.Add(Generic, data, unknownLength: true);
        segments.Add(49, []);

        var page = Jbig2Decoder.Decode(segments.ToArray());

        Jbig2Images.AssertSame(image, page);
    }

    [Fact]
    public void An_mmr_region_of_unknown_length_ends_at_its_zero_marker()
    {
        var bits = new BitWriter();
        bits.Write("001 0111 011 1");
        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(8, 1));
        segments.Add(Generic, [.. Region(8, -1), 1, .. bits.ToArray(), 0, 0, .. Be(1)], unknownLength: true);

        var page = Jbig2Decoder.Decode(segments.ToArray());

        Assert.Equal(Jbig2Images.Parse("..####.."), page.Pixels);
    }

    [Fact]
    public void A_segment_of_unknown_length_must_be_a_generic_region()
    {
        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(8, 1));
        segments.Add(Refinement, [.. Region(8, 1), 1, 0, 0], unknownLength: true);

        Assert.Throws<InvalidDataException>(() => Jbig2Decoder.Decode(segments.ToArray()));
    }

    [Fact]
    public void Extended_templates_are_not_supported()
    {
        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(8, 1));
        segments.Add(Generic, [.. Region(8, 1), 0x10, .. new byte[40]]);

        Assert.Throws<NotSupportedException>(() => Jbig2Decoder.Decode(segments.ToArray()));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    public void A_refinement_region_refines_the_intermediate_region_it_refers_to(int template, bool typicalPrediction)
    {
        var reference = Jbig2Images.Pattern(30, 14, 7);
        var refined = Jbig2Images.Pattern(30, 14, 7);
        refined[3, 4] ^= 1;
        refined[20, 9] ^= 1;
        refined[29, 13] ^= 1;
        (int X, int Y)[] adaptive = [(-1, -1), (-1, -1)];
        var writer = new ArithmeticWriter();
        writer.Refinement("GR", refined, reference, 0, 0, template, adaptive, typicalPrediction);
        var flags = template | (typicalPrediction ? 2 : 0);
        byte[] refinement = [.. Region(30, 14, 2, 1), (byte)flags, .. template == 0 ? Adaptive(adaptive) : [], .. writer.Mq.Finish()];
        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(34, 16));
        var intermediate = segments.Add(IntermediateGeneric, GenericSegment(reference, 0, [(3, -1), (-3, -1), (2, -2), (-2, -2)], false));
        segments.Add(Refinement, refinement, referred: [intermediate]);

        var page = Jbig2Decoder.Decode(segments.ToArray());

        Jbig2Images.AssertSame(refined, page.Extract(2, 1, 30, 14));
        Assert.Equal(0, page[0, 0]);
    }

    [Fact]
    public void A_refinement_region_without_reference_refines_the_page_under_it()
    {
        var first = Jbig2Images.Pattern(20, 10, 3);
        var refined = Jbig2Images.Pattern(20, 10, 3);
        refined[0, 0] ^= 1;
        refined[10, 5] ^= 1;
        var writer = new ArithmeticWriter();
        writer.Refinement("GR", refined, first, 0, 0, 1, [], false);
        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(20, 10, flags: 0x40));
        segments.Add(Generic, GenericSegment(first, 0, [(3, -1), (-3, -1), (2, -2), (-2, -2)], false));
        segments.Add(Refinement, [.. Region(20, 10, 0, 0, 4), 1, .. writer.Mq.Finish()]);

        var page = Jbig2Decoder.Decode(segments.ToArray());

        Jbig2Images.AssertSame(refined, page);
    }

    [Fact]
    public void An_intermediate_refinement_is_kept_for_the_next_refinement()
    {
        var base0 = Jbig2Images.Pattern(12, 8, 4);
        var step1 = Jbig2Images.Pattern(12, 8, 4);
        step1[5, 5] ^= 1;
        var step2 = Jbig2Images.Pattern(12, 8, 4);
        step2[5, 5] ^= 1;
        step2[1, 1] ^= 1;
        var first = new ArithmeticWriter();
        first.Refinement("GR", step1, base0, 0, 0, 1, [], false);
        var second = new ArithmeticWriter();
        second.Refinement("GR", step2, step1, 0, 0, 1, [], false);
        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(12, 8));
        var g = segments.Add(IntermediateGeneric, GenericSegment(base0, 3, [(2, -1)], false));
        var r = segments.Add(IntermediateRefinement, [.. Region(12, 8), 1, .. first.Mq.Finish()], referred: [g]);
        segments.Add(Refinement, [.. Region(12, 8), 1, .. second.Mq.Finish()], referred: [r]);

        var page = Jbig2Decoder.Decode(segments.ToArray());

        Jbig2Images.AssertSame(step2, page);
    }

    [Theory]
    [InlineData(0, "###.")]
    [InlineData(1, "#...")]
    [InlineData(2, ".##.")]
    [InlineData(3, "#..#")]
    [InlineData(4, "#.#.")]
    public void Regions_combine_onto_the_page_with_their_operator(int combination, string expected)
    {
        // The page holds "##.." (default pixel 0, then a first region); the second region "#.#." combines onto it.
        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(4, 1, flags: 0x40));
        segments.Add(Generic, Raw("##..", 0));
        segments.Add(Generic, Raw("#.#.", combination));

        var page = Jbig2Decoder.Decode(segments.ToArray());

        Assert.Equal(Jbig2Images.Parse(expected), page.Pixels);
    }

    [Fact]
    public void The_default_pixel_fills_the_page_and_regions_are_clipped_to_it()
    {
        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(4, 2, flags: 4));
        segments.Add(Generic, [.. Region(4, 1, 2, 1, 4), 1, .. MmrRow("..#.")]);

        var page = Jbig2Decoder.Decode(segments.ToArray());

        Assert.Equal(Jbig2Images.Parse("####", "##.."), page.Pixels);
    }

    [Fact]
    public void A_page_of_unknown_height_grows_with_its_regions_and_ends_with_its_last_stripe()
    {
        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(4, -1, striping: 0x8002));
        segments.Add(Generic, [.. Region(4, 1, 0, 0), 1, .. MmrRow("#...")]);
        segments.Add(50, Be(1));
        segments.Add(Generic, [.. Region(4, 1, 0, 2), 1, .. MmrRow(".#..")]);
        segments.Add(50, Be(4));
        segments.Add(49, []);

        var page = Jbig2Decoder.Decode(segments.ToArray());

        Assert.Equal(5, page.Height);
        Assert.Equal(Jbig2Images.Parse("#...", "....", ".#..", "....", "...."), page.Pixels);
    }

    [Fact]
    public void Only_the_first_page_is_decoded_and_unknown_segments_are_skipped()
    {
        var segments = new SegmentWriter(300);
        segments.Add(48, PageInfo(4, 1));
        segments.Add(62, [1, 2, 3]);
        segments.Add(52, []);
        segments.Add(Generic, Raw("#..#", 0));
        segments.Add(49, []);
        segments.Add(48, PageInfo(4, 1), page: 2);
        segments.Add(Generic, Raw("####", 0), page: 2);

        var page = Jbig2Decoder.Decode(segments.ToArray());

        Assert.Equal(Jbig2Images.Parse("#..#"), page.Pixels);
    }

    [Fact]
    public void A_region_before_the_page_information_or_data_without_a_page_is_refused()
    {
        var region = new SegmentWriter();
        region.Add(Generic, Raw("#..#", 0));
        var tables = new SegmentWriter();
        tables.Add(51, []);

        Assert.Throws<InvalidDataException>(() => Jbig2Decoder.Decode(region.ToArray()));
        Assert.Throws<InvalidDataException>(() => Jbig2Decoder.Decode(tables.ToArray()));
        Assert.Throws<InvalidDataException>(() => Jbig2Decoder.Decode([0, 0, 0, 0, 48, 0, 1, 0, 0, 0, 19, 0, 0]));
    }

    [Fact]
    public void Bitmaps_of_impossible_size_are_refused()
    {
        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(100000, 100000));

        Assert.Throws<InvalidDataException>(() => Jbig2Decoder.Decode(segments.ToArray()));
    }

    internal static byte[] GenericSegment(Jbig2Bitmap image, int template, (int X, int Y)[] adaptive, bool typicalPrediction, int x = 0, int y = 0)
    {
        var writer = new ArithmeticWriter();
        writer.Generic("GB", image, template, adaptive, typicalPrediction);
        var flags = (template << 1) | (typicalPrediction ? 8 : 0);
        return [.. Region(image.Width, image.Height, x, y), (byte)flags, .. Adaptive(adaptive), .. writer.Mq.Finish()];
    }

    // One MMR row of a region: horizontal mode codes for its runs.
    internal static byte[] MmrRow(string row) => MmrRows(false, row);

    // MMR rows, each coded in horizontal mode only (runs of 0 to 7 pixels), optionally ended by the end-of-block code.
    internal static byte[] MmrRows(bool endOfBlock, params string[] rows)
    {
        var bits = new BitWriter();
        foreach (var row in rows)
        {
            MmrRow(bits, row);
        }

        if (endOfBlock)
        {
            bits.Write("000000000001 000000000001");
        }

        return bits.ToArray();
    }

    private static void MmrRow(BitWriter bits, string row)
    {
        string[] white = ["00110101", "000111", "0111", "1000", "1011", "1100", "1110", "1111"];
        string[] black = ["0000110111", "010", "11", "10", "011", "0011", "0010", "00011"];
        var runs = new List<int>();
        var colour = '.';
        var count = 0;
        foreach (var c in row)
        {
            if (c == colour)
            {
                count++;
                continue;
            }

            runs.Add(count);
            colour = c;
            count = 1;
        }

        runs.Add(count);
        if (runs.Count % 2 == 1)
        {
            runs.Add(0);
        }

        for (var i = 0; i < runs.Count; i += 2)
        {
            bits.Write("001" + white[runs[i]] + black[runs[i + 1]]);
        }
    }

    private static byte[] Raw(string row, int combination) => [.. Region(row.Length, 1, 0, 0, combination), 1, .. MmrRow(row)];

    private static byte[] PageWith(byte[] generic)
    {
        var info = Jbig2RegionInfo.Parse(generic, 0);
        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(info.Width + info.X, info.Height + info.Y));
        segments.Add(Generic, generic);
        segments.Add(49, []);
        return segments.ToArray();
    }
}
