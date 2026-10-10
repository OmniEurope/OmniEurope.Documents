// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using OmniEurope.Documents.Text;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>
/// Puts a line laid out in reading order into drawing order (UAX #9, L1 to L4), span by span: tokens are cut where the
/// embedding level changes, white space ending the span or before a tab returns to the paragraph level, the pieces
/// are reordered by rule L2 and set side by side from the span's left end (its right end mirrored in a right-to-left
/// paragraph), and the characters of a right-to-left piece are drawn reversed (grapheme by grapheme, so marks stay on
/// their letter) with mirrored brackets. The widths of the pieces of a cut token are measured and share the width the
/// token had on the line (justified spaces keep theirs). Arabic letters keep their nominal forms: no joining forms are
/// chosen (the bundled fonts have no Arabic glyphs).
/// </summary>
internal static class BidiLine
{
    public static void Arrange(LayoutContext context, Line line, double width, bool rightToLeft, byte paragraphLevel)
    {
        var arranged = new List<Placed>(line.Items.Count);
        foreach (var span in line.Items.GroupBy(i => i.Span).OrderBy(g => g.Key))
        {
            arranged.AddRange(ArrangeSpan(context, span.ToList(), width, rightToLeft, paragraphLevel));
        }

        line.Items.Clear();
        line.Items.AddRange(arranged);
    }

    private static List<Placed> ArrangeSpan(LayoutContext context, List<Placed> items, double width, bool rightToLeft, byte paragraphLevel)
    {
        var start = items.Min(i => i.X);
        var end = items.Max(i => i.X + i.Width);
        var pieces = items.SelectMany(i => Pieces(context, i, paragraphLevel)).ToList();
        var levels = LineLevels(pieces, paragraphLevel);
        var x = rightToLeft ? width - end : start;
        var result = new List<Placed>(pieces.Count);
        foreach (var index in BidiAlgorithm.VisualOrder(levels))
        {
            var piece = pieces[index];
            var token = (levels[index] & 1) == 1 && piece.Token is TextToken text ? Reversed(text, piece.Width) : piece.Token;
            result.Add(piece.Source with { Token = token, X = x, Width = piece.Width });
            x += piece.Width;
        }

        return result;
    }

    // Rule L1 on the pieces: the white space ending the span and before each tab, and the tabs, at the paragraph level.
    private static byte[] LineLevels(List<Piece> pieces, byte paragraphLevel)
    {
        var levels = pieces.Select(p => p.Level).ToArray();
        var trailing = true;
        for (var i = pieces.Count - 1; i >= 0; i--)
        {
            var token = pieces[i].Token;
            if (token is TabToken)
            {
                levels[i] = paragraphLevel;
                trailing = true;
            }
            else if (trailing && token is TextToken { IsSpace: true } or BreakToken or AnchorToken)
            {
                levels[i] = paragraphLevel;
            }
            else
            {
                trailing = false;
            }
        }

        return levels;
    }

    // A token cut where its level changes; a token at one level stays whole with its width on the line.
    private static IEnumerable<Piece> Pieces(LayoutContext context, Placed item, byte paragraphLevel)
    {
        if (item.Token is not TextToken { Levels: { } levels } text || levels.Length != text.Text.Length || levels.All(l => l == levels[0]))
        {
            yield return new Piece(item, item.Token, item.Token.Levels?[0] ?? paragraphLevel, item.Width);
            yield break;
        }

        var runs = new List<(string Text, byte Level)>();
        var from = 0;
        for (var i = 1; i <= levels.Length; i++)
        {
            if (i == levels.Length || levels[i] != levels[from])
            {
                runs.Add((text.Text[from..i], levels[from]));
                from = i;
            }
        }

        var measured = runs.Select(r => context.Measure(r.Text, text.Style)).ToList();
        var scale = measured.Sum() > 0 ? item.Width / measured.Sum() : 0;
        for (var r = 0; r < runs.Count; r++)
        {
            yield return new Piece(item, new TextToken(runs[r].Text, text.Style) { Note = text.Note }, runs[r].Level, measured[r] * scale);
        }
    }

    private static TextToken Reversed(TextToken text, double width)
    {
        var elements = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text.Text);
        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.GetTextElement());
        }

        var builder = new StringBuilder(text.Text.Length);
        for (var i = elements.Count - 1; i >= 0; i--)
        {
            var element = elements[i];
            var first = char.ConvertToUtf32(element, 0);
            builder.Append(char.ConvertFromUtf32(BidiBrackets.Mirror(first)));
            builder.Append(element, char.IsSurrogatePair(element, 0) ? 2 : 1, element.Length - (char.IsSurrogatePair(element, 0) ? 2 : 1));
        }

        return new TextToken(builder.ToString(), text.Style) { Note = text.Note, Width = width, Levels = text.Levels };
    }

    private readonly record struct Piece(Placed Source, Token Token, byte Level, double Width);
}
