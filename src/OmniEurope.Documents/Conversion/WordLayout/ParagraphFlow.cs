// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Text;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>
/// The lines of one paragraph: its tokens broken at a column width, with its spacing, keep rules and decorations, and
/// broken again from any line when the paragraph lands in a column of another width or next to floating shapes. A
/// right-to-left paragraph (<c>w:bidi</c>) is laid out from the right edge: its indents, tabs and alignment are taken
/// from its start edge, its lines put in drawing order by the bidirectional algorithm, as is the mixed text of a
/// left-to-right paragraph.
/// </summary>
internal sealed class ParagraphFlow
{
    private readonly LayoutContext _context;
    private readonly WordParagraph _paragraph;
    private readonly WordParagraphProperties _p;
    private readonly List<Token> _tokens;
    private readonly TextStyle _markStyle;
    private readonly bool _rightToLeft;
    private readonly bool _bidi;

    public ParagraphFlow(LayoutContext context, WordParagraph paragraph, WordParagraphProperties properties, List<Token> tokens, TextStyle markStyle)
    {
        _context = context;
        _paragraph = paragraph;
        _p = properties;
        _tokens = tokens;
        _markStyle = markStyle;
        _rightToLeft = properties.RightToLeft == true;
        _bidi = AssignLevels();
        Anchors = tokens.OfType<AnchorToken>().ToList();
    }

    /// <summary>The floating shapes anchored in the paragraph.</summary>
    public IReadOnlyList<AnchorToken> Anchors { get; }

    public bool RightToLeft => _rightToLeft;

    /// <summary>All the lines of the paragraph at <paramref name="width"/>.</summary>
    public List<LineItem> Lay(double width, ILineArea? area = null)
    {
        var lines = Break(width, 0, area);
        var items = Items(lines, 0, width, area is not null);
        var first = items[0];
        first.SpaceBefore = _p.SpacingBefore ?? 0;
        first.Source = _paragraph.SourceAddress;
        if (items.Count > 1 && items[1].PageBreakBefore && LineBreaker.OpensWithBreak(lines[0]))
        {
            // A paragraph that opens with a page break starts on the next page, its space before kept.
            items[1].SpaceBefore = first.SpaceBefore;
        }

        first.PageBreakBefore |= _p.PageBreakBefore == true;
        items[^1].SpaceAfter = _p.SpacingAfter ?? 0;
        if (_p.OutlineLevel is >= 0 and < 9 && _paragraph.Text.Trim() is { Length: > 0 } title)
        {
            first.Bookmark = title.Replace('\n', ' ').Replace('\t', ' ');
        }

        return items;
    }

    /// <summary>
    /// The lines from <paramref name="from"/> on, broken again at <paramref name="width"/> in <paramref name="area"/>;
    /// they keep what the lines they replace had at the paragraph's ends (spacing, keep rules, breaks, outline entry).
    /// </summary>
    public List<LineItem> Relay(LineItem from, LineItem last, double width, ILineArea? area)
    {
        var lines = Break(width, from.Line.StartToken, area);
        var items = Items(lines, from.LineIndex, width, area is not null);
        var first = items[0];
        (first.SpaceBefore, first.FullSpaceBefore, first.CanBreakBefore) = (from.SpaceBefore, from.FullSpaceBefore, from.CanBreakBefore);
        (first.PageBreakBefore, first.ColumnBreakBefore, first.Bookmark, first.Source) = (from.PageBreakBefore, from.ColumnBreakBefore, from.Bookmark, from.Source);
        items[^1].SpaceAfter = last.SpaceAfter;
        return items;
    }

    private List<Line> Break(double width, int start, ILineArea? area)
    {
        var left = _p.IndentLeft ?? 0;
        var right = Math.Max(left + 1, width - (_p.IndentRight ?? 0));
        var geometry = new LineGeometry(
            width, left, right, _p.FirstLineIndent ?? 0, _p.Tabs ?? [], _context.Document.Settings.DefaultTabStop, _p.Alignment ?? WordAlignment.Left,
            _p.LineSpacing, _p.LineSpacingRule ?? WordLineSpacingRule.Multiple, _markStyle);
        var lines = new LineBreaker(_context, geometry).Break(_tokens, start, start == 0, area);
        if (_bidi)
        {
            foreach (var line in lines)
            {
                BidiLine.Arrange(_context, line, width, _rightToLeft, (byte)(_rightToLeft ? 1 : 0));
            }
        }

        return lines;
    }

    private List<LineItem> Items(List<Line> lines, int firstIndex, double width, bool wrapped)
    {
        var left = _p.IndentLeft ?? 0;
        var right = Math.Max(left + 1, width - (_p.IndentRight ?? 0));
        var (frameLeft, frameRight) = (Math.Min(left, left + (_p.FirstLineIndent ?? 0)), right);
        var frame = _rightToLeft
            ? new ParagraphFrame(width - frameRight, width - frameLeft, TextStyle.ParseColor(_p.Shading), _p.Borders)
            : new ParagraphFrame(frameLeft, frameRight, TextStyle.ParseColor(_p.Shading), _p.Borders);
        var items = new List<LineItem>(lines.Count);
        var count = firstIndex + lines.Count;
        for (var i = 0; i < lines.Count; i++)
        {
            var index = firstIndex + i;
            var item = new LineItem(lines[i], frame, index == 0, i == lines.Count - 1)
            {
                Height = lines[i].Height,
                Flow = this,
                LineIndex = index,
                LaidWidth = width,
                Wrapped = wrapped,
            };
            item.CanBreakBefore = index == 0 || CanBreakBefore(index, count);
            item.PageBreakBefore = i > 0 && lines[i - 1].BreakAfter == WordBreakKind.Page;
            item.ColumnBreakBefore = i > 0 && lines[i - 1].BreakAfter == WordBreakKind.Column;
            items.Add(item);
        }

        return items;
    }

    // Under widow control (Word's default) neither the first nor the last line stands alone on a page.
    private bool CanBreakBefore(int line, int count)
    {
        if (_p.KeepLines == true)
        {
            return false;
        }

        return _p.WidowControl == false || (line != 1 && line != count - 1);
    }

    // The embedding level of each character, when the paragraph is right to left or holds right-to-left text.
    private bool AssignLevels()
    {
        var text = new StringBuilder();
        var rightToLeftRun = new List<bool>();
        foreach (var token in _tokens)
        {
            var value = token switch
            {
                TextToken t => t.Text,
                TabToken => "\t",
                BreakToken => "\u2028",
                _ => "\uFFFC",
            };
            text.Append(value);
            rightToLeftRun.AddRange(Enumerable.Repeat(token.Style.RightToLeft, value.Length));
        }

        var paragraph = text.ToString();
        if (!_rightToLeft && !rightToLeftRun.Contains(true) && !BidiAlgorithm.NeedsReordering(paragraph))
        {
            return false;
        }

        var levels = BidiAlgorithm.Levels(paragraph, (byte)(_rightToLeft ? 1 : 0), i => rightToLeftRun[i]);
        var offset = 0;
        foreach (var token in _tokens)
        {
            var length = token is TextToken t ? t.Text.Length : 1;
            token.Levels = levels[offset..(offset + length)];
            offset += length;
        }

        return true;
    }
}
