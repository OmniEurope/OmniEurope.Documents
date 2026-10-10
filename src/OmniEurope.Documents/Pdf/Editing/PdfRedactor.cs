// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Editing;

/// <summary>An area to redact: a 1-based page and a rectangle in that page's user space (the coordinates of
/// <see cref="PdfLetter.BoundingBox"/> and <see cref="PdfPageImage.Bounds"/>).</summary>
public sealed record PdfRedaction(int PageNumber, PdfRectangle Area);

/// <summary>Options of <see cref="PdfRedactor"/>.</summary>
public sealed record PdfRedactionOptions
{
    /// <summary>The colour the areas are painted with, and that redacted pixels take; black by default.</summary>
    public PdfColor Fill { get; init; } = PdfColor.Black;

    /// <summary>Also drop the document information (only the producer is written) and the pages' XMP metadata.</summary>
    public bool RemoveMetadata { get; init; }
}

/// <summary>What a redaction removed: glyphs (with their text, for an audit), images dropped or redrawn, annotations,
/// and what it could only cover.</summary>
public sealed record PdfRedactionResult(byte[] Pdf, int RemovedGlyphs, string RemovedText, int RemovedImages, int BlankedImages, int RemovedAnnotations, IReadOnlyList<string> Gaps);

/// <summary>
/// True redaction: a new PDF in which what lies under the areas is gone from the content, not only hidden. A glyph
/// whose box meets an area is removed whole (the text after it keeps its place); an image an area covers is removed,
/// one it meets is redrawn with the pixels under the area in the fill colour; inline images an area meets and forms
/// that cannot be rewritten are removed whole; forms an area meets are rewritten; annotations whose rectangle meets
/// an area are removed; marked content around removed text loses its replacement texts. The areas are then painted.
/// The new file is written like <see cref="PdfEditor"/> writes one (document-level structures such as the outline
/// and forms are not carried over), so the original content streams are not in it. Vector graphics and shadings
/// under an area are painted over, not removed (listed in the gaps).
/// </summary>
public static class PdfRedactor
{
    private static readonly HashSet<string> PathOperators = new(StringComparer.Ordinal) { "m", "l", "c", "v", "y", "re", "sh" };

    /// <summary>Redacts the areas; the document itself is left unchanged.</summary>
    public static PdfRedactionResult Redact(PdfDocument document, IEnumerable<PdfRedaction> redactions, PdfRedactionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(redactions);
        options ??= new PdfRedactionOptions();
        var byPage = redactions.GroupBy(r => r.PageNumber).ToDictionary(g => document.GetPage(g.Key).Number, g => g.Select(r => r.Area.Normalize()).ToList());
        var table = new PdfObjectTable();
        var catalog = table.Reserve();
        var tree = table.Reserve();
        var copier = new PdfObjectCopier(table);
        var numbers = (ISet<int>)document.Pages.Select(p => p.Reference?.Number ?? -1).ToHashSet();
        foreach (var page in document.Pages)
        {
            copier.Reserve(page);
        }

        var totals = new Totals();
        var kids = new List<PdfObject>();
        foreach (var page in document.Pages)
        {
            var areas = byPage.GetValueOrDefault(page.Number);
            kids.Add(areas is null && !options.RemoveMetadata
                ? copier.CopyPage(page, tree, numbers)
                : RedactPage(page, areas ?? [], options, table, copier, tree, numbers, totals));
        }

        table.Set(tree, new PdfDictionary().SetName("Type", "Pages").Set("Kids", new PdfArray(kids)).SetNumber("Count", kids.Count));
        table.Set(catalog, new PdfDictionary().SetName("Type", "Catalog").Set("Pages", tree));
        var info = options.RemoveMetadata ? new PdfDictionary().Set("Producer", PdfString.FromText("OmniEurope.Documents")) : PdfEditor.Information(document.Information);
        using var output = new MemoryStream();
        table.Write(output, catalog, table.Add(info));
        return new PdfRedactionResult(output.ToArray(), totals.Glyphs, totals.Text.ToString(), totals.RemovedImages, totals.BlankedImages, totals.Annotations, totals.Gaps.ToList());
    }

