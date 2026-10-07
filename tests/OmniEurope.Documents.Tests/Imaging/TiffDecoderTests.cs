// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using static OmniEurope.Documents.Tests.Imaging.TiffWriter;

namespace OmniEurope.Documents.Tests.Imaging;

/// <summary>
/// TIFF pages written field by field (TIFF 6.0): several pages, fill order 2, CMYK, grey with alpha, LZW end
/// of information, damaged directories and strips. Expected pixels follow from the samples written.
/// </summary>
public sealed class TiffDecoderTests
{
    private static readonly (ushort, ushort, uint[]) Grey8 = (258, Short, [8]);
    private static readonly (ushort, ushort, uint[]) BlackIsZero = (262, Short, [1]);

    [Fact]
    public void Pages_are_counted_and_decoded_by_index()
    {
        // Big-endian; the second page stores its photometric interpretation as a BYTE field.
        var file = Write(
            false,
            new Page(2, 1, [Grey8, BlackIsZero], [10, 20]),
            new Page(1, 1, [(258, Short, [8, 8, 8]), (262, ByteField, [2]), (277, Short, [3])], [1, 2, 3]));

        Assert.Equal(2, TiffDecoder.PageCount(file));
        Assert.Equal(new byte[] { 10, 20 }, TiffDecoder.Decode(file).Pixels);
        Assert.Equal(new byte[] { 1, 2, 3 }, TiffDecoder.Decode(file, 1).Pixels);
        Assert.Throws<ArgumentOutOfRangeException>(() => TiffDecoder.Decode(file, 2));
    }

    [Fact]
    public void Fill_order_2_reverses_the_bits_of_each_byte()
    {
        // 0x0F read least significant bit first is 1111 0000: four white then four black pixels.
        var file = Write(true, new Page(8, 1, [(258, Short, [1]), BlackIsZero, (266, Short, [2])], [0x0F]));

        Assert.Equal(new byte[] { 255, 255, 255, 255, 0, 0, 0, 0 }, TiffDecoder.Decode(file).Pixels);
    }

    [Fact]
    public void Cmyk_and_grey_with_alpha_samples()
    {
        var cmyk = Write(true, new Page(1, 1, [(258, Short, [8, 8, 8, 8]), (262, Short, [5]), (277, Short, [4])], [255, 0, 0, 0]));
        var greyAlpha = Write(true, new Page(1, 1, [(258, Short, [8, 8]), BlackIsZero, (277, Short, [2]), (338, Short, [2])], [100, 50]));

        var cyan = TiffDecoder.Decode(cmyk);
        var translucent = TiffDecoder.Decode(greyAlpha);

        Assert.Equal((ImageColorType.Cmyk, ((byte)0, (byte)255, (byte)255, (byte)255)), (cyan.ColorType, cyan.GetRgba(0, 0)));
        Assert.Equal((ImageColorType.GrayAlpha, ((byte)100, (byte)100, (byte)100, (byte)50)), (translucent.ColorType, translucent.GetRgba(0, 0)));
    }

    [Fact]
    public void Lzw_stops_at_the_end_of_information_code()
    {
        // 9-bit codes: clear (256), literal 10, end of information (257): 100000000 000001010 100000001.
        var file = Write(true, new Page(2, 1, [Grey8, (259, Short, [5]), BlackIsZero], [0x80, 0x02, 0xA0, 0x20]));

        Assert.Equal(new byte[] { 10, 0 }, TiffDecoder.Decode(file).Pixels);
    }

    [Fact]
    public void Lzw_codes_not_yet_defined_are_invalid()
    {
        // First code 300 (100101100), before any string was added to the table.
        var file = Write(true, new Page(1, 1, [Grey8, (259, Short, [5]), BlackIsZero], [0x96, 0x00]));

        Assert.Throws<InvalidDataException>(() => TiffDecoder.Decode(file));
    }

    [Fact]
    public void Unknown_compressions_are_not_supported()
    {
        var file = Write(true, new Page(1, 1, [Grey8, (259, Short, [99]), BlackIsZero], [0]));

        Assert.Throws<NotSupportedException>(() => TiffDecoder.Decode(file));
    }

    [Fact]
    public void Missing_or_short_strips_leave_black_rows()
    {
        // Rows of one strip each: the second strip lies past the end of the file, a third strip has no row.
        var outside = Write(true, new Page(1, 2, [Grey8, BlackIsZero, (278, Long, [1])], [7], null, [9]));

        // One strip of two rows holding a single row, with the horizontal predictor: 10, +5.
        var shortStrip = Write(true, new Page(2, 2, [Grey8, BlackIsZero, (317, Short, [2])], [10, 5]));

        Assert.Equal(new byte[] { 7, 0 }, TiffDecoder.Decode(outside).Pixels);
        Assert.Equal(new byte[] { 10, 15, 0, 0 }, TiffDecoder.Decode(shortStrip).Pixels);
    }

    [Fact]
    public void Damaged_directories_keep_the_entries_that_can_be_read()
    {
        // An entry of unknown type 13 is ignored; a directory claiming more entries than the file holds stops early.
        var file = Write(true, new Page(1, 1, [Grey8, BlackIsZero, (700, 13, [1])], [42]));
        var directory = (int)BitConverter.ToUInt32(file, 4);
        var claimed = file.ToArray();
        claimed[directory] = 60;

        Assert.Equal(new byte[] { 42 }, TiffDecoder.Decode(file).Pixels);
        Assert.Equal(1, TiffDecoder.PageCount(claimed));
        Assert.Equal(new byte[] { 42 }, TiffDecoder.Decode(claimed).Pixels);
    }
}
