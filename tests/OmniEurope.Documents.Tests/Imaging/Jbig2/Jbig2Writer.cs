// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging.Jbig2;

namespace OmniEurope.Documents.Tests.Imaging.Jbig2;

/// <summary>
/// The MQ arithmetic encoder of ITU-T T.88 annex E (E.2), written from the standard to produce the test bitstreams:
/// every fixture of the JBIG2 tests is encoded here, none comes from elsewhere. A context is one byte, its state
/// times two plus its more probable symbol, as the decoder keeps it.
/// </summary>
internal sealed class MqWriter
{
    private static readonly int[] Qe =
    [
        0x5601, 0x3401, 0x1801, 0x0AC1, 0x0521, 0x0221, 0x5601, 0x5401, 0x4801, 0x3801, 0x3001, 0x2401, 0x1C01, 0x1601,
        0x5601, 0x5401, 0x5101, 0x4801, 0x3801, 0x3401, 0x3001, 0x2801, 0x2401, 0x2201, 0x1C01, 0x1801, 0x1601, 0x1401,
        0x1201, 0x1101, 0x0AC1, 0x09C1, 0x08A1, 0x0521, 0x0441, 0x02A1, 0x0221, 0x0141, 0x0111, 0x0085, 0x0049, 0x0025,
        0x0015, 0x0009, 0x0005, 0x0001, 0x5601,
    ];

    private static readonly int[] NextMps =
    [
        1, 2, 3, 4, 5, 38, 7, 8, 9, 10, 11, 12, 13, 29, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31,
        32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 45, 46,
    ];

    private static readonly int[] NextLps =
    [
        1, 6, 9, 12, 29, 33, 6, 14, 14, 14, 17, 18, 20, 21, 14, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 23, 24, 25, 26, 27,
        28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 46,
    ];

    // The byte before the first one (BPST - 1), dropped at the end.
    private readonly List<byte> _out = [0];
    private uint _a = 0x8000;
    private uint _c;
    private int _ct = 12;

    public void Encode(byte[] contexts, int index, int bit)
    {
        var state = contexts[index] >> 1;
        var mps = contexts[index] & 1;
        var qe = (uint)Qe[state];
        _a -= qe;
        if (bit == mps)
        {
            if ((_a & 0x8000) != 0)
            {
                _c += qe;
                return;
            }

            if (_a < qe)
            {
                _a = qe;
            }
            else
            {
                _c += qe;
            }

            state = NextMps[state];
        }
        else
        {
            if (_a < qe)
            {
                _c += qe;
            }
            else
            {
                _a = qe;
            }

            if (state is 0 or 6 or 14)
            {
                mps = 1 - mps;
            }

            state = NextLps[state];
        }

        contexts[index] = (byte)((state << 1) | mps);
        do
        {
            _a <<= 1;
            _c <<= 1;
            _ct--;
            if (_ct == 0)
            {
                ByteOut();
            }
        }
        while ((_a & 0x8000) == 0);
    }

    /// <summary>Flushes (E.2.9) and ends the data with the 0xFF 0xAC marker.</summary>
    public byte[] Finish()
    {
        var temp = _c + _a;
        _c |= 0xFFFF;
        if (_c >= temp)
        {
            _c -= 0x8000;
        }

        _c <<= _ct;
        ByteOut();
        _c <<= _ct;
        ByteOut();
        if (_out[^1] != 0xFF)
        {
            _out.Add(0xFF);
        }

        _out.Add(0xAC);
        if (_out[0] != 0)
        {
            throw new InvalidOperationException("A carry reached the byte before the data.");
        }

        return [.. _out.Skip(1)];
    }

    private void ByteOut()
    {
        if (_out[^1] == 0xFF)
        {
            Emit(20);
            return;
        }

        if (_c < 0x8000000)
        {
            Emit(19);
            return;
        }

        _out[^1]++;
        if (_out[^1] == 0xFF)
        {
            _c &= 0x7FFFFFF;
            Emit(20);
        }
        else
        {
            Emit(19);
        }
    }

    private void Emit(int shift)
    {
        _out.Add((byte)(_c >> shift));
        _c &= shift == 20 ? 0xFFFFFu : 0x7FFFFu;
        _ct = shift == 20 ? 7 : 8;
    }
}

