// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>Horizontal geometry and line spacing of one paragraph, relative to its column (0 is the column's left edge).</summary>
internal sealed record LineGeometry(
    double Width,
    double Left,
    double Right,
    double FirstLine,
    IReadOnlyList<WordTabStop> Tabs,
    double DefaultTab,
    WordAlignment Alignment,
    double? LineSpacing,
    WordLineSpacingRule Rule,
    TextStyle MarkStyle);

/// <summary>A token placed on a line.</summary>
internal readonly record struct Placed(Token Token, int Index, double X, double Width, WordTabLeader Leader = WordTabLeader.None);

/// <summary>A laid-out line: placed tokens, height and baseline.</summary>
internal sealed class Line
{
    public List<Placed> Items { get; init; } = [];

    public double Height { get; set; }

    /// <summary>Distance from the top of the line to its baseline.</summary>
    public double Baseline { get; set; }

    /// <summary>The page or column break that ends the line, if any.</summary>
    public WordBreakKind? BreakAfter { get; set; }

    public IEnumerable<(WordNoteKind Kind, int Id)> Notes =>
        Items.Select(i => i.Token).OfType<TextToken>().Where(t => t.Note is not null).Select(t => t.Note!.Value);
}

/// <summary>
/// Greedy line breaking, as Word does it: tokens fill a line until the next one overflows, then the line
/// breaks at the last opportunity (after a space or a hyphen). A word wider than the line is cut between
/// characters. Tabs go to the next custom stop (left, centre, right or decimal), the hanging indent, or the
/// default grid. Lines are then aligned or justified (extra space shared by the spaces after the last tab).
/// </summary>
internal sealed class LineBreaker(LayoutContext context, LineGeometry geometry)
{
    private const double Epsilon = 0.01;
    private List<Token> _tokens = [];

    public List<Line> Break(List<Token> tokens)
    {
        _tokens = tokens;
        var lines = new List<Line>();
        var index = 0;
        do
        {
            var state = new LineState(lines.Count == 0 ? geometry.Left + geometry.FirstLine : geometry.Left);
            index = Fill(index, state);
            lines.Add(Finish(state, last: index >= _tokens.Count));
        }
        while (index < _tokens.Count);

        if (lines[^1].BreakAfter == WordBreakKind.Line)
        {
            // A trailing line break leaves an empty line below it, as in Word.
            lines.Add(Finish(new LineState(geometry.Left), last: true));
        }

        return lines;
    }

    private int Fill(int start, LineState state)
    {
        for (var k = start; k < _tokens.Count; k++)
        {
            var token = _tokens[k];
            switch (token)
            {
                case BreakToken lineBreak:
                    state.Add(new Placed(token, k, state.X, 0));
                    state.BreakAfter = lineBreak.Kind;
                    return k + 1;
                case TabToken tab:
                    if (!PlaceTab(tab, k, state))
                    {
                        return k;
                    }

                    continue;
                case AnchorToken:
                    state.Add(new Placed(token, k, state.X, 0));
                    continue;
            }

            if (Overflows(token, state))
            {
                if (token.BreakBefore && state.HasContent)
                {
                    return k;
                }

                var resume = BreakPoint(state);
                if (resume >= 0)
                {
                    return resume;
                }

                if (state.HasContent)
                {
                    return k;
                }

                SplitWord(k, state);
            }

            state.Add(new Placed(_tokens[k], k, state.X, _tokens[k].Width));
            state.X += _tokens[k].Width;
        }

        return _tokens.Count;
    }

    private bool Overflows(Token token, LineState state) =>
        !(token is TextToken { IsSpace: true }) && state.X + token.Width > geometry.Right + Epsilon;

    // The last item a line may break before; the items from there move to the next line.
    private static int BreakPoint(LineState state)
    {
        for (var i = state.Items.Count - 1; i > 0; i--)
        {
            if (state.Items[i].Token.BreakBefore && state.Items.Take(i).Any(p => p.Token is not AnchorToken))
            {
                var resume = state.Items[i].Index;
                state.Truncate(i);
                return resume;
            }
        }

        return -1;
    }

