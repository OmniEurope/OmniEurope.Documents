// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;

namespace OmniEurope.Documents.Tests.Pdf;

public sealed class PdfFilterTests
{
    private const int Columns = 5;
    private const int Colors = 3;
    private const int RowLength = Columns * Colors;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Png_predictors_restore_the_rows(int filter)
    {
        var raw = Samples(4);

        var decoded = PdfFilters.Predict(PngEncode(raw, filter), Parameters(12, 8));

        Assert.Equal(raw, decoded);
    }

    [Fact]
    public void Tiff_predictor_restores_8_bit_rows_and_leaves_other_depths()
    {
        var raw = Samples(3);
        var differences = (byte[])raw.Clone();
        for (var start = 0; start < raw.Length; start += RowLength)
        {
            for (var i = RowLength - 1; i >= Colors; i--)
            {
                differences[start + i] = (byte)(raw[start + i] - raw[start + i - Colors]);
            }
        }

        Assert.Equal(raw, PdfFilters.Predict(differences, Parameters(2, 8)));
        Assert.Equal(differences, PdfFilters.Predict(differences, Parameters(2, 16)));
        Assert.Equal(differences, PdfFilters.Predict(differences, null));
    }

    [Fact]
    public void Rows_longer_than_the_data_leave_nothing_to_predict()
    {
        // 1 x 8 x 536870912 bits wraps to an empty row in 32 bits, which would never advance along the data.
        byte[] data = [1, 2, 3, 4, 5];

        Assert.Equal(data, PdfFilters.Predict(data, Predictor(2, 8, 1, 536870912)));
        Assert.Empty(PdfFilters.Predict([], Predictor(2, 8, 1, 536870912)));
        Assert.Empty(PdfFilters.Predict(data, Predictor(12, 8, 1, 536870912)));
    }

    [Theory]
    [InlineData(2, 16, 32, int.MaxValue)]
    [InlineData(2, 0, 1, 4)]
    [InlineData(12, 3, 1, 4)]
    [InlineData(2, 8, 33, 4)]
    public void Forged_predictor_parameters_are_refused(int predictor, int bits, int colors, int columns)
    {
        Assert.Throws<InvalidDataException>(() => PdfFilters.Predict([1, 2, 3, 4, 5], Predictor(predictor, bits, colors, columns)));
    }

    private static PdfDictionary Predictor(int predictor, int bits, int colors, int columns)
    {
        var parameters = new PdfDictionary();
        parameters.SetNumber("Predictor", predictor);
        parameters.SetNumber("BitsPerComponent", bits);
        parameters.SetNumber("Colors", colors);
        parameters.SetNumber("Columns", columns);
        return parameters;
    }

    [Fact]
    public void Lzw_decodes_the_strips_the_windows_tiff_encoder_wrote()
    {
        // TIFF LZW is PDF LZW with EarlyChange 1; the uncompressed TIFF of the same image gives the expected bytes.
        var lzw = Strips(Imaging.ImageFixtures.Read("rgb-lzw.tif"), out var predictor);
        var expected = Strips(Imaging.ImageFixtures.Read("rgb-none.tif"), out _).SelectMany(s => s).ToArray();
        var parameters = new PdfDictionary();
        parameters.SetNumber("Predictor", predictor);
        parameters.SetNumber("Colors", 3);
        parameters.SetNumber("Columns", 37);

        var decoded = lzw.SelectMany(s => PdfFilters.Decode(s, ["LZWDecode"], [parameters]).Data).ToArray();

        Assert.Equal(expected, decoded);
    }

    [Fact]
    public void Lzw_stops_at_a_code_not_yet_defined()
    {
        // 9-bit code 300 (100101100) first, before any string was added.
        Assert.Empty(PdfFilters.Decode([0x96, 0x00], ["LZW"], []).Data);
    }

