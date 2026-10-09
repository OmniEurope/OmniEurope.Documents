// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jbig2;

/// <summary>
/// A JBIG2 Huffman table (T.88 annex B): lines of a prefix length, a range length and the lowest value of the
/// range, possibly a lower range line (values below, counted down), an upper range line (values above) and an
/// out-of-band line. Prefix codes are assigned from the prefix lengths (B.3); the fifteen standard tables (B.5)
/// and table segments (7.4.13) give the lines.
/// </summary>
internal sealed class Jbig2HuffmanTable
{
    // Prefix (length << 32 | code) to the line it starts.
    private readonly Dictionary<long, Jbig2HuffmanLine> _codes = [];
    private readonly int _longest;

    /// <summary>Builds a table from its lines, in table order: value lines then the lower, upper and out-of-band lines.</summary>
    public Jbig2HuffmanTable(IReadOnlyList<Jbig2HuffmanLine> lines)
    {
        var lengths = lines.Select(l => l.PrefixLength).ToArray();
        var codes = AssignCodes(lengths);
        for (var i = 0; i < lines.Count; i++)
        {
            if (lengths[i] > 0)
            {
                _codes[((long)lengths[i] << 32) | codes[i]] = lines[i];
            }
        }

        _longest = lengths.Length == 0 ? 0 : lengths.Max();
    }

    /// <summary>The prefix codes of the given lengths (T.88 B.3): shorter codes first, table order within a length.</summary>
    public static uint[] AssignCodes(IReadOnlyList<int> lengths)
    {
        var longest = lengths.Count == 0 ? 0 : lengths.Max();
        var count = new int[longest + 1];
        foreach (var length in lengths)
        {
            count[length]++;
        }

        count[0] = 0;
        var codes = new uint[lengths.Count];
        uint first = 0;
        for (var length = 1; length <= longest; length++)
        {
            first = (first + (uint)count[length - 1]) << 1;
            var next = first;
            for (var i = 0; i < lengths.Count; i++)
            {
                if (lengths[i] == length)
                {
                    codes[i] = next++;
                }
            }
        }

        return codes;
    }

    /// <summary>Decodes the next value, or null for the out-of-band value.</summary>
    public long? Decode(Jbig2BitReader reader)
    {
        long code = 0;
        for (var length = 1; length <= _longest; length++)
        {
            code = (code << 1) | (uint)reader.ReadBit();
            if (_codes.TryGetValue(((long)length << 32) | code, out var line))
            {
                return line.Kind switch
                {
                    Jbig2LineKind.OutOfBand => null,
                    Jbig2LineKind.Lower => line.Low - reader.ReadBits(32),
                    Jbig2LineKind.Upper => line.Low + reader.ReadBits(32),
                    _ => line.Low + reader.ReadBits(line.RangeLength),
                };
            }
        }

        throw new InvalidDataException("A JBIG2 Huffman code matches no table line.");
    }

    /// <summary>Decodes a value that may not be out of band.</summary>
    public int DecodeValue(Jbig2BitReader reader) =>
        (int)(Decode(reader) ?? throw new InvalidDataException("A JBIG2 Huffman value is out of band where a number is required."));

