// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;

namespace OmniEurope.Documents.Tests.Imaging;

public sealed class PredictorAndFaxTests
{
    public static readonly ushort[] RgbSamples =
    [
        10, 20, 30, 250, 5, 60, 0, 255, 128, 77, 77, 77,
        200, 100, 50, 1, 2, 3, 255, 255, 255, 30, 220, 10,
    ];

    public static readonly ushort[] GraySamples = [1000, 30000, 65000, 500, 64000, 2];

    [Fact]
    public void Tiff_predictor_restores_8_bit_rgb_in_intel_order()
    {
        var image = TiffDecoder.Decode(TiffFixture.WithPredictor(true, 8, 3, 4, 2, RgbSamples));

        for (var i = 0; i < 8; i++)
        {
            var (r, g, b, _) = image.GetRgba(i % 4, i / 4);
            Assert.Equal((RgbSamples[i * 3], RgbSamples[(i * 3) + 1], RgbSamples[(i * 3) + 2]), ((ushort)r, (ushort)g, (ushort)b));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Tiff_predictor_restores_16_bit_grey_in_both_byte_orders(bool littleEndian)
    {
        var image = TiffDecoder.Decode(TiffFixture.WithPredictor(littleEndian, 16, 1, 3, 2, GraySamples));

        var grey = Enumerable.Range(0, 6).Select(i => image.GetRgba(i % 3, i / 3).R).ToArray();
        Assert.Equal(GraySamples.Select(s => (byte)(s >> 8)), grey);
    }

    [Fact]
    public void Tiff_without_pixels_is_rejected_as_invalid()
    {
        var file = TiffFixture.WithPredictor(true, 8, 3, 4, 2, RgbSamples, (256, 4, 1, 0));

        Assert.Throws<InvalidDataException>(() => TiffDecoder.Decode(file));
    }

    [Theory]
    [InlineData(284, 2u)]
    [InlineData(259, 7u)]
    [InlineData(259, 6u)]
    [InlineData(339, 3u)]
    public void Tiff_planar_jpeg_and_floating_point_pages_are_not_supported(ushort tag, uint value)
    {
        var file = TiffFixture.WithPredictor(true, 8, 3, 4, 2, RgbSamples, (tag, 3, 1, value));

        Assert.Throws<NotSupportedException>(() => TiffDecoder.Decode(file));
    }

    [Fact]
    public void Tiff_with_three_bit_samples_is_not_supported()
    {
        var file = TiffFixture.WithPredictor(true, 8, 1, 3, 2, [1, 2, 3, 4, 5, 6], (258, 3, 1, 3));

        Assert.Throws<NotSupportedException>(() => TiffDecoder.Decode(file));
    }

    [Fact]
    public void Group_3_rows_whose_end_of_line_codes_end_on_a_byte()
    {
        // Two rows of 8 pixels, each after an EOL padded so that it ends on a byte boundary (T.4 with
        // EncodedByteAlign): white 8 = 10011; white 0 = 00110101 then black 8 = 000101.
        byte[] data = [0x00, 0x01, 0x98, 0x00, 0x01, 0x35, 0x14, 0x00, 0x01];

        var rows = CcittFaxDecoder.Decode(data, new CcittOptions { K = 0, Columns = 8, Rows = 2, EncodedByteAlign = true }, out var count, out var complete);

        Assert.True(complete);
        Assert.Equal(2, count);
        Assert.Equal([0xFF, 0x00], rows);
    }
}
