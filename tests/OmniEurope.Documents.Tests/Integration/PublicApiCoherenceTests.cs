// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Csv;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Pdf.Writing;
using OmniEurope.Documents.Tests.Pdf;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Integration;

public sealed class PublicApiCoherenceTests
{
    [Fact]
    public async Task Skipped_empty_lines_do_not_consume_the_record_budget_or_lose_line_numbers()
    {
        const string text = "\n\n\n\n\n\n\n\na;b\n1;2\n";
        var options = new CsvReaderOptions { MaxRecordLength = 8 };
        using var normal = CsvReader.Open(new StringReader(text), options);
        using var sync = CsvReader.Open(new ShortReader(text), options);
        using var asyncReader = await CsvReader.OpenAsync(new ShortReader(text), options, cancellationToken: TestContext.Current.CancellationToken);
        foreach (var reader in new[] { normal, sync, asyncReader })
        {
            Assert.Equal(';', reader.Delimiter);
            Assert.True(reader.Read());
            Assert.Equal("2", reader[1]);
            Assert.Equal(10, reader.LineNumber);
        }
    }

    [Fact]
    public void Markdown_reports_cell_alignment_without_reporting_only_a_paragraph_mark()
    {
        var document = new WordDocument();
        var table = document.AddTable(new WordTable(100));
        var row = table.AddRow("Aligned");
        ((WordParagraph)row.Cells[0].Blocks[0]).Properties = new WordParagraphProperties { Alignment = WordAlignment.Right };
        Assert.Contains(WordToMarkdown.Convert(document).Gaps, gap => gap.Contains("paragraph", StringComparison.Ordinal));

        var plain = new WordDocument();
        plain.AddParagraph("Plain").Properties = new WordParagraphProperties { MarkProperties = new WordRunProperties { Bold = true } };
        Assert.DoesNotContain(WordToMarkdown.Convert(plain).Gaps, gap => gap.Contains("paragraph", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n\r\n")]
    public async Task Csv_detection_is_independent_of_short_reads(string prefix)
    {
        var text = prefix + "name;value\na;1\n";
        using var normal = CsvReader.Open(new StringReader(text));
        using var shortSync = CsvReader.Open(new ShortReader(text));
        using var shortAsync = await CsvReader.OpenAsync(new ShortReader(text), cancellationToken: TestContext.Current.CancellationToken);

        foreach (var reader in new[] { normal, shortSync, shortAsync })
        {
            Assert.Equal(';', reader.Delimiter);
            Assert.Equal(["name", "value"], reader.Headers);
            Assert.True(reader.Read());
            Assert.Equal("1", reader[1]);
        }
    }

    [Theory]
    [InlineData(65001)]
    [InlineData(1200)]
    [InlineData(1201)]
    [InlineData(12000)]
    [InlineData(12001)]
    public void Csv_preserves_strict_and_custom_encoding_fallbacks(int codePage)
    {
        var strict = Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        Assert.Throws<EncoderFallbackException>(() => Write(strict, "\ud800", false));
        var custom = Encoding.GetEncoding(codePage, new EncoderReplacementFallback("[invalid]"), DecoderFallback.ExceptionFallback);
        Assert.Equal("[invalid]", custom.GetString(Write(custom, "\ud800", false)));
        var withMark = Write(strict, "OK", true);
        Assert.True(withMark.AsSpan().StartsWith(Encoding.GetEncoding(codePage).Preamble));
        Assert.Equal(strict.GetBytes("OK"), Write(strict, "OK", false));
    }

    private static byte[] Write(Encoding encoding, string text, bool mark)
    {
        using var stream = new MemoryStream();
        using (var writer = CsvWriter.Create(stream, new CsvWriterOptions { Encoding = encoding, WriteByteOrderMark = mark }, leaveOpen: true)) writer.WriteField(text);
        return stream.ToArray();
    }

    [Fact]
    public void Deleted_table_rows_are_excluded_from_all_accepted_views_after_reload()
    {
        var document = new WordDocument();
        var table = document.AddTable(new WordTable(150));
        table.AddRow("REMOVED_AUDIT").Revision = new WordRevision(WordRevisionKind.Deleted);
        table.AddRow("INSERTED_AUDIT").Revision = new WordRevision(WordRevisionKind.Inserted);
        table.AddRow("PUBLIC");
        var nested = new WordTable(100);
        nested.AddRow("REMOVED_NESTED").Revision = new WordRevision(WordRevisionKind.Deleted);
        nested.AddRow("NESTED_PUBLIC");
        table.Rows[2].Cells[0].Blocks.Add(nested);
        var loaded = WordDocument.Load(document.ToArray());
        var originalRows = ((WordTable)loaded.Blocks.Single()).Rows.Count;

        var views = new[] { loaded.Text, WordToHtml.Convert(loaded).Html, MarkdownToWord.Convert(WordToMarkdown.Convert(loaded).Markdown).Text, PdfDocument.Open(WordToPdf.Convert(loaded).Pdf).GetPage(1).Text };

        foreach (var view in views)
        {
            Assert.DoesNotContain("REMOVED", view, StringComparison.Ordinal);
            Assert.Contains("INSERTED_AUDIT", view, StringComparison.Ordinal);
            Assert.Contains("NESTED_PUBLIC", view, StringComparison.Ordinal);
        }
        Assert.Equal(originalRows, ((WordTable)loaded.Blocks.Single()).Rows.Count);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void Pdf_dates_keep_the_same_instant_across_calendars(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var date = new DateTimeOffset(2026, 10, 30, 12, 34, 56, TimeSpan.FromMinutes(330));
            var builder = new PdfDocumentBuilder { CreationDate = date, Conformance = PdfConformance.PdfA2u };
            builder.AddPage().DrawText("Audit", 10, 30, PdfFont.Sans, 12);
            var document = PdfDocument.Open(builder.ToArray());
            Assert.Equal(date, document.Information.CreationDate);
            var metadata = document.Store.Get<Documents.Pdf.Objects.PdfStream>(document.Store.Catalog, "Metadata")!;
            var xmp = System.Xml.Linq.XDocument.Parse(Encoding.UTF8.GetString(document.Store.DecodeBytes(metadata)));
            Assert.Equal(date, DateTimeOffset.Parse(xmp.Descendants().Single(e => e.Name.LocalName == "CreateDate").Value, CultureInfo.InvariantCulture));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void Form_text_inherits_parameters_and_restores_the_callers_state()
    {
        var bytes = RawPdf.Page("BT /F1 12 Tf 2 Tc 80 Tz 4 Ts 2 Tr ET /Fm Do BT 10 120 Td (TAIL) Tj ET",
            "/Font << /F1 5 0 R >> /XObject << /Fm 6 0 R >>", "",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 200 200]", RawPdf.Ascii("BT 10 50 Td (AB) Tj /F1 24 Tf 9 Tc ET")));
        var page = PdfDocument.Open(bytes).GetPage(1);

        Assert.Contains("AB", page.Text, StringComparison.Ordinal);
        Assert.Equal(6, page.Letters.Count);
        Assert.Equal(12, page.Letters[0].FontSize);
        Assert.Equal(10, page.Letters[0].X, 3);
        Assert.Equal(54, page.Letters[0].Y, 3);
        Assert.Equal(18.0032, page.Letters[1].X, 3);
        Assert.Equal(12, page.Letters[2].FontSize);
        Assert.Equal(2, page.Letters[0].RenderingMode);
        Assert.Empty(PdfRenderer.Render(page).Gaps);
    }

    [Fact]
    public void Markdown_does_not_report_the_defaults_of_the_document_as_lost_formatting()
    {
        var document = new WordDocument();
        document.AddParagraph("Plain text");
        foreach (var view in new[] { document, WordDocument.Load(document.ToArray()) })
        {
            Assert.DoesNotContain(WordToMarkdown.Convert(view).Gaps, gap => gap.Contains("font", StringComparison.Ordinal) || gap.Contains("paragraph", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Markdown_measures_lost_formatting_against_custom_document_defaults()
    {
        var document = new WordDocument();
        document.Styles.DefaultRunProperties = new WordRunProperties { Font = "Arial", FontSize = 10, Color = "336699" };
        document.Styles.DefaultParagraphProperties = new WordParagraphProperties { SpacingAfter = 3 };
        document.AddParagraph("Plain text");
        var plain = WordToMarkdown.Convert(WordDocument.Load(document.ToArray())).Gaps;
        Assert.DoesNotContain(plain, gap => gap.Contains("font", StringComparison.Ordinal) || gap.Contains("colour", StringComparison.Ordinal) || gap.Contains("paragraph", StringComparison.Ordinal));

        document.AddParagraph().AddText("Direct", new WordRunProperties { Font = "Courier New", Color = "FF0000" });
        var direct = WordToMarkdown.Convert(WordDocument.Load(document.ToArray())).Gaps;
        Assert.Contains(direct, gap => gap.Contains("font", StringComparison.Ordinal));
        Assert.Contains(direct, gap => gap.Contains("colour", StringComparison.Ordinal));
    }

    [Fact]
    public void Markdown_reports_encountered_run_and_paragraph_formatting_once()
    {
        var document = new WordDocument();
        for (var i = 0; i < 2; i++)
        {
            var paragraph = document.AddParagraph();
            paragraph.Properties = new WordParagraphProperties { Alignment = WordAlignment.Right, SpacingBefore = 24 };
            paragraph.AddText("Formatting", new WordRunProperties { Font = "Courier New", FontSize = 24, Color = "FF0000" });
        }
        var result = WordToMarkdown.Convert(document);

        Assert.Contains(result.Gaps, gap => gap.Contains("font", StringComparison.Ordinal));
        Assert.Contains(result.Gaps, gap => gap.Contains("colour", StringComparison.Ordinal));
        Assert.Contains(result.Gaps, gap => gap.Contains("paragraph", StringComparison.Ordinal));
        Assert.Equal(result.Gaps.Count, result.Gaps.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("Formatting", result.Markdown, StringComparison.Ordinal);
    }

    private sealed class ShortReader(string text) : TextReader
    {
        private int _position;
        public override int Read(char[] buffer, int index, int count) => Read(buffer.AsSpan(index, count));
        public override int Read(Span<char> buffer)
        {
            if (buffer.Length == 0 || _position == text.Length) return 0;
            buffer[0] = text[_position++];
            return 1;
        }
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default) => ValueTask.FromResult(Read(buffer.Span));
    }
}
