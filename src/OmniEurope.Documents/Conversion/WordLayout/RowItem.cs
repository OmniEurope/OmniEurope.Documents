// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>What the rows of one table share: its header rows, repeated at the top of each new page.</summary>
internal sealed class TableContext
{
    public List<RowItem> HeaderRows { get; } = [];

    /// <summary>Set once a row of the table has been placed, so later pages repeat the header rows.</summary>
    public bool Started { get; set; }
}

/// <summary>A cell of a laid-out row: position, margins, formatting and content items.</summary>
internal sealed class CellBox(int column, double x, double width, (double Top, double Left, double Bottom, double Right) margins, WordTableCellProperties properties)
{
    public int Column { get; } = column;

    public double X { get; } = x;

    public double Width { get; } = width;

    public (double Top, double Left, double Bottom, double Right) Margins { get; } = margins;

    public WordTableCellProperties Properties { get; } = properties;

    public WordTableBorders Borders { get; init; } = new();

    public List<FlowItem> Items { get; } = [];

    public bool MergedAbove { get; set; }

    public bool MergedBelow { get; set; }

    /// <summary>Height of the whole vertical merge when this cell starts one.</summary>
    public double? SpanHeight { get; set; }

    public double ContentHeight => Items.Sum(i => i.SpaceBefore + i.Height + i.SpaceAfter);

    public CellBox Copy(IEnumerable<FlowItem> items, bool keepTopMargin)
    {
        var copy = new CellBox(Column, X, Width, keepTopMargin ? Margins : Margins with { Top = 0 }, Properties) { Borders = Borders, MergedAbove = MergedAbove, MergedBelow = MergedBelow };
        copy.Items.AddRange(items);
        return copy;
    }
}

/// <summary>A table row; it can split between the lines of its cells unless it is marked cannot-split.</summary>
internal sealed class RowItem : FlowItem
{
    public List<CellBox> Cells { get; } = [];

    public TableContext Table { get; set; } = new();

    public bool IsHeader { get; set; }

    public bool CantSplit { get; set; }

    public double MinimumHeight { get; set; }

    public bool ExactHeight { get; set; }

    public override IEnumerable<(WordNoteKind Kind, int Id)> Notes => Cells.SelectMany(c => c.Items).SelectMany(i => i.Notes);

    public void Measure()
    {
        var content = Cells.Where(c => c.Properties.VerticalMerge != WordVerticalMerge.Restart || c.SpanHeight is null)
            .Select(c => c.ContentHeight + c.Margins.Top + c.Margins.Bottom)
            .DefaultIfEmpty(0)
            .Max();
        Height = ExactHeight && MinimumHeight > 0 ? MinimumHeight : Math.Max(content, MinimumHeight);
    }

    public override (FlowItem First, FlowItem Remainder)? Split(double available)
    {
        if (CantSplit || ExactHeight)
        {
            return null;
        }

        var first = new RowItem { Table = Table, MinimumHeight = 0 };
        var rest = new RowItem { Table = Table, MinimumHeight = 0 };
        var moved = false;
        foreach (var cell in Cells)
        {
            var (taken, left) = SplitCell(cell, available - cell.Margins.Top - cell.Margins.Bottom);
            moved |= taken.Count > 0;
            first.Cells.Add(cell.Copy(taken, keepTopMargin: true));
            rest.Cells.Add(cell.Copy(left, keepTopMargin: false));
        }

        if (!moved || rest.Cells.All(c => c.Items.Count == 0))
        {
            return null;
        }

        first.Measure();
        rest.Measure();
        return (first, rest);
    }