    [Theory]
    [InlineData("ASCIIHexDecode", "48 65x6C6C6F7>", "48656C6C6F70")]
    [InlineData("AHx", "4", "40")]
    [InlineData("ASCII85Decode", "87cUR DZ~>", "48656C6C6F")]
    [InlineData("A85", "z87cUR~>", "0000000048656C6C")]
    public void Ascii_filters_skip_other_characters_and_finish_partial_groups(string filter, string encoded, string hex)
    {
        // "Hell" is 87cUR in base 85; a final "o" alone is DZ; an odd final hex digit is followed by 0.
        var decoded = PdfFilters.Decode(System.Text.Encoding.ASCII.GetBytes(encoded), [filter], []).Data;

        Assert.Equal(Convert.FromHexString(hex), decoded);
    }

    [Fact]
    public void Damaged_deflate_data_keeps_what_was_decoded()
    {
        // A stored block holding "Hello" (RFC 1951 §3.2.4), then a final block of the reserved type 3. The read
        // that meets the bad block loses the one byte it had decoded.
        byte[] data = [0x00, 0x05, 0x00, 0xFA, 0xFF, .. "Hello"u8, 0x07];
        byte[] empty = [0x07];

        Assert.Equal("Hell"u8.ToArray(), PdfFilters.Decode(data, ["FlateDecode"], []).Data);
        Assert.Throws<InvalidDataException>(() => PdfFilters.Decode(empty, ["FlateDecode"], []));
    }

    [Fact]
    public void Unknown_filters_are_not_supported()
    {
        Assert.Throws<NotSupportedException>(() => PdfFilters.Decode([1], ["BrotliDecode"], []));
    }

    // The strips of the first page of a TIFF file, and its predictor (tag 317, 1 when absent).
    private static List<byte[]> Strips(byte[] tiff, out int predictor)
    {
        var tags = new Documents.Imaging.TiffFile(tiff).Directories().First();
        predictor = tags.TryGetValue(317, out var p) && p.Length > 0 ? (int)p[0] : 1;
        var offsets = tags[273];
        var counts = tags[279];
        return [.. offsets.Select((offset, i) => tiff.AsSpan((int)offset, (int)counts[i]).ToArray())];
    }

    private static byte[] Samples(int rows)
    {
        var random = new Random(7);
        var data = new byte[rows * RowLength];
        random.NextBytes(data);
        return data;
    }

    // The encoder side of PNG filtering (RFC 2083 §6): each byte minus its predictor, row by row.
    private static byte[] PngEncode(byte[] raw, int filter)
    {
        var rows = raw.Length / RowLength;
        var output = new byte[rows * (RowLength + 1)];
        for (var r = 0; r < rows; r++)
        {
            output[r * (RowLength + 1)] = (byte)filter;
            for (var i = 0; i < RowLength; i++)
            {
                int At(int row, int column) => row < 0 || column < 0 ? 0 : raw[(row * RowLength) + column];
                var a = At(r, i - Colors);
                var b = At(r - 1, i);
                var c = At(r - 1, i - Colors);
                var predicted = filter switch
                {
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => PaethPredictor(a, b, c),
                    _ => 0,
                };
                output[(r * (RowLength + 1)) + 1 + i] = (byte)(raw[(r * RowLength) + i] - predicted);
            }
        }

        return output;
    }

    private static int PaethPredictor(int a, int b, int c)
    {
        var estimate = a + b - c;
        var distances = new[] { Math.Abs(estimate - a), Math.Abs(estimate - b), Math.Abs(estimate - c) };
        return distances[0] <= distances[1] && distances[0] <= distances[2] ? a : distances[1] <= distances[2] ? b : c;
    }

    private static PdfDictionary Parameters(int predictor, int bits) => new()
    {
        ["Predictor"] = new PdfNumber(predictor, true),
        ["Colors"] = new PdfNumber(Colors, true),
        ["BitsPerComponent"] = new PdfNumber(bits, true),
        ["Columns"] = new PdfNumber(Columns, true),
    };
}
