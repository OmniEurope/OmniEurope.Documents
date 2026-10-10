// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>Horizontal geometry and line spacing of one paragraph, relative to its column (0 is the column's start edge:
/// the left one, the right one in a right-to-left paragraph).</summary>
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

/// <summary>Where the lines of a paragraph may go next to floating shapes.</summary>
internal interface ILineArea
{
    /// <summary>
    /// The room of a line whose top lies <paramref name="top"/> below the paragraph's first line and whose height is
    /// <paramref name="height"/>, between <paramref name="left"/> and <paramref name="right"/> (its indents, from the
    /// start edge of the column).
    /// </summary>
    LineRoom Room(double top, double height, double left, double right);
}

/// <summary>How far a line moves down to find room, and the spans it fills in reading order (from the start edge).</summary>
internal sealed record LineRoom(double Drop, IReadOnlyList<(double Left, double Right)> Spans)
{
    public bool SameAs(LineRoom other) => Math.Abs(Drop - other.Drop) < 0.01 && Spans.SequenceEqual(other.Spans);
}

/// <summary>A token placed on a line, in the span (the stretch of line between floating shapes) it belongs to.</summary>
internal readonly record struct Placed(Token Token, int Index, double X, double Width, WordTabLeader Leader = WordTabLeader.None, int Span = 0);

/// <summary>A laid-out line: placed tokens, height and baseline.</summary>
internal sealed class Line
{
    public List<Placed> Items { get; init; } = [];

    public double Height { get; set; }

    /// <summary>Distance from the top of the line to its baseline.</summary>
    public double Baseline { get; set; }

    /// <summary>The page or column break that ends the line, if any.</summary>
    public WordBreakKind? BreakAfter { get; set; }

    /// <summary>The index of the line's first token in the paragraph's tokens.</summary>
    public int StartToken { get; set; }

    /// <summary>The room the line moved down to pass the floating shapes above it.</summary>
    public double Drop { get; set; }

    public IEnumerable<(WordNoteKind Kind, int Id)> Notes =>
        Items.Select(i => i.Token).OfType<TextToken>().Where(t => t.Note is not null).Select(t => t.Note!.Value);
}

/// <summary>
/// Greedy line breaking, as Word does it: tokens fill a line until the next one overflows, then the line
/// breaks at the last opportunity (after a space or a hyphen). A word wider than the line is cut between
/// characters. Tabs go to the next custom stop (left, centre, right or decimal), the hanging indent, or the
/// default grid. Lines are then aligned or justified (extra space shared by the spaces after the last tab).
/// Next to floating shapes a line is filled span by span (the stretches left free) and moves down when no span is
/// left; each span is aligned on its own.
/// </summary>
internal sealed class LineBreaker(LayoutContext context, LineGeometry geometry)
{
    private const double Epsilon = 0.01;
    private List<Token> _tokens = [];

    /// <summary>Breaks the tokens from <paramref name="start"/> into lines; the first line takes the first-line indent
    /// when <paramref name="first"/> is set; <paramref name="area"/> gives the room next to floating shapes.</summary>
    public List<Line> Break(List<Token> tokens, int start = 0, bool first = true, ILineArea? area = null)
    {
        _tokens = tokens;
        var lines = new List<Line>();
        var index = start;
        var top = 0.0;
        do
        {
            // A page or column break opening the paragraph leaves its first line (and first-line indent) to what follows.
            var left = first && lines.TrueForAll(OpensWithBreak) ? geometry.Left + geometry.FirstLine : geometry.Left;
            var (line, next) = BreakLine(index, left, top, lines.Count > 0 ? lines[^1].Height : null, area);
            lines.Add(line);
            top += line.Drop + line.Height;
            index = next;
        }
        while (index < _tokens.Count);

        if (lines[^1].BreakAfter is not null)
        {
            // After a break the paragraph mark starts a line of its own, as in Word (a page or column break moves it on).
            lines.Add(BreakLine(_tokens.Count, geometry.Left, top, lines[^1].Height, area).Line);
        }

        return lines;
    }

    /// <summary>True for a line holding nothing but a page or column break.</summary>
    public static bool OpensWithBreak(Line line) =>
        line.BreakAfter is WordBreakKind.Page or WordBreakKind.Column
        && line.Items.TrueForAll(i => i.Token is BreakToken or AnchorToken || i.Token is TextToken { Text: var text } && string.IsNullOrWhiteSpace(text));

