// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Layout;

/// <summary>A letter with its position in its text direction: x along the baseline, y across it.</summary>
internal readonly record struct PlacedLetter(PdfLetter Letter, double X, double Y)
{
    /// <summary>The font size, at least one point.</summary>
    public double Size => Math.Max(Letter.FontSize, 1);

    /// <summary>Where the advance of the letter ends.</summary>
    public double Right => X + Letter.Width;

    /// <summary>True for a space or another blank glyph.</summary>
    public bool IsBlank => string.IsNullOrWhiteSpace(Letter.Value);

    /// <summary>True for a letter or a digit (not a blank, a punctuation mark or an accent drawn on its own).</summary>
    public bool IsLetterOrDigit => Letter.Value.Any(char.IsLetterOrDigit);
}

/// <summary>
/// Groups letters into lines and tells where words break inside a line. Letters first gather by baseline. Two
/// baselines whose letters or digits are drawn over each other are two lines: a baseline never joins a line one of
/// whose baselines it covers. Otherwise a baseline joins the line above when it lies within a third of the smaller
/// font size of that line's main (largest) baseline, or when it is a smaller run raised or lowered by at most half
/// the larger size (a superscript, a subscript, a footnote reference) whose every run sits against a letter of the
/// main baseline. A word ends at a blank, at a gap wider than a fifth of the smaller of the two letters' sizes, or
/// where the baseline shifts by more than 15 % of the larger size (5 % when the size changes too); a line segment
/// ends at a gap wider than 1.6 times the smaller size. A blank lying for more than half its advance over visible
/// letters and touching another blank (a row of spaces printed under the text) is no word break and is left out; a
/// blank on its own whose advance runs under the next letters (a space ending a table cell, the next cell starting
/// before its advance is over) still ends its word. Letters spaced out (tracked)
/// are measured against their own spacing: in a run of the line (between gutters and baseline shifts) with at
/// least three gaps between visible letters, when the median gap is wider than a word gap but at most three
/// quarters of the size, and at least two thirds of the gaps lie within a tenth of the size of it, that gap is the
/// run's tracking and is taken off every gap between two visible letters of the run before it is judged.
/// </summary>
internal static class LineGrouping
{
    // Letters this close (fraction of their size) are on one baseline: rounding noise of the producer.
    private const double SameBaseline = 0.02;

    // Baselines this close (fraction of the smaller size) are one line, whatever their sizes.
    private const double LineShift = 1.0 / 3;

    // A raised or lowered run is at most this size (fraction of the larger) and this far (fraction of the larger).
    private const double ScriptRatio = 0.85;
    private const double ScriptShift = 0.5;

    // How far (fraction of the larger size) a run may stand from its neighbours, or overlap them, and still touch.
    private const double Reach = 0.25;

    private const double WordGap = 0.2;
    private const double SegmentGap = 1.6;
    private const double BaselineShift = 0.15;
    private const double ResizedShift = 0.05;

    // A blank covering visible letters over more than this fraction of its advance is printed under the text.
    private const double Underlay = 0.5;

    // Blanks this close (fraction of the size) touch: one row of blanks.
    private const double Touching = 0.1;

    // Tracking: at least this many gaps, at most this wide (fraction of the size), this even (fraction of the size).
    private const int TrackedGaps = 3;
    private const double MaxTracking = 0.75;
    private const double Evenness = 0.1;

    /// <summary>The letters of each line, lines from top to bottom (decreasing y).</summary>
    public static List<List<PlacedLetter>> Group(IEnumerable<PlacedLetter> letters)
    {
        var lines = new List<List<Baseline>>();
        foreach (var baseline in Baselines(letters))
        {
            if (lines.Count > 0 && Joins(lines[^1], baseline))
            {
                lines[^1].Add(baseline);
            }
            else
            {
                lines.Add([baseline]);
            }
        }

        return lines.Select(l => l.SelectMany(b => b.Letters).ToList()).ToList();
    }

