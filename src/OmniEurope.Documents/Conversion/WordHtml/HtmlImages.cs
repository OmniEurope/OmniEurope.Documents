// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Internal;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordHtml;

/// <summary>
/// Pictures as <c>data:</c> URIs, or as separate resources addressed under a prefix
/// (<see cref="WordHtmlOptions.ImageUrlPrefix"/>), one per picture data. The media type is read from the image
/// bytes, never taken from the package: PNG, JPEG, GIF, BMP and WebP go as they are, TIFF is decoded and written
/// as PNG, an Enhanced Metafile is drawn by the PDF conversion and rendered to PNG. Any other picture (WMF,
/// unreadable data) is left out and reported.
/// </summary>
internal sealed class HtmlImages(GapSet gaps, string? prefix)
{
    private const double MetafileDpi = 144;
    private readonly Dictionary<WordImage, string?> _sources = new(ReferenceEqualityComparer.Instance);
    private readonly List<WordHtmlImage> _resources = [];

    /// <summary>The separate resources written so far, in order of first use.</summary>
    public IReadOnlyList<WordHtmlImage> Resources => _resources;

    /// <summary>The <c>data:</c> URI or the address of a picture, or null when it cannot be shown.</summary>
    public string? Source(WordPicture picture)
    {
        if (!_sources.TryGetValue(picture.Image, out var source))
        {
            source = Convert(picture);
            _sources[picture.Image] = source;
        }

        return source;
    }

    private string? Convert(WordPicture picture)
    {
        var data = picture.Image.Data;
        try
        {
            if (ImageInfo.TryIdentify(data, out var info))
            {
                return info.Format == ImageFormat.Tiff ? Uri("image/png", PngCodec.Encode(ImageDecoder.Decode(data))) : Uri(info.ContentType, data);
            }

            if (IsMetafile(picture.Image) && Metafile(picture) is { } png)
            {
                return Uri("image/png", png);
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or ArgumentException or IOException)
        {
            gaps.Add(DocumentGap.Of(HtmlGapCodes.HtmlUnreadablePictureLeftOut));
            return null;
        }

        gaps.Add(DocumentGap.Of(HtmlGapCodes.HtmlPictureFormatLeftOut, Format(picture.Image)));
        return null;
    }

    private static bool IsMetafile(WordImage image) => WordImage.IsEnhancedMetafile(image.Data);

    // The picture alone on a page of its own size, laid out and drawn by the PDF conversion, then rendered.
    private static byte[]? Metafile(WordPicture picture)
    {
        var document = new WordDocument(language: null);
        document.Sections[0].Page = new WordPageSetup
        {
            Width = Math.Max(1, picture.Width),
            Height = Math.Max(1, picture.Height),
            MarginTop = 0,
            MarginBottom = 0,
            MarginLeft = 0,
            MarginRight = 0,
            HeaderDistance = 0,
            FooterDistance = 0,
        };
        document.Styles.DefaultParagraphProperties = new WordParagraphProperties { SpacingAfter = 0, LineSpacing = 1, LineSpacingRule = WordLineSpacingRule.Multiple };
        document.AddParagraph().Add(new WordPicture(new WordImage(picture.Image.Data, "image/x-emf"), picture.Width, picture.Height));
        var result = WordToPdf.Convert(document, new WordPdfOptions { Bookmarks = false });
        if (result.Gaps.Any(g => g.StartsWith("picture format", StringComparison.Ordinal)))
        {
            return null;
        }

        return PdfRenderer.RenderToPng(PdfDocument.Open(result.Pdf), 1, new PdfRenderOptions { Dpi = MetafileDpi, Annotations = false });
    }

    // A data: URI, or the next separate resource under the prefix (image/jpeg is named .jpg, others by subtype).
    private string Uri(string mediaType, byte[] data)
    {
        if (prefix is null)
        {
            return "data:" + mediaType + ";base64," + System.Convert.ToBase64String(data);
        }

        var subtype = mediaType[(mediaType.IndexOf('/', StringComparison.Ordinal) + 1)..];
        var name = "image" + (_resources.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + (subtype == "jpeg" ? "jpg" : subtype);
        _resources.Add(new WordHtmlImage(name, mediaType, (byte[])data.Clone()));
        return prefix + name;
    }

    // The declared type is only named in a gap; it is reduced to the characters a media type uses.
    private static string Format(WordImage image) =>
        new(image.ContentType.Where(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '.' or '+').Take(64).ToArray());
}
