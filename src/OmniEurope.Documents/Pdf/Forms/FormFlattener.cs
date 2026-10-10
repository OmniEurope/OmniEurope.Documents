// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using OmniEurope.Documents.Pdf.Editing;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Pdf.Forms;

/// <summary>
/// Draws the normal appearance of every widget that is not hidden into its page's content, as a form XObject
/// placed on the widget rectangle (ISO 32000-1 §12.5.5), then removes the widgets from the pages and the
/// interactive form from the catalog, in one incremental update.
/// </summary>
internal static class FormFlattener
{
    public static byte[] Flatten(byte[] original)
    {
        var document = PdfForm.OpenForUpdate(original);
        var store = document.Store;
        var table = new PdfObjectTable(store.NextObjectNumber);
        var update = new IncrementalUpdate(store);
        var prefix = "OEFlat" + store.NextObjectNumber.ToString(CultureInfo.InvariantCulture) + "_";
        foreach (var page in document.Pages)
        {
            FlattenPage(store, page, table, update, prefix);
        }

        if (FormTree.AcroForm(store) is not null)
        {
            update.Edit(store.Catalog, store.Trailer["Root"] as PdfReference).Remove("AcroForm");
        }

        var objects = table.Objects.Select(o => (o.Number, 0, o.Value)).Concat(update.Objects).ToList();
        return objects.Count == 0 ? [.. original] : PdfIncrementalWriter.Append(original, store, objects);
    }

    private static void FlattenPage(PdfObjectStore store, PdfPage page, PdfObjectTable table, IncrementalUpdate update, string prefix)
    {
        var annotations = store.Get<PdfArray>(page.Dictionary, "Annots")?.Items ?? [];
        var widgets = annotations.Where(a => store.Resolve(a) is PdfDictionary d && store.Get(d, "Subtype") is PdfName { Value: "Widget" }).ToList();
        if (widgets.Count == 0)
        {
            return;
        }

        var content = new StringBuilder("Q\n");
        var forms = new PdfDictionary();
        foreach (var widget in widgets.Select(w => (PdfDictionary)store.Resolve(w)!))
        {
            Draw(store, widget, update, prefix, content, forms);
        }

        var edited = update.Edit(page.Dictionary, page.Reference);
        var contents = page.Dictionary["Contents"];
        List<PdfObject> parts = store.Resolve(contents) switch
        {
            PdfArray array => array.Items,
            null => [],
            _ => [contents!],
        };
        var before = table.Add(new PdfStream("q\n"u8.ToArray()));
        var after = table.Add(EmbeddedFont.Stream(Encoding.ASCII.GetBytes(content.ToString())));
        edited.Set("Contents", new PdfArray([before, .. parts, after]));
        edited.Set("Resources", PdfStamper.MergeResources(store, page.Resources, new PdfDictionary().Set("XObject", forms)));
        var kept = annotations.Except(widgets).ToList();
        edited.Set("Annots", kept.Count > 0 ? new PdfArray(kept) : null);
    }

    // A widget that is not hidden and has a normal appearance is drawn with it, as a form placed on its rectangle.
    private static void Draw(PdfObjectStore store, PdfDictionary widget, IncrementalUpdate update, string prefix, StringBuilder content, PdfDictionary forms)
    {
        if (((int)store.Number(widget, "F") & 2) != 0 || AnnotationAppearance.Normal(store, widget, out var reference) is not { } form || reference is null
            || AnnotationAppearance.Placement(store, form, widget) is not { } m)
        {
            return;
        }

        if (store.Get(form, "Subtype") is not PdfName { Value: "Form" })
        {
            update.Edit(form, reference).SetName("Type", "XObject").SetName("Subtype", "Form");
        }

        var name = prefix + forms.Entries.Count.ToString(CultureInfo.InvariantCulture);
        forms.Set(name, reference);
        content.Append(CultureInfo.InvariantCulture, $"q {PdfFormat.Real(m.A)} {PdfFormat.Real(m.B)} {PdfFormat.Real(m.C)} {PdfFormat.Real(m.D)} {PdfFormat.Real(m.E)} {PdfFormat.Real(m.F)} cm /{name} Do Q\n");
    }
}
