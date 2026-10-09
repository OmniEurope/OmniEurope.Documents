// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging.Jbig2;
using OmniEurope.Documents.Pdf;

namespace OmniEurope.Documents.Tests.Imaging.Jbig2;

/// <summary>
/// Public JBIG2 conformance samples (Fixtures/Jbig2, licence in its LICENSE.txt), each page decoded pixel for pixel
/// to the reference bitmap an independent decoder gave for it (Fixtures/Jbig2/manifest.txt). The samples are grouped
/// by the region they exercise; <see cref="The_samples_hold_every_segment_kind_of_their_group"/> proves each group
/// really holds the segments it is named after.
/// </summary>
public sealed class Jbig2ConformanceTests
{
    private static readonly (string Group, string[] Prefixes, int[] Types)[] Groups =
    [
        ("generic", ["bitmap.", "bitmap-template", "bitmap-customat", "bitmap-tpgdon", "bitmap-mmr", "bitmap-stripe", "bitmap-initially",
            "bitmap-p32", "bitmap-randomaccess", "bitmap-trailing"], [Jbig2Segments.ImmediateGeneric, Jbig2Segments.ImmediateLosslessGeneric,
            Jbig2Segments.EndOfStripe]),
        ("refinement", ["bitmap-refine"], [Jbig2Segments.IntermediateGeneric, Jbig2Segments.IntermediateRefinement,
            Jbig2Segments.ImmediateRefinement, Jbig2Segments.ImmediateLosslessRefinement]),
        ("text", ["bitmap-symbol", "annex-h"], [Jbig2Segments.SymbolDictionary, Jbig2Segments.IntermediateText, Jbig2Segments.ImmediateText,
            Jbig2Segments.ImmediateLosslessText, Jbig2Segments.Tables]),
        ("halftone", ["bitmap-halftone"], [Jbig2Segments.PatternDictionary, Jbig2Segments.IntermediateHalftone,
            Jbig2Segments.ImmediateHalftone, Jbig2Segments.ImmediateLosslessHalftone]),
        ("composition", ["bitmap-composite"], [Jbig2Segments.ImmediateLosslessText, Jbig2Segments.ImmediateLosslessHalftone,
            Jbig2Segments.IntermediateRefinement]),
    ];

    public static TheoryData<string, int> Generic => Group("generic");

    public static TheoryData<string, int> Refinement => Group("refinement");

    public static TheoryData<string, int> Text => Group("text");

    public static TheoryData<string, int> Halftone => Group("halftone");

    public static TheoryData<string, int> Composition => Group("composition");

    public static TheoryData<string, int> WithGlobals => new(Jbig2Conformance.Samples("Samples")
        .Where(s => Jbig2Conformance.Embedded(s).Globals is not null)
        .Select(s => (s.File, s.Page)));

    [Theory]
    [MemberData(nameof(Generic))]
    public void Generic_region_samples_decode_to_their_reference_bitmap(string file, int page) => AssertReference(file, page);

    [Theory]
    [MemberData(nameof(Refinement))]
    public void Refinement_region_samples_decode_to_their_reference_bitmap(string file, int page) => AssertReference(file, page);

    [Theory]
    [MemberData(nameof(Text))]
    public void Symbol_dictionary_and_text_region_samples_decode_to_their_reference_bitmap(string file, int page) => AssertReference(file, page);

    [Theory]
    [MemberData(nameof(Halftone))]
    public void Pattern_dictionary_and_halftone_region_samples_decode_to_their_reference_bitmap(string file, int page) => AssertReference(file, page);

    [Theory]
    [MemberData(nameof(Composition))]
    public void Regions_combined_with_every_operator_decode_to_their_reference_bitmap(string file, int page) => AssertReference(file, page);

    [Theory]
    [MemberData(nameof(WithGlobals))]
    public void Samples_with_global_segments_decode_through_a_pdf_image_and_its_jbig2_globals(string file, int page)
    {
        var sample = Find(file, page);
        var (globals, data) = Jbig2Conformance.Embedded(sample);
        var (width, height, pixels) = Jbig2Conformance.Expected(sample);

        var image = Assert.Single(PdfDocument.Open(Jbig2Conformance.Pdf(data, globals, width, height)).GetPage(1).Images);
        var raster = image.Decode()!;

        Assert.Equal(pixels.Select(p => p == 1 ? (byte)0 : (byte)255), raster.Pixels);
    }

    [Fact]
    public void Every_sample_belongs_to_one_group_and_globals_are_exercised()
    {
        var samples = Jbig2Conformance.Samples("Samples").ToList();
        var grouped = Groups.Sum(g => InGroup(g.Group).Count());

        Assert.Equal(111, samples.Count);
        Assert.Equal(samples.Count, grouped);
        var withGlobals = samples.Count(s => Jbig2Conformance.Embedded(s).Globals is not null);
        Assert.True(withGlobals >= 3, $"only {withGlobals} samples have global segments");
    }

    [Fact]
    public void The_samples_hold_every_segment_kind_of_their_group()
    {
        foreach (var (group, _, types) in Groups)
        {
            var present = InGroup(group)
                .Select(s => s.File)
                .Distinct()
                .SelectMany(file => Jbig2Conformance.Segments(File.ReadAllBytes(Path.Combine(Jbig2Conformance.Folder, file))))
                .Select(s => s.Type)
                .ToHashSet();
            var missing = types.Where(t => !present.Contains(t)).ToList();
            Assert.True(missing.Count == 0, $"the {group} samples hold no segment of type {string.Join(", ", missing)} (they hold {string.Join(", ", present.Order())})");
        }

        var all = Jbig2Conformance.Samples("Samples").Select(s => s.File).Distinct()
            .SelectMany(file => Jbig2Conformance.Segments(File.ReadAllBytes(Path.Combine(Jbig2Conformance.Folder, file))))
            .Select(s => s.Type)
            .ToHashSet();
        Assert.Superset(new HashSet<int> { Jbig2Segments.PageInformation, Jbig2Segments.EndOfPage, Jbig2Segments.EndOfFile }, all);
    }

    private static TheoryData<string, int> Group(string name) => new(InGroup(name).Select(s => (s.File, s.Page)));

    // The samples of a group: those whose name starts with one of its prefixes, the first group matching winning.
    private static IEnumerable<Jbig2Sample> InGroup(string name) => Jbig2Conformance.Samples("Samples")
        .Where(s => Groups.First(g => g.Prefixes.Any(p => Path.GetFileName(s.File).StartsWith(p, StringComparison.Ordinal))).Group == name);

    private static Jbig2Sample Find(string file, int page) =>
        Jbig2Conformance.Samples("Samples").Single(s => s.File == file && s.Page == page);

    private static void AssertReference(string file, int page)
    {
        var sample = Find(file, page);
        var (globals, data) = Jbig2Conformance.Embedded(sample);
        var (width, height, pixels) = Jbig2Conformance.Expected(sample);

        var decoded = Jbig2Decoder.Decode(data, globals);

        Assert.Equal((width, height), (decoded.Width, decoded.Height));
        var wrong = pixels.Where((p, i) => decoded.Pixels[i] != p).Count();
        Assert.True(wrong == 0, $"{sample}: {wrong} pixels differ from the reference bitmap");
    }
}
