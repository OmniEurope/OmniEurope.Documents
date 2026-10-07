// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Text;

namespace OmniEurope.Documents.Tests.Text;

public sealed class EncodingTests
{
    [Fact]
    public void Windows_1252_round_trips_every_byte()
    {
        var all = Enumerable.Range(0, 256).Select(b => (byte)b).ToArray();

        var text = Windows1252Encoding.Instance.GetString(all);
        var back = Windows1252Encoding.Instance.GetBytes(text);

        Assert.Equal(all, back);
        Assert.Equal('€', text[0x80]);
        Assert.Equal('’', text[0x92]);
        Assert.Equal('Ÿ', text[0x9F]);
    }

    [Fact]
    public void Windows_1252_replaces_unmappable_characters()
    {
        Assert.Equal("a?b?"u8.ToArray(), Windows1252Encoding.Instance.GetBytes("aЖb😀"));
    }

    [Theory]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF, 0x41 }, 65001, 3)]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x41, 0x00 }, 1200, 2)]
    [InlineData(new byte[] { 0xFE, 0xFF, 0x00, 0x41 }, 1201, 2)]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x00, 0x00 }, 12000, 4)]
    [InlineData(new byte[] { 0x00, 0x00, 0xFE, 0xFF }, 12001, 4)]
    [InlineData(new byte[] { 0xC3, 0xA9 }, 65001, 0)]
    [InlineData(new byte[] { 0xE9, 0x41 }, 1252, 0)]
    public void Detects_encodings(byte[] sample, int codePage, int preamble)
    {
        var detected = TextEncodingDetector.Detect(sample, isComplete: true);

        Assert.Equal(codePage, detected.Encoding.CodePage);
        Assert.Equal(preamble, detected.PreambleLength);
    }

    [Theory]
    [InlineData(new byte[] { 0xC0, 0x80 })]
    [InlineData(new byte[] { 0xED, 0xA0, 0x80 })]
    [InlineData(new byte[] { 0xF4, 0x90, 0x80, 0x80 })]
    [InlineData(new byte[] { 0xE0, 0x80, 0x80 })]
    public void Rejects_malformed_utf8(byte[] bytes)
    {
        Assert.False(TextEncodingDetector.IsValidUtf8(bytes));
    }

    [Fact]
    public void A_sequence_cut_by_the_sample_end_is_accepted_only_when_incomplete()
    {
        byte[] cut = [0x41, 0xE2, 0x82];

        Assert.True(TextEncodingDetector.IsValidUtf8(cut, isComplete: false));
        Assert.False(TextEncodingDetector.IsValidUtf8(cut, isComplete: true));
        Assert.Equal(Encoding.UTF8.CodePage, TextEncodingDetector.Detect(cut).Encoding.CodePage);
    }
}
