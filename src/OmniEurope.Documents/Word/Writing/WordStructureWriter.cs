// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;
using static OmniEurope.Documents.Word.Writing.WordPropertyWriter;

namespace OmniEurope.Documents.Word.Writing;

/// <summary>Writes table, row, cell and section properties in schema order.</summary>
internal static class WordStructureWriter
{
    public static XElement Table(WordTableProperties p)
    {
        var element = new XElement(W + "tblPr");
        Add(element, p.StyleId is null ? null : ValElement("tblStyle", p.StyleId));
        Add(element, p.Width is { } width ? Width("tblW", width) : null);
        Add(element, p.Alignment is { } alignment ? ValElement("jc", alignment switch
        {
            WordAlignment.Center => "center",
            WordAlignment.Right => "right",
            _ => "left",
        }) : null);
        Add(element, p.Indent is { } indent ? Width("tblInd", WordWidth.Points(indent)) : null);
        Add(element, Borders("tblBorders", p.Borders));
        Add(element, Shading(p.Shading));
        Add(element, p.FixedLayout is { } fixedLayout ? new XElement(W + "tblLayout", new XAttribute(W + "type", fixedLayout ? "fixed" : "autofit")) : null);
        Add(element, Margins("tblCellMar", p.CellMargins));
        Add(element, Look(p.Look));
        return element;
    }

    public static XElement? Row(WordTableRowProperties p, WordRevision? revision, Func<WordRevision, string, XElement> revisionElement)
    {
        var element = new XElement(W + "trPr");
        Add(element, p.GridBefore is { } before ? ValElement("gridBefore", Format(before)) : null);
        Add(element, p.GridAfter is { } after ? ValElement("gridAfter", Format(after)) : null);
        Add(element, Flag("cantSplit", p.CantSplit));
        if (p.Height is { } height)
        {
            var heightElement = ValElement("trHeight", ToTwips(height));
            heightElement.Add(new XAttribute(W + "hRule", p.HeightRule == WordRowHeightRule.Exact ? "exact" : "atLeast"));
            element.Add(heightElement);
        }

        Add(element, Flag("tblHeader", p.IsHeader));
        if (revision is not null)
        {
            element.Add(revisionElement(revision, revision.Kind == WordRevisionKind.Deleted ? "del" : "ins"));
        }

        return element.HasElements ? element : null;
    }

    public static XElement? Cell(WordTableCellProperties p)
    {
        var element = new XElement(W + "tcPr");
        Add(element, p.Width is { } width ? Width("tcW", width) : null);
        Add(element, p.GridSpan is > 1 ? ValElement("gridSpan", Format(p.GridSpan.Value)) : null);
        Add(element, p.VerticalMerge switch
        {
            WordVerticalMerge.Restart => ValElement("vMerge", "restart"),
            WordVerticalMerge.Continue => new XElement(W + "vMerge"),
            _ => null,
        });
        Add(element, Borders("tcBorders", p.Borders));
        Add(element, Shading(p.Shading));
        Add(element, Flag("noWrap", p.NoWrap));
        Add(element, Margins("tcMar", p.Margins));
        Add(element, p.VerticalAlignment is { } vertical ? ValElement("vAlign", vertical switch
        {
            WordCellAlignment.Center => "center",
            WordCellAlignment.Bottom => "bottom",
            _ => "top",
        }) : null);
        return element.HasElements ? element : null;
    }

    private static XElement Width(string name, WordWidth width) => new(
        W + name,
        new XAttribute(W + "w", width.Type switch
        {
            WordWidthType.Points => ToTwips(width.Value),
            WordWidthType.Percent => Format((int)Math.Round(width.Value * 50)),
            _ => "0",
        }),
        new XAttribute(W + "type", width.Type switch
        {
            WordWidthType.Points => "dxa",
            WordWidthType.Percent => "pct",
            WordWidthType.Nil => "nil",
            _ => "auto",
        }));

    private static XElement? Borders(string name, WordTableBorders? borders)
    {
        if (borders is null)
        {
            return null;
        }

        var element = new XElement(W + name);
        Add(element, Border("top", borders.Top));
        Add(element, Border("left", borders.Left));
        Add(element, Border("bottom", borders.Bottom));
        Add(element, Border("right", borders.Right));
        Add(element, Border("insideH", borders.InsideHorizontal));
        Add(element, Border("insideV", borders.InsideVertical));
        return element.HasElements ? element : null;
    }

