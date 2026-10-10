// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;

namespace OmniEurope.Documents.Pdf.Forms;

/// <summary>A widget annotation of a terminal field, with its object reference and page.</summary>
internal sealed record FormWidgetNode(PdfDictionary Dictionary, PdfReference? Reference, int PageNumber);

/// <summary>A terminal field with its inherited attributes (ISO 32000-1 §12.7.3.1, table 220).</summary>
internal sealed record FormFieldNode(PdfDictionary Dictionary, PdfReference? Reference, string Name, string? Type, int Flags, PdfObject? Value,
    string? DefaultAppearance, int Quadding, IReadOnlyList<FormWidgetNode> Widgets)
{
    public PdfFieldKind Kind => Type switch
    {
        "Tx" => PdfFieldKind.Text,
        "Ch" => (Flags & (1 << 17)) != 0 ? PdfFieldKind.ComboBox : PdfFieldKind.ListBox,
        "Sig" => PdfFieldKind.Signature,
        _ when (Flags & (1 << 16)) != 0 => PdfFieldKind.PushButton,
        _ when (Flags & (1 << 15)) != 0 => PdfFieldKind.RadioButton,
        _ => PdfFieldKind.CheckBox,
    };
}

/// <summary>Walks the field tree of the document's interactive form (ISO 32000-1 §12.7.3).</summary>
internal static class FormTree
{
    private const int MaxDepth = 32;

    private sealed record Inherited(string Name, string? Type, int Flags, PdfObject? Value, string? DefaultAppearance, int Quadding);

    /// <summary>The interactive form dictionary of the catalog, or null.</summary>
    public static PdfDictionary? AcroForm(PdfObjectStore store) => store.Get<PdfDictionary>(store.Catalog, "AcroForm");

    /// <summary>The terminal fields in document order.</summary>
    public static List<FormFieldNode> Collect(PdfDocument document)
    {
        var store = document.Store;
        var fields = new List<FormFieldNode>();
        if (store.Get<PdfArray>(AcroForm(store), "Fields") is not { } roots)
        {
            return fields;
        }

        var pages = PageOfAnnotations(document);
        var root = new Inherited(string.Empty, null, 0, null, (store.Get(AcroForm(store), "DA") as PdfString)?.ToText(), (int)store.Number(AcroForm(store), "Q"));
        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<(PdfObject Item, Inherited Parent, int Depth)>();
        foreach (var item in Enumerable.Reverse(roots.Items))
        {
            stack.Push((item, root, 0));
        }

        while (stack.Count > 0)
        {
            var (item, parent, depth) = stack.Pop();
            if (store.Resolve(item) is not PdfDictionary dictionary || depth > MaxDepth || !visited.Add(dictionary))
            {
                continue;
            }

            var inherited = Inherit(store, dictionary, parent);
            var kids = store.Get<PdfArray>(dictionary, "Kids")?.Items ?? [];
            var children = kids.Where(k => store.Resolve(k) is PdfDictionary kid && kid.ContainsKey("T")).ToList();
            if (children.Count > 0)
            {
                foreach (var child in Enumerable.Reverse(children))
                {
                    stack.Push((child, inherited, depth + 1));
                }

                continue;
            }

            fields.Add(Terminal(store, dictionary, item as PdfReference, inherited, kids, pages));
        }

        return fields;
    }

    private static Inherited Inherit(PdfObjectStore store, PdfDictionary dictionary, Inherited parent)
    {
        var partial = (store.Get(dictionary, "T") as PdfString)?.ToText();
        var name = partial is null ? parent.Name : parent.Name.Length == 0 ? partial : parent.Name + "." + partial;
        return new Inherited(
            name,
            (store.Get(dictionary, "FT") as PdfName)?.Value ?? parent.Type,
            store.Get(dictionary, "Ff") is PdfNumber flags ? flags.IntValue : parent.Flags,
            store.Get(dictionary, "V") ?? parent.Value,
            (store.Get(dictionary, "DA") as PdfString)?.ToText() ?? parent.DefaultAppearance,
            store.Get(dictionary, "Q") is PdfNumber q ? q.IntValue : parent.Quadding);
    }

    private static FormFieldNode Terminal(PdfObjectStore store, PdfDictionary dictionary, PdfReference? reference, Inherited inherited, List<PdfObject> kids,
        Dictionary<int, int> pages)
    {
        // A terminal field is its own widget (merged dictionaries) unless its kids are the widgets.
        var widgets = kids.Count == 0
            ? [new FormWidgetNode(dictionary, reference, PageOf(pages, reference))]
            : kids.Select(k => (Kid: k, Dictionary: store.Resolve(k) as PdfDictionary))
                .Where(k => k.Dictionary is not null)
                .Select(k => new FormWidgetNode(k.Dictionary!, k.Kid as PdfReference, PageOf(pages, k.Kid as PdfReference)))
                .ToList();
        return new FormFieldNode(dictionary, reference, inherited.Name, inherited.Type, inherited.Flags, inherited.Value, inherited.DefaultAppearance,
            inherited.Quadding, widgets);
    }

    private static int PageOf(Dictionary<int, int> pages, PdfReference? reference) =>
        reference is not null && pages.TryGetValue(reference.Number, out var page) ? page : 0;

    // The page listing each annotation object in its Annots array.
    private static Dictionary<int, int> PageOfAnnotations(PdfDocument document)
    {
        var pages = new Dictionary<int, int>();
        foreach (var page in document.Pages)
        {
            foreach (var annotation in document.Store.Get<PdfArray>(page.Dictionary, "Annots")?.Items.OfType<PdfReference>() ?? [])
            {
                pages.TryAdd(annotation.Number, page.Number);
            }
        }

        return pages;
    }
}
