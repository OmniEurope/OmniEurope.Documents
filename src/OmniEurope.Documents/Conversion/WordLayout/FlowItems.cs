// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>
/// A unit of vertical flow that pages are filled with: a line of a paragraph or a table row. Items carry
/// the keep rules as "may the page break before me", and optional spacing above (dropped at the top of a
/// column after a natural break) and below (allowed to run past the bottom).
/// </summary>
internal abstract class FlowItem
{
    public double Height { get; set; }

    public double SpaceBefore { get; set; }

    public double SpaceAfter { get; set; }

    public bool CanBreakBefore { get; set; } = true;

    public bool PageBreakBefore { get; set; }

    public bool ColumnBreakBefore { get; set; }

    /// <summary>Outline entry of a heading, set on its first line.</summary>
    public string? Bookmark { get; set; }

    public virtual IEnumerable<(WordNoteKind Kind, int Id)> Notes => [];

    /// <summary>Splits the item so its first part fits in <paramref name="available"/>; null when it cannot.</summary>
    public virtual (FlowItem First, FlowItem Remainder)? Split(double available) => null;

    public abstract void Paint(PaintContext context, double x, double y, double width);
}

/// <summary>The box a paragraph's shading and borders fill, relative to its column.</summary>
internal sealed record ParagraphFrame(double Left, double Right, PdfColor? Shading, WordParagraphBorders? Borders);

/// <summary>One line of a paragraph, with the paragraph's shading and borders around it.</summary>
internal sealed class LineItem(Line line, ParagraphFrame frame, bool first, bool last) : FlowItem
{
    public Line Line { get; } = line;

    public override IEnumerable<(WordNoteKind Kind, int Id)> Notes => Line.Notes;

    public override void Paint(PaintContext context, double x, double y, double width)
    {
        PaintFrame(context, x, y);
        foreach (var item in Line.Items)
        {
            FragmentPainter.Paint(context, item, x, y, Line.Baseline);
        }
    }

    private void PaintFrame(PaintContext context, double x, double y)
    {
        var canvas = context.Canvas;
        var (left, right) = (x + frame.Left, x + frame.Right);
        if (frame.Shading is { } shading)
        {
            canvas.FillRectangle(left, y, right - left, Height, shading);
        }

        if (frame.Borders is not { } borders)
        {
            return;
        }

        Border(context, borders.Top, first, left, y, right, y);
        Border(context, borders.Bottom, last, left, y + Height, right, y + Height);
        Border(context, borders.Left, true, left, y, left, y + Height);
        Border(context, borders.Right, true, right, y, right, y + Height);
    }

    private static void Border(PaintContext context, WordBorder? border, bool draw, double x1, double y1, double x2, double y2)
    {
        if (draw && border is { IsNone: false })
        {
            BorderPainter.Line(context.Canvas, border, x1, y1, x2, y2);
        }
    }
}

/// <summary>A fixed vertical gap or a short rule (note separators).</summary>
internal sealed class RuleItem(double ruleWidth) : FlowItem
{
    public double RuleWidth { get; } = ruleWidth;

    public override void Paint(PaintContext context, double x, double y, double width)
    {
        if (RuleWidth > 0)
        {
            context.Canvas.DrawLine(x, y + (Height / 2), x + Math.Min(RuleWidth, width), y + (Height / 2), PdfColor.Black, 0.5);
        }
    }

    public static RuleItem Separator(double lineHeight) => new(144) { Height = lineHeight };
}