    /// <summary>
    /// The letters of a line in order along it, blanks printed under visible letters left out, each with the tracking
    /// of its run to take off the gap before it (0 outside a tracked run).
    /// </summary>
    public static List<(PlacedLetter Letter, double Tracking)> Arrange(IEnumerable<PlacedLetter> line)
    {
        var sorted = line.OrderBy(l => l.X).ToList();
        var visible = sorted.Where(l => !l.IsBlank).ToList();
        var blanks = sorted.Where(l => l.IsBlank).ToList();
        var kept = sorted.Where(l => !l.IsBlank || !IsUnderlay(l, visible, blanks)).ToList();
        var tracking = new double[kept.Count];
        var start = 0;
        for (var i = 1; i <= kept.Count; i++)
        {
            if (i == kept.Count || Between(kept[i - 1], kept[i]) == LetterBreak.Segment || IsShifted(kept[i - 1], kept[i]))
            {
                Track(kept, start, i, tracking);
                start = i;
            }
        }

        return kept.Select((l, i) => (l, tracking[i])).ToList();
    }

    /// <summary>What separates two letters that follow each other along a line, <paramref name="tracking"/> taken
    /// off the gap between them.</summary>
    public static LetterBreak Between(PlacedLetter previous, PlacedLetter current, double tracking = 0)
    {
        var gap = current.X - previous.Right - tracking;
        var smaller = Math.Min(previous.Size, current.Size);
        if (gap > smaller * SegmentGap)
        {
            return LetterBreak.Segment;
        }

        return current.IsBlank || gap > smaller * WordGap || IsShifted(previous, current) ? LetterBreak.Word : LetterBreak.None;
    }

    /// <summary>
    /// True when the baseline moves between two letters by more than 15 % of the larger size, or by more than 5 % when
    /// the size changes too (a smaller superscript or subscript).
    /// </summary>
    public static bool IsShifted(PlacedLetter previous, PlacedLetter current)
    {
        var larger = Math.Max(previous.Size, current.Size);
        var resized = Math.Min(previous.Size, current.Size) <= larger * ScriptRatio;
        return Math.Abs(current.Y - previous.Y) > larger * (resized ? ResizedShift : BaselineShift);
    }

    // A blank whose advance lies for more than half over visible letters and that touches another blank: one of a
    // row printed under the text, not between words. A lone blank overlapped by what follows it ends its word.
    private static bool IsUnderlay(PlacedLetter blank, List<PlacedLetter> visible, List<PlacedLetter> blanks)
    {
        var covered = 0.0;
        for (var i = Math.Max(FirstAtOrAfter(visible, blank.X) - 1, 0); i < visible.Count && visible[i].X < blank.Right; i++)
        {
            covered += Math.Max(0, Math.Min(blank.Right, visible[i].Right) - Math.Max(blank.X, visible[i].X));
        }

        return covered > blank.Letter.Width * Underlay && TouchesBlank(blank, blanks);
    }

    // Another blank ends where this one starts or starts where this one ends (within a tenth of the size).
    private static bool TouchesBlank(PlacedLetter blank, List<PlacedLetter> blanks)
    {
        var tolerance = blank.Size * Touching;
        for (var i = FirstAtOrAfter(blanks, blank.X - (blank.Size * 2)); i < blanks.Count && blanks[i].X <= blank.Right + tolerance; i++)
        {
            var other = blanks[i];
            if (other != blank && (Math.Abs(other.Right - blank.X) <= tolerance || Math.Abs(other.X - blank.Right) <= tolerance))
            {
                return true;
            }
        }

        return false;
    }

    // The letters [start, end) of a run: when the gaps between its visible letters are mostly one even width,
    // wider than a word gap, that width is the run's tracking.
    private static void Track(List<PlacedLetter> letters, int start, int end, double[] tracking)
    {
        var pairs = Enumerable.Range(start + 1, end - start - 1).Where(i => !letters[i - 1].IsBlank && !letters[i].IsBlank).ToList();
        if (pairs.Count < TrackedGaps)
        {
            return;
        }

        var gaps = pairs.Select(i => letters[i].X - letters[i - 1].Right).Order().ToList();
        var sizes = pairs.Select(i => letters[i].Size).Order().ToList();
        var (shared, size) = (gaps[(gaps.Count - 1) / 2], sizes[(sizes.Count - 1) / 2]);
        var even = gaps.Count(g => Math.Abs(g - shared) <= size * Evenness);
        if (shared <= size * WordGap || shared > size * MaxTracking || even * 3 < gaps.Count * 2)
        {
            return;
        }

        foreach (var i in pairs)
        {
            tracking[i] = shared;
        }
    }

