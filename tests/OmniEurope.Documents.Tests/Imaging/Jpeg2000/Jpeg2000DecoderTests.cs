// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;
using System.Text;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Imaging.Jpeg2000;

namespace OmniEurope.Documents.Tests.Imaging.Jpeg2000;

/// <summary>
/// What the conformance samples do not show by themselves: the features this decoder refuses, damaged data, data cut
/// short, and the JP2 boxes (palettes, component mappings, channel definitions, colour specifications, box lengths)
/// built around the samples' codestreams.
/// </summary>
public sealed class Jpeg2000DecoderTests
{
    private static readonly byte[] Gray = Codestream("lossless-gray-u8-prog1-layers1-res6.jp2");

    [Fact]
    public void The_high_throughput_block_coder_part_2_markers_and_arbitrary_wavelets_are_refused()
    {
        var highThroughput = (byte[])Gray.Clone();
        highThroughput[Segment(highThroughput, 0xFF52) + 12] |= 0x40;
        var wavelet = (byte[])Gray.Clone();
        wavelet[Segment(wavelet, 0xFF52) + 13] = 2;
        var siz = Segment(Gray, 0xFF51);
        var sizEnd = siz + 2 + BinaryPrimitives.ReadUInt16BigEndian(Gray.AsSpan(siz + 2));
        byte[] multipleComponent = [.. Gray[..sizEnd], 0xFF, 0x74, 0x00, 0x04, 0x00, 0x00, .. Gray[sizEnd..]];

        Assert.Throws<NotSupportedException>(() => Jpeg2000Decoder.Decode(highThroughput));
        Assert.Throws<NotSupportedException>(() => Jpeg2000Decoder.Decode(wavelet));
        Assert.Throws<NotSupportedException>(() => Jpeg2000Decoder.Decode(multipleComponent));
        Assert.Throws<NotSupportedException>(() => Jpeg2000Decoder.Decode(Patch(Gray, siz + 40, 0x7F)));
    }

    public static TheoryData<string> Damaged => new()
    {
        "garbage", "empty", "no codestream box", "box past its container", "SIZ length", "no component", "tile index", "no COD", "no QCD",
        "code-block size", "precinct exponent", "progression order", "quantization style", "too few step sizes", "cut in the main header",
        "channel definition", "component mapping", "palette column", "tile-part length", "empty image", "tile origin", "first tile outside",
        "too many tiles", "no subsampling", "far more samples than data", "no tile", "segment past the data", "no SOD", "code-block height",
        "code-block area", "decomposition levels", "palette past its box", "no colour channel",
    };

