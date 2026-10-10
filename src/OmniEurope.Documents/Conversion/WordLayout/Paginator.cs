// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>
/// Fills pages with the flow items of each section, column by column. An item that does not fit moves to
/// the next column, after stepping back over the items that may not end a column (keep rules); a row that
/// does not fit splits between lines. Space above an item is dropped at the top of a column reached by a
/// natural break. Footnotes referenced on a page are placed at its bottom and shrink its columns. Header
/// rows of a table continuing on a new column are repeated. Floating shapes are placed with the first line of
/// their paragraph; the lines after them are broken again around them, as are the paragraphs and tables reaching
/// a column of another width. Before a continuous section break, the columns of a multi-column section are
/// balanced.
/// </summary>
internal sealed class Paginator(LayoutContext context, BlockLayout blocks)
{
    private const double Epsilon = 0.01;
    private readonly List<PageFrame> _pages = [];
    private readonly Dictionary<(WordHeaderFooter, double), List<FlowItem>> _headerCache = [];
    private readonly Dictionary<int, PlacedNote> _noteCache = [];
    private List<FlowItem> _queue = [];
    private PageFrame _page = null!;
    private Region _region = null!;
    private int _column;
    private double _y;
    private int _columnStart;
    private bool _natural;
    private WordSection _section = null!;
    private int _droppedOnce = -1;

    // A balancing trial fills the region down to a given height and fails instead of opening a page.
    private double? _limit;
    private bool _overflow;

    private bool Trial => _limit is not null;

    private (double Left, double Width) Column => _region.Columns[_column];

    public List<PageFrame> Run()
    {
        var sections = context.Document.Sections;
        for (var s = 0; s < sections.Count; s++)
        {
            _section = sections[s];
            context.Notes.Section = s;
            var width = SectionColumns.Of(_section.Page)[0].Width;
            var items = blocks.Layout(_section.Blocks, width);
            if (s == sections.Count - 1)
            {
                items.AddRange(Endnotes(width));
            }

            StartSection(s);
            Place(items);
        }

        return _pages;
    }

    private void StartSection(int index)
    {
        var page = _section.Page;
        var continuous = index > 0 && page.Start is WordSectionStart.Continuous or WordSectionStart.NextColumn
            && _page.Setup.Width.Equals(page.Width) && _page.Setup.Height.Equals(page.Height);
        if (continuous)
        {
            Balance(_region);
            var top = _region.Bottoms.Max();
            if (top < _page.BodyBottom - 12)
            {
                _region = NewRegion(top, page);
                _page.Regions.Add(_region);
                (_column, _y, _columnStart, _natural) = (0, top, 0, false);
                return;
            }
        }

        if (index > 0 && page.Start is WordSectionStart.EvenPage or WordSectionStart.OddPage
            && (_page.PageNumber + 1) % 2 == (page.Start == WordSectionStart.EvenPage ? 1 : 0))
        {
            NewPage(firstOfSection: false);
        }

        NewPage(firstOfSection: true);
    }

    private Region NewRegion(double top, WordPageSetup setup)
    {
        var columns = SectionColumns.Of(setup).Select(c => (_page.BodyLeft + c.Left, c.Width)).ToList();
        return new Region(top, columns, _page.Placements.Count) { Separator = setup.ColumnSeparator && columns.Count > 1 };
    }

    private void Place(List<FlowItem> items)
    {
        _queue = items;
        _columnStart = 0;
        var i = 0;
        while (i < _queue.Count)
        {
            i = PlaceOne(i);
        }
    }

    // Places the item at i (or moves on); returns the next index to place.
    private int PlaceOne(int i)
    {
        if (!Break(_queue[i], i))
        {
            return _queue.Count;
        }

        RelayTable(i);
        if (RepeatHeaderRows(i))
        {
            return i;
        }

        var start = _queue[i];
        var before = AtColumnTop() ? (_natural ? 0 : start.FullSpaceBefore ?? start.SpaceBefore) : start.SpaceBefore;
        var item = RelayLines(i, _y + before);
        var drop = item.Drop + (item is RowItem ? PageFloats.RowDrop(_page, Column, _y + before, item.Height) : 0);
        var bottom = Bottom(item);
        if (_y + before + drop + item.Height <= bottom + Epsilon || StaysAtTheEnd(item, i))
        {
            Put(item, i, before + drop);
            return i + 1;
        }

        if (item.Split(bottom - _y - before - drop) is { } parts)
        {
            _queue[i] = parts.First;
            _queue.Insert(i + 1, parts.Remainder);
            Put(parts.First, i, before + drop);
            NextColumn(i + 1, natural: true);
            return i + 1;
        }

        return AtColumnTop() ? TooTall(item, i, before, drop) : Backtrack(i);
    }

