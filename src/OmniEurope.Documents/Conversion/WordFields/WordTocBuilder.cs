// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml.Linq;
using OmniEurope.Documents.Internal;
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Editing;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Conversion.WordFields;

/// <summary>A heading the table of contents lists: its paragraph, level (1 to 9), label and text.</summary>
internal sealed record TocHeading(XElement Paragraph, int Level, string Text);

/// <summary>
/// Rebuilds the result of a <c>TOC</c> field of the body (ECMA-376 Part 1 §17.16.5.68) as Word writes it: one
/// paragraph per heading in the <c>TOC1</c> to <c>TOC9</c> styles (added to the style sheet when missing), the
/// heading's list label and text, a right tab with dot leader at the text column's right edge, and a
/// <c>PAGEREF</c> field to a <c>_Toc</c> bookmark placed on the heading (its value is set by the field update).
/// <c>\o</c> and <c>\u</c> select the headings by their resolved outline level, <c>\t</c> by style name, <c>\h</c>
/// makes the entries links, <c>\n</c> leaves out the page numbers of its levels; without a selecting switch the
/// outline levels 1 to 9 are listed.
/// </summary>
internal sealed class WordTocBuilder(OpcPackage package, string mainPart, XElement root, WordDocument document)
{
    private static readonly string[] Refused = ["a", "b", "c", "s", "d"];

    private int _bookmarkNumber = -1;
    private int _bookmarkId = -1;

    /// <summary>Rebuilds the field; returns the number of entries, or null (with the reason) when it cannot.</summary>
    public (int? Entries, string? Gap) Rebuild(XmlField field)
    {
        var tokens = field.Tokens;
        if (Refused.FirstOrDefault(s => WordXmlFields.Switch(tokens, s, hasArgument: false) is not null) is { } refused)
        {
            return (null, $"table of contents with the \\{refused} switch left as it was");
        }

        var first = field.Paragraph;
        var last = field.End?.Ancestors(W + "p").FirstOrDefault();
        if (first is null || last is null || first.Parent != last.Parent || first.Parent is null || last.IsBefore(first))
        {
            return (null, "table of contents not made of whole paragraphs of one container left as it was");
        }

        var range = first == last ? [first] : new[] { first }.Concat(first.ElementsAfterSelf().TakeWhile(e => e != last)).Append(last).ToList();
        var headings = Headings(tokens, range.ToHashSet());
        var paragraphs = Entries(field, tokens, headings);
        Keep(first, field.Begin, before: true, paragraphs);
        Keep(last, field.End!, before: false, paragraphs);
        first.AddBeforeSelf(paragraphs);
        foreach (var element in range)
        {
            element.Remove();
        }

        return (headings.Count, WordXmlFields.Switch(tokens, "f", hasArgument: false) is not null || WordXmlFields.Switch(tokens, "l") is not null
            ? "table of contents entries from TC fields not collected"
            : null);
    }

    // What the first paragraph holds before the field and the last one after it stays around the new entries.
    private static void Keep(XElement paragraph, XElement fieldRun, bool before, List<XElement> paragraphs)
    {
        var top = fieldRun.AncestorsAndSelf().First(a => a.Parent == paragraph);
        var kept = (before ? top.NodesBeforeSelf() : top.NodesAfterSelf()).Where(n => n is not XElement { Name.LocalName: "pPr" }).ToList();
        if (!kept.OfType<XElement>().Any(e => e.DescendantsAndSelf(W + "t").Any()))
        {
            return;
        }

        foreach (var node in kept)
        {
            node.Remove();
        }

        if (before)
        {
            paragraphs[0].Element(W + "pPr")!.AddAfterSelf(kept);
            return;
        }

        var trailing = new XElement(W + "p", paragraph.Element(W + "pPr") is { } properties ? new XElement(properties) : null, kept);
        paragraphs.Add(trailing);
    }

    private List<TocHeading> Headings(IReadOnlyList<string> tokens, HashSet<XElement> excluded)
    {
        var selection = Selection(tokens);
        var elements = WordRunScanner.Paragraphs(root).ToList();
        var counter = new WordListCounter(document.Numbering);
        var headings = new List<TocHeading>();
        foreach (var paragraph in Paragraphs(document.Blocks))
        {
            var resolved = WordResolution.Paragraph(document, paragraph, null);
            var label = Label(counter, resolved);
            var level = selection.Level(resolved);
            var text = string.Join(' ', paragraph.Text.Split(['\t', '\n', ' '], StringSplitOptions.RemoveEmptyEntries));
            if (level > 0 && text.Length > 0 && Element(paragraph, elements) is { } element && !excluded.Contains(element))
            {
                headings.Add(new TocHeading(element, level, label.Length == 0 ? text : label + " " + text));
            }
        }

        return headings;
    }

