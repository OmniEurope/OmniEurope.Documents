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

        var lines = new List<int[]>();
        var reader = new CcittBitReader(data.ToArray());
        var reference = new[] { options.Columns, options.Columns };
        complete = true;
        try
        {
            while (options.Rows == 0 || lines.Count < options.Rows)
            {
                var changes = ReadRow(reader, options, reference, out var endOfData);
                if (endOfData)
                {
                    complete = options.Rows == 0 || lines.Count >= options.Rows;
                    break;
                }

                lines.Add(changes);
                reference = changes;
            }
        }
        catch (InvalidDataException) when (lines.Count > 0)
        {
            complete = false;
        }

        rows = options.Rows > 0 ? options.Rows : lines.Count;
        return Pack(lines, rows, options);
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

    private static byte[] Pack(List<int[]> lines, int rows, CcittOptions options)
    {
        var stride = (options.Columns + 7) / 8;
        var output = new byte[(long)stride * rows];
        var white = options.BlackIs1 ? (byte)0 : (byte)0xFF;
        Array.Fill(output, white);
        for (var y = 0; y < Math.Min(rows, lines.Count); y++)
        {
            var changes = lines[y];
            for (var i = 0; i + 1 < changes.Length; i += 2)
            {
                // Pairs of changing elements bound the black runs.
                var start = changes[i];
                var end = Math.Min(changes[i + 1], options.Columns);
                for (var x = start; x < end; x++)
                {
                    var index = (y * stride) + (x >> 3);
                    var mask = (byte)(0x80 >> (x & 7));
                    output[index] = options.BlackIs1 ? (byte)(output[index] | mask) : (byte)(output[index] & ~mask);
                }
            }
        }

        return output;
    }
}

/// <summary>Bit access to fax data, most significant bit first.</summary>
internal sealed class CcittBitReader(byte[] data)
{
    private long _bit;

    public bool AtEnd => _bit >= (long)data.Length * 8;

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
