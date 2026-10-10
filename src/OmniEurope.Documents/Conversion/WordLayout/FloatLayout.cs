// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>A floating shape placed on a page (top-left corner and size, in page points), with the queue index of the
/// first line of the paragraph anchoring it.</summary>
internal sealed record PlacedFloat(AnchorToken Token, double X, double Y, int QueueIndex)
{
    public WordFloatingPosition Position => Token.Shape.Floating!;

    /// <summary>Text flows around the shape (any wrapping but none).</summary>
    public bool Wraps => Position.Wrap != WordWrap.None;

    /// <summary>The box kept free of text: the shape's bounding box widened by its wrap distances (tight and through
    /// wrapping follow this box too: the outline polygon is not read).</summary>
    public (double Left, double Top, double Right, double Bottom) Box =>
        (X - Position.DistanceLeft, Y - Position.DistanceTop, X + Token.Shape.Width + Position.DistanceRight, Y + Token.Height + Position.DistanceBottom);
}

/// <summary>What a floating shape is positioned from: the page, its margins, the column, the anchoring character,
/// paragraph and line.</summary>
internal readonly record struct FloatFrames(
    double PageWidth,
    double PageHeight,
    (double Left, double Top, double Width, double Height) Margins,
    (double Left, double Width) Column,
    double CharacterX,
    double ParagraphTop,
    double LineTop,
    bool OddPage);

/// <summary>
/// The top-left corner of a floating shape (ECMA-376 part 1, §20.4.2.10 positionH and §20.4.2.11 positionV): an offset
/// from, or an alignment in, the reference it names. Horizontal references: page, margin, column, character, left and
/// right margins, inside and outside margins (left on odd pages, right on even ones, and the other way round). Vertical:
/// page, margin, paragraph, line, top and bottom margins, inside and outside margins (top and bottom alike).
/// </summary>
internal static class FloatGeometry
{
    public static (double X, double Y) Place(WordFloatingPosition position, double width, double height, FloatFrames frames)
    {
        var (left, span) = Horizontal(position.HorizontalRelativeTo, frames);
        var (top, extent) = Vertical(position.VerticalRelativeTo, frames);
        var x = Align(position.HorizontalAlignment, "left", "right", left, span, width, position.HorizontalOffset, frames.OddPage);
        var y = Align(position.VerticalAlignment, "top", "bottom", top, extent, height, position.VerticalOffset, frames.OddPage);
        return (x, y);
    }

    private static (double Start, double Length) Horizontal(string relativeTo, FloatFrames f)
    {
        var margins = f.Margins;
        var leftMargin = (0.0, margins.Left);
        var rightMargin = (margins.Left + margins.Width, f.PageWidth - margins.Left - margins.Width);
        return relativeTo switch
        {
            "page" => (0, f.PageWidth),
            "margin" => (margins.Left, margins.Width),
            "leftMargin" => leftMargin,
            "rightMargin" => rightMargin,
            "insideMargin" => f.OddPage ? leftMargin : rightMargin,
            "outsideMargin" => f.OddPage ? rightMargin : leftMargin,
            "character" => (f.CharacterX, 0),
            _ => f.Column,
        };
    }

    private static (double Start, double Length) Vertical(string relativeTo, FloatFrames f)
    {
        var margins = f.Margins;
        var topMargin = (0.0, margins.Top);
        var bottomMargin = (margins.Top + margins.Height, f.PageHeight - margins.Top - margins.Height);
        return relativeTo switch
        {
            "page" => (0, f.PageHeight),
            "margin" => (margins.Top, margins.Height),
            "topMargin" => topMargin,
            "bottomMargin" => bottomMargin,
            "insideMargin" => f.OddPage ? topMargin : bottomMargin,
            "outsideMargin" => f.OddPage ? bottomMargin : topMargin,
            "line" => (f.LineTop, 0),
            _ => (f.ParagraphTop, 0),
        };
    }

    // An alignment puts the shape at the start, centre or end of the reference (inside is the start on odd pages);
    // without one the offset is measured from the reference's start.
    private static double Align(string? alignment, string start, string end, double origin, double length, double size, double offset, bool odd) => alignment switch
    {
        _ when alignment == start => origin,
        _ when alignment == end => origin + length - size,
        "center" => origin + ((length - size) / 2),
        "inside" => odd ? origin : origin + length - size,
        "outside" => odd ? origin + length - size : origin,
        _ => origin + offset,
    };
}

