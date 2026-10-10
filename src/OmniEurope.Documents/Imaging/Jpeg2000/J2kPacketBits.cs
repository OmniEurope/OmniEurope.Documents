// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jpeg2000;

/// <summary>
/// The bits of packet headers (ISO/IEC 15444-1 B.10.1): most significant first, a byte after 0xFF holding only seven
/// (its top bit is a stuffed 0). Past the end of the data every bit reads as 1, which ends any tag tree or code the
/// header was decoding, and <see cref="Overrun"/> tells the packet was cut.
/// </summary>
internal sealed class J2kPacketBits(byte[] data, int start, int end)
{
    private int _byte;
    private int _bits;
    private bool _afterFF;

    /// <summary>Where the next unread byte lies.</summary>
    public int Position { get; set; } = start;

    public int End { get; } = end;

    public byte[] Data { get; } = data;

    /// <summary>True once a bit was read past the end of the data.</summary>
    public bool Overrun { get; private set; }

    public int Bit()
    {
        if (_bits == 0)
        {
            if (Position >= End)
            {
                Overrun = true;
                return 1;
            }

            _byte = Data[Position++];
            _bits = _afterFF ? 7 : 8;
            _afterFF = _byte == 0xFF;
        }

        _bits--;
        return (_byte >> _bits) & 1;
    }

    public int Bits(int count)
    {
        var value = 0;
        for (var i = 0; i < count; i++)
        {
            value = (value << 1) | Bit();
        }

        return value;
    }

    /// <summary>Ends a header: the rest of its byte is padding, and a byte after 0xFF still belongs to it.</summary>
    public void Align()
    {
        _bits = 0;
        if (_afterFF)
        {
            Position++;
            _afterFF = false;
        }
    }

    /// <summary>Skips the two-byte marker <paramref name="marker"/> (and <paramref name="length"/> more bytes) when it comes next.</summary>
    public void Skip(int marker, int length)
    {
        if (Position + 1 < End && Data[Position] == marker >> 8 && Data[Position + 1] == (marker & 0xFF))
        {
            Position += 2 + length;
        }
    }
}