    // Every numbered paragraph advances its list, so a heading's label counts the paragraphs before it.
    private static string Label(WordListCounter counter, WordParagraphProperties resolved) =>
        resolved.NumberingId is > 0 && counter.Next(resolved.NumberingId.Value, resolved.NumberingLevel ?? 0) is { } next ? next.Label.Trim() : string.Empty;

    private TocSelection Selection(IReadOnlyList<string> tokens)
    {
        var outline = WordXmlFields.Switch(tokens, "o");
        var styles = StyleLevels(WordXmlFields.Switch(tokens, "t"));
        var byOutline = outline is not null || WordXmlFields.Switch(tokens, "u", hasArgument: false) is not null || styles.Count == 0;
        var (low, high) = outline is null ? (1, 9) : Levels(outline);
        return new TocSelection(low, high, styles, byOutline);
    }

    /// <summary>The levels a table of contents lists: an outline level range, and styles with their levels.</summary>
    private sealed record TocSelection(int Low, int High, Dictionary<string, int> Styles, bool ByOutline)
    {
        public int Level(WordParagraphProperties resolved)
        {
            if (resolved.StyleId is { } style && Styles.TryGetValue(style, out var styled))
            {
                return styled;
            }

            return ByOutline && resolved.OutlineLevel is { } outline && outline + 1 >= Low && outline + 1 <= High ? outline + 1 : 0;
        }
    }

