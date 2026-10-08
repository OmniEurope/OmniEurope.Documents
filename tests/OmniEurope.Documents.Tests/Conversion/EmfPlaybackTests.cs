// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// Enhanced metafile playback (MS-EMF): fixed map modes, world transforms, 32-bit records, text alignment and
/// damaged records. Most checks are equalities between two pictures that must draw the same thing; the
/// pictures are 400 x 200 device units shown at 300 x 150 points and rendered at 72 dpi. An independent EMF player
/// plays the map-mode and world-transform files with the shapes at the same device positions.
/// </summary>
public sealed class EmfPlaybackTests
{
    // A rectangle from 0.5 to 1 inch on both axes, y up from a viewport origin at the bottom, in each fixed map
    // mode: 0.1 mm (2), 0.01 mm (3), 0.01 in (4), 0.001 in (5), twips (6).
    [Theory]
    [InlineData(3, 1270, 2540)]
    [InlineData(4, 50, 100)]
    [InlineData(5, 500, 1000)]
    [InlineData(6, 720, 1440)]
    public void Fixed_map_modes_measure_the_same_lengths(int mode, int low, int high)
    {
        var reference = Draw(Square(2, 127, 254));

        var image = Draw(Square(mode, low, high));

        Assert.True(Differences(reference, image) == 0, $"{Differences(reference, image)} pixels differ");
        Assert.True(Ink(reference) > 500);
    }

    [Theory]
    [InlineData(8, 100, 50)]
    [InlineData(7, 50, 50)]
    public void Anisotropic_mode_scales_each_axis_and_isotropic_mode_keeps_the_aspect(int mode, int width, int height)
    {
        // Window extents 100 x 100 onto a viewport of 200 x 100 (MS-EMF: MM_ISOTROPIC is 7, MM_ANISOTROPIC 8):
        // a 50-unit square becomes 100 x 50 when each axis has its own scale, 50 x 50 with the smaller one.
        var scaled = Draw(Filled().Ints(17, mode).Ints(9, 100, 100).Ints(11, 200, 100).Ints(43, 0, 0, 50, 50));

        var expected = Draw(Filled().Ints(43, 0, 0, width, height));

        Assert.True(Differences(expected, scaled) == 0, $"{Differences(expected, scaled)} pixels differ");
        Assert.True(Ink(expected) > 500);
    }

    [Theory]
    [InlineData(36, 4)]
    [InlineData(35, 0)]
    public void Setting_the_world_transform_scales_what_follows(uint record, int mode)
    {
        var scaled = Draw(Filled().World(record, 2, 0, 0, 2, 0, 0, mode).Ints(43, 20, 20, 60, 60));

        Assert.Equal(0, Differences(Draw(Filled().Ints(43, 40, 40, 120, 120)), scaled));
    }

    [Theory]
    [InlineData(1, 20, 60)]
    [InlineData(2, 60, 140)]
    [InlineData(3, 50, 130)]
    public void Modifying_the_world_transform_resets_or_multiplies_on_either_side(int mode, int left, int right)
    {
        // World scale 2, then a translation of 10: reset (1), translation first (2), or scale first (3).
        var modified = Draw(Filled().World(35, 2, 0, 0, 2, 0, 0).World(36, 1, 0, 0, 1, 10, 0, mode).Ints(43, 20, 20, 60, 60));
        var top = mode == 1 ? 20 : 40;
        var bottom = mode == 1 ? 60 : 120;

        Assert.Equal(0, Differences(Draw(Filled().Ints(43, left, top, right, bottom)), modified));
    }

    [Theory]
    [InlineData(3u, 86u)]
    [InlineData(4u, 87u)]
    [InlineData(2u, 85u)]
    public void Records_with_32_bit_points_draw_like_their_16_bit_forms(uint wide, uint narrow)
    {
        (short, short)[] points = [(20, 20), (200, 40), (300, 180), (40, 160)];
        var image = Draw((narrow == 86 ? Filled() : Stroked()).Points16(narrow, points));

        Assert.True(Ink(image) > 100);
        Assert.Equal(0, Differences(image, Draw((narrow == 86 ? Filled() : Stroked()).Points32(wide, points))));
    }