    // Takes the items that fit, splitting the first one that does not when it can.
    private static (List<FlowItem> Taken, List<FlowItem> Left) SplitCell(CellBox cell, double room)
    {
        var taken = new List<FlowItem>();
        var used = 0.0;
        for (var i = 0; i < cell.Items.Count; i++)
        {
            var item = cell.Items[i];
            var need = item.SpaceBefore + item.Height;
            if (used + need <= room + 0.01)
            {
                taken.Add(item);
                used += need + item.SpaceAfter;
                continue;
            }

            if (item.Split(room - used - item.SpaceBefore) is { } parts)
            {
                taken.Add(parts.First);
                return (taken, [parts.Remainder, .. cell.Items.Skip(i + 1)]);
            }

            // Keep rules: step back while the first item left may not start a page; break anyway if none may.
            var cut = i;
            while (cut > 1 && !cell.Items[cut].CanBreakBefore)
            {
                cut--;
            }

            cut = cell.Items[cut].CanBreakBefore ? cut : i;
            return (cell.Items.Take(cut).ToList(), cell.Items.Skip(cut).ToList());
        }

        return (taken, []);
    }

    public override void Paint(PaintContext context, double x, double y, double width)
    {
        // Fills first, then contents, then borders: content that runs over a neighbouring cell stays on top of
        // that cell's fill, and borders stay on top of everything.
        foreach (var cell in Cells)
        {
            if (TextStyle.ParseColor(cell.Properties.Shading) is { } shading)
            {
                context.Canvas.FillRectangle(x + cell.X, y, cell.Width, cell.MergedAbove ? Height : cell.SpanHeight ?? Height, shading);
            }
        }

        foreach (var cell in Cells)
        {
            PaintContent(context, cell, x + cell.X, y, cell.SpanHeight ?? Height);
        }

        foreach (var cell in Cells)
        {
            PaintBorders(context, cell, x + cell.X, y);
        }
    }

    private void PaintContent(PaintContext context, CellBox cell, double left, double y, double height)
    {
        var free = height - cell.Margins.Top - cell.Margins.Bottom - cell.ContentHeight;
        var top = y + cell.Margins.Top + cell.Properties.VerticalAlignment switch
        {
            WordCellAlignment.Center => Math.Max(0, free / 2),
            WordCellAlignment.Bottom => Math.Max(0, free),
            _ => 0,
        };
        var innerWidth = Math.Max(1, cell.Width - cell.Margins.Left - cell.Margins.Right);
        var (start, lineWidth) = Placement(cell, left, innerWidth);
        var overflow = cell.Properties.Overflow;
        var clipped = ExactHeight || overflow is not null;
        if (clipped)
        {
            var (before, after) = (overflow?.ExtendLeft ?? 0, overflow?.ExtendRight ?? 0);
            context.Canvas.SaveState();
            context.Canvas.ClipRectangle(left - before, y, cell.Width + before + after, ExactHeight ? Height : height);
        }

        foreach (var item in cell.Items)
        {
            top += item.SpaceBefore;
            item.Paint(context, start, top, lineWidth);
            top += item.Height + item.SpaceAfter;
        }

        if (clipped)
        {
            context.Canvas.RestoreState();
        }
    }

    // A line wider than the cell keeps its anchor: its start on the left edge, its end on the right edge, or its
    // middle on the centre; what passes the cell edges is clipped.
    private static (double Start, double Width) Placement(CellBox cell, double left, double innerWidth)
    {
        var innerLeft = left + cell.Margins.Left;
        if (cell.Properties.Overflow is not { } overflow || overflow.Width <= innerWidth)
        {
            return (innerLeft, innerWidth);
        }

        return (overflow.Alignment switch
        {
            WordAlignment.Right => innerLeft + innerWidth - overflow.Width,
            WordAlignment.Center => innerLeft + ((innerWidth - overflow.Width) / 2),
            _ => innerLeft,
        }, overflow.Width);
    }

    private void PaintBorders(PaintContext context, CellBox cell, double left, double y)
    {
        var borders = cell.Borders;
        var right = left + cell.Width;
        var bottom = y + Height;
        Edge(context, cell.MergedAbove ? null : borders.Top, left, y, right, y);
        Edge(context, cell.MergedBelow ? null : borders.Bottom, left, bottom, right, bottom);
        Edge(context, borders.Left, left, y, left, bottom);
        Edge(context, borders.Right, right, y, right, bottom);
    }

    private static void Edge(PaintContext context, WordBorder? border, double x1, double y1, double x2, double y2)
    {
        if (border is { IsNone: false })
        {
            BorderPainter.Line(context.Canvas, border, x1, y1, x2, y2);
        }
    }
}
