// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Pdf.Forms;
using OmniEurope.Documents.Pdf.Objects;

namespace OmniEurope.Documents.Pdf.Signing;

/// <summary>
/// The objects a signature adds (ISO 32000-1 §12.7.4.5, §12.8.1): the signature dictionary with its placeholders, a
/// signature field merged with an invisible, printable and locked widget on the first page, the page's annotation
/// list and the form's field list with <c>SigFlags</c> 3 (signatures exist, append only).
/// </summary>
internal static class SignatureField
{
    /// <summary>The byte range written first, as wide as any final one; the signer overwrites it in place.</summary>
    public const string ByteRangePlaceholder = "[0 9999999999 9999999999 9999999999]";

    public static List<(int Number, int Generation, PdfObject Value)> Prepare(PdfDocument document, PdfSignatureOptions options)
    {
        var store = document.Store;
        var page = document.Pages.FirstOrDefault() ?? throw new NotSupportedException("The PDF has no page to hold a signature field.");
        var table = new PdfObjectTable(store.NextObjectNumber);
        var update = new IncrementalUpdate(store);
        var signature = new PdfDictionary()
            .SetName("Type", "Sig")
            .SetName("Filter", "Adobe.PPKLite")
            .SetName("SubFilter", "ETSI.CAdES.detached")
            .Set("ByteRange", PdfArray.OfNumbers(0, 9999999999, 9999999999, 9999999999))
            .Set("Contents", new PdfString(new byte[options.ReservedSize], hex: true))
            .Set("M", PdfString.FromText(Date(options.SigningTime ?? DateTimeOffset.Now)));
        Text(signature, "Name", options.Name);
        Text(signature, "Reason", options.Reason);
        Text(signature, "Location", options.Location);
        Text(signature, "ContactInfo", options.ContactInfo);
        var names = FormTree.Collect(document).Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        var name = Enumerable.Range(1, names.Count + 1).Select(i => "Signature" + i.ToString(CultureInfo.InvariantCulture)).First(n => !names.Contains(n));
        var field = table.Add(new PdfDictionary()
            .SetName("FT", "Sig").Set("T", PdfString.FromText(name)).Set("V", table.Add(signature))
            .SetName("Type", "Annot").SetName("Subtype", "Widget").Set("Rect", PdfArray.OfNumbers(0, 0, 0, 0)).SetNumber("F", 132)
            .Set("P", page.Reference ?? throw new NotSupportedException("The first page is a direct object; the PDF cannot be updated incrementally.")));
        update.Edit(page.Dictionary, page.Reference).Set("Annots", new PdfArray([.. store.Get<PdfArray>(page.Dictionary, "Annots")?.Items ?? [], field]));
        AddToForm(store, update, field);
        return [.. table.Objects.Select(o => (o.Number, 0, o.Value)), .. update.Objects];
    }

    // The field joins the form's Fields; a form that is a direct object of the catalog is rewritten with it.
    private static void AddToForm(Reading.PdfObjectStore store, IncrementalUpdate update, PdfReference field)
    {
        var existing = FormTree.AcroForm(store);
        if (store.Catalog["AcroForm"] is PdfReference reference && existing is not null)
        {
            Extend(store, update.Edit(existing, reference), field);
            return;
        }

        var form = Copy(existing);
        Extend(store, form, field);
        update.Edit(store.Catalog, store.Trailer["Root"] as PdfReference).Set("AcroForm", form);
    }

    private static void Extend(Reading.PdfObjectStore store, PdfDictionary form, PdfReference field) =>
        form.Set("Fields", new PdfArray([.. store.Get<PdfArray>(form, "Fields")?.Items ?? [], field])).SetNumber("SigFlags", 3);

    private static PdfDictionary Copy(PdfDictionary? original)
    {
        var copy = new PdfDictionary();
        foreach (var (key, value) in original?.Entries ?? [])
        {
            copy.Set(key, value);
        }

        return copy;
    }

    private static void Text(PdfDictionary dictionary, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            dictionary.Set(key, PdfString.FromText(value));
        }
    }

    private static string Date(DateTimeOffset date)
    {
        var offset = date.Offset;
        var sign = offset < TimeSpan.Zero ? '-' : '+';
        return string.Create(CultureInfo.InvariantCulture, $"D:{date:yyyyMMddHHmmss}{sign}{Math.Abs(offset.Hours):00}'{Math.Abs(offset.Minutes):00}'");
    }
}
