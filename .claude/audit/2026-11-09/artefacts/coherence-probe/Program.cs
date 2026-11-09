using System.Reflection;
using System.IO.Compression;
using System.Text.Json;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Csv;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Excel.Editing;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Html;
using OmniEurope.Documents.Markdown;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Editing;
using OmniEurope.Documents.Pdf.Forms;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Pdf.Writing;
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Editing;

static object Attempt(Action action) { try { action(); return new { Result = "success", Exception = "", ParamName = "" }; } catch(Exception ex) { return new { Result = "failure", Exception = ex.GetType().Name, ParamName = (ex as ArgumentException)?.ParamName ?? "" }; } }
var root = AppContext.BaseDirectory;
var output = Path.GetFullPath(Path.Combine(root,"../../../"));
var results = new Dictionary<string,object>();
var workbook = new XlsxWorkbook();
workbook.AddWorksheet("Sheet1").Cell(1,1).Value = "Cell";
workbook.Gaps.Add("Caller supplied fidelity warning");
var excelHtml = ExcelToHtml.ConvertWithStyleSheet(workbook);
results["excelHtmlGaps"] = new { SourceGaps=workbook.Gaps, ResultProperties=typeof(ExcelHtmlResult).GetProperties().Select(p=>p.Name).ToArray(), HtmlContainsWarning=excelHtml.Html.Contains(workbook.Gaps[0]), PdfGaps=ExcelToPdf.Convert(workbook).Gaps };
using (var damaged = new MemoryStream()) {
  damaged.Write(workbook.ToArray());
  using(var zip = new ZipArchive(damaged,ZipArchiveMode.Update,leaveOpen:true)) {
    using(var rel=new StreamWriter(zip.CreateEntry("xl/worksheets/_rels/sheet1.xml.rels").Open())) rel.Write("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"drawing1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing\" Target=\"../drawings/drawing1.xml\"/></Relationships>");
    using(var drawing=new StreamWriter(zip.CreateEntry("xl/drawings/drawing1.xml").Open())) drawing.Write("<broken");
  }
  var bytes=damaged.ToArray();
  File.WriteAllBytes(Path.Combine(output,"unreadable-drawing.xlsx"),bytes);
  var loaded=XlsxWorkbook.Load(bytes);
  var converted=ExcelToHtml.ConvertWithStyleSheet(bytes);
  results["loadedExcelGaps"] = new { SourceGaps=loaded.Gaps,SourceGapCodes=loaded.GapDetails.Select(g=>g.Code).ToArray(),HtmlResultProperties=converted.GetType().GetProperties().Select(p=>p.Name).ToArray(),PdfGaps=ExcelToPdf.Convert(bytes).Gaps,HtmlContainsSourceWarning=loaded.Gaps.Any(converted.Html.Contains) };
}
var builder = new PdfDocumentBuilder();
using (var chartBytes = new MemoryStream()) {
  chartBytes.Write(workbook.ToArray());
  using(var zip = new ZipArchive(chartBytes,ZipArchiveMode.Update,leaveOpen:true)) {
    void Put(string path,string text) { using var writer=new StreamWriter(zip.CreateEntry(path).Open());writer.Write(text); }
    Put("xl/worksheets/_rels/sheet1.xml.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"d\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing\" Target=\"../drawings/drawing1.xml\"/></Relationships>");
    Put("xl/drawings/drawing1.xml","<xdr:wsDr xmlns:xdr=\"http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing\" xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><xdr:oneCellAnchor><xdr:from><xdr:col>0</xdr:col><xdr:row>0</xdr:row></xdr:from><xdr:ext cx=\"3000000\" cy=\"2000000\"/><xdr:graphicFrame><c:chart r:id=\"c\"/></xdr:graphicFrame></xdr:oneCellAnchor></xdr:wsDr>");
    Put("xl/drawings/_rels/drawing1.xml.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"c\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart\" Target=\"../charts/chart1.xml\"/></Relationships>");
    Put("xl/charts/chart1.xml","<c:chartSpace xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\"><c:chart><c:plotArea><c:barChart><c:ser><c:val><c:numLit><c:formatCode>General</c:formatCode><c:pt idx=\"0\"><c:v>-1E308</c:v></c:pt><c:pt idx=\"1\"><c:v>1E308</c:v></c:pt></c:numLit></c:val></c:ser></c:barChart></c:plotArea></c:chart></c:chartSpace>");
  }
  var bytes=chartBytes.ToArray();File.WriteAllBytes(Path.Combine(output,"finite-extreme-chart.xlsx"),bytes);
  var loaded=XlsxWorkbook.Load(bytes);
  var convertedChart=ExcelToPdf.Convert(bytes);
  File.WriteAllBytes(Path.Combine(output,"finite-extreme-chart.pdf"),convertedChart.Pdf);
  results["finiteChart"] = new {Charts=loaded.Worksheets[0].Charts.Count,Values=loaded.Worksheets[0].Charts.Single().Chart.Series.Single().Values,Conversion=Attempt(()=>ExcelToPdf.Convert(bytes)),PdfText=string.Join(" | ",PdfDocument.Open(convertedChart.Pdf).Pages.Select(p=>p.Text)),convertedChart.Gaps};
}
builder.AddPage(72,72);
var pdf = PdfDocument.Open(builder.ToArray());
byte[] MinimalContentPdf(bool badFilter,string? replacement=null) {
  var content=replacement ?? "BT /F1 12 Tf 10 30 Td (kept) Tj ET";
  var parts=new[]{"<</Type /Catalog /Pages 2 0 R>>","<</Type /Pages /Kids [3 0 R] /Count 1>>","<</Type /Page /Parent 2 0 R /MediaBox [0 0 72 72] /Resources <</Font <</F1 5 0 R>>>> /Contents 4 0 R>>",$"<</Length {content.Length}"+(badFilter?" /Filter /FlateDecode":"")+">>\nstream\n"+content+"\nendstream","<</Type /Font /Subtype /Type1 /BaseFont /Helvetica>>"};
  var text=new System.Text.StringBuilder("%PDF-1.7\n");var offsets=new List<int>{0};
  for(var i=0;i<parts.Length;i++){offsets.Add(text.Length);text.Append(i+1).Append(" 0 obj\n").Append(parts[i]).Append("\nendobj\n");}
  var xref=text.Length;text.Append("xref\n0 6\n0000000000 65535 f \n");foreach(var at in offsets.Skip(1))text.Append(at.ToString("0000000000",System.Globalization.CultureInfo.InvariantCulture)).Append(" 00000 n \n");text.Append("trailer\n<</Size 6 /Root 1 0 R>>\nstartxref\n").Append(xref).Append("\n%%EOF\n");return System.Text.Encoding.ASCII.GetBytes(text.ToString());
}
foreach(var damaged in new[]{false,true}){var raw=MinimalContentPdf(damaged);File.WriteAllBytes(Path.Combine(output,damaged?"unreadable-content.pdf":"valid-content.pdf"),raw);var d=PdfDocument.Open(raw);var imageResult=PdfRenderer.Render(d.GetPage(1),new PdfRenderOptions{Dpi=72});results[damaged?"unreadablePdfContent":"validPdfContent"]=new {d.WasRepaired,Text=d.GetPage(1).Text,imageResult.Gaps,imageResult.GapDetails,NonWhite=imageResult.Image.Pixels.Count(b=>b<250)};}
var nestedContent=MinimalContentPdf(false,new string('[',205)+"0"+new string(']',205)+" BT /F1 12 Tf 10 30 Td (kept) Tj ET");File.WriteAllBytes(Path.Combine(output,"nested-content.pdf"),nestedContent);var nestedDocument=PdfDocument.Open(nestedContent);results["nestedPdfContent"]=new {nestedDocument.WasRepaired,Render=Attempt(()=>PdfRenderer.Render(nestedDocument.GetPage(1))),Text=Attempt(()=>_ = nestedDocument.GetPage(1).Text)};
var datedBuilder=new PdfDocumentBuilder {CreationDate=new DateTimeOffset(2020,1,2,3,4,5,TimeSpan.Zero)};datedBuilder.AddPage(72,72);var dated=PdfDocument.Open(datedBuilder.ToArray());
results["pdfMetadataDates"] = new {Source=dated.Information.CreationDate,Rotated=PdfDocument.Open(PdfEditor.RotatePages(dated,[1],90)).Information.CreationDate,Compressed=PdfDocument.Open(PdfCompressor.Compress(dated)).Information.CreationDate};
byte[] InheritedFieldPdf(bool inherited) {
  var parts=new[]{"<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [4 0 R] /DA (/Helv 12 Tf 0 g)>>>>","<</Type /Pages /Kids [3 0 R] /Count 1>>","<</Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Annots [5 0 R]>>","<</T (Parent) /FT /Tx /Kids [5 0 R]"+(inherited?" /MaxLen 2":"")+">>","<</T (Child) /Parent 4 0 R /Type /Annot /Subtype /Widget /Rect [10 10 90 40]"+(!inherited?" /MaxLen 2":"")+">>"};
  var text=new System.Text.StringBuilder("%PDF-1.7\n");var offsets=new List<int>();
  for(var i=0;i<parts.Length;i++){offsets.Add(text.Length);text.Append(i+1).Append(" 0 obj\n").Append(parts[i]).Append("\nendobj\n");}
  var xref=text.Length;text.Append("xref\n0 6\n0000000000 65535 f \n");foreach(var at in offsets)text.Append(at.ToString("0000000000",System.Globalization.CultureInfo.InvariantCulture)).Append(" 00000 n \n");text.Append("trailer\n<</Size 6 /Root 1 0 R>>\nstartxref\n").Append(xref).Append("\n%%EOF\n");return System.Text.Encoding.ASCII.GetBytes(text.ToString());
}
foreach(var inherited in new[]{false,true}) { var raw=InheritedFieldPdf(inherited);File.WriteAllBytes(Path.Combine(output,inherited?"inherited-maxlen.pdf":"direct-maxlen.pdf"),raw);var fieldDocument=PdfDocument.Open(raw);var fields=PdfForm.Read(fieldDocument);results[inherited?"inheritedMaxLen":"directMaxLen"]=new{fieldDocument.WasRepaired,Fields=fields.Select(f=>new{f.Name,f.MaxLength}),Fill=Attempt(()=>PdfForm.Fill(raw,[new PdfFieldValue("Parent.Child","ABC")] ))}; }
foreach (var dpi in new[]{ 96d,2400d,0.5d }) {
  var rendered = PdfRenderer.Render(pdf.GetPage(1),new PdfRenderOptions { Dpi=dpi });
  var png = rendered.ToPng();
  var decoded = PngCodec.Decode(png);
  File.WriteAllBytes(Path.Combine(output,$"render-{dpi.ToString(System.Globalization.CultureInfo.InvariantCulture)}.png"),png);
  results[$"dpi-{dpi}"] = new { RequestedDpi=dpi, rendered.Image.Width, rendered.Image.Height, RasterDpi=rendered.Image.DpiX, DecodedPngDpi=decoded.DpiX, PhysicalWidthPoints=decoded.Width / decoded.DpiX * 72, ReimportedPdfWidth=PdfDocument.Open(ImagesToPdf.Convert([png])).GetPage(1).Width, Compare=Attempt(()=>PdfPageComparison.Compare(pdf,pdf,1,new PdfPageComparisonOptions { Dpi=dpi })) };
}
var doc = new WordDocument();
var pixel = new RasterImage(1,1,ImageColorType.Rgb);
var image = WordImage.FromBytes(PngCodec.Encode(pixel));
doc.AddParagraph().Add(new WordPicture(image,10,10));
var htmlImage = WordToHtml.Convert(doc,new WordHtmlOptions { ImageUrlPrefix="images/" }).Images.Single();
var markdownImage = WordToMarkdown.Convert(doc).Images.Single();
var spacedMarkdown=WordToMarkdown.Convert(doc,new WordMarkdownOptions { ImageFolder="my images" });
results["markdownImageFolder"] = new {Markdown=spacedMarkdown.Markdown,ImagePath=spacedMarkdown.Images.Single().Path,Html=MarkdownRenderer.ToHtml(spacedMarkdown.Markdown,MarkdownOptions.GitHub)};
results["imageMime"] = new { HtmlName=htmlImage.Name, HtmlMediaType=htmlImage.MediaType, MarkdownPath=markdownImage.Path, MarkdownContentType=markdownImage.ContentType, HtmlHasContentType=typeof(WordHtmlImage).GetProperty("ContentType")!=null, MarkdownHasMediaType=typeof(WordMarkdownImage).GetProperty("MediaType")!=null };
var editor = XlsxEditor.Open(workbook.ToArray());
results["sheetMissing"] = new { Model=Attempt(()=>workbook.Worksheet("missing")), Editor=Attempt(()=>editor.Sheet("missing")) };
results["headingLevel0"] = new { WordStyle=doc.AddHeading("Heading",0).StyleId, Markdown=Attempt(()=>new MarkdownWriter().Heading(0,"Heading")) };
foreach(var (name,data,open) in new (string,byte[],Action<Stream>)[]{("word",doc.ToArray(),s=>WordDocument.Load(s)),("excel",workbook.ToArray(),s=>XlsxWorkbook.Load(s)),("pdf",builder.ToArray(),s=>PdfDocument.Open(s)),("wordEditor",doc.ToArray(),s=>WordEditor.Open(s)),("excelEditor",workbook.ToArray(),s=>XlsxEditor.Open(s)) }) { using var stream=new MemoryStream(data); open(stream); results[$"stream-{name}"]=new {stream.CanRead,Position=stream.Position,stream.Length}; }
using(var stream = new MemoryStream("a,b\n1,2\n"u8.ToArray())) { using(var csv=CsvReader.Open(stream)) { csv.Read(); } results["stream-csv-default"]=new {stream.CanRead}; }
using(var stream = new MemoryStream("a,b\n1,2\n"u8.ToArray())) { using(var csv=CsvReader.Open(stream,leaveOpen:true)) { csv.Read(); } results["stream-csv-leaveOpen"]=new {stream.CanRead}; }
results["invalidFormat"]=new {Word=Attempt(()=>WordDocument.Load([1,2,3])),Excel=Attempt(()=>XlsxWorkbook.Load([1,2,3])),Pdf=Attempt(()=>PdfDocument.Open([1,2,3]))};
var selectorTree=HtmlParser.ParseFragment("<div title='a'><span>kept</span></div>");
results["selectorDescendant"] = new { Plain=selectorTree.QuerySelectorAll("div span").Count,Attribute=Attempt(()=>selectorTree.QuerySelectorAll("div[title] span")),Nth=Attempt(()=>selectorTree.QuerySelectorAll("div:nth-child(1) span")),Not=Attempt(()=>selectorTree.QuerySelectorAll("div:not(.missing) span")),ExplicitChild=selectorTree.QuerySelectorAll("div[title]>span").Count };
WordText ReadHtmlRun(string text) { var reopened=WordDocument.Load(HtmlToWord.Convert(text).ToArray());var block=reopened.Blocks.First();return (block is WordTable table ? table.Rows[0].Cells[0].Blocks.OfType<WordParagraph>().First():(WordParagraph)block).Inlines.OfType<WordText>().First(); }
results["htmlFormatting"] = new {LowercaseBold=ReadHtmlRun("<p style='font-weight:bold;font-style:italic'>word</p>").Properties,UppercaseBold=ReadHtmlRun("<p style='font-weight:BOLD;font-style:ITALIC'>word</p>").Properties,TableCell=ReadHtmlRun("<table><tr><td style='color:#ff0000;font-weight:bold;font-size:20pt'>word</td></tr></table>").Properties,Span=ReadHtmlRun("<p><span style='color:#ff0000;font-weight:bold;font-size:20pt'>word</span></p>").Properties,Pre=HtmlToWord.Convert("<pre>a\n<b>b</b>\nc</pre>").Text};
var invalidPictureDoc=new WordDocument();invalidPictureDoc.AddParagraph("kept").Add(new WordPicture(new WordImage([1,2,3],"image/png"),20,20));
var invalidPictureBytes=invalidPictureDoc.ToArray();File.WriteAllBytes(Path.Combine(output,"unreadable-picture.docx"),invalidPictureBytes);
results["invalidWordPicture"] = new {Pdf=Attempt(()=>WordToPdf.Convert(invalidPictureBytes)),HtmlGaps=WordToHtml.Convert(invalidPictureBytes).Gaps};
var symbolFont=OmniEurope.Documents.Fonts.FontLibrary.Default.Resolve("Noto Sans Symbols 2");
var tableBuilder=new PdfDocumentBuilder();var tableFlow=new PdfFlowLayout(tableBuilder,new PdfFlowOptions {PageWidth=100,PageHeight=100,Margins=(10,10,10,10)});tableFlow.Y=89;tableFlow.AddTable(new PdfTable(1).Header("HEAD").Row("BODY"));tableFlow.Finish();var tableBytes=tableBuilder.ToArray();File.WriteAllBytes(Path.Combine(output,"table-near-bottom.pdf"),tableBytes);results["tableNearBottom"]=PdfDocument.Open(tableBytes).Pages.Select(p=>new{p.Number,Text=p.Text,Letters=p.Letters.Select(l=>new{l.Value,l.Y})}).ToArray();
var supplementary=symbolFont.CharacterMap.First(kv=>kv.Key>0xffff && kv.Value>0).Key;
var supplementaryText=char.ConvertFromUtf32(supplementary);
object ConvertSymbol(double contentWidth) {var d=new WordDocument();d.Sections[0].Page=new WordPageSetup { Width=72,Height=150,MarginLeft=1,MarginRight=71-contentWidth,MarginTop=1,MarginBottom=1,HeaderDistance=0,FooterDistance=0 };d.AddParagraph().AddText(supplementaryText,new WordRunProperties {Font="Noto Sans Symbols 2",FontSize=12});var converted=WordToPdf.Convert(d);File.WriteAllBytes(Path.Combine(output,contentWidth>10?"symbol-wide.pdf":"symbol-narrow.pdf"),converted.Pdf);return new {ContentWidth=contentWidth,Text=string.Join("",PdfDocument.Open(converted.Pdf).Pages.Select(p=>p.Text)),converted.Gaps};}
results["surrogateWrap"] = new {CodePoint=supplementary,Source=supplementaryText,HasGlyph=symbolFont.HasGlyph(supplementary),Wide=ConvertSymbol(40),Narrow=ConvertSymbol(Math.Max(1,symbolFont.MeasureText(supplementaryText,12)/2))};
var columnDoc=new WordDocument();
var twoColumns=new WordPageSetup { Width=200,Height=200,MarginLeft=10,MarginRight=10,MarginTop=10,MarginBottom=10,HeaderDistance=0,FooterDistance=0,Columns=2,ColumnSpacing=10 };
columnDoc.Sections[0].Page=twoColumns;columnDoc.AddParagraph("AAAA");columnDoc.AddSection(twoColumns with {Start=WordSectionStart.NextColumn});columnDoc.AddParagraph("BBBB");
var columnPdf=WordToPdf.Convert(columnDoc);File.WriteAllBytes(Path.Combine(output,"next-column.pdf"),columnPdf.Pdf);var columnPage=PdfDocument.Open(columnPdf.Pdf).GetPage(1);var aLetter=columnPage.Letters.First(l=>l.Value=="A");var bLetter=columnPage.Letters.First(l=>l.Value=="B");
results["nextColumnSection"] = new {columnPdf.PageCount,FirstX=aLetter.X,FirstY=aLetter.Y,SecondX=bLetter.X,SecondY=bLetter.Y,ExpectedSecondColumnLeft=105,columnPdf.Gaps};
using (var csvStream = new MemoryStream("a,b\n1,2\n"u8.ToArray())) { await using var reader=await CsvReader.OpenAsync(csvStream,leaveOpen:true); results["asyncCsv"]=new {Read=await reader.ReadAsync(),Fields=reader.GetFields()}; }
foreach(var (name,save) in new (string,Action<Stream>)[]{("word",doc.Save),("excel",workbook.Save),("pdf",builder.Save)}) { using var stream=new MemoryStream(); save(stream); results[$"save-stream-{name}"]=new {stream.CanWrite,stream.Length}; }
var assemblies=new[]{typeof(WordDocument).Assembly,typeof(OmniEurope.Documents.Fonts.DocumentFonts).Assembly};
var inventory = assemblies.SelectMany(a=>a.GetExportedTypes()).OrderBy(t=>t.FullName).Select(t=> new {Type=t.FullName,Members=t.GetMembers(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Where(m=>m is not MethodInfo mi || !mi.IsSpecialName).Select(m=>m.ToString()).ToArray(),Callables=t.GetMembers(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).OfType<MethodBase>().Where(m=>m is ConstructorInfo || !m.IsSpecialName).Select(m=>new {m.Name,Parameters=m.GetParameters().Select(p=>new {p.Name,Type=p.ParameterType.ToString(),p.IsOptional,Default=p.HasDefaultValue?p.DefaultValue?.ToString():null}).ToArray()}).ToArray()}).ToArray();
File.WriteAllText(Path.Combine(output,"public-api.json"),JsonSerializer.Serialize(inventory,new JsonSerializerOptions{WriteIndented=true}));
results["publicApi"]=new {TypeCount=inventory.Length,assemblies=assemblies.Select(a=>new {a.FullName,a.Location}).ToArray()};
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
