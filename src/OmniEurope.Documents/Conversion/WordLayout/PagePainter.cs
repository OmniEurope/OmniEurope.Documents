// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>Draws laid-out pages: header, body items, footnotes and footer, and adds heading bookmarks.</summary>
internal sealed class PagePainter(LayoutContext context, bool bookmarks)
{
    public void Paint(IReadOnlyList<PageFrame> pages)
    {
        var sectionPages = pages.GroupBy(p => p.Section).ToDictionary(g => g.Key, g => g.Count());
        foreach (var page in pages)
        {
            var canvas = context.Builder.AddPage(page.Setup.Width, page.Setup.Height);
            var paint = new PaintContext(canvas, context)
            {
                PageNumber = page.PageNumber,
                PageFormat = page.Setup.PageNumberFormat,
                PageCount = pages.Count,
                SectionPages = sectionPages[page.Section],
                Margins = (page.BodyLeft, page.Setup.MarginTop, page.BodyWidth, page.Setup.Height - page.Setup.MarginTop - page.Setup.MarginBottom),
                Column = (page.BodyLeft, page.BodyWidth),
                PageSize = (page.Setup.Width, page.Setup.Height),
                Floats = page.Floats.GroupBy(f => f.Token).ToDictionary(g => g.Key, g => (g.Last().X, g.Last().Y)),
            };
            Stack(paint, page.Header, page.BodyLeft, page.Setup.HeaderDistance, page.BodyWidth);
            Stack(paint, page.Footer, page.BodyLeft, page.Setup.Height - page.Setup.FooterDistance - page.FooterHeight, page.BodyWidth);
            foreach (var placement in page.Placements)
            {
                paint.Column = (placement.X, placement.Width);
                placement.Item.Paint(paint, placement.X, placement.Y, placement.Width);
                if (bookmarks && placement.Item.Bookmark is { } title)
                {
                    context.Builder.AddBookmark(title, canvas, placement.Y);
                }
            }

            PaintFootnotes(paint, page);
            PaintSeparators(paint, page);
        }
    }

    // The line between columns (w:sep) runs down the middle of each gap, from the top of the columns to the bottom of the
    // longest one.
    private static void PaintSeparators(PaintContext paint, PageFrame page)
    {
        foreach (var region in page.Regions.Where(r => r.Separator))
        {
            var bottom = region.Bottoms.Max();
            for (var c = 0; c + 1 < region.Columns.Count && bottom > region.Top; c++)
            {
                var x = (region.Columns[c].Left + region.Columns[c].Width + region.Columns[c + 1].Left) / 2;
                paint.Canvas.DrawLine(x, region.Top, x, bottom, Pdf.PdfColor.Black, 0.5);
            }
        }
    }

    private static void PaintFootnotes(PaintContext paint, PageFrame page)
    {
        if (page.Footnotes.Count == 0)
        {
            return;
        }

        var y = page.BodyBottom - page.NotesHeight;
        paint.Column = (page.BodyLeft, page.BodyWidth);
        RuleItem.Separator(page.SeparatorHeight).Paint(paint, page.BodyLeft, y, page.BodyWidth);
        y += page.SeparatorHeight;
        foreach (var note in page.Footnotes)
        {
            y = Stack(paint, note.Items, page.BodyLeft, y, page.BodyWidth);
        }
    }

    private static double Stack(PaintContext paint, List<FlowItem> items, double x, double y, double width)
    {
        foreach (var item in items)
        {
            y += item.SpaceBefore;
            item.Paint(paint, x, y, width);
            y += item.Height + item.SpaceAfter;
        }

        return y;
    }
}
