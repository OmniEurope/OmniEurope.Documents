// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Internal;
using OmniEurope.Documents.Text;

namespace OmniEurope.Documents.Csv;

/// <summary>
/// Streaming CSV reader. Reads one record at a time with <see cref="Read"/> or <see cref="ReadAsync"/>,
/// so a file of any size runs in constant memory (bounded by the longest record). Fields are read by
/// position or by header name; a short record is tolerated (<see cref="GetFieldOrEmpty"/>).
/// </summary>
public sealed class CsvReader : IDisposable, IAsyncDisposable
{
    private const int ChunkSize = 64 * 1024;

    private readonly TextReader _reader;
    private readonly bool _leaveOpen;
    private readonly CsvReaderOptions _options;
    private char[] _buffer = new char[ChunkSize];
    private int _position;
    private int _length;
    private bool _endOfInput;
    private CsvRecordParser _parser = null!;
    private string?[] _cache = [];
    private Dictionary<string, int>? _ordinals;
    private bool _pendingReset;

    private CsvReader(TextReader reader, CsvReaderOptions options, bool leaveOpen, Encoding? encoding)
    {
        _reader = reader;
        _options = options;
        _leaveOpen = leaveOpen;
        Encoding = encoding;
    }

    /// <summary>The field delimiter in use (configured or detected).</summary>
    public char Delimiter { get; private set; }

    /// <summary>The encoding the stream is decoded with; null when reading from a <see cref="TextReader"/>.</summary>
    public Encoding? Encoding { get; }

    /// <summary>The column names, empty when <see cref="CsvReaderOptions.HasHeader"/> is false.</summary>
    public IReadOnlyList<string> Headers { get; private set; } = [];

    /// <summary>The number of fields of the current record.</summary>
    public int FieldCount => _parser.FieldCount;

    /// <summary>The 1-based number of the current data record (the header is not counted).</summary>
    public long RecordNumber { get; private set; }

    /// <summary>The 1-based line on which the current record starts.</summary>
    public long LineNumber => _parser.RecordLine;

    /// <summary>Opens a reader on a stream, detecting its encoding unless one is configured.</summary>
    public static CsvReader Open(Stream stream, CsvReaderOptions? options = null, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= new CsvReaderOptions();
        var (textReader, encoding) = OpenText(stream, options, leaveOpen, sample => PrefixedReadStream.ReadAtMost(stream, sample, sample.Length));
        return Initialized(new CsvReader(textReader, options, leaveOpen: false, encoding));
    }

