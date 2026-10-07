// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Tests.Imaging;

/// <summary>
/// GIF89a files written from the specification: a logical screen without a global palette, then one image with
/// a local palette and its LZW codes (variable width, least significant bit first, the width growing when the
/// next free code reaches it, 12 bits at most). Self-contained, so a probe can link it.
/// </summary>
internal static class GifFixture
{
    /// <summary>Four colours: black, red, green, blue.</summary>
    public static readonly byte[] Palette = [0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255];

    /// <summary>A file whose pixels, in stored order, are the given palette indexes (each one a literal code).</summary>
    public static byte[] Image(int width, int height, bool interlaced, IReadOnlyList<int> indexes)
    {
        var codes = Literals(indexes);
        var flags = (byte)(0x80 | (interlaced ? 0x40 : 0) | 1);
        var blocks = new List<byte>();
        foreach (var chunk in codes.Chunk(255))
        {
            blocks.Add((byte)chunk.Length);
            blocks.AddRange(chunk);
        }

        return
        [
            .. "GIF89a"u8, (byte)width, (byte)(width >> 8), (byte)height, (byte)(height >> 8), 0, 0, 0,
            0x2C, 0, 0, 0, 0, (byte)width, (byte)(width >> 8), (byte)height, (byte)(height >> 8), flags, .. Palette,
            2, .. blocks, 0, 0x3B,
        ];
    }

    // Clear, one literal code per index, end of information; minimum code size 2 (clear 4, end 5).
    private static byte[] Literals(IReadOnlyList<int> indexes)
    {
        var output = new List<byte>();
        var buffer = 0L;
        var count = 0;
        var size = 3;
        var next = 6;
        void Write(int code)
        {
            buffer |= (long)code << count;
            count += size;
            while (count >= 8)
            {
                output.Add((byte)buffer);
                buffer >>= 8;
                count -= 8;
            }
        }

        Write(4);
        for (var i = 0; i < indexes.Count; i++)
        {
            Write(indexes[i]);

            // Every code after the first defines a new string while the table has room.
            if (i > 0 && next < 4096)
            {
                next++;
                if (next == 1 << size && size < 12)
                {
                    size++;
                }
            }
        }

        Write(5);
        if (count > 0)
        {
            output.Add((byte)buffer);
        }

        return [.. output];
    }
}
