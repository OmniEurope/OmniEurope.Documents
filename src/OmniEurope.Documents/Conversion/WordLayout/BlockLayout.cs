// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>
/// Lays blocks out at a given width into flow items: paragraphs into lines (with their spacing, keep rules
/// and decorations), tables into rows. Paragraph keep rules become "no break before" marks: all lines of a
/// keep-lines paragraph, the second and last lines under widow control, and the first line after a
/// keep-with-next paragraph.
/// </summary>
internal sealed class BlockLayout
{
    private readonly LayoutContext _context;

    public BlockLayout(LayoutContext context)
    {
        _context = context;
        Shapes = new ShapeFactory(context, this);
    }

    public ShapeFactory Shapes { get; }

    public List<FlowItem> Layout(IReadOnlyList<WordBlock> blocks, double width, CellStyle? cell = null, bool numbering = true)
    {
        var items = new List<FlowItem>();
        WordParagraphProperties? previous = null;
        List<FlowItem>? previousItems = null;
        foreach (var block in blocks)
        {
            WordParagraphProperties? resolved = null;
            List<FlowItem> blockItems;
            if (block is WordParagraph paragraph)
            {
                resolved = Resolve(paragraph, cell);
                blockItems = Paragraph(paragraph, resolved, width, cell, numbering);
                ContextualSpacing(previous, previousItems, resolved, blockItems);
                CollapseSpacing(previous, previousItems, blockItems);
            }
            else
            {
                blockItems = new TableLayout(_context, this).Layout((WordTable)block, width);
            }

            if (previous?.KeepNext == true && blockItems.Count > 0)
            {
                blockItems[0].CanBreakBefore = false;
            }

            // A page or column break ending the previous paragraph moves this block on.
            if (items.Count > 0 && items[^1] is LineItem { Line.BreakAfter: { } pending } && blockItems.Count > 0)
            {
                blockItems[0].PageBreakBefore |= pending == WordBreakKind.Page;
                blockItems[0].ColumnBreakBefore |= pending == WordBreakKind.Column;
            }

            items.AddRange(blockItems);
            previous = resolved;
            previousItems = blockItems;
        }

        return items;
    }

    private WordParagraphProperties Resolve(WordParagraph paragraph, CellStyle? cell) => WordResolution.Paragraph(_context.Document, paragraph, cell);

    // Between two paragraphs Word keeps the larger of the space after and the space before, not their sum (measured
    // against Word); the full space before stays for the top of a column a hard break opens.
    private static void CollapseSpacing(WordParagraphProperties? previous, List<FlowItem>? previousItems, List<FlowItem> items)
    {
        if (previous is null || previousItems is not { Count: > 0 } || items.Count == 0 || previousItems[^1].SpaceAfter <= 0)
        {
            return;
        }

        var first = items[0];
        first.FullSpaceBefore = first.SpaceBefore;
        first.SpaceBefore = Math.Max(0, first.SpaceBefore - previousItems[^1].SpaceAfter);
    }

    // Space between two paragraphs of the same style is dropped on the side that asks for it.
    private static void ContextualSpacing(WordParagraphProperties? previous, List<FlowItem>? previousItems, WordParagraphProperties current, List<FlowItem> items)
    {
        if (previous is null || previousItems is not { Count: > 0 } || items.Count == 0 || previous.StyleId != current.StyleId)
        {
            return;
        }

        if (previous.ContextualSpacing == true)
        {
            previousItems[^1].SpaceAfter = 0;
        }

        if (current.ContextualSpacing == true)
        {
            items[0].SpaceBefore = 0;
        }
    }

    private List<FlowItem> Paragraph(WordParagraph paragraph, WordParagraphProperties p, double width, CellStyle? cell, bool numbering)
    {
        var resolver = new RunResolver(_context, p, cell);
        var tokens = new InlineBuilder(resolver, Shapes, numbering).Build(paragraph);
        var flow = new ParagraphFlow(_context, paragraph, p, tokens, resolver.Style(p.MarkProperties ?? WordRunProperties.Empty));
        return [.. flow.Lay(width)];
    }
}

/// <summary>Paint actions for pictures and text boxes.</summary>
internal sealed class ShapeFactory(LayoutContext context, BlockLayout blocks)
{
    private const double BoxInsetX = 7.2;
    private const double BoxInsetY = 3.6;

    /// <summary>Draws the shape with its top-left corner at the given point and the height it takes.</summary>
    public (Action<PaintContext, double, double> Paint, double Height) Painter(WordShape shape)
    {
        switch (shape)
        {
            case WordPicture picture:
                return ((paint, x, y) => Picture(paint, picture, x, y), picture.Height);
            case WordTextBox box:
                var (paintBox, height) = TextBox(box);
                return (paintBox, height);
            default:
                return ((_, _, _) => { }, shape.Height);
        }
    }


    private void Picture(PaintContext paint, WordPicture picture, double x, double y)
    {
        if (context.Image(picture.Image) is { } image)
        {
            paint.Canvas.DrawImage(image, x, y, picture.Width, picture.Height);
            return;
        }

        if (picture.Image.ContentType is "image/x-emf" or "image/emf" && Emf.EmfRenderer.TryDraw(paint, picture.Image.Data, x, y, picture.Width, picture.Height))
        {
            return;
        }

        context.Gaps.Add($"picture format {picture.Image.ContentType} drawn as an empty frame");
        paint.Canvas.StrokeRectangle(x, y, picture.Width, picture.Height, Pdf.PdfColor.LightGray, 0.5);
    }

    // The box content is laid out once, then drawn clipped to the box; a box fitting its text grows to hold it.
    private (Action<PaintContext, double, double> Paint, double Height) TextBox(WordTextBox box)
    {
        var width = Math.Max(1, box.Width - (2 * BoxInsetX));
        var items = blocks.Layout(box.Blocks, width);
        var height = box.FitsText ? Math.Max(box.Height, items.Sum(i => i.SpaceBefore + i.Height + i.SpaceAfter) + (2 * BoxInsetY)) : box.Height;
        return ((paint, x, y) =>
        {
            var canvas = paint.Canvas;
            canvas.SaveState();
            canvas.ClipRectangle(x, y, box.Width, height);
            var top = y + BoxInsetY;
            foreach (var item in items)
            {
                top += item.SpaceBefore;
                item.Paint(paint, x + BoxInsetX, top, width);
                top += item.Height + item.SpaceAfter;
            }

            canvas.RestoreState();
        }, height);
    }
}
