// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>
/// Draws the normal appearance stream of each visible annotation, fitted to its rectangle as the PDF
/// specification describes: the appearance box, transformed by its matrix, is mapped onto the rectangle.
/// Annotations without an appearance (and hidden or no-view ones) are not drawn.
/// </summary>
internal static class AnnotationPainter
{
    public static void Paint(PageRenderer renderer, PdfDictionary page, PdfDictionary? resources)
    {
        var store = renderer.Store;
        if (store.Get<PdfArray>(page, "Annots") is not { } annotations)
        {
            return;
        }

        foreach (var annotation in annotations.Items.Select(store.Resolve).OfType<PdfDictionary>())
        {
            var flags = (int)store.Number(annotation, "F");
            if ((flags & (2 | 32)) != 0 || Appearance(store, annotation) is not { } form || store.Get<PdfArray>(annotation, "Rect") is not { Count: 4 } rect)
            {
                continue;
            }

            var r = Values(store, rect);
            var box = store.Get<PdfArray>(form, "BBox") is { Count: 4 } b ? Values(store, b) : [0, 0, 1, 1];
            var matrix = Matrix.FromArray(store.Get<PdfArray>(form, "Matrix"));
            (double X, double Y)[] corners = [matrix.Transform(box[0], box[1]), matrix.Transform(box[2], box[1]), matrix.Transform(box[0], box[3]), matrix.Transform(box[2], box[3])];
            var (left, bottom) = (corners.Min(c => c.X), corners.Min(c => c.Y));
            var (width, height) = (corners.Max(c => c.X) - left, corners.Max(c => c.Y) - bottom);
            if (width <= 0 || height <= 0)
            {
                continue;
            }

            var (rectLeft, rectBottom) = (Math.Min(r[0], r[2]), Math.Min(r[1], r[3]));
            var (sx, sy) = (Math.Abs(r[2] - r[0]) / width, Math.Abs(r[3] - r[1]) / height);
            renderer.RunForm(form, resources, new Matrix(sx, 0, 0, sy, rectLeft - (left * sx), rectBottom - (bottom * sy)));
        }
    }

    // The normal appearance: a stream, or a dictionary of streams chosen by the appearance state.
    private static PdfStream? Appearance(Reading.PdfObjectStore store, PdfDictionary annotation)
    {
        var normal = store.Get(store.Get<PdfDictionary>(annotation, "AP"), "N");
        return normal switch
        {
            PdfStream stream => stream,
            PdfDictionary states when store.Get(annotation, "AS") is PdfName state => store.Get(states, state.Value) as PdfStream,
            _ => null,
        };
    }

    private static double[] Values(Reading.PdfObjectStore store, PdfArray array) =>
        array.Items.Select(i => store.Resolve(i) is PdfNumber n ? n.Value : 0).ToArray();
}
