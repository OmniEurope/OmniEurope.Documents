// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>A run of a paragraph, what it holds and the unit it belongs to (a field's runs share one unit,
/// every other run is a unit of its own).</summary>
internal sealed record ScannedRun(XElement Element, WordRunKind Kind, string? Name, int Unit)
{
    public bool IsText => Kind == WordRunKind.Text;
}

/// <summary>
/// Finds the runs that belong to a paragraph itself (not to a nested text box paragraph, not deleted, not in
/// an alternate-content fallback) and sorts them by kind. A text run holds only text, tabs, line breaks and
/// hyphens; runs of fields (instruction and result), notes, pictures, symbols, page and column breaks are not
/// text.
/// </summary>
internal static class WordRunScanner
{
    private static readonly HashSet<string> TextContent = new(StringComparer.Ordinal)
    {
        "rPr", "t", "tab", "br", "cr", "noBreakHyphen", "softHyphen", "lastRenderedPageBreak",
    };

    public static IEnumerable<XElement> Paragraphs(XElement root) =>
        root.Descendants(W + "p").Where(p => !p.Ancestors(Mc + "Fallback").Any());

    public static List<ScannedRun> Runs(XElement paragraph)
    {
        var result = new List<ScannedRun>();
        var depth = 0;
        XElement? simple = null;
        foreach (var run in paragraph.Descendants(W + "r").Where(r => Owns(paragraph, r)))
        {
            var characters = run.Elements(W + "fldChar").Select(f => Attr(f, "fldCharType")).ToList();
            var outermost = run.Ancestors().TakeWhile(a => a != paragraph).LastOrDefault(a => a.Name == W + "fldSimple");
            var continues = depth > 0 || (outermost is not null && outermost == simple);
            var unit = continues ? result[^1].Unit : result.Count;
            var inField = continues || outermost is not null || characters.Count > 0 || run.Element(W + "instrText") is not null;
            var (kind, name) = inField ? (WordRunKind.Field, null) : Classify(run);
            result.Add(new ScannedRun(run, kind, name, unit));
            depth = Math.Max(0, depth + characters.Count(c => c == "begin") - characters.Count(c => c == "end"));
            simple = outermost;
        }

        return result;
    }

    private static bool Owns(XElement paragraph, XElement run)
    {
        foreach (var ancestor in run.Ancestors())
        {
            if (ancestor == paragraph)
            {
                return true;
            }

            if (ancestor.Name == W + "p" || ancestor.Name == W + "del" || ancestor.Name == W + "moveFrom" || ancestor.Name == Mc + "Fallback")
            {
                return false;
            }
        }

        return false;
    }

    private static (WordRunKind Kind, string? Name) Classify(XElement run)
    {
        var hasText = false;
        foreach (var child in run.Elements())
        {
            if (Content(child) is { } other)
            {
                return other;
            }

            hasText |= child.Name.LocalName is not ("rPr" or "lastRenderedPageBreak");
        }

        return hasText ? (WordRunKind.Text, null) : (WordRunKind.Other, null);
    }

    // The kinds of the run content that is not text, by element name.
    private static readonly Dictionary<string, WordRunKind> Kinds = new(StringComparer.Ordinal)
    {
        ["footnoteReference"] = WordRunKind.FootnoteReference,
        ["endnoteReference"] = WordRunKind.EndnoteReference,
        ["drawing"] = WordRunKind.Drawing,
        ["pict"] = WordRunKind.Drawing,
        ["object"] = WordRunKind.Drawing,
        ["sym"] = WordRunKind.Symbol,
        ["br"] = WordRunKind.PageBreak,
    };

    // Null for text content; otherwise the kind of run the element makes.
    private static (WordRunKind Kind, string? Name)? Content(XElement child)
    {
        if (child.Name == Mc + "AlternateContent")
        {
            return Alternate(child);
        }

        var name = child.Name.LocalName;
        if (child.Name.Namespace != W)
        {
            return (WordRunKind.Other, name);
        }

        if (TextContent.Contains(name) && !(name == "br" && Attr(child, "type") is "page" or "column"))
        {
            return null;
        }

        return Kinds.TryGetValue(name, out var kind) ? (kind, null) : (WordRunKind.Other, name);
    }

    // Alternate content is what its first choice holds.
    private static (WordRunKind Kind, string? Name) Alternate(XElement alternate) =>
        alternate.Elements(Mc + "Choice").Elements().Select(Content).FirstOrDefault(c => c is not null) ?? (WordRunKind.Other, alternate.Name.LocalName);

    /// <summary>The text of text runs: tabs as <c>\t</c>, breaks as <c>\n</c>.</summary>
    public static string Text(IEnumerable<ScannedRun> runs) => string.Concat(runs.Where(r => r.IsText).Select(r => Text(r.Element)));

    /// <summary>The text of one text run.</summary>
    public static string Text(XElement run)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var child in run.Elements())
        {
            builder.Append(child.Name.LocalName switch
            {
                "t" => child.Value,
                "tab" => "\t",
                "br" or "cr" => "\n",
                "noBreakHyphen" => "‑",
                "softHyphen" => "­",
                _ => string.Empty,
            });
        }

        return builder.ToString();
    }
}
