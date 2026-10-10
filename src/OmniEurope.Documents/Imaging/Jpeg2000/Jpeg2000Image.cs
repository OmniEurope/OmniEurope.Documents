// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jpeg2000;

/// <summary>
/// The samples of one component or channel over the whole image, on its own (possibly subsampled) grid: sample
/// (x, y) of the plane sits at (X0 + x) * Dx, (Y0 + y) * Dy on the reference grid.
/// </summary>
internal sealed class Jpeg2000Plane
{
    public Jpeg2000Plane(int x0, int y0, int width, int height, int dx, int dy, int precision, bool signed, int[]? samples = null)
    {
        (X0, Y0, Width, Height, Dx, Dy, Precision, Signed) = (x0, y0, width, height, dx, dy, precision, signed);
        Samples = samples ?? new int[(long)width * height];
    }

    public int X0 { get; }

    public int Y0 { get; }

    public int Width { get; }

    public int Height { get; }

    public int Dx { get; }

    public int Dy { get; }

    public int Precision { get; }

    public bool Signed { get; }

    public int[] Samples { get; }

    /// <summary>The plane of component <paramref name="size"/> over the image area.</summary>
    public static Jpeg2000Plane Of(J2kImageSize image, J2kComponentSize size)
    {
        var x0 = J2kTileComponent.Ceil(image.X0, size.Dx);
        var y0 = J2kTileComponent.Ceil(image.Y0, size.Dy);
        return new Jpeg2000Plane(x0, y0, J2kTileComponent.Ceil(image.X1, size.Dx) - x0, J2kTileComponent.Ceil(image.Y1, size.Dy) - y0,
            size.Dx, size.Dy, size.Precision, size.Signed);
    }

    /// <summary>The sample covering reference grid point (<paramref name="x"/>, <paramref name="y"/>), scaled to 8 bits.</summary>
    public byte At(int x, int y) => Scale(Raw(x, y));

    /// <summary>The sample covering reference grid point (<paramref name="x"/>, <paramref name="y"/>) as decoded.</summary>
    public int Raw(int x, int y)
    {
        var px = Math.Clamp((x / Dx) - X0, 0, Width - 1);
        var py = Math.Clamp((y / Dy) - Y0, 0, Height - 1);
        return Samples[((long)py * Width) + px];
    }

    /// <summary>A sample scaled from the plane's depth to 8 bits (a signed one first offset to unsigned).</summary>
    public byte Scale(int value)
    {
        long v = Signed ? value + (1L << (Precision - 1)) : value;
        var max = (1L << Precision) - 1;
        return (byte)Math.Clamp(((v * 255) + (max / 2)) / max, 0, 255);
    }
}

/// <summary>
/// A decoded JPEG 2000 image: the codestream's components, the colour channels made of them (in order, through the
/// palette), the opacity channel if any, and the colour space it declares, ready to be turned into 8-bit pixels.
/// </summary>
internal sealed class Jpeg2000Image(J2kImageSize size, Jpeg2000Plane[] components, Jpeg2000Plane[] colors)
{
    public J2kImageSize Size { get; } = size;

    public int Width => Size.X1 - Size.X0;

    public int Height => Size.Y1 - Size.Y0;

    /// <summary>The components as the codestream codes them (palette indices for a palette image).</summary>
    public Jpeg2000Plane[] Components { get; } = components;

    public Jpeg2000Plane[] Colors { get; } = colors;

    public Jpeg2000Plane? Alpha { get; init; }

    public bool Premultiplied { get; init; }

    public Jpeg2000ColorSpace ColorSpace { get; init; }

    /// <summary>The ranges and offsets of CIE Lab samples, when the colour space is Lab.</summary>
    public Jpeg2000Lab? Lab { get; init; }

    /// <summary>The colour channels as 8-bit samples, interleaved, on the full image grid (subsampled ones repeated).</summary>
    public byte[] Interleaved(int channels)
    {
        var output = new byte[(long)Width * Height * channels];
        var o = 0;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                for (var c = 0; c < channels; c++)
                {
                    output[o++] = c < Colors.Length ? Colors[c].At(Size.X0 + x, Size.Y0 + y) : (byte)0;
                }
            }
        }

        return output;
    }

    /// <summary>The components' samples as they are (palette indices), interleaved, clipped to 255.</summary>
    public byte[] Indices(int channels)
    {
        var output = new byte[(long)Width * Height * channels];
        var o = 0;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                for (var c = 0; c < channels; c++)
                {
                    output[o++] = c < Components.Length ? (byte)Math.Clamp(Components[c].Raw(Size.X0 + x, Size.Y0 + y), 0, 255) : (byte)0;
                }
            }
        }

        return output;
    }

    /// <summary>The opacity channel as a grey image, or null.</summary>
    public RasterImage? AlphaImage()
    {
        if (Alpha is null)
        {
            return null;
        }

        var image = new RasterImage(Width, Height, ImageColorType.Gray);
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                image.Pixels[(y * Width) + x] = Alpha.At(Size.X0 + x, Size.Y0 + y);
            }
        }

        return image;
    }

    /// <summary>
    /// The image in the colour space it declares: grey, RGB (sYCC converted), CMYK, or for an ICC profile or no
    /// declaration the device space of its channel count; CIE Lab is converted to RGB. The opacity channel becomes
    /// alpha (premultiplied colours divided back).
    /// </summary>
    public RasterImage ToRaster(bool withAlpha = true)
    {
        var (type, channels) = Output();
        var image = new RasterImage(Width, Height, type, Interleaved(channels));
        if (ColorSpace == Jpeg2000ColorSpace.Ycc && channels == 3)
        {
            Jpeg2000Colors.YccToRgb(image.Pixels);
        }
        else if (ColorSpace == Jpeg2000ColorSpace.Lab && channels == 3)
        {
            Jpeg2000Colors.LabToRgb(image.Pixels, Lab ?? Jpeg2000Lab.Default(Colors));
        }

        return withAlpha && AlphaImage() is { } alpha ? Jpeg2000Colors.WithAlpha(image, alpha, Premultiplied) : image;
    }

    private (ImageColorType Type, int Channels) Output()
    {
        var count = ColorSpace switch
        {
            Jpeg2000ColorSpace.Gray => 1,
            Jpeg2000ColorSpace.Cmyk => 4,
            Jpeg2000ColorSpace.Rgb or Jpeg2000ColorSpace.Ycc or Jpeg2000ColorSpace.Lab => 3,
            _ => Colors.Length switch { 1 or 2 => 1, 4 => 4, _ => 3 },
        };
        return count switch
        {
            1 => (ImageColorType.Gray, 1),
            4 => (ImageColorType.Cmyk, 4),
            _ => (ImageColorType.Rgb, 3),
        };
    }
}