    [Theory]
    [MemberData(nameof(Damaged))]
    public void Damaged_data_is_reported_as_such(string damage)
    {
        var data = damage switch
        {
            "garbage" => [1, 2, 3, 4, 5, 6, 7, 8],
            "empty" => [],
            "no codestream box" => [.. Box("jP  ", [0x0D, 0x0A, 0x87, 0x0A]), .. Box("jp2h", Box("colr", [1, 0, 0, 0, 0, 0, 17]))],
            "box past its container" => [.. Box("jP  ", [0x0D, 0x0A, 0x87, 0x0A]), 0x7F, 0, 0, 0, .. "jp2h"u8.ToArray(), .. Gray],
            "SIZ length" => Patch(Gray, Segment(Gray, 0xFF51) + 3, 0x30),
            "no component" => Patch(Patch(Gray, Segment(Gray, 0xFF51) + 39, 0), Segment(Gray, 0xFF51) + 3, 38),
            "tile index" => Patch(Gray, Segment(Gray, 0xFF90) + 5, 9),
            "no COD" => Without(Gray, 0xFF52),
            "no QCD" => Without(Gray, 0xFF5C),
            "code-block size" => Patch(Gray, Segment(Gray, 0xFF52) + 10, 9),
            "precinct exponent" => PrecinctExponent(),
            "progression order" => Patch(Gray, Segment(Gray, 0xFF52) + 5, 7),
            "quantization style" => Patch(Gray, Segment(Gray, 0xFF5C) + 4, 0x43),
            "too few step sizes" => TooFewStepSizes(),
            "cut in the main header" => Gray[..30],
            "channel definition" => Jp2(Gray, Box("cdef", [0, 1, 0, 9, 0, 0, 0, 1])),
            "component mapping" => Jp2(Gray, Box("cmap", [0, 3, 0, 0])),
            "palette column" => Jp2(Gray, Box("cmap", [0, 0, 1, 0])),
            "empty image" => Siz(Gray, 12, 500),
            "tile origin" => Siz(Gray, 28, 1),
            "first tile outside" => Siz(Siz(Gray, 12, 50), 20, 40),
            "too many tiles" => Siz(Siz(Siz(Siz(Gray, 4, 1000), 8, 1000), 20, 1), 24, 1),
            "no subsampling" => Patch(Gray, Segment(Gray, 0xFF51) + 41, 0),
            "far more samples than data" => Siz(Siz(Gray, 4, 8192), 8, 8192),
            "no tile" => Gray[..Segment(Gray, 0xFF90)],
            "segment past the data" => Gray[..(Segment(Gray, 0xFF52) + 6)],
            "no SOD" => Gray[..(Segment(Gray, 0xFF90) + 12)],
            "code-block height" => Patch(Gray, Segment(Gray, 0xFF52) + 11, 9),
            "code-block area" => Patch(Patch(Gray, Segment(Gray, 0xFF52) + 10, 5), Segment(Gray, 0xFF52) + 11, 6),
            "decomposition levels" => Patch(Gray, Segment(Gray, 0xFF52) + 9, 33),
            "palette past its box" => Jp2(Gray, Box("pclr", [0, 9, 1, 7, 1, 2])),
            "no colour channel" => Jp2(Gray, Box("cdef", [0, 1, 0, 0, 0, 1, 0, 0])),
            _ => Patch(Patch(Patch(Patch(Gray, Segment(Gray, 0xFF90) + 6, 0), Segment(Gray, 0xFF90) + 7, 0), Segment(Gray, 0xFF90) + 8, 0), Segment(Gray, 0xFF90) + 9, 5),
        };

        Assert.Throws<InvalidDataException>(() => Jpeg2000Decoder.Decode(data));
    }

    [Fact]
    public void A_resolution_level_with_absurdly_many_precincts_is_refused()
    {
        var style = new J2kComponentStyle(1, 6, 6, J2kBlockStyle.None, true, [1, 1], [1, 1]);
        var quantization = new J2kQuantization(J2kQuantization.None, 2, [8, 9, 9, 10], [0, 0, 0, 0]);

        Assert.Throws<InvalidDataException>(() => new J2kTileComponent((0, 0, 8192, 8192), new J2kComponentSize(8, false, 1, 1), style, quantization, 0));
    }

    [Fact]
    public void Data_cut_short_decodes_what_it_holds()
    {
        var sample = Jpeg2000Conformance.Sample("lossless-rgba-u8-prog0-tile4x2-cblk4x16-tp3-layers3-res2.jp2");
        var data = Jpeg2000Conformance.Data(sample);
        var expected = Jpeg2000Conformance.Expected(sample);

        var image = Jpeg2000Decoder.Decode(data[..(data.Length * 3 / 5)]);

        Assert.Equal((expected[0].Width, expected[0].Height), (image.Width, image.Height));
        Assert.NotNull(Jpeg2000Conformance.Mismatch(Jpeg2000ConformanceTests.Planes(image), expected, 0));
    }

