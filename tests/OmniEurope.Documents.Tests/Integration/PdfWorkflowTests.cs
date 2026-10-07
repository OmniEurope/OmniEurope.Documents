// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Editing;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Pdf.Writing;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Integration;

/// <summary>
/// PDF produced by the package's own writers (drawing, Word, images) assembled into one file, then cut,
/// reordered, rotated, stamped and compressed; every result is reopened, read and rendered.
/// </summary>
public sealed class PdfWorkflowTests
{
    [Fact]
    public void Sources_from_three_writers_merge_into_one_readable_file()
    {
        var merged = PdfDocument.Open(SampleDocuments.MergedPdf());

        Assert.Equal(6, merged.PageCount);
        Assert.Equal(["Rapport page 1", "Rapport page 2", "Rapport page 3", "Annexe Word", string.Empty, string.Empty],
            merged.Pages.Select(p => p.Text.Split('\n')[0].Trim()));
        Assert.Equal([0, 1, 0, 0, 1, 1], merged.Pages.Select(p => p.Images.Count));
        Assert.Equal((60, 40), (merged.GetPage(2).Images[0].PixelWidth, merged.GetPage(2).Images[0].PixelHeight));

        // The landscape photo turned its page; the portrait one did not.
        Assert.True(merged.GetPage(5).Width > merged.GetPage(5).Height);
        Assert.True(merged.GetPage(6).Width < merged.GetPage(6).Height);
        for (var i = 0; i < 3; i++)
        {
            Assert.True(Samples.Pixels(merged.GetPage(i + 1), SampleDocuments.PdfColours[i].R, SampleDocuments.PdfColours[i].G, SampleDocuments.PdfColours[i].B) > 100);
        }
    }

    [Fact]
    public void Extracting_splitting_and_removing_keep_the_right_pages()
    {
        var source = PdfDocument.Open(SampleDocuments.MergedPdf());

        var extracted = PdfDocument.Open(PdfEditor.ExtractPages(source, "2-3,6"));
        Assert.Equal(["Rapport page 2", "Rapport page 3", string.Empty], extracted.Pages.Select(p => p.Text.Split('\n')[0].Trim()));
        Assert.Single(extracted.GetPage(3).Images);

        var parts = PdfEditor.Split(source, 2).Select(b => PdfDocument.Open(b)).ToList();
        Assert.Equal([2, 2, 2], parts.Select(p => p.PageCount));
        Assert.Equal(source.Pages.Select(p => p.Text), parts.SelectMany(p => p.Pages).Select(p => p.Text));

        var ranges = PdfEditor.SplitByRanges(source, "4", "1-2").Select(b => PdfDocument.Open(b)).ToList();
        Assert.Equal(["Annexe Word"], ranges[0].Pages.Select(p => p.Text.Trim()));
        Assert.Equal(2, ranges[1].PageCount);

        var removed = PdfDocument.Open(PdfEditor.RemovePages(source, [1, 4]));
        Assert.Equal(4, removed.PageCount);
        Assert.Equal("Rapport page 2", removed.GetPage(1).Text.Trim());
        Assert.True(Samples.Pixels(removed.GetPage(1), SampleDocuments.PdfColours[1].R, SampleDocuments.PdfColours[1].G, SampleDocuments.PdfColours[1].B) > 100);

        // Pieces put back together give the original pages again.
        var rebuilt = PdfDocument.Open(PdfEditor.Merge(parts));
        Assert.Equal(source.Pages.Select(p => p.Text), rebuilt.Pages.Select(p => p.Text));
    }

    [Fact]
    public void Rotating_stamping_and_compressing_build_on_each_other()
    {
        var original = SampleDocuments.MergedPdf();

        var rotated = PdfEditor.RotatePages(PdfDocument.Open(original), [1], 90);
        var page = PdfDocument.Open(rotated).GetPage(1);
        Assert.Equal(90, page.Rotation);
        var image = PdfRenderer.Render(page, new PdfRenderOptions { Dpi = 36 }).Image;
        Assert.True(image.Width > image.Height);
        Assert.Contains("Rapport page 1", page.Text, StringComparison.Ordinal);

        // The stamp is an incremental update: the rotated file is kept byte for byte in front of it.
        var stamped = PdfStamper.StampText(rotated, "Confidentiel", new PdfStampOptions { Position = PdfStampPosition.Top, Size = 9 });
        Assert.True(stamped.AsSpan(0, rotated.Length).SequenceEqual(rotated));
        var reread = PdfDocument.Open(stamped);
        Assert.All(reread.Pages, p => Assert.Contains("Confidentiel", p.Text, StringComparison.Ordinal));
        Assert.Contains("Rapport page 3", reread.GetPage(3).Text, StringComparison.Ordinal);
        Assert.Equal(90, reread.GetPage(1).Rotation);

        var compressed = PdfCompressor.Compress(reread, new PdfCompressionOptions { ImageQuality = 60, MaxImageSide = 30 });
        var small = PdfDocument.Open(compressed);
        Assert.True(compressed.Length < stamped.Length);
        Assert.Equal(reread.Pages.Select(p => p.Text), small.Pages.Select(p => p.Text));
        Assert.All(small.Pages.SelectMany(p => p.Images), i => Assert.True(Math.Max(i.PixelWidth, i.PixelHeight) <= 30));
        Assert.True(Samples.Pixels(small.GetPage(3), SampleDocuments.PdfColours[2].R, SampleDocuments.PdfColours[2].G, SampleDocuments.PdfColours[2].B) > 100);
    }
}