    /// <summary>A table segment (T.88 7.4.13).</summary>
    public static Jbig2HuffmanTable Parse(byte[] data, int start, int end)
    {
        if (end - start < 9)
        {
            throw new InvalidDataException("A JBIG2 table segment is too short.");
        }

        var flags = data[start];
        var prefixBits = ((flags >> 1) & 7) + 1;
        var rangeBits = ((flags >> 4) & 7) + 1;
        var low = Jbig2Bytes.Int32(data, start + 1);
        var high = Jbig2Bytes.Int32(data, start + 5);
        var reader = new Jbig2BitReader(data, start + 9, end);
        var lines = new List<Jbig2HuffmanLine>();
        long current = low;
        while (current < high)
        {
            var prefix = (int)reader.ReadBits(prefixBits);
            var range = (int)reader.ReadBits(rangeBits);
            lines.Add(new Jbig2HuffmanLine(current, prefix, range));
            current += 1L << Math.Min(range, 40);
        }

        lines.Add(new Jbig2HuffmanLine((long)low - 1, (int)reader.ReadBits(prefixBits), 32, Jbig2LineKind.Lower));
        lines.Add(new Jbig2HuffmanLine(high, (int)reader.ReadBits(prefixBits), 32, Jbig2LineKind.Upper));
        if ((flags & 1) != 0)
        {
            lines.Add(new Jbig2HuffmanLine(0, (int)reader.ReadBits(prefixBits), 0, Jbig2LineKind.OutOfBand));
        }

        return new Jbig2HuffmanTable(lines);
    }

    /// <summary>Standard table B.1 to B.15 (<paramref name="number"/> 1 to 15).</summary>
    public static Jbig2HuffmanTable Standard(int number) => StandardTables[number - 1].Value;

    private static readonly Lazy<Jbig2HuffmanTable>[] StandardTables = Enumerable.Range(1, 15)
        .Select(n => new Lazy<Jbig2HuffmanTable>(() => new Jbig2HuffmanTable(StandardLines(n)))).ToArray();

    // Each standard table as (low, prefix length, range length) value lines, then its lower and upper range lines
    // (low, prefix length; 0 prefix length for none) and its out-of-band prefix length (0 for none).
    private static readonly int[][] StandardValues =
    [
        [0, 1, 4, 16, 2, 8, 272, 3, 16],
        [0, 1, 0, 1, 2, 0, 2, 3, 0, 3, 4, 3, 11, 5, 6],
        [-256, 8, 8, 0, 1, 0, 1, 2, 0, 2, 3, 0, 3, 4, 3, 11, 5, 6],
        [1, 1, 0, 2, 2, 0, 3, 3, 0, 4, 4, 3, 12, 5, 6],
        [-255, 7, 8, 1, 1, 0, 2, 2, 0, 3, 3, 0, 4, 4, 3, 12, 5, 6],
        [-2048, 5, 10, -1024, 4, 9, -512, 4, 8, -256, 4, 7, -128, 5, 6, -64, 5, 5, -32, 4, 5, 0, 2, 7, 128, 3, 7, 256, 3, 8,
            512, 4, 9, 1024, 4, 10],
        [-1024, 4, 9, -512, 3, 8, -256, 4, 7, -128, 5, 6, -64, 5, 5, -32, 4, 5, 0, 4, 5, 32, 5, 5, 64, 5, 6, 128, 4, 7,
            256, 3, 8, 512, 3, 9, 1024, 3, 10],
        [-15, 8, 3, -7, 9, 1, -5, 8, 1, -3, 9, 0, -2, 7, 0, -1, 4, 0, 0, 2, 1, 2, 5, 0, 3, 6, 0, 4, 3, 4, 20, 6, 1, 22, 4, 4,
            38, 4, 5, 70, 5, 6, 134, 5, 7, 262, 6, 7, 390, 7, 8, 646, 6, 10],
        [-31, 8, 4, -15, 9, 2, -11, 8, 2, -7, 9, 1, -5, 7, 1, -3, 4, 1, -1, 3, 1, 1, 3, 1, 3, 5, 1, 5, 6, 1, 7, 3, 5,
            39, 6, 2, 43, 4, 5, 75, 4, 6, 139, 5, 7, 267, 5, 8, 523, 6, 8, 779, 7, 9, 1291, 6, 11],
        [-21, 7, 4, -5, 8, 0, -4, 7, 0, -3, 5, 0, -2, 2, 2, 2, 5, 0, 3, 6, 0, 4, 7, 0, 5, 8, 0, 6, 2, 6, 70, 5, 5, 102, 6, 5,
            134, 6, 6, 198, 6, 7, 326, 6, 8, 582, 6, 9, 1094, 6, 10, 2118, 7, 11],
        [1, 1, 0, 2, 2, 1, 4, 4, 0, 5, 4, 1, 7, 5, 1, 9, 5, 2, 13, 6, 2, 17, 7, 2, 21, 7, 3, 29, 7, 4, 45, 7, 5, 77, 7, 6],
        [1, 1, 0, 2, 2, 0, 3, 3, 1, 5, 5, 0, 6, 5, 1, 8, 6, 1, 10, 7, 0, 11, 7, 1, 13, 7, 2, 17, 7, 3, 25, 7, 4, 41, 8, 5],
        [1, 1, 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 4, 1, 7, 3, 3, 15, 6, 1, 17, 6, 2, 21, 6, 3, 29, 6, 4, 45, 6, 5, 77, 7, 6],
        [-2, 3, 0, -1, 3, 0, 0, 1, 0, 1, 3, 0, 2, 3, 0],
        [-24, 7, 4, -8, 6, 2, -4, 5, 1, -2, 4, 0, -1, 3, 0, 0, 1, 0, 1, 3, 0, 2, 4, 0, 3, 5, 1, 5, 6, 2, 9, 7, 4],
    ];

