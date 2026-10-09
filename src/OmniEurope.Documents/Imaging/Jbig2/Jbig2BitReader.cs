// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jbig2;

/// <summary>Bit access to the Huffman coded parts of JBIG2 data, most significant bit first, within a byte range.</summary>
internal sealed class Jbig2BitReader(byte[] data, int start, int end)
{
    private long _bit = (long)start * 8;

    public byte[] Data => data;

    /// <summary>The byte the next bit is read from (after <see cref="AlignToByte"/>, the next whole byte).</summary>
    public int BytePosition => (int)(_bit >> 3);

    public int End => end;

    public int ReadBit()
    {
        if (_bit >= (long)end * 8)
        {
            throw new InvalidDataException("JBIG2 Huffman data ended early.");
        }

        var value = (data[_bit >> 3] >> (7 - (int)(_bit & 7))) & 1;
        _bit++;
        return value;
    }

    /// <summary>Reads <paramref name="count"/> bits (up to 32) as an unsigned number.</summary>
    public long ReadBits(int count)
    {
        long value = 0;
        for (var i = 0; i < count; i++)
        {
            value = (value << 1) | (uint)ReadBit();
        }

        return value;
    }

    public void AlignToByte() => _bit = (_bit + 7) & ~7L;

    /// <summary>Moves to byte <paramref name="position"/>.</summary>
    public void Seek(int position) => _bit = (long)position * 8;
}
