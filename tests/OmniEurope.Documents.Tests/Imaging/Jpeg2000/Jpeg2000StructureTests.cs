// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Imaging.Jpeg2000;
using OmniEurope.Documents.Pdf;
using static OmniEurope.Documents.Tests.Imaging.Jpeg2000.Jpeg2000DecoderTests;

namespace OmniEurope.Documents.Tests.Imaging.Jpeg2000;

/// <summary>
/// The pieces of the decoder on cases the samples do not reach: packet header bits after 0xFF, packet order with
/// overlapping progression changes and components of different depths of decomposition, one-sample signals at odd
/// coordinates, component transforms over mismatched components, and JP2 colour specifications.
/// </summary>
public sealed class Jpeg2000StructureTests
{
    private static readonly J2kQuantization NoQuantization = new(J2kQuantization.None, 2, [8, 9, 9, 10, 9, 9, 10], new int[7]);

    [Fact]
    public void A_packet_header_ending_on_0xFF_owns_the_next_byte()
    {
        var bits = new J2kPacketBits([0xFF, 0x7F, 0xAA], 0, 3);

        Assert.Equal(0xFF, bits.Bits(8));
        bits.Align();

        Assert.Equal(2, bits.Position);
        Assert.False(bits.Overrun);
    }

    [Fact]
    public void Packet_header_bits_past_the_end_read_as_one_and_tell_the_packet_was_cut()
    {
        var bits = new J2kPacketBits([0x00], 0, 1);

        Assert.Equal(0, bits.Bits(8));
        Assert.Equal(1, bits.Bit());
        Assert.True(bits.Overrun);
    }

