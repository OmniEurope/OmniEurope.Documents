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
/// Text written at an angle (a page or a table turned by 90 degrees, a vertical column heading) is grouped by
/// direction first: its words, lines and blocks are built along its own baselines, and the blocks of all
/// directions are read in the frame of the direction carrying the most letters.
/// Blanks printed under visible letters are no word breaks, and letter-spaced (tracked) text is split into words
/// against its own letter spacing.
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
        var directions = LinesByDirection(page.Letters).ToList();
        var reading = directions.OrderByDescending(d => d.Lines.Sum(l => l.Words.Sum(w => w.Letters.Count))).Select(d => d.Direction).FirstOrDefault();
        var blocks = directions.SelectMany(d => BlockBuilder.Build(d.Lines, d.Direction)).ToList();
        var ordered = XyCut.Order(blocks, reading).Select((b, i) => b with { ReadingOrder = i }).ToList();
        return new PdfPageLayout(page, ordered, IsScanned(page));
    }

    /// <summary>The words of a page in reading order of their lines.</summary>
    public static IReadOnlyList<PdfWord> Words(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return Lines(page.Letters).SelectMany(l => l.Words).ToList();
    }

    /// <summary>
    /// The line segments of a page, each text direction read along its own baselines: lines top to bottom, segments
    /// left to right, in the frame of their direction (a table turned on the page is read row after row).
    /// </summary>
    public static IReadOnlyList<PdfTextLine> Lines(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return Lines(page.Letters);
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

    internal static List<PdfTextLine> Lines(IReadOnlyList<PdfLetter> letters) => LinesByDirection(letters).SelectMany(d => d.Lines).ToList();

    // The line segments of each direction, top to bottom along it; clipping-only text (mode 7) is not drawn.
    private static IEnumerable<(TextDirection Direction, List<PdfTextLine> Lines)> LinesByDirection(IReadOnlyList<PdfLetter> letters) =>
        TextDirection.Group(letters.Where(l => l.RenderingMode != 7)).Select(d => (d.Direction, LineGrouping.Group(d.Letters).SelectMany(Segments).ToList()));

    // Splits a line into words at blanks, gaps and baseline shifts, and into segments at gaps wide enough to be a
    // gutter.
    private static IEnumerable<PdfTextLine> Segments(List<PlacedLetter> line)
    {
        var words = new List<List<PlacedLetter>>();
        var segments = new List<List<List<PlacedLetter>>>();
        List<PlacedLetter>? word = null;
        PlacedLetter? previous = null;
        foreach (var (current, tracking) in LineGrouping.Arrange(line))
        {
            var cut = previous is { } p ? LineGrouping.Between(p, current, tracking) : LetterBreak.None;
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

/// <summary>Groups the line segments of one direction into blocks, measured along that direction.</summary>
internal static class BlockBuilder
{
    public static List<PdfTextBlock> Build(List<PdfTextLine> lines, TextDirection direction)
    {
        var blocks = new List<List<FramedLine>>();
        foreach (var line in lines.Select(l => FramedLine.Of(l, direction)).OrderByDescending(l => l.Box.Top).ThenBy(l => l.Box.Left))
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

        return blocks.Select(b => new PdfTextBlock(b.Select(l => l.Line).ToList(), PdfLayoutAnalyzer.Bounds(b.Select(l => l.Line.BoundingBox)))).ToList();
    }

    // The next line of a block: just below its last line, overlapping it horizontally, same size, and
    // not much further away than the block's own line spacing.
    private static bool Continues(List<FramedLine> block, FramedLine line)
    {
        var last = block[^1];
        var size = Math.Max(last.Line.FontSize, 1);
        if (Math.Abs(last.Line.FontSize - line.Line.FontSize) > size * 0.25)
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

        var overlap = Math.Min(last.Box.Right, line.Box.Right) - Math.Max(last.Box.Left, line.Box.Left);
        return overlap > Math.Min(last.Box.Width, line.Box.Width) * 0.3;
    }

    // A line with its box and baseline in the frame of its direction (the baseline shared by the most letters,
    // weighted by their size, as PdfTextLine.Baseline is on the page).
    private sealed record FramedLine(PdfTextLine Line, PdfRectangle Box, double Baseline)
    {
        public static FramedLine Of(PdfTextLine line, TextDirection direction)
        {
            var baseline = line.Words.SelectMany(w => w.Letters).Select(l => (Y: direction.Project(l.X, l.Y).Y, Size: l.FontSize))
                .GroupBy(l => Math.Round(l.Y, 1)).MaxBy(g => g.Sum(l => l.Size))!.First().Y;
            return new FramedLine(line, direction.Project(line.BoundingBox), baseline);
        }
    }
}

/// <summary>
/// Recursive XY-cut in the frame of the page's main text direction: split by the widest white band across it, else
/// by the widest one along it.
/// </summary>
internal static class XyCut
{
    public static List<PdfTextBlock> Order(List<PdfTextBlock> blocks, TextDirection direction) =>
        Order(blocks.Select(b => new FramedBlock(b, direction.Project(b.BoundingBox))).ToList()).Select(b => b.Block).ToList();

    private static List<FramedBlock> Order(List<FramedBlock> blocks)
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
            ? blocks.OrderByDescending(b => b.Box.Top).ThenBy(b => b.Box.Left).ToList()
            : chosen.SelectMany(Order).ToList();
    }

    // Splits at every gap of the projection; rows top to bottom, columns left to right. Null when no gap.
    private static List<List<FramedBlock>>? Split(List<FramedBlock> blocks, bool horizontal, out double widestGap)
    {
        widestGap = 0;
        var spans = blocks.Select(b => horizontal ? (Start: -b.Box.Top, End: -b.Box.Bottom, Block: b) : (Start: b.Box.Left, End: b.Box.Right, Block: b))
            .OrderBy(s => s.Start).ToList();
        var groups = new List<List<FramedBlock>>();
        var current = new List<FramedBlock>();
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

    // A block with its box in the reading frame.
    private sealed record FramedBlock(PdfTextBlock Block, PdfRectangle Box);
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

    // A block is in a margin when it lies in the top or bottom 12 % of the page, both measured in the frame
    // of the block's own direction: a running head is horizontal, while a column of a table turned on the page lies
    // along an edge without being in a margin of its text.
    private static bool InMargin(PdfTextBlock block, PdfPage page)
    {
        var (box, crop) = Frame(block, page);
        var band = crop.Height * 0.12;
        return box.Bottom >= crop.Top - band || box.Top <= crop.Bottom + band;
    }

    // Same text with numbers masked, in the same direction, band and at about the same height.
    private static string Key(PdfTextBlock block, PdfPage page)
    {
        var (box, crop) = Frame(block, page);
        var text = Digits.Replace(block.Text.Trim(), "#");
        var top = box.Bottom >= crop.Top - (crop.Height * 0.12);
        var height = Math.Round((box.Bottom - crop.Bottom) / 10);
        return new StringBuilder(text).Append('|').Append(top ? 'T' : 'B').Append('|').Append(height).Append('|').Append(Direction(block).Angle).ToString();
    }

    // The block and the page in the frame of the block's direction.
    private static (PdfRectangle Block, PdfRectangle Page) Frame(PdfTextBlock block, PdfPage page)
    {
        var direction = Direction(block);
        return (direction.Project(block.BoundingBox), direction.Project(page.CropBox));
    }

    private static TextDirection Direction(PdfTextBlock block) => TextDirection.Of(block.Lines[0].Words[0].Letters[0].Rotation);

    private static bool IsPageNumber(PdfTextBlock block)
    {
        var text = block.Text.Trim();
        return text.Length is > 0 and < 16 && Regex.IsMatch(text, @"^(page\s*)?[-–]?\s*\d+(\s*(/|sur|of|de)\s*\d+)?\s*[-–]?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