    // A word wider than the whole line is cut after the last character that fits (at least one).
    private void SplitWord(int k, LineState state)
    {
        if (_tokens[k] is not TextToken { IsSpace: false } word || word.Text.Length < 2)
        {
            return;
        }

        var room = geometry.Right - state.X;
        var length = 1;
        while (length < word.Text.Length - 1 && context.Measure(word.Text[..(length + 1)], word.Style) <= room)
        {
            length++;
        }

        var rest = new TextToken(word.Text[length..], word.Style) { Note = word.Note, BreakBefore = true };
        rest.Width = context.Measure(rest.Text, rest.Style);
        word.Text = word.Text[..length];
        word.Width = context.Measure(word.Text, word.Style);
        _tokens.Insert(k + 1, rest);
    }

    // Returns false when the tab must start the next line.
    private bool PlaceTab(TabToken tab, int k, LineState state)
    {
        state.CloseSegment();
        var (stop, alignment, leader) = NextStop(tab, state.X);
        if (stop > geometry.Right + Epsilon && tab.Positional is null && state.HasContent && stop > geometry.Width)
        {
            return false;
        }

        if (alignment is WordTabAlignment.Left or WordTabAlignment.Bar)
        {
            var width = Math.Max(0, stop - state.X);
            state.Add(new Placed(tab, k, state.X, width, leader));
            state.X += width;
            return true;
        }

        state.OpenSegment(state.Items.Count, stop, alignment);
        state.Add(new Placed(tab, k, state.X, 0, leader));
        return true;
    }

    private (double Stop, WordTabAlignment Alignment, WordTabLeader Leader) NextStop(TabToken tab, double x)
    {
        if (tab.Positional is { Alignment: { } positional })
        {
            return (PositionalStop(positional), positional, tab.Positional.Leader);
        }

        var custom = geometry.Tabs.FirstOrDefault(t => t.Position > x + Epsilon);
        var hanging = geometry.FirstLine < 0 && x < geometry.Left - Epsilon ? geometry.Left : (double?)null;
        if (hanging is { } indent && (custom is null || indent < custom.Position))
        {
            return (indent, WordTabAlignment.Left, WordTabLeader.None);
        }

        if (custom is not null)
        {
            return (custom.Position, custom.Alignment, custom.Leader);
        }

        var step = geometry.DefaultTab > 1 ? geometry.DefaultTab : 36;
        return ((Math.Floor((x + Epsilon) / step) + 1) * step, WordTabAlignment.Left, WordTabLeader.None);
    }

    // A positional tab aligns on the margins: left edge, centre or right edge of the column.
    private double PositionalStop(WordTabAlignment alignment) => alignment switch
    {
        WordTabAlignment.Center => geometry.Width / 2,
        WordTabAlignment.Right => geometry.Width,
        _ => 0,
    };

    private Line Finish(LineState state, bool last)
    {
        state.CloseSegment();
        var line = new Line { BreakAfter = state.BreakAfter };
        var content = state.Items.Where(i => i.Token is not AnchorToken and not BreakToken).ToList();
        var ascent = content.Count == 0 ? context.Metrics(geometry.MarkStyle).Ascent : content.Max(i => i.Token.Ascent(context));
        var descent = content.Count == 0 ? context.Metrics(geometry.MarkStyle).Descent : content.Max(i => i.Token.Descent(context));
        var natural = ascent + descent;
        line.Height = geometry.Rule switch
        {
            WordLineSpacingRule.Exact when geometry.LineSpacing is { } exact => exact,
            WordLineSpacingRule.AtLeast when geometry.LineSpacing is { } least => Math.Max(natural, least),
            _ => natural * (geometry.LineSpacing ?? 1),
        };
        line.Baseline = line.Height - descent;
        line.Items.AddRange(Align(state, last || state.BreakAfter is not null));
        if (state.BreakAfter is not null && line.Items.Count == 1 && last)
        {
            // A paragraph holding only a break still takes its line.
            line.Items.Clear();
        }
        return line;
    }

