// SPDX-License-Identifier: EUPL-1.2
using System.IO.Compression;
using System.Xml.Linq;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Tests.Word;
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Reading;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// Freehand ink of Word 2010 (<c>w14:contentPart</c> naming an InkML part, with a VML picture as fallback): its strokes
/// are decoded from the compact InkML text, placed in the anchor's box, drawn as vector strokes in the PDF and written
/// back on save; without the InkML part the fallback picture is drawn, and only without both is the ink reported. The
/// pages are rendered at 72 dpi (one pixel per point) and the pixels are read.
/// </summary>
public sealed class WordInkTests
{
    private const string InkContentType = "application/inkml+xml";
    private const string Rels = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";

    [Fact]
    public void The_compact_text_form_is_decoded_with_its_prefixes_units_and_brush()
    {
        var root = XElement.Parse(Ink(
            """
            <inkml:definitions>
            <inkml:context xml:id="ctx0"><inkml:inkSource xml:id="s"><inkml:traceFormat><inkml:channel name="X" type="integer" units="cm"/><inkml:channel name="Y" type="integer" units="cm"/><inkml:channel name="F" type="integer" units="dev"/></inkml:traceFormat>
            <inkml:channelProperties><inkml:channelProperty channel="X" name="resolution" value="1000" units="1/cm"/><inkml:channelProperty channel="Y" name="resolution" value="1000" units="1/cm"/></inkml:channelProperties></inkml:inkSource></inkml:context>
            <inkml:context xml:id="ctx1"><inkml:inkSource xml:id="t"><inkml:traceFormat><inkml:channel name="X" type="decimal" units="mm"/><inkml:channel name="Y" type="decimal" units="mm"/></inkml:traceFormat></inkml:inkSource></inkml:context>
            <inkml:brush xml:id="br0"><inkml:brushProperty name="width" value="0.05" units="cm"/><inkml:brushProperty name="height" value="0.05" units="cm"/></inkml:brush>
            <inkml:brush xml:id="br1"><inkml:brushProperty name="width" value="2" units="mm"/><inkml:brushProperty name="color" value="#FF8000"/><inkml:brushProperty name="transparency" value="191"/></inkml:brush>
            </inkml:definitions>
            <inkml:trace contextRef="#ctx0" brushRef="#br0">4063 3582 24575,'225'2'0,"275"-5"0,-445 2 0,0-2 0</inkml:trace>
            <inkml:trace contextRef="#ctx1" brushRef="#br1">10 20,!15.5-4, 2 3</inkml:trace>
            <inkml:trace type="penUp" contextRef="#ctx0">1 1,2 2</inkml:trace>
            """));

        var traces = InkMlReader.Read(root);

        Assert.Equal(2, traces.Count);
        // Explicit first point, then a first difference (225, 2), then second differences (velocity 225+275 = 500, then 55; 2-5 = -3, then -1).
        Assert.Equal(
            [(4.063, 3.582), (4.288, 3.584), (4.788, 3.581), (4.843, 3.580), (4.898, 3.577)],
            traces[0].Points.Select(p => (Math.Round(p.X, 3), Math.Round(p.Y, 3))));
        Assert.Equal((0.05, "000000", 1.0), (traces[0].Width, traces[0].Color, traces[0].Opacity));
        // Millimetres without a resolution; an explicit negative value follows a blank-free sign; the brush has its own width, colour and transparency.
        Assert.Equal([(1.0, 2.0), (1.55, -0.4), (0.2, 0.3)], traces[1].Points.Select(p => (Math.Round(p.X, 3), Math.Round(p.Y, 3))));
        Assert.Equal((0.2, "FF8000"), (Math.Round(traces[1].Width, 3), traces[1].Color));
        Assert.Equal(1 - (191 / 255.0), traces[1].Opacity, 6);
    }

