// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Writing;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>Draws placed tokens: text with its decorations, tab leaders, page fields, boxes and floating shapes.</summary>
internal static class FragmentPainter
{
    public static void Paint(PaintContext context, Placed item, double lineX, double lineY, double baselineOffset)
    {
        var x = lineX + item.X;
        var baseline = lineY + baselineOffset;
        switch (item.Token)
        {
            case TextToken text:
                PaintText(context, text.Style, text.Text, text.IsSpace, x, baseline, item.Width);
                break;
            case FieldToken field:
                var value = context.FieldText(field.Kind);
                PaintText(context, field.Style, value, false, x, baseline, context.Layout.Measure(value, field.Style));
                break;
            case TabToken tab:
                PaintLeader(context, tab.Style, item.Leader, x, baseline, item.Width);
                break;
            case BoxToken box:
                box.Paint(context, x, baseline - box.Height);
                break;
            case AnchorToken anchor:
                var (left, top) = FloatPosition(context, anchor.Shape.Floating!, x, lineY);
                anchor.Paint(context, left, top);
                break;
        }
    }

    // Spaces are drawn as glyphs (so text extraction keeps them) but their decorations span their
    // stretched width.
    private static void PaintText(PaintContext context, TextStyle style, string text, bool space, double x, double baseline, double width)
    {
        var canvas = context.Canvas;
        var metrics = context.Layout.Metrics(style);
        var shifted = baseline - style.Shift;
        if (style.Background is { } background)
        {
            canvas.FillRectangle(x, shifted - metrics.Ascent, width, metrics.Ascent + metrics.Descent, background);
        }

        var simpleUnderline = style.Underline == WordUnderline.Single || (style.Underline == WordUnderline.Words && !space);
        canvas.DrawText(text, x, shifted, style.Font, style.Size, style.Color, style.CharacterSpacing, underline: simpleUnderline && !space, strikethrough: style.Strike && !space);
        if (space && (simpleUnderline || style.Strike))
        {
            Decorate(canvas, style, metrics, x, shifted, width, simpleUnderline ? WordUnderline.Single : WordUnderline.None, style.Strike);
        }

        Decorate(canvas, style, metrics, x, shifted, width, simpleUnderline ? WordUnderline.None : style.Underline, false);
        if (style.DoubleStrike)
        {
            var y = shifted - (style.Size * 0.28);
            canvas.DrawLine(x, y - (metrics.UnderlineThickness * 1.2), x + width, y - (metrics.UnderlineThickness * 1.2), style.Color, metrics.UnderlineThickness);
            canvas.DrawLine(x, y + (metrics.UnderlineThickness * 1.2), x + width, y + (metrics.UnderlineThickness * 1.2), style.Color, metrics.UnderlineThickness);
        }

        if (style.Link is { } link && !space)
        {
            canvas.AddLink(x, shifted - metrics.Ascent, width, metrics.Ascent + metrics.Descent, link);
        }
    }

    private static void Decorate(PdfCanvas canvas, TextStyle style, PdfFontMetrics metrics, double x, double baseline, double width, WordUnderline underline, bool strike)
    {
        var thickness = metrics.UnderlineThickness;
        var under = baseline + metrics.UnderlinePosition;
        switch (underline)
        {
            case WordUnderline.Single or WordUnderline.Words:
                canvas.DrawLine(x, under, x + width, under, style.Color, thickness);
                break;
            case WordUnderline.Double:
                canvas.DrawLine(x, under - thickness, x + width, under - thickness, style.Color, thickness);
                canvas.DrawLine(x, under + thickness, x + width, under + thickness, style.Color, thickness);
                break;
            case WordUnderline.Thick:
                canvas.DrawLine(x, under, x + width, under, style.Color, thickness * 2);
                break;
            case WordUnderline.Dotted or WordUnderline.Dash or WordUnderline.Wave:
                double[] dash = underline == WordUnderline.Dotted ? [thickness, thickness * 2] : [thickness * 4, thickness * 2];
                canvas.DrawLine(x, under, x + width, under, style.Color, thickness, dash);
                break;
        }

        if (strike)
        {
            var y = baseline - (style.Size * 0.28);
            canvas.DrawLine(x, y, x + width, y, style.Color, thickness);
        }
    }

    private static void PaintLeader(PaintContext context, TextStyle style, WordTabLeader leader, double x, double baseline, double width)
    {
        var glyph = leader switch
        {
            WordTabLeader.Dot => ".",
            WordTabLeader.Hyphen => "-",
            WordTabLeader.MiddleDot => "·",
            WordTabLeader.Underscore or WordTabLeader.Heavy => null,
            _ => string.Empty,
        };
        if (glyph is null)
        {
            var metrics = context.Layout.Metrics(style);
            var thickness = metrics.UnderlineThickness * (leader == WordTabLeader.Heavy ? 2 : 1);
            context.Canvas.DrawLine(x, baseline + metrics.UnderlinePosition, x + width, baseline + metrics.UnderlinePosition, style.Color, thickness);
            return;
        }

        if (glyph.Length == 0 || width <= 0)
        {
            return;
        }

        // Leader characters sit on a grid ending at the stop, like Word's, so successive lines line up.
        var step = context.Layout.Measure(glyph, style) * 1.5;
        var count = (int)Math.Floor((width - 2) / step);
        if (count > 0)
        {
            var text = string.Concat(Enumerable.Repeat(glyph, count));
            context.Canvas.DrawText(text, x + width - (count * step) - 1, baseline, style.Font, style.Size, style.Color, characterSpacing: step - context.Layout.Measure(glyph, style));
        }
    }

    /// <summary>The top-left corner of a floating shape from its anchor offsets.</summary>
    public static (double Left, double Top) FloatPosition(PaintContext context, WordFloatingPosition floating, double anchorX, double paragraphTop)
    {
        var margins = context.Margins;
        var left = floating.HorizontalRelativeTo switch
        {
            "page" => 0,
            "margin" or "leftMargin" or "insideMargin" => margins.Left,
            "character" => anchorX,
            _ => context.Column.Left,
        };
        var top = floating.VerticalRelativeTo switch
        {
            "page" or "topMargin" => 0,
            "margin" => margins.Top,
            _ => paragraphTop,
        };
        return (left + floating.HorizontalOffset, top + floating.VerticalOffset);
    }
}

/// <summary>Draws border lines in their style (single, double, dashed, dotted).</summary>
internal static class BorderPainter
{
    public static void Line(PdfCanvas canvas, WordBorder border, double x1, double y1, double x2, double y2)
    {
        var width = Math.Max(border.Width, 0.25);
        var color = TextStyle.ParseColor(border.Color) ?? PdfColor.Black;
        switch (border.Style)
        {
            case "double":
                var horizontal = Math.Abs(y2 - y1) < Math.Abs(x2 - x1);
                var (dx, dy) = horizontal ? (0.0, width) : (width, 0.0);
                canvas.DrawLine(x1 - dx, y1 - dy, x2 - dx, y2 - dy, color, width);
                canvas.DrawLine(x1 + dx, y1 + dy, x2 + dx, y2 + dy, color, width);
                break;
            case "dotted":
                canvas.DrawLine(x1, y1, x2, y2, color, width, [width, width * 2]);
                break;
            case "dashed" or "dashSmallGap" or "dotDash" or "dotDotDash":
                canvas.DrawLine(x1, y1, x2, y2, color, width, [width * 4, width * 2]);
                break;
            default:
                canvas.DrawLine(x1, y1, x2, y2, color, width);
                break;
        }
    }
}
