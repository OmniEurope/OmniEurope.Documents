// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using static OmniEurope.Documents.Tests.Imaging.ImageFixtures;

namespace OmniEurope.Documents.Tests.Imaging;

public sealed class EncoderAndInfoTests
{
    [Theory]
    [InlineData(95, false, 1.5)]
    [InlineData(85, true, 6.0)]
    [InlineData(10, true, 20.0)]
    public void Jpeg_encoding_round_trips_within_the_expected_error(int quality, bool subsample, double maxMean)
    {
        var source = Gradient();

        var decoded = JpegDecoder.Decode(JpegEncoder.Encode(source, quality, subsample));

        Assert.Equal((source.Width, source.Height), (decoded.Width, decoded.Height));
        var mean = MeanError(source, decoded);
        Assert.True(mean < maxMean, $"mean error {mean:F2}");
    }

    [Fact]
    public void Higher_quality_gives_bigger_files_and_greyscale_stays_grey()
    {
        var source = Gradient();

        Assert.True(JpegEncoder.Encode(source, 95).Length > JpegEncoder.Encode(source, 30).Length);
        var gray = JpegDecoder.Decode(JpegEncoder.Encode(source.ConvertTo(ImageColorType.Gray)));
        Assert.Equal(ImageColorType.Gray, gray.ColorType);
    }

    [Theory]
    [InlineData("rgb.png", ImageFormat.Png, Width, Height)]
    [InlineData("baseline.jpg", ImageFormat.Jpeg, Width, Height)]
    [InlineData("indexed.gif", ImageFormat.Gif, Width, Height)]
    [InlineData("rgb.bmp", ImageFormat.Bmp, Width, Height)]
    [InlineData("bilevel-ccitt4.tif", ImageFormat.Tiff, 1800, 22)]
    public void Identifies_formats_from_headers(string name, ImageFormat format, int width, int height)
    {
        Assert.True(ImageInfo.TryIdentify(Read(name), out var info));
        Assert.Equal((format, width, height), (info.Format, info.Width, info.Height));
        Assert.Equal((width, height), (ImageDecoder.Decode(Read(name)).Width, ImageDecoder.Decode(Read(name)).Height));
    }

    [Fact]
    public void Unknown_data_is_not_identified()
    {
        Assert.False(ImageInfo.TryIdentify("hello world, not an image"u8.ToArray(), out _));
        Assert.Throws<NotSupportedException>(() => ImageDecoder.Decode([1, 2, 3]));
    }

    private static RasterImage Gradient()
    {
        var image = new RasterImage(121, 77, ImageColorType.Rgb);
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var i = ((y * image.Width) + x) * 3;
                image.Pixels[i] = (byte)(x * 2);
                image.Pixels[i + 1] = (byte)(y * 3);
                image.Pixels[i + 2] = (byte)(128 + (x - y));
            }
        }

        return image;
    }

    private static double MeanError(RasterImage a, RasterImage b)
    {
        long total = 0;
        for (var i = 0; i < a.Pixels.Length; i++)
        {
            total += Math.Abs(a.Pixels[i] - b.Pixels[i]);
        }

        return (double)total / a.Pixels.Length;
    }
}
