// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging.Jpeg2000;

namespace OmniEurope.Documents.Tests.Imaging.Jpeg2000;

/// <summary>
/// JPEG 2000 conformance samples (Fixtures/Jpeg2000, licences in its LICENSE.txt), each decoded plane for plane to
/// the reference planes independent decoders gave for it (Fixtures/Jpeg2000/manifest.txt): exactly for the lossless
/// 5-3 samples, within one level for the irreversible 9-7 ones. The samples are grouped by what they exercise;
/// <see cref="The_samples_hold_every_feature_of_their_group"/> proves each group holds the features it is named after.
/// </summary>
public sealed class Jpeg2000ConformanceTests
{
    private static readonly Dictionary<string, string[]> GroupFeatures = new()
    {
        ["block"] = ["block-style:1", "block-style:2", "block-style:4", "block-style:8", "block-style:16", "block-style:32", "layers"],
        ["progression"] = ["order:0", "order:1", "order:2", "order:3", "order:4", "POC:main", "POC:tile", "COD:tile", "precincts", "tiles",
            "tile-parts", "origin", "layers"],
        ["markers"] = ["SOP", "EPH", "PLT", "TLM", "RGN", "PPM", "PPT"],
        ["components"] = ["subsampling:2x2", "subsampling:2x1", "subsampling:1x2", "subsampling:4x4", "signed", "depth:4", "depth:16",
            "components:5", "box:pclr", "box:cmap", "box:cdef", "colr:16", "colr:17", "colr:18", "colr:12", "colr:14", "colr:icc", "codestream",
            "RCT", "COC", "QCC"],
        ["irreversible"] = ["wavelet:9-7", "quantization:1", "quantization:2", "ICT"],
    };

    public static TheoryData<string> Block => Group("block");

    public static TheoryData<string> Progression => Group("progression");

    public static TheoryData<string> Markers => Group("markers");

    public static TheoryData<string> Components => Group("components");

    public static TheoryData<string> Irreversible => Group("irreversible");

    [Theory]
    [MemberData(nameof(Block))]
    public void Code_block_style_samples_decode_to_their_reference_planes(string file) => AssertReference(file);

    [Theory]
    [MemberData(nameof(Progression))]
    public void Progression_tile_and_precinct_samples_decode_to_their_reference_planes(string file) => AssertReference(file);

    [Theory]
    [MemberData(nameof(Markers))]
    public void Samples_with_packet_markers_packed_headers_and_regions_of_interest_decode_to_their_reference_planes(string file) =>
        AssertReference(file);

    [Theory]
    [MemberData(nameof(Components))]
    public void Component_colour_and_channel_samples_decode_to_their_reference_planes(string file) => AssertReference(file);

    [Theory]
    [MemberData(nameof(Irreversible))]
    public void Irreversible_samples_decode_to_their_reference_planes_within_one_level(string file) => AssertReference(file);

    [Fact]
    public void Every_sample_belongs_to_a_group_and_lossless_ones_must_be_exact()
    {
        var samples = Jpeg2000Conformance.Samples().ToList();

        Assert.Equal(69, samples.Count);
        Assert.All(samples, s => Assert.Contains(s.Group, GroupFeatures.Keys));
        Assert.All(samples, s => Assert.Equal(s.Group == "irreversible" ? 1 : 0, s.Tolerance));
        Assert.All(samples.Where(s => s.Tolerance > 0), s => Assert.Contains("wavelet:9-7", Jpeg2000Conformance.Features(Jpeg2000Conformance.Data(s))));
    }

    [Fact]
    public void The_samples_hold_every_feature_of_their_group()
    {
        foreach (var (group, required) in GroupFeatures)
        {
            var present = Jpeg2000Conformance.Samples().Where(s => s.Group == group)
                .SelectMany(s => Jpeg2000Conformance.Features(Jpeg2000Conformance.Data(s)))
                .ToHashSet();
            var missing = required.Where(f => !present.Contains(f)).ToList();
            Assert.True(missing.Count == 0, $"the {group} samples miss {string.Join(", ", missing)} (they hold {string.Join(", ", present.Order())})");
        }
    }

    [Fact]
    public void A_wrong_sample_in_the_reference_planes_fails_the_comparison()
    {
        foreach (var sample in new[] { Jpeg2000Conformance.Sample("lossless-rgba-u8-PLT.jp2"), Jpeg2000Conformance.Sample("irreversible-rgb-a.jp2") })
        {
            var decoded = Planes(Jpeg2000Decoder.Decode(Jpeg2000Conformance.Data(sample)));
            var expected = Jpeg2000Conformance.Expected(sample);
            Assert.Null(Jpeg2000Conformance.Mismatch(decoded, expected, sample.Tolerance));

            // The reference file itself altered by one byte, by one more level than the tolerance allows.
            var bytes = File.ReadAllBytes(Path.Combine(Jpeg2000Conformance.Folder, "Expected", sample.Expected));
            var at = bytes.Length - 1 - (bytes.Length / 3);
            bytes[at] = (byte)(bytes[at] > 127 ? bytes[at] - sample.Tolerance - 1 : bytes[at] + sample.Tolerance + 1);
            var altered = Jpeg2000Conformance.Planes(bytes);

            var mismatch = Jpeg2000Conformance.Mismatch(decoded, altered, sample.Tolerance);
            Assert.NotNull(mismatch);
            Assert.Contains("1 samples differ", mismatch, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_missing_or_resized_plane_fails_the_comparison()
    {
        var sample = Jpeg2000Conformance.Sample("lossless-gray-alpha-u8-prog1-layers1-res6.jp2");
        var decoded = Planes(Jpeg2000Decoder.Decode(Jpeg2000Conformance.Data(sample)));
        var expected = Jpeg2000Conformance.Expected(sample);

        Assert.NotNull(Jpeg2000Conformance.Mismatch(decoded.Take(1).ToList(), expected, 0));
        Assert.NotNull(Jpeg2000Conformance.Mismatch([decoded[0], decoded[1] with { Width = decoded[1].Width - 1 }], expected, 0));
    }

    /// <summary>The decoded colour channels then opacity channel, signed samples offset by half their range.</summary>
    internal static List<Jpeg2000TestPlane> Planes(Jpeg2000Image image) =>
        [.. image.Colors.Concat(image.Alpha is null ? [] : [image.Alpha]).Select(p => new Jpeg2000TestPlane(p.Width, p.Height, (1 << p.Precision) - 1,
            p.Signed ? [.. p.Samples.Select(v => v + (1 << (p.Precision - 1)))] : p.Samples))];

    private static TheoryData<string> Group(string name) =>
        new(Jpeg2000Conformance.Samples().Where(s => s.Group == name).Select(s => Path.GetFileName(s.File)));

    private static void AssertReference(string file)
    {
        var sample = Jpeg2000Conformance.Sample(file);

        var image = Jpeg2000Decoder.Decode(Jpeg2000Conformance.Data(sample));

        var mismatch = Jpeg2000Conformance.Mismatch(Planes(image), Jpeg2000Conformance.Expected(sample), sample.Tolerance);
        Assert.True(mismatch is null, $"{sample}: {mismatch}");
    }
}
