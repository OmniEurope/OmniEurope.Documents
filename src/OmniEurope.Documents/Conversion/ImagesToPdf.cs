// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Conversion;

/// <summary>Options of <see cref="ImagesToPdf"/>.</summary>
public sealed record ImagePdfOptions
{
    /// <summary>Page size in points; null gives each page the image's own size at its resolution.</summary>
    public (double Width, double Height)? PageSize { get; init; }

    /// <summary>Margin around the image, in points, when <see cref="PageSize"/> is set.</summary>
    public double Margin { get; init; } = 28.35;

    /// <summary>Turns a page to landscape when the image is wider than tall (with <see cref="PageSize"/>).</summary>
    public bool AutoRotate { get; init; } = true;

    /// <summary>Document title.</summary>
    public string? Title { get; init; }
}

/// <summary>
/// One image per page (PNG, JPEG, GIF, BMP, TIFF; every page of a multi-page TIFF): either pages sized to the
/// images, or the images fitted and centred on pages of a given size.
/// </summary>
public static class ImagesToPdf
{
    /// <summary>Converts image files into a PDF.</summary>
    /// <exception cref="ArgumentException">An image format is not recognised.</exception>
    public static byte[] Convert(IEnumerable<byte[]> images, ImagePdfOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(images);
        options ??= new ImagePdfOptions();
        var builder = new PdfDocumentBuilder { Title = options.Title, Creator = "OmniEurope.Documents" };
        var count = 0;
        foreach (var data in images)
        {
            foreach (var image in Pages(builder, data))
            {
                Place(builder, image, options);
                count++;
            }
        }

        if (count == 0)
        {
            throw new ArgumentException("No image to convert.", nameof(images));
        }

        return builder.ToArray();
    }

    private static IEnumerable<PdfImage> Pages(PdfDocumentBuilder builder, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!ImageInfo.TryIdentify(data, out var info) || info.Format == ImageFormat.WebP)
        {
            throw new ArgumentException("Unrecognised or unsupported image format.", nameof(data));
        }

        var pages = info.Format == ImageFormat.Tiff ? TiffDecoder.PageCount(data) : 1;
        if (pages <= 1)
        {
            yield return builder.AddImage(data);
            yield break;
        }

        for (var page = 0; page < pages; page++)
        {
            yield return builder.AddImage(TiffDecoder.Decode(data, page));
        }
    }

    private static void Place(PdfDocumentBuilder builder, PdfImage image, ImagePdfOptions options)
    {
        var (width, height) = image.NaturalSize;
        if (options.PageSize is not { } size)
        {
            // A page is 3 to 14400 points a side (ISO 32000-1, annex C): larger images are scaled down, smaller
            // ones keep their size on a page that is at least 3 points.
            var fit = Math.Min(1, 14400 / Math.Max(width, height));
            (width, height) = (width * fit, height * fit);
            builder.AddPage(Math.Max(width, 3), Math.Max(height, 3)).DrawImage(image, 0, 0, width, height);
            return;
        }

        if (options.AutoRotate && width > height != size.Width > size.Height)
        {
            size = (size.Height, size.Width);
        }

        var room = (Width: size.Width - (2 * options.Margin), Height: size.Height - (2 * options.Margin));
        var scale = Math.Min(room.Width / width, room.Height / height);
        var (drawWidth, drawHeight) = (width * scale, height * scale);
        builder.AddPage(size.Width, size.Height).DrawImage(image, (size.Width - drawWidth) / 2, (size.Height - drawHeight) / 2, drawWidth, drawHeight);
    }
}
