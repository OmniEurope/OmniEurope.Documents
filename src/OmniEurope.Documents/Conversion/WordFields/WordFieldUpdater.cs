// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml.Linq;
using OmniEurope.Documents.Conversion.WordFields;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Editing;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Conversion;

/// <summary>Options of <see cref="WordFieldUpdater"/>.</summary>
public sealed record WordFieldOptions
{
    /// <summary>The fonts the pages are laid out with, as in <see cref="WordPdfOptions.Fonts"/>; the bundled fonts by default.</summary>
    public FontLibrary? Fonts { get; init; }

    /// <summary>The moment <c>DATE</c> and <c>TIME</c> show; the current time by default.</summary>
    public DateTimeOffset? Now { get; init; }

    /// <summary>Month and day names, AM and PM, and the default <c>DATE</c> and <c>TIME</c> formats; the invariant culture by default.</summary>
    public CultureInfo? Culture { get; init; }
}

/// <summary>What an update did: the fields computed, the table of contents entries written, the page count of the
/// layout used, and what was left as it was.</summary>
public sealed record WordFieldUpdate(int Fields, int TableOfContentsEntries, int PageCount, IReadOnlyList<string> Gaps);

/// <summary>
/// Updates the tables of contents and the simple fields of a document edited with <see cref="WordEditor"/>, from the
/// pages <see cref="WordToPdf"/> lays it out on: <c>TOC</c> rebuilt from the headings (see the remarks of each switch
/// in <see cref="WordFieldUpdate.Gaps"/>), then <c>PAGE</c>, <c>NUMPAGES</c> and <c>SECTIONPAGES</c> of the body and the
/// notes, <c>PAGEREF</c> (the displayed number of the page a bookmark starts on), <c>REF</c> (a bookmark's text),
/// <c>DATE</c>, <c>TIME</c>, <c>CREATEDATE</c> and <c>SAVEDATE</c> (<c>\@</c> pictures, <c>\*</c> number formats). The
/// layout is made again until no page number moves (four times at most). Page fields of headers and footers keep
/// their stored result, as each page computes its own. Other fields are listed in the gaps.
/// </summary>
public static class WordFieldUpdater
{
    private const int MaxPasses = 4;

    /// <summary>Updates the fields in place; save the editor to keep them.</summary>
    public static WordFieldUpdate Update(WordEditor editor, WordFieldOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(editor);
        options ??= new WordFieldOptions();
        var gaps = new SortedSet<string>(StringComparer.Ordinal);
        var entries = RebuildContents(editor, gaps);
        var fonts = options.Fonts ?? FontLibrary.Default;
        var evaluator = new FieldEvaluator(editor, options, gaps);
        var (fields, pages) = (0, 0);
        for (var pass = 1; ; pass++)
        {
            var document = editor.ToDocument();
            var map = WordPageMap.Build(document, fonts);
            pages = map.PageCount;
            var (count, changed) = evaluator.Pass(document, map);
            fields = count;
            if (!changed)
            {
                break;
            }

            if (pass == MaxPasses)
            {
                gaps.Add("page numbers still moving after four layouts");
                break;
            }
        }

        return new WordFieldUpdate(fields, entries, pages, gaps.ToList());
    }

    // Each table of contents is rebuilt from a fresh reading, as the previous one changed the paragraphs.
    private static int RebuildContents(WordEditor editor, SortedSet<string> gaps)
    {
        var entries = 0;
        for (var index = 0; ; index++)
        {
            var document = editor.ToDocument();
            var root = editor.Package.GetXml(editor.MainPart)!.Root!;
            var tables = WordXmlFields.Scan(root).Where(f => f.Kind == "TOC" && !f.InInstruction).ToList();
            if (index >= tables.Count)
            {
                return entries;
            }

            var (count, gap) = new WordTocBuilder(editor.Package, editor.MainPart, root, document).Rebuild(tables[index]);
            entries += count ?? 0;
            if (gap is not null)
            {
                gaps.Add(gap);
            }
        }
    }