    // A line holding nothing but a page or column break, and the empty paragraph mark that ends a section before a new
    // page, stay at the end of the column they end even when they pass its bottom, as in Word.
    private bool StaysAtTheEnd(FlowItem item, int i)
    {
        if (item is not LineItem { Line: var line })
        {
            return false;
        }

        if (LineBreaker.OpensWithBreak(line))
        {
            return true;
        }

        var sections = context.Document.Sections;
        var index = sections.IndexOf(_section);
        return i == _queue.Count - 1 && index + 1 < sections.Count
            && sections[index + 1].Page.Start is not (WordSectionStart.Continuous or WordSectionStart.NextColumn)
            && line.Items.TrueForAll(p => p.Token is AnchorToken || p.Token is TextToken { IsSpace: true });
    }

    // Page and column breaks before the item; false when a balancing trial cannot take them.
    private bool Break(FlowItem item, int i)
    {
        if (item.PageBreakBefore && _page.Placements.Count > 0)
        {
            if (Trial)
            {
                _overflow = true;
                return false;
            }

            NewPage(firstOfSection: false, natural: false);
        }
        else if (item.ColumnBreakBefore && !AtColumnTop())
        {
            NextColumn(i, natural: false);
        }

        return !_overflow;
    }

    // An item that does not fit at the top of a column: pushed down by floating shapes, it tries the next column once;
    // else it runs past the bottom.
    private int TooTall(FlowItem item, int i, double before, double drop)
    {
        if (Trial)
        {
            _overflow = true;
            return _queue.Count;
        }

        if (drop > Epsilon && _droppedOnce != i)
        {
            _droppedOnce = i;
            _page.Floats.RemoveAll(f => f.QueueIndex >= i);
            NextColumn(i, natural: true);
            return i;
        }

        context.Gaps.Add("content taller than a page runs past its bottom");
        Put(item, i, before + drop);
        return i + 1;
    }

    // A line placed where it was not broken for (another width, or next to floating shapes) is broken again with the
    // rest of its paragraph; the first line of a paragraph places the shapes anchored in it first.
    private FlowItem RelayLines(int i, double top)
    {
        if (_queue[i] is not LineItem { Flow: { } flow } line)
        {
            return _queue[i];
        }

        if (line.LineIndex == 0 && flow.Anchors.Count > 0)
        {
            PageFloats.Register(_page, _queue, i, top, Column);
        }

        var shapes = PageFloats.Wrapping(_page, Column, top);
        if (!NeedsRelay(line, top, shapes.Count > 0))
        {
            return line;
        }

        var end = i;
        while (end + 1 < _queue.Count && _queue[end + 1] is LineItem next && next.Flow == flow)
        {
            end++;
        }

        var area = shapes.Count > 0 ? new FloatArea(shapes, Column.Left, Column.Width, top, flow.RightToLeft) : null;
        var lines = flow.Relay(line, (LineItem)_queue[end], Column.Width, area);
        var y = top;
        foreach (var laid in lines)
        {
            laid.LaidAt = (_pages.Count, _column, y);
            y += laid.Drop + laid.Height;
        }

        _queue.RemoveRange(i, end - i + 1);
        _queue.InsertRange(i, lines);
        return _queue[i];
    }

    // A line is broken again unless it lands at the width it was broken at and either where it was broken for or, laid
    // out free of floating shapes, where no shape is in its way.
    private bool NeedsRelay(LineItem line, double top, bool shapes)
    {
        if (Math.Abs(line.LaidWidth - Column.Width) >= Epsilon)
        {
            return true;
        }

        var placedAsLaid = line.LaidAt is { } at && at.Page == _pages.Count && at.Column == _column && Math.Abs(at.Top - top) < Epsilon;
        return !placedAsLaid && (line.Wrapped || shapes);
    }

    // A table reaching a column of another width is laid out again at that width, from the row reached on.
    private void RelayTable(int i)
    {
        if (_queue[i] is not RowItem { Index: >= 0 } row || row.Table.Relayout is not { } relayout || Math.Abs(row.Table.Width - Column.Width) < Epsilon
            || (row.IsHeader && row.Table.Started))
        {
            return;
        }

        var end = i;
        while (end + 1 < _queue.Count && _queue[end + 1] is RowItem next && next.Table == row.Table)
        {
            end++;
        }

        var rows = relayout(Column.Width);
        rows[0].Table.Started = row.Table.Started;
        _queue.RemoveRange(i, end - i + 1);
        _queue.InsertRange(i, rows.Where(r => r.Index >= row.Index));
    }

