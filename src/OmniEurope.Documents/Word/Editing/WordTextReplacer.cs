// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>
/// Replaces text inside a paragraph even when Word split it across several runs (as it does after spell
/// checking or partial formatting). The replacement takes the formatting of the run where the match
/// starts; the matched characters are removed from the following runs. Field instructions and results
/// are not searched.
/// </summary>
internal static class WordTextReplacer
{
    public static int Replace(XElement paragraph, string search, string replacement, StringComparison comparison)
    {
        var texts = WordRunScanner.Runs(paragraph).Where(r => r.IsText).SelectMany(r => r.Element.Elements(W + "t")).ToList();
        if (texts.Count == 0)
        {
            return 0;
        }

        var values = texts.Select(t => t.Value).ToArray();
        var joined = string.Concat(values);
        var matches = new List<int>();
        for (var at = joined.IndexOf(search, comparison); at >= 0; at = joined.IndexOf(search, at + search.Length, comparison))
        {
            matches.Add(at);
            if (at + search.Length >= joined.Length)
            {
                break;
            }
        }

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
            Apply(values, starts, matches[m], search.Length, replacement);
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