    private IEnumerable<Placed> Align(LineState state, bool lastLine)
    {
        var items = state.Items;
        var end = items.LastOrDefault(i => !(i.Token is TextToken { IsSpace: true }) && i.Token is not BreakToken and not AnchorToken);
        if (items.Count == 0 || end.Token is null)
        {
            return items;
        }

        var free = geometry.Right - (end.X + end.Width);
        var hasTabs = items.Any(i => i.Token is TabToken);
        var alignment = geometry.Alignment;
        if (free <= Epsilon || (hasTabs && alignment is WordAlignment.Center or WordAlignment.Right))
        {
            return items;
        }

        return alignment switch
        {
            WordAlignment.Center => items.Select(i => i with { X = i.X + (free / 2) }),
            WordAlignment.Right => items.Select(i => i with { X = i.X + free }),
            WordAlignment.Justify when !lastLine => Justify(items, end, free),
            WordAlignment.Distribute => Justify(items, end, free),
            _ => items,
        };
    }

    // Shares the free space among the spaces after the last tab, up to the last visible item.
    private static List<Placed> Justify(List<Placed> items, Placed end, double free)
    {
        var lastTab = items.FindLastIndex(i => i.Token is TabToken);
        var endIndex = items.IndexOf(end);
        var spaces = 0;
        for (var i = lastTab + 1; i < endIndex; i++)
        {
            spaces += items[i].Token is TextToken { IsSpace: true } ? 1 : 0;
        }

        if (spaces == 0)
        {
            return items;
        }

        var extra = free / spaces;
        var shift = 0.0;
        var result = new List<Placed>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i] with { X = items[i].X + shift };
            if (i > lastTab && i < endIndex && item.Token is TextToken { IsSpace: true })
            {
                item = item with { Width = item.Width + extra };
                shift += extra;
            }

            result.Add(item);
        }

        return result;
    }

    /// <summary>The line being filled.</summary>
    private sealed class LineState(double start)
    {
        private (int TabItem, double Stop, WordTabAlignment Alignment)? _segment;

        public double X { get; set; } = start;

        public List<Placed> Items { get; } = [];

        public WordBreakKind? BreakAfter { get; set; }

        public bool HasContent => Items.Any(i => i.Token is not AnchorToken);

        public void Add(Placed placed) => Items.Add(placed);

        public void Truncate(int count)
        {
            Items.RemoveRange(count, Items.Count - count);
            X = Items.Count == 0 ? X : Items[^1].X + Items[^1].Width;
            if (_segment is { } segment && segment.TabItem >= count)
            {
                _segment = null;
            }
        }

        public void OpenSegment(int tabItem, double stop, WordTabAlignment alignment) => _segment = (tabItem, stop, alignment);

        // Moves the text after a right, centre or decimal tab so it ends, centres or aligns its decimal point on the stop.
        public void CloseSegment()
        {
            if (_segment is not { } segment)
            {
                return;
            }

            _segment = null;
            var tab = Items[segment.TabItem];
            var width = X - tab.X;
            var anchor = segment.Alignment switch
            {
                WordTabAlignment.Center => width / 2,
                WordTabAlignment.Decimal => DecimalOffset(segment.TabItem, tab.X) ?? width,
                _ => width,
            };
            var shift = Math.Max(0, segment.Stop - anchor - tab.X);
            Items[segment.TabItem] = tab with { Width = shift };
            for (var i = segment.TabItem + 1; i < Items.Count; i++)
            {
                Items[i] = Items[i] with { X = Items[i].X + shift };
            }

            X += shift;
        }

        private double? DecimalOffset(int tabItem, double origin)
        {
            for (var i = tabItem + 1; i < Items.Count; i++)
            {
                if (Items[i].Token is TextToken text && text.Text.IndexOfAny(['.', ',']) is var at and >= 0)
                {
                    return Items[i].X - origin + (Items[i].Width * at / Math.Max(1, text.Text.Length));
                }
            }

            return null;
        }
    }
}