    private static PdfReference RedactPage(PdfPage page, List<PdfRectangle> areas, PdfRedactionOptions options, PdfObjectTable table, PdfObjectCopier copier, PdfReference tree, ISet<int> numbers, Totals totals)
    {
        var store = page.Store;
        PdfObject Register(PdfStream stream)
        {
            var placeholder = new PdfDictionary();
            copier.Substitute(placeholder, table.Add(copier.CopyObject(store, stream)));
            return placeholder;
        }

        var context = new RedactionContext(store, areas, options.Fill, Register);
        var content = page.ContentBytes();
        var rewritten = areas.Count == 0 ? null : RedactionRewriter.Rewrite(context, content, page.Resources, Matrix.Identity, 0);
        var body = rewritten?.Content ?? content;
        var entries = new PdfDictionary();
        foreach (var (key, value) in page.Dictionary.Entries.Where(e => !(options.RemoveMetadata && e.Key == "Metadata")))
        {
            entries.Set(key, value);
        }

        if (areas.Count > 0)
        {
            entries.Set("Contents", Register(RedactionContext.Compressed(Painted(body, areas, options.Fill))));
            entries.Set("Annots", Annotations(store, page, areas, totals));
            if (ContentStreamReader.Read(body).Any(o => PathOperators.Contains(o.Operator)))
            {
                context.Gaps.Add("vector graphics and shadings under an area are painted over, not removed");
            }
        }

        totals.Add(context);
        return copier.CopyPage(page, tree, numbers, entries: entries, resources: rewritten?.Resources ?? page.Resources);
    }

    // The page's own drawing inside q/Q (whatever it leaves in the graphics state), then the areas filled.
    private static byte[] Painted(byte[] content, List<PdfRectangle> areas, PdfColor fill)
    {
        var rgb = FormattableString.Invariant($"{fill.R / 255.0:0.###} {fill.G / 255.0:0.###} {fill.B / 255.0:0.###} rg\n");
        var rectangles = string.Concat(areas.Select(a => FormattableString.Invariant($"{a.Left:0.###} {a.Bottom:0.###} {a.Width:0.###} {a.Height:0.###} re\n")));
        return [.. "q\n"u8, .. content, .. "\nQ\nq\n"u8, .. System.Text.Encoding.ASCII.GetBytes(rgb + rectangles + "f\nQ\n")];
    }

    private static PdfArray? Annotations(Reading.PdfObjectStore store, PdfPage page, List<PdfRectangle> areas, Totals totals)
    {
        if (store.Get<PdfArray>(page.Dictionary, "Annots") is not { } annotations)
        {
            return null;
        }

        var kept = new List<PdfObject>();
        foreach (var item in annotations.Items)
        {
            var rect = store.Resolve(item) is PdfDictionary annotation && store.Get<PdfArray>(annotation, "Rect") is { Count: 4 } r
                ? new PdfRectangle(Number(store, r[0]), Number(store, r[1]), Number(store, r[2]), Number(store, r[3])).Normalize()
                : (PdfRectangle?)null;
            if (rect is { } box && areas.Any(a => box.Left < a.Right && box.Right > a.Left && box.Bottom < a.Top && box.Top > a.Bottom))
            {
                totals.Annotations++;
            }
            else
            {
                kept.Add(item);
            }
        }

        return new PdfArray(kept);
    }

    private static double Number(Reading.PdfObjectStore store, PdfObject value) => store.Resolve(value) is PdfNumber n ? n.Value : 0;

    private sealed class Totals
    {
        public int Glyphs { get; private set; }

        public System.Text.StringBuilder Text { get; } = new();

        public int RemovedImages { get; private set; }

        public int BlankedImages { get; private set; }

        public int Annotations { get; set; }

        public SortedSet<string> Gaps { get; } = new(StringComparer.Ordinal);

        public void Add(RedactionContext context)
        {
            Glyphs += context.RemovedGlyphs;
            Text.Append(context.RemovedText);
            RemovedImages += context.RemovedImages;
            BlankedImages += context.BlankedImages;
            Gaps.UnionWith(context.Gaps);
        }
    }
}