    // Lower low, lower prefix, upper low, upper prefix, out-of-band prefix.
    private static readonly int[][] StandardEnds =
    [
        [0, 0, 65808, 3, 0],
        [0, 0, 75, 6, 6],
        [-257, 8, 75, 7, 6],
        [0, 0, 76, 5, 0],
        [-256, 7, 76, 6, 0],
        [-2049, 6, 2048, 6, 0],
        [-1025, 5, 2048, 5, 0],
        [-16, 9, 1670, 9, 2],
        [-32, 9, 3339, 9, 2],
        [-22, 8, 4166, 8, 2],
        [0, 0, 141, 7, 0],
        [0, 0, 73, 8, 0],
        [0, 0, 141, 7, 0],
        [0, 0, 0, 0, 0],
        [-25, 7, 25, 7, 0],
    ];

    private static List<Jbig2HuffmanLine> StandardLines(int number)
    {
        var values = StandardValues[number - 1];
        var ends = StandardEnds[number - 1];
        var lines = new List<Jbig2HuffmanLine>();
        for (var i = 0; i < values.Length; i += 3)
        {
            lines.Add(new Jbig2HuffmanLine(values[i], values[i + 1], values[i + 2]));
        }

        if (ends[1] > 0)
        {
            lines.Add(new Jbig2HuffmanLine(ends[0], ends[1], 32, Jbig2LineKind.Lower));
        }

        if (ends[3] > 0)
        {
            lines.Add(new Jbig2HuffmanLine(ends[2], ends[3], 32, Jbig2LineKind.Upper));
        }

        if (ends[4] > 0)
        {
            lines.Add(new Jbig2HuffmanLine(0, ends[4], 0, Jbig2LineKind.OutOfBand));
        }

        return lines;
    }
}

internal enum Jbig2LineKind
{
    Value,
    Lower,
    Upper,
    OutOfBand,
}

/// <summary>One line of a JBIG2 Huffman table.</summary>
internal readonly record struct Jbig2HuffmanLine(long Low, int PrefixLength, int RangeLength, Jbig2LineKind Kind = Jbig2LineKind.Value);

/// <summary>Big-endian numbers of JBIG2 segments.</summary>
internal static class Jbig2Bytes
{
    public static int Int32(byte[] data, int at) =>
        Check(data, at, 4) ? (data[at] << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3] : 0;

    public static int UInt16(byte[] data, int at) => Check(data, at, 2) ? (data[at] << 8) | data[at + 1] : 0;

    public static int Byte(byte[] data, int at) => Check(data, at, 1) ? data[at] : 0;

    private static bool Check(byte[] data, int at, int count) =>
        at >= 0 && at + count <= data.Length ? true : throw new InvalidDataException("A JBIG2 segment ended early.");
}
