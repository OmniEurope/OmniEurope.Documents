// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;
using System.IO.Compression;

namespace OmniEurope.Documents.Tests.Imaging;

/// <summary>
/// Writes a one-strip TIFF (TIFF 6.0) with the horizontal differencing predictor (tag 317 = 2) and Adobe
/// Deflate compression, in either byte order, from raw samples. Self-contained, so a probe can link it.
/// </summary>
internal static class TiffFixture
{
    /// <param name="overrides">Directory entries that replace (same tag) or join the written ones; a bits-per-sample
    /// override with one value stores that value.</param>
    public static byte[] WithPredictor(bool littleEndian, int bits, int samplesPerPixel, int width, int height, ushort[] samples, params (ushort Tag, ushort Type, uint Count, uint Value)[] overrides)
    {
        var bytesPerSample = bits / 8;
        var rowLength = width * samplesPerPixel;
        var raw = new byte[samples.Length * bytesPerSample];
        for (var row = 0; row < height; row++)
        {
            for (var i = 0; i < rowLength; i++)
            {
                var index = (row * rowLength) + i;
                var left = i >= samplesPerPixel ? samples[index - samplesPerPixel] : 0;
                var difference = (ushort)(samples[index] - left);
                if (bytesPerSample == 1)
                {
                    raw[index] = (byte)difference;
                }
                else if (littleEndian)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(index * 2), difference);
                }
                else
                {
                    BinaryPrimitives.WriteUInt16BigEndian(raw.AsSpan(index * 2), difference);
                }
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        var strip = compressed.ToArray();
        var entries = new List<(ushort Tag, ushort Type, uint Count, uint Value)>
        {
            (256, 4, 1, (uint)width),
            (257, 4, 1, (uint)height),
            (258, 3, (uint)samplesPerPixel, 0),
            (259, 3, 1, 8),
            (262, 3, 1, samplesPerPixel == 3 ? 2u : 1u),
            (273, 4, 1, 0),
            (277, 3, 1, (uint)samplesPerPixel),
            (278, 4, 1, (uint)height),
            (279, 4, 1, (uint)strip.Length),
            (317, 3, 1, 2),
        };
        entries.RemoveAll(e => overrides.Any(o => o.Tag == e.Tag));
        entries.AddRange(overrides);
        entries.Sort((a, b) => a.Tag.CompareTo(b.Tag));

        // Header, the bits-per-sample array, the strip, then the directory.
        var bitsOffset = 8;
        var stripOffset = bitsOffset + (samplesPerPixel * 2);
        var directoryOffset = stripOffset + strip.Length + (strip.Length & 1);
        var file = new byte[directoryOffset + 2 + (entries.Count * 12) + 4];
        void U16(int at, uint value)
        {
            if (littleEndian)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(at), (ushort)value);
            }
            else
            {
                BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(at), (ushort)value);
            }
        }

        void U32(int at, uint value)
        {
            if (littleEndian)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(at), value);
            }
            else
            {
                BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(at), value);
            }
        }

        file[0] = file[1] = littleEndian ? (byte)'I' : (byte)'M';
        U16(2, 42);
        U32(4, (uint)directoryOffset);
        for (var i = 0; i < samplesPerPixel; i++)
        {
            U16(bitsOffset + (i * 2), (uint)bits);
        }

        strip.CopyTo(file, stripOffset);
        U16(directoryOffset, (uint)entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var (tag, type, count, value) = entries[i];
            var at = directoryOffset + 2 + (i * 12);
            U16(at, tag);
            U16(at + 2, type);
            U32(at + 4, count);
            if (tag == 258)
            {
                // One short fits in the entry; more are stored at bitsOffset.
                if (count == 1)
                {
                    U16(at + 8, value == 0 ? (uint)bits : value);
                }
                else
                {
                    U32(at + 8, (uint)bitsOffset);
                }
            }
            else if (tag == 273)
            {
                U32(at + 8, (uint)stripOffset);
            }
            else if (type == 3)
            {
                U16(at + 8, value);
            }
            else
            {
                U32(at + 8, value);
            }
        }

        return file;
    }
}