    private XElement? Element(WordParagraph paragraph, List<XElement> elements)
    {
        var address = paragraph.SourceAddress;
        var hash = address?.LastIndexOf('#') ?? -1;
        return hash > 0 && string.Equals(address![..hash], mainPart, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(address[(hash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < elements.Count
            ? elements[index]
            : null;
    }

    private static IEnumerable<WordParagraph> Paragraphs(IEnumerable<WordBlock> blocks)
    {
        foreach (var block in blocks)
        {
            if (block is WordParagraph paragraph)
            {
                yield return paragraph;
            }
            else if (block is WordTable table)
            {
                foreach (var nested in Paragraphs(table.Rows.SelectMany(r => r.Cells).SelectMany(c => c.Blocks)))
                {
                    yield return nested;
                }
            }
        }
    }

    private static (int Low, int High) Levels(string? range)
    {
        var parts = (range ?? string.Empty).Split('-', StringSplitOptions.TrimEntries);
        var low = parts.Length > 0 && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var l) ? Math.Clamp(l, 1, 9) : 1;
        var high = parts.Length > 1 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var h) ? Math.Clamp(h, low, 9) : parts.Length == 1 ? low : 9;
        return (low, high);
    }

    // \t "Style name,level,Other style,level": names (or ids) of the style sheet with their levels.
    private Dictionary<string, int> StyleLevels(string? list)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        var parts = (list ?? string.Empty).Split([',', ';'], StringSplitOptions.TrimEntries);
        for (var i = 0; i + 1 < parts.Length; i += 2)
        {
            var style = document.Styles.Styles.Values.FirstOrDefault(s => string.Equals(s.Name, parts[i], StringComparison.OrdinalIgnoreCase) || s.Id == parts[i]);
            if (style is not null && int.TryParse(parts[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var level))
            {
                result[style.Id] = Math.Clamp(level, 1, 9);
            }
        }

        return result;
    }

    private List<XElement> Entries(XmlField field, IReadOnlyList<string> tokens, List<TocHeading> headings)
    {
        var links = WordXmlFields.Switch(tokens, "h", hasArgument: false) is not null;
        var (noPageLow, noPageHigh) = WordXmlFields.Switch(tokens, "n") is { } omitted ? (omitted.Length == 0 ? (1, 9) : Levels(omitted)) : (0, -1);
        var tab = Format(TabPosition(field.End!));
        var instruction = new XElement(W + "instrText", new XAttribute(XNamespace.Xml + "space", "preserve"), " " + field.Instruction + " ");
        var opening = new object[] { FieldChar("begin"), WordXmlFields.Run(null, null, instruction), FieldChar("separate") };
        var paragraphs = new List<XElement>();
        foreach (var heading in headings)
        {
            var bookmark = Bookmark(heading.Paragraph);
            var content = new List<object> { WordXmlFields.Run(null, heading.Text) };
            if (heading.Level < noPageLow || heading.Level > noPageHigh)
            {
                content.Add(WordXmlFields.Run(null, null, new XElement(W + "tab")));
                content.AddRange(PageReference(bookmark, links));
            }

            var properties = new XElement(W + "pPr", ValElement("pStyle", Style(heading.Level)),
                new XElement(W + "tabs", new XElement(W + "tab", new XAttribute(W + "val", "right"), new XAttribute(W + "leader", "dot"), new XAttribute(W + "pos", tab))));
            object body = links ? new XElement(W + "hyperlink", new XAttribute(W + "anchor", bookmark), new XAttribute(W + "history", "1"), content) : content;
            paragraphs.Add(new XElement(W + "p", properties, paragraphs.Count == 0 ? opening : null, body));
        }

        if (paragraphs.Count == 0)
        {
            paragraphs.Add(new XElement(W + "p", new XElement(W + "pPr"), opening, WordXmlFields.Run(null, "No table of contents entries found.")));
        }

        paragraphs[^1].Add(FieldChar("end"));
        return paragraphs;
    }

    private static IEnumerable<XElement> PageReference(string bookmark, bool link)
    {
        yield return FieldChar("begin");
        yield return WordXmlFields.Run(null, null, new XElement(W + "instrText", new XAttribute(XNamespace.Xml + "space", "preserve"), " PAGEREF " + bookmark + (link ? " \\h " : " ")));
        yield return FieldChar("separate");
        yield return WordXmlFields.Run(null, string.Empty);
        yield return FieldChar("end");
    }

    private static XElement FieldChar(string type) => WordXmlFields.Run(null, null, new XElement(W + "fldChar", new XAttribute(W + "fldCharType", type)));

    // The heading's _Toc bookmark, added around its content when it has none.
    private string Bookmark(XElement paragraph)
    {
        if (paragraph.Elements(W + "bookmarkStart").Select(b => Attr(b, "name")).FirstOrDefault(n => n?.StartsWith("_Toc", StringComparison.Ordinal) == true) is { } existing)
        {
            return existing;
        }

        if (_bookmarkId < 0)
        {
            var starts = root.Descendants(W + "bookmarkStart").ToList();
            _bookmarkId = starts.Select(b => Int(Attr(b, "id")) ?? 0).DefaultIfEmpty(0).Max();
            _bookmarkNumber = starts.Select(b => Attr(b, "name")).Where(n => n?.StartsWith("_Toc", StringComparison.Ordinal) == true)
                .Select(n => Int(n![4..]) ?? 0).DefaultIfEmpty(0).Max();
        }

        var name = "_Toc" + (++_bookmarkNumber).ToString(CultureInfo.InvariantCulture);
        var id = Format(++_bookmarkId);
        var start = new XElement(W + "bookmarkStart", new XAttribute(W + "id", id), new XAttribute(W + "name", name));
        if (paragraph.Element(W + "pPr") is { } properties)
        {
            properties.AddAfterSelf(start);
        }
        else
        {
            paragraph.AddFirst(start);
        }

        paragraph.Add(new XElement(W + "bookmarkEnd", new XAttribute(W + "id", id)));
        return name;
    }

    // The right edge of the text column of the section holding the field, in twentieths of a point.
    private int TabPosition(XElement end)
    {
        var section = end.Ancestors(W + "p").Concat(end.Ancestors(W + "p").SelectMany(p => p.ElementsAfterSelf(W + "p")))
            .Select(p => p.Element(W + "pPr")?.Element(W + "sectPr")).FirstOrDefault(s => s is not null) ?? root.Element(W + "body")?.Element(W + "sectPr");
        var width = Int(Attr(section?.Element(W + "pgSz"), "w")) ?? 11906;
        var margins = section?.Element(W + "pgMar");
        return Math.Max(720, width - (Int(Attr(margins, "left")) ?? 1440) - (Int(Attr(margins, "right")) ?? 1440) - (Int(Attr(margins, "gutter")) ?? 0));
    }

    // TOC1 to TOC9 by id, else by name; the style is added to the style sheet when the document has neither.
    private string Style(int level)
    {
        var name = "toc " + Format(level);
        var styles = package.GetXml(StylesPart() ?? string.Empty)?.Root;
        if (styles is null)
        {
            return "TOC" + Format(level);
        }

        var existing = styles.Elements(W + "style").FirstOrDefault(s => Attr(s, "styleId") == "TOC" + Format(level))
            ?? styles.Elements(W + "style").FirstOrDefault(s => string.Equals(Val(s, "name"), name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return Attr(existing, "styleId")!;
        }

        var normal = styles.Elements(W + "style").FirstOrDefault(s => Attr(s, "type") == "paragraph" && Attr(s, "default") is "1" or "true");
        var basedOn = normal is null ? null : Attr(normal, "styleId");
        styles.Add(new XElement(W + "style", new XAttribute(W + "type", "paragraph"), new XAttribute(W + "styleId", "TOC" + Format(level)),
            ValElement("name", name),
            basedOn is null ? null : ValElement("basedOn", basedOn),
            basedOn is null ? null : ValElement("next", basedOn),
            ValElement("uiPriority", "39"),
            new XElement(W + "unhideWhenUsed"),
            new XElement(W + "pPr", new XElement(W + "spacing", new XAttribute(W + "after", "100")), new XElement(W + "ind", new XAttribute(W + "left", Format(220 * (level - 1)))))));
        return "TOC" + Format(level);
    }

    private string? StylesPart() => package.Relationships(mainPart).Find(r => r.Type == StylesType && !r.External)?.Target;
}
