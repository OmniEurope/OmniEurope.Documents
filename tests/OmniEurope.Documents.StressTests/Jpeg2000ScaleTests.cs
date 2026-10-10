// SPDX-License-Identifier: EUPL-1.2
using System.Diagnostics;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Tests;

namespace OmniEurope.Documents.StressTests;

/// <summary>
/// A large JPEG 2000 image (Fixtures/Jpeg2000/Large: 4096 by 3072 RGB pixels in twelve tiles, lossless) decoded
/// through a PDF image pixel for pixel to the pattern it encodes, within a time limit; and damaged conformance
/// samples, which decode or are refused as damaged or unsupported, never with an internal error or a hang.
/// </summary>
public sealed class Jpeg2000ScaleTests
{
    [Fact]
    public void A_large_tiled_image_decodes_exactly_within_the_time_limit()
    {
        var data = File.ReadAllBytes(Path.Combine(Jpeg2000Conformance.Folder, "Large", "large-4096x3072.jp2"));
        var expected = Jpeg2000Conformance.Pattern(4096, 3072);
        var watch = Stopwatch.StartNew();

        var raster = Assert.Single(PdfDocument.Open(Jpeg2000Conformance.Pdf(data, 4096, 3072)).GetPage(1).Images).Decode()!;

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20), $"took {watch.Elapsed}");
        Assert.Equal((4096, 3072), (raster.Width, raster.Height));
        var wrong = Enumerable.Range(0, expected.Length).Count(i => raster.Pixels[i] != expected[i]);
        Assert.True(wrong == 0, $"{wrong} of {expected.Length} samples differ from the pattern");
    }

    public static TheoryData<string> Damageable => new(
        "lossless-rgba-u8-prog2-tile4x2-cblk4x16-tp3-layers3-res2.jp2", "rgba-u8-cbstyle-63-all.jp2", "lossy-rgba-u8-prog0-layers1-res6-mct.jp2",
        "subsampled-420-cprl.j2k", "packed-ppm-tiles.jp2", "lossless-indexed-u8-rgb-u8.jp2", "lossless-RGN.jp2", "poc-tile.jp2");

    [Theory]
    [MemberData(nameof(Damageable))]
    public async Task Damaged_samples_decode_or_are_refused_cleanly(string file)
    {
        var source = File.ReadAllBytes(Path.Combine(Jpeg2000Conformance.Folder, "Samples", file));
        var limit = TimeSpan.FromSeconds(10);
        var failures = new List<string>();
        var decoded = 0;
        var index = 0;
        foreach (var bytes in Damage.Bytes(source, 150, file.Length))
        {
            var i = index++;
            try
            {
                // Decode reports damaged and unsupported data as null; anything thrown is an internal error.
                var raster = await Task.Run(() => Assert.Single(PdfDocument.Open(Jpeg2000Conformance.Pdf(bytes, 119, 101)).GetPage(1).Images).Decode(),
                    TestContext.Current.CancellationToken).WaitAsync(limit, TestContext.Current.CancellationToken);
                decoded += raster is null ? 0 : 1;
            }
            catch (TimeoutException)
            {
                failures.Add($"#{i}: still running after {limit.TotalSeconds} s");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add($"#{i}: {exception.GetType().Name} {exception.StackTrace?.Split('\n')[0].Trim()}");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} of {index} inputs failed:\n" + string.Join("\n", failures.Take(10)));
        Assert.True(decoded > 0, "none of the damaged inputs decoded: the decoder refuses everything");
    }
}