    private sealed class FieldEvaluator(WordEditor editor, WordFieldOptions options, SortedSet<string> gaps)
    {
        private readonly CultureInfo _culture = options.Culture ?? CultureInfo.InvariantCulture;
        private readonly DateTimeOffset _now = options.Now ?? DateTimeOffset.Now;

        public (int Fields, bool Changed) Pass(WordDocument document, WordPageMap map)
        {
            var (count, changed) = (0, false);
            var main = editor.Package.GetXml(editor.MainPart)!.Root!;
            var pageParts = editor.Package.Relationships(editor.MainPart).Where(r => r.Type is HeaderType or FooterType).Select(r => r.Target).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var part in editor.TextParts)
            {
                if (editor.Package.GetXml(part)?.Root is not { } root)
                {
                    continue;
                }

                var context = new PartContext(part, root, main, editor.MainPart, map, pageParts.Contains(part));
                foreach (var field in WordXmlFields.Scan(root).Where(f => !f.InInstruction))
                {
                    if (Evaluate(field, context, document) is { } value)
                    {
                        count++;
                        changed |= WordXmlFields.SetResult(field, value);
                    }
                }
            }

            return (count, changed);
        }

        private static readonly HashSet<string> Skipped = new(StringComparer.Ordinal) { "TOC", "HYPERLINK", string.Empty };
        private static readonly HashSet<string> PageKinds = new(StringComparer.Ordinal) { "PAGE", "NUMPAGES", "SECTIONPAGES" };
        private static readonly HashSet<string> DateKinds = new(StringComparer.Ordinal) { "DATE", "TIME", "CREATEDATE", "SAVEDATE" };

        private string? Evaluate(XmlField field, PartContext context, WordDocument document)
        {
            var kind = field.Kind;
            if (Skipped.Contains(kind))
            {
                return null;
            }

            if (PageKinds.Contains(kind))
            {
                return context.PagePart ? null : PageField(field, context);
            }

            if (DateKinds.Contains(kind))
            {
                return DateField(field, document);
            }

            return kind is "PAGEREF" or "REF" ? Bookmarked(field, field.Tokens, context, page: kind == "PAGEREF") : Missing($"{kind} fields not updated");
        }

        private string? PageField(XmlField field, PartContext context)
        {
            var tokens = field.Tokens;
            if (field.Kind == "NUMPAGES")
            {
                return WordFieldValues.Number(context.Map.PageCount, tokens);
            }

            if (field.Kind == "SECTIONPAGES")
            {
                return context.SectionPagesOf(field.Paragraph) is { } pages ? WordFieldValues.Number(pages, tokens) : Laid(field, null, tokens);
            }

            return Laid(field, context.PageOf(field.Paragraph), tokens);
        }

        private string? DateField(XmlField field, WordDocument document)
        {
            var date = field.Kind switch
            {
                "CREATEDATE" => document.Information.Created,
                "SAVEDATE" => document.Information.Modified,
                _ => _now,
            };
            return date is { } known
                ? WordFieldValues.Date(known, Picture(field.Tokens, field.Kind == "TIME" ? "TIME" : "DATE"), _culture)
                : Missing($"{field.Kind} without the date in the core properties not updated");
        }

        private string? Laid(XmlField field, string? label, IReadOnlyList<string> tokens) =>
            label is not null ? WordFieldValues.Page(label, tokens) : Missing($"{field.Kind} fields outside the laid-out body and notes not updated");

        private string? Bookmarked(XmlField field, IReadOnlyList<string> tokens, PartContext context, bool page)
        {
            var name = tokens.Count > 1 && !tokens[1].StartsWith('\\') ? tokens[1] : string.Empty;
            if (context.Bookmark(name) is not { } start)
            {
                return Missing($"{field.Kind} to a missing bookmark not updated");
            }

            if (!page)
            {
                return WordXmlFields.Switch(tokens, "n", hasArgument: false) is not null || WordXmlFields.Switch(tokens, "r", hasArgument: false) is not null
                    || WordXmlFields.Switch(tokens, "w", hasArgument: false) is not null
                    ? Missing("REF fields giving a paragraph number not updated")
                    : PartContext.BookmarkText(start);
            }

            return WordXmlFields.Switch(tokens, "p", hasArgument: false) is not null
                ? Missing("PAGEREF fields with \\p (above or below) not updated")
                : Laid(field, context.PageOf(PartContext.BookmarkParagraph(start)), tokens);
        }