    [Theory]
    [InlineData(5u, 88u)]
    [InlineData(6u, 89u)]
    public void Drawing_from_the_current_position_with_32_bit_points(uint wide, uint narrow)
    {
        (short, short)[] points = [(200, 40), (300, 180), (40, 160)];

        var image = Draw(Stroked().Ints(27, 20, 20).Points16(narrow, points));

        Assert.True(Ink(image) > 100);
        Assert.Equal(0, Differences(image, Draw(Stroked().Ints(27, 20, 20).Points32(wide, points))));
    }

    [Theory]
    [InlineData(8u, 91u)]
    [InlineData(7u, 90u)]
    public void Poly_polygons_and_polylines_with_32_bit_points(uint wide, uint narrow)
    {
        (short, short)[][] shapes = [[(10, 10), (100, 10), (100, 100), (10, 100)], [(150, 20), (380, 20), (260, 180)]];
        var paint = narrow == 90 ? Stroked() : Filled();
        var again = narrow == 90 ? Stroked() : Filled();

        var image = Draw(paint.PolyPoly(narrow, true, shapes));

        Assert.True(Ink(image) > 100);
        Assert.Equal(0, Differences(image, Draw(again.PolyPoly(wide, false, shapes))));
    }

    [Fact]
    public void Short_curves_lines_and_stock_objects_draw_what_they_can()
    {
        // A two-point Bézier is a line; a polyline-to without points draws nothing; a stock object changes nothing.
        var image = Draw(Stroked().Points16(85, (20, 100), (380, 100)).Points16(89).Select(0x80000004));

        Assert.True(Ink(image) > 100);
    }

    [Theory]
    [InlineData(6, -0.5)]
    [InlineData(2, -1.0)]
    public void Text_alignment_moves_the_text_left_of_its_reference(int align, double shift)
    {
        var left = Letters(Text(0));
        var aligned = Letters(Text(align));
        var width = left[^1].X + left[^1].Width - left[0].X;

        Assert.Equal(Math.Round(left[0].X + (shift * width), 1), Math.Round(aligned[0].X, 1));
    }

    [Fact]
    public void Vertical_alignment_puts_the_reference_at_the_top_baseline_or_bottom()
    {
        // In PDF space y goes up: the baseline of top-aligned text is lowest, of bottom-aligned text highest.
        var top = Letters(Text(0))[0].Y;
        var baseline = Letters(Text(24))[0].Y;
        var bottom = Letters(Text(8))[0].Y;

        Assert.True(baseline > top && bottom > baseline, $"{top} {baseline} {bottom}");
    }

    [Fact]
    public void Text_may_start_at_the_current_position()
    {
        var atReference = Letters(Text(0));
        var atPosition = Letters(new EmfFixture().Font(3, -24, 400, "Arial").Select(3).Ints(27, 100, 80).Ints(22, 1).Text("AB", 0, 0));

        Assert.Equal(Math.Round(atReference[0].X, 1), Math.Round(atPosition[0].X, 1));
        Assert.Equal(Math.Round(atReference[0].Y, 1), Math.Round(atPosition[0].Y, 1));
    }

    [Fact]
    public void Rotated_fonts_are_reported_and_empty_or_tiny_text_skipped()
    {
        var (_, gaps) = Convert(new EmfFixture().Brush(1, 0).Select(1).Ints(43, 0, 0, 10, 10)
            .Font(3, -24, 400, "Arial", escapement: 900).Select(3).Text("R", 100, 80)
            .Font(4, 0, 400, "Arial").Select(4).Text("tiny", 100, 120).Text(string.Empty, 10, 10));

        Assert.Contains("rotated text in pictures drawn horizontally", gaps);
    }

    [Fact]
    public void A_header_without_a_frame_falls_back_to_the_bounds()
    {
        var emf = Filled().Ints(43, 100, 50, 300, 150);

        Assert.True(Ink(Draw(emf, emptyFrame: true)) > 500);
    }

