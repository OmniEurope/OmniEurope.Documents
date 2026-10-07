// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace OmniEurope.Documents.Tests.Imaging;

/// <summary>
/// PNG files written from the specification: signature, IHDR, optional chunks, one zlib IDAT of the given
/// filtered rows, IEND, each chunk with its CRC-32. Self-contained, so a probe can link it.
/// </summary>
internal static class PngFixture
{
    private static readonly uint[] CrcTable = [.. Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++)
        {
            c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        }

        return c;
    })];

    /// <summary>A PNG; <paramref name="raw"/> is the decompressed data (a filter byte before each row of each pass).</summary>
    public static byte[] Png(int width, int height, byte bitDepth, byte colorType, bool interlaced, byte[] raw, params (string Type, byte[] Body)[] chunks)
    {
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = bitDepth;
        ihdr[9] = colorType;
        ihdr[12] = (byte)(interlaced ? 1 : 0);
        return File([("IHDR", ihdr), .. chunks, ("IDAT", Zlib(raw)), ("IEND", [])]);
    }

    /// <summary>The signature followed by the given chunks, each with its length and CRC.</summary>
    public static byte[] File(params (string Type, byte[] Body)[] chunks)
    {
        var output = new List<byte> { 137, 80, 78, 71, 13, 10, 26, 10 };
        foreach (var (type, body) in chunks)
        {
            var typed = Encoding.ASCII.GetBytes(type).Concat(body).ToArray();
            output.AddRange(BigEndian((uint)body.Length));
            output.AddRange(typed);
            output.AddRange(BigEndian(Crc(typed)));
        }

        return [.. output];
    }

    public static byte[] Zlib(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }

    private static uint Crc(byte[] data)
    {
        var c = 0xFFFFFFFF;
        foreach (var b in data)
        {
            c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        }

        return c ^ 0xFFFFFFFF;
    }

    private static byte[] BigEndian(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }
}
