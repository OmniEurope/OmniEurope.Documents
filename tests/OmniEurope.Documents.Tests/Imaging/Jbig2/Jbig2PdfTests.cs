// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Tests.Pdf;
using static OmniEurope.Documents.Tests.Imaging.Jbig2.SegmentWriter;

namespace OmniEurope.Documents.Tests.Imaging.Jbig2;

/// <summary>The JBIG2Decode filter: PDF images whose data is JBIG2, with or without JBIG2Globals.</summary>
public sealed class Jbig2PdfTests
{
    [Fact]
    public void A_jbig2_image_reads_its_global_segments_and_decodes_black_as_zero()
    {
        var symbol = Jbig2Images.Glyph(3, 3, 0);
        var globals = new SegmentWriter();
        globals.Add(0, Jbig2SymbolTests.Dictionary([symbol]), page: 0);
        var text = new ArithmeticWriter();
        text.Integer("IADT", 0);
        text.Integer("IADT", 1);
        text.Integer("IAFS", 2);
        text.SymbolId(0, 0);
        text.Integer("IADS", null);
        var stream = new SegmentWriter(1);
        stream.Add(48, PageInfo(8, 5));
        stream.Add(6, Jbig2SymbolTests.TextSegment(8, 5, 0x0010, 1, text), referred: [0]);
        stream.Add(49, []);

        var image = Assert.Single(PdfDocument.Open(Pdf(stream.ToArray(), globals.ToArray())).GetPage(1).Images);
        var raster = image.Decode()!;

        var expected = Jbig2Images.Compose(8, 5, (symbol, 2, 1));
        Assert.Equal(expected.Pixels.Select(p => p == 1 ? (byte)0 : (byte)255), raster.Pixels);
        Assert.Equal(["JBIG2Decode"], image.Filters);
    }

    [Fact]
    public void A_jbig2_page_smaller_than_the_image_is_padded_with_white_and_a_larger_one_is_cut()
    {
        var small = new SegmentWriter();
        small.Add(48, PageInfo(2, 1));
        small.Add(38, [.. Region(2, 1), 1, .. Jbig2GenericTests.MmrRow("##")]);
        var large = new SegmentWriter();
        large.Add(48, PageInfo(4, 2));
        large.Add(38, [.. Region(4, 1), 1, .. Jbig2GenericTests.MmrRow("####")]);

        var padded = Assert.Single(PdfDocument.Open(Pdf(small.ToArray(), null, width: 3, height: 2)).GetPage(1).Images).Decode()!;
        var cut = Assert.Single(PdfDocument.Open(Pdf(large.ToArray(), null, width: 3, height: 1)).GetPage(1).Images).Decode()!;

        Assert.Equal(new byte[] { 0, 0, 255, 255, 255, 255 }, padded.Pixels);
        Assert.Equal(new byte[] { 0, 0, 0 }, cut.Pixels);
    }

    [Fact]
    public void Broken_jbig2_data_is_not_drawn_and_is_reported()
    {
        var pdf = Pdf([1, 2, 3], null);

        var document = PdfDocument.Open(pdf);
        var rendering = PdfRenderer.Render(document.GetPage(1), new PdfRenderOptions { Dpi = 72 });

        Assert.Null(Assert.Single(document.GetPage(1).Images).Decode());
        Assert.Contains("images in an unsupported format are not drawn", rendering.Gaps);
    }

    [Fact]
    public void A_jbig2_stencil_mask_paints_its_black_pixels()
    {
        var stream = new SegmentWriter();
        stream.Add(48, PageInfo(2, 1));
        stream.Add(38, [.. Region(2, 1), 1, .. Jbig2GenericTests.MmrRow("#.")]);
        var pdf = RawPdf.Page(
            "0 0 1 rg q 2 0 0 1 0 0 cm /Im1 Do Q",
            "/XObject << /Im1 5 0 R >>",
            string.Empty,
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 2 /Height 1 /ImageMask true /Filter /JBIG2Decode", stream.ToArray()));

        var page = PdfRenderer.Render(PdfDocument.Open(pdf).GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image;

        Assert.Equal((0, 0, 255, 255), page.GetRgba(0, 199));
        Assert.Equal((255, 255, 255, 255), page.GetRgba(1, 199));
    }

    private static byte[] Pdf(byte[] data, byte[]? globals, int width = 8, int height = 5)
    {
        var parameters = globals is null ? string.Empty : "/DecodeParms << /JBIG2Globals 6 0 R >>";
        return RawPdf.Page(
            "q 1 0 0 1 0 0 cm /Im1 Do Q",
            "/XObject << /Im1 5 0 R >>",
            string.Empty,
            RawPdf.Stream($"/Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace /DeviceGray /BitsPerComponent 1 /Filter /JBIG2Decode {parameters}", data),
            globals is null ? "0" : RawPdf.Stream(string.Empty, globals));
    }
}
