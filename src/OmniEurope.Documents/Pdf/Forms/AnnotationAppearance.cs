// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Forms;

/// <summary>The normal appearance of an annotation and where it lands on the page (ISO 32000-1 §12.5.5).</summary>
internal static class AnnotationAppearance
{
    /// <summary>The normal appearance: a stream, or the stream of a dictionary chosen by the appearance state.</summary>
    public static PdfStream? Normal(PdfObjectStore store, PdfDictionary annotation) => Normal(store, annotation, out _);

    /// <summary>The normal appearance and, when it is an indirect object, its reference.</summary>
    public static PdfStream? Normal(PdfObjectStore store, PdfDictionary annotation, out PdfReference? reference)
    {
        reference = null;
        var entry = store.Get<PdfDictionary>(annotation, "AP")?["N"];
        if (store.Resolve(entry) is PdfDictionary states and not PdfStream)
        {
            entry = store.Get(annotation, "AS") is PdfName state ? states[state.Value] : null;
        }

        reference = entry as PdfReference;
        return store.Resolve(entry) as PdfStream;
    }

    /// <summary>
    /// The matrix that draws an appearance form inside the annotation rectangle: its box, transformed by its own
    /// matrix, is mapped onto the rectangle (algorithm 8.1). Null when the box or the rectangle is empty.
    /// </summary>
    public static Matrix? Placement(PdfObjectStore store, PdfStream form, PdfDictionary annotation)
    {
        if (store.Get<PdfArray>(annotation, "Rect") is not { Count: 4 } rect)
        {
            return null;
        }

        var r = Values(store, rect);
        var box = store.Get<PdfArray>(form, "BBox") is { Count: 4 } b ? Values(store, b) : [0, 0, 1, 1];
        var matrix = Matrix.FromArray(store.Get<PdfArray>(form, "Matrix"));
        (double X, double Y)[] corners = [matrix.Transform(box[0], box[1]), matrix.Transform(box[2], box[1]), matrix.Transform(box[0], box[3]), matrix.Transform(box[2], box[3])];
        var (left, bottom) = (corners.Min(c => c.X), corners.Min(c => c.Y));
        var (width, height) = (corners.Max(c => c.X) - left, corners.Max(c => c.Y) - bottom);
        var (rectWidth, rectHeight) = (Math.Abs(r[2] - r[0]), Math.Abs(r[3] - r[1]));
        if (width <= 0 || height <= 0 || rectWidth <= 0 || rectHeight <= 0)
        {
            return null;
        }

        var (rectLeft, rectBottom) = (Math.Min(r[0], r[2]), Math.Min(r[1], r[3]));
        var (sx, sy) = (rectWidth / width, rectHeight / height);
        return new Matrix(sx, 0, 0, sy, rectLeft - (left * sx), rectBottom - (bottom * sy));
    }

    /// <summary>The rectangle of an annotation, corners ordered (empty when missing).</summary>
    public static PdfRectangle Rectangle(PdfObjectStore store, PdfDictionary annotation) =>
        store.Get<PdfArray>(annotation, "Rect") is { Count: 4 } rect && Values(store, rect) is var r
            ? new PdfRectangle(r[0], r[1], r[2], r[3]).Normalize()
            : default;

    private static double[] Values(PdfObjectStore store, PdfArray array) =>
        array.Items.Select(i => store.Resolve(i) is PdfNumber n ? n.Value : 0).ToArray();
}
