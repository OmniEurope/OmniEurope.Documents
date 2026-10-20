// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging;

/// <summary>Parameters of CCITT fax data (the PDF CCITTFaxDecode filter and TIFF compressions 2, 3 and 4).</summary>
public sealed record CcittOptions
{
    /// <summary>Coding: negative for Group 4 (2-D only), 0 for Group 3 one-dimensional, positive for Group 3
    /// mixed one- and two-dimensional (a tag bit after each end-of-line says which).</summary>
    public int K { get; init; }

    /// <summary>Pixels per row. Default 1728.</summary>
    public int Columns { get; init; } = 1728;

    /// <summary>Rows, or 0 to decode until the data or the end-of-block code ends.</summary>
    public int Rows { get; init; }

    /// <summary>Each row (Group 4) or each end-of-line (Group 3) starts on a byte boundary.</summary>
    public bool EncodedByteAlign { get; init; }

    /// <summary>TIFF compression 2: one-dimensional rows without end-of-line codes, each starting on a byte.</summary>
    public bool ByteAlignedRowsWithoutEol { get; init; }

    /// <summary>True when 1 bits mean black in the output (PDF BlackIs1, TIFF WhiteIsZero); false gives 0 = black.</summary>
    public bool BlackIs1 { get; init; }
}

/// <summary>Decodes CCITT Group 3 and Group 4 fax data into packed 1-bit rows (most significant bit first).</summary>
public static class CcittFaxDecoder
{
    /// <summary>Decodes; <paramref name="complete"/> is false when the data ended early or broke off in a
    /// row (the rows not decoded are white).</summary>
    /// <returns>The rows, <c>(Columns + 7) / 8</c> bytes each.</returns>
    public static byte[] Decode(ReadOnlySpan<byte> data, CcittOptions options, out int rows, out bool complete)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Columns is < 1 or > 65536)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Columns must be between 1 and 65536.");
        }

        // The declared size is bounded before the output is allocated, and rows read from the data (Rows = 0) stop
        // at the same limit: the output, whose rows are whole bytes, holds at most MaxPixels / 8 bytes.
        var sink = new FaxRows(options);
        if (options.Rows > sink.MaxRows)
        {
            throw new InvalidDataException($"A fax image of {options.Columns} by {options.Rows} pixels is out of range.");
        }

        var reader = new CcittBitReader(data.ToArray());
        var reference = new[] { options.Columns, options.Columns };
        complete = true;

        // Each row is painted into the output as soon as it is read, so only the row above is kept.
        try
        {
            while (options.Rows == 0 || sink.Count < options.Rows)
            {
                if (sink.Count >= sink.MaxRows)
                {
                    throw new InvalidDataException("The fax data holds more rows than the pixel limit allows.");
                }

                var changes = ReadRow(reader, options, reference, out var endOfData);
                if (endOfData)
                {
                    complete = options.Rows == 0 || sink.Count >= options.Rows;
                    break;
                }

                sink.Add(changes);
                reference = changes;
            }
        }
        catch (InvalidDataException) when (sink.Count > 0)
        {
            complete = false;
        }

        rows = options.Rows > 0 ? options.Rows : sink.Count;
        return sink.ToArray(rows);
    }

    private static int[] ReadRow(CcittBitReader reader, CcittOptions options, int[] reference, out bool endOfData)
    {
        var twoDimensional = options.K < 0;
        var ended = options.K >= 0 && !options.ByteAlignedRowsWithoutEol
            ? !TryStartGroup3Row(reader, options, ref twoDimensional)
            : options.K < 0 && AtGroup4End(reader, options);
        endOfData = ended || reader.AtEnd;
        if (endOfData)
        {
            return [];
        }

        var changes = twoDimensional ? CcittRows.Read2D(reader, options.Columns, reference) : CcittRows.Read1D(reader, options.Columns);
        if (options.ByteAlignedRowsWithoutEol)
        {
            reader.AlignToByte();
        }

        return changes;
    }

    // Group 3: skips the end-of-line codes before a row and reads its 1-D/2-D tag bit; false at the end of the data.
    private static bool TryStartGroup3Row(CcittBitReader reader, CcittOptions options, ref bool twoDimensional)
    {
        if (options.EncodedByteAlign)
        {
            reader.AlignForEol();
        }

        var eols = reader.SkipEols();

        // In mixed coding the return-to-control sequence is EOL+1 repeated: a tag bit then another EOL.
        if (options.K > 0 && eols == 1 && reader.Peek(13) == 0x1001)
        {
            eols = 2;
        }

        if (eols >= 2 || reader.AtEnd)
        {
            return false;
        }

        if (options.K > 0)
        {
            twoDimensional = reader.ReadBit() == 0;
        }

        return true;
    }

    // Group 4: rows may start on a byte boundary; the end-of-facsimile-block code ends the data.
    private static bool AtGroup4End(CcittBitReader reader, CcittOptions options)
    {
        if (options.EncodedByteAlign)
        {
            reader.AlignToByte();
        }

        return reader.AtEnd || reader.PeekEndOfBlock();
    }

    // The packed rows under construction: the declared rows are allocated white at once, rows read from the data make
    // the buffer grow by doubling, never beyond MaxRows.
    private sealed class FaxRows
    {
        private const int FirstRows = 16;

        private readonly int _columns;
        private readonly int _stride;
        private readonly bool _blackIs1;
        private readonly byte _white;
        private byte[] _output;

        public FaxRows(CcittOptions options)
        {
            _columns = options.Columns;
            _stride = (options.Columns + 7) / 8;
            _blackIs1 = options.BlackIs1;
            _white = options.BlackIs1 ? (byte)0 : (byte)0xFF;
            MaxRows = (int)(RasterImage.MaxPixels / 8 / _stride);
            _output = [];
            if (options.Rows <= MaxRows)
            {
                _output = White(options.Rows > 0 ? options.Rows : FirstRows);
            }
        }

        /// <summary>The most rows the output may hold: its size in bytes stays within MaxPixels / 8, which also keeps
        /// rows times columns within MaxPixels.</summary>
        public int MaxRows { get; }

        public int Count { get; private set; }

        public void Add(int[] changes)
        {
            if ((Count + 1L) * _stride > _output.Length)
            {
                var rows = (int)Math.Min(MaxRows, Math.Max(FirstRows, (long)_output.Length / _stride * 2));
                var grown = White(rows);
                Buffer.BlockCopy(_output, 0, grown, 0, _output.Length);
                _output = grown;
            }

            Paint(changes, Count * _stride);
            Count++;
        }

        /// <summary>The first <paramref name="rows"/> rows, the ones not read white.</summary>
        public byte[] ToArray(int rows) => _output.Length == rows * _stride ? _output : _output[..(rows * _stride)];

        private byte[] White(int rows)
        {
            var bytes = new byte[(long)rows * _stride];
            Array.Fill(bytes, _white);
            return bytes;
        }

        // Pairs of changing elements bound the black runs.
        private void Paint(int[] changes, int offset)
        {
            for (var i = 0; i + 1 < changes.Length; i += 2)
            {
                var end = Math.Min(changes[i + 1], _columns);
                for (var x = changes[i]; x < end; x++)
                {
                    var index = offset + (x >> 3);
                    var mask = (byte)(0x80 >> (x & 7));
                    _output[index] = _blackIs1 ? (byte)(_output[index] | mask) : (byte)(_output[index] & ~mask);
                }
            }
        }
    }
}

