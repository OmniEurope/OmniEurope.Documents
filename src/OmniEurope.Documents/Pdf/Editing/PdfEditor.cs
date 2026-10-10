// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Pdf.Editing;

/// <summary>
/// Page-level operations that produce a new PDF: merge, extract (in any order, so also reorder),
/// split, remove, rotate. Pages keep their content, resources, annotations and inherited attributes;
/// document-level structures that name pages (outline, forms, tagged structure) are not carried over.
/// The output is encrypted only by <see cref="Encrypt"/>.
/// </summary>
public static class PdfEditor
{
    /// <summary>Concatenates the pages of <paramref name="documents"/>; metadata comes from the first.</summary>
    public static byte[] Merge(params IEnumerable<PdfDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var list = documents.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("Nothing to merge.", nameof(documents));
        }

        return Build(list.SelectMany(d => d.Pages.Select(p => (p, (int?)null))).ToList(), list[0]);
    }

    /// <summary>A new PDF with the given 1-based pages, in the given order (pages may repeat).</summary>
    public static byte[] ExtractPages(PdfDocument document, IEnumerable<int> pageNumbers)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(pageNumbers);
        return Build(pageNumbers.Select(n => (document.GetPage(n), (int?)null)).ToList(), document);
    }

    /// <summary>A new PDF with the pages of a range expression such as <c>1-3,5,8-</c>.</summary>
    public static byte[] ExtractPages(PdfDocument document, string ranges) => ExtractPages(document, PageRanges.Parse(ranges, document.PageCount));

    /// <summary>One PDF per group of <paramref name="pagesPerPart"/> pages.</summary>
    public static IReadOnlyList<byte[]> Split(PdfDocument document, int pagesPerPart = 1)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentOutOfRangeException.ThrowIfLessThan(pagesPerPart, 1);
        return Enumerable.Range(1, document.PageCount).Chunk(pagesPerPart).Select(chunk => ExtractPages(document, chunk)).ToList();
    }

    /// <summary>One PDF per range expression (<c>"1-2", "3-"</c>).</summary>
    public static IReadOnlyList<byte[]> SplitByRanges(PdfDocument document, params IEnumerable<string> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        return ranges.Select(r => ExtractPages(document, r)).ToList();
    }

    /// <summary>A new PDF without the given 1-based pages.</summary>
    public static byte[] RemovePages(PdfDocument document, IEnumerable<int> pageNumbers)
    {
        ArgumentNullException.ThrowIfNull(document);
        var removed = pageNumbers.ToHashSet();
        var kept = Enumerable.Range(1, document.PageCount).Where(n => !removed.Contains(n)).ToList();
        if (kept.Count == 0)
        {
            throw new ArgumentException("A PDF cannot be left without pages.", nameof(pageNumbers));
        }

        return ExtractPages(document, kept);
    }

    /// <summary>A new PDF where the given pages are turned by <paramref name="degrees"/> (a multiple of 90, clockwise).</summary>
    public static byte[] RotatePages(PdfDocument document, IEnumerable<int> pageNumbers, int degrees)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (degrees % 90 != 0)
        {
            throw new ArgumentException("Rotation must be a multiple of 90 degrees.", nameof(degrees));
        }

        var turned = pageNumbers.ToHashSet();
        return Build(document.Pages.Select(p => (p, turned.Contains(p.Number) ? (int?)((((p.Rotation + degrees) % 360) + 360) % 360) : null)).ToList(), document);
    }

    /// <summary>A copy of every page encrypted with AES-256 (<see cref="PdfEncryption"/>), the metadata kept.</summary>
    /// <exception cref="ArgumentException">A password SASLprep refuses, or an empty owner password.</exception>
    public static byte[] Encrypt(PdfDocument document, PdfEncryption encryption)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(encryption);
        return Build(document.Pages.Select(p => (p, (int?)null)).ToList(), document, PdfEncryptor.Create(encryption));
    }

    private static byte[] Build(List<(PdfPage Page, int? Rotate)> pages, PdfDocument metadataSource, PdfEncryptor? encryptor = null)
    {
        var table = new PdfObjectTable();
        var catalog = table.Reserve();
        var tree = table.Reserve();
        var copier = new PdfObjectCopier(table);
        var bySource = pages.GroupBy(p => p.Page.Document).ToDictionary(g => g.Key, g => (ISet<int>)g.Select(x => x.Page.Reference?.Number ?? -1).ToHashSet());
        foreach (var (page, _) in pages)
        {
            copier.Reserve(page);
        }

        var kids = new List<PdfObject>();
        var seen = new HashSet<(PdfDocument, int)>();
        foreach (var (page, rotate) in pages)
        {
            // A page used twice must be two page objects.
            var copy = seen.Add((page.Document, page.Number)) ? page : null;
            kids.Add(copy is not null
                ? copier.CopyPage(page, tree, bySource[page.Document], rotate)
                : new PdfObjectCopier(table).CopyPage(page, tree, bySource[page.Document], rotate));
        }

        table.Set(tree, new PdfDictionary().SetName("Type", "Pages").Set("Kids", new PdfArray(kids)).SetNumber("Count", kids.Count));
        table.Set(catalog, new PdfDictionary().SetName("Type", "Catalog").Set("Pages", tree));
        using var output = new MemoryStream();
        table.Write(output, catalog, table.Add(Information(metadataSource.Information)), encryptor: encryptor);
        return output.ToArray();
    }

    internal static PdfDictionary Information(PdfInformation information)
    {
        var info = new PdfDictionary();
        void Text(string key, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                info.Set(key, PdfString.FromText(value));
            }
        }

        Text("Title", information.Title);
        Text("Author", information.Author);
        Text("Subject", information.Subject);
        Text("Keywords", information.Keywords);
        Text("Creator", information.Creator);
        Text("Producer", information.Producer is null ? "OmniEurope.Documents" : information.Producer + "; OmniEurope.Documents");
        return info;
    }
}

/// <summary>Page range expressions: <c>1-3,5,8-</c> (open ends run to the last page).</summary>
public static class PageRanges
{
    /// <summary>The page numbers of <paramref name="expression"/> in order.</summary>
    /// <exception cref="FormatException">Malformed expression or a page outside 1..<paramref name="pageCount"/>.</exception>
    public static IReadOnlyList<int> Parse(string expression, int pageCount)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var pages = new List<int>();
        foreach (var part in expression.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var dash = part.IndexOf('-');
            var first = dash == 0 ? 1 : Number(dash < 0 ? part : part[..dash], pageCount);
            var last = dash < 0 ? first : dash == part.Length - 1 ? pageCount : Number(part[(dash + 1)..], pageCount);
            if (first > last)
            {
                throw new FormatException($"Range '{part}' is reversed.");
            }

            pages.AddRange(Enumerable.Range(first, last - first + 1));
        }

        return pages.Count > 0 ? pages : throw new FormatException("The range expression selects no page.");
    }

    private static int Number(string text, int pageCount) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1 && n <= pageCount
            ? n
            : throw new FormatException($"'{text}' is not a page between 1 and {pageCount}.");
}
