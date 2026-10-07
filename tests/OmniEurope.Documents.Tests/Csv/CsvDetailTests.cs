// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Csv;
using OmniEurope.Documents.Text;

namespace OmniEurope.Documents.Tests.Csv;

/// <summary>
/// CSV details (RFC 4180 and the tolerances real files need): delimiter detection past blank lines and inside
/// quotes, text after a closing quote, a byte order mark left in a header read from text, forced encodings,
/// records longer than the buffer, formula neutralisation, typed fields, flushing; Windows-1252 and UTF-8 checks.
/// </summary>
public sealed class CsvDetailTests
{
    [Fact]
    public void Detection_skips_blank_lines_and_ignores_delimiters_inside_quotes()
    {
        // The quoted field holds three commas; the semicolon is the delimiter.
        var rows = CsvReader.ReadAll("\r\n\r\n\"a,b,c,d\";x\r\n1;2", new CsvReaderOptions { HasHeader = false });

        Assert.Equal(["a,b,c,d", "x"], rows[0]);
        Assert.Equal(["1", "2"], rows[1]);
    }

    [Fact]
    public void Text_after_a_closing_quote_stays_in_the_field_and_the_last_record_needs_no_newline()
    {
        var rows = CsvReader.ReadAll("\"ab\"cd,e", new CsvReaderOptions { HasHeader = false, Delimiter = ',' });

        Assert.Equal(["abcd", "e"], Assert.Single(rows));
    }

    [Fact]
    public async Task A_byte_order_mark_read_as_text_is_dropped_from_the_first_header()
    {
        await using var reader = await CsvReader.OpenAsync(new StringReader("﻿Nom;Ville\nAda;Londres"), new CsvReaderOptions { Delimiter = ';' }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Ada", reader["Nom"]);
    }

    [Fact]
    public void A_forced_encoding_is_used_and_long_records_grow_the_buffer()
    {
        // Latin-1 é (0xE9) would not be valid UTF-8; a field of 300 000 characters is longer than one buffer.
        var big = new string('x', 300_000);
        var bytes = Encoding.Latin1.GetBytes("nom;texte\ncafé;" + big + "\n");

        using var reader = CsvReader.Open(new MemoryStream(bytes), new CsvReaderOptions { Encoding = Encoding.Latin1, Delimiter = ';' });

        Assert.True(reader.Read());
        Assert.Equal(("café", big.Length), (reader["nom"], reader["texte"].Length));
    }

    [Fact]
    public async Task Writing_neutralises_formulas_formats_values_and_flushes()
    {
        using var output = new MemoryStream();
        await using (var writer = CsvWriter.Create(output, new CsvWriterOptions { NeutralizeFormulas = true }, leaveOpen: true))
        {
            writer.WriteField("=1+1");
            writer.WriteField<string?>(null);
            writer.WriteField("texte");
            writer.WriteField(new Guid("00000000-0000-0000-0000-000000000001"));
            writer.NextRecord();
            writer.Flush();
            await writer.FlushAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal("'=1+1,,texte,00000000-0000-0000-0000-000000000001\r\n", Encoding.UTF8.GetString(output.ToArray()));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("=SUM(A1)", true)]
    public void Formula_detection(string value, bool dangerous)
    {
        Assert.Equal(dangerous, CsvFormula.IsDangerous(value));
    }

    [Theory]
    [InlineData("1:60", false)]
    [InlineData("1:2:61", false)]
    [InlineData("1:2:3", true)]
    public void Durations_reject_minutes_or_seconds_beyond_59(string text, bool valid)
    {
        Assert.Equal(valid, TextValueParser.TryParseDuration(text, out _));
    }

    [Fact]
    public void Utf8_with_an_incomplete_tail_must_continue_with_continuation_bytes()
    {
        Assert.True(TextEncodingDetector.IsValidUtf8([0x41, 0xE2, 0x82], isComplete: false));
        Assert.False(TextEncodingDetector.IsValidUtf8([0x41, 0xE2, 0x41], isComplete: false));
    }

    [Fact]
    public void Windows_1252_describes_itself_and_checks_buffers()
    {
        var encoding = new Windows1252Encoding();

        Assert.Equal(("windows-1252", "iso-8859-1", "windows-1252", true), (encoding.WebName, encoding.BodyName, encoding.HeaderName, encoding.IsSingleByte));
        Assert.Contains("Western European", encoding.EncodingName, StringComparison.Ordinal);
        Assert.Empty(encoding.GetPreamble());
        Assert.True(encoding.GetMaxCharCount(10) >= 10);
        Assert.Throws<ArgumentOutOfRangeException>(() => encoding.GetMaxCharCount(-1));
        Assert.Throws<ArgumentException>(() => encoding.GetBytes("€uro".ToCharArray(), 0, 4, new byte[2], 0));
        Assert.Throws<ArgumentException>(() => encoding.GetChars([0x80, 0x41, 0x42], 0, 3, new char[1], 0));
        Assert.Equal("€", encoding.GetString([0x80]));
    }
}
