// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Diff;

/// <summary>One change: <see cref="DeleteCountA"/> pieces of the old sequence starting at
/// <see cref="DeleteStartA"/> are replaced by <see cref="InsertCountB"/> pieces of the new sequence starting
/// at <see cref="InsertStartB"/>. Either count may be zero.</summary>
public readonly record struct DiffBlock(int DeleteStartA, int DeleteCountA, int InsertStartB, int InsertCountB);

/// <summary>The pieces compared and the changes between them, in order.</summary>
public sealed class DiffResult(IReadOnlyList<string> piecesOld, IReadOnlyList<string> piecesNew, IReadOnlyList<DiffBlock> blocks)
{
    /// <summary>The old text split into pieces (lines, words or characters).</summary>
    public IReadOnlyList<string> PiecesOld { get; } = piecesOld;

    /// <summary>The new text split into pieces.</summary>
    public IReadOnlyList<string> PiecesNew { get; } = piecesNew;

    /// <summary>The changes, ordered by position.</summary>
    public IReadOnlyList<DiffBlock> Blocks { get; } = blocks;
}

/// <summary>How a line of an inline diff relates to the two texts.</summary>
public enum DiffChange
{
    /// <summary>Present in both.</summary>
    Unchanged,

    /// <summary>Only in the old text.</summary>
    Deleted,

    /// <summary>Only in the new text.</summary>
    Inserted,
}

/// <summary>A line of an inline (unified) diff, with its 1-based number in each text where it exists.</summary>
public readonly record struct DiffLine(DiffChange Change, string Text, int? OldLineNumber, int? NewLineNumber);

/// <summary>Differences between texts by line, word or character, and between arbitrary sequences.</summary>
public static class TextDiff
{
    /// <summary>Separators used by <see cref="Words"/> when none are given.</summary>
    public static ReadOnlySpan<char> DefaultSeparators => " \t\r\n.,;:!?()[]{}\"'";

    /// <summary>Line diff. Lines are split on CRLF, LF or CR, without their terminators.</summary>
    public static DiffResult Lines(string oldText, string newText, bool ignoreWhitespace = false, bool ignoreCase = false)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);
        return Pieces(SplitLines(oldText), SplitLines(newText), new PieceComparer(ignoreWhitespace, ignoreCase));
    }

    /// <summary>Word diff: each run of non-separator characters is a piece and each separator is a piece of
    /// its own, so joining the pieces gives back the text exactly.</summary>
    public static DiffResult Words(string oldText, string newText, ReadOnlySpan<char> separators = default, bool ignoreCase = false)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);
        var set = separators.IsEmpty ? DefaultSeparators : separators;
        return Pieces(SplitWords(oldText, set), SplitWords(newText, set), new PieceComparer(false, ignoreCase));
    }

    /// <summary>Character diff (UTF-16 units).</summary>
    public static DiffResult Characters(string oldText, string newText)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);
        return Pieces(oldText.Select(c => c.ToString()).ToList(), newText.Select(c => c.ToString()).ToList(), StringComparer.Ordinal);
    }

    /// <summary>Diff of already split pieces.</summary>
    public static DiffResult Pieces(IReadOnlyList<string> oldPieces, IReadOnlyList<string> newPieces, IEqualityComparer<string>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(oldPieces);
        ArgumentNullException.ThrowIfNull(newPieces);
        return new DiffResult(oldPieces, newPieces, SequenceDiff.Compute(oldPieces, newPieces, comparer ?? StringComparer.Ordinal));
    }

    /// <summary>Inline line diff: unchanged lines, then for each change its deleted lines followed by its
    /// inserted lines.</summary>
    public static IReadOnlyList<DiffLine> Inline(string oldText, string newText, bool ignoreWhitespace = false)
    {
        var diff = Lines(oldText, newText, ignoreWhitespace);
        var lines = new List<DiffLine>();
        var a = 0;
        var b = 0;
        foreach (var block in diff.Blocks)
        {
            while (a < block.DeleteStartA)
            {
                lines.Add(new DiffLine(DiffChange.Unchanged, diff.PiecesNew[b], a + 1, b + 1));
                a++;
                b++;
            }

            for (; a < block.DeleteStartA + block.DeleteCountA; a++)
            {
                lines.Add(new DiffLine(DiffChange.Deleted, diff.PiecesOld[a], a + 1, null));
            }

            for (; b < block.InsertStartB + block.InsertCountB; b++)
            {
                lines.Add(new DiffLine(DiffChange.Inserted, diff.PiecesNew[b], null, b + 1));
            }
        }

        for (; a < diff.PiecesOld.Count; a++, b++)
        {
            lines.Add(new DiffLine(DiffChange.Unchanged, diff.PiecesNew[b], a + 1, b + 1));
        }

        return lines;
    }

    internal static List<string> SplitLines(string text)
    {
        var lines = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is '\r' or '\n')
            {
                lines.Add(text[start..i]);
                if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                start = i + 1;
            }
        }

        if (start < text.Length)
        {
            lines.Add(text[start..]);
        }

        return lines;
    }

    private static List<string> SplitWords(string text, ReadOnlySpan<char> separators)
    {
        var pieces = new List<string>();
        var word = new StringBuilder();
        foreach (var c in text)
        {
            if (separators.Contains(c))
            {
                if (word.Length > 0)
                {
                    pieces.Add(word.ToString());
                    word.Clear();
                }

                pieces.Add(c.ToString());
            }
            else
            {
                word.Append(c);
            }
        }

        if (word.Length > 0)
        {
            pieces.Add(word.ToString());
        }

        return pieces;
    }

    private sealed class PieceComparer(bool ignoreWhitespace, bool ignoreCase) : IEqualityComparer<string>
    {
        private readonly StringComparer _inner = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        public bool Equals(string? x, string? y) => _inner.Equals(Normalize(x), Normalize(y));

        public int GetHashCode(string obj) => _inner.GetHashCode(Normalize(obj)!);

        private string? Normalize(string? value) => ignoreWhitespace && value is not null ? value.Trim() : value;
    }
}