/// <summary>
/// The room of the lines of a paragraph in one column next to the floating shapes of the page. A shape with square,
/// tight or through wrapping takes its box out of the lines it overlaps, on both sides, or leaves text on its left or
/// right side only, or on the side with the more room (<c>wrapText</c>); a shape with top-and-bottom wrapping takes
/// the whole line. Spans narrower than <see cref="MinimumSpan"/> are not filled (an approximation: Word's own threshold
/// is not published). A line with no span left moves down below the shapes in its way.
/// </summary>
internal sealed class FloatArea(IReadOnlyList<PlacedFloat> floats, double columnLeft, double width, double origin, bool rightToLeft) : ILineArea
{
    /// <summary>The narrowest span a line fills next to a floating shape.</summary>
    public const double MinimumSpan = 18;

    private const double Epsilon = 0.01;

    public LineRoom Room(double top, double height, double left, double right)
    {
        // The paragraph's indents, from its start edge, as page positions.
        var (lineLeft, lineRight) = rightToLeft ? (columnLeft + width - right, columnLeft + width - left) : (columnLeft + left, columnLeft + right);
        var y = origin + top;
        for (var pass = 0; pass <= floats.Count; pass++)
        {
            var blocking = Blocking(y, height, lineLeft, lineRight);
            if (blocking.Count == 0)
            {
                return new LineRoom(y - origin - top, [(left, right)]);
            }

            var spans = Spans(blocking.Select(b => b.Excluded).ToList(), lineLeft, lineRight);
            if (spans.Count > 0)
            {
                return new LineRoom(y - origin - top, spans.Select(ToStart).OrderBy(s => s.Left).ToList());
            }

            y = Math.Max(y + Epsilon, blocking.Min(b => b.Bottom));
        }

        return new LineRoom(y - origin - top, [(left, right)]);
    }

    // Page positions back to positions from the column's start edge.
    private (double Left, double Right) ToStart((double Left, double Right) span) =>
        rightToLeft ? (columnLeft + width - span.Right, columnLeft + width - span.Left) : (span.Left - columnLeft, span.Right - columnLeft);

    // The wrapping shapes the line overlaps: the range each one takes out of it, and the shape's bottom.
    private List<(double Bottom, (double Left, double Right) Excluded)> Blocking(double y, double height, double lineLeft, double lineRight)
    {
        var blocking = new List<(double, (double, double))>();
        foreach (var shape in floats)
        {
            var box = shape.Box;
            if (!shape.Wraps || box.Top >= y + height - Epsilon || box.Bottom <= y + Epsilon || box.Right <= lineLeft || box.Left >= lineRight)
            {
                continue;
            }

            blocking.Add((box.Bottom, Excluded(shape, box.Left, box.Right, lineLeft, lineRight)));
        }

        return blocking;
    }

    private static (double Left, double Right) Excluded(PlacedFloat shape, double left, double right, double lineLeft, double lineRight)
    {
        if (shape.Position.Wrap == WordWrap.TopAndBottom)
        {
            return (double.NegativeInfinity, double.PositiveInfinity);
        }

        return shape.Position.WrapSide switch
        {
            WordWrapSide.Left => (left, double.PositiveInfinity),
            WordWrapSide.Right => (double.NegativeInfinity, right),
            WordWrapSide.Largest => left - lineLeft >= lineRight - right ? (left, double.PositiveInfinity) : (double.NegativeInfinity, right),
            _ => (left, right),
        };
    }

    // The stretches of the line left free by the excluded ranges, at least MinimumSpan wide.
    private static List<(double Left, double Right)> Spans(List<(double Left, double Right)> excluded, double lineLeft, double lineRight)
    {
        var spans = new List<(double Left, double Right)>();
        var x = lineLeft;
        foreach (var (start, end) in excluded.OrderBy(e => e.Left))
        {
            if (start > x)
            {
                spans.Add((x, Math.Min(start, lineRight)));
            }

            x = Math.Max(x, end);
        }

        if (x < lineRight)
        {
            spans.Add((x, lineRight));
        }

        return spans.Where(s => s.Right - s.Left >= MinimumSpan).ToList();
    }
}

