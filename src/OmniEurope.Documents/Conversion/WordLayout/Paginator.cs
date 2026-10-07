// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>
/// Fills pages with the flow items of each section, column by column. An item that does not fit moves to
/// the next column, after stepping back over the items that may not end a column (keep rules); a row that
/// does not fit splits between lines. Space above an item is dropped at the top of a column reached by a
/// natural break. Footnotes referenced on a page are placed at its bottom and shrink its columns. Header
/// rows of a table continuing on a new column are repeated. Before a continuous section break, the columns
/// of a multi-column section are balanced.
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

    public List<PageFrame> Run()
    {
        var sections = context.Document.Sections;
        for (var s = 0; s < sections.Count; s++)
        {
            _section = sections[s];
            context.Notes.Section = s;
            var width = ColumnWidths(_section.Page)[0].Width;
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
                _region = new Region(top, Columns(_page, page), _page.Placements.Count);
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
        var item = _queue[i];
        if (item.PageBreakBefore && _page.Placements.Count > 0)
        {
            NewPage(firstOfSection: false, natural: false);
        }
        else if (item.ColumnBreakBefore && !AtColumnTop())
        {
            NextColumn(i, natural: false);
        }

        if (RepeatHeaderRows(i))
        {
            return i;
        }

        var before = AtColumnTop() && _natural ? 0 : item.SpaceBefore;
        var bottom = Bottom(item);
        if (_y + before + item.Height <= bottom + Epsilon)
        {
            Put(item, i, before);
            return i + 1;
        }

        if (item.Split(bottom - _y - before) is { } parts)
        {
            _queue[i] = parts.First;
            _queue.Insert(i + 1, parts.Remainder);
            Put(parts.First, i, before);
            NextColumn(i + 1, natural: true);
            return i + 1;
        }

        if (AtColumnTop())
        {
            context.Gaps.Add("content taller than a page runs past its bottom");
            Put(item, i, before);
            return i + 1;
        }

        return Backtrack(i);
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
        RefreshNotes();
        NextColumn(k, natural: true);
        return k;
    }

    private bool AtColumnTop() => !_page.Placements.Skip(_region.FirstPlacement).Any(p => p.Column == _column);

    private double Bottom(FlowItem item)
    {
        var notes = NewNotes(item).ToList();
        var extra = notes.Sum(n => n.Height) + (notes.Count > 0 && _page.Footnotes.Count == 0 ? Separator() : 0);
        return _page.BodyBottom - _page.NotesHeight - extra;
    }

    private void Put(FlowItem item, int index, double before)
    {
        var (left, width) = _region.Columns[_column];
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
        _region = new Region(_page.BodyTop, Columns(_page, setup), 0);
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

    private static List<(double Left, double Width)> Columns(PageFrame page, WordPageSetup setup)
    {
        var widths = ColumnWidths(setup);
        return widths.Select(c => (page.BodyLeft + c.Left, c.Width)).ToList();
    }

    /// <summary>Columns relative to the left margin: explicit widths, else equal shares.</summary>
    public static List<(double Left, double Width)> ColumnWidths(WordPageSetup setup)
    {
        var result = new List<(double, double)>();
        var x = 0.0;
        if (setup.ColumnWidths is { Count: > 0 } widths)
        {
            foreach (var width in widths)
            {
                result.Add((x, width));
                x += width + setup.ColumnSpacing;
            }

            return result;
        }

        var count = Math.Max(1, setup.Columns);
        var equal = (setup.ContentWidth - ((count - 1) * setup.ColumnSpacing)) / count;
        for (var i = 0; i < count; i++)
        {
            result.Add((i * (equal + setup.ColumnSpacing), equal));
        }

        return result;
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
}

/// <summary>Greedy fill of items into columns of a fixed height, for balancing.</summary>
internal static class ColumnBalancer
{
    /// <summary>Column and offset of each item, or null when they do not fit in <paramref name="columns"/>.</summary>
    public static List<(int Column, double Y)>? Fit(List<FlowItem> items, int columns, double height)
    {
        var slots = new List<(int, double)>(items.Count);
        var (column, y, start) = (0, 0.0, 0);
        for (var i = 0; i < items.Count; i++)
        {
            var before = y == 0 ? 0 : items[i].SpaceBefore;
            if (y + before + items[i].Height > height + 0.01 && y > 0)
            {
                var k = i;
                while (k > start && !items[k].CanBreakBefore)
                {
                    k--;
                }

                k = k > start ? k : i;
                slots.RemoveRange(k, slots.Count - k);
                (column, y, start, i) = (column + 1, 0, k, k - 1);
                if (column >= columns)
                {
                    return null;
                }

                continue;
            }

            slots.Add((column, y + before));
            y += before + items[i].Height + items[i].SpaceAfter;
        }

        return slots;
    }
}