    // The room is asked for the height of the line before (or of an empty line), then again for the line's own height
    // when it came out taller and the room differs there.
    private (Line Line, int Next) BreakLine(int index, double left, double top, double? previous, ILineArea? area)
    {
        if (area is null)
        {
            return Attempt(index, new LineRoom(0, [(left, geometry.Right)]));
        }

        var estimate = previous ?? Finish(new LineState([(left, geometry.Right)]), last: true).Height;
        var room = area.Room(top, estimate, left, geometry.Right);
        var attempt = Attempt(index, room);
        if (attempt.Line.Height > estimate + Epsilon && area.Room(top, attempt.Line.Height, left, geometry.Right) is var taller && !taller.SameAs(room))
        {
            attempt = Attempt(index, taller);
        }

        return attempt;
    }

    private (Line Line, int Next) Attempt(int index, LineRoom room)
    {
        var state = new LineState(room.Spans);
        var next = index < _tokens.Count ? Fill(index, state) : index;
        var line = Finish(state, last: next >= _tokens.Count);
        line.StartToken = index;
        line.Drop = room.Drop;
        return (line, next);
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
                var (next, carryOn) = Overflow(k, token, state);
                if (!carryOn)
                {
                    return next;
                }

                if (next >= 0)
                {
                    k = next - 1;
                    continue;
                }
            }

