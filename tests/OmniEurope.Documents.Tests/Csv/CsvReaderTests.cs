// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Csv;
using OmniEurope.Documents.Text;

namespace OmniEurope.Documents.Tests.Csv;

public sealed class CsvReaderTests
{
    [Fact]
    public void Reads_fec_tab_separated_utf8_with_bom_by_header_and_ordinal()
    {
        var text = "JournalCode\tJournalLib\tEcritureDate\tDebit\tCredit\r\n"
                 + "AC\tAchats à crédit\t20221231\t1 234,56\t0,00\r\n"
                 + "VE\tVentes \"export\"\t20220105\t0\t99,5\r\n";
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray();

        using var csv = CsvReader.Open(new MemoryStream(bytes), new CsvReaderOptions { Quoting = CsvQuoting.None });

        Assert.Equal('\t', csv.Delimiter);
        Assert.Equal("JournalCode", csv.Headers[0]);
        Assert.True(csv.Read());
        Assert.Equal("Achats à crédit", csv["journallib"]);
        Assert.True(TextValueParser.TryParseDecimal(csv.GetSpan(3), out var debit));
        Assert.Equal(1234.56m, debit);
        Assert.True(TextValueParser.TryParseCompactDate(csv[2], out var date));
        Assert.Equal(new DateOnly(2022, 12, 31), date);
        Assert.True(csv.Read());
        Assert.Equal("Ventes \"export\"", csv[1]);
        Assert.Equal(2, csv.RecordNumber);
        Assert.False(csv.Read());
    }

    [Fact]
    public void Detects_windows_1252_semicolon_file_without_bom()
    {
        var bytes = Windows1252Encoding.Instance.GetBytes("Compte;Libellé;k€HT\r\n601;Achats;12\r\n");

        using var csv = CsvReader.Open(new MemoryStream(bytes));

        Assert.Equal(';', csv.Delimiter);
        Assert.Equal(1252, csv.Encoding!.CodePage);
        Assert.Equal("k€HT", csv.Headers[2]);
        Assert.True(csv.Read());
        Assert.Equal("601", csv[0]);
    }

    [Fact]
    public void Utf8_without_bom_is_detected_as_utf8()
    {
        var bytes = new UTF8Encoding(false).GetBytes("a,b\r\nété,ü\r\n");

        using var csv = CsvReader.Open(new MemoryStream(bytes));

        Assert.Equal(65001, csv.Encoding!.CodePage);
        Assert.True(csv.Read());
        Assert.Equal("été", csv[0]);
    }

    [Fact]
    public void Quoted_fields_hold_delimiters_line_breaks_and_doubled_quotes()
    {
        var rows = CsvReader.ReadAll("h1,h2,h3\n\"a,b\",\"line1\r\nline2\",\"say \"\"hi\"\"\"\nx,,\n", new CsvReaderOptions());

        Assert.Equal(2, rows.Count);
        Assert.Equal(["a,b", "line1\r\nline2", "say \"hi\""], rows[0]);
        Assert.Equal(["x", "", ""], rows[1]);
    }

    [Fact]
    public void Line_numbers_count_line_breaks_inside_quotes()
    {
        using var csv = CsvReader.Open(new StringReader("h\r\n\"a\r\nb\"\r\n\r\nc\r\n"));

        Assert.True(csv.Read());
        Assert.Equal(2, csv.LineNumber);
        Assert.True(csv.Read());
        Assert.Equal("c", csv[0]);
        Assert.Equal(5, csv.LineNumber);
    }

    [Fact]
    public void Short_records_are_tolerated()
    {
        using var csv = CsvReader.Open(new StringReader("a;b;c\n1;2\n"));

        Assert.True(csv.Read());
        Assert.Equal(2, csv.FieldCount);
        Assert.Equal(string.Empty, csv.GetFieldOrEmpty(2));
        Assert.Equal(string.Empty, csv["c"]);
        Assert.Throws<ArgumentOutOfRangeException>(() => csv[2]);
    }

    [Fact]
    public void Unterminated_quote_reports_the_line()
    {
        var error = Assert.Throws<CsvFormatException>(() => CsvReader.ReadAll("a\n\"open\nmore\n"));

        Assert.Equal(2, error.LineNumber);
    }

