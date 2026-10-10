// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;

namespace OmniEurope.Documents.Pdf.Forms;

/// <summary>
/// The objects an incremental update replaces: each is a copy of the original object, made once and changed in
/// place, so a field and its merged widget, edited twice, are written once.
/// </summary>
internal sealed class IncrementalUpdate(PdfObjectStore store)
{
    private readonly SortedDictionary<int, PdfDictionary> _edited = [];

    /// <summary>The objects to append, with their generations.</summary>
    public IEnumerable<(int Number, int Generation, PdfObject Value)> Objects =>
        _edited.Select(e => (e.Key, store.GenerationOf(e.Key), (PdfObject)e.Value));

    /// <summary>The editable copy of an indirect object.</summary>
    /// <exception cref="NotSupportedException">The object is direct: only its parent could be replaced.</exception>
    public PdfDictionary Edit(PdfDictionary original, PdfReference? reference)
    {
        if (reference is null)
        {
            throw new NotSupportedException("A form object is a direct object; the PDF cannot be updated incrementally.");
        }

        if (!_edited.TryGetValue(reference.Number, out var copy))
        {
            copy = original is PdfStream stream ? new PdfStream(stream.Data) : new PdfDictionary();
            foreach (var (key, value) in original.Entries)
            {
                copy.Set(key, value);
            }

            _edited.Add(reference.Number, copy);
        }

        return copy;
    }
}