    [Fact]
    public void A_last_tile_part_of_length_zero_runs_to_the_end_of_the_codestream()
    {
        var sample = Jpeg2000Conformance.Sample("lossless-rgba-u8-PLT.jp2");
        var codestream = Codestream("lossless-rgba-u8-PLT.jp2");
        var open = (byte[])codestream.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(open.AsSpan(Segment(open, 0xFF90) + 6), 0);

        var image = Jpeg2000Decoder.Decode(open);

        Assert.Null(Jpeg2000Conformance.Mismatch(Jpeg2000ConformanceTests.Planes(image), Jpeg2000Conformance.Expected(sample), 0));
    }

    [Fact]
    public void A_palette_of_signed_and_short_columns_maps_the_component_to_colour_and_premultiplied_opacity()
    {
        var gray = Jpeg2000Conformance.Expected(Jpeg2000Conformance.Sample("lossless-gray-u8-prog1-layers1-res6.jp2"))[0];
        var entries = new List<byte>();
        for (var i = 0; i < 256; i++)
        {
            var signed = (ushort)(((i * 16) - 2048) & 0xFFF);
            entries.AddRange([(byte)(signed >> 8), (byte)signed, (byte)(i & 15)]);
        }

        var data = Jp2(Gray,
            Box("pclr", [1, 0, 2, 0x8B, 0x03, .. entries]),
            Box("cmap", [0, 0, 1, 0, 0, 0, 1, 1]),
            Box("cdef", [0, 2, 0, 0, 0, 0, 0, 1, 0, 1, 0, 2, 0, 0]));

        var image = Jpeg2000Decoder.Decode(data);

        Assert.Equal((12, true), (image.Colors[0].Precision, image.Colors[0].Signed));
        Assert.Equal(gray.Samples.Select(v => (v * 16) - 2048), image.Colors[0].Samples);
        Assert.Equal(gray.Samples.Select(v => v & 15), image.Alpha!.Samples);
        Assert.True(image.Premultiplied);
        Assert.Equal(ImageColorType.GrayAlpha, image.ToRaster().ColorType);
    }

    [Fact]
    public void A_component_mapping_without_palette_reorders_the_components()
    {
        var expected = Jpeg2000Conformance.Expected(Jpeg2000Conformance.Sample("lossless-gray-alpha-u8-prog1-layers1-res6.jp2"));
        var codestream = Codestream("lossless-gray-alpha-u8-prog1-layers1-res6.jp2");

        var image = Jpeg2000Decoder.Decode(Jp2(codestream, Box("cmap", [0, 1, 0, 0, 0, 0, 0, 0])));

        Assert.Equal(expected[1].Samples, image.Colors[0].Samples);
        Assert.Equal(expected[0].Samples, image.Colors[1].Samples);
        Assert.Null(image.Alpha);
    }

    [Fact]
    public void Without_colour_specification_the_channel_count_chooses_grey_rgb_or_cmyk()
    {
        var two = Jpeg2000Decoder.Decode(Codestream("lossless-gray-alpha-u8-prog1-layers1-res6.jp2"));
        var three = Jpeg2000Decoder.Decode(Codestream("lossless-rgb-u8-prog1-layers1-res6-mct.jp2"));
        var four = Jpeg2000Decoder.Decode(Codestream("lossless-rgba-u8-PLT.jp2"));

        Assert.Equal(ImageColorType.Gray, two.ToRaster().ColorType);
        Assert.Equal(ImageColorType.Rgb, three.ToRaster().ColorType);
        Assert.Equal(ImageColorType.Cmyk, four.ToRaster().ColorType);
    }

    [Theory]
    [InlineData("GRAY", "lossless-gray-u8-prog1-layers1-res6.jp2", ImageColorType.Gray)]
    [InlineData("RGB ", "lossless-rgb-u8-prog1-layers1-res6-mct.jp2", ImageColorType.Rgb)]
    [InlineData("CMYK", "lossless-rgba-u8-PLT.jp2", ImageColorType.Cmyk)]
    [InlineData("XYZ ", "lossless-rgb-u8-prog1-layers1-res6-mct.jp2", ImageColorType.Rgb)]
    public void An_icc_profile_stands_for_the_device_space_of_its_data_colour_space(string space, string sample, ImageColorType type)
    {
        byte[] profile = [.. new byte[16], .. Encoding.ASCII.GetBytes(space), .. new byte[108]];

        var image = Jpeg2000Decoder.Decode(Jp2(Codestream(sample), Box("colr", [2, 0, 0, .. profile])));

        Assert.Equal(type, image.ToRaster().ColorType);
    }