    [Fact]
    public void Records_larger_than_the_read_buffer_are_read_whole()
    {
        var big = new string('x', 1_200_000);
        using var csv = CsvReader.Open(new StringReader($"a,b\n\"{big}\",end\n"));

        Assert.True(csv.Read());
        Assert.Equal(big.Length, csv[0].Length);
        Assert.Equal("end", csv[1]);
    }

    [Fact]
    public void Record_over_the_limit_throws_instead_of_growing()
    {
        var options = new CsvReaderOptions { MaxRecordLength = 100, Delimiter = ',' };

        Assert.Throws<CsvFormatException>(() => CsvReader.ReadAll("h\n" + new string('y', 500) + "\n", options));
    }

    [Fact]
    public void Delimiters_count_towards_the_record_limit()
    {
        var options = new CsvReaderOptions { MaxRecordLength = 100, Delimiter = ',' };

        // Empty fields copy no character: only the delimiters make the record long.
        Assert.Throws<CsvFormatException>(() => CsvReader.ReadAll("h\n" + new string(',', 500) + "\n", options));
        Assert.Equal(101, CsvReader.ReadAll("h\n" + new string(',', 100) + "\n", options)[0].Length);
        Assert.Throws<CsvFormatException>(() => CsvReader.ReadAll("h\n" + new string(',', 101) + "\n", options));

        var short3 = new CsvReaderOptions { MaxRecordLength = 3, Delimiter = ',' };
        Assert.Equal(["a", "b"], CsvReader.ReadAll("h\na,b\n", short3)[0]);
        Assert.Throws<CsvFormatException>(() => CsvReader.ReadAll("h\na,bc\n", short3));
    }

    [Fact]
    public void Chunk_boundaries_inside_crlf_and_quotes_do_not_change_the_result()
    {
        // A reader returning one character at a time forces every state across a chunk boundary.
        var text = "a,b\r\n\"q\"\"x\",\"\r\n\"\r\nlast,\"\"\r\n";
        using var csv = CsvReader.Open(new OneCharReader(text));

        var rows = new List<string[]>();
        while (csv.Read())
        {
            rows.Add(csv.GetFields());
        }

        Assert.Equal([["q\"x", "\r\n"], ["last", ""]], rows);
    }

    [Fact]
    public async Task Reads_asynchronously_from_a_stream()
    {
        var bytes = Encoding.UTF8.GetBytes("n|v\n1|un\n2|deux\n");
        await using var csv = await CsvReader.OpenAsync(new MemoryStream(bytes), cancellationToken: TestContext.Current.CancellationToken);

        var values = new List<string>();
        while (await csv.ReadAsync(TestContext.Current.CancellationToken))
        {
            values.Add(csv["v"]);
        }

        Assert.Equal('|', csv.Delimiter);
        Assert.Equal(["un", "deux"], values);
    }

    [Fact]
    public void Without_header_every_line_is_data_and_aliases_resolve()
    {
        using var noHeader = CsvReader.Open(new StringReader("1,2\n3,4\n"), new CsvReaderOptions { HasHeader = false });
        Assert.True(noHeader.Read());
        Assert.Equal("1", noHeader[0]);

        using var aliased = CsvReader.Open(new StringReader("Start date,Duration\nx,y\n"));
        Assert.Equal(1, aliased.FindOrdinal("Time", "duration"));
        Assert.Equal(-1, aliased.FindOrdinal("missing"));
    }

    [Fact]
    public void Trim_applies_to_unquoted_fields_only()
    {
        var rows = CsvReader.ReadAll("h,i\n  a  ,\"  b  \"\n", new CsvReaderOptions { TrimFields = true });

        Assert.Equal(["a", "  b  "], rows[0]);
    }

    private sealed class OneCharReader(string text) : TextReader
    {
        private int _position;

        public override int Read(char[] buffer, int index, int count)
        {
            if (_position >= text.Length || count == 0)
            {
                return 0;
            }

            buffer[index] = text[_position++];
            return 1;
        }
    }
}
