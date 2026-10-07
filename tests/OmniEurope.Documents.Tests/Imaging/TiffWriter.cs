// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;

namespace OmniEurope.Documents.Tests.Imaging;

/// <summary>
/// Writes classic TIFF files (TIFF 6.0, section 2) page by page: strips first, then values that do not fit in
/// an entry, then the image file directory, each directory linked to the next. Width, height, strip offsets and
/// strip byte counts are added; a null strip gets an offset past the end of the file.
/// </summary>
internal static class TiffWriter
{
    public const ushort ByteField = 1;
    public const ushort Short = 3;
    public const ushort Long = 4;
    public const ushort Rational = 5;

    public sealed record Page(int Width, int Height, (ushort Tag, ushort Type, uint[] Values)[] Tags, params byte[]?[] Strips);

    public static byte[] Write(bool littleEndian, params Page[] pages)
    {
        var file = new List<byte> { (byte)(littleEndian ? 'I' : 'M'), (byte)(littleEndian ? 'I' : 'M') };
        file.AddRange(U16(littleEndian, 42));
        var link = file.Count;
        file.AddRange(U32(littleEndian, 0));
        foreach (var page in pages)
        {
            var offsets = new List<uint>();
            foreach (var strip in page.Strips)
            {
                offsets.Add(strip is null ? 0xFFFFFF : (uint)file.Count);
                file.AddRange(strip ?? []);
            }

            var entries = page.Tags.ToList();
            entries.Add((256, Long, [(uint)page.Width]));
            entries.Add((257, Long, [(uint)page.Height]));
            entries.Add((273, Long, [.. offsets]));
            entries.Add((279, Long, [.. page.Strips.Select(s => (uint)(s?.Length ?? 1))]));
            entries.Sort((a, b) => a.Tag.CompareTo(b.Tag));

            // Values longer than four bytes go before the directory.
            var outOfLine = new Dictionary<int, uint>();
            for (var i = 0; i < entries.Count; i++)
            {
                var bytes = Values(littleEndian, entries[i].Type, entries[i].Values);
                if (bytes.Length > 4)
                {
                    Align(file);
                    outOfLine[i] = (uint)file.Count;
                    file.AddRange(bytes);
                }
            }

            Align(file);
            Patch(file, link, U32(littleEndian, (uint)file.Count));
            file.AddRange(U16(littleEndian, (ushort)entries.Count));
            for (var i = 0; i < entries.Count; i++)
            {
                var (tag, type, values) = entries[i];
                file.AddRange(U16(littleEndian, tag));
                file.AddRange(U16(littleEndian, type));
                file.AddRange(U32(littleEndian, (uint)(type == Rational ? values.Length / 2 : values.Length)));
                var value = outOfLine.TryGetValue(i, out var at) ? U32(littleEndian, at) : Values(littleEndian, type, values);
                file.AddRange([.. value, .. new byte[4 - value.Length]]);
            }

            link = file.Count;
            file.AddRange(U32(littleEndian, 0));
        }

        return [.. file];
    }

    private static byte[] Values(bool littleEndian, ushort type, uint[] values) => type switch
    {
        ByteField => [.. values.Select(v => (byte)v)],
        Short => [.. values.SelectMany(v => U16(littleEndian, (ushort)v))],
        _ => [.. values.SelectMany(v => U32(littleEndian, v))],
    };

    private static void Align(List<byte> file)
    {
        if (file.Count % 2 == 1)
        {
            file.Add(0);
        }
    }

    private static void Patch(List<byte> file, int at, byte[] bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            file[at + i] = bytes[i];
        }
    }

    private static byte[] U16(bool littleEndian, ushort value)
    {
        var bytes = new byte[2];
        if (littleEndian)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        }

        return bytes;
    }

    private static byte[] U32(bool littleEndian, uint value)
    {
        var bytes = new byte[4];
        if (littleEndian)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        }

        return bytes;
    }
}
