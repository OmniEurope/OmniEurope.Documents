// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Diff;

namespace OmniEurope.Documents.Tests.Diff;

public sealed class TextDiffTests
{
    [Fact]
    public void Random_sequences_get_a_minimal_script_that_rebuilds_the_new_one()
    {
        var random = new Random(20261006);
        for (var round = 0; round < 400; round++)
        {
            var a = RandomSequence(random, random.Next(0, 40), alphabet: 1 + random.Next(4));
            var b = round % 5 == 0 ? Mutate(random, a) : RandomSequence(random, random.Next(0, 40), alphabet: 1 + random.Next(4));

            var blocks = SequenceDiff.Compute(a, b);

            Assert.Equal(b, Apply(a, b, blocks));
            var edits = blocks.Sum(x => x.DeleteCountA + x.InsertCountB);
            Assert.Equal(a.Length + b.Length - (2 * Lcs(a, b)), edits);
        }
    }

    [Fact]
    public void Large_inputs_are_handled_without_quadratic_memory()
    {
        var a = Enumerable.Range(0, 60_000).Select(i => i % 997).ToArray();
        var b = a.Where((_, i) => i % 1000 != 0).Concat([-1, -2]).ToArray();

        var blocks = SequenceDiff.Compute(a, b);

        Assert.Equal(b, Apply(a, b, blocks));
    }

    [Fact]
    public void Line_diff_reports_blocks_and_pieces()
    {
        var diff = TextDiff.Lines("a\r\nb\nc\nd", "a\nB\nc\nd\ne");

        Assert.Equal(["a", "b", "c", "d"], diff.PiecesOld);
        Assert.Equal([new DiffBlock(1, 1, 1, 1), new DiffBlock(4, 0, 4, 1)], diff.Blocks);
    }

    [Fact]
    public void Ignore_options_hide_whitespace_and_case_changes()
    {
        Assert.Empty(TextDiff.Lines("  x\nY", "x  \ny", ignoreWhitespace: true, ignoreCase: true).Blocks);
        Assert.Single(TextDiff.Lines("x", " x").Blocks);
    }

    [Fact]
    public void Word_diff_keeps_separators_so_pieces_rebuild_the_text()
    {
        var diff = TextDiff.Words("the quick fox", "the slow fox", [' ']);

        Assert.Equal("the quick fox", string.Concat(diff.PiecesOld));
        Assert.Equal([new DiffBlock(2, 1, 2, 1)], diff.Blocks);
        Assert.Equal("quick", diff.PiecesOld[2]);
        Assert.Equal("slow", diff.PiecesNew[2]);
    }

    [Fact]
    public void Inline_diff_lists_deletions_before_insertions_with_line_numbers()
    {
        var lines = TextDiff.Inline("one\ntwo\nthree", "one\n2\nthree\nfour");

        Assert.Equal(
            [
                new DiffLine(DiffChange.Unchanged, "one", 1, 1),
                new DiffLine(DiffChange.Deleted, "two", 2, null),
                new DiffLine(DiffChange.Inserted, "2", null, 2),
                new DiffLine(DiffChange.Unchanged, "three", 3, 3),
                new DiffLine(DiffChange.Inserted, "four", null, 4),
            ],
            lines);
    }

    [Fact]
    public void Character_diff_and_empty_inputs()
    {
        Assert.Equal([new DiffBlock(1, 1, 1, 1)], TextDiff.Characters("cat", "cut").Blocks);
        Assert.Empty(TextDiff.Lines(string.Empty, string.Empty).Blocks);
        Assert.Equal([new DiffBlock(0, 0, 0, 2)], TextDiff.Lines(string.Empty, "a\nb").Blocks);
    }

    private static int[] RandomSequence(Random random, int length, int alphabet) =>
        Enumerable.Range(0, length).Select(_ => random.Next(alphabet)).ToArray();

    private static int[] Mutate(Random random, int[] source)
    {
        var list = source.ToList();
        for (var i = 0; i < 3 && list.Count > 0; i++)
        {
            list.RemoveAt(random.Next(list.Count));
            list.Insert(random.Next(list.Count + 1), random.Next(9));
        }

        return list.ToArray();
    }

    private static int[] Apply(int[] a, int[] b, IReadOnlyList<DiffBlock> blocks)
    {
        var result = new List<int>();
        var position = 0;
        foreach (var block in blocks)
        {
            result.AddRange(a[position..block.DeleteStartA]);
            result.AddRange(b.Skip(block.InsertStartB).Take(block.InsertCountB));
            position = block.DeleteStartA + block.DeleteCountA;
        }

        result.AddRange(a[position..]);
        return result.ToArray();
    }

    private static int Lcs(int[] a, int[] b)
    {
        var table = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
        {
            for (var j = b.Length - 1; j >= 0; j--)
            {
                table[i, j] = a[i] == b[j] ? table[i + 1, j + 1] + 1 : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        return table[0, 0];
    }
}
