// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using OmniEurope.Documents.Charts;

namespace OmniEurope.Documents.Excel;

/// <summary>A chart of a worksheet's drawing and where it lies over the cells.</summary>
public sealed class XlsxChart
{
    internal XlsxChart(Chart chart, XElement part, XlsxRange cells, (double X, double Y) offset, (double Width, double Height) size)
    {
        Chart = chart;
        Part = part;
        Cells = cells;
        (OffsetX, OffsetY) = offset;
        (Width, Height) = size;
    }

    /// <summary>The chart.</summary>
    public Chart Chart { get; }

    /// <summary>The cells the chart lies over: from the one holding its top-left corner to the one holding its
    /// bottom-right corner (1-based).</summary>
    public XlsxRange Cells { get; }

    /// <summary>The distance in points from the left edge of the first cell to the chart's left edge.</summary>
    public double OffsetX { get; }

    /// <summary>The distance in points from the top edge of the first cell to the chart's top edge.</summary>
    public double OffsetY { get; }

    /// <summary>The width in points, from the anchor's extent or the widths of the columns it spans.</summary>
    public double Width { get; }

    /// <summary>The height in points, from the anchor's extent or the rows it spans at the default height of 15 points
    /// (row heights are not read).</summary>
    public double Height { get; }

    /// <summary>The chart part as read (<c>c:chartSpace</c>).</summary>
    internal XElement Part { get; }
}
