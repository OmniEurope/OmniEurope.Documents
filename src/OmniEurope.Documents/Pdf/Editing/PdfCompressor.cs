// SPDX-License-Identifier: EUPL-1.2
using System.IO.Compression;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Editing;

/// <summary>Options of <see cref="PdfCompressor.Compress"/>.</summary>
public sealed record PdfCompressionOptions
{
    /// <summary>Re-encode colour and greyscale images as JPEG at this quality (1-100) when that makes them
    /// smaller; null keeps images as they are. Default null (lossless only).</summary>
    public int? ImageQuality { get; init; }

    /// <summary>Scale colour and greyscale images down so neither side exceeds this many pixels (with
    /// <see cref="ImageQuality"/>; a scaled image stays lossless when JPEG would not be smaller); null keeps
    /// their size.</summary>
    public int? MaxImageSide { get; init; }
}

/// <summary>
/// Rewrites a PDF smaller: only objects reachable from the catalog are kept (old revisions and orphans go),
/// streams without lossy encoding are recompressed with the strongest Flate level, objects are packed into
/// compressed object streams, and images can optionally be re-encoded as JPEG and downsampled. The
/// document structure (pages, outline, forms, metadata) is kept; the output is never encrypted.
/// </summary>
public static class PdfCompressor
{
    private static readonly HashSet<string> Recompressible = new(StringComparer.Ordinal)
    {
        "FlateDecode", "Fl", "LZWDecode", "LZW", "ASCIIHexDecode", "AHx", "ASCII85Decode", "A85", "RunLengthDecode", "RL",
    };

    /// <summary>Compresses <paramref name="document"/>.</summary>
    public static byte[] Compress(PdfDocument document, PdfCompressionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= new PdfCompressionOptions();
        var store = document.Store;
        var table = new PdfObjectTable();
        var copier = new PdfObjectCopier(table, (s, stream) => Transform(s, stream, options));
        var catalog = table.Add(copier.CopyObject(store, store.Catalog));
        var info = Info(document, table);
        using var output = new MemoryStream();
        PdfCompactWriter.Write(table, output, catalog, info);
        return output.ToArray();
    }

    private static PdfReference Info(PdfDocument document, PdfObjectTable table) =>
        table.Add(PdfEditor.Information(document.Information));

    private static PdfStream Transform(PdfObjectStore store, PdfStream stream, PdfCompressionOptions options)
    {
        try
        {
            if (options.ImageQuality is { } quality && store.Get(stream, "Subtype") is PdfName { Value: "Image" } && Reencode(store, stream, quality, options.MaxImageSide) is { } image)
            {
                return image;
            }

            return Recompress(store, stream);
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or IndexOutOfRangeException)
        {
            return stream;
        }
    }

    private static PdfStream Recompress(PdfObjectStore store, PdfStream stream)
    {
        var filters = PdfStreams.FilterNames(stream);
        if (!filters.All(Recompressible.Contains) || stream["Type"] is PdfName { Value: "XRef" or "ObjStm" })
        {
            return stream;
        }

        var (decoded, _, _) = store.Decode(stream);
        var deflated = PngCodec.Deflate(decoded, CompressionLevel.SmallestSize);
        if (deflated.Length >= stream.Data.Length)
        {
            return stream;
        }

        var result = new PdfStream(deflated);
        foreach (var (key, value) in stream.Entries)
        {
            if (key is not ("Filter" or "DecodeParms" or "Length"))
            {
                result.Set(key, value);
            }
        }

        result.SetName("Filter", "FlateDecode");
        return result;
    }

    private static PdfStream? Reencode(PdfObjectStore store, PdfStream stream, int quality, int? maxSide)
    {
        if (store.Get(stream, "ImageMask") is PdfBoolean { Value: true } || store.Number(stream, "BitsPerComponent", 8) < 8)
        {
            return null;
        }

        var image = PdfImageDecoder.TryDecode(store, stream, null, null);
        var oversize = image is not null && maxSide is { } side && Math.Max(image.Width, image.Height) > side;
        if (image is null || (!oversize && image.Width * image.Height < 64 * 64))
        {
            return null;
        }

        var opaque = Opaque(image, maxSide);
        var data = JpegEncoder.Encode(opaque, quality);

        // Kept as it is unless the JPEG saves at least a tenth; an image above maxSide is scaled down anyway
        // and then stays lossless when JPEG does not pay.
        if (data.Length < stream.Data.Length * 0.9)
        {
            return ImageStream(stream, opaque, data, "DCTDecode");
        }

        return oversize ? ImageStream(stream, opaque, PngCodec.Deflate(opaque.Pixels, CompressionLevel.SmallestSize), "FlateDecode") : null;
    }

    // Alpha leaves (the soft mask stays a separate stream), CMYK becomes RGB, and the image fits within maxSide.
    private static RasterImage Opaque(RasterImage image, int? maxSide)
    {
        var opaque = image.ColorType switch
        {
            ImageColorType.Rgba or ImageColorType.Cmyk => image.ConvertTo(ImageColorType.Rgb),
            ImageColorType.GrayAlpha => image.ConvertTo(ImageColorType.Gray),
            _ => image,
        };
        return maxSide is { } side && Math.Max(opaque.Width, opaque.Height) > side ? ImageScaler.Fit(opaque, side) : opaque;
    }

    private static PdfStream ImageStream(PdfStream stream, RasterImage opaque, byte[] data, string filter)
    {
        var result = (PdfStream)new PdfStream(data)
            .SetName("Type", "XObject").SetName("Subtype", "Image")
            .SetNumber("Width", opaque.Width).SetNumber("Height", opaque.Height)
            .SetName("ColorSpace", opaque.ColorType == ImageColorType.Gray ? "DeviceGray" : "DeviceRGB")
            .SetNumber("BitsPerComponent", 8).SetName("Filter", filter);
        foreach (var key in (string[])["SMask", "Interpolate", "Intent", "OC", "Metadata"])
        {
            result.Set(key, stream[key]);
        }

        return result;
    }
}
