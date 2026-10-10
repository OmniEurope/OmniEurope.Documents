// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Text;

/// <summary>
/// The Unicode Bidirectional Algorithm (UAX #9) for one paragraph whose base level is given (higher-level protocol
/// HL1: a Word paragraph's direction is its <c>w:bidi</c> property): explicit embeddings, overrides and isolates
/// (X1 to X10, with isolating run sequences), weak types (W1 to W7), paired brackets (N0), neutrals (N1, N2),
/// implicit levels (I1, I2), then per line the reset of trailing white space (L1) and the reordering (L2). Text is
/// processed in UTF-16 code units, both units of a surrogate pair taking the class of their code point. Character
/// classes come from <see cref="BidiClasses"/>, with its stated approximation.
/// </summary>
internal static class BidiAlgorithm
{
    /// <summary>The resolved embedding level of each code unit of <paramref name="text"/>.</summary>
    /// <param name="text">The paragraph text.</param>
    /// <param name="paragraphLevel">0 for a left-to-right paragraph, 1 for a right-to-left one.</param>
    /// <param name="strongRtl">Code units whose neutral (ON) class is taken as R (higher-level protocol HL2: Word's
    /// right-to-left runs); null for none.</param>
    public static byte[] Levels(string text, byte paragraphLevel, Func<int, bool>? strongRtl = null)
    {
        var types = Classify(text, strongRtl);
        return new BidiParagraph(text, types, paragraphLevel).Resolve();
    }

    /// <summary>The classes of each code unit, before any rule.</summary>
    public static BidiClass[] Classify(string text, Func<int, bool>? strongRtl = null)
    {
        var types = new BidiClass[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var codePoint = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])
                ? char.ConvertToUtf32(text[i], text[i + 1])
                : text[i];
            types[i] = BidiClasses.Of(codePoint);
            if (types[i] == BidiClass.ON && strongRtl?.Invoke(i) == true)
            {
                types[i] = BidiClass.R;
            }

            if (codePoint > 0xFFFF)
            {
                types[++i] = types[i - 1];
            }
        }

        return types;
    }

    /// <summary>True when the text holds anything that may not be drawn left to right as it is stored.</summary>
    public static bool NeedsReordering(string text) =>
        Classify(text).Any(t => BidiClasses.IsRightToLeft(t) || t is BidiClass.AN or >= BidiClass.LRE);

    /// <summary>
    /// Rule L1 on one line: the levels of the code units from <paramref name="start"/> (<paramref name="length"/> of
    /// them), segment and paragraph separators and the white space before them and at the line end reset to the
    /// paragraph level.
    /// </summary>
    public static byte[] LineLevels(string text, byte[] levels, int start, int length, byte paragraphLevel)
    {
        var line = levels.AsSpan(start, length).ToArray();
        var types = Classify(text.Substring(start, length));
        var trailing = true;
        for (var i = length - 1; i >= 0; i--)
        {
            var type = types[i];
            if (type is BidiClass.S or BidiClass.B)
            {
                line[i] = paragraphLevel;
                trailing = true;
            }
            else if (trailing && IsWhiteSpaceOrIsolate(type))
            {
                line[i] = paragraphLevel;
            }
            else
            {
                trailing = false;
            }
        }

        return line;
    }

    /// <summary>Rule L2: the logical indices of a line's units in visual order, left to right.</summary>
    public static int[] VisualOrder(byte[] levels)
    {
        var order = Enumerable.Range(0, levels.Length).ToArray();
        if (levels.Length == 0)
        {
            return order;
        }

        var highest = levels.Max();
        var lowestOdd = (byte)(levels.Min() | 1);
        for (var level = highest; level >= lowestOdd; level--)
        {
            for (var i = 0; i < levels.Length; i++)
            {
                if (levels[order[i]] < level)
                {
                    continue;
                }

                var end = i;
                while (end + 1 < levels.Length && levels[order[end + 1]] >= level)
                {
                    end++;
                }

                Array.Reverse(order, i, end - i + 1);
                i = end;
            }
        }

        return order;
    }

    private static bool IsWhiteSpaceOrIsolate(BidiClass type) =>
        type is BidiClass.WS or BidiClass.BN or BidiClass.FSI or BidiClass.LRI or BidiClass.RLI or BidiClass.PDI
        or BidiClass.LRE or BidiClass.RLE or BidiClass.LRO or BidiClass.RLO or BidiClass.PDF;
}
