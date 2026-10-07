// SPDX-License-Identifier: EUPL-1.2
using System.IO.Compression;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf.Objects;

namespace OmniEurope.Documents.Pdf.Writing;

/// <summary>An image placed in a document being written. Obtain one from <see cref="PdfDocumentBuilder.AddImage(byte[])"/>.</summary>
public sealed class PdfImage
{
    internal PdfImage(string resourceName, PdfReference reference, int width, int height, double dpiX, double dpiY)
    {
        ResourceName = resourceName;
        Reference = reference;
        Width = width;
        Height = height;
        DpiX = dpiX;
        DpiY = dpiY;
    }

    /// <summary>Width in pixels.</summary>
    public int Width { get; }

    /// <summary>Height in pixels.</summary>
    public int Height { get; }

    /// <summary>Horizontal resolution stored in the file, 0 when unknown.</summary>
    public double DpiX { get; }

    /// <summary>Vertical resolution stored in the file, 0 when unknown.</summary>
    public double DpiY { get; }

    /// <summary>The natural size in points (96 dpi when the file gives no resolution).</summary>
    public (double Width, double Height) NaturalSize =>
        (Width * 72.0 / (DpiX > 0 ? DpiX : 96), Height * 72.0 / (DpiY > 0 ? DpiY : 96));

    internal string ResourceName { get; }

    internal PdfReference Reference { get; }

    /// <summary>Builds the image XObject: JPEG data is embedded as is (DCTDecode), other formats are decoded
    /// and stored compressed, with their alpha channel as a soft mask.</summary>
    internal static PdfImage Create(byte[] file, PdfObjectTable table, string resourceName)
    {
        if (JpegDecoder.IsJpeg(file))
        {
            var (width, height, components) = JpegDecoder.ReadHeader(file);
            var jpeg = new PdfStream(file)
                .SetName("Type", "XObject").SetName("Subtype", "Image")
                .SetNumber("Width", width).SetNumber("Height", height)
                .SetName("ColorSpace", components switch { 1 => "DeviceGray", 4 => "DeviceCMYK", _ => "DeviceRGB" })
                .SetNumber("BitsPerComponent", 8)
                .SetName("Filter", "DCTDecode");
            if (components == 4 && HasAdobeMarker(file))
            {
                // Adobe CMYK JPEGs store inverted inks.
                jpeg.Set("Decode", PdfArray.OfNumbers(1, 0, 1, 0, 1, 0, 1, 0));
            }

            ImageInfo.TryIdentify(file, out var info);
            return new PdfImage(resourceName, table.Add(jpeg), width, height, info.DpiX, info.DpiY);
        }

        var image = ImageDecoder.Decode(file);
        return FromRaster(image, table, resourceName);
    }

    internal static PdfImage FromRaster(RasterImage image, PdfObjectTable table, string resourceName)
    {
        var (colorSpace, colorBytes, alpha) = Split(image);
        var stream = Compressed(colorBytes)
            .SetName("Type", "XObject").SetName("Subtype", "Image")
            .SetNumber("Width", image.Width).SetNumber("Height", image.Height)
            .SetName("ColorSpace", colorSpace)
            .SetNumber("BitsPerComponent", 8);
        if (alpha is not null)
        {
            var mask = Compressed(alpha)
                .SetName("Type", "XObject").SetName("Subtype", "Image")
                .SetNumber("Width", image.Width).SetNumber("Height", image.Height)
                .SetName("ColorSpace", "DeviceGray")
                .SetNumber("BitsPerComponent", 8);
            stream.Set("SMask", table.Add(mask));
        }

        return new PdfImage(resourceName, table.Add(stream), image.Width, image.Height, image.DpiX, image.DpiY);
    }

    private static (string ColorSpace, byte[] Color, byte[]? Alpha) Split(RasterImage image)
    {
        var pixels = image.Pixels;
        var count = image.Width * image.Height;
        switch (image.ColorType)
        {
            case ImageColorType.Gray:
                return ("DeviceGray", pixels, null);
            case ImageColorType.Rgb:
                return ("DeviceRGB", pixels, null);
            case ImageColorType.Cmyk:
                return ("DeviceCMYK", pixels, null);
            case ImageColorType.GrayAlpha:
                var gray = new byte[count];
                var grayAlpha = new byte[count];
                for (var i = 0; i < count; i++)
                {
                    gray[i] = pixels[i * 2];
                    grayAlpha[i] = pixels[(i * 2) + 1];
                }

                return ("DeviceGray", gray, image.HasTransparency ? grayAlpha : null);
            default:
                var rgb = new byte[count * 3];
                var rgbAlpha = new byte[count];
                for (var i = 0; i < count; i++)
                {
                    rgb[i * 3] = pixels[i * 4];
                    rgb[(i * 3) + 1] = pixels[(i * 4) + 1];
                    rgb[(i * 3) + 2] = pixels[(i * 4) + 2];
                    rgbAlpha[i] = pixels[(i * 4) + 3];
                }

                return ("DeviceRGB", rgb, image.HasTransparency ? rgbAlpha : null);
        }
    }

    private static PdfStream Compressed(byte[] data)
    {
        var stream = new PdfStream(PngCodec.Deflate(data, CompressionLevel.Optimal));
        stream.SetName("Filter", "FlateDecode");
        return stream;
    }

    private static bool HasAdobeMarker(byte[] data)
    {
        for (var i = 2; i + 10 < data.Length && data[i] == 0xFF; i += 2 + ((data[i + 2] << 8) | data[i + 3]))
        {
            if (data[i + 1] == 0xEE && data.AsSpan(i + 4, 5).SequenceEqual("Adobe"u8))
            {
                return true;
            }

            if (data[i + 1] == 0xDA)
            {
                break;
            }
        }

        return false;
    }
}