    [Fact]
    public void Boxes_with_an_extended_length_or_running_to_the_end_are_read()
    {
        var expected = Jpeg2000Conformance.Expected(Jpeg2000Conformance.Sample("lossless-gray-u8-prog1-layers1-res6.jp2"));
        var signature = Box("jP  ", [0x0D, 0x0A, 0x87, 0x0A]);
        var extended = new byte[16];
        BinaryPrimitives.WriteUInt32BigEndian(extended, 1);
        "jp2c"u8.CopyTo(extended.AsSpan(4));
        BinaryPrimitives.WriteUInt64BigEndian(extended.AsSpan(8), (ulong)(16 + Gray.Length));
        byte[] toEnd = [0, 0, 0, 0, .. "jp2c"u8.ToArray()];

        var long_ = Jpeg2000Decoder.Decode([.. signature, .. extended, .. Gray]);
        var open = Jpeg2000Decoder.Decode([.. signature, .. Box("jp2h", Box("colr", [1, 0, 0, 0, 0, 0, 17])), .. toEnd, .. Gray]);

        Assert.Equal(expected[0].Samples, long_.Colors[0].Samples);
        Assert.Equal(expected[0].Samples, open.Colors[0].Samples);
        Assert.Equal(Jpeg2000ColorSpace.Gray, open.ColorSpace);
    }

    [Fact]
    public void Lab_images_are_converted_to_rgb_with_their_ranges_and_illuminant()
    {
        var plane = new Jpeg2000Plane(0, 0, 1, 1, 1, 1, 8, false);
        var given = Jpeg2000Lab.From([100, 0, 255, 128, 255, 128], [plane, plane, plane]);
        var d65 = given with { D65 = true };
        byte[] white = [255, 128, 128, 0, 128, 128];
        byte[] whiteD65 = [.. white];

        Jpeg2000Colors.LabToRgb(white, given);
        Jpeg2000Colors.LabToRgb(whiteD65, d65);
        var defaults = Jpeg2000Lab.Default([plane, plane, plane]);

        Assert.All(white[..3], v => Assert.InRange(v, 254, 255));
        Assert.Equal(new byte[] { 0, 0, 0 }, white[3..]);
        Assert.All(whiteD65[..3], v => Assert.InRange(v, 254, 255));
        Assert.Equal((100.0, 170.0, 200.0), (defaults.RangeL, defaults.RangeA, defaults.RangeB));
        Assert.Equal((128 / 255.0, 0.75 * 128 / 255), (defaults.OffsetA, defaults.OffsetB));
        Assert.Equal(ImageColorType.Rgb, Jpeg2000Decoder.Decode(Jpeg2000Conformance.Data(Jpeg2000Conformance.Sample("lossless-lab-u8-prog1-layers1-res6.jp2"))).ToRaster().ColorType);
        Assert.Equal(ImageColorType.Rgba, Jpeg2000Decoder.Decode(Jpeg2000Conformance.Data(Jpeg2000Conformance.Sample("lossless-lab-alpha-u8-prog1-layers1-res6.jp2"))).ToRaster().ColorType);
    }

    [Fact]
    public void Ycc_and_cmyk_with_opacity_become_rgb()
    {
        byte[] pixels = [128, 128, 128, 100, 128, 228];

        Jpeg2000Colors.YccToRgb(pixels);
        var cmyka = Jpeg2000Decoder.Decode(Jpeg2000Conformance.Data(Jpeg2000Conformance.Sample("lossless-cmyka-u8-prog1-layers1-res6.jp2"))).ToRaster();

        Assert.Equal(new byte[] { 128, 128, 128, 240, 29, 100 }, pixels);
        Assert.Equal(ImageColorType.Rgba, cmyka.ColorType);
    }

