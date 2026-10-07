// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Internal;

/// <summary>CRC-32 (ISO 3309, polynomial 0xEDB88320 reflected), as used by PNG and ZIP.</summary>
internal static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    /// <summary>Continues a running CRC (start with 0) over <paramref name="data"/>.</summary>
    public static uint Update(uint crc, ReadOnlySpan<byte> data)
    {
        var c = ~crc;
        foreach (var b in data)
        {
            c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
        }

        return ~c;
    }

    public static uint Compute(ReadOnlySpan<byte> data) => Update(0, data);
}
