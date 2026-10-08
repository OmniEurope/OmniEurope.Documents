// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using static OmniEurope.Documents.Tests.Imaging.ImageFixtures;

namespace OmniEurope.Documents.Tests.Imaging;

public sealed class JpegDecoderTests
{
    [Theory]
    [InlineData("baseline.jpg")]
    [InlineData("s444.jpg")]
    [InlineData("s422-progressive.jpg")]
    [InlineData("gray.jpg")]
    [InlineData("restart.jpg")]
    [InlineData("progressive-restart.jpg")]
    public void Matches_the_windows_decoder(string name)
    {
        var image = JpegDecoder.Decode(Read(name));

        var (mean, max) = Compare(image, Read(name + ".reference.bgr"));

        Assert.Equal((Width, Height), (image.Width, image.Height));
        Assert.True(mean < 2.5, $"mean difference {mean:F2}");
        Assert.True(max < 48, $"largest difference {max}");
    }

    [Theory]
    [InlineData("baseline.jpg", "progressive.jpg")]
    [InlineData("baseline.jpg", "progressive-refine.jpg")]
    [InlineData("plain.jpg", "restart.jpg")]
    [InlineData("plain.jpg", "progressive-restart.jpg")]
    public void Same_coefficients_decode_to_the_same_pixels(string reference, string name)
    {
        var expected = JpegDecoder.Decode(Read(reference));

        var actual = JpegDecoder.Decode(Read(name));

        Assert.Equal(expected.Pixels, actual.Pixels);
    }

    [Fact]
    public void Reads_the_header_without_decoding()
    {
        Assert.Equal((Width, Height, 3), JpegDecoder.ReadHeader(Read("baseline.jpg")));
        Assert.Equal((Width, Height, 1), JpegDecoder.ReadHeader(Read("gray.jpg")));
        Assert.Equal(ImageColorType.Gray, JpegDecoder.Decode(Read("gray.jpg")).ColorType);
    }

    [Fact]
    public void Truncated_data_still_decodes_what_is_there()
    {
        var data = Read("baseline.jpg");

        var image = JpegDecoder.Decode(data[..(data.Length - 200)]);

        Assert.Equal(Width, image.Width);
    }

    [Theory]
    [InlineData("cmyk-ycck.jpg")]
    [InlineData("cmyk-plain.jpg")]
    public void Four_component_files_match_the_windows_decoder(string name)
    {
        var image = JpegDecoder.Decode(Read(name));
        var expected = Read(name + ".reference.cmyk");

        Assert.Equal(ImageColorType.Cmyk, image.ColorType);
        Assert.Equal(expected.Length, image.Pixels.Length);
        var differences = expected.Zip(image.Pixels, (a, b) => Math.Abs(a - b)).ToList();
        Assert.True(differences.Average() < 2.5, $"mean difference {differences.Average():F2}");
        Assert.True(differences.Max() < 48, $"largest difference {differences.Max()}");
    }

    [Theory]
    [InlineData(0xC3, 8, 1, 0x11, typeof(NotSupportedException))]
    [InlineData(0xC9, 8, 1, 0x11, typeof(NotSupportedException))]
    [InlineData(0xC0, 12, 1, 0x11, typeof(NotSupportedException))]
    [InlineData(0xC0, 8, 2, 0x11, typeof(InvalidDataException))]
    [InlineData(0xC0, 8, 1, 0x51, typeof(InvalidDataException))]
    [InlineData(0xC0, 8, 1, 0x10, typeof(InvalidDataException))]
    public void Frames_it_cannot_decode_are_refused(int marker, int precision, int components, int sampling, Type error)
    {
        // SOI, a frame header of 16 x 16 pixels, EOI.
        var frame = new List<byte> { 0xFF, 0xD8, 0xFF, (byte)marker, 0, (byte)(8 + (components * 3)), (byte)precision, 0, 16, 0, 16, (byte)components };
        for (var i = 0; i < components; i++)
        {
            frame.AddRange([(byte)(i + 1), (byte)sampling, 0]);
        }

        frame.AddRange([0xFF, 0xD9]);

        Assert.Throws(error, () => JpegDecoder.Decode([.. frame]));
    }

    [Fact]
    public void A_segment_longer_than_the_file_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => JpegDecoder.Decode([0xFF, 0xD8, 0xFF, 0xDB, 0x10, 0x00, 1, 2]));
    }

    [Fact]
    public void A_component_sampled_two_for_three_takes_the_sample_covering_each_pixel()
    {
        // R and B sampled 3 across, G 2 across (ITU-T T.81 A.1.1): six pixels hold four G samples, pixel x
        // taking G sample floor(2x / 3). R, G, B identifiers keep the samples as they are.
        var frame = new JpegFrame(6, 1, progressive: false, [new JpegComponent('R', 3, 1, 0), new JpegComponent('G', 2, 1, 0), new JpegComponent('B', 3, 1, 0)]);
        var flat = new JpegPlane(6, 1, 3, 1, [9, 9, 9, 9, 9, 9]);
        var green = new JpegPlane(4, 1, 2, 1, [0, 100, 200, 250]);

        var image = JpegColor.Convert(frame, [flat, green, flat], adobeTransform: -1);

        Assert.Equal([0, 0, 100, 200, 200, 250], Enumerable.Range(0, 6).Select(x => (int)image.GetRgba(x, 0).G));
    }
}
