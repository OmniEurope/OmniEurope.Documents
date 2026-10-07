// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;
using System.IO.Compression;

namespace OmniEurope.Documents.Imaging;

/// <summary>
/// TIFF decoding of one page: strips or tiles, chunky samples of 1 to 16 bits, bilevel, grey, palette, RGB
/// and CMYK, extra alpha sample, compression none, CCITT (RLE, Group 3, Group 4), LZW, Deflate and
/// PackBits, horizontal predictor, both fill orders. JPEG-compressed, planar and floating-point TIFF throw
/// <see cref="NotSupportedException"/>.
/// </summary>
public static class TiffDecoder
{
    /// <summary>True when <paramref name="data"/> starts with a classic TIFF header.</summary>
    public static bool IsTiff(ReadOnlySpan<byte> data) =>
        data.Length > 8 && ((data[0] == 'I' && data[1] == 'I' && data[2] == 42 && data[3] == 0) || (data[0] == 'M' && data[1] == 'M' && data[2] == 0 && data[3] == 42));

    /// <summary>The number of pages (image file directories).</summary>
    public static int PageCount(byte[] data) => new TiffFile(data).Directories().Count();

    /// <summary>Decodes page <paramref name="page"/> (0-based).</summary>
    public static RasterImage Decode(byte[] data, int page = 0)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!IsTiff(data))
        {
            throw new InvalidDataException("Not a TIFF file.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(page);
        int count;
        try
        {
            var directories = new TiffFile(data).Directories().Take(page + 1).ToList();
            count = directories.Count;
            if (count > page)
            {
                return new TiffPage(data, directories[page]).Decode();
            }
        }
        catch (Exception exception) when (DamagedData.IsOverrun(exception))
        {
            throw DamagedData.Error("TIFF", exception);
        }

        throw count == 0 ? new InvalidDataException("The TIFF file has no readable image directory.") : new ArgumentOutOfRangeException(nameof(page));
    }
}

/// <summary>The byte order and the chain of image file directories.</summary>
internal sealed class TiffFile(byte[] data)
{
    public bool LittleEndian { get; } = data[0] == 'I';

    public IEnumerable<Dictionary<int, uint[]>> Directories()
    {
        var offset = ReadUInt32(4);
        var seen = new HashSet<uint>();
        while (offset != 0 && offset + 2 <= data.Length && seen.Add(offset))
        {
            var count = ReadUInt16((int)offset);
            var entries = new Dictionary<int, uint[]>();
            for (var i = 0; i < count; i++)
            {
                var entry = (int)offset + 2 + (i * 12);
                if (entry + 12 > data.Length)
                {
                    break;
                }

                entries[ReadUInt16(entry)] = ReadValues(entry);
            }

            entries[-1] = [LittleEndian ? 1u : 0u];
            yield return entries;
            var next = (int)offset + 2 + (count * 12);
            offset = next + 4 <= data.Length ? ReadUInt32(next) : 0;
        }
    }

    // Byte size of each TIFF field type, from 1 (BYTE) to 12 (DOUBLE); 0 for an unknown type.
    private static readonly int[] TypeSizes = [0, 1, 1, 2, 4, 8, 1, 1, 2, 4, 8, 4, 8];

    private uint[] ReadValues(int entry)
    {
        var type = ReadUInt16(entry + 2);
        var count = ReadUInt32(entry + 4);
        var size = type < TypeSizes.Length ? TypeSizes[type] : 0;
        var total = (long)size * count;
        var position = total <= 4 ? (uint)(entry + 8) : ReadUInt32(entry + 8);
        if (size == 0 || count == 0 || count > 1 << 24 || position + total > data.Length)
        {
            return [];
        }

        // A rational (types 5 and 10) is read as its numerator and denominator.
        var (length, stride) = type is 5 or 10 ? (count * 2, 4) : (count, size);
        var values = new uint[length];
        for (var i = 0; i < values.Length; i++)
        {
            var at = (int)position + (i * stride);
            values[i] = stride switch
            {
                1 => data[at],
                2 => ReadUInt16(at),
                _ => ReadUInt32(at),
            };
        }

        return values;
    }
    private ushort ReadUInt16(int at) => LittleEndian ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(at)) : BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at));

    private uint ReadUInt32(int at) => LittleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at)) : BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at));
}

/// <summary>One page: reads the tags, decompresses strips or tiles and converts samples.</summary>
internal sealed class TiffPage(byte[] data, Dictionary<int, uint[]> tags)
{
    private static readonly HashSet<int> SampleBits = [1, 2, 4, 8, 16];

    private uint Tag(int id, uint fallback = 0) => tags.TryGetValue(id, out var v) && v.Length > 0 ? v[0] : fallback;

    private uint[] Tags(int id) => tags.TryGetValue(id, out var v) ? v : [];

    public RasterImage Decode()
    {
        var width = (int)Tag(256);
        var height = (int)Tag(257);
        var samples = (int)Tag(277, 1);
        var bits = (int)Tag(258, 1);
        var compression = (int)Tag(259, 1);
        var photometric = (int)Tag(262, 1);
        Validate(width, height, samples, bits, compression);
        var tiled = Tags(322).Length > 0;
        var chunkWidth = tiled ? (int)Tag(322) : width;
        var chunkHeight = tiled ? (int)Tag(323) : (int)Math.Min(Tag(278, (uint)height), (uint)height);
        if (chunkWidth < 1 || chunkHeight < 1)
        {
            throw new InvalidDataException("Invalid TIFF tile or strip size.");
        }

        var layout = new TiffLayout(width, height, samples, bits, chunkWidth, chunkHeight, Tag(-1) == 1);
        var raw = ReadChunks(layout, compression, photometric);
        var image = new TiffSamples(layout, photometric, Tags(320), Tags(338).Length > 0).ToImage(raw);
        ApplyResolution(image);
        return image;
    }

