// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;

namespace OmniEurope.Documents.Pdf.Text;

/// <summary>An image drawn on a page: where (the image's unit square through the current matrix), its pixel
/// size and its compression.</summary>
public sealed class PdfPageImage
{
    private readonly PdfObjectStore _store;
    private readonly PdfDictionary _dictionary;
    private readonly byte[]? _inlineData;
    private readonly PdfDictionary? _resources;

    internal PdfPageImage(PdfObjectStore store, PdfDictionary dictionary, byte[]? inlineData, PdfDictionary? resources, PdfRectangle bounds)
    {
        _store = store;
        _dictionary = dictionary;
        _inlineData = inlineData;
        _resources = resources;
        Bounds = bounds;
        PixelWidth = (int)store.Number(dictionary, "Width", store.Number(dictionary, "W"));
        PixelHeight = (int)store.Number(dictionary, "Height", store.Number(dictionary, "H"));
        Filters = Names(store.Get(dictionary, "Filter") ?? store.Get(dictionary, "F"));
    }

    /// <summary>Where the image is drawn (user space).</summary>
    public PdfRectangle Bounds { get; }

    /// <summary>Width in samples.</summary>
    public int PixelWidth { get; }

    /// <summary>Height in samples.</summary>
    public int PixelHeight { get; }

    /// <summary>The filters of the image data (DCTDecode for JPEG...).</summary>
    public IReadOnlyList<string> Filters { get; }

    /// <summary>True for an inline image (BI ... EI).</summary>
    public bool IsInline => _inlineData is not null;

    /// <summary>The decoded pixels (with the soft mask as alpha), or null for an encoding that cannot be
    /// decoded here (JPEG 2000 features this package does not decode) or broken data.</summary>
    public RasterImage? Decode() => PdfImageDecoder.TryDecode(_store, _dictionary, _inlineData, _resources);

    /// <summary>The image as PNG bytes, or null when it cannot be decoded.</summary>
    public byte[]? ToPng() => Decode() is { } image ? PngCodec.Encode(image) : null;

    private static IReadOnlyList<string> Names(PdfObject? value) => value switch
    {
        PdfName name => [name.Value],
        PdfArray array => array.Items.OfType<PdfName>().Select(n => n.Value).ToList(),
        _ => [],
    };
}

/// <summary>Finds the images a page draws, through form XObjects too.</summary>
internal static class PageImages
{
    public static IReadOnlyList<PdfPageImage> Collect(PdfPage page)
    {
        var images = new List<PdfPageImage>();
        Walk(page.Store, page.ContentBytes(), page.Resources, Matrix.Identity, images, 0, new HashSet<PdfStream>(ReferenceEqualityComparer.Instance));
        return images;
    }

    private static void Walk(PdfObjectStore store, byte[] content, PdfDictionary? resources, Matrix ctm, List<PdfPageImage> images, int depth, HashSet<PdfStream> active)
    {
        var saved = new Stack<Matrix>();
        foreach (var operation in ContentStreamReader.Read(content))
        {
            switch (operation.Operator)
            {
                case "q":
                    saved.Push(ctm);
                    break;
                case "Q" when saved.Count > 0:
                    ctm = saved.Pop();
                    break;
                case "cm":
                    ctm = Matrix.FromOperands(operation.Operands).Multiply(ctm);
                    break;
                case "BI" when operation.Operands.Count == 1 && operation.Operands[0] is PdfDictionary inline:
                    images.Add(new PdfPageImage(store, inline, operation.InlineData, resources, Bounds(ctm)));
                    break;
                case "Do" when operation.Operands.Count == 1 && operation.Operands[0] is PdfName name:
                    var xobject = store.Get(store.Get<PdfDictionary>(resources, "XObject"), name.Value) as PdfStream;
                    WalkXObject(store, xobject, resources, ctm, images, depth, active);
                    break;
            }
        }
    }

    // An image XObject is collected; a form XObject is walked in turn (12 levels at most, never itself).
    private static void WalkXObject(PdfObjectStore store, PdfStream? xobject, PdfDictionary? resources, Matrix ctm, List<PdfPageImage> images, int depth, HashSet<PdfStream> active)
    {
        var subtype = store.Get(xobject, "Subtype") is PdfName s ? s.Value : null;
        if (subtype == "Image")
        {
            images.Add(new PdfPageImage(store, xobject!, null, resources, Bounds(ctm)));
        }
        else if (subtype == "Form" && depth < 12 && active.Add(xobject!))
        {
            var matrix = Matrix.FromArray(store.Get<PdfArray>(xobject, "Matrix")).Multiply(ctm);
            Walk(store, store.DecodeBytes(xobject!), store.Get<PdfDictionary>(xobject, "Resources") ?? resources, matrix, images, depth + 1, active);
            active.Remove(xobject!);
        }
    }

    private static PdfRectangle Bounds(Matrix ctm)
    {
        var corners = new[] { ctm.Transform(0, 0), ctm.Transform(1, 0), ctm.Transform(0, 1), ctm.Transform(1, 1) };
        return new PdfRectangle(corners.Min(c => c.X), corners.Min(c => c.Y), corners.Max(c => c.X), corners.Max(c => c.Y));
    }
}