    private static XElement? Margins(string name, WordCellMargins? margins)
    {
        if (margins is null)
        {
            return null;
        }

        var element = new XElement(W + name);
        Add(element, margins.Top is { } top ? Width("top", WordWidth.Points(top)) : null);
        Add(element, margins.Left is { } left ? Width("left", WordWidth.Points(left)) : null);
        Add(element, margins.Bottom is { } bottom ? Width("bottom", WordWidth.Points(bottom)) : null);
        Add(element, margins.Right is { } right ? Width("right", WordWidth.Points(right)) : null);
        return element.HasElements ? element : null;
    }

    private static XElement? Look(WordTableLook? look)
    {
        if (look is not { } value)
        {
            return null;
        }

        return new XElement(
            W + "tblLook",
            new XAttribute(W + "firstRow", Bit(value, WordTableLook.FirstRow)),
            new XAttribute(W + "lastRow", Bit(value, WordTableLook.LastRow)),
            new XAttribute(W + "firstColumn", Bit(value, WordTableLook.FirstColumn)),
            new XAttribute(W + "lastColumn", Bit(value, WordTableLook.LastColumn)),
            new XAttribute(W + "noHBand", Bit(value, WordTableLook.NoHorizontalBanding)),
            new XAttribute(W + "noVBand", Bit(value, WordTableLook.NoVerticalBanding)));
    }

    private static string Bit(WordTableLook value, WordTableLook flag) => value.HasFlag(flag) ? "1" : "0";

    public static string Region(WordTableRegion region) => WordValues.Name(WordValues.Regions, region);

    /// <summary>A <c>w:sectPr</c>; <paramref name="references"/> are the header and footer references.</summary>
    public static XElement Section(WordPageSetup page, IEnumerable<XElement> references)
    {
        var element = new XElement(W + "sectPr", references);
        if (page.Start != WordSectionStart.NextPage)
        {
            element.Add(ValElement("type", page.Start switch
            {
                WordSectionStart.Continuous => "continuous",
                WordSectionStart.EvenPage => "evenPage",
                WordSectionStart.OddPage => "oddPage",
                _ => "nextColumn",
            }));
        }

        var size = new XElement(W + "pgSz", new XAttribute(W + "w", ToTwips(page.Width)), new XAttribute(W + "h", ToTwips(page.Height)));
        if (page.Landscape)
        {
            size.Add(new XAttribute(W + "orient", "landscape"));
        }

        element.Add(size, new XElement(
            W + "pgMar",
            new XAttribute(W + "top", ToTwips(page.MarginTop)),
            new XAttribute(W + "right", ToTwips(page.MarginRight)),
            new XAttribute(W + "bottom", ToTwips(page.MarginBottom)),
            new XAttribute(W + "left", ToTwips(page.MarginLeft)),
            new XAttribute(W + "header", ToTwips(page.HeaderDistance)),
            new XAttribute(W + "footer", ToTwips(page.FooterDistance)),
            new XAttribute(W + "gutter", ToTwips(page.Gutter))));
        Add(element, PageNumbers(page));
        element.Add(Columns(page));
        Add(element, page.TitlePage ? new XElement(W + "titlePg") : null);
        return element;
    }

    private static XElement? PageNumbers(WordPageSetup page)
    {
        if (page.PageNumberStart is null && page.PageNumberFormat == WordNumberFormat.Decimal)
        {
            return null;
        }

        var element = new XElement(W + "pgNumType");
        if (page.PageNumberFormat != WordNumberFormat.Decimal)
        {
            element.Add(new XAttribute(W + "fmt", NumberFormat(page.PageNumberFormat)));
        }

        AddAttribute(element, "start", page.PageNumberStart is { } start ? Format(start) : null);
        return element;
    }

    private static XElement Columns(WordPageSetup page)
    {
        var element = new XElement(W + "cols", new XAttribute(W + "space", ToTwips(page.ColumnSpacing)));
        if (page.Columns > 1)
        {
            element.Add(new XAttribute(W + "num", Format(page.Columns)));
        }

        if (page.ColumnSeparator)
        {
            element.Add(new XAttribute(W + "sep", "1"));
        }

        if (page.ColumnWidths is { Count: > 0 } widths)
        {
            element.Add(new XAttribute(W + "equalWidth", "0"));
            element.Add(widths.Select(w => new XElement(W + "col", new XAttribute(W + "w", ToTwips(w)), new XAttribute(W + "space", ToTwips(page.ColumnSpacing)))));
        }

        return element;
    }

    public static string NumberFormat(WordNumberFormat format) => WordValues.Name(WordValues.NumberFormats, format);
}
