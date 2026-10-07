// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Pdf.Layout;

namespace OmniEurope.Documents.Pdf.Text;

/// <summary>
/// Plain text from letters: letters are grouped by direction (text drawn at an angle is read along its own
/// baselines, each direction after the one drawn before it), then into lines as the layout analysis does (one
/// baseline, a superscript or subscript kept on the line of its text, blanks printed under the text left out);
/// lines go from top to bottom of their direction, letters along it, and a gap wider than a quarter of the font
/// size (once the tracking of letter-spaced text is taken off), or a baseline shift (into or out of a superscript
/// or subscript), becomes a space.
/// </summary>
internal static class SimpleTextOrder
{
    public static string Build(IReadOnlyList<PdfLetter> letters)
    {
        var lines = TextDirection.Group(letters.Where(l => l.Value.Length > 0)).SelectMany(d => LineGrouping.Group(d.Letters));
        var text = new StringBuilder();
        foreach (var line in lines)
        {
            if (text.Length > 0)
            {
                text.Append('\n');
            }

            AppendLine(LineGrouping.Arrange(line), text);
        }

        return text.ToString();
    }

    private static void AppendLine(List<(PlacedLetter Letter, double Tracking)> line, StringBuilder text)
    {
        PlacedLetter? previous = null;
        foreach (var (letter, tracking) in line)
        {
            if (previous is { } p && NeedsSpace(p, letter, tracking, text))
            {
                text.Append(' ');
            }

            text.Append(letter.Letter.Value);
            previous = letter;
        }
    }

    private static bool NeedsSpace(PlacedLetter previous, PlacedLetter letter, double tracking, StringBuilder text)
    {
        var apart = letter.X - previous.Right - tracking > previous.Size * 0.25 || LineGrouping.IsShifted(previous, letter);
        return apart && text[^1] != ' ' && letter.Letter.Value != " ";
    }
}
