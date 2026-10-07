// SPDX-License-Identifier: EUPL-1.2
using System.Buffers;
using System.Globalization;
using System.Text;

namespace OmniEurope.Documents.Csv;

/// <summary>When <see cref="CsvWriter"/> surrounds a field with quotes.</summary>
public enum CsvQuoteMode
{
    /// <summary>Only when the field holds the delimiter, the quote character, CR or LF (RFC 4180).</summary>
    AsNeeded,

    /// <summary>Every field.</summary>
    Always,

    /// <summary>Never; a field that would need quotes throws <see cref="CsvFormatException"/>.</summary>
    Never,
}

/// <summary>Options of <see cref="CsvWriter"/>.</summary>
public sealed class CsvWriterOptions
{
    /// <summary>The field delimiter. Default comma.</summary>
    public char Delimiter { get; init; } = ',';

    /// <summary>The quote character. Default <c>"</c>.</summary>
    public char Quote { get; init; } = '"';

    /// <summary>The record terminator. Default CRLF (RFC 4180).</summary>
    public string NewLine { get; init; } = "\r\n";

    /// <summary>Quoting policy. Default <see cref="CsvQuoteMode.AsNeeded"/>.</summary>
    public CsvQuoteMode QuoteMode { get; init; } = CsvQuoteMode.AsNeeded;

    /// <summary>True to neutralise values a spreadsheet would run as formulas (<see cref="CsvFormula"/>).</summary>
    public bool NeutralizeFormulas { get; init; }

    /// <summary>Encoding when writing to a stream. Default UTF-8.</summary>
    public Encoding Encoding { get; init; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>True to start a stream with the encoding's byte order mark (Excel needs it to read UTF-8).</summary>
    public bool WriteByteOrderMark { get; init; }

    /// <summary>Culture used to format non-string values. Default invariant.</summary>
    public IFormatProvider FormatProvider { get; init; } = CultureInfo.InvariantCulture;
}

/// <summary>Writes CSV records field by field or record by record.</summary>
public sealed class CsvWriter : IDisposable, IAsyncDisposable
{
    private readonly TextWriter _writer;
    private readonly CsvWriterOptions _options;
    private readonly bool _leaveOpen;
    private readonly SearchValues<char> _special;
    private bool _fieldWritten;

    /// <summary>Writes to an existing text writer.</summary>
    public CsvWriter(TextWriter writer, CsvWriterOptions? options = null, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
        _options = options ?? new CsvWriterOptions();
        _leaveOpen = leaveOpen;
        _special = SearchValues.Create([_options.Delimiter, _options.Quote, '\r', '\n']);
    }

    /// <summary>Writes to a stream with <see cref="CsvWriterOptions.Encoding"/>.</summary>
    public static CsvWriter Create(Stream stream, CsvWriterOptions? options = null, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= new CsvWriterOptions();
        if (options.WriteByteOrderMark)
        {
            stream.Write(ByteOrderMark(options.Encoding));
        }

        var encoding = WithoutPreamble(options.Encoding);
        return new CsvWriter(new StreamWriter(stream, encoding, 64 * 1024, leaveOpen), options, leaveOpen: false);
    }

    /// <summary>Writes every row to a string. Convenient for small outputs.</summary>
    public static string WriteAll(IEnumerable<IEnumerable<string?>> rows, CsvWriterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        using (var csv = new CsvWriter(text, options, leaveOpen: true))
        {
            foreach (var row in rows)
            {
                csv.WriteRecord(row);
            }
        }

        return text.ToString();
    }

    /// <summary>Writes one field of the current record.</summary>
    public void WriteField(string? value)
    {
        if (_fieldWritten)
        {
            _writer.Write(_options.Delimiter);
        }

        _fieldWritten = true;
        var text = value ?? string.Empty;
        if (_options.NeutralizeFormulas)
        {
            text = CsvFormula.Neutralize(text);
        }

        var needsQuotes = text.AsSpan().ContainsAny(_special);
        if (_options.QuoteMode == CsvQuoteMode.Always || (needsQuotes && _options.QuoteMode == CsvQuoteMode.AsNeeded))
        {
            WriteQuoted(text);
            return;
        }

        if (needsQuotes)
        {
            throw new CsvFormatException("Field needs quotes but quoting is disabled", 0);
        }

        _writer.Write(text);
    }

    /// <summary>Writes one field, formatted with <see cref="CsvWriterOptions.FormatProvider"/>.</summary>
    public void WriteField<T>(T value)
    {
        var text = value switch
        {
            null => null,
            string s => s,
            IFormattable f => f.ToString(null, _options.FormatProvider),
            _ => value.ToString(),
        };
        WriteField(text);
    }

    /// <summary>Ends the current record.</summary>
    public void NextRecord()
    {
        _writer.Write(_options.NewLine);
        _fieldWritten = false;
    }

    /// <summary>Writes a whole record.</summary>
    public void WriteRecord(IEnumerable<string?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        foreach (var field in fields)
        {
            WriteField(field);
        }

        NextRecord();
    }

    /// <summary>Writes a whole record.</summary>
    public void WriteRecord(params ReadOnlySpan<string?> fields)
    {
        foreach (var field in fields)
        {
            WriteField(field);
        }

        NextRecord();
    }

    /// <summary>Flushes buffered text.</summary>
    public void Flush() => _writer.Flush();

    /// <summary>Flushes buffered text.</summary>
    public Task FlushAsync(CancellationToken cancellationToken = default) => _writer.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        _writer.Flush();
        if (!_leaveOpen)
        {
            _writer.Dispose();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _writer.FlushAsync().ConfigureAwait(false);
        if (!_leaveOpen)
        {
            await _writer.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void WriteQuoted(string text)
    {
        var quote = _options.Quote;
        _writer.Write(quote);
        var span = text.AsSpan();
        while (true)
        {
            var at = span.IndexOf(quote);
            if (at < 0)
            {
                _writer.Write(span);
                break;
            }

            _writer.Write(span[..(at + 1)]);
            _writer.Write(quote);
            span = span[(at + 1)..];
        }

        _writer.Write(quote);
    }

    // The mark of the encoding even when the instance was built not to emit one (UTF8Encoding(false)).
    private static ReadOnlySpan<byte> ByteOrderMark(Encoding encoding) => encoding.CodePage switch
    {
        65001 => [0xEF, 0xBB, 0xBF],
        1200 => [0xFF, 0xFE],
        1201 => [0xFE, 0xFF],
        12000 => [0xFF, 0xFE, 0x00, 0x00],
        12001 => [0x00, 0x00, 0xFE, 0xFF],
        _ => encoding.Preamble,
    };

    private static Encoding WithoutPreamble(Encoding encoding) => encoding switch
    {
        UTF8Encoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        UnicodeEncoding u => new UnicodeEncoding(u.CodePage == 1201, byteOrderMark: false),
        UTF32Encoding u => new UTF32Encoding(u.CodePage == 12001, byteOrderMark: false),
        _ => encoding,
    };
}
