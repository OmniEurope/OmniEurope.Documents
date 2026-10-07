// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;

namespace OmniEurope.Documents.Tests.Imaging;

/// <summary>
/// Fax data written bit by bit from ITU-T T.4 and T.6 code tables, 8 pixels per row. Codes used: white run 0
/// = 00110101, white 8 = 10011, black 2 = 11, black 8 = 000101, EOL = 000000000001, vertical 0 = 1,
/// vertical right 1 = 011, horizontal = 001.
/// </summary>
public sealed class CcittFaxTests
{
    private const string Eol = "000000000001";
    private const string WhiteRow1D = "10011";
    private const string BlackRow1D = "00110101 000101";

    // Two-dimensional rows against an all-white reference: a white row, then two black pixels at the left.
    private const string White2D = "1";
    private const string TwoBlack2D = "001 00110101 11 1";

    [Fact]
    public void Group_4_stops_at_the_end_of_block_when_rows_are_not_given()
    {
        var data = Bits(White2D + TwoBlack2D + Eol + Eol);

        var rows = CcittFaxDecoder.Decode(data, new CcittOptions { K = -1, Columns = 8 }, out var count, out var complete);
        var inverted = CcittFaxDecoder.Decode(data, new CcittOptions { K = -1, Columns = 8, BlackIs1 = true }, out _, out _);

        Assert.True(complete);
        Assert.Equal(2, count);
        Assert.Equal([0xFF, 0x3F], rows);
        Assert.Equal([0x00, 0xC0], inverted);
    }

    [Fact]
    public void Group_4_rows_may_start_on_byte_boundaries()
    {
        var data = Bits(White2D, TwoBlack2D);

        var rows = CcittFaxDecoder.Decode(data, new CcittOptions { K = -1, Columns = 8, Rows = 2, EncodedByteAlign = true }, out _, out var complete);

        Assert.True(complete);
        Assert.Equal([0xFF, 0x3F], rows);
    }

    [Fact]
    public void Mixed_group_3_reads_the_tag_bit_and_stops_at_the_return_to_control()
    {
        // Rows: 1-D white, 2-D copy of the row above (vertical 0), 1-D black; then six EOL+1.
        var rtc = string.Concat(Enumerable.Repeat(Eol + "1", 6));
        var data = Bits(Eol + "1" + WhiteRow1D + Eol + "0" + White2D + Eol + "1" + BlackRow1D + rtc);

        var rows = CcittFaxDecoder.Decode(data, new CcittOptions { K = 2, Columns = 8 }, out var count, out var complete);

        Assert.True(complete);
        Assert.Equal(3, count);
        Assert.Equal([0xFF, 0xFF, 0x00], rows);
    }

    [Fact]
    public void One_dimensional_group_3_stops_at_two_end_of_lines()
    {
        var data = Bits(Eol + BlackRow1D + Eol + WhiteRow1D + Eol + Eol + Eol);

        var rows = CcittFaxDecoder.Decode(data, new CcittOptions { K = 0, Columns = 8 }, out var count, out var complete);

        Assert.True(complete);
        Assert.Equal(2, count);
        Assert.Equal([0x00, 0xFF], rows);
    }

    [Fact]
    public void Byte_aligned_rows_without_end_of_line_codes()
    {
        // TIFF compression 2: each one-dimensional row starts on a byte.
        var data = Bits(WhiteRow1D, BlackRow1D);

        var rows = CcittFaxDecoder.Decode(data, new CcittOptions { K = 0, Columns = 8, Rows = 2, ByteAlignedRowsWithoutEol = true }, out _, out var complete);

        Assert.True(complete);
        Assert.Equal([0xFF, 0x00], rows);
    }

    [Theory]
    [InlineData("1", 3)]
    [InlineData("1 001 0011", 2)]
    public void Data_ending_early_leaves_the_remaining_rows_white(string bits, int rows)
    {
        // The first row decodes; the rest is fill bits (an unsupported mode code) or stops inside a run code.
        var output = CcittFaxDecoder.Decode(Bits(bits), new CcittOptions { K = -1, Columns = 8, Rows = rows }, out var count, out var complete);

        Assert.False(complete);
        Assert.Equal(rows, count);
        Assert.All(output, b => Assert.Equal(0xFF, b));
    }

    [Fact]
    public void Missing_rows_are_reported_incomplete()
    {
        var output = CcittFaxDecoder.Decode([], new CcittOptions { K = -1, Columns = 8, Rows = 2 }, out var count, out var complete);

        Assert.False(complete);
        Assert.Equal(2, count);
        Assert.Equal([0xFF, 0xFF], output);
    }

    [Theory]
    [InlineData(-1, "011")]
    [InlineData(0, "0000000000000000")]
    public void Codes_pointing_outside_the_row_or_unknown_are_invalid(int k, string bits)
    {
        Assert.Throws<InvalidDataException>(() => CcittFaxDecoder.Decode(Bits(bits), new CcittOptions { K = k, Columns = 8, Rows = 1 }, out _, out _));
    }

    [Fact]
    public void Row_width_must_be_positive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CcittFaxDecoder.Decode([0], new CcittOptions { Columns = 0 }, out _, out _));
    }

    // Each argument is a group of bits starting on a new byte; spaces are ignored, the last byte is padded with zeros.
    private static byte[] Bits(params string[] groups) => [.. groups.SelectMany(group =>
    {
        var bits = group.Replace(" ", string.Empty, StringComparison.Ordinal);
        return Enumerable.Range(0, (bits.Length + 7) / 8).Select(i => Convert.ToByte(bits.Substring(i * 8, Math.Min(8, bits.Length - (i * 8))).PadRight(8, '0'), 2));
    })];
}