    private void Validate(int width, int height, int samples, int bits, int compression)
    {
        if (width < 1 || height < 1 || (long)width * height > RasterImage.MaxPixels || samples is < 1 or > 5)
        {
            throw new InvalidDataException("Invalid TIFF dimensions.");
        }

        if (Tag(284, 1) == 2 && samples > 1)
        {
            throw new NotSupportedException("Planar TIFF is not supported.");
        }

        // Old-style JPEG (6), JPEG (7) and floating-point samples (SampleFormat 3) are not decoded.
        if (compression is 6 or 7 || Tag(339, 1) == 3 || !SampleBits.Contains(bits))
        {
            throw new NotSupportedException($"TIFF compression {compression} or sample format is not supported.");
        }
    }

    // XResolution and YResolution in pixels per inch (unit 2) or per centimetre (unit 3).
    private void ApplyResolution(RasterImage image)
    {
        var unit = Tag(296, 2);
        if (Rational(282) is not { } x || unit is not (2 or 3))
        {
            return;
        }

        var factor = unit == 3 ? 2.54 : 1;
        image.DpiX = Math.Round(x * factor, 2);
        image.DpiY = Rational(283) is { } y ? Math.Round(y * factor, 2) : image.DpiX;
    }

    private double? Rational(int tag)
    {
        var values = Tags(tag);
        return values.Length >= 2 && values[1] != 0 ? values[0] / (double)values[1] : null;
    }
    // Decompresses every strip or tile into one buffer of whole rows (chunky samples, no padding between rows
    // beyond the byte alignment of each row).
    private byte[] ReadChunks(TiffLayout layout, int compression, int photometric)
    {
        var tiled = Tags(324).Length > 0;
        var offsets = tiled ? Tags(324) : Tags(273);
        var counts = tiled ? Tags(325) : Tags(279);
        var output = new byte[(long)layout.RowBytes * layout.Height];
        var across = (layout.Width + layout.ChunkWidth - 1) / layout.ChunkWidth;
        for (var i = 0; i < offsets.Length; i++)
        {
            var count = i < counts.Length ? counts[i] : (uint)(data.Length - offsets[i]);
            if (offsets[i] >= data.Length)
            {
                continue;
            }

            var source = data.AsSpan((int)offsets[i], (int)Math.Min(count, (uint)data.Length - offsets[i])).ToArray();
            if (Tag(266, 1) == 2)
            {
                ReverseBits(source);
            }

            var chunkRows = tiled ? layout.ChunkHeight : Math.Min(layout.ChunkHeight, layout.Height - (i * layout.ChunkHeight));
            if (chunkRows <= 0)
            {
                continue;
            }

            var chunk = Decompress(source, compression, layout, layout.ChunkWidth, chunkRows, photometric);
            Place(chunk, output, layout, tiled ? i % across : 0, tiled ? i / across : i, chunkRows);
        }

        return output;
    }

    private byte[] Decompress(byte[] source, int compression, TiffLayout layout, int columns, int rows, int photometric)
    {
        var expected = (int)(layout.ChunkRowBytes(columns) * (long)rows);
        var bytes = compression switch
        {
            1 => source,
            2 or 3 or 4 => CcittFaxDecoder.Decode(source, Ccitt(compression, columns, rows, photometric), out _, out _),
            5 => TiffLzw.Decode(source, expected),
            8 or 32946 => Inflate(source, expected),
            32773 => PackBits.Decode(source, expected),
            _ => throw new NotSupportedException($"TIFF compression {compression} is not supported."),
        };
        if (Tag(317, 1) == 2)
        {
            HorizontalPredictor.Undo(bytes, layout, columns, rows);
        }

        return bytes;
    }

    private CcittOptions Ccitt(int compression, int columns, int rows, int photometric)
    {
        var options = Tag(compression == 3 ? 292 : 293);
        return new CcittOptions
        {
            K = compression == 4 ? -1 : compression == 3 && (options & 1) != 0 ? 1 : 0,
            Columns = columns,
            Rows = rows,
            ByteAlignedRowsWithoutEol = compression == 2,
            BlackIs1 = photometric == 0,
        };
    }

    private static void Place(byte[] chunk, byte[] output, TiffLayout layout, int chunkX, int chunkY, int chunkRows)
    {
        var chunkRowBytes = layout.ChunkRowBytes(layout.ChunkWidth);
        var startByte = layout.ByteOffsetOfColumn(chunkX * layout.ChunkWidth);
        for (var row = 0; row < chunkRows; row++)
        {
            var y = (chunkY * layout.ChunkHeight) + row;
            if (y >= layout.Height || (row + 1) * (long)chunkRowBytes > chunk.Length)
            {
                break;
            }

            var length = Math.Min(chunkRowBytes, layout.RowBytes - startByte);
            Array.Copy(chunk, row * chunkRowBytes, output, ((long)y * layout.RowBytes) + startByte, length);
        }
    }

    private static byte[] Inflate(byte[] source, int expected)
    {
        using var input = new ZLibStream(new MemoryStream(source), CompressionMode.Decompress);
        var output = new byte[expected];
        var read = 0;
        int n;
        while (read < expected && (n = input.Read(output, read, expected - read)) > 0)
        {
            read += n;
        }

        return output;
    }

    private static void ReverseBits(byte[] bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            b = (byte)(((b & 0xF0) >> 4) | ((b & 0x0F) << 4));
            b = (byte)(((b & 0xCC) >> 2) | ((b & 0x33) << 2));
            bytes[i] = (byte)(((b & 0xAA) >> 1) | ((b & 0x55) << 1));
        }
    }
}
