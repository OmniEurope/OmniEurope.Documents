// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using System.Text.RegularExpressions;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Layout;

/// <summary>
/// Recovers the structure of text pages: letters into words (by baseline and gap), words into line segments
/// (never across a column gutter), segments into blocks (aligned lines with a regular spacing and size), and
/// blocks into reading order by recursive XY-cut (columns read top to bottom, left column first). Across a
/// document, blocks repeated near the top or bottom of most pages are marked as decoration.
/// Text written at an angle is laid out in its own direction.
/// A superscript or subscript (a smaller run raised or lowered against the text, such as a footnote reference)
/// stays on the line of the text it belongs to but is a word of its own; letters of different sizes on one
/// baseline with no gap between them (small capitals) stay one word.
/// </summary>
public static class PdfLayoutAnalyzer
{
    /// <summary>The layout of every page, decoration flagged across the document.</summary>
    public static IReadOnlyList<PdfPageLayout> Analyze(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var pages = document.Pages.Select(AnalyzePage).ToList();
        return Decorations.Mark(pages);
    }

    /// <summary>The layout of one page (no cross-page decoration detection).</summary>
    public static PdfPageLayout AnalyzePage(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var lines = Lines(page.Letters);
        var blocks = BlockBuilder.Build(lines);
        var ordered = XyCut.Order(blocks).Select((b, i) => b with { ReadingOrder = i }).ToList();
        return new PdfPageLayout(page, ordered, IsScanned(page));
    }

    /// <summary>The words of a page in reading order of their lines.</summary>
    public static IReadOnlyList<PdfWord> Words(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return Lines(page.Letters).SelectMany(l => l.Words).ToList();
    }

    /// <summary>
    /// A page is treated as scanned when it carries almost no text (fewer than 20 letters other than spaces)
    /// and an image covers at least half of it.
    /// </summary>
    public static bool IsScanned(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (page.Letters.Count(l => !string.IsNullOrWhiteSpace(l.Value)) >= 20)
        {
            return false;
        }

        var area = page.CropBox.Width * page.CropBox.Height;
        return page.Images.Any(i => i.Bounds.Width * i.Bounds.Height >= area * 0.5);
    }

    internal static List<PdfTextLine> Lines(IReadOnlyList<PdfLetter> letters)
    {
        var lines = new List<PdfTextLine>();
        foreach (var direction in letters.Where(l => l.RenderingMode != 7).GroupBy(l => Math.Round(l.Rotation / 5) * 5))
        {
            var angle = direction.Key * Math.PI / 180;
            var (cos, sin) = (Math.Cos(-angle), Math.Sin(-angle));
            var projected = direction.Select(l => new PlacedLetter(l, (l.X * cos) - (l.Y * sin), (l.X * sin) + (l.Y * cos)));
            foreach (var line in LineGrouping.Group(projected))
            {
                lines.AddRange(Segments(line));
            }
        }

        return lines;
    }

    // Splits a line into words at blanks, gaps and baseline shifts, and into segments at gaps wide enough to be a
    // gutter.
    private static IEnumerable<PdfTextLine> Segments(List<PlacedLetter> line)
    {
        var words = new List<List<PlacedLetter>>();
        var segments = new List<List<List<PlacedLetter>>>();
        List<PlacedLetter>? word = null;
        PlacedLetter? previous = null;
        foreach (var current in line.OrderBy(l => l.X))
        {
            var cut = previous is { } p ? LineGrouping.Between(p, current) : LetterBreak.None;
            if (cut != LetterBreak.None)
            {
                Close(ref word, words);
            }

            if (cut == LetterBreak.Segment && words.Count > 0)
            {
                segments.Add(words);
                words = [];
            }

            if (!current.IsBlank)
            {
                (word ??= []).Add(current);
            }

            previous = current;
        }

        Close(ref word, words);
        if (words.Count > 0)
        {
            segments.Add(words);
        }

        return segments.Select(segment =>
        {
            var built = segment.Select(w => new PdfWord(string.Concat(w.Select(l => l.Letter.Value)), Bounds(w.Select(l => l.Letter.BoundingBox)), w.Select(l => l.Letter).ToList())).ToList();
            return new PdfTextLine(built, Bounds(built.Select(w => w.BoundingBox)));
        });
    }

    private static void Close(ref List<PlacedLetter>? word, List<List<PlacedLetter>> words)
    {
        if (word is { Count: > 0 })
        {
            words.Add(word);
        }

        word = null;
    }

    internal static PdfRectangle Bounds(IEnumerable<PdfRectangle> boxes)
    {
        PdfRectangle? result = null;
        foreach (var box in boxes)
        {
            result = result is { } r ? r.Union(box) : box;
        }

        return result ?? default;
    }
}

/// <summary>Groups line segments into blocks.</summary>
internal static class BlockBuilder
{
    public static List<PdfTextBlock> Build(List<PdfTextLine> lines)
    {
        var blocks = new List<List<PdfTextLine>>();
        foreach (var line in lines.OrderByDescending(l => l.BoundingBox.Top).ThenBy(l => l.BoundingBox.Left))
        {
            var target = blocks.LastOrDefault(b => Continues(b, line));
            if (target is null)
            {
                blocks.Add([line]);
            }
            else
            {
                target.Add(line);
            }
        }

        return blocks.Select(b => new PdfTextBlock(b, PdfLayoutAnalyzer.Bounds(b.Select(l => l.BoundingBox)))).ToList();
    }