    private static List<Baseline> Baselines(IEnumerable<PlacedLetter> letters)
    {
        var baselines = new List<Baseline>();
        foreach (var letter in letters.OrderByDescending(l => l.Y).ThenBy(l => l.X))
        {
            if (baselines.Count > 0 && baselines[^1].Y - letter.Y <= letter.Size * SameBaseline)
            {
                baselines[^1].Letters.Add(letter);
            }
            else
            {
                baselines.Add(new Baseline(letter.Y, [letter]));
            }
        }

        return baselines;
    }

    private static bool Joins(List<Baseline> line, Baseline next)
    {
        if (line.Exists(b => Covers(next, b)))
        {
            return false;
        }

        var main = Main(line);
        var (small, large) = next.Size < main.Size ? (next, main) : (main, next);
        var shift = Math.Abs(main.Y - next.Y);
        if (shift <= small.Size * LineShift)
        {
            return true;
        }

        return IsScript(small, large, shift) && SitsAgainst(small, large, large.Size * Reach);
    }

    // The baseline the others are measured against: the largest one carrying a letter or digit (the topmost on a tie),
    // so a row of underscores or dots drawn a little higher does not set the line.
    private static Baseline Main(List<Baseline> line) =>
        line.Where(b => b.Letters.Exists(l => l.IsLetterOrDigit)).MaxBy(b => b.Size) ?? line.MaxBy(b => b.Size)!;

    // Smaller and raised or lowered by at most half the larger size, both baselines carrying visible text (a stray
    // blank drawn on a baseline of its own neither hosts nor is a superscript).
    private static bool IsScript(Baseline small, Baseline large, double shift) =>
        small.Size <= large.Size * ScriptRatio && shift <= large.Size * ScriptShift
        && small.Letters.Exists(l => !l.IsBlank) && large.Letters.Exists(l => !l.IsBlank);

    // A letter or digit of one baseline drawn over one of the other: two lines printed over each other.
    private static bool Covers(Baseline one, Baseline other)
    {
        var reach = Math.Max(one.Size, other.Size) * Reach;
        return Runs(one.Letters.Where(l => l.IsLetterOrDigit), other.Letters.Where(l => l.IsLetterOrDigit)).Any(g => g.Before < -reach || g.After < -reach);
    }

    // Each run of the smaller baseline touches a letter of the larger one on one side: text raised or lowered
    // inside the line, not a line of its own beside it.
    private static bool SitsAgainst(Baseline small, Baseline large, double reach) =>
        Runs(small.Letters, large.Letters).All(g => g.Before <= reach || g.After <= reach);

    // The runs of the first letters (those between two consecutive letters of the others), each with its distance to
    // the other letter just before and just after it (negative when they overlap, infinite when there is none).
    private static IEnumerable<(double Before, double After)> Runs(IEnumerable<PlacedLetter> letters, IEnumerable<PlacedLetter> others)
    {
        var sorted = others.OrderBy(l => l.X).ToList();
        return letters.GroupBy(l => FirstAtOrAfter(sorted, l.X)).Select(run =>
        {
            var before = run.Key > 0 ? run.Min(l => l.X) - sorted[run.Key - 1].Right : double.PositiveInfinity;
            var after = run.Key < sorted.Count ? sorted[run.Key].X - run.Max(l => l.Right) : double.PositiveInfinity;
            return (before, after);
        });
    }

    private static int FirstAtOrAfter(List<PlacedLetter> sorted, double x)
    {
        var (low, high) = (0, sorted.Count);
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (sorted[middle].X < x)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    // Letters on one baseline; its size is the most frequent size of its letters (the larger on a tie).
    private sealed record Baseline(double Y, List<PlacedLetter> Letters)
    {
        public double Size => Letters.GroupBy(l => Math.Round(l.Size, 1)).MaxBy(g => (g.Count(), g.Key))!.Key;
    }
}

/// <summary>What separates two consecutive letters of a line.</summary>
internal enum LetterBreak
{
    /// <summary>Same word.</summary>
    None,

    /// <summary>A new word.</summary>
    Word,

    /// <summary>A new line segment (a gap as wide as a column gutter).</summary>
    Segment,
}
