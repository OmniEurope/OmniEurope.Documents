// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>
/// Accepts or rejects one tracked change as ECMA-376 Part 1 §17.13.5 describes it. Accepting an insertion keeps its
/// content without the revision mark, rejecting it removes the content; a deletion the other way round, its
/// <c>w:delText</c> and <c>w:delInstrText</c> becoming text and field instruction again when rejected. A paragraph
/// mark that goes (an accepted deletion, a rejected insertion) joins the paragraph with the next one, which keeps
/// its own properties; the last paragraph of a container keeps its mark. A property change accepted drops the
/// record of the former properties; rejected, the former properties replace the current ones. Rows and cells that
/// go are removed with their content, a table left without rows with them.
/// </summary>
internal static class WordTrackedChangeApplier
{
    // For each property element: the children that the former properties do not hold (kept as they are), in
    // schema order before and after the properties.
    private static readonly Dictionary<string, (string[] Before, string[] After)> Kept = new(StringComparer.Ordinal)
    {
        ["rPr"] = (["ins", "del", "moveFrom", "moveTo"], []),
        ["pPr"] = ([], ["rPr", "sectPr"]),
        ["sectPr"] = (["headerReference", "footerReference"], []),
        ["trPr"] = ([], ["ins", "del"]),
        ["tcPr"] = ([], ["cellIns", "cellDel", "cellMerge"]),
    };

    private static readonly HashSet<string> NotBlocks = new(StringComparer.Ordinal)
    {
        "bookmarkStart", "bookmarkEnd", "commentRangeStart", "commentRangeEnd", "moveFromRangeStart", "moveFromRangeEnd",
        "moveToRangeStart", "moveToRangeEnd", "permStart", "permEnd", "proofErr",
    };

    public static void Apply(ScannedChange change, bool accept)
    {
        foreach (var element in change.Members.ToList())
        {
            if (element.Document is not null)
            {
                Apply(change.Kind == WordTrackedChangeKind.Move ? MoveMember(element) : change.Kind, element, accept);
            }
        }
    }

    private static WordTrackedChangeKind? MoveMember(XElement element) => element.Name.LocalName switch
    {
        "moveFrom" when element.Parent?.Name == W + "rPr" => WordTrackedChangeKind.ParagraphMarkDeletion,
        "moveTo" when element.Parent?.Name == W + "rPr" => WordTrackedChangeKind.ParagraphMarkInsertion,
        "moveFrom" => WordTrackedChangeKind.Deletion,
        "moveTo" => WordTrackedChangeKind.Insertion,
        _ => null,
    };

    private static void Apply(WordTrackedChangeKind? kind, XElement element, bool accept)
    {
        switch (kind)
        {
            case WordTrackedChangeKind.Insertion or WordTrackedChangeKind.MoveTo:
                Content(element, keep: accept);
                break;
            case WordTrackedChangeKind.Deletion or WordTrackedChangeKind.MoveFrom:
                Content(element, keep: !accept);
                break;
            case WordTrackedChangeKind.ParagraphMarkInsertion:
                Mark(element, keep: accept);
                break;
            case WordTrackedChangeKind.ParagraphMarkDeletion:
                Mark(element, keep: !accept);
                break;
            case WordTrackedChangeKind.RowInsertion or WordTrackedChangeKind.RowDeletion:
                Structure(element, keep: accept == (kind == WordTrackedChangeKind.RowInsertion), "tr");
                break;
            case WordTrackedChangeKind.CellInsertion or WordTrackedChangeKind.CellDeletion:
                Structure(element, keep: accept == (kind == WordTrackedChangeKind.CellInsertion), "tc");
                break;
            case WordTrackedChangeKind.CellMerge:
                Merge(element, Attr(element, accept ? "vMerge" : "vMergeOrig"));
                break;
            case WordTrackedChangeKind.NumberingInsertion when !accept:
                element.Parent?.Remove();
                break;
            case WordTrackedChangeKind.NumberingChange or WordTrackedChangeKind.NumberingInsertion or null:
                element.Remove();
                break;
            default:
                Properties(element, accept);
                break;
        }
    }

