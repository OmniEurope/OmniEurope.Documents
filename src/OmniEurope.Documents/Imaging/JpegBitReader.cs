// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging;

/// <summary>A canonical Huffman table of a JPEG stream (DHT segment).</summary>
internal sealed class JpegHuffmanTable
{
    private readonly int[] _maxCode = new int[18];
    private readonly int[] _valuePointer = new int[17];
    private readonly int[] _minCode = new int[17];
    private readonly byte[] _values;

    public JpegHuffmanTable(ReadOnlySpan<byte> counts, byte[] values)
    {
        _values = values;
        var code = 0;
        var k = 0;
        for (var length = 1; length <= 16; length++)
        {
            _valuePointer[length] = k;
            _minCode[length] = code;
            code += counts[length - 1];
            k += counts[length - 1];
            _maxCode[length] = counts[length - 1] == 0 ? -1 : code - 1;
            code <<= 1;
        }

        _maxCode[17] = int.MaxValue;
    }

    public int Decode(JpegBitReader reader)
    {
        var code = 0;
        for (var length = 1; length <= 16; length++)
        {
            code = (code << 1) | reader.ReadBit();
            if (code <= _maxCode[length])
            {
                var index = _valuePointer[length] + code - _minCode[length];
                return index < _values.Length ? _values[index] : throw new InvalidDataException("Bad Huffman code in JPEG data.");
            }
        }

        throw new InvalidDataException("Bad Huffman code in JPEG data.");
    }
}

/// <summary>
/// Reads entropy-coded JPEG data bit by bit: a stuffed <c>FF 00</c> is one <c>FF</c> byte, and a marker
/// ends the data (zero bits are returned past it, as decoders conventionally do with truncated scans).
/// </summary>
internal sealed class JpegBitReader(byte[] data, int position)
{
    private int _buffer;
    private int _bits;

    public int Position { get; private set; } = position;

    public bool HitMarker { get; private set; }

    public int ReadBit()
    {
        if (_bits == 0)
        {
            _buffer = NextByte();
            _bits = 8;
        }

        _bits--;
        return (_buffer >> _bits) & 1;
    }

    public int Receive(int length)
    {
        var value = 0;
        for (var i = 0; i < length; i++)
        {
            value = (value << 1) | ReadBit();
        }

        return value;
    }

    /// <summary>Reads <paramref name="length"/> bits and sign-extends them (JPEG "EXTEND").</summary>
    public int ReceiveExtend(int length)
    {
        if (length == 0)
        {
            return 0;
        }

        var value = Receive(length);
        return value < (1 << (length - 1)) ? value - (1 << length) + 1 : value;
    }

    /// <summary>Drops the remaining bits and steps over the next RSTn marker.</summary>
    public void Restart()
    {
        _bits = 0;
        HitMarker = false;
        while (Position + 1 < data.Length)
        {
            if (data[Position] == 0xFF && data[Position + 1] is >= 0xD0 and <= 0xD7)
            {
                Position += 2;
                return;
            }

            if (data[Position] == 0xFF && data[Position + 1] != 0 && data[Position + 1] != 0xFF)
            {
                // Another marker: the restart marker is missing; let the next scan or the end take over.
                return;
            }

            Position++;
        }
    }

    /// <summary>Moves to the first marker at or after the current position (end of the scan data).</summary>
    public int SkipToMarker()
    {
        while (Position + 1 < data.Length && !(data[Position] == 0xFF && data[Position + 1] is not 0x00 and not 0xFF and not (>= 0xD0 and <= 0xD7)))
        {
            Position++;
        }

        return Position;
    }

    private int NextByte()
    {
        if (HitMarker || Position >= data.Length)
        {
            return 0;
        }

        var b = data[Position];
        if (b != 0xFF)
        {
            Position++;
            return b;
        }

        // Fill bytes (FF FF ...) may precede a marker.
        while (Position + 1 < data.Length && data[Position + 1] == 0xFF)
        {
            Position++;
        }

        var next = Position + 1 < data.Length ? data[Position + 1] : 0xD9;
        if (next == 0x00)
        {
            Position += 2;
            return 0xFF;
        }

        HitMarker = true;
        return 0;
    }
}
