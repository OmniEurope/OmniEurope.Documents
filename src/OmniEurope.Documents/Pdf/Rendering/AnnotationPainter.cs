// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Forms;
using OmniEurope.Documents.Pdf.Objects;

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
            if ((flags & (2 | 32)) == 0 && AnnotationAppearance.Normal(store, annotation) is { } form
                && AnnotationAppearance.Placement(store, form, annotation) is { } placement)
            {
                renderer.RunForm(form, resources, placement);
            }
        }
    }
}