        private string Picture(IReadOnlyList<string> tokens, string kind) =>
            WordXmlFields.Switch(tokens, "@") is { Length: > 0 } picture ? picture
                : kind == "TIME" ? _culture.DateTimeFormat.ShortTimePattern.Replace("tt", "AM/PM", StringComparison.Ordinal)
                : _culture.DateTimeFormat.ShortDatePattern;

        private string? Missing(string gap)
        {
            gaps.Add(gap);
            return null;
        }
    }

    /// <summary>The part being updated: its paragraphs' addresses and its bookmarks, then the body's.</summary>
    private sealed class PartContext(string part, XElement root, XElement main, string mainPart, WordPageMap map, bool pagePart)
    {
        private Dictionary<XElement, int>? _indexes;
        private Dictionary<XElement, int>? _mainIndexes;

        public WordPageMap Map => map;

        public bool PagePart => pagePart;

        public string? PageOf(XElement? paragraph) => Address(paragraph) is { } address ? map.PageOf(address) : null;

        public int? SectionPagesOf(XElement? paragraph) => Address(paragraph) is { } address ? map.SectionPagesOf(address) : null;



        public XElement? Bookmark(string name) =>
            name.Length == 0 ? null
            : Find(root, name) ?? Find(main, name);

        private static XElement? Find(XElement scope, string name) =>
            scope.Descendants(W + "bookmarkStart").FirstOrDefault(b => string.Equals(Attr(b, "name"), name, StringComparison.OrdinalIgnoreCase));

        public static XElement? BookmarkParagraph(XElement start) =>
            start.Ancestors(W + "p").FirstOrDefault() ?? start.ElementsAfterSelf().SelectMany(e => e.DescendantsAndSelf(W + "p")).FirstOrDefault();

        // The text between a bookmark's start and its end, paragraphs joined by a space; deleted text left out.
        public static string BookmarkText(XElement start)
        {
            var id = Attr(start, "id");
            var parts = new List<string>();
            XElement? paragraph = null;
            foreach (var element in start.ElementsAfterSelf().Prepend(start).SelectMany(e => e.DescendantsAndSelf())
                .Concat(start.Ancestors().SelectMany(a => a.ElementsAfterSelf()).SelectMany(e => e.DescendantsAndSelf())))
            {
                if (element.Name == W + "bookmarkEnd" && Attr(element, "id") == id)
                {
                    break;
                }

                if (element.Name == W + "t" && element.Ancestors().All(a => a.Name != W + "del" && a.Name != W + "moveFrom"))
                {
                    var owner = element.Ancestors(W + "p").FirstOrDefault();
                    if (owner != paragraph && parts.Count > 0)
                    {
                        parts.Add(" ");
                    }

                    paragraph = owner;
                    parts.Add(element.Value);
                }
            }

            return string.Concat(parts);
        }

        // A paragraph of the body or of the part being updated, as part#index.
        private string? Address(XElement? paragraph)
        {
            if (paragraph is null)
            {
                return null;
            }

            var inMain = paragraph.Document == main.Document;
            var name = inMain ? mainPart : part;
            var indexes = inMain ? (_mainIndexes ??= Index(main)) : (_indexes ??= Index(root));
            return indexes.TryGetValue(paragraph, out var index) ? name + "#" + index.ToString(CultureInfo.InvariantCulture) : null;
        }

        private static Dictionary<XElement, int> Index(XElement scope)
        {
            var indexes = new Dictionary<XElement, int>(ReferenceEqualityComparer.Instance);
            foreach (var paragraph in WordRunScanner.Paragraphs(scope))
            {
                indexes[paragraph] = indexes.Count;
            }

            return indexes;
        }
    }
}
