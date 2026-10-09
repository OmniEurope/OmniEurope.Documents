// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging.Jbig2;
using static OmniEurope.Documents.Tests.Imaging.Jbig2.SegmentWriter;

namespace OmniEurope.Documents.Tests.Imaging.Jbig2;

/// <summary>
/// Pattern dictionaries and halftone regions (T.88 6.6, 6.7, C.5), arithmetic and MMR coded, on bitstreams encoded
/// by the test writer: grey values written as Gray-coded bit planes, most significant first.
/// </summary>
public sealed class Jbig2HalftoneTests
{
    private const int PatternDictionary = 16;
    private const int Halftone = 22;

    // Four 2 by 2 patterns of growing darkness, side by side in one collective bitmap.
    private static readonly string[] CollectiveRows = ["..#.###", "....###"];

    private static readonly int[,] Values = { { 0, 1, 2, 3 }, { 3, 2, 1, 0 }, { 1, 1, 3, 2 } };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void A_halftone_region_draws_the_pattern_of_each_grey_value(int template)
    {
        var grid = new Grid(4, 3, 0, 0, 512, 0);

        var page = Decode(8, 6, PatternsArithmetic(), HalftoneArithmetic(8, 6, grid, template, skip: false, flags: template << 1));

        Jbig2Images.AssertSame(Expected(8, 6, grid), page);
    }

    [Fact]
    public void Skipped_cells_and_a_turned_grid_are_handled()
    {
        // The grid starts 4 pixels left of the region: its two first columns fall outside and are skipped.
        var grid = new Grid(4, 3, -4 * 256, 0, 512, 0);
        var turned = new Grid(4, 3, 256, 2 * 256, 512, 256);

        var skipped = Decode(8, 6, PatternsArithmetic(), HalftoneArithmetic(8, 6, grid, 0, skip: true, flags: 8));
        var rotated = Decode(12, 9, PatternsArithmetic(), HalftoneArithmetic(12, 9, turned, 1, skip: false, flags: 2));

        Jbig2Images.AssertSame(Expected(8, 6, grid), skipped);
        Jbig2Images.AssertSame(Expected(12, 9, turned), rotated);
    }

    [Fact]
    public void Halftones_combine_their_cells_on_a_default_pixel()
    {
        // HCOMBOP XOR (2) on a black background: every pattern pixel turns white.
        var grid = new Grid(4, 3, 0, 0, 512, 0);

        var page = Decode(8, 6, PatternsArithmetic(), HalftoneArithmetic(8, 6, grid, 0, skip: false, flags: (2 << 4) | 0x80));

        var expected = Expected(8, 6, grid);
        Assert.Equal(expected.Pixels.Select(p => (byte)(1 - p)), page.Pixels);
    }

    [Fact]
    public void Mmr_pattern_dictionaries_and_bit_planes_decode()
    {
        var grid = new Grid(4, 3, 0, 0, 512, 0);
        byte[] patterns = [1, 2, 2, .. Be(3), .. Jbig2GenericTests.MmrRows(true, CollectiveRows[0] + ".", CollectiveRows[1] + ".")];
        var planes = new List<byte>();
        foreach (var plane in Planes())
        {
            planes.AddRange(Jbig2GenericTests.MmrRows(true, [.. Enumerable.Range(0, 3).Select(m => string.Concat(Enumerable.Range(0, 4).Select(n => plane[n, m] == 1 ? '#' : '.')))]));
        }

        byte[] halftone = [.. Region(8, 6), 1, .. Be(4), .. Be(3), .. Be(0), .. Be(0), .. Be16(512), .. Be16(0), .. planes];

        var page = Decode(8, 6, patterns, halftone);

        Jbig2Images.AssertSame(Expected(8, 6, grid), page);
    }

    [Fact]
    public void Broken_halftones_are_refused()
    {
        var noPatterns = new SegmentWriter();
        noPatterns.Add(48, PageInfo(8, 6));
        noPatterns.Add(Halftone, [.. Region(8, 6), 0, .. new byte[20]]);
        var empty = new SegmentWriter();
        empty.Add(48, PageInfo(8, 6));
        empty.Add(PatternDictionary, [0, 0, 2, .. Be(3)]);
        var hugeGrid = new SegmentWriter();
        hugeGrid.Add(48, PageInfo(8, 6));
        var p = hugeGrid.Add(PatternDictionary, PatternsArithmetic());
        hugeGrid.Add(Halftone, [.. Region(8, 6), 0, .. Be(100000), .. Be(100000), .. new byte[12]], referred: [p]);

        Assert.Throws<InvalidDataException>(() => Jbig2Decoder.Decode(noPatterns.ToArray()));
        Assert.Throws<InvalidDataException>(() => Jbig2Decoder.Decode(empty.ToArray()));
        Assert.Throws<InvalidDataException>(() => Jbig2Decoder.Decode(hugeGrid.ToArray()));
    }

