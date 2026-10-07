// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Pdf.Text;

/// <summary>
/// Plain text from letters: letters on the same baseline (within a third of the font size) form a line,
/// lines go from top to bottom, letters left to right, and a gap wider than a quarter of the font size
/// becomes a space.
/// </summary>
internal static class SimpleTextOrder
{
    public static string Build(IReadOnlyList<PdfLetter> letters)
    {
        var visible = letters.Where(l => l.Value.Length > 0).ToList();
        if (visible.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<List<PdfLetter>>();
        foreach (var letter in visible.OrderByDescending(l => Math.Round(l.Y, 1)).ThenBy(l => l.X))
        {
            var line = lines.Count > 0 ? lines[^1] : null;
            if (line is not null && Math.Abs(line[0].Y - letter.Y) <= Math.Max(line[0].FontSize, letter.FontSize) / 3)
            {
                line.Add(letter);
            }
            else
            {
                lines.Add([letter]);
            }
        }

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

    private static void AppendLine(List<PdfLetter> line, StringBuilder text)
    {
        PdfLetter? previous = null;
        foreach (var letter in line)
        {
            if (previous is not null)
            {
                var gap = letter.X - (previous.X + previous.Width);
                var endsWithSpace = text.Length > 0 && text[^1] == ' ';
                if (gap > Math.Max(previous.FontSize, 1) * 0.25 && !endsWithSpace && letter.Value != " ")
                {
                    text.Append(' ');
                }
            }

            text.Append(letter.Value);
            previous = letter;
        }
    }
}
