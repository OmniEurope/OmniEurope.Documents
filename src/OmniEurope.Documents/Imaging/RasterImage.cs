// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging;

/// <summary>The channels of a <see cref="RasterImage"/>, 8 bits each.</summary>
public enum ImageColorType
{
    /// <summary>One grey channel.</summary>
    Gray = 1,

    /// <summary>Grey and alpha.</summary>
    GrayAlpha = 2,

    /// <summary>Red, green, blue.</summary>
    Rgb = 3,

    /// <summary>Red, green, blue, alpha (straight, not premultiplied).</summary>
    Rgba = 4,

    /// <summary>Cyan, magenta, yellow, black (0 = no ink).</summary>
    Cmyk = 5,
}

/// <summary>An uncompressed image: rows of 8-bit samples, top row first, no padding between rows.</summary>
public sealed class RasterImage
{
    /// <summary>Most pixels a decoder accepts (a decompression bomb guard): 268 million.</summary>
    public const long MaxPixels = 1L << 28;

    /// <summary>Creates an image; <paramref name="pixels"/> is allocated (black, transparent) when null.</summary>
    public RasterImage(int width, int height, ImageColorType colorType, byte[]? pixels = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if ((long)width * height > MaxPixels)
        {
            throw new ArgumentException($"An image of {width}x{height} pixels is larger than allowed.");
        }

        Width = width;
        Height = height;
        ColorType = colorType;
        var size = (long)width * height * Components;
        if (pixels is not null && pixels.LongLength != size)
        {
            throw new ArgumentException($"Expected {size} bytes of pixels, got {pixels.Length}.", nameof(pixels));
        }

        Pixels = pixels ?? new byte[size];
    }

    /// <summary>Width in pixels.</summary>
    public int Width { get; }

    /// <summary>Height in pixels.</summary>
    public int Height { get; }

    /// <summary>The channels.</summary>
    public ImageColorType ColorType { get; }

    /// <summary>Bytes per pixel.</summary>
    public int Components => ComponentsOf(ColorType);

    /// <summary>Bytes per row.</summary>
    public int Stride => Width * Components;

    /// <summary>The samples.</summary>
    public byte[] Pixels { get; }

    /// <summary>Horizontal resolution in dots per inch, 0 when unknown.</summary>
    public double DpiX { get; set; }

    /// <summary>Vertical resolution in dots per inch, 0 when unknown.</summary>
    public double DpiY { get; set; }

    /// <summary>True when the image has an alpha channel with at least one non-opaque pixel.</summary>
    public bool HasTransparency
    {
        get
        {
            if (ColorType is not (ImageColorType.GrayAlpha or ImageColorType.Rgba))
            {
                return false;
            }

            var step = Components;
            for (var i = step - 1; i < Pixels.Length; i += step)
            {
                if (Pixels[i] != 255)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Bytes per pixel of a colour type.</summary>
    public static int ComponentsOf(ImageColorType type) => type switch
    {
        ImageColorType.Gray => 1,
        ImageColorType.GrayAlpha => 2,
        ImageColorType.Rgb => 3,
        _ => 4,
    };

    /// <summary>The pixel at (x, y) as red, green, blue, alpha.</summary>
    public (byte R, byte G, byte B, byte A) GetRgba(int x, int y)
    {
        var i = (y * Stride) + (x * Components);
        var p = Pixels;
        return ColorType switch
        {
            ImageColorType.Gray => (p[i], p[i], p[i], 255),
            ImageColorType.GrayAlpha => (p[i], p[i], p[i], p[i + 1]),
            ImageColorType.Rgb => (p[i], p[i + 1], p[i + 2], 255),
            ImageColorType.Rgba => (p[i], p[i + 1], p[i + 2], p[i + 3]),
            _ => CmykToRgba(p[i], p[i + 1], p[i + 2], p[i + 3]),
        };
    }

    /// <summary>A copy converted to <paramref name="target"/> (alpha dropped or set opaque, CMYK converted
    /// with the simple complement formula).</summary>
    public RasterImage ConvertTo(ImageColorType target)
    {
        if (target == ColorType)
        {
            return new RasterImage(Width, Height, ColorType, (byte[])Pixels.Clone()) { DpiX = DpiX, DpiY = DpiY };
        }

        if (target == ImageColorType.Cmyk)
        {
            throw new NotSupportedException("Conversion to CMYK is not supported.");
        }

        var result = new RasterImage(Width, Height, target) { DpiX = DpiX, DpiY = DpiY };
        var output = result.Pixels;
        var o = 0;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var (r, g, b, a) = GetRgba(x, y);
                o = Write(output, o, target, r, g, b, a);
            }
        }

        return result;
    }

    private static int Write(byte[] output, int o, ImageColorType target, byte r, byte g, byte b, byte a)
    {
        switch (target)
        {
            case ImageColorType.Gray:
                output[o] = Luma(r, g, b);
                return o + 1;
            case ImageColorType.GrayAlpha:
                output[o] = Luma(r, g, b);
                output[o + 1] = a;
                return o + 2;
            case ImageColorType.Rgb:
                output[o] = r;
                output[o + 1] = g;
                output[o + 2] = b;
                return o + 3;
            default:
                output[o] = r;
                output[o + 1] = g;
                output[o + 2] = b;
                output[o + 3] = a;
                return o + 4;
        }
    }

    /// <summary>ITU-R BT.601 luma, rounded.</summary>
    public static byte Luma(byte r, byte g, byte b) => (byte)(((299 * r) + (587 * g) + (114 * b) + 500) / 1000);

    private static (byte, byte, byte, byte) CmykToRgba(byte c, byte m, byte y, byte k) =>
        ((byte)((255 - c) * (255 - k) / 255), (byte)((255 - m) * (255 - k) / 255), (byte)((255 - y) * (255 - k) / 255), 255);
}