    [Fact]
    public void Ink_strokes_are_drawn_as_vector_strokes_in_the_box_of_the_anchor()
    {
        var docx = Package(Anchor(), SyntheticInk, withInkPart: true);
        var document = WordDocument.Load(docx);
        var ink = Assert.IsType<WordInk>(document.Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordShape>().Single());
        var result = WordToPdf.Convert(docx);
        var image = Render(result.Pdf);

        Assert.DoesNotContain(result.Gaps, gap => gap.Contains("skipped", StringComparison.Ordinal));
        Assert.Equal((100.0, 50.0, 3), (ink.Width, ink.Height, ink.Strokes.Count));
        Assert.Equal(("page", 100.0, "page", 200.0), (ink.Floating!.HorizontalRelativeTo, ink.Floating.HorizontalOffset, ink.Floating.VerticalRelativeTo, ink.Floating.VerticalOffset));
        // The box measures 100 x 50 pt at (100, 200): the black diagonal crosses its centre, the red base line lies along its bottom edge,
        // the one-point trace is a dot; nothing leaves the box.
        Assert.True(Painted(image, 148, 223, 153, 228) > 4, "the diagonal is missing");
        Assert.True(Painted(image, 148, 210, 153, 215) > 4, "the dot is missing");
        var (r, g, b, _) = image.GetRgba(150, 250);
        Assert.True(r > 200 && g < 90 && b < 90, $"the red base line is missing ({r},{g},{b})");
        Assert.Equal(0, Painted(image, 0, 0, 595, 195) + Painted(image, 0, 256, 595, 842) + Painted(image, 0, 195, 95, 256) + Painted(image, 205, 195, 595, 256));
    }

    [Fact]
    public void Ink_is_written_back_and_drawn_the_same_after_saving()
    {
        var docx = Package(Anchor(), SyntheticInk, withInkPart: true);
        var document = WordDocument.Load(docx);
        var saved = document.ToArray();
        var reread = Assert.IsType<WordInk>(WordDocument.Load(saved).Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordShape>().Single());
        var original = Assert.IsType<WordInk>(document.Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordShape>().Single());

        var entries = DocxFactory.Entries(saved);
        Assert.Contains("word/ink/ink1.xml", entries.Keys);
        Assert.Contains(InkContentType, System.Text.Encoding.UTF8.GetString(entries["[Content_Types].xml"]));
        Assert.Equal(original.Strokes.Count, reread.Strokes.Count);
        Assert.Equal((original.Width, original.Height, original.Floating), (reread.Width, reread.Height, reread.Floating));
        for (var i = 0; i < original.Strokes.Count; i++)
        {
            Assert.Equal((original.Strokes[i].Color, Math.Round(original.Strokes[i].Width, 1)), (reread.Strokes[i].Color, Math.Round(reread.Strokes[i].Width, 1)));
            Assert.Equal(original.Strokes[i].Points.Count, reread.Strokes[i].Points.Count);
            Assert.All(original.Strokes[i].Points.Zip(reread.Strokes[i].Points), pair => Assert.True(Math.Abs(pair.First.X - pair.Second.X) < 0.02 && Math.Abs(pair.First.Y - pair.Second.Y) < 0.02));
        }

        var (before, after) = (Render(WordToPdf.Convert(docx).Pdf), Render(WordToPdf.Convert(saved).Pdf));
        double Dark(RasterImage page) => Painted(page, 90, 190, 210, 260);
        Assert.True(Dark(before) > 100, "the ink is not drawn");
        Assert.InRange(Dark(after), Dark(before) * 0.98, Dark(before) * 1.02);
    }

    [Fact]
    public void A_translucent_pen_and_a_programmatic_ink_round_trip()
    {
        var document = new WordDocument(language: null);
        var ink = new WordInk(100, 50) { Floating = new WordFloatingPosition(100, "page", 200, "page", WordWrap.None) };
        ink.Strokes.Add(new WordInkStroke([(0, 25), (100, 25)], 10, "00FF00", 0.5));
        document.Body.Add(new WordParagraph().Add(ink));

        var reread = Assert.IsType<WordInk>(WordDocument.Load(document.ToArray()).Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordShape>().Single());
        var image = Render(WordToPdf.Convert(document).Pdf);

        var stroke = Assert.Single(reread.Strokes);
        Assert.Equal(("00FF00", 0.5), (stroke.Color, Math.Round(stroke.Opacity, 1)));
        Assert.InRange(stroke.Width, 9.9, 10.1);
        // A half-transparent green over the white page is a light green.
        var (r, g, b, _) = image.GetRgba(150, 225);
        Assert.True(r is > 100 and < 160 && g > 240 && b is > 100 and < 160, $"unexpected colour ({r},{g},{b})");
    }

