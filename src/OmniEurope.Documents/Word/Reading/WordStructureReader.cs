// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Reading;

/// <summary>Reads table, row, cell and section properties.</summary>
internal static class WordStructureReader
{
    public static WordTableProperties? Table(XElement? tblPr)
    {
        if (tblPr is null)
        {
            return null;
        }

        return new WordTableProperties
        {
            StyleId = Val(tblPr, "tblStyle"),
            Width = Width(tblPr.Element(W + "tblW")),
            Alignment = WordPropertyReader.Alignment(Val(tblPr, "jc")),
            Indent = Twips(Attr(tblPr.Element(W + "tblInd"), "w")),
            FixedLayout = Attr(tblPr.Element(W + "tblLayout"), "type") is { } layout ? layout == "fixed" : null,
            Borders = Borders(tblPr.Element(W + "tblBorders")),
            CellMargins = Margins(tblPr.Element(W + "tblCellMar")),
            Shading = WordPropertyReader.Fill(tblPr.Element(W + "shd")),
            Look = Look(tblPr.Element(W + "tblLook")),
        };
    }

    public static WordTableRowProperties Row(XElement? trPr)
    {
        if (trPr is null)
        {
            return WordTableRowProperties.Empty;
        }

        var height = trPr.Element(W + "trHeight");
        return new WordTableRowProperties
        {
            Height = Twips(Attr(height, "val")),
            HeightRule = Attr(height, "hRule") switch
            {
                "exact" => WordRowHeightRule.Exact,
                "atLeast" => WordRowHeightRule.AtLeast,
                _ => null,
            },
            IsHeader = OnOff(trPr, "tblHeader"),
            CantSplit = OnOff(trPr, "cantSplit"),
            GridBefore = Int(Val(trPr, "gridBefore")),
            GridAfter = Int(Val(trPr, "gridAfter")),
        };
    }

    public static WordTableCellProperties? Cell(XElement? tcPr)
    {
        if (tcPr is null)
        {
            return null;
        }

        var merge = tcPr.Element(W + "vMerge");
        return new WordTableCellProperties
        {
            Width = Width(tcPr.Element(W + "tcW")),
            GridSpan = Int(Val(tcPr, "gridSpan")),
            VerticalMerge = merge is null ? null : Attr(merge, "val") == "restart" ? WordVerticalMerge.Restart : WordVerticalMerge.Continue,
            VerticalAlignment = Val(tcPr, "vAlign") switch
            {
                "center" => WordCellAlignment.Center,
                "bottom" => WordCellAlignment.Bottom,
                "top" => WordCellAlignment.Top,
                _ => null,
            },
            Shading = WordPropertyReader.Fill(tcPr.Element(W + "shd")),
            Borders = Borders(tcPr.Element(W + "tcBorders")),
            Margins = Margins(tcPr.Element(W + "tcMar")),
            NoWrap = OnOff(tcPr, "noWrap"),
        };
    }

    public static WordWidth? Width(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var value = Attr(element, "w");
        return Attr(element, "type") switch
        {
            "pct" => WordWidth.Percent(value is not null && value.EndsWith('%') ? Measure(value[..^1], 1) ?? 0 : Measure(value, 50) ?? 0),
            "auto" => WordWidth.Auto,
            "nil" => new WordWidth(0, WordWidthType.Nil),
            _ => WordWidth.Points(Twips(value) ?? 0),
        };
    }

    private static WordTableBorders? Borders(XElement? element) => element is null ? null : new WordTableBorders(
        WordPropertyReader.Border(element.Element(W + "top")),
        WordPropertyReader.Border(element.Element(W + "left") ?? element.Element(W + "start")),
        WordPropertyReader.Border(element.Element(W + "bottom")),
        WordPropertyReader.Border(element.Element(W + "right") ?? element.Element(W + "end")),
        WordPropertyReader.Border(element.Element(W + "insideH")),
        WordPropertyReader.Border(element.Element(W + "insideV")));

    private static WordCellMargins? Margins(XElement? element) => element is null ? null : new WordCellMargins(
        Twips(Attr(element.Element(W + "top"), "w")),
        Twips(Attr(element.Element(W + "left") ?? element.Element(W + "start"), "w")),
        Twips(Attr(element.Element(W + "bottom"), "w")),
        Twips(Attr(element.Element(W + "right") ?? element.Element(W + "end"), "w")));

    // Either explicit attributes or the legacy hexadecimal bit mask.
    private static WordTableLook? Look(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var legacy = Attr(element, "firstRow") is null && Attr(element, "val") is { } hex
            && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed) ? parsed : (int?)null;
        var look = WordTableLook.None;
        foreach (var (flag, attribute, mask) in WordValues.LookBits)
        {
            if (legacy is { } bits ? (bits & mask) != 0 : IsOn(element, attribute))
            {
                look |= flag;
            }
        }

