// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;

namespace OmniEurope.Documents.Imaging;

/// <summary>Image file formats recognised from their first bytes.</summary>
public enum ImageFormat
{
    /// <summary>Not recognised.</summary>
    Unknown,

    /// <summary>PNG.</summary>
    Png,

    /// <summary>JPEG.</summary>
    Jpeg,

    /// <summary>GIF.</summary>
    Gif,

    /// <summary>BMP.</summary>
    Bmp,

    /// <summary>TIFF.</summary>
    Tiff,

    /// <summary>WebP (identified only; not decoded).</summary>
    WebP,
}

/// <summary>Format, pixel size and resolution of an image file, read from its header only.</summary>
/// <param name="Format">The format.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="DpiX">Horizontal resolution, 0 when the file does not say.</param>
/// <param name="DpiY">Vertical resolution, 0 when the file does not say.</param>
public readonly record struct ImageInfo(ImageFormat Format, int Width, int Height, double DpiX, double DpiY)
{
    /// <summary>The MIME type of the format.</summary>
    public string ContentType => Format switch
    {
        ImageFormat.Png => "image/png",
        ImageFormat.Jpeg => "image/jpeg",
        ImageFormat.Gif => "image/gif",
        ImageFormat.Bmp => "image/bmp",
        ImageFormat.Tiff => "image/tiff",
        ImageFormat.WebP => "image/webp",
        _ => "application/octet-stream",
    };

    /// <summary>Identifies <paramref name="data"/>; returns false when the format or the header is not recognised.</summary>
    public static bool TryIdentify(byte[] data, out ImageInfo info)
    {
        ArgumentNullException.ThrowIfNull(data);
        info = default;
        try
        {
            info = Identify(data);
            return info.Format != ImageFormat.Unknown && info.Width > 0 && info.Height > 0;
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or IndexOutOfRangeException or NotSupportedException)
        {
            return false;
        }
    }

    private static ImageInfo Identify(byte[] data)
    {
        if (PngCodec.IsPng(data) && data.Length >= 24)
        {
            return new ImageInfo(ImageFormat.Png, BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20)), 0, 0) with
            {
                DpiX = PngDpi(data).X,
                DpiY = PngDpi(data).Y,
            };
        }

        if (JpegDecoder.IsJpeg(data))
        {
            var (width, height, _) = JpegDecoder.ReadHeader(data);
            var (dpiX, dpiY) = JfifDpi(data);
            return new ImageInfo(ImageFormat.Jpeg, width, height, dpiX, dpiY);
        }

        if (GifDecoder.IsGif(data))
        {
            return new ImageInfo(ImageFormat.Gif, BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6)), BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(8)), 0, 0);
        }

        if (BmpDecoder.IsBmp(data))
        {
            var header = DibHeader.Parse(data.AsSpan(14));
            return new ImageInfo(ImageFormat.Bmp, header.Width, Math.Abs(header.Height), Math.Round(header.PixelsPerMeterX * 0.0254, 1), Math.Round(header.PixelsPerMeterY * 0.0254, 1));
        }

        if (TiffDecoder.IsTiff(data))
        {
            // A damaged file may have no readable directory: not identified.
            return new TiffFile(data).Directories().FirstOrDefault() is { } tags
                ? new ImageInfo(ImageFormat.Tiff, (int)First(tags, 256), (int)First(tags, 257), 0, 0)
                : default;
        }

        if (data.Length >= 30 && data.AsSpan(0, 4).SequenceEqual("RIFF"u8) && data.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            return WebP(data);
        }

        return default;
    }

    private static uint First(Dictionary<int, uint[]> tags, int id) => tags.TryGetValue(id, out var v) && v.Length > 0 ? v[0] : 0;

    private static (double X, double Y) PngDpi(byte[] data)
    {
        var position = 8;
        while (position + 12 <= data.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(position));
            if (length < 0 || position + 12 + (long)length > data.Length)
            {
                break;
            }

            if (data.AsSpan(position + 4, 4).SequenceEqual("pHYs"u8) && length >= 9 && data[position + 16] == 1)
            {
                return (Math.Round(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position + 8)) * 0.0254, 1), Math.Round(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position + 12)) * 0.0254, 1));
            }

            if (data.AsSpan(position + 4, 4).SequenceEqual("IDAT"u8))
            {
                break;
            }

            position += 12 + length;
        }

        return (0, 0);
    }

    private static (double X, double Y) JfifDpi(byte[] data)
    {
        if (data.Length < 18 || data[2] != 0xFF || data[3] != 0xE0 || !data.AsSpan(6, 5).SequenceEqual("JFIF\0"u8))
        {
            return (0, 0);
        }

        var factor = data[13] switch { 1 => 1d, 2 => 2.54, _ => 0d };
        return (BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(14)) * factor, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(16)) * factor);
    }

    private static ImageInfo WebP(byte[] data)
    {
        var chunk = data.AsSpan(12, 4);
        if (chunk.SequenceEqual("VP8X"u8))
        {
            return new ImageInfo(ImageFormat.WebP, 1 + (data[24] | (data[25] << 8) | (data[26] << 16)), 1 + (data[27] | (data[28] << 8) | (data[29] << 16)), 0, 0);
        }

        if (chunk.SequenceEqual("VP8L"u8))
        {
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(21));
            return new ImageInfo(ImageFormat.WebP, (int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1, 0, 0);
        }

        return new ImageInfo(ImageFormat.WebP, BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(26)) & 0x3FFF, BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(28)) & 0x3FFF, 0, 0);
    }
}

/// <summary>Decodes any supported image file.</summary>
public static class ImageDecoder
{
    /// <summary>Decodes a PNG, JPEG, GIF, BMP or TIFF file (first page or frame).</summary>
    /// <exception cref="NotSupportedException">The format is not recognised or not decodable.</exception>
    public static RasterImage Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (PngCodec.IsPng(data))
        {
            return PngCodec.Decode(data);
        }

        if (JpegDecoder.IsJpeg(data))
        {
            return JpegDecoder.Decode(data);
        }

        if (GifDecoder.IsGif(data))
        {
            return GifDecoder.Decode(data);
        }

        if (BmpDecoder.IsBmp(data))
        {
            return BmpDecoder.Decode(data);
        }

        return TiffDecoder.IsTiff(data) ? TiffDecoder.Decode(data) : throw new NotSupportedException("Unsupported image format.");
    }
}
