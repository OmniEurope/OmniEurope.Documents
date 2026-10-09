// SPDX-License-Identifier: EUPL-1.2
using System.Diagnostics;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Tests;

namespace OmniEurope.Documents.StressTests;

/// <summary>
/// The large JBIG2 conformance samples (Fixtures/Jbig2/Large: dictionaries of many symbols, arithmetic and Huffman
/// coded), decoded through a PDF image pixel for pixel to their reference bitmap within a time limit.
/// </summary>
public sealed class Jbig2ScaleTests
{
    public static TheoryData<string> Large => new(Jbig2Conformance.Samples("Large").Select(s => s.File));

    [Theory]
    [MemberData(nameof(Large))]
    public void A_dictionary_of_many_symbols_decodes_to_its_reference_bitmap(string file)
    {
        var sample = Assert.Single(Jbig2Conformance.Samples("Large"), s => s.File == file);
        var (globals, data) = Jbig2Conformance.Embedded(sample);
        var (width, height, pixels) = Jbig2Conformance.Expected(sample);
        var watch = Stopwatch.StartNew();

        var raster = Assert.Single(PdfDocument.Open(Jbig2Conformance.Pdf(data, globals, width, height)).GetPage(1).Images).Decode()!;

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"{file} took {watch.Elapsed}");
        Assert.Equal(pixels.Select(p => p == 1 ? (byte)0 : (byte)255), raster.Pixels);
    }
}
