// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;

namespace OmniEurope.Documents.Tests.Imaging;

/// <summary>Pixel buffers: size checks, transparency, colour conversions (BT.601 luma), limits of the encoders.</summary>
public sealed class RasterImageTests
{
    [Fact]
    public void Sizes_beyond_the_limit_or_buffers_of_the_wrong_length_are_refused()
    {
        Assert.Throws<ArgumentException>(() => new RasterImage(65536, 4097, ImageColorType.Gray));
        Assert.Throws<ArgumentException>(() => new RasterImage(2, 2, ImageColorType.Rgb, new byte[11]));
    }

    [Fact]
    public void Transparency_needs_an_alpha_channel_with_a_pixel_below_255()
    {
        Assert.False(new RasterImage(1, 1, ImageColorType.Rgb).HasTransparency);
        Assert.False(new RasterImage(2, 1, ImageColorType.Rgba, [1, 2, 3, 255, 4, 5, 6, 255]).HasTransparency);
        Assert.True(new RasterImage(2, 1, ImageColorType.GrayAlpha, [9, 255, 9, 254]).HasTransparency);
    }

    [Fact]
    public void Grey_with_alpha_converts_to_rgba_and_red_to_its_luma()
    {
        var greyAlpha = new RasterImage(1, 1, ImageColorType.GrayAlpha, [90, 40]);
        var red = new RasterImage(1, 1, ImageColorType.Rgb, [255, 0, 0]);

        Assert.Equal(((byte)90, (byte)90, (byte)90, (byte)40), greyAlpha.GetRgba(0, 0));
        Assert.Equal(new byte[] { 90, 90, 90, 40 }, greyAlpha.ConvertTo(ImageColorType.Rgba).Pixels);

        // 0.299 x 255 = 76.2.
        Assert.Equal(new byte[] { 76, 255 }, red.ConvertTo(ImageColorType.GrayAlpha).Pixels);
    }

    [Fact]
    public void Conversion_to_cmyk_is_not_supported()
    {
        Assert.Throws<NotSupportedException>(() => new RasterImage(1, 1, ImageColorType.Rgb).ConvertTo(ImageColorType.Cmyk));
    }

    [Fact]
    public void Images_already_small_enough_are_not_scaled_and_jpeg_sides_stop_at_65535()
    {
        var image = new RasterImage(10, 5, ImageColorType.Gray);

        Assert.Same(image, ImageScaler.Fit(image, 10));
        Assert.Throws<ArgumentException>(() => JpegEncoder.Encode(new RasterImage(65536, 1, ImageColorType.Gray)));
    }
}