/// <summary>
/// The floating shapes of a page: placed when the first line of their anchoring paragraph is (positioned from that
/// paragraph, its line, its column or the page), looked up for the lines and rows placed after them.
/// </summary>
internal static class PageFloats
{
    private const double Epsilon = 0.01;

    /// <summary>Places the shapes anchored in the paragraph whose first line is queue item <paramref name="index"/>, at
    /// <paramref name="top"/> in <paramref name="column"/>; shapes placed for it before are replaced.</summary>
    public static void Register(PageFrame page, List<FlowItem> queue, int index, double top, (double Left, double Width) column)
    {
        page.Floats.RemoveAll(f => f.QueueIndex == index);
        var line = (LineItem)queue[index];
        foreach (var anchor in line.Flow!.Anchors.Where(a => a.Shape.Floating is not null))
        {
            var (characterX, lineTop) = Anchoring(queue, index, anchor, top);
            var setup = page.Setup;
            var frames = new FloatFrames(
                setup.Width,
                setup.Height,
                (page.BodyLeft, setup.MarginTop, page.BodyWidth, setup.Height - setup.MarginTop - setup.MarginBottom),
                column,
                column.Left + characterX,
                top,
                lineTop,
                page.PageNumber % 2 == 1);
            var (x, y) = FloatGeometry.Place(anchor.Shape.Floating!, anchor.Shape.Width, anchor.Height, frames);
            page.Floats.Add(BelowOthers(page, new PlacedFloat(anchor, x, y, index)));
        }
    }

    // A wrapping shape positioned from its paragraph or line does not overlap the wrapping shapes placed before it: it
    // moves down below them, as Word was seen to do (a shape positioned from the page or margin stays where it is).
    private static PlacedFloat BelowOthers(PageFrame page, PlacedFloat shape)
    {
        if (!shape.Wraps || shape.Position.VerticalRelativeTo is not ("paragraph" or "line"))
        {
            return shape;
        }

        for (var pass = 0; pass < page.Floats.Count; pass++)
        {
            var box = shape.Box;
            var other = page.Floats.FirstOrDefault(f => f.Wraps && f.Box.Left < box.Right && f.Box.Right > box.Left && f.Box.Top < box.Bottom && f.Box.Bottom > box.Top);
            if (other is null)
            {
                break;
            }

            shape = shape with { Y = other.Box.Bottom + shape.Position.DistanceTop };
        }

        return shape;
    }

    // Where the anchor stands: its x on its line and the top of that line, from the paragraph's lines as laid out.
    private static (double X, double LineTop) Anchoring(List<FlowItem> queue, int index, AnchorToken anchor, double top)
    {
        var flow = ((LineItem)queue[index]).Flow;
        for (var k = index; k < queue.Count && queue[k] is LineItem line && line.Flow == flow; k++)
        {
            if (line.Line.Items.FindIndex(p => p.Token == anchor) is var at and >= 0)
            {
                return (line.Line.Items[at].X, top);
            }

            top += line.Drop + line.Height;
        }

        return (0, top);
    }

    /// <summary>The wrapping shapes of the page that reach below <paramref name="top"/> across <paramref name="column"/>.</summary>
    public static List<PlacedFloat> Wrapping(PageFrame page, (double Left, double Width) column, double top) =>
        page.Floats.Where(f => f.Wraps && f.Box.Bottom > top + Epsilon && f.Box.Right > column.Left && f.Box.Left < column.Left + column.Width).ToList();

    /// <summary>
    /// How far a table row moves down to pass the wrapping shapes it would overlap: rows do not flow around shapes, they
    /// go below them (an approximation of Word, which may also narrow a table beside a shape).
    /// </summary>
    public static double RowDrop(PageFrame page, (double Left, double Width) column, double top, double height)
    {
        var shapes = Wrapping(page, column, top);
        var y = top;
        for (var pass = 0; pass <= shapes.Count; pass++)
        {
            var below = shapes.Where(f => f.Box.Top < y + height - Epsilon && f.Box.Bottom > y + Epsilon).Select(f => f.Box.Bottom).DefaultIfEmpty(y).Max();
            if (below <= y + Epsilon)
            {
                break;
            }

            y = below;
        }

        return y - top;
    }
}
