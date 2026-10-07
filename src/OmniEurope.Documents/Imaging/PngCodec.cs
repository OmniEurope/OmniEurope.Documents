// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;
using System.IO.Compression;
using OmniEurope.Documents.Internal;

namespace OmniEurope.Documents.Imaging;

/// <summary>
/// PNG decoding (every colour type and bit depth, palettes, transparency chunks, Adam7 interlacing, CRC
/// checked) and encoding (adaptive per-row filters, zlib, resolution chunk).
/// </summary>
public static class PngCodec
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>True when <paramref name="data"/> starts with the PNG signature.</summary>
    public static bool IsPng(ReadOnlySpan<byte> data) => data.StartsWith(Signature);

    /// <summary>Decodes a PNG to 8-bit samples (16-bit samples keep their high byte).</summary>
    /// <exception cref="InvalidDataException">The data is not a valid PNG.</exception>
    public static RasterImage Decode(ReadOnlySpan<byte> data)
    {
        if (!IsPng(data))
        {
            throw new InvalidDataException("Not a PNG file.");
        }

        var header = default(PngHeader);
        byte[]? palette = null;
        byte[]? transparency = null;
        double dpiX = 0;
        double dpiY = 0;
        using var compressed = new MemoryStream();
        var position = 8;
        var seenHeader = false;
        while (position + 12 <= data.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(data[position..]);
            if (length < 0 || position + 12 + (long)length > data.Length)
            {
                throw new InvalidDataException("Truncated PNG chunk.");
            }

            var type = data.Slice(position + 4, 4);
            var body = data.Slice(position + 8, length);
            var crc = BinaryPrimitives.ReadUInt32BigEndian(data[(position + 8 + length)..]);
            if (Crc32.Compute(data.Slice(position + 4, length + 4)) != crc)
            {
                throw new InvalidDataException("PNG chunk CRC mismatch.");
            }

            position += 12 + length;
            switch (System.Text.Encoding.ASCII.GetString(type))
            {
                case "IHDR":
                    header = PngHeader.Parse(body);
                    seenHeader = true;
                    break;
                case "PLTE":
                    palette = body.ToArray();
                    break;
                case "tRNS":
                    transparency = body.ToArray();
                    break;
                case "pHYs" when body.Length >= 9 && body[8] == 1:
                    dpiX = BinaryPrimitives.ReadUInt32BigEndian(body) * 0.0254;
                    dpiY = BinaryPrimitives.ReadUInt32BigEndian(body[4..]) * 0.0254;
                    break;
                case "IDAT":
                    compressed.Write(body);
                    break;
                case "IEND":
                    position = data.Length;
                    break;
            }
        }

        if (!seenHeader)
        {
            throw new InvalidDataException("PNG without IHDR.");
        }

        var raw = Inflate(compressed, header.RawSize());
        var image = new PngUnpacker(header, palette, transparency).Unpack(raw);
        image.DpiX = Math.Round(dpiX, 1);
        image.DpiY = Math.Round(dpiY, 1);
        return image;
    }

    /// <summary>Encodes an image as PNG (CMYK is converted to RGB first).</summary>
    public static byte[] Encode(RasterImage image, CompressionLevel level = CompressionLevel.Optimal)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.ColorType == ImageColorType.Cmyk)
        {
            image = image.ConvertTo(ImageColorType.Rgb);
        }

        using var output = new MemoryStream();
        output.Write(Signature);
        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, image.Width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr[4..], image.Height);
        ihdr[8] = 8;
        ihdr[9] = image.ColorType switch
        {
            ImageColorType.Gray => 0,
            ImageColorType.GrayAlpha => 4,
            ImageColorType.Rgb => 2,
            _ => 6,
        };
        WriteChunk(output, "IHDR", ihdr);
        if (image.DpiX > 0 && image.DpiY > 0)
        {
            Span<byte> phys = stackalloc byte[9];
            BinaryPrimitives.WriteUInt32BigEndian(phys, (uint)Math.Round(image.DpiX / 0.0254));
            BinaryPrimitives.WriteUInt32BigEndian(phys[4..], (uint)Math.Round(image.DpiY / 0.0254));
            phys[8] = 1;
            WriteChunk(output, "pHYs", phys);
        }

        WriteChunk(output, "IDAT", Deflate(PngFilters.FilterRows(image), level));
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    internal static byte[] Deflate(byte[] data, CompressionLevel level)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, level, leaveOpen: true))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }

    private static byte[] Inflate(MemoryStream compressed, long expected)
    {
        if (expected > Array.MaxLength)
        {
            throw new InvalidDataException("PNG image data is too large.");
        }

        compressed.Position = 0;
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        var raw = new byte[expected];
        var read = 0;
        while (read < raw.Length)
        {
            var n = zlib.Read(raw, read, raw.Length - read);
            if (n == 0)
            {
                throw new InvalidDataException("PNG image data is shorter than its size requires.");
            }

            read += n;
        }

        return raw;
    }

    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> body)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, body.Length);
        output.Write(word);
        Span<byte> typeBytes = stackalloc byte[4];
        System.Text.Encoding.ASCII.GetBytes(type, typeBytes);
        output.Write(typeBytes);
        output.Write(body);
        var crc = Crc32.Update(Crc32.Compute(typeBytes), body);
        BinaryPrimitives.WriteUInt32BigEndian(word, crc);
        output.Write(word);
    }
}

/// <summary>The IHDR fields.</summary>
internal readonly record struct PngHeader(int Width, int Height, int BitDepth, int ColorType, bool Interlaced)
{
    // The bit depths each colour type allows (PNG specification, table 11.1).
    private static readonly Dictionary<int, int[]> AllowedDepths = new()
    {
        [0] = [1, 2, 4, 8, 16],
        [2] = [8, 16],
        [3] = [1, 2, 4, 8],
        [4] = [8, 16],
        [6] = [8, 16],
    };

    public int Channels => ColorType switch
    {
        0 => 1,
        2 => 3,
        3 => 1,
        4 => 2,
        _ => 4,
    };

    public int BitsPerPixel => Channels * BitDepth;

    public static PngHeader Parse(ReadOnlySpan<byte> body)
    {
        if (body.Length < 13)
        {
            throw new InvalidDataException("Short IHDR chunk.");
        }

        var header = new PngHeader(
            BinaryPrimitives.ReadInt32BigEndian(body),
            BinaryPrimitives.ReadInt32BigEndian(body[4..]),
            body[8],
            body[9],
            body[12] == 1);
        var validDepth = AllowedDepths.TryGetValue(header.ColorType, out var depths) && depths.Contains(header.BitDepth);
        if (!validDepth || header.Width < 1 || header.Height < 1 || (long)header.Width * header.Height > RasterImage.MaxPixels)
        {
            throw new InvalidDataException("Unsupported or invalid PNG header.");
        }

        return header;
    }

    public long RowBytes(int width) => 1 + (((long)width * BitsPerPixel) + 7) / 8;

    /// <summary>Size of the decompressed data, including the filter byte of each row of each pass.</summary>
    public long RawSize()
    {
        if (!Interlaced)
        {
            return RowBytes(Width) * Height;
        }

        long total = 0;
        foreach (var pass in PngUnpacker.Passes)
        {
            var (w, h) = pass.Size(Width, Height);
            if (w > 0 && h > 0)
            {
                total += RowBytes(w) * h;
            }
        }

        return total;
    }
}
