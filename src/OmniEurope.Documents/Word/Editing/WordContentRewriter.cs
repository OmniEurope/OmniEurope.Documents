// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>
/// Rewrites the runs of a paragraph from <see cref="WordContentPiece"/>s. Replaced text runs are removed and
/// their emptied wrappers with them; the new content goes where the first replaced text run was (outside the
/// wrappers that held only replaced text), or where the first run was when no text is replaced. A kept unit (a
/// run, or every run of a field) moves with the wrappers that hold nothing else (a hyperlink, a simple field,
/// a content control, a tracked insertion...). Units the pieces do not name follow the new content in their
/// original order. Deleted runs and every element that is not an own run stay where they are.
/// </summary>
internal sealed class WordContentRewriter
{
    private static readonly XName Marker = XNamespace.Get("urn:omnieurope:documents:edit") + "here";

    private static readonly HashSet<string> Climbable = new(StringComparer.Ordinal)
    {
        "hyperlink", "fldSimple", "smartTag", "customXml", "sdt", "sdtContent", "ins", "moveTo", "dir", "bdo", "Choice", "AlternateContent",
    };

    private readonly XElement _paragraph;
    private readonly List<ScannedRun> _runs;
    private readonly List<WordContentPiece> _pieces;
    private readonly HashSet<int> _kept;

    private WordContentRewriter(XElement paragraph, List<ScannedRun> runs, List<WordContentPiece> pieces)
    {
        _paragraph = paragraph;
        _runs = runs;
        _pieces = pieces;
        _kept = pieces.OfType<WordKeptRun>().Select(k => runs[k.RunIndex].Unit).ToHashSet();
    }

    public static void Apply(XElement paragraph, IEnumerable<WordContentPiece> pieces, string? language)
    {
        var runs = WordRunScanner.Runs(paragraph);
        var list = pieces.ToList();
        foreach (var piece in list)
        {
            switch (piece)
            {
                case WordTextPiece text:
                    ArgumentNullException.ThrowIfNull(text.Text, nameof(pieces));
                    break;
                case WordKeptRun kept:
                    ArgumentOutOfRangeException.ThrowIfNegative(kept.RunIndex, nameof(pieces));
                    ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(kept.RunIndex, runs.Count, nameof(pieces));
                    break;
                default:
                    throw new ArgumentException("A piece is null.", nameof(pieces));
            }
        }

        var rewriter = new WordContentRewriter(paragraph, runs, list);
        if (language is not null || list.OfType<WordTextPiece>().Any(p => p.Format is not null) || !rewriter.Signature(rewriter.Current()).SequenceEqual(rewriter.Signature(rewriter.Requested())))
        {
            rewriter.Rewrite(language);
        }
    }

    private void Rewrite(string? language)
    {
        var replaced = _runs.Where(r => r.IsText && !_kept.Contains(r.Unit)).Select(r => r.Element).ToList();
        var template = WordRunBuilder.Template(_paragraph, _runs.Find(r => r.IsText)?.Element);
        var units = _runs.Where(r => !r.IsText || _kept.Contains(r.Unit)).GroupBy(r => r.Unit)
            .Select(g => new MovedUnit(g.Key, Movables(g.Select(r => r.Element).ToList()))).ToList();
        var marker = new XElement(Marker);
        if (replaced.Count > 0)
        {
            Outermost(replaced[0], replaced.ToHashSet()).AddBeforeSelf(marker);
        }
        else if (units.Count > 0)
        {
            units[0].Elements[0].AddBeforeSelf(marker);
        }
        else
        {
            _paragraph.Add(marker);
        }

        foreach (var element in replaced.Concat(units.SelectMany(u => u.Elements)))
        {
            var parent = element.Parent;
            element.Remove();
            WordRunBuilder.RemoveIfEmpty(_paragraph, parent);
        }

        marker.AddBeforeSelf(Sequence(units, template, language));
        var container = marker.Parent;
        marker.Remove();
        WordRunBuilder.RemoveIfEmpty(_paragraph, container);
    }

    private List<XElement> Sequence(List<MovedUnit> units, XElement? template, string? language)
    {
        var sequence = new List<XElement>();
        var placed = new HashSet<int>();
        foreach (var piece in _pieces)
        {
            if (piece is WordTextPiece text)
            {
                if (text.Text.Length > 0)
                {
                    sequence.Add(WordRunBuilder.NewRun(text, template, language));
                }
            }
            else if (_runs[((WordKeptRun)piece).RunIndex].Unit is var unit && placed.Add(unit))
            {
                sequence.AddRange(units.Find(u => u.Id == unit)!.Elements);
            }
        }

        sequence.AddRange(units.Where(u => !placed.Contains(u.Id)).SelectMany(u => u.Elements));
        return sequence;
    }

    // The elements to move for a unit, in document order: each run, or the outermost wrapper holding nothing
    // but runs of the unit.
    private List<XElement> Movables(List<XElement> runs)
    {
        var set = runs.ToHashSet();
        var result = new List<XElement>();
        foreach (var run in runs)
        {
            var top = run;
            while (top.Parent is { } parent && parent != _paragraph && Climbable.Contains(parent.Name.LocalName) && OnlyHolds(parent, set))
            {
                top = parent;
            }

            if (!result.Contains(top))
            {
                result.Add(top);
            }
        }

        return result;
    }


    // The replaced run, or the outermost removable wrapper holding nothing but replaced runs.
    private XElement Outermost(XElement run, HashSet<XElement> replaced)
    {
        var top = run;
        while (top.Parent is { } parent && parent != _paragraph && WordRunBuilder.Removable.Contains(parent.Name.LocalName) && OnlyHolds(parent, replaced))
        {
            top = parent;
        }

        return top;
    }

    private static bool OnlyHolds(XElement wrapper, HashSet<XElement> runs) =>
        wrapper.Elements().All(child => runs.Contains(child)
            || child.Name.LocalName.EndsWith("Pr", StringComparison.Ordinal)
            || child.Name.LocalName == "fldData"
            || child.Name == Mc + "Fallback"
            || (Climbable.Contains(child.Name.LocalName) && OnlyHolds(child, runs)));

    // The content as text and units: adjacent text joined, each kept unit once.
    private List<(string? Text, int Unit)> Current() =>
        _runs.Select(r => r.IsText ? (WordRunScanner.Text(r.Element), -1) : ((string?)null, r.Unit)).ToList();

    private List<(string? Text, int Unit)> Requested()
    {
        var tokens = _pieces.Select(p => p is WordTextPiece text ? (text.Text, -1) : ((string?)null, _runs[((WordKeptRun)p).RunIndex].Unit)).ToList();
        tokens.AddRange(_runs.Where(r => !r.IsText && !_kept.Contains(r.Unit)).Select(r => ((string?)null, r.Unit)));
        return tokens;
    }

    private List<(string? Text, int Unit)> Signature(List<(string? Text, int Unit)> tokens)
    {
        var result = new List<(string? Text, int Unit)>();
        var seen = new HashSet<int>();
        foreach (var (text, unit) in tokens)
        {
            if (text is not null && result.Count > 0 && result[^1].Text is { } previous)
            {
                result[^1] = (previous + text, -1);
            }
            else if (text is not null || seen.Add(unit))
            {
                result.Add((text, unit));
            }
        }

        return result.Where(t => t.Text is not { Length: 0 }).ToList();
    }

    private sealed record MovedUnit(int Id, List<XElement> Elements);
}