    [Fact]
    public void The_fallback_picture_is_drawn_when_the_ink_part_is_missing_or_unreadable()
    {
        foreach (var inkPart in new[] { null, "<not-xml" })
        {
            var docx = Package(Anchor(), inkPart, withInkPart: inkPart is not null);
            var result = WordToPdf.Convert(docx);
            var picture = Assert.IsType<WordPicture>(WordDocument.Load(docx).Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordShape>().Single());
            var (r, g, b, _) = Render(result.Pdf).GetRgba(150, 225);

            Assert.DoesNotContain(result.Gaps, gap => gap.Contains("skipped", StringComparison.Ordinal));
            Assert.Equal((100.0, 50.0, "page"), (picture.Width, picture.Height, picture.Floating!.HorizontalRelativeTo));
            Assert.True(r > 200 && g < 60 && b < 60, $"the fallback picture is not drawn ({r},{g},{b})");
        }
    }

    [Fact]
    public void Ink_without_strokes_and_without_fallback_is_reported_and_drawn_as_nothing()
    {
        var docx = Package(Anchor(fallback: false), null, withInkPart: false);

        var result = WordToPdf.Convert(docx);

        Assert.Contains("ink skipped", result.Gaps);
        Assert.Equal(0, Painted(Render(result.Pdf), 0, 150, 595, 300));
    }

    [Fact]
    public void The_reference_template_ink_is_drawn_where_Word_365_draws_it()
    {
        var docx = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Word", "rent-receipt-template.docx"));
        var reference = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Word", "rent-receipt-template.word365.pdf"));
        var result = WordToPdf.Convert(docx);
        var ours = Render(result.Pdf);
        var theirs = Render(reference);

        Assert.DoesNotContain(result.Gaps, gap => gap.Contains("skipped", StringComparison.Ordinal));
        // Below the text of the page only the ink remains: every dark pixel of Word's page has one of ours within 2 pt, and conversely.
        var (missed, extra, darkTheirs, darkOurs) = (0, 0, 0, 0);
        for (var y = 480; y < 700; y++)
        {
            for (var x = 0; x < 595; x++)
            {
                var (ourDark, theirDark) = (IsDark(ours, x, y), IsDark(theirs, x, y));
                darkTheirs += theirDark ? 1 : 0;
                darkOurs += ourDark ? 1 : 0;
                missed += theirDark && !NearDark(ours, x, y) ? 1 : 0;
                extra += ourDark && !NearDark(theirs, x, y) ? 1 : 0;
            }
        }

        Assert.True(darkTheirs > 800, "the reference shows no ink");
        Assert.True(missed < darkTheirs * 0.02, $"{missed} of {darkTheirs} dark pixels of Word's ink are not in ours");
        Assert.True(extra < darkOurs * 0.02, $"{extra} of {darkOurs} dark pixels of ours are not in Word's ink");
    }

    private const string SyntheticInk =
        """
        <inkml:ink xmlns:inkml="http://www.w3.org/2003/InkML"><inkml:definitions>
        <inkml:context xml:id="ctx0"><inkml:inkSource xml:id="s"><inkml:traceFormat><inkml:channel name="X" type="integer" units="cm"/><inkml:channel name="Y" type="integer" units="cm"/></inkml:traceFormat>
        <inkml:channelProperties><inkml:channelProperty channel="X" name="resolution" value="1000" units="1/cm"/><inkml:channelProperty channel="Y" name="resolution" value="1000" units="1/cm"/></inkml:channelProperties></inkml:inkSource></inkml:context>
        <inkml:brush xml:id="br0"><inkml:brushProperty name="width" value="0.1" units="cm"/><inkml:brushProperty name="height" value="0.1" units="cm"/></inkml:brush>
        <inkml:brush xml:id="br1"><inkml:brushProperty name="width" value="0.2" units="cm"/><inkml:brushProperty name="height" value="0.2" units="cm"/><inkml:brushProperty name="color" value="#FF0000"/></inkml:brush>
        </inkml:definitions>
        <inkml:trace contextRef="#ctx0" brushRef="#br0">0 0,1764 882,3528 1764</inkml:trace>
        <inkml:trace contextRef="#ctx0" brushRef="#br1">0 1764,'3528'0</inkml:trace>
        <inkml:trace contextRef="#ctx0" brushRef="#br0">1764 441</inkml:trace>
        </inkml:ink>
        """;