    // The next line of a block: just below its last line, overlapping it horizontally, same size, and
    // not much further away than the block's own line spacing.
    private static bool Continues(List<PdfTextLine> block, PdfTextLine line)
    {
        var last = block[^1];
        var size = Math.Max(last.FontSize, 1);
        if (Math.Abs(last.FontSize - line.FontSize) > size * 0.25)
        {
            return false;
        }

        var gap = last.Baseline - line.Baseline;
        if (gap <= 0)
        {
            return false;
        }

        var expected = block.Count >= 2 ? block[^2].Baseline - last.Baseline : size * 1.25;
        if (gap > Math.Max(expected * 1.3, size * 1.1) || gap > size * 2.2)
        {
            return false;
        }

        var overlap = Math.Min(last.BoundingBox.Right, line.BoundingBox.Right) - Math.Max(last.BoundingBox.Left, line.BoundingBox.Left);
        return overlap > Math.Min(last.BoundingBox.Width, line.BoundingBox.Width) * 0.3;
    }
}

/// <summary>Recursive XY-cut: split by the widest horizontal white band, else by the widest vertical one.</summary>
internal static class XyCut
{
    public static List<PdfTextBlock> Order(List<PdfTextBlock> blocks)
    {
        if (blocks.Count <= 1)
        {
            return blocks;
        }

        // The wider white band wins: a column gutter beats the space between two paragraphs, while a
        // full-width title leaves no gutter to cut at all.
        var rows = Split(blocks, horizontal: true, out var rowGap);
        var columns = Split(blocks, horizontal: false, out var columnGap);
        var chosen = rows is not null && (columns is null || rowGap >= columnGap) ? rows : columns;
        return chosen is null
            ? blocks.OrderByDescending(b => b.BoundingBox.Top).ThenBy(b => b.BoundingBox.Left).ToList()
            : chosen.SelectMany(Order).ToList();
    }

    // Splits at every gap of the projection; rows top to bottom, columns left to right. Null when no gap.
    private static List<List<PdfTextBlock>>? Split(List<PdfTextBlock> blocks, bool horizontal, out double widestGap)
    {
        widestGap = 0;
        var spans = blocks.Select(b => horizontal ? (Start: -b.BoundingBox.Top, End: -b.BoundingBox.Bottom, Block: b) : (Start: b.BoundingBox.Left, End: b.BoundingBox.Right, Block: b))
            .OrderBy(s => s.Start).ToList();
        var groups = new List<List<PdfTextBlock>>();
        var current = new List<PdfTextBlock>();
        var reach = double.MinValue;
        foreach (var span in spans)
        {
            if (current.Count > 0 && span.Start > reach)
            {
                widestGap = Math.Max(widestGap, span.Start - reach);
                groups.Add(current);
                current = [];
            }

            current.Add(span.Block);
            reach = Math.Max(reach, span.End);
        }

        groups.Add(current);
        return groups.Count > 1 ? groups : null;
    }
}

/// <summary>Marks running headers, footers and page numbers.</summary>
internal static class Decorations
{
    private static readonly Regex Digits = new(@"\d+", RegexOptions.CultureInvariant);

    public static List<PdfPageLayout> Mark(List<PdfPageLayout> pages)
    {
        if (pages.Count < 2)
        {
            return pages;
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            foreach (var key in page.Blocks.Where(b => InMargin(b, page.Page)).Select(b => Key(b, page.Page)).Distinct())
            {
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
        }

        var threshold = Math.Max(2, (pages.Count + 1) / 2);
        return pages.Select(page => page with
        {
            Blocks = page.Blocks.Select(b => InMargin(b, page.Page) && (counts.GetValueOrDefault(Key(b, page.Page)) >= threshold || IsPageNumber(b))
                ? b with { IsDecoration = true }
                : b).ToList(),
        }).ToList();
    }

    private static bool InMargin(PdfTextBlock block, PdfPage page)
    {
        var box = page.CropBox;
        var band = box.Height * 0.12;
        return block.BoundingBox.Bottom >= box.Top - band || block.BoundingBox.Top <= box.Bottom + band;
    }

    // Same text with numbers masked, in the same band and at about the same height.
    private static string Key(PdfTextBlock block, PdfPage page)
    {
        var text = Digits.Replace(block.Text.Trim(), "#");
        var top = block.BoundingBox.Bottom >= page.CropBox.Top - (page.CropBox.Height * 0.12);
        var height = Math.Round((block.BoundingBox.Bottom - page.CropBox.Bottom) / 10);
        return new StringBuilder(text).Append('|').Append(top ? 'T' : 'B').Append('|').Append(height).ToString();
    }

    private static bool IsPageNumber(PdfTextBlock block)
    {
        var text = block.Text.Trim();
        return text.Length is > 0 and < 16 && Regex.IsMatch(text, @"^(page\s*)?[-–]?\s*\d+(\s*(/|sur|of|de)\s*\d+)?\s*[-–]?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
