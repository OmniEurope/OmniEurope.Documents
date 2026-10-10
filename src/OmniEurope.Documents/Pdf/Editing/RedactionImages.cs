// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Editing;

/// <summary>
/// Redraws an image an area partly covers: decoded to RGB, every pixel whose centre falls under an area takes the
/// fill colour (fully opaque), and the result is a new 8-bit RGB image (Flate), with its soft mask when the
/// original had transparency. The original samples never reach the output.
/// </summary>
internal static class RedactionImages
{
    /// <summary>The new image stream, or null when the image cannot be decoded or is a stencil mask (it is then removed whole).</summary>
    public static PdfStream? Blank(RedactionContext context, PdfStream image, PdfDictionary? resources, Matrix ctm)
    {
        var store = context.Store;
        if (store.Get(image, "ImageMask") is PdfBoolean { Value: true }
            || PdfImageDecoder.TryDecode(store, image, null, resources) is not { } decoded)
        {
            return null;
        }

        var pixels = decoded.ConvertTo(ImageColorType.Rgba);
        var (width, height) = (pixels.Width, pixels.Height);
        var data = pixels.Pixels;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // Image space puts the first row at the top of the unit square (ISO 32000-1 §8.9.4).
                var (px, py) = ctm.Transform((x + 0.5) / width, 1 - ((y + 0.5) / height));
                if (context.Areas.Any(a => px >= a.Left && px <= a.Right && py >= a.Bottom && py <= a.Top))
                {
                    var i = ((y * width) + x) * 4;
                    (data[i], data[i + 1], data[i + 2], data[i + 3]) = (context.Fill.R, context.Fill.G, context.Fill.B, 255);
                }
            }
        }

        var rgb = new byte[width * height * 3];
        var alpha = new byte[width * height];
        var transparent = false;
        for (var p = 0; p < width * height; p++)
        {
            (rgb[p * 3], rgb[(p * 3) + 1], rgb[(p * 3) + 2], alpha[p]) = (data[p * 4], data[(p * 4) + 1], data[(p * 4) + 2], data[(p * 4) + 3]);
            transparent |= alpha[p] != 255;
        }

        var result = Sampled(RedactionContext.Compressed(rgb), width, height, "DeviceRGB");
        if (transparent)
        {
            result.Set("SMask", context.Register(Sampled(RedactionContext.Compressed(alpha), width, height, "DeviceGray")));
        }

        if (store.Get(image, "Interpolate") is PdfBoolean interpolate)
        {
            result.Set("Interpolate", interpolate);
        }

        return result;
    }

    private static PdfStream Sampled(PdfStream stream, int width, int height, string space)
    {
        stream.SetName("Type", "XObject").SetName("Subtype", "Image").SetNumber("Width", width).SetNumber("Height", height)
            .SetName("ColorSpace", space).SetNumber("BitsPerComponent", 8);
        return stream;
    }
}
