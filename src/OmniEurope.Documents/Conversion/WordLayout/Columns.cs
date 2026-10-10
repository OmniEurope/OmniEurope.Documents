// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>The text columns of a section (ECMA-376 part 1, §17.6.4 cols): explicit widths each followed by its own
/// space (<c>w:col</c>), else equal shares of the text width.</summary>
internal static class SectionColumns
{
    /// <summary>Columns relative to the left margin.</summary>
    public static List<(double Left, double Width)> Of(WordPageSetup setup)
    {
        var result = new List<(double, double)>();
        var x = 0.0;
        if (setup.ColumnWidths is { Count: > 0 } widths)
        {
            for (var i = 0; i < widths.Count; i++)
            {
                result.Add((x, widths[i]));
                x += widths[i] + (setup.ColumnSpacings is { } spaces && i < spaces.Count ? spaces[i] : setup.ColumnSpacing);
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

    /// <summary>True when the columns are not all of one width.</summary>
    public static bool Unequal(IReadOnlyList<(double Left, double Width)> columns) =>
        columns.Any(c => Math.Abs(c.Width - columns[0].Width) > 0.01);
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
