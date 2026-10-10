// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>A tracked change found in a part: its markup element, and for a named move every element of the move.</summary>
internal sealed record ScannedChange(WordTrackedChangeKind Kind, string PartName, XElement Element, string? Id, IReadOnlyList<XElement> Members);

/// <summary>
/// Lists the tracked changes of a part in document order (ECMA-376 Part 1 §17.13.5). <c>w:ins</c>, <c>w:del</c>,
/// <c>w:moveFrom</c> and <c>w:moveTo</c> are content changes around runs, paragraph mark changes in a paragraph
/// mark's properties and row changes in row properties; a move whose content sits inside named move ranges is
/// one change with both its ends and its range markers. Revision markup in math control properties is not listed.
/// </summary>
internal static class WordTrackedChangeScanner
{
    private static readonly Dictionary<string, WordTrackedChangeKind> PropertyChanges = new(StringComparer.Ordinal)
    {
        ["rPrChange"] = WordTrackedChangeKind.RunFormatting,
        ["pPrChange"] = WordTrackedChangeKind.ParagraphFormatting,
        ["sectPrChange"] = WordTrackedChangeKind.SectionFormatting,
        ["tblPrChange"] = WordTrackedChangeKind.TableFormatting,
        ["tblPrExChange"] = WordTrackedChangeKind.TableFormatting,
        ["trPrChange"] = WordTrackedChangeKind.TableFormatting,
        ["tcPrChange"] = WordTrackedChangeKind.TableFormatting,
        ["tblGridChange"] = WordTrackedChangeKind.TableFormatting,
        ["cellIns"] = WordTrackedChangeKind.CellInsertion,
        ["cellDel"] = WordTrackedChangeKind.CellDeletion,
        ["cellMerge"] = WordTrackedChangeKind.CellMerge,
        ["numberingChange"] = WordTrackedChangeKind.NumberingChange,
    };

    public static List<ScannedChange> Scan(XElement root, string partName, bool includeFallback)
    {
        var scan = new ScanState(partName);
        foreach (var element in root.Descendants())
        {
            if (element.Name.Namespace == W && (includeFallback || !element.Ancestors(Mc + "Fallback").Any()))
            {
                scan.Visit(element);
            }
        }

        return scan.Changes;
    }

    /// <summary>The changes found so far and the move ranges open at the element being read.</summary>
    private sealed class ScanState(string partName)
    {
        private readonly Dictionary<string, List<XElement>> _moves = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (string Name, bool From)> _open = new(StringComparer.Ordinal);
        private readonly HashSet<string> _listedMoves = new(StringComparer.Ordinal);

        public List<ScannedChange> Changes { get; } = [];

        public void Visit(XElement element)
        {
            var name = element.Name.LocalName;
            if (TrackRange(element, name, _open, _moves))
            {
                return;
            }

            if (PropertyChanges.TryGetValue(name, out var propertyKind))
            {
                Changes.Add(Single(propertyKind, partName, element));
                return;
            }

            if (name is not ("ins" or "del" or "moveFrom" or "moveTo") || Kind(element, name) is not { } kind)
            {
                return;
            }

            var from = name == "moveFrom";
            if (kind != WordTrackedChangeKind.Move)
            {
                Changes.Add(Single(kind, partName, element));
            }
            else if (_open.Values.FirstOrDefault(r => r.From == from).Name is { } moveName)
            {
                AddMove(moveName, element);
            }
            else
            {
                Changes.Add(Single(from ? WordTrackedChangeKind.MoveFrom : WordTrackedChangeKind.MoveTo, partName, element));
            }
        }

        private void AddMove(string moveName, XElement element)
        {
            var members = Members(_moves, moveName);
            members.Add(element);
            if (_listedMoves.Add(moveName))
            {
                Changes.Add(new ScannedChange(WordTrackedChangeKind.Move, partName, element, moveName, members));
            }
        }
    }