/// <summary>Shortest edit script between two sequences.</summary>
public static class SequenceDiff
{
    /// <summary>The changes that turn <paramref name="oldItems"/> into <paramref name="newItems"/>.</summary>
    public static IReadOnlyList<DiffBlock> Compute<T>(IReadOnlyList<T> oldItems, IReadOnlyList<T> newItems, IEqualityComparer<T>? comparer = null)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(oldItems);
        ArgumentNullException.ThrowIfNull(newItems);
        var symbols = new Dictionary<T, int>(comparer ?? EqualityComparer<T>.Default);
        var a = Encode(oldItems, symbols);
        var b = Encode(newItems, symbols);
        var diff = MyersDiff.Run(a, b);
        return ToBlocks(diff.ModifiedA, diff.ModifiedB);
    }

    private static int[] Encode<T>(IReadOnlyList<T> items, Dictionary<T, int> symbols)
        where T : notnull
    {
        var codes = new int[items.Count];
        for (var i = 0; i < codes.Length; i++)
        {
            if (!symbols.TryGetValue(items[i], out var code))
            {
                code = symbols.Count;
                symbols.Add(items[i], code);
            }

            codes[i] = code;
        }

        return codes;
    }

    private static List<DiffBlock> ToBlocks(bool[] modifiedA, bool[] modifiedB)
    {
        var blocks = new List<DiffBlock>();
        var a = 0;
        var b = 0;
        while (a < modifiedA.Length || b < modifiedB.Length)
        {
            if (a < modifiedA.Length && b < modifiedB.Length && !modifiedA[a] && !modifiedB[b])
            {
                a++;
                b++;
                continue;
            }

            var startA = a;
            var startB = b;
            while (a < modifiedA.Length && modifiedA[a])
            {
                a++;
            }

            while (b < modifiedB.Length && modifiedB[b])
            {
                b++;
            }

            blocks.Add(new DiffBlock(startA, a - startA, startB, b - startB));
        }

        return blocks;
    }
}
