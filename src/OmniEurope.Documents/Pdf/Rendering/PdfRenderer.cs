// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>Options of <see cref="PdfRenderer"/>.</summary>
public sealed record PdfRenderOptions
{
    /// <summary>Resolution in dots per inch (72 gives one pixel per point).</summary>
    public double Dpi { get; init; } = 96;

    /// <summary>Fonts used for text whose font is not embedded; the bundled fonts by default.</summary>
    public FontLibrary? Fonts { get; init; }

    /// <summary>Draws the normal appearance of annotations (stamps, form fields, notes).</summary>
    public bool Annotations { get; init; } = true;
}

/// <summary>A rendered page: its pixels and what could not be drawn faithfully.</summary>
public sealed record PdfPageRendering(RasterImage Image, IReadOnlyList<string> Gaps)
{
    /// <summary>The image as PNG.</summary>
    public byte[] ToPng() => PngCodec.Encode(Image);
}

/// <summary>
/// Renders PDF pages to images: anti-aliased paths (non-zero and even-odd fills, strokes with joins, caps and
/// dashes), clipping, opacity, images (masks and soft masks), text with embedded TrueType, CFF and Type 1
/// programs (bundled look-alikes for fonts that are not embedded), Type 3 fonts, forms and annotation
/// appearances, in grey, RGB, CMYK and the colour spaces built on them; axial and radial shadings, tiling and shading
/// patterns, the sixteen blend modes, alpha constants, soft masks and transparency groups (isolated and knockout)
/// composited in RGB. The page rotation and crop box apply.
/// </summary>
public static class PdfRenderer
{
    /// <summary>Renders one page on a white background.</summary>
    public static PdfPageRendering Render(PdfPage page, PdfRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        options ??= new PdfRenderOptions();
        var scale = Math.Clamp(options.Dpi, 1, 1200) / 72;
        var crop = page.CropBox.Normalize();
        var (width, height) = ((int)Math.Max(1, Math.Round(page.Width * scale)), (int)Math.Max(1, Math.Round(page.Height * scale)));
        if ((long)width * height > RasterImage.MaxPixels / 3)
        {
            throw new ArgumentException("The requested resolution gives an image too large.", nameof(options));
        }

        var surface = new Surface(width, height);
        var context = new RenderContext(options.Fonts ?? FontLibrary.Default);
        var renderer = new PageRenderer(page.Store, surface, DeviceMatrix(crop, page.Rotation, scale), context);
        renderer.Run(page.ContentBytes(), page.Resources);
        if (options.Annotations)
        {
            AnnotationPainter.Paint(renderer, page.Dictionary, page.Resources);
        }

        var image = surface.ToImage();
        image.DpiX = image.DpiY = options.Dpi;
        return new PdfPageRendering(image, [.. context.Gaps]);
    }

    /// <summary>Renders one page of a document (numbered from 1) as PNG.</summary>
    public static byte[] RenderToPng(PdfDocument document, int pageNumber, PdfRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Render(document.GetPage(pageNumber), options).ToPng();
    }

    // User space to device pixels: the crop box fills the image, y points down, the page rotation turns it clockwise.
    private static Matrix DeviceMatrix(PdfRectangle crop, int rotation, double scale)
    {
        var flip = new Matrix(scale, 0, 0, -scale, -crop.Left * scale, crop.Top * scale);
        var (w, h) = (crop.Width * scale, crop.Height * scale);
        var turn = rotation switch
        {
            90 => new Matrix(0, 1, -1, 0, h, 0),
            180 => new Matrix(-1, 0, 0, -1, w, h),
            270 => new Matrix(0, -1, 1, 0, 0, w),
            _ => Matrix.Identity,
        };
        return flip.Multiply(turn);
    }
}