/// <summary>
/// Arithmetic coding of JBIG2 values (T.88 annex A integers and symbol IDs, 6.2 generic regions, 6.3 refinements),
/// each procedure with its own contexts, named as the standard names them.
/// </summary>
internal sealed class ArithmeticWriter
{
    // Template pixels, most significant context bit first; (99, n) stands for adaptive pixel An.
    public static readonly (int X, int Y)[][] GenericTemplates =
    [
        [(99, 4), (-1, -2), (0, -2), (1, -2), (99, 3), (99, 2), (-2, -1), (-1, -1), (0, -1), (1, -1), (2, -1), (99, 1), (-4, 0), (-3, 0), (-2, 0), (-1, 0)],
        [(-1, -2), (0, -2), (1, -2), (2, -2), (-2, -1), (-1, -1), (0, -1), (1, -1), (2, -1), (99, 1), (-3, 0), (-2, 0), (-1, 0)],
        [(-1, -2), (0, -2), (1, -2), (-2, -1), (-1, -1), (0, -1), (1, -1), (99, 1), (-2, 0), (-1, 0)],
        [(-3, -1), (-2, -1), (-1, -1), (0, -1), (1, -1), (99, 1), (-4, 0), (-3, 0), (-2, 0), (-1, 0)],
    ];

    private static readonly int[] GenericPrediction = [0x9B25, 0x0795, 0x00E5, 0x0195];

    private readonly Dictionary<string, byte[]> _contexts = [];

    public MqWriter Mq { get; } = new();

    public byte[] Contexts(string name, int size) => _contexts.TryGetValue(name, out var c) ? c : _contexts[name] = new byte[size];

    /// <summary>An integer (A.2) with the contexts of <paramref name="name"/>; null encodes the out-of-band value.</summary>
    public void Integer(string name, int? value)
    {
        var contexts = Contexts(name, 512);
        var previous = 1;
        void Bit(int bit)
        {
            Mq.Encode(contexts, previous, bit);
            previous = previous < 256 ? (previous << 1) | bit : ((((previous << 1) | bit) & 511) | 256);
        }

        void Bits(long v, int count)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                Bit((int)((v >> i) & 1));
            }
        }

        if (value is not { } number)
        {
            Bit(1);
            Bits(0, 3);
            return;
        }

        Bit(number < 0 ? 1 : 0);
        long magnitude = Math.Abs((long)number);
        (int Prefix, int Length, long Low) range = magnitude switch
        {
            < 4 => (0b0, 2, 0),
            < 20 => (0b10, 4, 4),
            < 84 => (0b110, 6, 20),
            < 340 => (0b1110, 8, 84),
            < 4436 => (0b11110, 12, 340),
            _ => (0b11111, 32, 4436),
        };
        var prefixLength = range.Length switch { 2 => 1, 4 => 2, 6 => 3, 8 => 4, _ => 5 };
        Bits(range.Prefix, prefixLength);
        Bits(magnitude - range.Low, range.Length);
    }

    /// <summary>A symbol ID (A.3) of <paramref name="length"/> bits.</summary>
    public void SymbolId(int value, int length)
    {
        var contexts = Contexts("IAID", 1 << (length + 1));
        var previous = 1;
        for (var i = length - 1; i >= 0; i--)
        {
            var bit = (value >> i) & 1;
            Mq.Encode(contexts, previous, bit);
            previous = (previous << 1) | bit;
        }
    }

    /// <summary>A generic region (6.2) with the contexts of <paramref name="name"/>.</summary>
    public void Generic(string name, Jbig2Bitmap bitmap, int template, (int X, int Y)[] adaptive, bool typicalPrediction, Jbig2Bitmap? skip = null)
    {
        var pixels = GenericTemplates[template].Select(p => p.X == 99 ? adaptive[p.Y - 1] : p).ToArray();
        var contexts = Contexts(name, 1 << 16);
        var predicted = false;
        for (var y = 0; y < bitmap.Height; y++)
        {
            if (typicalPrediction)
            {
                var same = Enumerable.Range(0, bitmap.Width).All(x => bitmap[x, y] == bitmap[x, y - 1]);
                Mq.Encode(contexts, GenericPrediction[template], same == predicted ? 0 : 1);
                predicted = same;
                if (same)
                {
                    continue;
                }
            }

            for (var x = 0; x < bitmap.Width; x++)
            {
                if (skip is null || skip[x, y] == 0)
                {
                    Mq.Encode(contexts, Context(bitmap, x, y, pixels), bitmap[x, y]);
                }
            }
        }
    }

    /// <summary>A refinement (6.3) of <paramref name="reference"/>, pixel (x, y) over reference pixel (x - dx, y - dy).</summary>
    public void Refinement(string name, Jbig2Bitmap bitmap, Jbig2Bitmap reference, int dx, int dy, int template, (int X, int Y)[] adaptive, bool typicalPrediction)
    {
        (int X, int Y)[] coding = template == 0 ? [(0, -1), (1, -1), (-1, 0), adaptive[0]] : [(-1, -1), (0, -1), (1, -1), (-1, 0)];
        (int X, int Y)[] referencePixels = template == 0
            ? [(0, -1), (1, -1), (-1, 0), (0, 0), (1, 0), (-1, 1), (0, 1), (1, 1), adaptive[1]]
            : [(0, -1), (-1, 0), (0, 0), (1, 0), (0, 1), (1, 1)];
        var contexts = Contexts(name, 1 << 13);
        var predicted = false;
        for (var y = 0; y < bitmap.Height; y++)
        {
            var row = y;
            var uniform = Enumerable.Range(0, bitmap.Width).Select(x => Uniform(reference, x - dx, row - dy)).ToArray();
            if (typicalPrediction)
            {
                var typical = Enumerable.Range(0, bitmap.Width).All(x => uniform[x] is not { } v || bitmap[x, row] == v);
                Mq.Encode(contexts, template == 0 ? 0x0020 : 0x0008, typical == predicted ? 0 : 1);
                predicted = typical;
            }

            for (var x = 0; x < bitmap.Width; x++)
            {
                if (predicted && uniform[x] is not null)
                {
                    continue;
                }

                var context = Context(bitmap, x, y, coding);
                foreach (var (px, py) in referencePixels)
                {
                    context = (context << 1) | reference[x - dx + px, y - dy + py];
                }

                Mq.Encode(contexts, context, bitmap[x, y]);
            }
        }
    }

    private static int Context(Jbig2Bitmap bitmap, int x, int y, (int X, int Y)[] pixels)
    {
        var context = 0;
        foreach (var (px, py) in pixels)
        {
            context = (context << 1) | bitmap[x + px, y + py];
        }

        return context;
    }

    private static int? Uniform(Jbig2Bitmap reference, int x, int y)
    {
        var first = reference[x - 1, y - 1];
        for (var j = -1; j <= 1; j++)
        {
            for (var i = -1; i <= 1; i++)
            {
                if (reference[x + i, y + j] != first)
                {
                    return null;
                }
            }
        }

        return first;
    }
}

