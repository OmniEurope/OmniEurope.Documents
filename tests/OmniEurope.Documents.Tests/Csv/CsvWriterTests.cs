// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Csv;

namespace OmniEurope.Documents.Tests.Csv;

public sealed class CsvWriterTests
{
    [Fact]
    public void Quotes_only_fields_that_need_it()
    {
        var text = CsvWriter.WriteAll([["plain", "a,b", "say \"hi\"", "two\nlines", null]]);

        Assert.Equal("plain,\"a,b\",\"say \"\"hi\"\"\",\"two\nlines\",\r\n", text);
    }

    [Fact]
    public void Written_text_reads_back_identically()
    {
        string[][] rows = [["h1", "h2"], ["x;y", "\"q\""], ["", "multi\r\nline"]];
        var text = CsvWriter.WriteAll(rows, new CsvWriterOptions { Delimiter = ';' });

        var back = CsvReader.ReadAll(text, new CsvReaderOptions { Delimiter = ';', HasHeader = false });

        Assert.Equal(rows, back);
    }

    [Theory]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("+cmd", "'+cmd")]
    [InlineData("@x", "'@x")]
    [InlineData("  =1+1", "'  =1+1")]
    [InlineData("\t1", "'\t1")]
    [InlineData("-12.5", "-12.5")]
    [InlineData("plain", "plain")]
    public void Neutralizes_formulas(string value, string expected)
    {
        Assert.Equal(expected, CsvFormula.Neutralize(value));
    }

    [Fact]
    public void Stream_output_carries_the_bom_when_asked()
    {
        var stream = new MemoryStream();
        using (var csv = CsvWriter.Create(stream, new CsvWriterOptions { WriteByteOrderMark = true }, leaveOpen: true))
        {
            csv.WriteRecord("é", "1");
        }

        var bytes = stream.ToArray();
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Equal("é,1\r\n", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }

    [Theory]
    [InlineData(1200, new byte[] { 0xFF, 0xFE })]
    [InlineData(1201, new byte[] { 0xFE, 0xFF })]
    [InlineData(12000, new byte[] { 0xFF, 0xFE, 0x00, 0x00 })]
    [InlineData(12001, new byte[] { 0x00, 0x00, 0xFE, 0xFF })]
    [InlineData(20127, new byte[0])]
    public void Unicode_encodings_get_their_own_bom_once(int codePage, byte[] bom)
    {
        var encoding = codePage switch
        {
            1200 => new UnicodeEncoding(false, false),
            1201 => new UnicodeEncoding(true, false),
            12000 => new UTF32Encoding(false, false),
            12001 => new UTF32Encoding(true, false),
            _ => Encoding.ASCII,
        };
        var stream = new MemoryStream();
        using (var csv = CsvWriter.Create(stream, new CsvWriterOptions { WriteByteOrderMark = true, Encoding = encoding }, leaveOpen: true))
        {
            csv.WriteRecord("a", "1");
        }

        var bytes = stream.ToArray();
        Assert.Equal(bom, bytes[..bom.Length]);
        Assert.Equal("a,1\r\n", encoding.GetString(bytes, bom.Length, bytes.Length - bom.Length));
    }

    [Fact]
    public void Formats_values_with_the_invariant_culture()
    {
        var writer = new StringWriter();
        using (var csv = new CsvWriter(writer, leaveOpen: true))
        {
            csv.WriteField(1234.5m);
            csv.WriteField(new DateOnly(2026, 10, 6));
            csv.NextRecord();
        }

        Assert.Equal("1234.5,10/06/2026\r\n", writer.ToString());
    }

    [Fact]
    public void Never_mode_refuses_a_field_that_needs_quotes()
    {
        Assert.Throws<CsvFormatException>(() => CsvWriter.WriteAll([["a,b"]], new CsvWriterOptions { QuoteMode = CsvQuoteMode.Never }));
    }
}
