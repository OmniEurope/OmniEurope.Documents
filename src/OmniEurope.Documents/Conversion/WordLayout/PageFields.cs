// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>
/// Lays the headers and footers out again for each page once the page count is known, so a PAGE, NUMPAGES or
/// SECTIONPAGES field takes the width of the value it shows there (as Word does) rather than that of its cached result.
/// A layout that would change the height of the header or footer, which the body was paginated against, is not taken.
/// </summary>
internal static class PageFields
{
    public static void Apply(List<PageFrame> pages, LayoutContext context, BlockLayout blocks)
    {
        var sectionPages = pages.GroupBy(p => p.Section).ToDictionary(g => g.Key, g => g.Count());
        var cache = new Dictionary<(WordHeaderFooter, double, FieldValues), List<FlowItem>>();
        foreach (var page in pages)
        {
            var values = new FieldValues(page.PageNumber, page.Setup.PageNumberFormat, pages.Count, sectionPages[page.Section]);
            page.Header = Layout(page.Parts.Header, page.Header, page.Setup.ContentWidth, values);
            page.Footer = Layout(page.Parts.Footer, page.Footer, page.Setup.ContentWidth, values);
        }

        List<FlowItem> Layout(WordHeaderFooter? part, List<FlowItem> current, double width, FieldValues values)
        {
            if (part is null || !current.Any(HasField))
            {
                return current;
            }

            if (!cache.TryGetValue((part, width, values), out var items))
            {
                context.Fields = values;
                try
                {
                    items = blocks.Layout(part.Blocks, width, numbering: false);
                }
                finally
                {
                    context.Fields = null;
                }

                cache[(part, width, values)] = items;
            }

            return Math.Abs(Height(items) - Height(current)) < 0.01 ? items : current;
        }
    }

    private static double Height(List<FlowItem> items) => items.Sum(i => i.SpaceBefore + i.Height + i.SpaceAfter);

    private static bool HasField(FlowItem item) => item switch
    {
        LineItem line => line.Line.Items.Exists(i => i.Token is FieldToken),
        RowItem row => row.Cells.Any(c => c.Items.Any(HasField)),
        _ => false,
    };
}