    // Moves the items that may not end the column (keep rules) to the next column along with item i.
    private int Backtrack(int i)
    {
        var k = i;
        while (k > _columnStart && !_queue[k].CanBreakBefore)
        {
            k--;
        }

        if (k <= _columnStart || !_queue[k].CanBreakBefore)
        {
            k = i;
        }

        _page.Placements.RemoveAll(p => p.QueueIndex >= k && p.QueueIndex < i && _page.Placements.IndexOf(p) >= _region.FirstPlacement);
        _page.Floats.RemoveAll(f => f.QueueIndex >= k);
        RefreshNotes();
        NextColumn(k, natural: true);
        return _overflow ? _queue.Count : k;
    }

    private bool AtColumnTop() => !_page.Placements.Skip(_region.FirstPlacement).Any(p => p.Column == _column);

    private double Bottom(FlowItem item)
    {
        var notes = NewNotes(item).ToList();
        var extra = notes.Sum(n => n.Height) + (notes.Count > 0 && _page.Footnotes.Count == 0 ? Separator() : 0);
        return Math.Min(_page.BodyBottom - _page.NotesHeight - extra, _limit ?? double.PositiveInfinity);
    }

    private void Put(FlowItem item, int index, double before)
    {
        var (left, width) = Column;
        _y += before;
        _page.Placements.Add(new Placement(item, left, _y, width, index, _column));
        _y += item.Height + item.SpaceAfter;
        _region.Bottoms[_column] = Math.Max(_region.Bottoms[_column], Math.Min(_y, _page.BodyBottom));
        foreach (var note in NewNotes(item).ToList())
        {
            _page.SeparatorHeight = Separator();
            _page.Footnotes.Add(note);
        }

        if (item is RowItem row)
        {
            row.Table.Started = true;
        }
    }

    // At the top of a column a table that already started repeats its header rows.
    private bool RepeatHeaderRows(int i)
    {
        if (_queue[i] is not RowItem { IsHeader: false } row || !row.Table.Started || row.Table.HeaderRows.Count == 0 || !AtColumnTop()
            || (i > 0 && _queue[i - 1] is RowItem { IsHeader: true } previous && previous.Table == row.Table))
        {
            return false;
        }

        _queue.InsertRange(i, row.Table.HeaderRows);
        return true;
    }

    private void NextColumn(int queueIndex, bool natural)
    {
        if (_column < _region.Columns.Count - 1)
        {
            _column++;
            _y = _region.Top;
            _columnStart = queueIndex;
            _natural = natural;
            return;
        }

        if (Trial)
        {
            _overflow = true;
            return;
        }

        NewPage(firstOfSection: false, natural);
        _columnStart = queueIndex;
    }

    private void NewPage(bool firstOfSection, bool natural = false)
    {
        var setup = _section.Page;
        var number = firstOfSection && setup.PageNumberStart is { } start ? start : (_pages.Count == 0 ? 1 : _pages[^1].PageNumber + 1);
        var width = setup.ContentWidth;
        _page = new PageFrame
        {
            Section = _section,
            Setup = setup,
            PageNumber = number,
            Header = HeaderFooter(_section.Headers, setup, firstOfSection, number, width),
            Footer = HeaderFooter(_section.Footers, setup, firstOfSection, number, width),
        };
        _pages.Add(_page);
        _region = NewRegion(_page.BodyTop, setup);
        _page.Regions.Add(_region);
        (_column, _y, _natural) = (0, _region.Top, natural);
    }

    private List<FlowItem> HeaderFooter(Dictionary<WordHeaderFooterKind, WordHeaderFooter> parts, WordPageSetup setup, bool firstOfSection, int number, double width)
    {
        var kind = setup.TitlePage && firstOfSection ? WordHeaderFooterKind.First
            : context.Document.Settings.EvenAndOddHeaders && number % 2 == 0 ? WordHeaderFooterKind.Even
            : WordHeaderFooterKind.Default;
        if (!parts.TryGetValue(kind, out var content))
        {
            return [];
        }

        if (!_headerCache.TryGetValue((content, width), out var items))
        {
            items = blocks.Layout(content.Blocks, width, numbering: false);
            _headerCache[(content, width)] = items;
        }

        return items;
    }

    private IEnumerable<PlacedNote> NewNotes(FlowItem item)
    {
        foreach (var (kind, id) in item.Notes.Distinct())
        {
            if (kind == WordNoteKind.Footnote && !_page.Footnotes.Exists(n => n.Id == id) && Note(id) is { } note)
            {
                yield return note;
            }
        }
    }