    [Fact]
    public void Overlapping_progression_changes_give_each_packet_once_and_shallower_components_skip_missing_resolutions()
    {
        var deep = Component(levels: 2);
        var shallow = Component(levels: 1);
        var style = new J2kTileStyle(false, false, J2kProgression.LayerResolutionComponentPosition, 2, false);
        J2kProgressionChange[] changes =
        [
            new(0, 0, 1, 3, 2, J2kProgression.ResolutionPositionComponentLayer),
            new(0, 0, 2, 3, 2, J2kProgression.PositionComponentResolutionLayer),
            new(0, 0, 2, 9, 9, J2kProgression.LayerResolutionComponentPosition),
            new(1, 2, 2, 3, 2, J2kProgression.ResolutionPositionComponentLayer),
            new(0, 0, 2, 3, 2, J2kProgression.ComponentPositionResolutionLayer),
        ];

        var packets = J2kPacketOrder.Packets([deep, shallow], (0, 0, 16, 16), style, changes).ToList();

        Assert.Equal(2 * (3 + 2), packets.Count);
        Assert.Equal(packets.Count, packets.Distinct().Count());
        Assert.Equal(5, packets.Count(p => p.Layer == 0));
        Assert.DoesNotContain(packets, p => p.Component == 1 && p.Resolution == 2);
        Assert.Equal(new J2kPacket(0, 0, 0, 0), packets[0]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_single_sample_at_an_odd_coordinate_is_halved(bool reversible)
    {
        var style = new J2kComponentStyle(1, 6, 6, J2kBlockStyle.None, reversible, [15, 15], [15, 15]);
        var component = new J2kTileComponent((1, 0, 2, 1), new J2kComponentSize(8, false, 1, 1), style, NoQuantization, 0);
        foreach (var band in component.Resolutions.SelectMany(r => r.Bands))
        {
            band.Integers = [.. Enumerable.Repeat(40, band.Width * band.Height)];
            band.Reals = [.. Enumerable.Repeat(40f, band.Width * band.Height)];
        }

        J2kWavelet.Inverse(component);

        Assert.Equal(20, reversible ? component.Integers!.Single() : component.Reals!.Single());
    }

    [Fact]
    public void A_component_transform_over_mismatched_or_mixed_components_is_refused()
    {
        var reversible = new J2kComponentStyle(1, 6, 6, J2kBlockStyle.None, true, [15, 15], [15, 15]);
        var irreversible = reversible with { Reversible = false };
        var size = new J2kComponentSize(8, false, 1, 1);
        J2kTileComponent Make(J2kComponentStyle style, J2kComponentSize componentSize) =>
            new((0, 0, 4, 4), componentSize, style, NoQuantization, 0) { Integers = style.Reversible ? new int[16] : null, Reals = style.Reversible ? null : new float[16] };

        Assert.Throws<InvalidDataException>(() => J2kColorTransform.Inverse([Make(reversible, size), Make(reversible, size with { Dx = 2 }), Make(reversible, size)]));
        Assert.Throws<InvalidDataException>(() => J2kColorTransform.Inverse([Make(reversible, size), Make(irreversible, size), Make(reversible, size)]));
    }

    [Fact]
    public void Sub_bands_of_more_than_30_bit_planes_are_refused()
    {
        var style = new J2kComponentStyle(0, 6, 6, J2kBlockStyle.None, true, [15], [15]);

        Assert.Throws<NotSupportedException>(() => new J2kTileComponent((0, 0, 4, 4), new J2kComponentSize(8, false, 1, 1), style,
            new J2kQuantization(J2kQuantization.None, 7, [31], [0]), 0));
    }

    [Theory]
    [InlineData(new byte[] { 1, 0, 0, 0, 0, 0, 99 }, "Rgb")]
    [InlineData(new byte[] { 1, 0, 0, 0, 0, 0, 14 }, "Lab")]
    [InlineData(new byte[] { 3, 0, 0, 1, 2 }, "Unknown")]
    [InlineData(new byte[] { 4, 0, 0 }, "Rgb")]
    public void The_first_colour_specification_this_decoder_knows_names_the_colour_space(byte[] colr, string space)
    {
        var codestream = Codestream("lossless-rgb-u8-prog1-layers1-res6-mct.jp2");

        var image = Jpeg2000Decoder.Decode(Jp2(codestream, Box("colr", colr), Box("cdef", [0, 4, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 2,
            0, 2, 0, 0, 0, 3, 0, 9, 0, 5, 0, 0]), Box("colr", [1, 0, 0, 0, 0, 0, 16])));

        Assert.Equal(space, image.ColorSpace.ToString());
        Assert.Equal(3, image.Colors.Length);
        Assert.Equal(ImageColorType.Rgb, image.ToRaster().ColorType);
    }

    [Fact]
    public void A_colour_space_with_more_components_than_the_image_fills_the_others_with_zero()
    {
        var sample = Jpeg2000Conformance.Sample("lossless-gray-u8-prog1-layers1-res6.jp2");
        var gray = Jpeg2000Conformance.Expected(sample)[0];

        var raster = Assert.Single(PdfDocument.Open(Jpeg2000Conformance.Pdf(Jpeg2000Conformance.Data(sample), gray.Width, gray.Height, "/ColorSpace /DeviceRGB"))
            .GetPage(1).Images).Decode()!;

        Assert.Equal(gray.Samples.Select(v => (byte)v), raster.Pixels.Where((_, i) => i % 3 == 0));
        Assert.All(raster.Pixels.Where((_, i) => i % 3 != 0), v => Assert.Equal(0, v));
    }

    [Fact]
    public void Data_cut_anywhere_decodes_what_it_holds_or_is_reported_damaged()
    {
        var data = Jpeg2000Conformance.Data(Jpeg2000Conformance.Sample("rgba-u8-cbstyle-05-bypass-termall.jp2"));
        var decoded = 0;

        for (var cut = data.Length / 10; cut < data.Length; cut += data.Length / 10)
        {
            try
            {
                var image = Jpeg2000Decoder.Decode(data[..cut]);
                Assert.Equal(119, image.Width);
                decoded++;
            }
            catch (InvalidDataException)
            {
                // A cut inside the headers.
            }
        }

        Assert.True(decoded >= 8, $"only {decoded} cuts decoded");
    }

    // A one-component tile-component of 16 by 16 samples with the given number of decomposition levels.
    private static J2kTileComponent Component(int levels)
    {
        var precincts = Enumerable.Repeat(15, levels + 1).ToArray();
        var style = new J2kComponentStyle(levels, 6, 6, J2kBlockStyle.None, true, precincts, precincts);
        return new J2kTileComponent((0, 0, 16, 16), new J2kComponentSize(8, false, 1, 1), style, NoQuantization, 0);
    }
}
