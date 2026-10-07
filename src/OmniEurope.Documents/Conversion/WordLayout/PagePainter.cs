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
