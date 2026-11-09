// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>
/// Replaces text inside a paragraph even when Word split it across several runs (as it does after spell
/// checking or partial formatting). The replacement takes the formatting of the run where the match
/// starts; the matched characters are removed from the following runs. Field instructions and results
/// are not searched, and a match never spans a tab, a break, a hyphen character or a run that is not text
/// (a field, a note reference, a picture): the text is searched in the stretches between them.
/// </summary>
internal static class WordTextReplacer
{
    public static int Replace(XElement paragraph, string search, string replacement, StringComparison comparison)
    {
        var count = 0;
        foreach (var stretch in Stretches(paragraph))
        {
            count += Replace(stretch, search, replacement, comparison);
        }

        return count;
    }

    // The w:t elements that read as one piece of text: across run boundaries, not across anything else.
    private static List<List<XElement>> Stretches(XElement paragraph)
    {
        var stretches = new List<List<XElement>> { new() };
        foreach (var run in WordRunScanner.Runs(paragraph))
        {
            foreach (var child in run.IsText ? run.Element.Elements() : [run.Element])
            {
                if (child.Name == W + "t")
                {
                    stretches[^1].Add(child);
                }
                else if (child.Name != W + "rPr" && child.Name != W + "lastRenderedPageBreak" && stretches[^1].Count > 0)
                {
                    stretches.Add([]);
                }
            }
        }

        return stretches;
    }

    private static int Replace(List<XElement> texts, string search, string replacement, StringComparison comparison)
    {
        if (texts.Count == 0)
        {
            return 0;
        }

        var values = texts.Select(t => t.Value).ToArray();
        var joined = string.Concat(values);
        var matches = FindMatches(joined, search, comparison);
        if (matches.Count == 0)
        {
            return 0;
        }

        var starts = new int[values.Length];
        for (var i = 1; i < values.Length; i++)
        {
            starts[i] = starts[i - 1] + values[i - 1].Length;
        }

        // Right to left: the start of each segment keeps its offset while later text changes.
        for (var m = matches.Count - 1; m >= 0; m--)
        {
            Apply(values, starts, matches[m].Position, matches[m].Length, replacement);
        }

        for (var i = 0; i < texts.Count; i++)
        {
            if (texts[i].Value != values[i])
            {
                texts[i].ReplaceWith(TextElement(values[i]));
            }
        }

        return matches.Count;
    }

    // A linguistic comparison can match more or fewer UTF-16 units than the search has (an ignorable
    // character such as a soft hyphen inside the match), so each match keeps its own length.
    private static List<(int Position, int Length)> FindMatches(string text, string search, StringComparison comparison)
    {
        var matches = new List<(int, int)>();
        var linguistic = comparison is not (StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase);
        var culture = comparison is StringComparison.InvariantCulture or StringComparison.InvariantCultureIgnoreCase ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;
        var options = comparison is StringComparison.CurrentCultureIgnoreCase or StringComparison.InvariantCultureIgnoreCase ? CompareOptions.IgnoreCase : CompareOptions.None;
        var start = 0;
        while (start <= text.Length)
        {
            int at;
            var length = search.Length;
            if (linguistic)
            {
                at = culture.CompareInfo.IndexOf(text.AsSpan(start), search, options, out length);
                if (at >= 0)
                {
                    at += start;
                }
            }
            else
            {
                at = text.IndexOf(search, start, comparison);
            }

            if (at < 0)
            {
                break;
            }

            matches.Add((at, length));
            start = at + Math.Max(length, 1);
            if (at + length >= text.Length)
            {
                break;
            }
        }

        return matches;
    }

    private static void Apply(string[] values, int[] starts, int position, int length, string replacement)
    {
        var end = position + length;
        var first = Segment(starts, values, position);
        var last = Segment(starts, values, end - 1);
        var offset = position - starts[first];
        if (first == last)
        {
            values[first] = values[first][..offset] + replacement + values[first][(offset + length)..];
            return;
        }

        values[first] = values[first][..offset] + replacement;
        for (var i = first + 1; i < last; i++)
        {
            values[i] = string.Empty;
        }

        values[last] = values[last][(end - starts[last])..];
    }

    private static int Segment(int[] starts, string[] values, int position)
    {
        for (var i = starts.Length - 1; i >= 0; i--)
        {
            if (starts[i] <= position && values[i].Length > 0)
            {
                return i;
            }
        }

        return 0;
    }
}