            state.Add(new Placed(_tokens[k], k, state.X, _tokens[k].Width));
            state.X += _tokens[k].Width;
        }

        return _tokens.Count;
    }

    // Where filling goes on when token k overflows its span: the index to go on from and whether the line goes on (in
    // the next span); (-1, true) places the token here, cut when it is wider than the whole line.
    private (int Next, bool CarryOn) Overflow(int k, Token token, LineState state)
    {
        var resume = token.BreakBefore && state.SpanHasContent ? k : BreakPoint(state);
        if (resume < 0 && state.SpanHasContent)
        {
            resume = k;
        }

        if (resume >= 0)
        {
            return (resume, state.NextSpan());
        }

        if (state.NextSpan())
        {
            return (k, true);
        }

        if (state.HasContent)
        {
            return (k, false);
        }

        SplitWord(k, state);
        return (-1, true);
    }

    private static bool Overflows(Token token, LineState state) =>
        !(token is TextToken { IsSpace: true }) && state.X + token.Width > state.Right + Epsilon;

    // The last item of the span a line may break before; the items from there move on.
    private static int BreakPoint(LineState state)
    {
        for (var i = state.Items.Count - 1; i > state.SpanStart; i--)
        {
            if (state.Items[i].Token.BreakBefore && state.Items.Skip(state.SpanStart).Take(i - state.SpanStart).Any(p => p.Token is not AnchorToken))
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

        var room = state.Right - state.X;
        var length = 1;
        while (length < word.Text.Length - 1 && context.Measure(word.Text[..(length + 1)], word.Style) <= room)
        {
            length++;
        }

        var rest = new TextToken(word.Text[length..], word.Style) { Note = word.Note, BreakBefore = true, Levels = word.Levels?[length..] };
        rest.Width = context.Measure(rest.Text, rest.Style);
        word.Text = word.Text[..length];
        word.Levels = word.Levels?[..length];
        word.Width = context.Measure(word.Text, word.Style);
        _tokens.Insert(k + 1, rest);
    }

    // Returns false when the tab must start the next line.
    private bool PlaceTab(TabToken tab, int k, LineState state)
    {
        state.CloseTabRun();
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

        state.OpenTabRun(state.Items.Count, stop, alignment);
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
        state.CloseTabRun();
        var line = new Line { BreakAfter = state.BreakAfter };
        (line.Height, line.Baseline) = Measure(state.Items);
        var lastLine = last || state.BreakAfter is not null;
        for (var span = 0; span <= state.Span; span++)
        {
            var spanItems = state.Items.Where(i => i.Span == span).ToList();
            line.Items.AddRange(Align(spanItems, state.SpanRight(span), lastLine && span == state.Span));
        }

        if (state.BreakAfter is not null && line.Items.Count == 1 && last)
        {
            // A paragraph holding only a break still takes its line.
            line.Items.Clear();
        }

        return line;
    }

    // The height of a line and the distance from its top to its baseline, from its tallest content and its spacing.
    private (double Height, double Baseline) Measure(List<Placed> items)
    {
        var content = items.Where(i => i.Token is not AnchorToken and not BreakToken).ToList();
        if (content.Exists(i => i.Token is not TabToken))
        {
            // A tab does not make its line taller: Word measures a line by its text (a table of contents tab set in
            // a larger size than its entry leaves the entry's line height).
            content.RemoveAll(i => i.Token is TabToken);
        }

        var ascent = content.Count == 0 ? context.LineMetrics(geometry.MarkStyle).Ascent : content.Max(i => i.Token.Ascent(context));
        var descent = content.Count == 0 ? context.Metrics(geometry.MarkStyle).Descent : content.Max(i => i.Token.Descent(context));
        var natural = ascent + descent;
        var height = geometry.Rule switch
        {
            WordLineSpacingRule.Exact when geometry.LineSpacing is { } exact => exact,
            WordLineSpacingRule.AtLeast when geometry.LineSpacing is { } least => Math.Max(natural, least),
            _ => natural * (geometry.LineSpacing ?? 1),
        };
        var baseline = geometry.Rule switch
        {
            // An exact height smaller than the text cuts its descent first.
            WordLineSpacingRule.Exact when geometry.LineSpacing is not null => height - (descent * Math.Min(1, height / Math.Max(natural, 0.01))),
            WordLineSpacingRule.AtLeast when geometry.LineSpacing is not null => height - descent,
            // Word puts the extra space of a multiple below the text: the baseline stays one ascent down.
            _ => ascent,
        };
        return (height, baseline);
    }

    private IEnumerable<Placed> Align(List<Placed> items, double right, bool lastLine)
    {
        var end = items.LastOrDefault(i => !(i.Token is TextToken { IsSpace: true }) && i.Token is not BreakToken and not AnchorToken);
        if (items.Count == 0 || end.Token is null)
        {
            return items;
        }

        var free = right - (end.X + end.Width);
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

    /// <summary>The line being filled, span by span.</summary>
    private sealed class LineState(IReadOnlyList<(double Left, double Right)> spans)
    {
        private (int TabItem, double Stop, WordTabAlignment Alignment)? _tabRun;

        public double X { get; set; } = spans[0].Left;

        public List<Placed> Items { get; } = [];

        public WordBreakKind? BreakAfter { get; set; }

        /// <summary>The span being filled.</summary>
        public int Span { get; private set; }

        /// <summary>The index of the first item of the span being filled.</summary>
        public int SpanStart { get; private set; }

        public double Right => spans[Span].Right;

        public bool HasContent => Items.Any(i => i.Token is not AnchorToken);

        public bool SpanHasContent => Items.Skip(SpanStart).Any(i => i.Token is not AnchorToken);

        public double SpanRight(int span) => spans[span].Right;

        public void Add(Placed placed) => Items.Add(placed with { Span = Span });

        /// <summary>Moves to the next span; false when the line has no other span.</summary>
        public bool NextSpan()
        {
            if (Span + 1 >= spans.Count)
            {
                return false;
            }

            CloseTabRun();
            Span++;
            SpanStart = Items.Count;
            X = spans[Span].Left;
            return true;
        }

        public void Truncate(int count)
        {
            Items.RemoveRange(count, Items.Count - count);
            X = Items.Count == 0 ? X : Items[^1].X + Items[^1].Width;
            if (_tabRun is { } run && run.TabItem >= count)
            {
                _tabRun = null;
            }
        }

        public void OpenTabRun(int tabItem, double stop, WordTabAlignment alignment) => _tabRun = (tabItem, stop, alignment);

        // Moves the text after a right, centre or decimal tab so it ends, centres or aligns its decimal point on the stop.
        public void CloseTabRun()
        {
            if (_tabRun is not { } run)
            {
                return;
            }

            _tabRun = null;
            var tab = Items[run.TabItem];
            var width = X - tab.X;
            var anchor = run.Alignment switch
            {
                WordTabAlignment.Center => width / 2,
                WordTabAlignment.Decimal => DecimalOffset(run.TabItem, tab.X) ?? width,
                _ => width,
            };
            var shift = Math.Max(0, run.Stop - anchor - tab.X);
            Items[run.TabItem] = tab with { Width = shift };
            for (var i = run.TabItem + 1; i < Items.Count; i++)
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