/// <summary>Bit access to fax data, most significant bit first.</summary>
internal sealed class CcittBitReader(byte[] data)
{
    private long _bit;

    public bool AtEnd => _bit >= (long)data.Length * 8;

    /// <summary>The number of bits read or skipped so far.</summary>
    public long BitPosition => _bit;

    public int ReadBit()
    {
        if (AtEnd)
        {
            throw new InvalidDataException("CCITT data ended in the middle of a row.");
        }

        var value = (data[_bit >> 3] >> (7 - (int)(_bit & 7))) & 1;
        _bit++;
        return value;
    }

    public int Peek(int count)
    {
        var value = 0;
        for (var i = 0; i < count; i++)
        {
            var position = _bit + i;
            var bit = position < (long)data.Length * 8 ? (data[position >> 3] >> (7 - (int)(position & 7))) & 1 : 0;
            value = (value << 1) | bit;
        }

        return value;
    }

    public void Skip(int count) => _bit += count;

    public void AlignToByte() => _bit = (_bit + 7) & ~7L;

    /// <summary>Byte-aligned EOLs end on a byte boundary: skip the fill zeros so the EOL's last bit ends one.</summary>
    public void AlignForEol()
    {
        while (!AtEnd && Peek(12) == 0 && ((_bit + 12) & 7) != 0)
        {
            _bit++;
        }
    }

    /// <summary>Skips fill zeros and end-of-line codes; returns how many EOLs were skipped.</summary>
    public int SkipEols()
    {
        var count = 0;
        while (!AtEnd)
        {
            var zeros = 0;
            while (!AtEnd && Peek(1) == 0 && zeros < 64)
            {
                _bit++;
                zeros++;
            }

            if (zeros >= 11 && !AtEnd && Peek(1) == 1)
            {
                _bit++;
                count++;
                continue;
            }

            _bit -= zeros;
            break;
        }

        return count;
    }

    public bool PeekEndOfBlock() => Peek(24) == 0x001001;
}