/// <summary>Bits written most significant first, for the Huffman coded parts.</summary>
internal sealed class BitWriter
{
    private readonly List<byte> _bytes = [];
    private int _bit;

    public void Write(long value, int count)
    {
        for (var i = count - 1; i >= 0; i--)
        {
            if (_bit == 0)
            {
                _bytes.Add(0);
            }

            _bytes[^1] |= (byte)(((value >> i) & 1) << (7 - _bit));
            _bit = (_bit + 1) & 7;
        }
    }

    public void Write(string bits)
    {
        foreach (var c in bits.Where(c => c is '0' or '1'))
        {
            Write(c - '0', 1);
        }
    }

    public void Align() => _bit = 0;

    public void Bytes(byte[] data)
    {
        Align();
        _bytes.AddRange(data);
    }

    public byte[] ToArray() => [.. _bytes];
}

/// <summary>
/// A Huffman table for encoding (T.88 B.2 lines, B.3 code assignment), written here from the standard: lines of
/// (low, prefix length, range length) then optional lower, upper and out-of-band prefix lengths.
/// </summary>
internal sealed class HuffmanWriter
{
    public static readonly HuffmanWriter B1 = new([(0, 1, 4), (16, 2, 8), (272, 3, 16)], null, (65808, 3), 0);
    public static readonly HuffmanWriter B2 = new([(0, 1, 0), (1, 2, 0), (2, 3, 0), (3, 4, 3), (11, 5, 6)], null, (75, 6), 6);
    public static readonly HuffmanWriter B4 = new([(1, 1, 0), (2, 2, 0), (3, 3, 0), (4, 4, 3), (12, 5, 6)], null, (76, 5), 0);
    public static readonly HuffmanWriter B6 = new([(-2048, 5, 10), (-1024, 4, 9), (-512, 4, 8), (-256, 4, 7), (-128, 5, 6), (-64, 5, 5),
        (-32, 4, 5), (0, 2, 7), (128, 3, 7), (256, 3, 8), (512, 4, 9), (1024, 4, 10)], (-2049, 6), (2048, 6), 0);
    public static readonly HuffmanWriter B8 = new([(-15, 8, 3), (-7, 9, 1), (-5, 8, 1), (-3, 9, 0), (-2, 7, 0), (-1, 4, 0), (0, 2, 1), (2, 5, 0),
        (3, 6, 0), (4, 3, 4), (20, 6, 1), (22, 4, 4), (38, 4, 5), (70, 5, 6), (134, 5, 7), (262, 6, 7), (390, 7, 8), (646, 6, 10)], (-16, 9), (1670, 9), 2);
    public static readonly HuffmanWriter B11 = new([(1, 1, 0), (2, 2, 1), (4, 4, 0), (5, 4, 1), (7, 5, 1), (9, 5, 2), (13, 6, 2), (17, 7, 2),
        (21, 7, 3), (29, 7, 4), (45, 7, 5), (77, 7, 6)], null, (141, 7), 0);
    public static readonly HuffmanWriter B15 = new([(-24, 7, 4), (-8, 6, 2), (-4, 5, 1), (-2, 4, 0), (-1, 3, 0), (0, 1, 0), (1, 3, 0), (2, 4, 0),
        (3, 5, 1), (5, 6, 2), (9, 7, 4)], (-25, 7), (25, 7), 0);