    private static string Ink(string content) => $"""<inkml:ink xmlns:inkml="http://www.w3.org/2003/InkML">{content}</inkml:ink>""";

    // A 100 x 50 pt drawing at (100, 200) from the page corner, over the text, with a fallback picture of the same box.
    private static string Anchor(bool fallback = true) =>
        $"""
        <w:p><w:r><mc:AlternateContent xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:w14="http://schemas.microsoft.com/office/word/2010/wordml" xmlns:wpi="http://schemas.microsoft.com/office/word/2010/wordprocessingInk" xmlns:v="urn:schemas-microsoft-com:vml" xmlns:o="urn:schemas-microsoft-com:office:office">
        <mc:Choice Requires="wpi"><w:drawing><wp:anchor distT="0" distB="0" distL="0" distR="0" simplePos="0" relativeHeight="1" behindDoc="0" locked="0" layoutInCell="1" allowOverlap="1"><wp:simplePos x="0" y="0"/><wp:positionH relativeFrom="page"><wp:posOffset>1270000</wp:posOffset></wp:positionH><wp:positionV relativeFrom="page"><wp:posOffset>2540000</wp:posOffset></wp:positionV><wp:extent cx="1270000" cy="635000"/><wp:effectExtent l="57150" t="57150" r="52705" b="52705"/><wp:wrapNone/><wp:docPr id="1" name="Ink 1"/><wp:cNvGraphicFramePr/><a:graphic><a:graphicData uri="http://schemas.microsoft.com/office/word/2010/wordprocessingInk"><w14:contentPart bwMode="auto" r:id="rId5"><w14:nvContentPartPr><w14:cNvContentPartPr/></w14:nvContentPartPr><w14:xfrm><a:off x="0" y="0"/><a:ext cx="1270000" cy="635000"/></w14:xfrm></w14:contentPart></a:graphicData></a:graphic></wp:anchor></w:drawing></mc:Choice>
        {(fallback ? """<mc:Fallback><w:pict><v:shape style="position:absolute;margin-left:100pt;margin-top:200pt;width:100pt;height:50pt;mso-position-horizontal-relative:page;mso-position-vertical-relative:page"><v:imagedata r:id="rId6" o:title=""/></v:shape></w:pict></mc:Fallback>""" : string.Empty)}
        </mc:AlternateContent></w:r></w:p>
        """;

    private static byte[] Package(string body, string? inkPart, bool withInkPart)
    {
        var parts = withInkPart ? new Dictionary<string, string> { ["word/ink/ink1.xml"] = inkPart! } : [];
        var relationships = $"""<Relationship Id="rId5" Type="{Rels}customXml" Target="ink/ink1.xml"/><Relationship Id="rId6" Type="{Rels}image" Target="media/image1.png"/>""";
        return DocxFactory.Build(
            body,
            extraParts: parts,
            documentRelationships: relationships,
            binaryParts: new Dictionary<string, byte[]> { ["word/media/image1.png"] = Samples.Png(20, 10, 230, 20, 20) },
            contentTypes: new Dictionary<string, string> { ["word/ink/ink1.xml"] = InkContentType });
    }

    private static RasterImage Render(byte[] pdf) => PdfRenderer.Render(PdfDocument.Open(pdf).GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image;

    private static bool IsDark(RasterImage image, int x, int y)
    {
        var (r, g, b, _) = image.GetRgba(x, y);
        return r < 128 && g < 128 && b < 128;
    }

    private static bool NearDark(RasterImage image, int x, int y)
    {
        for (var dy = -2; dy <= 2; dy++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                if (x + dx >= 0 && y + dy >= 0 && x + dx < image.Width && y + dy < image.Height && IsDark(image, x + dx, y + dy))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // The number of non-white pixels in the rectangle (from the page's top-left corner).
    private static int Painted(RasterImage image, double left, double top, double right, double bottom)
    {
        var count = 0;
        for (var y = (int)top; y < (int)bottom; y++)
        {
            for (var x = (int)left; x < (int)right; x++)
            {
                var (r, g, b, _) = image.GetRgba(x, y);
                count += r < 245 || g < 245 || b < 245 ? 1 : 0;
            }
        }

        return count;
    }
}
