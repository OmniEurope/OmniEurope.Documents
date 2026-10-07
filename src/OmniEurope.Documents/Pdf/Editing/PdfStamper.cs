// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Pdf.Editing;

/// <summary>Where <see cref="PdfStamper.StampText"/> places its line.</summary>
public enum PdfStampPosition
{
    /// <summary>Bottom of the page.</summary>
    Bottom,

    /// <summary>Top of the page.</summary>
    Top,
}

/// <summary>Options of <see cref="PdfStamper.StampText"/>.</summary>
public sealed record PdfStampOptions
{
    /// <summary>Font. Default Liberation Sans.</summary>
    public PdfFont Font { get; init; } = PdfFont.Sans;

    /// <summary>Size in points. Default 7.</summary>
    public double Size { get; init; } = 7;

    /// <summary>Colour. Default gray.</summary>
    public PdfColor Color { get; init; } = PdfColor.Gray;

    /// <summary>Opacity from 0 to 1. Default 1.</summary>
    public double Opacity { get; init; } = 1;

    /// <summary>Top or bottom.</summary>
    public PdfStampPosition Position { get; init; } = PdfStampPosition.Bottom;

    /// <summary>Horizontal alignment.</summary>
    public PdfTextAlignment Alignment { get; init; } = PdfTextAlignment.Left;

    /// <summary>Distance from the page edges in points. Default 14 vertically, 36 horizontally.</summary>
    public (double Horizontal, double Vertical) Margin { get; init; } = (36, 14);
}

/// <summary>
/// Draws over the pages of an existing PDF without rewriting it: the original bytes are kept and an
/// incremental update appends the drawing (signatures and other revisions stay intact). Coordinates are
/// those of a <see cref="PdfCanvas"/> on the page's crop box, before page rotation. Encrypted and damaged
/// files are refused with <see cref="NotSupportedException"/>: the caller decides what to do with them.
/// </summary>
public static class PdfStamper
{
    private const string Prefix = "OEStamp";

    /// <summary>Calls <paramref name="draw"/> for each selected page and appends what it drew.</summary>
    public static byte[] Stamp(byte[] original, Action<PdfCanvas, PdfPage> draw, Func<PdfPage, bool>? pages = null, FontLibrary? fonts = null)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(draw);
        var document = PdfDocument.Open(original);
        var store = document.Store;
        if (store.IsEncrypted)
        {
            throw new NotSupportedException("Encrypted PDFs cannot be stamped.");
        }

        if (store.Repaired || store.StartXref <= 0)
        {
            throw new NotSupportedException("The PDF's cross-reference information is damaged; it cannot be updated incrementally.");
        }

        // Resource names unique to this update, so stamping a stamped file never overwrites earlier stamps.
        var builder = new PdfDocumentBuilder(store.NextObjectNumber, Prefix + store.NextObjectNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_") { Fonts = fonts ?? FontLibrary.Default };
        var drawn = new List<(PdfPage Page, PdfCanvas Canvas)>();
        foreach (var page in document.Pages.Where(p => pages?.Invoke(p) ?? true))
        {
            if (page.Reference is null)
            {
                throw new NotSupportedException("A page is a direct object; the PDF cannot be updated incrementally.");
            }

            var canvas = builder.CreateDetachedCanvas(page.CropBox.Width, page.CropBox.Height);
            draw(canvas, page);
            drawn.Add((page, canvas));
        }

        builder.WriteFonts();
        var table = builder.Table;
        var updates = new List<(int, int, PdfObject)>();
        foreach (var (page, canvas) in drawn)
        {
            updates.Add((page.Reference!.Number, store.GenerationOf(page.Reference.Number), UpdatedPage(page, canvas, table)));
        }

        var objects = table.Objects.Select(o => (o.Number, 0, o.Value)).Concat(updates).ToList();
        return PdfIncrementalWriter.Append(original, store, objects);
    }

    /// <summary>Writes one line of text at the top or bottom of every page (a footer, a watermark notice).</summary>
    public static byte[] StampText(byte[] original, string text, PdfStampOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        options ??= new PdfStampOptions();
        return Stamp(original, (canvas, _) =>
        {
            var metrics = canvas.Metrics(options.Font, options.Size);
            var baseline = options.Position == PdfStampPosition.Top
                ? options.Margin.Vertical + metrics.Ascent
                : canvas.Height - options.Margin.Vertical;
            var width = canvas.MeasureText(text, options.Font, options.Size);
            var available = canvas.Width - (2 * options.Margin.Horizontal);
            var x = options.Alignment switch
            {
                PdfTextAlignment.Center => options.Margin.Horizontal + ((available - width) / 2),
                PdfTextAlignment.Right => canvas.Width - options.Margin.Horizontal - width,
                _ => options.Margin.Horizontal,
            };
            canvas.DrawText(text, x, baseline, options.Font, options.Size, options.Color, opacity: options.Opacity);
        });
    }

    private static PdfDictionary UpdatedPage(PdfPage page, PdfCanvas canvas, PdfObjectTable table)
    {
        var store = page.Store;
        var before = table.Add(new PdfStream("q\n"u8.ToArray()));
        var box = page.CropBox;
        var content = Encoding.ASCII.GetBytes($"Q\nq 1 0 0 1 {PdfFormat.Real(box.Left)} {PdfFormat.Real(box.Bottom)} cm\n");
        var after = table.Add(EmbeddedFont.Stream([.. content, .. canvas.FinishContent(), .. "\nQ\n"u8]));
        var original = page.Dictionary["Contents"] switch
        {
            PdfArray array => array.Items,
            { } single => [single],
            _ => new List<PdfObject>(),
        };
        var updated = new PdfDictionary();
        foreach (var (key, value) in page.Dictionary.Entries)
        {
            updated.Set(key, value);
        }

        updated.Set("Contents", new PdfArray([before, .. original, after]));
        updated.Set("Resources", MergeResources(store, page.Resources, canvas.BuildResources()));
        if (canvas.Annotations.Count > 0)
        {
            var annotations = store.Get<PdfArray>(page.Dictionary, "Annots")?.Items ?? [];
            updated.Set("Annots", new PdfArray([.. annotations, .. canvas.Annotations.Select(a => (PdfObject)table.Add(a))]));
        }

        return updated;
    }

    private static PdfDictionary MergeResources(Reading.PdfObjectStore store, PdfDictionary? original, PdfDictionary added)
    {
        var merged = new PdfDictionary();
        foreach (var (key, value) in original?.Entries ?? [])
        {
            merged.Set(key, value);
        }

        foreach (var (category, entries) in added.Entries)
        {
            var combined = new PdfDictionary();
            if (store.Get<PdfDictionary>(original, category) is { } existing)
            {
                foreach (var (name, value) in existing.Entries)
                {
                    combined.Set(name, value);
                }
            }

            foreach (var (name, value) in ((PdfDictionary)entries).Entries)
            {
                combined.Set(name, value);
            }

            merged.Set(category, combined);
        }

        return merged;
    }
}