    // Kept content loses its revision wrapper; removed content goes with it.
    private static void Content(XElement wrapper, bool keep)
    {
        if (!keep)
        {
            wrapper.Remove();
            return;
        }

        if (wrapper.Name.LocalName is "del" or "moveFrom")
        {
            foreach (var text in wrapper.Descendants().Where(d => d.Name == W + "delText" || d.Name == W + "delInstrText").ToList())
            {
                if (text.Ancestors().TakeWhile(a => a != wrapper).All(a => a.Name != W + "del" && a.Name != W + "moveFrom"))
                {
                    text.Name = W + (text.Name.LocalName == "delText" ? "t" : "instrText");
                }
            }
        }

        var children = wrapper.Nodes().ToList();
        wrapper.RemoveNodes();
        wrapper.ReplaceWith(children);
    }

    private static void Mark(XElement marker, bool keep)
    {
        var paragraph = marker.Parent?.Parent?.Parent;
        marker.Remove();
        if (!keep && paragraph?.Name == W + "p")
        {
            JoinWithNext(paragraph);
        }
    }

    // The paragraph's content moves to the start of the next paragraph, which keeps its properties.
    private static void JoinWithNext(XElement paragraph)
    {
        var next = paragraph.ElementsAfterSelf().SkipWhile(e => e.Name.Namespace == W && NotBlocks.Contains(e.Name.LocalName)).FirstOrDefault();
        if (next?.Name != W + "p")
        {
            return;
        }

        var content = paragraph.Nodes().Where(n => n is not XElement { Name.LocalName: "pPr" }).ToList();
        foreach (var node in content)
        {
            node.Remove();
        }

        if (next.Element(W + "pPr") is { } properties)
        {
            properties.AddAfterSelf(content);
        }
        else
        {
            next.AddFirst(content);
        }

        paragraph.Remove();
    }

    // A row or cell kept loses its revision mark; one that goes is removed, with the row or table it empties.
    private static void Structure(XElement marker, bool keep, string owner)
    {
        var target = marker.Ancestors(W + owner).FirstOrDefault();
        marker.Remove();
        if (keep || target is null)
        {
            return;
        }

        if (owner == "tr")
        {
            RemoveRow(target);
            return;
        }

        var row = target.Ancestors(W + "tr").FirstOrDefault();
        target.Remove();
        if (row is not null && !row.Descendants(W + "tc").Any())
        {
            RemoveRow(row);
        }
    }

    private static void RemoveRow(XElement row)
    {
        var table = row.Ancestors(W + "tbl").FirstOrDefault();
        row.Remove();
        if (table is null || table.Descendants(W + "tr").Any())
        {
            return;
        }

        var container = table.Parent;
        table.Remove();
        if (container?.Name == W + "tc" && container.Elements().LastOrDefault()?.Name != W + "p")
        {
            container.Add(new XElement(W + "p"));
        }
    }

    // The cell's vertical merge becomes the one recorded (rest: starts a merge, cont: continues one, none: no merge).
    private static void Merge(XElement marker, string? state)
    {
        var properties = marker.Parent!;
        marker.Remove();
        properties.Element(W + "vMerge")?.Remove();
        if (state is not ("rest" or "cont"))
        {
            return;
        }

        var merge = state == "rest" ? ValElement("vMerge", "restart") : new XElement(W + "vMerge");
        var before = properties.Elements().LastOrDefault(e => e.Name.LocalName is "cnfStyle" or "tcW" or "gridSpan" or "hMerge");
        if (before is not null)
        {
            before.AddAfterSelf(merge);
        }
        else
        {
            properties.AddFirst(merge);
        }
    }

    // Accepted, the record goes; rejected, the former properties replace the current ones.
    private static void Properties(XElement change, bool accept)
    {
        var owner = change.Parent!;
        change.Remove();
        if (accept)
        {
            return;
        }

        var (before, after) = Kept.GetValueOrDefault(owner.Name.LocalName, ([], []));
        var former = change.Elements().FirstOrDefault()?.Elements().Where(e => !before.Contains(e.Name.LocalName) && !after.Contains(e.Name.LocalName)).ToList() ?? [];
        var current = owner.Elements().ToList();
        var head = current.Where(e => before.Contains(e.Name.LocalName)).ToList();
        var tail = current.Where(e => after.Contains(e.Name.LocalName) || e.Name.Namespace != W).ToList();
        foreach (var element in current.Concat(former))
        {
            element.Remove();
        }

        owner.Add(head, former, tail);
    }
}
