// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;

namespace OmniEurope.Documents.Pdf.Editing;

/// <summary>
/// Copies pages and everything they reference from a source document into a new object table. Each source
/// object is copied once (shared fonts and images stay shared). References to pages that are not copied
/// (links, outline targets) become null instead of dragging those pages along. Strings and streams arrive
/// decrypted, so the output is never encrypted.
/// </summary>
internal sealed class PdfObjectCopier(PdfObjectTable target, Func<PdfObjectStore, PdfStream, PdfStream>? transformStream = null)
{
    private const int MaxDepth = 256;
    private readonly Dictionary<(PdfObjectStore, int), PdfReference> _copied = [];
    private readonly Queue<(PdfObjectStore Store, PdfObject Source, PdfReference Target, ISet<int>? Pages)> _pending = new();
    private readonly Dictionary<PdfObject, PdfObject> _substitutes = new(ReferenceEqualityComparer.Instance);

    /// <summary>Makes every later copy of <paramref name="placeholder"/> (by identity) the target object <paramref name="replacement"/>.</summary>
    public void Substitute(PdfObject placeholder, PdfObject replacement) => _substitutes[placeholder] = replacement;

    /// <summary>Copies a whole object graph (a catalog with its page tree, an information dictionary): every
    /// reference is followed and parents are kept.</summary>
    public PdfObject CopyObject(PdfObjectStore store, PdfObject value)
    {
        var copy = Copy(store, value, null, 0);
        Drain();
        return copy;
    }

    /// <summary>Copies a page: inherited attributes become its own, its parent becomes <paramref name="parent"/>;
    /// <paramref name="entries"/> and <paramref name="resources"/> stand for the page's own when given.</summary>
    public PdfReference CopyPage(PdfPage page, PdfReference parent, ISet<int> copiedPageNumbers, int? rotate = null, PdfDictionary? entries = null, PdfDictionary? resources = null)
    {
        var store = page.Store;
        var reference = page.Reference is { } r ? Map(store, r) : target.Reserve();
        var copy = new PdfDictionary();
        foreach (var (key, value) in (entries ?? page.Dictionary).Entries)
        {
            // Resources are copied below (the inherited ones, or the replacement given): copying them here too would write unused objects.
            if (key is "Parent" or "B" or "StructParents" or "PieceInfo" or "Resources")
            {
                continue;
            }

            copy.Set(key, Copy(store, value, copiedPageNumbers, 0));
        }

        copy.SetName("Type", "Page").Set("Parent", parent);
        var ownResources = resources ?? page.Resources;
        copy.Set("Resources", ownResources is null ? new PdfDictionary() : Copy(store, ownResources, copiedPageNumbers, 0));
        copy.Set("MediaBox", Box(page.MediaBox));
        if (!page.CropBox.Equals(page.MediaBox))
        {
            copy.Set("CropBox", Box(page.CropBox));
        }

        var rotation = rotate ?? page.Rotation;
        copy.Set("Rotate", rotation == 0 ? null : PdfNumber.Of(rotation));
        target.Set(reference, copy);
        Drain();
        return reference;
    }

    /// <summary>Reserves the target references of the pages to copy, so links between them survive.</summary>
    public void Reserve(PdfPage page)
    {
        if (page.Reference is { } reference)
        {
            Map(page.Store, reference);
        }
    }

    private PdfReference Map(PdfObjectStore store, PdfReference source)
    {
        if (!_copied.TryGetValue((store, source.Number), out var mapped))
        {
            mapped = target.Reserve();
            _copied[(store, source.Number)] = mapped;
        }

        return mapped;
    }

    private PdfObject Copy(PdfObjectStore store, PdfObject value, ISet<int>? pages, int depth)
    {
        if (depth > MaxDepth)
        {
            return PdfNull.Instance;
        }

        if (_substitutes.TryGetValue(value, out var substitute))
        {
            return substitute;
        }

        switch (value)
        {
            case PdfReference reference:
                return CopyReference(store, reference, pages, depth);
            case PdfStream stream:
                var source = transformStream?.Invoke(store, stream) ?? stream;
                var streamCopy = new PdfStream(source.Data);
                CopyEntries(store, source, streamCopy, pages, depth);
                return streamCopy;
            case PdfDictionary dictionary:
                var dictionaryCopy = new PdfDictionary();
                CopyEntries(store, dictionary, dictionaryCopy, pages, depth);
                return dictionaryCopy;
            case PdfArray array:
                return new PdfArray(array.Items.Select(item => Copy(store, item, pages, depth + 1)));
            default:
                return value;
        }
    }

    private PdfObject CopyReference(PdfObjectStore store, PdfReference reference, ISet<int>? pages, int depth)
    {
        if (_copied.TryGetValue((store, reference.Number), out var existing))
        {
            return existing;
        }

        var resolved = store.Resolve(reference);
        if (resolved is null)
        {
            return PdfNull.Instance;
        }

        if (pages is not null && resolved is PdfDictionary { } dictionary && dictionary["Type"] is PdfName { Value: "Page" or "Pages" } && !pages.Contains(reference.Number))
        {
            return PdfNull.Instance;
        }

        // Copied later from the queue: long reference chains never deepen the call stack.
        var mapped = Map(store, reference);
        _pending.Enqueue((store, resolved, mapped, pages));
        return mapped;
    }

    private void CopyEntries(PdfObjectStore store, PdfDictionary source, PdfDictionary destination, ISet<int>? pages, int depth)
    {
        foreach (var (key, value) in source.Entries)
        {
            if (pages is not null && key == "Parent" && value is PdfReference parent && store.Resolve(parent) is PdfDictionary { } p && p["Type"] is PdfName { Value: "Pages" })
            {
                continue;
            }

            destination.Set(key, Copy(store, value, pages, depth + 1));
        }
    }

    private void Drain()
    {
        while (_pending.TryDequeue(out var item))
        {
            target.Set(item.Target, Copy(item.Store, item.Source, item.Pages, 0));
        }
    }

    private static PdfArray Box(PdfRectangle box) => PdfArray.OfNumbers(box.Left, box.Bottom, box.Right, box.Top);
}
