// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>A run of a paragraph and whether it holds only editable text.</summary>
internal sealed record ScannedRun(XElement Element, bool IsText);

/// <summary>
/// Finds the runs that belong to a paragraph itself (not to a nested text box paragraph, not deleted, not in
/// an alternate-content fallback) and sorts them into text runs and kept runs. A text run holds only text,
/// tabs, line breaks and hyphens; runs of fields (instruction and result), notes, pictures, symbols, page and
/// column breaks are kept as they are.
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
        foreach (var run in paragraph.Descendants(W + "r").Where(r => Owns(paragraph, r)))
        {
            var characters = run.Elements(W + "fldChar").Select(f => Attr(f, "fldCharType")).ToList();
            var inField = depth > 0 || characters.Count > 0 || run.Element(W + "instrText") is not null || InSimpleField(paragraph, run);
            depth += characters.Count(c => c == "begin") - characters.Count(c => c == "end");
            depth = Math.Max(0, depth);
            result.Add(new ScannedRun(run, !inField && IsTextOnly(run)));
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

    private static bool InSimpleField(XElement paragraph, XElement run) =>
        run.Ancestors().TakeWhile(a => a != paragraph).Any(a => a.Name == W + "fldSimple");

    private static bool IsTextOnly(XElement run)
    {
        var hasText = false;
        foreach (var child in run.Elements())
        {
            if (child.Name.Namespace != W || !TextContent.Contains(child.Name.LocalName))
            {
                return false;
            }

            if (child.Name.LocalName == "br" && Attr(child, "type") is "page" or "column")
            {
                return false;
            }

            hasText |= child.Name.LocalName != "rPr" && child.Name.LocalName != "lastRenderedPageBreak";
        }

        return hasText;
    }

    /// <summary>The text of text runs: tabs as <c>\t</c>, breaks as <c>\n</c>.</summary>
    public static string Text(IEnumerable<ScannedRun> runs)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var child in runs.Where(r => r.IsText).SelectMany(r => r.Element.Elements()))
        {
            builder.Append(child.Name.LocalName switch
            {
                "t" => child.Value,
                "tab" => "\t",
                "br" or "cr" => "\n",
                "noBreakHyphen" => "\u2011",
                "softHyphen" => "\u00AD",
                _ => string.Empty,
            });
        }

        return builder.ToString();
    }
}
