// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>An item placed on a page, with the column it belongs to.</summary>
internal readonly record struct Placement(FlowItem Item, double X, double Y, double Width, int QueueIndex, int Column);

/// <summary>Columns sharing a horizontal band of a page (a page holds several after continuous section breaks).</summary>
internal sealed class Region(double top, IReadOnlyList<(double Left, double Width)> columns, int firstPlacement)
{
    /// <summary>A line is drawn between the columns (<c>w:sep</c>).</summary>
    public bool Separator { get; init; }

    public double Top { get; } = top;

    public IReadOnlyList<(double Left, double Width)> Columns { get; } = columns;

    public int FirstPlacement { get; } = firstPlacement;

    /// <summary>Lowest point reached in each column.</summary>
    public double[] Bottoms { get; } = Enumerable.Repeat(top, columns.Count).ToArray();
}

/// <summary>A footnote placed at the bottom of a page.</summary>
internal sealed record PlacedNote(int Id, List<FlowItem> Items, double Height);

/// <summary>A laid-out page: setup, header and footer, placed body items and footnotes.</summary>
internal sealed class PageFrame
{
    public required WordSection Section { get; init; }

    public required WordPageSetup Setup { get; init; }

    public int PageNumber { get; init; }

    public List<FlowItem> Header { get; init; } = [];

    public List<FlowItem> Footer { get; init; } = [];

    public double HeaderHeight => Header.Sum(i => i.SpaceBefore + i.Height + i.SpaceAfter);

    public double FooterHeight => Footer.Sum(i => i.SpaceBefore + i.Height + i.SpaceAfter);

    public double BodyLeft => Setup.MarginLeft + Setup.Gutter;

    public double BodyWidth => Setup.ContentWidth;

    public double BodyTop => Math.Max(Setup.MarginTop, Setup.HeaderDistance + HeaderHeight);

    public double BodyBottom => Setup.Height - Math.Max(Setup.MarginBottom, Setup.FooterDistance + FooterHeight);

    public List<Placement> Placements { get; } = [];

    public List<Region> Regions { get; } = [];

    public List<PlacedNote> Footnotes { get; } = [];

    /// <summary>The floating shapes anchored in the body paragraphs placed on the page.</summary>
    public List<PlacedFloat> Floats { get; } = [];

    /// <summary>Height of the separator line drawn above the footnotes.</summary>
    public double SeparatorHeight { get; set; }

    public double NotesHeight => Footnotes.Count == 0 ? 0 : SeparatorHeight + Footnotes.Sum(n => n.Height);
}