    private static Jbig2Bitmap Collective()
    {
        var bitmap = new Jbig2Bitmap(8, 2);
        Jbig2Images.Parse(CollectiveRows[0] + ".", CollectiveRows[1] + ".").CopyTo(bitmap.Pixels, 0);
        return bitmap;
    }

    private static byte[] PatternsArithmetic()
    {
        var writer = new ArithmeticWriter();
        writer.Generic("GB", Collective(), 0, [(-2, 0), (-3, -1), (2, -2), (-2, -2)], false);
        return [0, 2, 2, .. Be(3), .. writer.Mq.Finish()];
    }

    // The bit planes as coded: bit j of each value's Gray code, most significant plane first.
    private static List<Jbig2Bitmap> Planes()
    {
        var planes = new List<Jbig2Bitmap>();
        for (var j = 1; j >= 0; j--)
        {
            var plane = new Jbig2Bitmap(4, 3);
            for (var m = 0; m < 3; m++)
            {
                for (var n = 0; n < 4; n++)
                {
                    var value = Values[m, n];
                    plane[n, m] = ((value ^ (value >> 1)) >> j) & 1;
                }
            }

            planes.Add(plane);
        }

        return planes;
    }

    private static byte[] HalftoneArithmetic(int width, int height, Grid grid, int template, bool skip, int flags)
    {
        var writer = new ArithmeticWriter();
        (int X, int Y)[] adaptive = template == 0 ? [(3, -1), (-3, -1), (2, -2), (-2, -2)] : [(template == 1 ? 3 : 2, -1)];
        Jbig2Bitmap? skipped = null;
        if (skip)
        {
            skipped = new Jbig2Bitmap(4, 3);
            for (var m = 0; m < 3; m++)
            {
                for (var n = 0; n < 4; n++)
                {
                    var (x, y) = grid.Cell(m, n);
                    skipped[n, m] = x + 2 <= 0 || x >= width || y + 2 <= 0 || y >= height ? 1 : 0;
                }
            }
        }

        foreach (var plane in Planes())
        {
            // A skipped pixel is not coded and reads as 0 in the contexts of the others.
            for (var i = 0; skipped is not null && i < plane.Pixels.Length; i++)
            {
                plane.Pixels[i] &= (byte)(1 - skipped.Pixels[i]);
            }

            writer.Generic("GB", plane, template, adaptive, false, skipped);
        }

        return [.. Region(width, height), (byte)flags, .. Be(grid.Width), .. Be(grid.Height), .. Be(grid.X), .. Be(grid.Y), .. Be16(grid.StepX), .. Be16(grid.StepY), .. writer.Mq.Finish()];
    }

    private static Jbig2Bitmap Expected(int width, int height, Grid grid)
    {
        var collective = Collective();
        var cells = new List<(Jbig2Bitmap, int, int)>();
        for (var m = 0; m < 3; m++)
        {
            for (var n = 0; n < 4; n++)
            {
                var (x, y) = grid.Cell(m, n);
                cells.Add((collective.Extract(Values[m, n] * 2, 0, 2, 2), x, y));
            }
        }

        return Jbig2Images.Compose(width, height, [.. cells]);
    }

    private static Jbig2Bitmap Decode(int width, int height, byte[] patterns, byte[] halftone)
    {
        var segments = new SegmentWriter();
        segments.Add(48, PageInfo(width, height));
        var p = segments.Add(PatternDictionary, patterns);
        segments.Add(Halftone, halftone, referred: [p]);
        return Jbig2Decoder.Decode(segments.ToArray());
    }

    private sealed record Grid(int Width, int Height, int X, int Y, int StepX, int StepY)
    {
        // T.88 6.6.5.2: x = (HGX + m HRY + n HRX) >> 8, y = (HGY + m HRX - n HRY) >> 8.
        public (int X, int Y) Cell(int m, int n) => ((X + (m * StepY) + (n * StepX)) >> 8, (Y + (m * StepX) - (n * StepY)) >> 8);
    }
}
