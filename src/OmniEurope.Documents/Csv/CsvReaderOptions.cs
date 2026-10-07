// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Csv;

/// <summary>How a CSV reader treats the quote character.</summary>
public enum CsvQuoting
{
    /// <summary>RFC 4180: a field that starts with the quote character runs to the closing quote and may hold
    /// delimiters and line breaks; a doubled quote inside it is one quote.</summary>
    Rfc4180,

    /// <summary>No quoting: the quote character is ordinary text (French FEC files, tab-separated exports).</summary>
    None,
}

/// <summary>Options of <see cref="CsvReader"/>.</summary>
public sealed class CsvReaderOptions
{
    /// <summary>The field delimiter, or null to detect it from the first line among
    /// <see cref="CandidateDelimiters"/>.</summary>
    public char? Delimiter { get; init; }

    /// <summary>Delimiters considered by detection, in order of preference on a tie.</summary>
    public IReadOnlyList<char> CandidateDelimiters { get; init; } = ['\t', ';', ',', '|'];

    /// <summary>Quote handling. Default <see cref="CsvQuoting.Rfc4180"/>.</summary>
    public CsvQuoting Quoting { get; init; } = CsvQuoting.Rfc4180;

    /// <summary>The quote character. Default <c>"</c>.</summary>
    public char Quote { get; init; } = '"';

    /// <summary>True when the first record holds the column names. Default true.</summary>
    public bool HasHeader { get; init; } = true;

    /// <summary>Encoding of a stream, or null to detect it (byte order mark, then UTF-8 validity, then
    /// <see cref="FallbackEncoding"/>). Ignored when reading from a <see cref="TextReader"/>.</summary>
    public Encoding? Encoding { get; init; }

    /// <summary>Encoding used by detection when the start of the stream is not valid UTF-8; Windows-1252
    /// when null.</summary>
    public Encoding? FallbackEncoding { get; init; }

    /// <summary>Bytes inspected by encoding detection. Default 1 MiB.</summary>
    public int EncodingSampleSize { get; init; } = 1 << 20;

    /// <summary>True to skip lines that hold nothing at all. Default true.</summary>
    public bool SkipEmptyLines { get; init; } = true;

    /// <summary>True to trim white space around each unquoted field. Default false.</summary>
    public bool TrimFields { get; init; }

    /// <summary>Largest record accepted, in characters; a longer one throws <see cref="CsvFormatException"/>
    /// instead of exhausting memory. Default 64 Mi characters.</summary>
    public int MaxRecordLength { get; init; } = 64 << 20;
}

/// <summary>Malformed CSV: an unterminated quoted field or a record longer than the configured limit.</summary>
public sealed class CsvFormatException : FormatException
{
    /// <summary>Creates the exception.</summary>
    public CsvFormatException(string message, long lineNumber)
        : base($"{message} (line {lineNumber})")
    {
        LineNumber = lineNumber;
    }

    /// <summary>The 1-based line where the faulty record starts.</summary>
    public long LineNumber { get; }
}
