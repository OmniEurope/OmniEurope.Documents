// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.IO.Compression;
using System.Text;
using OmniEurope.Documents.Tests.Word;

namespace OmniEurope.Documents.Tests.Charts;

/// <summary>
/// Chart parts written by hand after ECMA-376 part 1, §21.2 (element order of the dml-chart schema), and the
/// documents and workbooks holding them: a Word body with an inline or anchored chart frame, a workbook whose sheet
/// drawing anchors the chart over cells.
/// </summary>
internal static class ChartFactory
{
    public const string C = "http://schemas.openxmlformats.org/drawingml/2006/chart";
    public const string A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    public const string R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string Pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string ChartType = "application/vnd.openxmlformats-officedocument.drawingml.chart+xml";

    /// <summary>A solid fill, as a series' or chart area's shape properties.</summary>
    public static string Fill(string color) => $"<c:spPr><a:solidFill><a:srgbClr val=\"{color}\"/></a:solidFill></c:spPr>";

    /// <summary>A line colour, as a line series' shape properties.</summary>
    public static string Line(string color) => $"<c:spPr><a:ln w=\"28575\"><a:solidFill><a:srgbClr val=\"{color}\"/></a:solidFill></a:ln></c:spPr>";

    /// <summary>
    /// A series: index, cached name, shape properties, data points, labels, cached categories and values (or only
    /// their formulas when <paramref name="cached"/> is false). <paramref name="beforeCategories"/> goes where the
    /// series type wants its marker, points and labels.
    /// </summary>
    public static string Series(int index, string? name, string[] categories, double?[] values, string properties = "", string beforeCategories = "", bool cached = true, string sheet = "Feuil1")
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"<c:ser><c:idx val=\"{index}\"/><c:order val=\"{index}\"/>");
        var column = (char)('B' + index);
        if (name is not null)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<c:tx><c:strRef><c:f>{sheet}!${column}$1</c:f>");
            builder.Append(cached ? $"<c:strCache><c:ptCount val=\"1\"/><c:pt idx=\"0\"><c:v>{name}</c:v></c:pt></c:strCache>" : string.Empty);
            builder.Append("</c:strRef></c:tx>");
        }

        builder.Append(properties).Append(beforeCategories);
        var last = categories.Length + 1;
        builder.Append(CultureInfo.InvariantCulture, $"<c:cat><c:strRef><c:f>{sheet}!$A$2:$A${last}</c:f>");
        if (cached)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<c:strCache><c:ptCount val=\"{categories.Length}\"/>");
            for (var i = 0; i < categories.Length; i++)
            {
                builder.Append(CultureInfo.InvariantCulture, $"<c:pt idx=\"{i}\"><c:v>{categories[i]}</c:v></c:pt>");
            }

            builder.Append("</c:strCache>");
        }

        builder.Append(CultureInfo.InvariantCulture, $"</c:strRef></c:cat><c:val><c:numRef><c:f>{sheet}!${column}$2:${column}${last}</c:f>");
        if (cached)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<c:numCache><c:formatCode>General</c:formatCode><c:ptCount val=\"{values.Length}\"/>");
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] is { } value)
                {
                    builder.Append(CultureInfo.InvariantCulture, $"<c:pt idx=\"{i}\"><c:v>{value.ToString(CultureInfo.InvariantCulture)}</c:v></c:pt>");
                }
            }

            builder.Append("</c:numCache>");
        }

        return builder.Append("</c:numRef></c:val></c:ser>").ToString();
    }

    /// <summary>Data labels showing values and/or percentages (the six flags are required in this order).</summary>
    public static string Labels(bool values, bool percentages = false) =>
        $"<c:dLbls><c:showLegendKey val=\"0\"/><c:showVal val=\"{(values ? 1 : 0)}\"/><c:showCatName val=\"0\"/><c:showSerName val=\"0\"/><c:showPercent val=\"{(percentages ? 1 : 0)}\"/><c:showBubbleSize val=\"0\"/></c:dLbls>";

    public static string Bar(string series, string direction = "col", string grouping = "clustered", string labels = "") =>
        $"<c:barChart><c:barDir val=\"{direction}\"/><c:grouping val=\"{grouping}\"/><c:varyColors val=\"0\"/>{series}{labels}<c:gapWidth val=\"150\"/>{(grouping == "clustered" ? string.Empty : "<c:overlap val=\"100\"/>")}<c:axId val=\"1\"/><c:axId val=\"2\"/></c:barChart>";

    public static string LineChart(string series, string grouping = "standard", string labels = "") =>
        $"<c:lineChart><c:grouping val=\"{grouping}\"/><c:varyColors val=\"0\"/>{series}{labels}<c:marker val=\"1\"/><c:axId val=\"1\"/><c:axId val=\"2\"/></c:lineChart>";

    public static string Pie(string series, string labels = "", int firstSlice = 0) =>
        $"<c:pieChart><c:varyColors val=\"1\"/>{series}{labels}<c:firstSliceAng val=\"{firstSlice}\"/></c:pieChart>";

    public static string Doughnut(string series, int hole = 50, string labels = "") =>
        $"<c:doughnutChart><c:varyColors val=\"1\"/>{series}{labels}<c:firstSliceAng val=\"0\"/><c:holeSize val=\"{hole}\"/></c:doughnutChart>";

    /// <summary>A category and a value axis (ids 1 and 2), with optional titles, gridlines and reversed categories.</summary>
    public static string Axes(string? categoryTitle = null, string? valueTitle = null, bool gridlines = false, bool reverse = false, string categoryPosition = "b", string valuePosition = "l") =>
        $"<c:catAx><c:axId val=\"1\"/><c:scaling><c:orientation val=\"{(reverse ? "maxMin" : "minMax")}\"/></c:scaling><c:delete val=\"0\"/><c:axPos val=\"{categoryPosition}\"/>{Title(categoryTitle)}<c:crossAx val=\"2\"/></c:catAx>"
        + $"<c:valAx><c:axId val=\"2\"/><c:scaling><c:orientation val=\"minMax\"/></c:scaling><c:delete val=\"0\"/><c:axPos val=\"{valuePosition}\"/>{(gridlines ? "<c:majorGridlines/>" : string.Empty)}{Title(valueTitle)}<c:crossAx val=\"1\"/></c:valAx>";

    /// <summary>A title in rich text, or nothing.</summary>
    public static string Title(string? text) =>
        text is null ? string.Empty : $"<c:title><c:tx><c:rich><a:bodyPr/><a:p><a:r><a:t>{text}</a:t></a:r></a:p></c:rich></c:tx><c:overlay val=\"0\"/></c:title>";

    /// <summary>A chart part: title, plot area (types then axes), legend and the chart area's shape properties.</summary>
    public static string ChartSpace(string plot, string axes = "", string? title = null, string? legend = null, string area = "", string afterTitle = "<c:autoTitleDeleted val=\"1\"/>") =>
        $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><c:chartSpace xmlns:c="{C}" xmlns:a="{A}" xmlns:r="{R}"><c:chart>{Title(title)}{afterTitle}<c:plotArea><c:layout/>{plot}{axes}</c:plotArea>{(legend is null ? string.Empty : $"<c:legend><c:legendPos val=\"{legend}\"/><c:overlay val=\"0\"/></c:legend>")}<c:plotVisOnly val="1"/></c:chart>{area}</c:chartSpace>""";

    /// <summary>A Word document whose only paragraph holds the chart, inline or anchored to the page at the given
    /// point; sizes in points.</summary>
    public static byte[] Docx(string chartPart, double width = 300, double height = 200, (double X, double Y)? page = null, string? theme = null, string uri = C)
    {
        var (cx, cy) = (Emu(width), Emu(height));
        var graphic = $"""<a:graphic xmlns:a="{A}"><a:graphicData uri="{uri}"><c:chart xmlns:c="{C}" r:id="rId1"/></a:graphicData></a:graphic>""";
        var frame = page is { } at
            ? $"""<wp:anchor distT="0" distB="0" distL="0" distR="0" simplePos="0" relativeHeight="1" behindDoc="0" locked="0" layoutInCell="1" allowOverlap="1"><wp:simplePos x="0" y="0"/><wp:positionH relativeFrom="page"><wp:posOffset>{Emu(at.X)}</wp:posOffset></wp:positionH><wp:positionV relativeFrom="page"><wp:posOffset>{Emu(at.Y)}</wp:posOffset></wp:positionV><wp:extent cx="{cx}" cy="{cy}"/><wp:effectExtent l="0" t="0" r="0" b="0"/><wp:wrapNone/><wp:docPr id="1" name="Graphique 1"/><wp:cNvGraphicFramePr/>{graphic}</wp:anchor>"""
            : $"""<wp:inline distT="0" distB="0" distL="0" distR="0"><wp:extent cx="{cx}" cy="{cy}"/><wp:effectExtent l="0" t="0" r="0" b="0"/><wp:docPr id="1" name="Graphique 1"/><wp:cNvGraphicFramePr/>{graphic}</wp:inline>""";
        var body = $"""<w:p><w:r><w:drawing xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing">{frame}</w:drawing></w:r></w:p>""";
        var parts = new Dictionary<string, string> { ["word/charts/chart1.xml"] = chartPart };
        var relationships = $"""<Relationship Id="rId1" Type="{R}/chart" Target="charts/chart1.xml"/>""";
        var types = new Dictionary<string, string> { ["word/charts/chart1.xml"] = ChartType };
        if (theme is not null)
        {
            parts["word/theme/theme1.xml"] = Theme(theme);
            relationships += $"""<Relationship Id="rId2" Type="{R}/theme" Target="theme/theme1.xml"/>""";
            types["word/theme/theme1.xml"] = "application/vnd.openxmlformats-officedocument.theme+xml";
        }

        return DocxFactory.Build(body, extraParts: parts, documentRelationships: relationships, contentTypes: types);
    }

    /// <summary>A theme whose first accent is <paramref name="accent1"/>, the others those of the Office theme.</summary>
    public static string Theme(string accent1) =>
        $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><a:theme xmlns:a="{A}" name="Test"><a:themeElements><a:clrScheme name="Test"><a:dk1><a:sysClr val="windowText" lastClr="000000"/></a:dk1><a:lt1><a:sysClr val="window" lastClr="FFFFFF"/></a:lt1><a:dk2><a:srgbClr val="44546A"/></a:dk2><a:lt2><a:srgbClr val="E7E6E6"/></a:lt2><a:accent1><a:srgbClr val="{accent1}"/></a:accent1><a:accent2><a:srgbClr val="ED7D31"/></a:accent2><a:accent3><a:srgbClr val="A5A5A5"/></a:accent3><a:accent4><a:srgbClr val="FFC000"/></a:accent4><a:accent5><a:srgbClr val="5B9BD5"/></a:accent5><a:accent6><a:srgbClr val="70AD47"/></a:accent6><a:hlink><a:srgbClr val="0563C1"/></a:hlink><a:folHlink><a:srgbClr val="954F72"/></a:folHlink></a:clrScheme><a:fontScheme name="Test"><a:majorFont><a:latin typeface="Calibri Light"/><a:ea typeface=""/><a:cs typeface=""/></a:majorFont><a:minorFont><a:latin typeface="Calibri"/><a:ea typeface=""/><a:cs typeface=""/></a:minorFont></a:fontScheme><a:fmtScheme name="Test"><a:fillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:fillStyleLst><a:lnStyleLst><a:ln><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln><a:ln><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln><a:ln><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln></a:lnStyleLst><a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst><a:bgFillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:bgFillStyleLst></a:fmtScheme></a:themeElements></a:theme>""";

    /// <summary>
    /// A workbook: sheet "Feuil1" holding <paramref name="sheetData"/> (rows of <c>c</c> elements) and a drawing whose
    /// anchor (<paramref name="anchor"/>, a full <c>xdr:*Anchor</c> without its graphic frame) holds the chart;
    /// <paramref name="columns"/> goes before <c>sheetData</c> and <paramref name="afterData"/> (such as
    /// <c>mergeCells</c>) right after it.
    /// </summary>
    public static byte[] Xlsx(string sheetData, string chartPart, string anchor, string? theme = null, string columns = "", string afterData = "")
    {
        var ml = "application/vnd.openxmlformats-officedocument.spreadsheetml.";
        var main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var frame = $"""<xdr:graphicFrame macro=""><xdr:nvGraphicFramePr><xdr:cNvPr id="2" name="Graphique 1"/><xdr:cNvGraphicFramePr/></xdr:nvGraphicFramePr><xdr:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/></xdr:xfrm><a:graphic><a:graphicData uri="{C}"><c:chart xmlns:c="{C}" r:id="rId1"/></a:graphicData></a:graphic></xdr:graphicFrame><xdr:clientData/>""";
        var parts = new Dictionary<string, string>
        {
            ["[Content_Types].xml"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="{ml}sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="{ml}worksheet+xml"/><Override PartName="/xl/drawings/drawing1.xml" ContentType="application/vnd.openxmlformats-officedocument.drawing+xml"/><Override PartName="/xl/charts/chart1.xml" ContentType="{ChartType}"/><Override PartName="/xl/theme/theme1.xml" ContentType="application/vnd.openxmlformats-officedocument.theme+xml"/></Types>""",
            ["_rels/.rels"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="{Pkg}"><Relationship Id="rId1" Type="{R}/officeDocument" Target="xl/workbook.xml"/></Relationships>""",
            ["xl/workbook.xml"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="{main}" xmlns:r="{R}"><sheets><sheet name="Feuil1" sheetId="1" r:id="rId1"/></sheets></workbook>""",
            ["xl/_rels/workbook.xml.rels"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="{Pkg}"><Relationship Id="rId1" Type="{R}/worksheet" Target="worksheets/sheet1.xml"/>{(theme is null ? string.Empty : $"<Relationship Id=\"rId2\" Type=\"{R}/theme\" Target=\"theme/theme1.xml\"/>")}</Relationships>""",
            ["xl/worksheets/sheet1.xml"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="{main}" xmlns:r="{R}">{columns}<sheetData>{sheetData}</sheetData>{afterData}<drawing r:id="rId1"/></worksheet>""",
            ["xl/worksheets/_rels/sheet1.xml.rels"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="{Pkg}"><Relationship Id="rId1" Type="{R}/drawing" Target="../drawings/drawing1.xml"/></Relationships>""",
            ["xl/drawings/drawing1.xml"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><xdr:wsDr xmlns:xdr="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing" xmlns:a="{A}" xmlns:r="{R}">{anchor.Replace("{frame}", frame, StringComparison.Ordinal)}</xdr:wsDr>""",
            ["xl/drawings/_rels/drawing1.xml.rels"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="{Pkg}"><Relationship Id="rId1" Type="{R}/chart" Target="../charts/chart1.xml"/></Relationships>""",
            ["xl/charts/chart1.xml"] = chartPart,
        };
        if (theme is not null)
        {
            parts["xl/theme/theme1.xml"] = Theme(theme);
        }

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in parts)
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        return stream.ToArray();
    }

    /// <summary>A two-cell anchor from (0-based) column and row to column and row, offsets in EMU.</summary>
    public static string TwoCell(int fromColumn, int fromRow, int toColumn, int toRow, long fromColumnOffset = 0, long fromRowOffset = 0) =>
        $"<xdr:twoCellAnchor><xdr:from><xdr:col>{fromColumn}</xdr:col><xdr:colOff>{fromColumnOffset}</xdr:colOff><xdr:row>{fromRow}</xdr:row><xdr:rowOff>{fromRowOffset}</xdr:rowOff></xdr:from><xdr:to><xdr:col>{toColumn}</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>{toRow}</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:to>{{frame}}</xdr:twoCellAnchor>";

    public static long Emu(double points) => (long)Math.Round(points * 12700);
}