    // Move range markers open and close named ranges; each marker belongs to its move.
    private static bool TrackRange(XElement element, string name, Dictionary<string, (string Name, bool From)> open, Dictionary<string, List<XElement>> moves)
    {
        var id = Attr(element, "id") ?? string.Empty;
        switch (name)
        {
            case "moveFromRangeStart" or "moveToRangeStart":
                var moveName = Attr(element, "name") ?? id;
                open[id + (name == "moveFromRangeStart" ? "f" : "t")] = (moveName, name == "moveFromRangeStart");
                Members(moves, moveName).Add(element);
                return true;
            case "moveFromRangeEnd" or "moveToRangeEnd":
                var key = id + (name == "moveFromRangeEnd" ? "f" : "t");
                if (open.Remove(key, out var range))
                {
                    Members(moves, range.Name).Add(element);
                }

                return true;
            default:
                return false;
        }
    }

    private static List<XElement> Members(Dictionary<string, List<XElement>> moves, string name)
    {
        if (!moves.TryGetValue(name, out var members))
        {
            members = [];
            moves[name] = members;
        }

        return members;
    }

    // Move stands for a move end, before the scanner knows whether it sits in a named range.
    private static WordTrackedChangeKind? Kind(XElement element, string name)
    {
        var parent = element.Parent?.Name;
        if (parent == W + "trPr")
        {
            return name switch
            {
                "ins" => WordTrackedChangeKind.RowInsertion,
                "del" => WordTrackedChangeKind.RowDeletion,
                _ => null,
            };
        }

        if (parent == W + "rPr")
        {
            return element.Parent!.Parent?.Name != W + "pPr" ? null : name switch
            {
                "ins" => WordTrackedChangeKind.ParagraphMarkInsertion,
                "del" => WordTrackedChangeKind.ParagraphMarkDeletion,
                _ => WordTrackedChangeKind.Move,
            };
        }

        if (parent == W + "numPr")
        {
            return name == "ins" ? WordTrackedChangeKind.NumberingInsertion : null;
        }

        if (parent?.Namespace == M)
        {
            return null;
        }

        return name switch
        {
            "ins" => WordTrackedChangeKind.Insertion,
            "del" => WordTrackedChangeKind.Deletion,
            _ => WordTrackedChangeKind.Move,
        };
    }

    private static ScannedChange Single(WordTrackedChangeKind kind, string partName, XElement element) =>
        new(kind, partName, element, Attr(element, "id"), [element]);

    /// <summary>The public description of a scanned change.</summary>
    public static WordTrackedChange Describe(ScannedChange change, int index)
    {
        var date = Attr(change.Element, "date") is { } text
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : (DateTimeOffset?)null;
        return new WordTrackedChange(index, change.Kind, change.PartName, change.Id, Attr(change.Element, "author"), date, Text(change));
    }

    private static string Text(ScannedChange change)
    {
        var scope = change.Kind switch
        {
            WordTrackedChangeKind.Insertion or WordTrackedChangeKind.Deletion or WordTrackedChangeKind.MoveFrom or WordTrackedChangeKind.MoveTo => [change.Element],
            WordTrackedChangeKind.Move => MoveEnds(change, "moveTo") is { Count: > 0 } to ? to : MoveEnds(change, "moveFrom"),
            WordTrackedChangeKind.RunFormatting or WordTrackedChangeKind.ParagraphFormatting => change.Element.Parent?.Parent is { } owner && owner.Name.LocalName is "r" or "p" ? [owner] : [],
            _ => new List<XElement>(),
        };
        var builder = new StringBuilder();
        foreach (var text in scope.SelectMany(s => s.Descendants()).Where(d => d.Name == W + "t" || d.Name == W + "delText" || (d.Name == W + "tab" && d.Parent?.Name == W + "r")))
        {
            builder.Append(text.Name.LocalName == "tab" ? "\t" : text.Value);
        }

        return builder.ToString();
    }

    private static List<XElement> MoveEnds(ScannedChange change, string name) =>
        change.Members.Where(m => m.Name == W + name && m.Parent?.Name != W + "rPr").ToList();
}