    [Fact]
    public void Unreadable_bitmaps_are_reported_and_empty_ones_skipped()
    {
        // A DIB header of 20 bytes is not a known header; a record without bitmap bytes has nothing to draw.
        var emf = Filled().Ints(43, 0, 0, 10, 10)
            .Add(81, w =>
            {
                w.Write(0); w.Write(0); w.Write(400); w.Write(200);
                w.Write(10); w.Write(10); w.Write(0); w.Write(0); w.Write(2); w.Write(2);
                w.Write(80); w.Write(20); w.Write(100); w.Write(4);
                w.Write(0u); w.Write(0x00CC0020u); w.Write(50); w.Write(50);
                w.Write(20); w.Write(2); w.Write(2); w.Write(0); w.Write(0);
                w.Write(0);
            })
            .Add(81, w =>
            {
                w.Write(0); w.Write(0); w.Write(400); w.Write(200);
                w.Write(10); w.Write(10); w.Write(0); w.Write(0); w.Write(2); w.Write(2);
                w.Write(0); w.Write(0); w.Write(0); w.Write(0);
                w.Write(0u); w.Write(0x00CC0020u); w.Write(50); w.Write(50);
            });

        Assert.Contains("unreadable bitmap in a picture skipped", Convert(emf).Gaps);
    }

    [Fact]
    public void Forged_counts_and_offsets_draw_only_what_the_record_holds()
    {
        // Counts of points and polygons far beyond the record, and bitmap offsets whose sum overflows.
        var emf = Filled().Ints(43, 100, 50, 300, 150)
            .Ints(86, 0, 0, 400, 200, int.MaxValue)
            .Ints(3, 0, 0, 400, 200, int.MaxValue, 1, 2)
            .Ints(91, 0, 0, 400, 200, int.MaxValue, int.MaxValue)
            .Ints(8, 0, 0, 400, 200, 1, 1, int.MaxValue, 5, 5)
            .Ints(81, 0, 0, 400, 200, 10, 10, 0, 0, 2, 2, int.MaxValue - 8, 40, int.MaxValue - 8, 16, 0, 0x00CC0020, 50, 50);

        var image = Draw(emf);

        Assert.True(Ink(image) > 500);
    }

    // A blue brush (COLORREF 0x00BBGGRR) and a null pen, so that shapes are compared by their fill alone.
    private static EmfFixture Filled() => new EmfFixture().Brush(1, 0xFF0000).Select(1).Pen(2, 1, 0xFF0000, style: 5).Select(2);

    private static EmfFixture Stroked() => new EmfFixture().Pen(2, 3, 0xFF0000).Select(2);

    private static EmfFixture Square(int mode, int low, int high) =>
        Filled().Ints(17, mode).Ints(12, 0, 200).Ints(43, low, high, high, low);

    private static EmfFixture Text(int align) =>
        new EmfFixture().Font(3, -24, 400, "Arial").Select(3).Ints(22, align).Text("AB", 100, 80);

    private static (byte[] Pdf, IReadOnlyList<string> Gaps) Convert(EmfFixture emf, bool emptyFrame = false)
    {
        var document = new WordDocument();
        document.AddParagraph().Add(new WordPicture(WordImage.FromBytes(emf.ToArray(emptyFrame)), 300, 150));
        var result = WordToPdf.Convert(document);
        return (result.Pdf, result.Gaps);
    }

    private static RasterImage Draw(EmfFixture emf, bool emptyFrame = false) =>
        PdfRenderer.Render(PdfDocument.Open(Convert(emf, emptyFrame).Pdf).GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image;

    private static IReadOnlyList<Documents.Pdf.Text.PdfLetter> Letters(EmfFixture emf) =>
        PdfDocument.Open(Convert(emf).Pdf).GetPage(1).Letters;

    private static int Differences(RasterImage a, RasterImage b) =>
        Enumerable.Range(0, a.Pixels.Length).Count(i => Math.Abs(a.Pixels[i] - b.Pixels[i]) > 1);

    private static int Ink(RasterImage image) => Enumerable.Range(0, image.Pixels.Length / 3).Count(i => image.Pixels[i * 3] < 128);
}