    private readonly List<(long Low, long High, int RangeLength, string Code, int Kind)> _lines = [];
    private readonly string? _outOfBand;

    public HuffmanWriter((int Low, int Prefix, int Range)[] lines, (int Low, int Prefix)? lower, (int Low, int Prefix)? upper, int outOfBand)
    {
        var lengths = lines.Select(l => l.Prefix).Concat(lower is { } lo ? [lo.Prefix] : []).Concat(upper is { } up ? [up.Prefix] : [])
            .Concat(outOfBand > 0 ? [outOfBand] : []).ToArray();
        var codes = Codes(lengths);
        var i = 0;
        foreach (var line in lines)
        {
            _lines.Add((line.Low, line.Low + (1L << line.Range) - 1, line.Range, codes[i++], 0));
        }

        if (lower is { } l)
        {
            _lines.Add((long.MinValue, l.Low, 32, codes[i++], -1));
        }

        if (upper is { } u)
        {
            _lines.Add((u.Low, long.MaxValue, 32, codes[i++], 1));
        }

        _outOfBand = outOfBand > 0 ? codes[i] : null;
    }

    /// <summary>Canonical codes from prefix lengths (B.3), as bit strings.</summary>
    public static string[] Codes(int[] lengths)
    {
        var codes = new string[lengths.Length];
        var code = 0;
        for (var length = 1; length <= lengths.DefaultIfEmpty(0).Max(); length++)
        {
            for (var i = 0; i < lengths.Length; i++)
            {
                if (lengths[i] == length)
                {
                    codes[i] = Convert.ToString(code++, 2).PadLeft(length, '0');
                }
            }

            code <<= 1;
        }

        return codes;
    }

    public void Write(BitWriter writer, long? value)
    {
        if (value is not { } v)
        {
            writer.Write(_outOfBand ?? throw new InvalidOperationException("No out-of-band line."));
            return;
        }

        var line = _lines.First(l => v >= l.Low && v <= l.High);
        writer.Write(line.Code);
        writer.Write(line.Kind switch { -1 => line.High - v, 1 => v - line.Low, _ => v - line.Low }, line.RangeLength);
    }
}

/// <summary>JBIG2 segments laid end to end as PDF embeds them (T.88 7.2 headers, annex D.3).</summary>
internal sealed class SegmentWriter(int firstNumber = 0)
{
    private readonly List<byte> _bytes = [];
    private int _next = firstNumber;

    public int Add(int type, byte[] data, int page = 1, int[]? referred = null, bool unknownLength = false)
    {
        referred ??= [];
        var number = _next++;
        Int(number);
        _bytes.Add((byte)(type | (page > 255 ? 0x40 : 0)));
        if (referred.Length <= 4)
        {
            _bytes.Add((byte)(referred.Length << 5));
        }
        else
        {
            Int(unchecked((int)0xE0000000) | referred.Length);
            _bytes.AddRange(new byte[(referred.Length + 8) / 8]);
        }

        foreach (var r in referred)
        {
            if (number <= 256)
            {
                _bytes.Add((byte)r);
            }
            else if (number <= 65536)
            {
                _bytes.Add((byte)(r >> 8));
                _bytes.Add((byte)r);
            }
            else
            {
                Int(r);
            }
        }

        if (page > 255)
        {
            Int(page);
        }
        else
        {
            _bytes.Add((byte)page);
        }

        Int(unknownLength ? -1 : data.Length);
        _bytes.AddRange(data);
        return number;
    }

    public byte[] ToArray() => [.. _bytes];

    private void Int(int value) => _bytes.AddRange([(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]);

    public static byte[] PageInfo(int width, int height, int flags = 0, int striping = 0) =>
        [.. Be(width), .. Be(height), .. Be(0), .. Be(0), (byte)flags, (byte)(striping >> 8), (byte)striping];

    public static byte[] Region(int width, int height, int x = 0, int y = 0, int combination = 0) =>
        [.. Be(width), .. Be(height), .. Be(x), .. Be(y), (byte)combination];

    public static byte[] Be(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    public static byte[] Be16(int value) => [(byte)(value >> 8), (byte)value];

    public static byte[] Adaptive(params (int X, int Y)[] pixels) => [.. pixels.SelectMany(p => new[] { (byte)(sbyte)p.X, (byte)(sbyte)p.Y })];
}
