// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Csv;

/// <summary>
/// The CSV state machine. It consumes characters chunk by chunk (a chunk boundary may fall anywhere,
/// even inside a quoted field or between CR and LF) and stores the current record as one character
/// array plus field boundaries, so no string is allocated until a field is asked for.
/// </summary>
internal sealed class CsvRecordParser(char delimiter, bool quoting, char quote, bool skipEmptyLines, int maxRecordLength)
{
    private enum State
    {
        FieldStart,
        Unquoted,
        Quoted,
        QuoteInQuoted,
    }

    private char[] _record = new char[256];
    private int _length;
    private int _fieldStart;
    private readonly List<(int Start, int End, bool Quoted)> _fields = new(32);

    private State _state;
    private bool _started;
    private bool _fieldQuoted;
    private bool _previousWasCr;
    private bool _skipNextLf;
    private long _line = 1;

    /// <summary>The 1-based line on which the last completed record starts.</summary>
    public long RecordLine { get; private set; } = 1;

    /// <summary>The number of fields of the last completed record.</summary>
    public int FieldCount { get; private set; }

    /// <summary>The field at <paramref name="index"/> of the last completed record.</summary>
    public ReadOnlySpan<char> Field(int index, bool trim)
    {
        var (start, end, quoted) = _fields[index];
        var span = _record.AsSpan(start, end - start);
        return trim && !quoted ? span.Trim() : span;
    }

    /// <summary>
    /// Consumes characters from <paramref name="chunk"/> until a record completes. Returns the number of
    /// characters consumed and whether a record is ready; when no record is ready, the whole chunk was
    /// consumed and more input is needed.
    /// </summary>
    public (int Consumed, bool RecordReady) Consume(ReadOnlySpan<char> chunk)
    {
        if (_fields.Count == 0 && !_started)
        {
            RecordLine = _line;
        }

        var i = 0;
        while (i < chunk.Length)
        {
            if (_skipNextLf)
            {
                _skipNextLf = false;
                if (chunk[i] == '\n')
                {
                    _previousWasCr = false;
                    i++;
                    continue;
                }
            }

            var ready = _state switch
            {
                State.FieldStart => StepFieldStart(chunk[i], ref i),
                State.Unquoted => StepUnquoted(chunk, ref i),
                State.Quoted => StepQuoted(chunk, ref i),
                _ => StepAfterQuote(chunk[i], ref i),
            };
            if (ready)
            {
                return (i, true);
            }
        }

        return (chunk.Length, false);
    }

    private bool StepFieldStart(char c, ref int i)
    {
        if (quoting && c == quote)
        {
            _state = State.Quoted;
            _fieldQuoted = true;
            _started = true;
            _previousWasCr = false;
            i++;
            return false;
        }

        _state = State.Unquoted;
        return false;
    }

    private bool StepUnquoted(ReadOnlySpan<char> chunk, ref int i)
    {
        var run = chunk[i..].IndexOfAny(delimiter, '\r', '\n');
        if (run != 0)
        {
            var count = run < 0 ? chunk.Length - i : run;
            Append(chunk.Slice(i, count));
            _started = true;
            _previousWasCr = false;
            i += count;
            return false;
        }

        var c = chunk[i++];
        if (c != delimiter)
        {
            return EndLine(c);
        }

        _previousWasCr = false;
        EndField();
        _started = true;
        _state = State.FieldStart;
        return false;
    }

    private bool StepQuoted(ReadOnlySpan<char> chunk, ref int i)
    {
        var run = chunk[i..].IndexOfAny(quote, '\r', '\n');
        if (run != 0)
        {
            var count = run < 0 ? chunk.Length - i : run;
            Append(chunk.Slice(i, count));
            _previousWasCr = false;
            i += count;
            return false;
        }

        var c = chunk[i++];
        if (c == quote)
        {
            _state = State.QuoteInQuoted;
            _previousWasCr = false;
            return false;
        }

        CountLineBreak(c);
        Append(c);
        return false;
    }

    private bool StepAfterQuote(char c, ref int i)
    {
        i++;
        if (c is '\r' or '\n')
        {
            return EndLine(c);
        }

        _previousWasCr = false;
        if (c == quote)
        {
            Append(quote);
            _state = State.Quoted;
        }
        else if (c == delimiter)
        {
            EndField();
            _state = State.FieldStart;
        }
        else
        {
            // Lenient: text after the closing quote stays part of the field.
            Append(c);
            _state = State.Unquoted;
        }

        return false;
    }

    /// <summary>Completes the last record at the end of input. Returns false when nothing was pending.</summary>
    public bool Finish()
    {
        if (_state == State.Quoted)
        {
            throw new CsvFormatException("Unterminated quoted field", RecordLine);
        }

        if (!_started && _fields.Count == 0)
        {
            return false;
        }

        EndField();
        CompleteRecord();
        return true;
    }

    /// <summary>Clears the completed record before the next one is consumed.</summary>
    public void Reset()
    {
        _length = 0;
        _fieldStart = 0;
        _fields.Clear();
        _started = false;
        _fieldQuoted = false;
        _state = State.FieldStart;
        RecordLine = _line;
    }

    private bool EndLine(char c)
    {
        CountLineBreak(c);
        if (c == '\r')
        {
            _skipNextLf = true;
        }

        if (!_started && _fields.Count == 0 && skipEmptyLines)
        {
            Reset();
            return false;
        }

        EndField();
        CompleteRecord();
        return true;
    }

    private void CountLineBreak(char c)
    {
        if (c == '\r')
        {
            _line++;
            _previousWasCr = true;
        }
        else if (c == '\n')
        {
            if (!_previousWasCr)
            {
                _line++;
            }

            _previousWasCr = false;
        }
    }

    private void CompleteRecord()
    {
        FieldCount = _fields.Count;
        _state = State.FieldStart;
    }

    private void EndField()
    {
        _fields.Add((_fieldStart, _length, _fieldQuoted));
        Bound((long)_length + _fields.Count - 1);
        _fieldStart = _length;
        _fieldQuoted = false;
    }

    private void Append(char c)
    {
        EnsureCapacity(1);
        _record[_length++] = c;
    }

    private void Append(ReadOnlySpan<char> chars)
    {
        EnsureCapacity(chars.Length);
        chars.CopyTo(_record.AsSpan(_length));
        _length += chars.Length;
    }

    // The characters of the record: the field text and one delimiter between two fields, so that a record
    // made of delimiters alone is bounded too.
    private void Bound(long characters)
    {
        if (characters > maxRecordLength)
        {
            throw new CsvFormatException($"Record longer than {maxRecordLength} characters", RecordLine);
        }
    }

    private void EnsureCapacity(int extra)
    {
        Bound((long)_length + extra + _fields.Count);
        var needed = _length + extra;
        if (needed > _record.Length)
        {
            var size = Math.Max(needed, (int)Math.Min((long)_record.Length * 2, int.MaxValue));
            Array.Resize(ref _record, size);
        }
    }
}
