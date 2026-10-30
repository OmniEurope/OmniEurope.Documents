// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Markdown;

namespace OmniEurope.Documents.Tests.Integration;

public sealed class CoveragePathsTests
{
    [Theory]
    [InlineData("[a\\]b]", 6)]
    [InlineData("[unfinished", 0)]
    [InlineData("[nested[bad]]", 0)]
    [InlineData("[]", 0)]
    public void Reference_labels_handle_escaped_delimiters_and_incomplete_input(string input, int end)
    {
        Assert.Equal(end, LinkSyntax.ParseLabel(input, 0));
    }

    [Theory]
    [InlineData(0xE1)]
    [InlineData(0xE0)]
    [InlineData(0xEE)]
    public void Unrecognized_jpeg_application_metadata_does_not_change_the_image(int marker)
    {
        var source = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "baseline.jpg"));
        byte[] extended = [.. source[..^2], 0xFF, (byte)marker, 0, 6, 1, 2, 3, 4, .. source[^2..]];
        var expected = JpegDecoder.Decode(source);

        var actual = JpegDecoder.Decode(extended);

        Assert.Equal(expected.Pixels, actual.Pixels);
        Assert.Equal((expected.Width, expected.Height, expected.DpiX, expected.DpiY), (actual.Width, actual.Height, actual.DpiX, actual.DpiY));
    }
}
