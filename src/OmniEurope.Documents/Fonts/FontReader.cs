// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;

namespace OmniEurope.Documents.Fonts;

/// <summary>Big-endian reads with bounds checks over font data (a malformed font throws, never overruns).</summary>
internal readonly struct FontReader(byte[] data)
{
    public byte[] Data { get; } = data;

    public int Length => Data.Length;

    public byte U8(int offset) => offset >= 0 && offset < Data.Length ? Data[offset] : throw Bad(offset);

    public ushort U16(int offset) => Has(offset, 2) ? BinaryPrimitives.ReadUInt16BigEndian(Data.AsSpan(offset)) : throw Bad(offset);

    public short S16(int offset) => (short)U16(offset);

    public uint U32(int offset) => Has(offset, 4) ? BinaryPrimitives.ReadUInt32BigEndian(Data.AsSpan(offset)) : throw Bad(offset);

    public int S32(int offset) => (int)U32(offset);

    public string Tag(int offset) => System.Text.Encoding.ASCII.GetString(Slice(offset, 4));

    public ReadOnlySpan<byte> Slice(int offset, int length) => Has(offset, length) ? Data.AsSpan(offset, length) : throw Bad(offset);

    public bool Has(int offset, int length) => offset >= 0 && length >= 0 && (long)offset + length <= Data.Length;

    private static InvalidDataException Bad(int offset) => new($"Font data is truncated or corrupt (offset {offset}).");
}

/// <summary>The location of one table in the font file.</summary>
internal readonly record struct FontTable(int Offset, int Length);