    private PlacedNote? Note(int id)
    {
        if (_noteCache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        if (!context.Document.Footnotes.TryGetValue(id, out var source))
        {
            return null;
        }

        var items = blocks.Layout(source.Blocks, _page.BodyWidth, numbering: false);
        var note = new PlacedNote(id, items, items.Sum(i => i.SpaceBefore + i.Height + i.SpaceAfter));
        _noteCache[id] = note;
        return note;
    }

    private void RefreshNotes()
    {
        var present = _page.Placements.SelectMany(p => p.Item.Notes).Where(n => n.Kind == WordNoteKind.Footnote).Select(n => n.Id).ToHashSet();
        _page.Footnotes.RemoveAll(n => !present.Contains(n.Id));
    }

    private double Separator() => context.Metrics(DefaultStyle()).LineHeight;

    private TextStyle DefaultStyle() => TextStyle.From(context.Styles.ResolveRun(WordParagraphProperties.Empty, WordRunProperties.Empty));

    private List<FlowItem> Endnotes(double width)
    {
        var notes = context.Notes.EndnoteOrder.Where(context.Document.Endnotes.ContainsKey).ToList();
        if (notes.Count == 0)
        {
            return [];
        }

        var items = new List<FlowItem> { RuleItem.Separator(Separator()) };
        foreach (var id in notes)
        {
            items.AddRange(blocks.Layout(context.Document.Endnotes[id].Blocks, width, numbering: false));
        }

        return items;
    }

    // Shortest column height that holds the region's items in its columns, found by bisection.
    private void Balance(Region region)
    {
        var placed = _page.Placements.Skip(region.FirstPlacement).ToList();
        if (region.Columns.Count < 2 || placed.Count < 2 || _page.Footnotes.Count > 0)
        {
            return;
        }

        if (SectionColumns.Unequal(region.Columns) || _page.Floats.Count > 0)
        {
            BalanceByPlacing(region, placed);
            return;
        }

        var items = placed.Select(p => p.Item).ToList();
        var (low, high) = (items.Max(i => i.Height), region.Bottoms.Max() - region.Top);
        if (ColumnBalancer.Fit(items, region.Columns.Count, high) is null)
        {
            return;
        }

        for (var step = 0; step < 30 && high - low > 0.5; step++)
        {
            var middle = (low + high) / 2;
            (low, high) = ColumnBalancer.Fit(items, region.Columns.Count, middle) is null ? (middle, high) : (low, middle);
        }

        var slots = ColumnBalancer.Fit(items, region.Columns.Count, high)!;
        _page.Placements.RemoveRange(region.FirstPlacement, placed.Count);
        Array.Fill(region.Bottoms, region.Top);
        for (var k = 0; k < placed.Count; k++)
        {
            var (column, y) = slots[k];
            var (left, width) = region.Columns[column];
            _page.Placements.Add(placed[k] with { X = left, Y = region.Top + y, Width = width, Column = column });
            region.Bottoms[column] = Math.Max(region.Bottoms[column], region.Top + y + items[k].Height + items[k].SpaceAfter);
        }
    }

    // Columns of other widths (or around floating shapes) hold other numbers of lines: the region's items are placed
    // again, the paragraphs broken at each column's width, down to the lowest height that holds them.
    private void BalanceByPlacing(Region region, List<Placement> placed)
    {
        var first = placed.Min(p => p.QueueIndex);
        var (low, high) = (placed.Max(p => p.Item.Height), region.Bottoms.Max() - region.Top);
        if (!TryPlace(region, first, high, keep: false))
        {
            return;
        }

        for (var step = 0; step < 20 && high - low > 0.5; step++)
        {
            var middle = (low + high) / 2;
            (low, high) = TryPlace(region, first, middle, keep: false) ? (low, middle) : (middle, high);
        }

        TryPlace(region, first, high, keep: true);
    }

    // Places the items from queue index first in the region, no column taller than height; restores the region as it
    // was unless the items fit and keep is set.
    private bool TryPlace(Region region, int first, double height, bool keep)
    {
        var (queue, placements, floats, bottoms) = (_queue, _page.Placements.ToList(), _page.Floats.ToList(), region.Bottoms.ToArray());
        var state = (_column, _y, _columnStart, _natural);
        _queue = [.. queue];
        _page.Placements.RemoveRange(region.FirstPlacement, _page.Placements.Count - region.FirstPlacement);
        _page.Floats.RemoveAll(f => f.QueueIndex >= first);
        Array.Fill(region.Bottoms, region.Top);
        (_column, _y, _columnStart, _natural, _overflow, _limit) = (0, region.Top, first, false, false, region.Top + height);
        var i = first;
        while (i < _queue.Count && !_overflow)
        {
            i = PlaceOne(i);
        }

        var fits = !_overflow;
        (_limit, _overflow) = (null, false);
        if (fits && keep)
        {
            return true;
        }

        _queue = queue;
        _page.Placements.Clear();
        _page.Placements.AddRange(placements);
        _page.Floats.Clear();
        _page.Floats.AddRange(floats);
        Array.Copy(bottoms, region.Bottoms, bottoms.Length);
        (_column, _y, _columnStart, _natural) = state;
        return fits;
    }
}