        return look;
    }

    private static bool IsOn(XElement element, string name) => Attr(element, name) is "1" or "true" or "on";

    public static WordTableRegion? Region(string? type) => WordValues.Find(WordValues.Regions, type);

    // Without section properties the page is A4 (the documents this package serves); section properties without a page
    // size or margins mean what the standard and Word take then: US Letter, one inch margins.
    public static WordPageSetup Page(XElement? sectPr)
    {
        var page = sectPr is null ? WordPageSetup.A4 : WordPageSetup.Letter;
        if (sectPr is null)
        {
            return page;
        }

        var size = sectPr.Element(W + "pgSz");
        var margins = sectPr.Element(W + "pgMar");
        var columns = sectPr.Element(W + "cols");
        var numbers = sectPr.Element(W + "pgNumType");
        return page with
        {
            Width = Twips(Attr(size, "w")) ?? page.Width,
            Height = Twips(Attr(size, "h")) ?? page.Height,
            Landscape = Attr(size, "orient") == "landscape",
            MarginTop = Math.Abs(Twips(Attr(margins, "top")) ?? page.MarginTop),
            MarginBottom = Math.Abs(Twips(Attr(margins, "bottom")) ?? page.MarginBottom),
            MarginLeft = Twips(Attr(margins, "left")) ?? page.MarginLeft,
            MarginRight = Twips(Attr(margins, "right")) ?? page.MarginRight,
            HeaderDistance = Twips(Attr(margins, "header")) ?? page.HeaderDistance,
            FooterDistance = Twips(Attr(margins, "footer")) ?? page.FooterDistance,
            Gutter = Twips(Attr(margins, "gutter")) ?? 0,
            Columns = Math.Max(1, Int(Attr(columns, "num")) ?? 1),
            ColumnSpacing = Twips(Attr(columns, "space")) ?? page.ColumnSpacing,
            ColumnSeparator = Attr(columns, "sep") is "1" or "true" or "on",
            ColumnWidths = ColumnWidths(columns),
            ColumnSpacings = ColumnSpacings(columns, Twips(Attr(columns, "space")) ?? page.ColumnSpacing),
            TitlePage = OnOff(sectPr, "titlePg") ?? false,
            Start = SectionStart(Val(sectPr, "type")),
            PageNumberStart = Int(Attr(numbers, "start")),
            PageNumberFormat = NumberFormat(Attr(numbers, "fmt")) ?? WordNumberFormat.Decimal,
            FootnoteNumbering = NoteNumbering(sectPr.Element(W + "footnotePr")),
        };
    }

    private static WordNoteNumbering? NoteNumbering(XElement? properties)
    {
        if (properties is null)
        {
            return null;
        }

        var numbering = new WordNoteNumbering
        {
            Format = NumberFormat(Val(properties, "numFmt")),
            Start = Int(Val(properties, "numStart")),
            Restart = NoteRestart(Val(properties, "numRestart")),
        };
        return numbering == new WordNoteNumbering() ? null : numbering;
    }

    /// <summary>The <c>w:numRestart</c> value of note properties, or null when absent.</summary>
    public static WordNoteRestart? NoteRestart(string? value) => value switch
    {
        null => null,
        "eachSect" => WordNoteRestart.EachSection,
        "eachPage" => WordNoteRestart.EachPage,
        _ => WordNoteRestart.Continuous,
    };

    private static List<double>? ColumnWidths(XElement? columns)
    {
        if (columns is null || Attr(columns, "equalWidth") is not ("0" or "false" or "off"))
        {
            return null;
        }

        var widths = columns.Elements(W + "col").Select(c => Twips(Attr(c, "w")) ?? 0).ToList();
        return widths.Count > 0 ? widths : null;
    }

    // The space after each unequal column, when one of them gives its own.
    private static List<double>? ColumnSpacings(XElement? columns, double spacing)
    {
        if (ColumnWidths(columns) is null || !columns!.Elements(W + "col").Any(c => Attr(c, "space") is not null))
        {
            return null;
        }

        return columns.Elements(W + "col").Select(c => Twips(Attr(c, "space")) ?? spacing).ToList();
    }

    private static WordSectionStart SectionStart(string? value) => value switch
    {
        "continuous" => WordSectionStart.Continuous,
        "evenPage" => WordSectionStart.EvenPage,
        "oddPage" => WordSectionStart.OddPage,
        "nextColumn" => WordSectionStart.NextColumn,
        _ => WordSectionStart.NextPage,
    };

    public static WordNumberFormat? NumberFormat(string? value) =>
        value is null ? null : WordValues.Find(WordValues.NumberFormats, value) ?? WordNumberFormat.Decimal;
}
