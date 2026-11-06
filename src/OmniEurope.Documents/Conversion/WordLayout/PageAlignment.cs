// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Internal;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>
/// Moves the body of a laid-out page down to the vertical alignment of its section (<c>w:vAlign</c>): centred or
/// against the bottom of the body (above the footnotes), or, justified, with the free space of a full column shared
/// between its paragraphs so that its last line ends on the bottom of the body. As in Word, a justified page ended by a
/// break or by the end of its section stays at the top. A page with several bands of columns (a continuous section
/// break) or with floating shapes is left at the top and reported.
/// </summary>
internal static class PageAlignment
{
    private const double Epsilon = 0.01;

    public static void Apply(PageFrame page, GapSet gaps)
    {
        var alignment = page.Setup.VerticalAlignment;
        if (alignment == WordPageVerticalAlignment.Top || page.Placements.Count == 0 || (alignment == WordPageVerticalAlignment.Both && !page.Full))
        {
            return;
        }

        if (page.Regions.Count != 1 || page.Floats.Count > 0)
        {
            gaps.Add(DocumentGap.Of(WordPdfGapCodes.PdfVerticalAlignmentNotApplied));
            return;
        }

        var region = page.Regions[0];
        var bottom = page.BodyBottom - page.NotesHeight;
        if (alignment == WordPageVerticalAlignment.Both)
        {
            for (var column = 0; column < region.Columns.Count; column++)
            {
                Justify(page, region, column, bottom);
            }

            return;
        }

        var free = bottom - Enumerable.Range(0, page.Placements.Count).Max(k => End(page.Placements[k]));
        if (free > Epsilon)
        {
            Shift(page, region, alignment == WordPageVerticalAlignment.Center ? free / 2 : free);
        }
    }

    // The free space of the column goes in equal shares above each item that starts a paragraph or a table, the
    // first excepted.
    private static void Justify(PageFrame page, Region region, int column, double bottom)
    {
        var indexes = Enumerable.Range(0, page.Placements.Count).Where(k => page.Placements[k].Column == column).OrderBy(k => page.Placements[k].Y).ToList();
        if (indexes.Count < 2)
        {
            return;
        }

        var free = bottom - End(page.Placements[indexes[^1]]);
        var starts = Enumerable.Range(1, indexes.Count - 1).Count(n => Starts(page.Placements[indexes[n]].Item, page.Placements[indexes[n - 1]].Item));
        if (free <= Epsilon || starts == 0)
        {
            return;
        }

        var (share, offset) = (free / starts, 0.0);
        for (var n = 1; n < indexes.Count; n++)
        {
            var placement = page.Placements[indexes[n]];
            offset += Starts(placement.Item, page.Placements[indexes[n - 1]].Item) ? share : 0;
            page.Placements[indexes[n]] = placement with { Y = placement.Y + offset };
        }

        region.Bottoms[column] += offset;
    }

    private static void Shift(PageFrame page, Region region, double offset)
    {
        for (var k = 0; k < page.Placements.Count; k++)
        {
            page.Placements[k] = page.Placements[k] with { Y = page.Placements[k].Y + offset };
        }

        for (var column = 0; column < region.Bottoms.Length; column++)
        {
            region.Bottoms[column] += offset;
        }
    }

    // An item starts a block unless it continues the paragraph or the table of the item above it.
    private static bool Starts(FlowItem item, FlowItem previous) => item switch
    {
        LineItem { LineIndex: > 0 } => false,
        RowItem row => previous is not RowItem above || above.Table != row.Table,
        _ => true,
    };

    private static double End(Placement placement) => placement.Y + placement.Item.Height;
}
