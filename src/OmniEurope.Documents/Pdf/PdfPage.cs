// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf;

/// <summary>A page of a <see cref="PdfDocument"/>.</summary>
public sealed class PdfPage
{
    private IReadOnlyList<PdfLetter>? _letters;
    private IReadOnlyList<PdfPageImage>? _images;

    internal PdfPage(PdfDocument document, int number, PdfDictionary dictionary, PdfDictionary? resources, PdfRectangle mediaBox, PdfRectangle cropBox, int rotation, PdfReference? reference)
    {
        Reference = reference;
        Document = document;
        Number = number;
        Dictionary = dictionary;
        Resources = resources;
        MediaBox = mediaBox;
        CropBox = cropBox;
        Rotation = rotation;
    }

    /// <summary>The document.</summary>
    public PdfDocument Document { get; }

    /// <summary>The 1-based page number.</summary>
    public int Number { get; }

    /// <summary>The media box (the physical page) in user space.</summary>
    public PdfRectangle MediaBox { get; }

    /// <summary>The crop box (the visible area), the media box when absent.</summary>
    public PdfRectangle CropBox { get; }

    /// <summary>Clockwise rotation applied when displaying: 0, 90, 180 or 270.</summary>
    public int Rotation { get; }

    /// <summary>The displayed width in points (rotation applied).</summary>
    public double Width => Rotation is 90 or 270 ? CropBox.Height : CropBox.Width;

    /// <summary>The displayed height in points (rotation applied).</summary>
    public double Height => Rotation is 90 or 270 ? CropBox.Width : CropBox.Height;

    /// <summary>Every glyph drawn on the page, in drawing order, with its Unicode text and position (user
    /// space, unrotated). Invisible text (render mode 3, such as an OCR layer) is included.</summary>
    public IReadOnlyList<PdfLetter> Letters => _letters ??= TextExtractor.Extract(this);

    /// <summary>The images drawn on the page with where they are placed.</summary>
    public IReadOnlyList<PdfPageImage> Images => _images ??= PageImages.Collect(this);

    /// <summary>The page text in a simple reading order: lines from top to bottom, letters left to right,
    /// spaces where glyphs are far apart. For multi-column layout use the layout analysis.</summary>
    public string Text => SimpleTextOrder.Build(Letters);

    internal PdfDictionary Dictionary { get; }

    /// <summary>The indirect reference of the page object (null for a direct page object).</summary>
    internal PdfReference? Reference { get; }

    internal PdfDictionary? Resources { get; }

    internal PdfObjectStore Store => Document.Store;

    /// <summary>The concatenated, decoded content streams of the page.</summary>
    internal byte[] ContentBytes()
    {
        var contents = Store.Get(Dictionary, "Contents");
        var parts = contents switch
        {
            PdfStream stream => [stream],
            PdfArray array => array.Items.Select(Store.Resolve).OfType<PdfStream>().ToList(),
            _ => new List<PdfStream>(),
        };
        using var output = new MemoryStream();
        foreach (var part in parts)
        {
            try
            {
                output.Write(Store.DecodeBytes(part));
            }
            catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
            {
                // A broken content stream part is skipped; the others still draw.
            }

            output.WriteByte((byte)'\n');
        }

        return output.ToArray();
    }
}

/// <summary>Walks the page tree with inherited attributes.</summary>
internal static class PdfPageTree
{
    public static List<PdfPage> Collect(PdfDocument document)
    {
        var store = document.Store;
        var pages = new List<PdfPage>();
        var root = store.Get<PdfDictionary>(store.Catalog, "Pages");
        if (root is null)
        {
            return pages;
        }

        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<(PdfDictionary Node, PdfDictionary? Resources, PdfRectangle? Media, PdfRectangle? Crop, int Rotate, int Depth, PdfReference? Reference)>();
        stack.Push((root, null, null, null, 0, 0, store.Catalog["Pages"] as PdfReference));
        while (stack.Count > 0)
        {
            var (node, resources, media, crop, rotate, depth, reference) = stack.Pop();
            if (!visited.Add(node) || depth > 64)
            {
                continue;
            }

            resources = store.Get<PdfDictionary>(node, "Resources") ?? resources;
            media = Box(store, node, "MediaBox") ?? media;
            crop = Box(store, node, "CropBox") ?? crop;
            rotate = store.Get(node, "Rotate") is PdfNumber r ? r.IntValue : rotate;
            var kids = store.Get<PdfArray>(node, "Kids");
            var isPage = node["Type"] is PdfName { Value: "Page" } || kids is null;
            if (isPage)
            {
                var mediaBox = media ?? new PdfRectangle(0, 0, 612, 792);
                var cropBox = Intersect(crop ?? mediaBox, mediaBox);
                var rotation = (((rotate % 360) + 360) % 360) / 90 * 90;
                pages.Add(new PdfPage(document, pages.Count + 1, node, resources, mediaBox, cropBox, rotation, reference));
                continue;
            }

            for (var i = kids!.Count - 1; i >= 0; i--)
            {
                if (store.Resolve(kids[i]) is PdfDictionary kid)
                {
                    stack.Push((kid, resources, media, crop, rotate, depth + 1, kids[i] as PdfReference));
                }
            }
        }

        return pages;
    }

    private static PdfRectangle? Box(PdfObjectStore store, PdfDictionary node, string key)
    {
        if (store.Get<PdfArray>(node, key) is not { Count: 4 } array)
        {
            return null;
        }

        var values = array.Items.Select(i => store.Resolve(i) is PdfNumber n ? n.Value : 0).ToArray();
        return new PdfRectangle(values[0], values[1], values[2], values[3]).Normalize();
    }

    private static PdfRectangle Intersect(PdfRectangle a, PdfRectangle b)
    {
        var result = new PdfRectangle(Math.Max(a.Left, b.Left), Math.Max(a.Bottom, b.Bottom), Math.Min(a.Right, b.Right), Math.Min(a.Top, b.Top));
        return result.Width > 0 && result.Height > 0 ? result : b;
    }
}
