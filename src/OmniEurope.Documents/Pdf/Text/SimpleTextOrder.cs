// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Pdf.Layout;

namespace OmniEurope.Documents.Pdf.Text;

/// <summary>
/// Plain text from letters: letters are grouped into lines as the layout analysis does (one baseline, a
/// superscript or subscript kept on the line of its text), lines go from top to bottom, letters left to right,
/// and a gap wider than a quarter of the font size, or a baseline shift (into or out of a superscript or
/// subscript), becomes a space.
/// </summary>
internal static class SimpleTextOrder
{
    public static string Build(IReadOnlyList<PdfLetter> letters)
    {
        var lines = LineGrouping.Group(letters.Where(l => l.Value.Length > 0).Select(l => new PlacedLetter(l, l.X, l.Y)));
        var text = new StringBuilder();
        foreach (var line in lines)
        {
            if (text.Length > 0)
            {
                text.Append('\n');
            }

            AppendLine(line.OrderBy(l => l.X).ToList(), text);
        }

        return text.ToString();
    }

    private static void AppendLine(List<PlacedLetter> line, StringBuilder text)
    {
        PlacedLetter? previous = null;
        foreach (var letter in line)
        {
            if (previous is { } p && NeedsSpace(p, letter, text))
            {
                text.Append(' ');
            }

            text.Append(letter.Letter.Value);
            previous = letter;
        }
    }

    private static bool NeedsSpace(PlacedLetter previous, PlacedLetter letter, StringBuilder text)
    {
        var apart = letter.X - previous.Right > previous.Size * 0.25 || LineGrouping.IsShifted(previous, letter);
        return apart && text[^1] != ' ' && letter.Letter.Value != " ";
    }
}
