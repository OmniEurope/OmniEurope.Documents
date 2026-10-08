// SPDX-License-Identifier: EUPL-1.2
using System.IO.Compression;
using System.Text;

namespace OmniEurope.Documents.Tests.Excel;

/// <summary>
/// A workbook written part by part (ECMA-376 part 1) holding what the package's own writer never produces: a
/// chart and a picture in a drawing, conditional formats, data validations, comments, a table, a pivot cache, a
/// calculation chain and shared strings.
/// </summary>
internal static class XlsxFactory
{
    public const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string Pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string Ml = "application/vnd.openxmlformats-officedocument.spreadsheetml.";

    /// <summary>
    /// Sheet "Données": A1 "Titre" (shared), B1 10, C1 =B1*2 (cached 20), A3 TRUE; column D formatted with style 1;
    /// a table over E1:F3. Sheet "Autre": A1 "x".
    /// </summary>
    public static byte[] Rich(string sheetData = DefaultSheetData, bool date1904 = false, string formulas = "")
    {
        var parts = new Dictionary<string, string>
        {
            ["[Content_Types].xml"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Default Extension="png" ContentType="image/png"/><Default Extension="vml" ContentType="application/vnd.openxmlformats-officedocument.vmlDrawing"/><Override PartName="/xl/workbook.xml" ContentType="{Ml}sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="{Ml}worksheet+xml"/><Override PartName="/xl/worksheets/sheet2.xml" ContentType="{Ml}worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="{Ml}styles+xml"/><Override PartName="/xl/sharedStrings.xml" ContentType="{Ml}sharedStrings+xml"/><Override PartName="/xl/calcChain.xml" ContentType="{Ml}calcChain+xml"/><Override PartName="/xl/drawings/drawing1.xml" ContentType="application/vnd.openxmlformats-officedocument.drawing+xml"/><Override PartName="/xl/charts/chart1.xml" ContentType="application/vnd.openxmlformats-officedocument.drawingml.chart+xml"/><Override PartName="/xl/comments1.xml" ContentType="{Ml}comments+xml"/><Override PartName="/xl/tables/table1.xml" ContentType="{Ml}table+xml"/><Override PartName="/xl/pivotCache/pivotCacheDefinition1.xml" ContentType="{Ml}pivotCacheDefinition+xml"/></Types>""",
            ["_rels/.rels"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="{Pkg}"><Relationship Id="rId1" Type="{Rel}/officeDocument" Target="xl/workbook.xml"/></Relationships>""",
            ["xl/workbook.xml"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="{Main}" xmlns:r="{Rel}"><workbookPr{(date1904 ? " date1904=\"1\"" : string.Empty)}/><bookViews><workbookView/></bookViews><sheets><sheet name="Données" sheetId="1" r:id="rId1"/><sheet name="Autre" sheetId="2" r:id="rId2"/></sheets><pivotCaches><pivotCache cacheId="1" r:id="rId6"/></pivotCaches></workbook>""",
            ["xl/_rels/workbook.xml.rels"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="{Pkg}"><Relationship Id="rId1" Type="{Rel}/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="{Rel}/worksheet" Target="worksheets/sheet2.xml"/><Relationship Id="rId3" Type="{Rel}/styles" Target="styles.xml"/><Relationship Id="rId4" Type="{Rel}/sharedStrings" Target="sharedStrings.xml"/><Relationship Id="rId5" Type="{Rel}/calcChain" Target="calcChain.xml"/><Relationship Id="rId6" Type="{Rel}/pivotCacheDefinition" Target="pivotCache/pivotCacheDefinition1.xml"/></Relationships>""",
            ["xl/worksheets/sheet1.xml"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="{Main}" xmlns:r="{Rel}"><dimension ref="A1:F3"/><cols><col min="4" max="4" width="12" style="1" customWidth="1"/></cols><sheetData>{sheetData}</sheetData><conditionalFormatting sqref="B1:B3"><cfRule type="cellIs" dxfId="0" priority="1" operator="greaterThan"><formula>5</formula></cfRule></conditionalFormatting><dataValidations count="1"><dataValidation type="whole" sqref="B1"><formula1>0</formula1><formula2>100</formula2></dataValidation></dataValidations><drawing r:id="rId1"/><legacyDrawing r:id="rId3"/><tableParts count="1"><tablePart r:id="rId4"/></tableParts></worksheet>""",
            ["xl/worksheets/_rels/sheet1.xml.rels"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="{Pkg}"><Relationship Id="rId1" Type="{Rel}/drawing" Target="../drawings/drawing1.xml"/><Relationship Id="rId2" Type="{Rel}/comments" Target="../comments1.xml"/><Relationship Id="rId3" Type="{Rel}/vmlDrawing" Target="../drawings/vmlDrawing1.vml"/><Relationship Id="rId4" Type="{Rel}/table" Target="../tables/table1.xml"/></Relationships>""",
            ["xl/worksheets/sheet2.xml"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="{Main}"><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>x</t></is></c></row></sheetData></worksheet>""",
            ["xl/styles.xml"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="{Main}"><numFmts count="1"><numFmt numFmtId="164" formatCode="0.00"/></numFmts><fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border/></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="3"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="14" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs><dxfs count="1"><dxf><font><b/></font></dxf></dxfs></styleSheet>""",
            ["xl/sharedStrings.xml"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><sst xmlns="{Main}" count="3" uniqueCount="3"><si><t>Titre</t></si><si><r><t>Nom</t></r><r><rPr><b/></rPr><t xml:space="preserve"> complet</t></r><rPh sb="0" eb="1"><t>ignoré</t></rPh></si><si><t>Montant</t></si></sst>""",
            ["xl/calcChain.xml"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><calcChain xmlns="{Main}"><c r="C1" i="1"/>{formulas}</calcChain>""",
            ["xl/drawings/drawing1.xml"] = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><xdr:wsDr xmlns:xdr="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><xdr:twoCellAnchor><xdr:from><xdr:col>7</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>1</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from><xdr:to><xdr:col>12</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>15</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:to><xdr:graphicFrame macro=""><xdr:nvGraphicFramePr><xdr:cNvPr id="2" name="Graphique 1"/><xdr:cNvGraphicFramePr/></xdr:nvGraphicFramePr><xdr:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/></xdr:xfrm><a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/chart"><c:chart xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" r:id="rId1"/></a:graphicData></a:graphic></xdr:graphicFrame><xdr:clientData/></xdr:twoCellAnchor></xdr:wsDr>""",
            ["xl/drawings/_rels/drawing1.xml.rels"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="{Pkg}"><Relationship Id="rId1" Type="{Rel}/chart" Target="../charts/chart1.xml"/><Relationship Id="rId2" Type="{Rel}/image" Target="../media/image1.png"/></Relationships>""",
            ["xl/charts/chart1.xml"] = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><c:chartSpace xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart"><c:chart><c:plotArea><c:barChart><c:barDir val="col"/><c:ser><c:idx val="0"/><c:order val="0"/><c:val><c:numRef><c:f>'Données'!$B$1:$B$3</c:f></c:numRef></c:val></c:ser></c:barChart></c:plotArea></c:chart></c:chartSpace>""",
            ["xl/comments1.xml"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><comments xmlns="{Main}"><authors><author>Relecteur</author></authors><commentList><comment ref="B1" authorId="0"><text><t>À vérifier</t></text></comment></commentList></comments>""",
            ["xl/drawings/vmlDrawing1.vml"] = """<xml xmlns:v="urn:schemas-microsoft-com:vml"><v:shape id="_x0000_s1025" type="#_x0000_t202"/></xml>""",
            ["xl/tables/table1.xml"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><table xmlns="{Main}" id="1" name="Tableau1" displayName="Tableau1" ref="E1:F3"><autoFilter ref="E1:F3"/><tableColumns count="2"><tableColumn id="1" name="Nom"/><tableColumn id="2" name="Montant"/></tableColumns></table>""",
            ["xl/pivotCache/pivotCacheDefinition1.xml"] = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><pivotCacheDefinition xmlns="{Main}" refreshOnLoad="1"><cacheSource type="worksheet"><worksheetSource ref="E1:F3" sheet="Données"/></cacheSource><cacheFields count="1"><cacheField name="Nom" numFmtId="0"/></cacheFields></pivotCacheDefinition>""",
        };
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in parts)
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(Encoding.UTF8.GetBytes(content));
            }

            using var image = zip.CreateEntry("xl/media/image1.png").Open();
            image.Write(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "rgb.png")));
        }

        return stream.ToArray();
    }

    public const string DefaultSheetData =
        """<row r="1" spans="1:6"><c r="A1" t="s"><v>0</v></c><c r="B1"><v>10</v></c><c r="C1"><f>B1*2</f><v>20</v></c><c r="E1" t="s"><v>1</v></c><c r="F1" t="s"><v>2</v></c></row>"""
        + """<row r="3" spans="1:6"><c r="A3" t="b"><v>1</v></c><c r="E3" t="inlineStr"><is><t>Durand</t></is></c><c r="F3"><v>5</v></c></row>""";
}