    /// <summary>Asynchronous <see cref="Open(Stream, CsvReaderOptions?, bool)"/>.</summary>
    public static async Task<CsvReader> OpenAsync(Stream stream, CsvReaderOptions? options = null, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= new CsvReaderOptions();
        byte[]? sample = null;
        var sampleLength = 0;
        if (options.Encoding is null)
        {
            sample = new byte[Math.Max(4, options.EncodingSampleSize)];
            sampleLength = await PrefixedReadStream.ReadAtMostAsync(stream, sample, sample.Length, cancellationToken).ConfigureAwait(false);
        }

        var (textReader, encoding) = OpenText(stream, options, leaveOpen, buffer =>
        {
            sample!.AsSpan(0, sampleLength).CopyTo(buffer);
            return sampleLength;
        });
        return await InitializedAsync(new CsvReader(textReader, options, leaveOpen: false, encoding), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens a reader on text that is already decoded.</summary>
    public static CsvReader Open(TextReader reader, CsvReaderOptions? options = null, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return Initialized(new CsvReader(reader, options ?? new CsvReaderOptions(), leaveOpen, encoding: null));
    }

    /// <summary>Asynchronous <see cref="Open(TextReader, CsvReaderOptions?, bool)"/>.</summary>
    public static async Task<CsvReader> OpenAsync(TextReader reader, CsvReaderOptions? options = null, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return await InitializedAsync(new CsvReader(reader, options ?? new CsvReaderOptions(), leaveOpen, encoding: null), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads every data record of <paramref name="text"/>. Convenient for small inputs.</summary>
    public static List<string[]> ReadAll(string text, CsvReaderOptions? options = null)
    {
        using var reader = Open(new StringReader(text), options);
        var rows = new List<string[]>();
        while (reader.Read())
        {
            rows.Add(reader.GetFields());
        }

        return rows;
    }

    /// <summary>Advances to the next record. Returns false at the end of input.</summary>
    public bool Read()
    {
        BeginRecord();
        while (true)
        {
            if (_position >= _length && !_endOfInput)
            {
                Fill(_reader.Read(_buffer, 0, _buffer.Length));
            }

            if (TryAdvance(out var hasRecord))
            {
                return hasRecord;
            }
        }
    }

    /// <summary>Asynchronous <see cref="Read"/>.</summary>
    public async ValueTask<bool> ReadAsync(CancellationToken cancellationToken = default)
    {
        BeginRecord();
        while (true)
        {
            if (_position >= _length && !_endOfInput)
            {
                Fill(await _reader.ReadAsync(_buffer.AsMemory(), cancellationToken).ConfigureAwait(false));
            }

            if (TryAdvance(out var hasRecord))
            {
                return hasRecord;
            }
        }
    }

    /// <summary>The field at <paramref name="ordinal"/> of the current record.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The record has no such field.</exception>
    public string this[int ordinal] => GetString(ordinal);

    /// <summary>The field under the column named <paramref name="header"/> (case-insensitive).</summary>
    /// <exception cref="KeyNotFoundException">No column has that name.</exception>
    public string this[string header] =>
        TryGetOrdinal(header, out var ordinal) ? GetFieldOrEmpty(ordinal) : throw new KeyNotFoundException($"No CSV column named '{header}'.");

    /// <summary>The field at <paramref name="ordinal"/> as a string.</summary>
    public string GetString(int ordinal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(ordinal, FieldCount);
        return _cache[ordinal] ??= _parser.Field(ordinal, _options.TrimFields).ToString();
    }

    /// <summary>The field at <paramref name="ordinal"/> without allocating; valid until the next read.</summary>
    public ReadOnlySpan<char> GetSpan(int ordinal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(ordinal, FieldCount);
        return _parser.Field(ordinal, _options.TrimFields);
    }

    /// <summary>The field at <paramref name="ordinal"/>, or an empty string when the record is shorter.</summary>
    public string GetFieldOrEmpty(int ordinal) => ordinal >= 0 && ordinal < FieldCount ? GetString(ordinal) : string.Empty;

    /// <summary>A copy of every field of the current record.</summary>
    public string[] GetFields()
    {
        var fields = new string[FieldCount];
        for (var i = 0; i < fields.Length; i++)
        {
            fields[i] = GetString(i);
        }

        return fields;
    }

    /// <summary>Finds the column named <paramref name="header"/> (case-insensitive, surrounding spaces ignored).</summary>
    public bool TryGetOrdinal(string header, out int ordinal)
    {
        ArgumentNullException.ThrowIfNull(header);
        ordinal = -1;
        return _ordinals is not null && _ordinals.TryGetValue(header.Trim(), out ordinal);
    }

    /// <summary>The first column matching any of <paramref name="aliases"/>, or -1.</summary>
    public int FindOrdinal(params ReadOnlySpan<string> aliases)
    {
        foreach (var alias in aliases)
        {
            if (TryGetOrdinal(alias, out var ordinal))
            {
                return ordinal;
            }
        }

        return -1;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_leaveOpen)
        {
            _reader.Dispose();
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    // A reader whose header cannot be read is disposed before the error goes up: the caller never gets it, so
    // it would otherwise keep the stream it owns (and its file) open.
    private static CsvReader Initialized(CsvReader reader)
    {
        try
        {
            reader.Initialize();
            return reader;
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    private static async Task<CsvReader> InitializedAsync(CsvReader reader, CancellationToken cancellationToken)
    {
        try
        {
            await reader.InitializeAsync(cancellationToken).ConfigureAwait(false);
            return reader;
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    private static (TextReader Reader, Encoding Encoding) OpenText(Stream stream, CsvReaderOptions options, bool leaveOpen, Func<byte[], int> readSample)
    {
        if (options.Encoding is not null)
        {
            return (new StreamReader(stream, options.Encoding, detectEncodingFromByteOrderMarks: false, ChunkSize, leaveOpen), options.Encoding);
        }

        var sample = new byte[Math.Max(4, options.EncodingSampleSize)];
        var count = readSample(sample);
        var detected = TextEncodingDetector.Detect(sample.AsSpan(0, count), options.FallbackEncoding, isComplete: count < sample.Length);
        var rest = new PrefixedReadStream(sample, detected.PreambleLength, count - detected.PreambleLength, stream, leaveOpen);
        return (new StreamReader(rest, detected.Encoding, detectEncodingFromByteOrderMarks: false, ChunkSize, leaveOpen: false), detected.Encoding);
    }

    private void BeginRecord()
    {
        if (_pendingReset)
        {
            _parser.Reset();
            _pendingReset = false;
        }
    }

    private void Fill(int read)
    {
        _position = 0;
        _length = read;
        _endOfInput = read == 0;
    }

    // One step of Read: true when the call can return (hasRecord tells whether a record is ready).
    private bool TryAdvance(out bool hasRecord)
    {
        if (_position < _length)
        {
            var (consumed, ready) = _parser.Consume(_buffer.AsSpan(_position, _length - _position));
            _position += consumed;
            if (!ready)
            {
                hasRecord = false;
                return false;
            }
        }
        else if (!_endOfInput || !_parser.Finish())
        {
            hasRecord = false;
            return _endOfInput;
        }

        CompleteRecord();
        hasRecord = true;
        return true;
    }

    private void CompleteRecord()
    {
        RecordNumber++;
        if (_cache.Length < FieldCount)
        {
            _cache = new string?[Math.Max(FieldCount, _cache.Length * 2)];
        }
        else
        {
            Array.Clear(_cache, 0, FieldCount);
        }

        _pendingReset = true;
    }

    private void Initialize()
    {
        FillForDetection(() => _reader.Read(_buffer, _length, _buffer.Length - _length));
        CreateParser();
        if (_options.HasHeader && Read())
        {
            ReadHeader();
        }
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        while (NeedsMoreForDetection())
        {
            GrowIfFull();
            var n = await _reader.ReadAsync(_buffer.AsMemory(_length), cancellationToken).ConfigureAwait(false);
            AppendFill(n);
        }

        CreateParser();
        if (_options.HasHeader && await ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ReadHeader();
        }
    }

    private void FillForDetection(Func<int> read)
    {
        while (NeedsMoreForDetection())
        {
            GrowIfFull();
            AppendFill(read());
        }
    }

    // The first line must be in the buffer to detect the delimiter; the header row decides it.
    private bool NeedsMoreForDetection() =>
        _options.Delimiter is null && !_endOfInput && _buffer.AsSpan(0, _length).IndexOfAny('\r', '\n') < 0
        && _length < _options.MaxRecordLength;

    private void GrowIfFull()
    {
        if (_length == _buffer.Length)
        {
            Array.Resize(ref _buffer, _buffer.Length * 2);
        }
    }

    private void AppendFill(int read)
    {
        _length += read;
        _endOfInput = read == 0;
    }

    private void CreateParser()
    {
        Delimiter = _options.Delimiter ?? CsvDelimiterDetector.Detect(_buffer.AsSpan(0, _length), _options);
        _parser = new CsvRecordParser(Delimiter, _options.Quoting == CsvQuoting.Rfc4180, _options.Quote, _options.SkipEmptyLines, _options.MaxRecordLength);
    }

    private void ReadHeader()
    {
        var headers = GetFields();
        if (headers.Length > 0 && headers[0].StartsWith('﻿'))
        {
            headers[0] = headers[0][1..];
        }

        Headers = headers;
        _ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length; i++)
        {
            _ordinals.TryAdd(headers[i].Trim(), i);
        }

        RecordNumber = 0;
    }
}
