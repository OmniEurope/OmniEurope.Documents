// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Pdf.Writing;

/// <summary>One wrapped line and whether a line break in the source (not wrapping) ended it.</summary>
public readonly record struct WrappedLine(string Text, double Width, bool EndsParagraph);

/// <summary>
/// Breaks text into lines that fit a width, measured with the real font: breaks at spaces and after
/// hyphens and dashes, keeps line feeds as hard breaks, and cuts a word wider than the line between
/// characters.
/// </summary>
public static class TextWrapper
{
    /// <summary>Wraps <paramref name="text"/> to <paramref name="maxWidth"/> points.</summary>
    public static IReadOnlyList<WrappedLine> Wrap(PdfDocumentBuilder document, string text, PdfFont font, double size, double maxWidth)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(text);
        var lines = new List<WrappedLine>();
        foreach (var paragraph in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            WrapParagraph(document, paragraph, font, size, Math.Max(maxWidth, 1), lines);
        }

        return lines;
    }

    private static void WrapParagraph(PdfDocumentBuilder document, string paragraph, PdfFont font, double size, double maxWidth, List<WrappedLine> lines)
    {
        var tokens = Tokens(paragraph);
        var line = new StringBuilder();
        double width = 0;
        foreach (var token in tokens)
        {
            var tokenWidth = document.MeasureText(token, font, size);
            if (width + tokenWidth <= maxWidth || line.Length == 0 && tokenWidth <= maxWidth)
            {
                line.Append(token);
                width += tokenWidth;
                continue;
            }

            if (token.Trim().Length == 0)
            {
                // A space at the end of a full line is dropped.
                Flush(document, line, font, size, lines, endsParagraph: false);
                width = 0;
                continue;
            }

            if (line.Length > 0)
            {
                Flush(document, line, font, size, lines, endsParagraph: false);
                width = 0;
            }

            if (tokenWidth <= maxWidth)
            {
                line.Append(token);
                width = tokenWidth;
                continue;
            }

            width = BreakLongWord(document, token, font, size, maxWidth, line, lines);
        }

        Flush(document, line, font, size, lines, endsParagraph: true);
    }

    // Splits a word wider than the line character by character; returns the width left on the current line.
    private static double BreakLongWord(PdfDocumentBuilder document, string word, PdfFont font, double size, double maxWidth, StringBuilder line, List<WrappedLine> lines)
    {
        double width = 0;
        foreach (var rune in word.EnumerateRunes())
        {
            var text = rune.ToString();
            var runeWidth = document.MeasureText(text, font, size);
            if (width + runeWidth > maxWidth && line.Length > 0)
            {
                Flush(document, line, font, size, lines, endsParagraph: false);
                width = 0;
            }

            line.Append(text);
            width += runeWidth;
        }

        return width;
    }

    private static void Flush(PdfDocumentBuilder document, StringBuilder line, PdfFont font, double size, List<WrappedLine> lines, bool endsParagraph)
    {
        var text = line.ToString().TrimEnd(' ');
        lines.Add(new WrappedLine(text, document.MeasureText(text, font, size), endsParagraph));
        line.Clear();
    }

    // Words with their trailing hyphen or dash, and runs of spaces, in order.
    private static List<string> Tokens(string paragraph)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        foreach (var c in paragraph)
        {
            if (c == ' ')
            {
                if (current.Length > 0 && current[^1] != ' ')
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }

                current.Append(c);
                continue;
            }

            if (current.Length > 0 && current[^1] == ' ')
            {
                tokens.Add(current.ToString());
                current.Clear();
            }

            current.Append(c);
            if (c is '-' or '–' or '—' or '/')
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