    // The codestream of a sample: the content of its contiguous codestream box.
    internal static byte[] Codestream(string sample)
    {
        var data = File.ReadAllBytes(Path.Combine(Jpeg2000Conformance.Folder, "Samples", sample));
        var at = 0;
        while (Encoding.ASCII.GetString(data, at + 4, 4) != "jp2c")
        {
            at += (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at));
        }

        return data[(at + 8)..(at + (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at)))];
    }

    internal static byte[] Box(string type, byte[] content)
    {
        var box = new byte[8 + content.Length];
        BinaryPrimitives.WriteUInt32BigEndian(box, (uint)box.Length);
        Encoding.ASCII.GetBytes(type).CopyTo(box, 4);
        content.CopyTo(box, 8);
        return box;
    }

    // A JP2 file holding the codestream after a header box made of the given boxes.
    internal static byte[] Jp2(byte[] codestream, params byte[][] boxes) =>
        [.. Box("jP  ", [0x0D, 0x0A, 0x87, 0x0A]), .. Box("jp2h", [.. boxes.SelectMany(b => b)]), .. Box("jp2c", codestream)];

    // Where the first marker segment with this marker starts in the main header (or the first SOT).
    internal static int Segment(byte[] codestream, int marker)
    {
        var at = 2;
        while (BinaryPrimitives.ReadUInt16BigEndian(codestream.AsSpan(at)) != marker)
        {
            at += 2 + BinaryPrimitives.ReadUInt16BigEndian(codestream.AsSpan(at + 2));
        }

        return at;
    }

    // The SIZ field at this offset from the marker (Xsiz 4, Ysiz 8, XOsiz 12, YOsiz 16, XTsiz 20, YTsiz 24, XTOsiz 28,
    // YTOsiz 32) set to a value.
    private static byte[] Siz(byte[] codestream, int offset, int value)
    {
        var copy = (byte[])codestream.Clone();
        BinaryPrimitives.WriteInt32BigEndian(copy.AsSpan(Segment(copy, 0xFF51) + 2 + offset), value);
        return copy;
    }


    private static byte[] Patch(byte[] data, int at, byte value)
    {
        var copy = (byte[])data.Clone();
        copy[at] = value;
        return copy;
    }

    private static byte[] Without(byte[] codestream, int marker)
    {
        var at = Segment(codestream, marker);
        return [.. codestream[..at], .. codestream[(at + 2 + BinaryPrimitives.ReadUInt16BigEndian(codestream.AsSpan(at + 2)))..]];
    }

    // A COD declaring precincts with a zero exponent at resolution 1.
    private static byte[] PrecinctExponent()
    {
        var at = Segment(Gray, 0xFF52);
        var length = BinaryPrimitives.ReadUInt16BigEndian(Gray.AsSpan(at + 2));
        var levels = Gray[at + 9];
        var cod = Gray[at..(at + 2 + length)];
        cod[4] |= 1;
        byte[] precincts = [.. Enumerable.Range(0, levels + 1).Select(r => (byte)(r == 1 ? 0x00 : 0xFF))];
        byte[] segment = [.. cod, .. precincts];
        BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(2), (ushort)(length + precincts.Length));
        return [.. Gray[..at], .. segment, .. Gray[(at + 2 + length)..]];
    }

    // An expounded quantization listing only the LL band of a five-level image.
    private static byte[] TooFewStepSizes()
    {
        var at = Segment(Gray, 0xFF5C);
        var length = BinaryPrimitives.ReadUInt16BigEndian(Gray.AsSpan(at + 2));
        byte[] qcd = [0xFF, 0x5C, 0, 5, 0x42, 0x40, 0x00];
        return [.. Gray[..at], .. qcd, .. Gray[(at + 2 + length)..]];
    }
}
